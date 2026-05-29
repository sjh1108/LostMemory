using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Multiplayer
{
    /// <summary>
    /// LostMemory 자체 Relay 용 NGO NetworkTransport 참조 구현.
    ///
    /// 사용 흐름:
    ///   1. Sessions API 로 세션 생성/조인 → sessionToken + myUserId 받음
    ///   2. NetworkManager 의 Transport 자리에 본 컴포넌트 부착, Inspector 에 relayHost/relayPort 입력
    ///   3. SetSession(sessionToken, myUserId) 호출
    ///   4. NetworkManager.StartHost() (호스트) 또는 StartClient() (게스트) 호출
    ///   5. NGO 가 Initialize → Start* → Send/PollEvent 순으로 호출
    ///
    /// 프로토콜 (NGO 측 wire 포맷):
    ///   [MAGIC_DATA=0x01][senderUserId : 8 bytes BE][targetUserId : 8 bytes BE][NGO payload bytes]
    ///
    ///   targetUserId 는 NGO 가 Send(clientId, ...) 호출 시 의도한 unicast 대상.
    ///   Relay 가 라우팅 안 하고 broadcast 만 하더라도 받는 측이 본인 대상 아니면 drop 하므로
    ///   N×N 중복 spawn (같은 NetworkObjectId 두 번 받는 NGO 에러) 회피.
    ///
    /// Relay 핸드셰이크 (텍스트 JSON):
    ///   클라 → Relay : {"type":"HELLO","token":"&lt;sessionToken&gt;"}
    ///   Relay → 클라 : {"type":"ACK","ok":true,"sessionId":N,"role":"HOST|GUEST"}
    ///
    /// 한계 / 후속 개선 포인트 (클라 담당에게 전달용):
    ///   - 클라 측 targetUserId drop 으로 NGO 중복 spawn 에러는 해결됐지만 Relay 단에서는
    ///     여전히 broadcast — 대역폭 N×N 그대로. Relay 서버가 wire 의 targetUserId 헤더 읽어
    ///     unicast forward 하도록 보완 예정 (서버 측 라우팅).
    ///   - Disconnect 감지는 timeout 기반이 아니라 명시적 Shutdown 시에만. 다른 peer 가 조용히 떠나면 미감지.
    ///   - clientId 매핑은 서버(호스트)·클라 양쪽 모두 senderUserId 그대로 사용.
    ///     단 ServerClientId 는 NGO 관례상 0 으로 매핑 (게스트 입장에서 호스트는 항상 0).
    ///   - reliability/ordering 보장 X (UDP 위 raw forwarding). NGO 의 NetworkVariable 등 신뢰성 필요한 메시지는
    ///     상위 레이어(Mirror/NGO)의 reliable channel 로 알아서 처리됨.
    /// </summary>
    [DisallowMultipleComponent]
    public class LostMemoryRelayTransport : NetworkTransport
    {
        // ================================================================
        // Inspector 설정값
        // ================================================================

        [Header("Relay Server")]
        [Tooltip("자체 Relay 서버 주소. dev: localhost, prod: k14c201.p.ssafy.io")]
        [SerializeField] private string relayHost = "localhost";

        [Tooltip("자체 Relay UDP 포트. 기본 7777")]
        [SerializeField] private int relayPort = 7777;

        [Header("Behavior")]
        [Tooltip("Relay 핸드셰이크 ACK 대기 timeout (초). 초과 시 StartHost/Client 실패")]
        [SerializeField] private float handshakeTimeoutSeconds = 5f;

        [Tooltip("디버그 로그 출력 여부")]
        [SerializeField] private bool verboseLog = false;

        [Tooltip("Editor 진입 시 relayHost 를 localhost 로 자동 강제. 끄면 Inspector 값 사용 (prod EC2 직접 디버깅 등).")]
        [SerializeField] private bool editorAutoOverride = true;

        // ================================================================
        // 런타임 상태
        // ================================================================

        private const byte MAGIC_DATA = 0x01;
        private const ulong NGO_SERVER_CLIENT_ID = 0UL;

        private string _sessionToken;
        private ulong _myUserId;
        private bool _sessionConfigured;

        private UdpClient _udp;
        private IPEndPoint _relayEndpoint;
        private Thread _receiveThread;
        private volatile bool _running;

        // keep-alive — 호스트가 게스트 합류 전 NGO 데이터 송신 0 인 동안 백엔드 idle timeout 방지
        private Thread _keepAliveThread;
        private static readonly byte[] _pingPacket = Encoding.UTF8.GetBytes("{\"type\":\"PING\"}");

        // 핸드셰이크 ACK 대기용
        private readonly ManualResetEventSlim _ackEvent = new(false);
        private volatile bool _ackOk;
        private string _ackFailReason;

        // 백그라운드 receive → 메인 스레드 PollEvent 로 이벤트 전달
        private readonly ConcurrentQueue<TransportEvent> _eventQueue = new();

        // 호스트 입장에서 알려진 게스트 userId 집합. 같은 게스트로부터 첫 패킷 도착 시 Connect 이벤트 1회 emit
        private readonly ConcurrentDictionary<ulong, byte> _knownRemoteUserIds = new();

        // 호스트 본인을 "host" 로 추적 (게스트 transport 입장에서 호스트는 ServerClientId=0 으로 보임)
        private bool _isHost;
        private ulong _hostUserId; // 호스트 본인은 자기 userId, 게스트는 SetHostUserId() 로 받음 (옵션)

        // 백그라운드 ReceiveLoop 에서도 안전한 시간 측정 (Time.realtimeSinceStartup 은 메인 스레드 전용)
        private static readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();


        // ================================================================
        // 외부에서 호출하는 셋업 메서드
        // ================================================================

        /// <summary>
        /// Sessions API 응답으로 받은 sessionToken 과 본인 userId 를 transport 에 주입.
        /// StartHost / StartClient 전에 반드시 호출.
        /// </summary>
        public void SetSession(string sessionToken, ulong myUserId)
        {
            _sessionToken = sessionToken;
            _myUserId = myUserId;
            _sessionConfigured = true;
            if (verboseLog) Debug.Log($"[Relay] Session set. myUserId={myUserId}");
        }

        /// <summary>
        /// (게스트 전용) 게스트 입장에서 호스트의 userId 를 알려준다 (Sessions 응답 members[role=HOST] 에서 추출).
        /// 호스트의 senderUserId 가 들어오면 NGO 의 ServerClientId(=0) 로 매핑하기 위함.
        /// </summary>
        public void SetHostUserId(ulong hostUserId)
        {
            _hostUserId = hostUserId;
        }

        // ================================================================
        // NGO NetworkTransport 추상 멤버 구현
        // ================================================================

        public override ulong ServerClientId => NGO_SERVER_CLIENT_ID;

#if UNITY_EDITOR
        /// <summary>
        /// Editor 진입 시 dev relay (localhost) 강제. prefab 의 Inspector 값은 prod 유지하되
        /// Editor 에서만 자동 우회 — 빌드본은 Inspector 값 그대로 사용.
        ///
        /// `editorAutoOverride` 체크박스 끄면 우회 skip — Editor 에서 prod EC2 향해 직접 디버깅
        /// (prod relay 패킷 캡쳐, prod 백엔드 연동 테스트 등) 가능.
        /// </summary>
        private void Awake()
        {
            if (!editorAutoOverride) return;

            const string editorRelayHost = "localhost";
            if (relayHost != editorRelayHost)
            {
                Debug.Log($"[RelayTransport] Editor override: relayHost {relayHost} → {editorRelayHost}");
                relayHost = editorRelayHost;
            }
        }
#endif

        public override void Initialize(NetworkManager networkManager = null)
        {
            // 의존성 0. 별도 init 작업 불필요
        }

        public override bool StartServer()
        {
            _isHost = true;
            _hostUserId = _myUserId; // 호스트 본인이 호스트
            return StartUdpAndHandshake("Server");
        }

        public override bool StartClient()
        {
            _isHost = false;
            return StartUdpAndHandshake("Client");
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            if (!_running) return;

            // NGO clientId → 실제 targetUserId 변환
            //   호스트: clientId 는 게스트의 senderUserId 그대로 (HandleDataPacket 매핑 참고)
            //   게스트: clientId == NGO_SERVER_CLIENT_ID(0) → _hostUserId, 그 외 (peer 게스트) clientId 그대로
            ulong targetUserId;
            if (_isHost)
            {
                targetUserId = clientId;
            }
            else
            {
                targetUserId = (clientId == NGO_SERVER_CLIENT_ID) ? _hostUserId : clientId;
            }

            if (targetUserId == 0)
            {
                if (verboseLog) Debug.LogWarning($"[Relay] Send 대상 미해소 (clientId={clientId}, isHost={_isHost}, hostUserId={_hostUserId}). 패킷 drop.");
                return;
            }

            if (verboseLog && payload.Count + 17 > 1400)
            {
                Debug.LogWarning($"[Relay] 큰 메시지 송신: payload={payload.Count}B, total={payload.Count + 17}B (UDP MTU 위험)");
            }

            // wire frame: [MAGIC_DATA][senderUserId 8B BE][targetUserId 8B BE][payload]
            byte[] wire = new byte[1 + 8 + 8 + payload.Count];
            wire[0] = MAGIC_DATA;
            WriteUInt64BE(wire, 1, _myUserId);
            WriteUInt64BE(wire, 9, targetUserId);
            Buffer.BlockCopy(payload.Array!, payload.Offset, wire, 17, payload.Count);

            try
            {
                _udp.Send(wire, wire.Length);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Relay] Send 실패 (clientId={clientId}): {e.Message}");
            }

            // NetworkDelivery 무시 (UDP raw). reliability 는 상위 NGO layer 가 보장.
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            if (_eventQueue.TryDequeue(out var evt))
            {
                clientId = evt.ClientId;
                payload = evt.Payload;
                receiveTime = evt.ReceiveTime;
                return evt.Type;
            }

            clientId = 0;
            payload = default;
            receiveTime = 0f;
            return NetworkEvent.Nothing;
        }

        public override void DisconnectLocalClient()
        {
            ShutdownInternal("DisconnectLocalClient");
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            // Relay 가 명시적 연결 추적 안 하므로 wire-level 신호 X.
            // 호스트가 특정 게스트를 "끊었다" 로 처리할 때는 이 transport 가 해당 clientId 무시하는 식.
            _knownRemoteUserIds.TryRemove(clientId, out _);
            EnqueueEvent(NetworkEvent.Disconnect, clientId, default);
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            // Relay 가 RTT 측정 인프라 미제공. 0 반환 (NGO 가 RTT 기반 결정 안 한다고 가정)
            return 0UL;
        }

        public override void Shutdown()
        {
            ShutdownInternal("Shutdown");
        }

        // ================================================================
        // 내부 — UDP 시작·핸드셰이크
        // ================================================================

        private bool StartUdpAndHandshake(string roleLabel)
        {
            if (!_sessionConfigured)
            {
                Debug.LogError("[Relay] SetSession() 호출 누락. sessionToken/myUserId 가 없음");
                return false;
            }

            try
            {
                _udp = new UdpClient(0); // 0 = 임의의 ephemeral 포트 자동 할당
                IPAddress addr = ResolveAddress(relayHost);
                _relayEndpoint = new IPEndPoint(addr, relayPort);
                _udp.Connect(_relayEndpoint); // UDP "connect" — sender/receiver 고정 (multi-peer 송신은 안 하지만 보내기 편의)

                _running = true;
                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "LostMemoryRelay-Recv" };
                _receiveThread.Start();

                // 핸드셰이크 패킷 송신
                SendHelloHandshake();

                // ACK 대기
                bool acked = _ackEvent.Wait(TimeSpan.FromSeconds(handshakeTimeoutSeconds));
                if (!acked)
                {
                    Debug.LogError($"[Relay] {roleLabel} 핸드셰이크 ACK 대기 timeout ({handshakeTimeoutSeconds}s)");
                    ShutdownInternal("handshake-timeout");
                    return false;
                }
                if (!_ackOk)
                {
                    Debug.LogError($"[Relay] {roleLabel} 핸드셰이크 거절: {_ackFailReason}");
                    ShutdownInternal("handshake-rejected");
                    return false;
                }

                // 자기 자신 connect 이벤트 emit — 게스트 입장에서만 필요.
                // 호스트는 NGO 가 server+self-client 를 자체 묶어 처리하므로 emit 시 중복 경고 발생.
                if (!_isHost)
                {
                    EnqueueEvent(NetworkEvent.Connect, NGO_SERVER_CLIENT_ID, default);
                }

                // keep-alive 스레드 시작 — Relay 의 idle timeout 방지
                _keepAliveThread = new Thread(KeepAliveLoop) { IsBackground = true, Name = "LostMemoryRelay-KeepAlive" };
                _keepAliveThread.Start();

                if (verboseLog) Debug.Log($"[Relay] {roleLabel} 핸드셰이크 OK. myUserId={_myUserId}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Relay] {roleLabel} 시작 실패: {e}");
                ShutdownInternal("start-exception");
                return false;
            }
        }

        /// <summary>
        /// 1초 간격으로 Relay 에 PING 송신. RoomRegistry.lastSeenAt 갱신용.
        /// 호스트가 게스트 합류 전까진 NGO 자체 데이터 송신이 0 이라 백엔드 idle timeout 에 걸림.
        /// </summary>
        private void KeepAliveLoop()
        {
            while (_running)
            {
                try
                {
                    Thread.Sleep(1000);
                    if (!_running) break;
                    _udp?.Send(_pingPacket, _pingPacket.Length);
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) when (!_running) { return; }
                catch (Exception e)
                {
                    if (verboseLog) Debug.LogWarning($"[Relay] keep-alive 송신 예외: {e.Message}");
                }
            }
        }

        private void SendHelloHandshake()
        {
            string helloJson = $"{{\"type\":\"HELLO\",\"token\":\"{_sessionToken}\"}}";
            byte[] bytes = Encoding.UTF8.GetBytes(helloJson);
            _udp.Send(bytes, bytes.Length);
        }

        private static IPAddress ResolveAddress(string host)
        {
            if (IPAddress.TryParse(host, out var ip)) return ip;
            var entries = Dns.GetHostAddresses(host);
            foreach (var a in entries)
                if (a.AddressFamily == AddressFamily.InterNetwork)
                    return a;
            return entries[0];
        }

        // ================================================================
        // 내부 — 백그라운드 수신 루프
        // ================================================================

        private void ReceiveLoop()
        {
            IPEndPoint anyEp = new(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    byte[] data = _udp.Receive(ref anyEp);
                    if (data == null || data.Length == 0) continue;

                    if (data[0] == MAGIC_DATA)
                    {
                        HandleDataPacket(data);
                    }
                    else
                    {
                        HandleControlMessage(data);
                    }
                }
                catch (SocketException) when (!_running)
                {
                    // shutdown 중 정상 종료
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Relay] 수신 루프 예외: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 컨트롤 메시지 (JSON) 디스패치. type 별 처리:
        ///  - ACK: 핸드셰이크 응답
        ///  - PEER_LEFT: 같은 세션의 다른 peer 가 떠남 → NGO Disconnect 이벤트 emit
        ///  - 그 외: 무시
        /// </summary>
        private void HandleControlMessage(byte[] data)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                JObject obj = JObject.Parse(json);
                string type = obj.Value<string>("type");

                if (type == "ACK")
                {
                    HandleAck(obj);
                }
                else if (type == "PEER_LEFT")
                {
                    HandlePeerLeft(obj);
                }
                // 그 외 type 은 무시
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Relay] 컨트롤 메시지 파싱 실패: {e.Message}");
            }
        }

        private void HandleAck(JObject obj)
        {
            try
            {
                _ackOk = obj.Value<bool>("ok");
                if (!_ackOk)
                {
                    _ackFailReason = obj.Value<string>("reason");
                }
            }
            finally
            {
                _ackEvent.Set();
            }
        }

        private void HandlePeerLeft(JObject obj)
        {
            ulong leftUserId = obj.Value<ulong>("userId");
            if (verboseLog) Debug.Log($"[Relay] PEER_LEFT 수신: userId={leftUserId}");

            _knownRemoteUserIds.TryRemove(leftUserId, out _);

            ulong ngoClientId = MapSenderToClientId(leftUserId);
            EnqueueEvent(NetworkEvent.Disconnect, ngoClientId, default);
        }

        private void HandleDataPacket(byte[] data)
        {
            if (data.Length < 1 + 8 + 8) return; // MAGIC + sender + target 최소

            ulong senderUserId = ReadUInt64BE(data, 1);
            ulong targetUserId = ReadUInt64BE(data, 9);
            int payloadOffset = 1 + 8 + 8;
            int payloadLength = data.Length - payloadOffset;

            // 본인 송신이 Relay 에 의해 echo 되는 경우 안전 가드
            if (senderUserId == _myUserId) return;

            // 본인 대상 아니면 drop — N×N broadcast 환경에서 다른 peer 대상 메시지가
            // 본 클라까지 도달하는 케이스 차단. NGO 의 같은 NetworkObjectId 중복 spawn 에러 회피.
            if (targetUserId != _myUserId) return;

            // sender → NGO clientId 매핑
            ulong ngoClientId = MapSenderToClientId(senderUserId);

            // 처음 보는 게스트면 Connect 이벤트 먼저 emit (호스트 입장)
            if (_isHost && _knownRemoteUserIds.TryAdd(senderUserId, 0))
            {
                EnqueueEvent(NetworkEvent.Connect, ngoClientId, default);
            }

            byte[] payload = new byte[payloadLength];
            Buffer.BlockCopy(data, payloadOffset, payload, 0, payloadLength);
            EnqueueEvent(NetworkEvent.Data, ngoClientId, new ArraySegment<byte>(payload));
        }

        /// <summary>
        /// senderUserId → NGO clientId 매핑.
        /// - 호스트 입장: 게스트의 senderUserId 그대로 사용 (NGO 가 game 측에서 user 식별 가능)
        /// - 게스트 입장: senderUserId 가 호스트(_hostUserId) 와 일치하면 ServerClientId(=0) 으로,
        ///                다른 게스트면 그 senderUserId 그대로 (peer-to-peer 통신용)
        /// </summary>
        private ulong MapSenderToClientId(ulong senderUserId)
        {
            if (!_isHost && _hostUserId != 0 && senderUserId == _hostUserId)
            {
                return NGO_SERVER_CLIENT_ID;
            }
            return senderUserId;
        }

        // ================================================================
        // 내부 — 이벤트 큐
        // ================================================================

        private void EnqueueEvent(NetworkEvent type, ulong clientId, ArraySegment<byte> payload)
        {
            _eventQueue.Enqueue(new TransportEvent
            {
                Type = type,
                ClientId = clientId,
                Payload = payload,
                ReceiveTime = (float)_clock.Elapsed.TotalSeconds
            });
        }

        private struct TransportEvent
        {
            public NetworkEvent Type;
            public ulong ClientId;
            public ArraySegment<byte> Payload;
            public float ReceiveTime;
        }

        // ================================================================
        // 내부 — 종료
        // ================================================================

        private void ShutdownInternal(string reason)
        {
            if (!_running && _udp == null) return; // 이미 정리 끝난 두 번째 호출 가드

            if (_ackOk && _udp != null)
            {
                try
                {
                    byte[] bye = Encoding.UTF8.GetBytes("{\"type\":\"BYE\"}");
                    _udp.Send(bye, bye.Length);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Relay] BYE 송신 실패: {e.Message}");
                }
            }

            _running = false;

            try { _udp?.Close(); } catch { }
            try { _udp?.Dispose(); } catch { }
            _udp = null;

            if (_receiveThread != null && _receiveThread.IsAlive) _receiveThread.Join(500);
            _receiveThread = null;

            if (_keepAliveThread != null && _keepAliveThread.IsAlive) _keepAliveThread.Join(500);
            _keepAliveThread = null;

            _eventQueue.Clear();
            _knownRemoteUserIds.Clear();
            _ackEvent.Reset();
            _ackOk = false;
            _ackFailReason = null;

            // 재진입 대비 — 세션 메타도 비움. 다음 SetSession() 호출이 다시 채움
            _sessionToken = null;
            _myUserId = 0;
            _sessionConfigured = false;
            _isHost = false;
            _hostUserId = 0;

            if (verboseLog) Debug.Log($"[Relay] Transport shutdown ({reason})");
        }

        private void OnDestroy()
        {
            ShutdownInternal("OnDestroy");
        }

        // ================================================================
        // 헬퍼 — big-endian 8 byte uint64 읽기/쓰기
        // ================================================================

        private static void WriteUInt64BE(byte[] buf, int offset, ulong value)
        {
            buf[offset + 0] = (byte)((value >> 56) & 0xFF);
            buf[offset + 1] = (byte)((value >> 48) & 0xFF);
            buf[offset + 2] = (byte)((value >> 40) & 0xFF);
            buf[offset + 3] = (byte)((value >> 32) & 0xFF);
            buf[offset + 4] = (byte)((value >> 24) & 0xFF);
            buf[offset + 5] = (byte)((value >> 16) & 0xFF);
            buf[offset + 6] = (byte)((value >> 8) & 0xFF);
            buf[offset + 7] = (byte)(value & 0xFF);
        }

        private static ulong ReadUInt64BE(byte[] buf, int offset)
        {
            return ((ulong)buf[offset + 0] << 56)
                 | ((ulong)buf[offset + 1] << 48)
                 | ((ulong)buf[offset + 2] << 40)
                 | ((ulong)buf[offset + 3] << 32)
                 | ((ulong)buf[offset + 4] << 24)
                 | ((ulong)buf[offset + 5] << 16)
                 | ((ulong)buf[offset + 6] << 8)
                 | (ulong)buf[offset + 7];
        }
    }
}

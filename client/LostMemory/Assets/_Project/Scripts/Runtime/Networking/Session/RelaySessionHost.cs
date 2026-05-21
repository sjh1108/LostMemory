using System;
using System.Text;
using System.Threading.Tasks;
using LostMemory.Multiplayer;
using LostMemory.Networking.Common;
using Unity.Netcode;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 호스트 세션 생성 진입점. 백엔드 Sessions API + 자체 Relay + NGO StartHost 까지 묶어 처리.
    ///
    /// 흐름:
    ///   1. 백엔드 로그인 보장 (RelaySession.EnsureInitializedAsync)
    ///   2. 입장 코드 생성 (랜덤 6자리)
    ///   3. POST /api/sessions 호출 → sessionToken·sessionId 받음
    ///   4. LostMemoryRelayTransport 에 sessionToken·myUserId 주입
    ///   5. NetworkManager.StartHost() 호출 → Relay 핸드셰이크 + NGO 호스트 활성화
    ///
    /// 사용 예: <c>var result = await RelaySessionHost.CreateAsync(2);</c>
    /// </summary>
    public static class RelaySessionHost
    {
        public readonly struct CreateResult
        {
            public readonly bool Success;
            public readonly string JoinCode;
            public readonly long SessionId;
            public readonly SessionErrorKind ErrorKind;
            public readonly string ErrorDetail;

            public CreateResult(bool success, string joinCode, long sessionId, SessionErrorKind kind, string detail)
            {
                Success = success;
                JoinCode = joinCode;
                SessionId = sessionId;
                ErrorKind = kind;
                ErrorDetail = detail;
            }

            public static CreateResult Ok(string code, long sessionId)
                => new CreateResult(true, code, sessionId, SessionErrorKind.None, null);

            public static CreateResult Fail(SessionErrorKind kind, string detail)
                => new CreateResult(false, null, 0, kind, detail);
        }

        public static async Task<CreateResult> CreateAsync(int maxPlayers, string sessionName = "LostMemorySession")
        {
            if (RelaySession.IsInSession)
            {
                NetLog.Warn("Host", "Already in a session. Ignoring CreateAsync.");
                return CreateResult.Fail(SessionErrorKind.Unknown, "Already in a session.");
            }

            // L-1: Local Fallback 모드 — backend 우회, UTP swap, StartHost.
            if (RelaySession.UseLocalFallback)
            {
                return await CreateLocalFallbackAsync();
            }

            try
            {
                await RelaySession.EnsureInitializedAsync();
            }
            catch (Exception ex)
            {
                return CreateResult.Fail(SessionErrorPolicy.Classify(ex), ex.Message);
            }

            string joinCode = GenerateJoinCode(6);

            // outer catch 에서도 접근 가능하도록 try 블록 밖에 선언 —
            // CreateSessionAsync 직후 ~ ActiveSessionId 설정 직전 사이에 예외 발생 시
            // 백엔드 sessions row 가 잔존하지 않도록 rollback 호출에 필요.
            SessionApiClient.SessionResponseData data = null;

            try
            {
                NetLog.Info("Host", $"Creating session via backend (max={maxPlayers}, code={joinCode})...");
                data = await SessionApiClient.CreateSessionAsync(maxPlayers, joinCode);
                if (data == null)
                {
                    var kind = SessionErrorKind.RelayAllocateFailed;
                    RelaySession.RaiseFailed(kind, "백엔드 createSession 실패");
                    return CreateResult.Fail(kind, "createSession returned null");
                }

                NetLog.Info("Host", $"Session created. id={data.sessionId}, code={data.privateCode}");

                // L-1: 이전 Editor 세션에서 fallback 으로 UTP swap 됐을 수 있어 LostMemoryRelay 복원 (idempotent).
                RelaySession.RestoreRelayTransport(out _);

                // Transport 에 sessionToken + 본인 userId 주입
                var transport = NetworkManager.Singleton.GetComponent<LostMemoryRelayTransport>();
                if (transport == null)
                {
                    // 백엔드 sessions row rollback — UDP transport 자체가 없으니 호스트 시작 불가
                    await TryRollbackSessionAsync(data.sessionId);
                    return CreateResult.Fail(SessionErrorKind.TransportStartFailed,
                        "LostMemoryRelayTransport 컴포넌트가 NetworkManager 에 부착돼있지 않음");
                }
                transport.SetSession(data.sessionToken, (ulong)data.hostId);

                // F-3 ReconnectGatekeeper 비활성 — NetworkConfig mismatch 우회. 필요시 한 번에 다시 켜는 작업 필요.
                // ReconnectGatekeeper.AttachHost((ulong)SessionApiClient.MyUserId);

                // NGO 호스트 모드 시작 (StartHost = StartServer + StartClient)
                bool started = NetworkManager.Singleton.StartHost();
                if (!started)
                {
                    // 백엔드 sessions row rollback — StartHost 실패 시 row 가 잔존하면 다음 시도가
                    // USER_ALREADY_IN_SESSION 가드에 차단됨. 호스트 본인이 DELETE 호출.
                    await TryRollbackSessionAsync(data.sessionId);
                    return CreateResult.Fail(SessionErrorKind.TransportStartFailed, "NetworkManager.StartHost() 실패");
                }
                // 안전: scene-placed NetworkManager 가 LoadScene(Single) 시 destroy 방지.
                UnityEngine.Object.DontDestroyOnLoad(NetworkManager.Singleton.gameObject);
                NetworkManager.Singleton.OnClientStopped -= OnClientStoppedHandler;
                NetworkManager.Singleton.OnClientStopped += OnClientStoppedHandler;

                RelaySession.ActiveSessionId = data.sessionId;
                RelaySession.IsHost = true;
                RelaySession.ActiveJoinCode = joinCode;
                RelaySession.RaiseJoined(asHost: true);

                NetLog.Info("Host", $"Session activated. JoinCode={joinCode}");
                return CreateResult.Ok(joinCode, data.sessionId);
            }
            catch (Exception ex)
            {
                NetLog.Error("Host", $"CreateAsync 실패: {ex.Message}");

                // 백엔드 sessions row 잔존 방지 — CreateSessionAsync 성공해서 row 가 생긴 상태에서
                // transport setup / StartHost / 핸들러 wiring 중 예외 (NetworkManager null 화,
                // OnClientStopped += handler throw, RaiseJoined subscriber 예외 등) 가 터지면
                // ActiveSessionId 가 안 잡힌 채로 row 만 살아남아 다음 시도가
                // USER_ALREADY_IN_SESSION 가드에 막힘. !IsInSession 가드로 정상 join 후 흐름과 구분.
                if (data != null && data.sessionId > 0 && !RelaySession.IsInSession)
                {
                    await TryRollbackSessionAsync(data.sessionId);
                }

                SessionErrorKind kind = SessionErrorPolicy.Classify(ex);
                if (kind == SessionErrorKind.Unknown) kind = SessionErrorKind.RelayAllocateFailed;
                RelaySession.RaiseFailed(kind, ex.Message);
                return CreateResult.Fail(kind, ex.Message);
            }
        }

        /// <summary>L-1: backend 전체 우회 호스트 시작. UTP swap + 가짜 ID/nickname.</summary>
        private static async Task<CreateResult> CreateLocalFallbackAsync()
        {
            await RelaySession.EnsureInitializedAsync(); // no-op in fallback

            if (!RelaySession.SwapToUnityTransport(out string transportError))
            {
                return CreateResult.Fail(SessionErrorKind.TransportStartFailed,
                    $"[LOCAL FALLBACK] Transport swap 실패: {transportError}");
            }

            ulong fallbackUserId = RelaySession.LocalFallbackUserId;
            NetLog.Info("Host", $"[LOCAL FALLBACK] CreateAsync — backend bypass. userId={fallbackUserId}, nick='{RelaySession.LocalFallbackNickname}'");

            // F-3 비활성 — NetworkConfig mismatch 회피.
            // ReconnectGatekeeper.AttachHost(fallbackUserId);

            bool started = NetworkManager.Singleton.StartHost();
            if (!started)
            {
                // ReconnectGatekeeper.DetachHost();
                return CreateResult.Fail(SessionErrorKind.TransportStartFailed,
                    "[LOCAL FALLBACK] StartHost 실패 — UTP swap / 포트 충돌 확인.");
            }
            // 안전: scene-placed NetworkManager 가 LoadScene(Single) 시 destroy 방지.
            UnityEngine.Object.DontDestroyOnLoad(NetworkManager.Singleton.gameObject);

            NetworkManager.Singleton.OnClientStopped -= OnClientStoppedHandler;
            NetworkManager.Singleton.OnClientStopped += OnClientStoppedHandler;

            RelaySession.ActiveSessionId = 1;
            RelaySession.IsHost = true;
            RelaySession.ActiveJoinCode = "LOCAL";
            RelaySession.RaiseJoined(asHost: true);

            NetLog.Info("Host", $"[LOCAL FALLBACK] Session activated. JoinCode=LOCAL");
            return CreateResult.Ok("LOCAL", 1);
        }

        private static async void OnClientStoppedHandler(bool _)
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientStopped -= OnClientStoppedHandler;
            }
            // F-3 비활성.
            // ReconnectGatekeeper.DetachHost();
            if (!RelaySession.IsInSession) return;
            try { await RelaySession.LeaveAsync(); }
            catch (Exception ex) { NetLog.Warn("Host", $"Auto-leave threw: {ex.Message}"); }
        }

        /// <summary>
        /// createSession 으로 만든 백엔드 sessions row 를 명시 삭제 — StartHost 실패 등으로
        /// 클라 측 ActiveSessionId 가 set 안 됐을 때 백엔드 row 잔존을 막아 다음 시도가
        /// USER_ALREADY_IN_SESSION 가드에 차단되지 않게 한다.
        /// </summary>
        private static async Task TryRollbackSessionAsync(long sessionId)
        {
            try
            {
                await SessionApiClient.DeleteSessionAsync(sessionId);
                NetLog.Info("Host", $"Rollback OK — session id={sessionId} deleted.");
            }
            catch (Exception ex)
            {
                NetLog.Warn("Host", $"Rollback 실패 (session id={sessionId}): {ex.Message}");
            }
        }

        /// <summary>O/0/1/I 같이 헷갈리는 글자 제외한 6자리 영숫자 코드.</summary>
        private static string GenerateJoinCode(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var sb = new StringBuilder(length);
            var rng = new System.Random();
            for (int i = 0; i < length; i++) sb.Append(chars[rng.Next(chars.Length)]);
            return sb.ToString();
        }
    }
}

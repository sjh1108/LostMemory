using System.Collections;
using LostMemory.Networking.Common;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 세션 종료/실패 시 단일 플레이어용 마을 씬으로 복귀시키는 핸들러.
    ///
    /// 트리거:
    ///   - RelaySession.Left  : 자발적 이탈 또는 disconnect 정리 직후 (LeaveAsync finally)
    ///   - RelaySession.Failed: host disconnect / transport failure / kicked 등 비정상 종료
    ///
    /// 동작:
    ///   - 이미 townSceneName 씬이라면 skip.
    ///   - NetworkManager 가 아직 listening 이면 Shutdown(discardMessageQueue:true) 보정.
    ///   - SceneManager.LoadScene(townSceneName, Single) — 단일 플레이어 일반 로드.
    ///
    /// 부착: NetworkManager / SessionLifecycle 옆 같은 GameObject (씬 마다 1개).
    /// Bootstrap 씬에 두지 않아도 됨 — Test_MultiLobby / Mob_Sync_Test 등 멀티 세션 가능한 씬에
    /// SessionLifecycle 과 함께 1개 배치하면 충분.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Session Town Return Handler")]
    public sealed class SessionTownReturnHandler : MonoBehaviour
    {
        [SerializeField, Tooltip("disconnect / leave 발생 시 복귀할 솔로 마을 씬 이름. Build Settings 에 enabled 상태여야 함. " +
            "기본값 Town_Preview — Town_solo 는 BuildSettings 에 disabled 되어 있음.")]
        private string townSceneName = "Town_Preview";

        [SerializeField, Tooltip("RelaySession.Failed (host disconnect / transport failure 등) 시 복귀할지.")]
        private bool returnOnFailed = true;

        [SerializeField, Tooltip("RelaySession.Left (자발적 이탈 포함) 시 복귀할지.")]
        private bool returnOnLeft = true;

        [Header("Disconnect Safety Net (NGO 이벤트 직접 hook)")]
        [Tooltip("NGO OnClientStopped/OnClientDisconnectCallback 도 추가로 hook 해서 RelaySession 이벤트 누락 대비. 게스트 측에서 호스트 끊김 감지 시 자동 복귀.")]
        [SerializeField] private bool enableNgoDisconnectSafetyNet = true;

        [Tooltip("호스트 disconnect 감지 후 자동 복귀까지 대기 시간 (초). 재연결 가능성 대비.")]
        [SerializeField, Min(0f)] private float disconnectTimeoutSeconds = 10f;

        private bool _subscribed;
        private bool _returning;
        private bool _ngoSubscribed;
        private Coroutine _disconnectTimeoutRoutine;

        /// <summary>
        /// 앱 시작 + 매 씬 로드 시 SessionTownReturnHandler GameObject 존재 보장.
        /// 씬 자산에 명시 배치 안 해도 자동 활성화 — 모든 멀티 씬에서 NGO 이벤트 hook 보장.
        /// InventoryFullModal 의 Bootstrap 패턴 재사용.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureExists();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureExists();
        }

        private static void EnsureExists()
        {
            if (FindAnyObjectByType<SessionTownReturnHandler>() != null) return;
            GameObject go = new GameObject("SessionTownReturnHandler_Auto");
            go.AddComponent<SessionTownReturnHandler>();
        }

        private void OnEnable()
        {
            // 영속화 — 씬 전환 후에도 NGO 이벤트 구독 유지 (InventoryFullModal 동일 패턴).
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            if (!_subscribed)
            {
                RelaySession.Left += HandleLeft;
                RelaySession.Failed += HandleFailed;
                _subscribed = true;
            }

            // NGO safety net: RelaySession 이벤트가 누락되거나 race 로 발화 안 될 경우 대비.
            // OnClientStopped: 호스트 종료 / 본인 종료 시 발화.
            // OnClientDisconnectCallback: 네트워크 끊김 (WiFi/transport timeout) 시 발화.
            if (enableNgoDisconnectSafetyNet)
            {
                TrySubscribeNgo();
            }
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                RelaySession.Left -= HandleLeft;
                RelaySession.Failed -= HandleFailed;
                _subscribed = false;
            }
            UnsubscribeNgo();

            if (_disconnectTimeoutRoutine != null)
            {
                StopCoroutine(_disconnectTimeoutRoutine);
                _disconnectTimeoutRoutine = null;
            }
        }

        private void TrySubscribeNgo()
        {
            if (_ngoSubscribed) return;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;

            nm.OnClientStopped += HandleClientStopped;
            nm.OnClientDisconnectCallback += HandleClientDisconnect;
            _ngoSubscribed = true;
        }

        /// <summary>
        /// NetworkManager 가 Bootstrap 시점에 null 일 수 있음 (NGO 가 늦게 spawn 또는 Shutdown 후 재시작).
        /// 매 프레임 lazy 구독 시도 — 한 번 구독되면 _ngoSubscribed 가 true 라 일찍 return.
        /// 구독 후 NetworkManager 가 Destroy 되면 _ngoSubscribed=false 로 되돌려 다음 NM 인스턴스 구독.
        /// </summary>
        private void Update()
        {
            if (!enableNgoDisconnectSafetyNet) return;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                if (_ngoSubscribed) _ngoSubscribed = false; // 이전 NM 사라짐 — 다음 인스턴스 대비 리셋
                return;
            }
            if (!_ngoSubscribed) TrySubscribeNgo();
        }

        private void UnsubscribeNgo()
        {
            if (!_ngoSubscribed) return;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientStopped -= HandleClientStopped;
                nm.OnClientDisconnectCallback -= HandleClientDisconnect;
            }
            _ngoSubscribed = false;
        }

        /// <summary>
        /// NGO OnClientStopped — 호스트 종료 또는 본인 종료 시 발화.
        /// wasHost=true 면 호스트 본인 (정상 흐름, 별도 처리 X).
        /// wasHost=false 인데 본 핸들러까지 도달했다는 건 게스트가 호스트 끊김으로 본인도 stopped → 마을 복귀.
        /// </summary>
        private void HandleClientStopped(bool wasHost)
        {
            if (wasHost) return; // 호스트 본인은 정상 종료 — 별도 처리 없음
            if (ShouldSuppressForResulting("NGO/OnClientStopped"))
            {
                NetLog.Info("TownReturn", "OnClientStopped (guest) — Resulting 중이라 보류. AutoReturnAfterResult 가 처리.");
                return;
            }
            NetLog.Warn("TownReturn", "OnClientStopped (guest) — 호스트 끊김 감지, 자동 솔로 마을 복귀.");
            ReturnToTown("NGO/OnClientStopped");
        }

        /// <summary>
        /// NGO OnClientDisconnectCallback — 네트워크 끊김 (WiFi off, transport timeout 등) 시 발화.
        /// 게스트 측에서 호스트 끊김 감지 시 N초 wait 후 자동 복귀. 재연결 가능성 대비.
        /// </summary>
        private void HandleClientDisconnect(ulong clientId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (nm.IsHost || nm.IsServer) return; // 호스트 측은 게스트 disconnect — 별도 처리

            // 0 = 호스트 (NGO 기본), 또는 자기 자신 (LocalClientId) — 둘 중 하나 끊기면 게스트 freeze 위험
            if (clientId != 0 && clientId != nm.LocalClientId) return;

            if (ShouldSuppressForResulting($"NGO/Disconnect:{clientId}")) return;

            NetLog.Warn("TownReturn", $"OnClientDisconnectCallback clientId={clientId} — {disconnectTimeoutSeconds:F0}s wait 후 솔로 마을 복귀 (재연결 대비).");

            if (_disconnectTimeoutRoutine != null) StopCoroutine(_disconnectTimeoutRoutine);
            _disconnectTimeoutRoutine = StartCoroutine(DisconnectTimeoutCoroutine(disconnectTimeoutSeconds));
        }

        private IEnumerator DisconnectTimeoutCoroutine(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _disconnectTimeoutRoutine = null;

            // 재연결 안 됐을 때만 복귀
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null && nm.IsConnectedClient)
            {
                NetLog.Info("TownReturn", "Disconnect timeout 종료 시점에 재연결됨 — 마을 복귀 abort.");
                yield break;
            }
            ReturnToTown("NGO/DisconnectTimeout");
        }

        private void HandleLeft()
        {
            if (!returnOnLeft) return;
            if (ShouldSuppressForResulting("Left")) return;
            ReturnToTown("Left");
        }

        private void HandleFailed(SessionErrorKind kind, string detail)
        {
            if (!returnOnFailed) return;
            if (ShouldSuppressForResulting($"Failed/{kind}"))
            {
                NetLog.Warn("TownReturn", $"Session failed ({kind}): {detail} — Resulting 상태라 자동 Town 복귀 보류. 결과창 AutoReturn 이 처리.");
                return;
            }
            NetLog.Warn("TownReturn", $"Session failed ({kind}): {detail} — Town 복귀.");
            ReturnToTown($"Failed/{kind}");
        }

        /// <summary>
        /// RunManager 가 결과창 표시 중(Resulting) 이면 자동 Town 복귀 보류.
        /// 결과창의 AutoReturnAfterResult 코루틴이 게스트 fallback(로컬 로비 로드 + Shutdown) 으로 처리하게 둠.
        /// 호스트 이탈로 RelaySession.Failed 가 발화돼도 결과창 사용자 경험을 깨지 않도록.
        /// </summary>
        private bool ShouldSuppressForResulting(string reason)
        {
            LostMemory.Stage.RunManager rm = LostMemory.Stage.RunManager.Instance;
            if (rm == null) return false;
            if (!rm.IsResulting) return false;
            NetLog.Info("TownReturn", $"Suppress auto-return ({reason}) — RunManager.IsResulting=true. AutoReturnAfterResult 가 처리.");
            return true;
        }

        private void ReturnToTown(string reason)
        {
            if (_returning) return;
            if (string.IsNullOrWhiteSpace(townSceneName)) return;

            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.name == townSceneName)
            {
                NetLog.Info("TownReturn", $"Already in {townSceneName} ({reason}). Skip scene load.");
                return;
            }

            ShutdownNetworkManagerIfNeeded();

            _returning = true;
            NetLog.Info("TownReturn", $"Loading {townSceneName} ({reason}).");
            SceneManager.LoadScene(townSceneName, LoadSceneMode.Single);
        }

        private static void ShutdownNetworkManagerIfNeeded()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (!nm.IsListening) return;

            try
            {
                nm.Shutdown(discardMessageQueue: true);
                NetLog.Info("TownReturn", "NetworkManager shutdown for clean Town reload.");
            }
            catch (System.Exception ex)
            {
                NetLog.Warn("TownReturn", $"NetworkManager.Shutdown threw: {ex.Message}");
            }
        }
    }
}

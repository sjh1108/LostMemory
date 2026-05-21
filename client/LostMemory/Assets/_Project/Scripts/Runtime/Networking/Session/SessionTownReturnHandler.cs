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
        [SerializeField, Tooltip("disconnect / leave 발생 시 복귀할 솔로 마을 씬 이름.")]
        private string townSceneName = "Town_solo";

        [SerializeField, Tooltip("RelaySession.Failed (host disconnect / transport failure 등) 시 복귀할지.")]
        private bool returnOnFailed = true;

        [SerializeField, Tooltip("RelaySession.Left (자발적 이탈 포함) 시 복귀할지.")]
        private bool returnOnLeft = true;

        private bool _subscribed;
        private bool _returning;

        private void OnEnable()
        {
            if (_subscribed) return;
            RelaySession.Left += HandleLeft;
            RelaySession.Failed += HandleFailed;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            RelaySession.Left -= HandleLeft;
            RelaySession.Failed -= HandleFailed;
            _subscribed = false;
        }

        private void HandleLeft()
        {
            if (!returnOnLeft) return;
            ReturnToTown("Left");
        }

        private void HandleFailed(SessionErrorKind kind, string detail)
        {
            if (!returnOnFailed) return;
            NetLog.Warn("TownReturn", $"Session failed ({kind}): {detail} — Town 복귀.");
            ReturnToTown($"Failed/{kind}");
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

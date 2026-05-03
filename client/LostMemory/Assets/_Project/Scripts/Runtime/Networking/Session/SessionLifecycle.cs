using LostMemory.Networking.Common;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 세션 라이프사이클 감시자. NetworkManager 이벤트를 구독해 호스트 이탈/피어 disconnect 를
    /// 분류하고 RelaySession 에 반영한다.
    ///
    /// 정책 (MVP, docs/04_multiplayer.md 기준):
    ///   - 호스트 이탈 → 클라이언트 측은 즉시 세션 종료 + UI 메시지
    ///   - 클라 이탈 → 호스트 측은 정보 로그만 (재접속 미지원)
    ///   - 자발 종료(RelaySession.LeaveAsync) → HostDisconnected 발화 안 함
    ///
    /// 테스트 씬에 빈 GameObject 로 1개 배치. NetworkManager 가 같은 씬에 있을 것.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Session Lifecycle")]
    public sealed class SessionLifecycle : MonoBehaviour
    {
        private NetworkManager _subscribedNetworkManager;

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Update()
        {
            if (_subscribedNetworkManager == null)
            {
                TrySubscribe();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void TrySubscribe()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (_subscribedNetworkManager == nm) return;

            nm.OnClientDisconnectCallback += HandleClientDisconnect;
            nm.OnTransportFailure += HandleTransportFailure;
            _subscribedNetworkManager = nm;
            NetLog.Info("Lifecycle", "Subscribed to NetworkManager events.");
        }

        private void Unsubscribe()
        {
            if (_subscribedNetworkManager == null) return;
            _subscribedNetworkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
            _subscribedNetworkManager.OnTransportFailure -= HandleTransportFailure;
            _subscribedNetworkManager = null;
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;

            // 이미 자발적으로 떠난 직후라면 재진입한 RaiseLeft 가 처리. 추가 상태 변경 없음.
            if (RelaySession.Active == null)
            {
                NetLog.Info("Lifecycle", $"Disconnect after session already cleared. clientId={clientId}");
                return;
            }

            // 클라이언트 입장에서 서버(호스트) ID 가 끊긴 경우 = 호스트 이탈
            if (nm.IsClient && !nm.IsServer && clientId == NetworkManager.ServerClientId)
            {
                NetLog.Warn("Lifecycle", "Host disconnected. Ending session.");
                RelaySession.RaiseFailed(SessionErrorKind.HostDisconnected, null);
                _ = RelaySession.LeaveAsync();
                return;
            }

            // 호스트 입장에서 피어가 떠난 경우
            if (nm.IsServer)
            {
                NetLog.Info("Lifecycle", $"Peer disconnected. clientId={clientId}");
            }
        }

        private void HandleTransportFailure()
        {
            NetLog.Error("Lifecycle", "Transport failure detected. Ending session.");
            if (RelaySession.Active != null)
            {
                RelaySession.RaiseFailed(SessionErrorKind.TransportStartFailed, "transport failure");
                _ = RelaySession.LeaveAsync();
            }
        }
    }
}

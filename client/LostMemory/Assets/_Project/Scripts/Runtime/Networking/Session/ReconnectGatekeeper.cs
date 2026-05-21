using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// F-3: Late-Join 재접속 게이트키퍼.
    /// - InRun 중 새 userId 입장 거부 (재접속만 허용)
    /// - 호스트 측 ConnectionApprovalCallback 으로 게이트
    /// - 클라이언트 측 NetworkConfig.ConnectionData 에 userId UTF-8 payload 주입
    /// </summary>
    public static class ReconnectGatekeeper
    {
        private static readonly HashSet<ulong> _knownUserIds = new HashSet<ulong>();
        private static readonly Dictionary<ulong, ulong> _clientToUser = new Dictionary<ulong, ulong>();
        private static bool _runInProgress;
        private static bool _hostAttached;

        public static void SetRunInProgress(bool inProgress)
        {
            _runInProgress = inProgress;
            if (!inProgress) _knownUserIds.Clear();
            Debug.Log($"[ReconnectGatekeeper] SetRunInProgress({inProgress}) known={_knownUserIds.Count}");
        }

        public static void AttachHost(ulong hostUserId)
        {
            if (_hostAttached) return;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;

            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = HandleConnectionApproval;
            nm.OnClientConnectedCallback += HandleClientConnected;
            nm.OnClientDisconnectCallback += HandleClientDisconnect;

            _knownUserIds.Add(hostUserId);
            SetClientPayload(hostUserId);
            _hostAttached = true;
            Debug.Log($"[ReconnectGatekeeper] AttachHost hostUserId={hostUserId}");
        }

        public static void DetachHost()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.ConnectionApprovalCallback = null;
                nm.OnClientConnectedCallback -= HandleClientConnected;
                nm.OnClientDisconnectCallback -= HandleClientDisconnect;
            }
            _knownUserIds.Clear();
            _clientToUser.Clear();
            _runInProgress = false;
            _hostAttached = false;
        }

        public static void SetClientPayload(ulong userId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;
            // NGO 는 host/client 의 NetworkConfig 가 match 해야 connection 성공.
            // 호스트의 AttachHost 가 ConnectionApproval=true 로 설정하므로 게스트도 동일하게.
            nm.NetworkConfig.ConnectionApproval = true;
            nm.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(userId.ToString());
        }

        public static ulong GetUserId(ulong clientId)
        {
            return _clientToUser.TryGetValue(clientId, out ulong uid) ? uid : 0UL;
        }

        private static void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            ulong userId = ParseUserIdFromPayload(request.Payload);

            if (userId == 0UL)
            {
                Debug.LogWarning($"[ReconnectGatekeeper] REJECT — payload userId 파싱 실패. clientId={request.ClientNetworkId}");
                response.Approved = false;
                response.Reason = "userId 누락";
                return;
            }

            if (_runInProgress && !_knownUserIds.Contains(userId))
            {
                Debug.LogWarning($"[ReconnectGatekeeper] REJECT — InRun 중 신규 userId={userId} 입장 시도.");
                response.Approved = false;
                response.Reason = "런 진행 중 신규 입장 불가";
                return;
            }

            _clientToUser[request.ClientNetworkId] = userId;
            _knownUserIds.Add(userId);
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.PlayerPrefabHash = null;
            Debug.Log($"[ReconnectGatekeeper] OK userId={userId} clientId={request.ClientNetworkId}");
        }

        private static void HandleClientConnected(ulong clientId) { }

        private static void HandleClientDisconnect(ulong clientId)
        {
            if (_clientToUser.TryGetValue(clientId, out ulong uid))
            {
                _clientToUser.Remove(clientId);
                Debug.Log($"[ReconnectGatekeeper] Disconnect clientId={clientId} userId={uid} — 재접속 대기.");
            }
        }

        private static ulong ParseUserIdFromPayload(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return 0UL;
            try
            {
                string s = Encoding.UTF8.GetString(payload);
                return ulong.TryParse(s, out ulong v) ? v : 0UL;
            }
            catch { return 0UL; }
        }
    }
}

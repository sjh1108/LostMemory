using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 멀티 로비 (Test_MultiLobby) 의 현재 참가자 수를 TMP_Text 에 표시.
    /// 호스트 측 NetworkManager 의 `ConnectedClientsIds.Count` 를 매 client connect/disconnect 시 갱신,
    /// `NetworkVariable<int>` 로 모든 클라에 sync. 게스트 측도 정확한 카운트 표시.
    ///
    /// 부착:
    ///   - Test_MultiLobby 의 scene-placed GameObject (NetworkObject 부착 필요)
    ///   - `label` 슬롯에 TMP_Text 드래그
    ///
    /// 표시 형식: `format` (기본 "참가자: {0} / {1}") 에 {0}=현재, {1}=maxPlayers 치환.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Multi Lobby Player Count Display")]
    public sealed class MultiLobbyPlayerCountDisplay : NetworkBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string format = "참가자: {0} / {1}";
        [SerializeField, Min(1)] private int maxPlayers = 4;

        private readonly NetworkVariable<int> _playerCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _playerCount.OnValueChanged += HandleCountChanged;
            UpdateLabel(_playerCount.Value);

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
                RefreshCount();
            }
        }

        public override void OnNetworkDespawn()
        {
            _playerCount.OnValueChanged -= HandleCountChanged;

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            base.OnNetworkDespawn();
        }

        private void HandleClientConnected(ulong clientId)
        {
            RefreshCount();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            RefreshCount();
        }

        private void RefreshCount()
        {
            if (!IsServer || NetworkManager == null) return;
            _playerCount.Value = NetworkManager.ConnectedClientsIds.Count;
        }

        private void HandleCountChanged(int previous, int current)
        {
            UpdateLabel(current);
        }

        private void UpdateLabel(int count)
        {
            if (label == null) return;
            label.text = string.Format(format, count, maxPlayers);
        }
    }
}

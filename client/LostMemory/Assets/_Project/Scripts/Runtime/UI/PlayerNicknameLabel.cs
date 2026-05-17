using LostMemory.Networking.Session;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 플레이어 캐릭터 머리 위 닉네임 라벨.
    ///
    /// - 솔로 (NGO 미시작): `RelaySession.AutoLoginNickname` 직접 표시
    /// - 멀티 (NGO spawn 후):
    ///     - owner: `OnNetworkSpawn` 에서 자기 닉네임을 `_syncedNickname` NetworkVariable 에 write
    ///     - non-owner: `OnValueChanged` 로 sync 된 owner 의 닉네임 표시
    ///
    /// 부착:
    ///   - 캐릭터 prefab 의 root 또는 자식 (NetworkObject hierarchy 안)
    ///   - `label` 슬롯에 TMP_Text 드래그 (또는 비워두면 자식에서 자동 검색)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Player Nickname Label")]
    public sealed class PlayerNicknameLabel : NetworkBehaviour
    {
        [SerializeField, Tooltip("닉네임을 표시할 TMP_Text. 비워두면 자식에서 자동 검색.")]
        private TMP_Text label;

        [SerializeField, Tooltip("닉네임 미설정 시 fallback 텍스트.")]
        private string fallback = "Player";

        // owner write, everyone read. FixedString64Bytes = UTF-8 64 byte (한글 약 20 글자).
        private readonly NetworkVariable<FixedString64Bytes> _syncedNickname = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private void Awake()
        {
            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void OnEnable()
        {
            // NGO 미스폰 (솔로) 시 즉시 fallback 표시
            if (!IsSpawned)
            {
                RefreshLocal();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _syncedNickname.OnValueChanged += HandleSyncedNicknameChanged;

            if (IsOwner)
            {
                // owner — 본 클라의 AutoLoginNickname 을 NetworkVariable 에 write
                string nick = RelaySession.AutoLoginNickname;
                if (string.IsNullOrWhiteSpace(nick)) nick = fallback;
                _syncedNickname.Value = new FixedString64Bytes(nick);
                ApplyText(nick);
            }
            else
            {
                // non-owner — 이미 sync 된 값이 있으면 즉시 적용
                ApplyText(_syncedNickname.Value.ToString());
            }
        }

        public override void OnNetworkDespawn()
        {
            _syncedNickname.OnValueChanged -= HandleSyncedNicknameChanged;
            base.OnNetworkDespawn();
        }

        private void HandleSyncedNicknameChanged(FixedString64Bytes previous, FixedString64Bytes current)
        {
            ApplyText(current.ToString());
        }

        /// <summary>솔로 환경 (NGO 미스폰) fallback — 본 클라의 AutoLoginNickname 그대로 표시.</summary>
        public void RefreshLocal()
        {
            string nick = RelaySession.AutoLoginNickname;
            ApplyText(!string.IsNullOrWhiteSpace(nick) ? nick : fallback);
        }

        private void ApplyText(string text)
        {
            if (label == null) return;
            label.text = !string.IsNullOrWhiteSpace(text) ? text : fallback;
        }
    }
}

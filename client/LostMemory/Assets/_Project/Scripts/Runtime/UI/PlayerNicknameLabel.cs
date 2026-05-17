using LostMemory.Networking.Session;
using TMPro;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 플레이어 캐릭터 머리 위 닉네임 라벨.
    /// 솔로 환경 (Town_solo 등) 에서 RelaySession.AutoLoginNickname 을 직접 표시.
    ///
    /// 부착:
    ///   - 캐릭터 prefab (예: Test_shm_nickname) 의 자식 GameObject 에 World Space Canvas + TMP_Text
    ///     준비 → 본 컴포넌트는 그 Canvas 와 같은 GameObject 또는 부모 어디든 부착 가능
    ///   - `label` 슬롯에 TMP_Text 드래그 (또는 비워두면 Awake 에서 자식 검색)
    ///
    /// 후속 (멀티 환경):
    ///   - 다른 player 의 닉네임 sync 가 필요하면 본 컴포넌트를 NetworkBehaviour 로 변환 +
    ///     NetworkVariable&lt;FixedString64Bytes&gt; 추가. 현재는 솔로 한정.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Player Nickname Label")]
    public sealed class PlayerNicknameLabel : MonoBehaviour
    {
        [SerializeField, Tooltip("닉네임을 표시할 TMP_Text. 비워두면 자식에서 자동 검색.")]
        private TMP_Text label;

        [SerializeField, Tooltip("닉네임 미설정 시 fallback 텍스트.")]
        private string fallback = "Player";

        private void Awake()
        {
            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (label == null) return;
            string nick = RelaySession.AutoLoginNickname;
            label.text = !string.IsNullOrWhiteSpace(nick) ? nick : fallback;
        }
    }
}

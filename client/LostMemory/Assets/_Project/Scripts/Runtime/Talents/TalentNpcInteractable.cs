using UnityEngine;

namespace LostMemory.Talents
{
    /// <summary>
    /// 재능 NPC 의 상호작용 트리거. NPC GameObject 에 부착.
    ///
    /// 플레이어가 범위에 진입한 뒤 interactKey 를 누르면 TalentPanelView.Open() 을 호출한다.
    /// ShopNpcInteractable 과 동일한 패턴.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Talents/Talent Npc Interactable")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class TalentNpcInteractable : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TalentPanelView _talentPanel;

        [Header("Config")]
        [SerializeField] private KeyCode _interactKey = KeyCode.F;
        [Tooltip("플레이어 검증용 tag. 비워두면 모든 trigger 인식.")]
        [SerializeField] private string _playerTag = "Player";
        [Tooltip("(선택) 'F 누르세요' 안내 GameObject. 범위 진입 시 활성, 이탈 시 비활성.")]
        [SerializeField] private GameObject _promptObject;

        [Header("Debug")]
        [SerializeField] private bool _logInteraction = true;

        private bool _playerInRange;

        private void Awake()
        {
            if (_promptObject != null) _promptObject.SetActive(false);
        }

        private void Update()
        {
            if (!_playerInRange) return;
            if (!Input.GetKeyDown(_interactKey)) return;

            if (_talentPanel == null)
            {
                Debug.LogError("[TalentNpcInteractable] TalentPanelView 가 연결되지 않았습니다.", this);
                return;
            }

            if (_logInteraction) Debug.Log("[TalentNpcInteractable] 재능 패널 열기.", this);
            _talentPanel.Open();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = true;
            if (_promptObject != null) _promptObject.SetActive(true);
            if (_logInteraction) Debug.Log("[TalentNpcInteractable] 플레이어 범위 진입.", this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = false;
            if (_promptObject != null) _promptObject.SetActive(false);
            if (_logInteraction) Debug.Log("[TalentNpcInteractable] 플레이어 범위 이탈.", this);
        }

        private bool IsPlayer(Collider2D other)
        {
            if (string.IsNullOrEmpty(_playerTag)) return true;
            return other.CompareTag(_playerTag);
        }
    }
}

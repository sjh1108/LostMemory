using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// CL-113 — Shop NPC 의 *F 키 상호작용 트리거*. NPC GameObject 에 부착.
    ///
    /// 책임:
    ///   1. 플레이어 trigger zone 진입/이탈 추적 (OnTriggerEnter2D / OnTriggerExit2D)
    ///   2. in-range + F 키 입력 → `ShopController.Toggle(shopData)` 호출
    ///   3. (선택) *F 누르세요* placeholder 표시 — `promptObject` 슬롯의 GameObject 활성/비활성
    ///
    /// 주의: 본 컴포넌트는 *NPC GameObject* 에 붙는다 (player 가 아님).
    /// 따라서 OnTriggerEnter2D 의 other = player. player tag 또는 layer 로 검증.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Shop/Shop Npc Interactable")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class ShopNpcInteractable : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ShopController shopController;
        [SerializeField] private ShopData shopData;

        [Header("Config")]
        [SerializeField] private KeyCode interactKey = KeyCode.F;
        [Tooltip("플레이어 검증용 tag. 비워두면 모든 trigger 인식.")]
        [SerializeField] private string playerTag = "Player";
        [Tooltip("(선택) 'F 누르세요' placeholder GameObject. in-range 시 활성, 이탈 시 비활성. null 허용.")]
        [SerializeField] private GameObject promptObject;

        [Header("Debug")]
        [SerializeField] private bool logInteraction = true;

        private bool _playerInRange;

        private void Awake()
        {
            // 시작 시 prompt 숨김 — 이탈 상태 기본.
            if (promptObject != null) promptObject.SetActive(false);
        }

        private void Update()
        {
            if (!_playerInRange) return;
            if (Input.GetKeyDown(interactKey))
            {
                if (shopController == null)
                {
                    Debug.LogError("[ShopNpcInteractable] shopController 가 null. wiring 확인.", this);
                    return;
                }
                if (shopData == null)
                {
                    Debug.LogError("[ShopNpcInteractable] shopData 가 null. wiring 확인.", this);
                    return;
                }
                if (logInteraction) Debug.Log($"[ShopNpcInteractable] '{interactKey}' 입력 → ShopController.Toggle.");
                shopController.Toggle(shopData);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = true;
            if (promptObject != null) promptObject.SetActive(true);
            if (logInteraction) Debug.Log($"[ShopNpcInteractable] Player in range.", this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = false;
            if (promptObject != null) promptObject.SetActive(false);
            if (logInteraction) Debug.Log($"[ShopNpcInteractable] Player out of range.", this);
        }

        private bool IsPlayer(Collider2D other)
        {
            if (string.IsNullOrEmpty(playerTag)) return true;
            return other.CompareTag(playerTag);
        }
    }
}

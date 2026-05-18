using LostMemory.Relics;
using LostMemory.Rewards;
using LostMemory.Shop;
using UnityEngine;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-228 — 미스터리 구매 NPC 의 F 키 trigger.
    /// ShopNpcInteractable ([Shop/ShopNpcInteractable.cs]) 패턴 복제 + 단순화:
    ///   - 정적 ShopData 폴백 X (반드시 동적 generation)
    ///   - 1회 가드 X — F 토글 자유 (다중 슬롯 자유 구매)
    ///   - ResolveShopData: MysteryShopGenerator.Generate 호출, 첫 visit 만 generate, 이후 캐시 재사용 (sold-out 보존)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Events/Mystery Shop Npc Interactable")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class MysteryShopNpcInteractable : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private MysteryShopController mysteryShopController;
        [SerializeField] private MysteryShopConfig mysteryShopConfig;
        [SerializeField] private RewardPool rewardPool;
        [SerializeField] private PlayerRelicInventory playerRelicInventory;

        [Header("Config")]
        [SerializeField] private KeyCode interactKey = KeyCode.F;
        [Tooltip("플레이어 검증용 tag. 비워두면 모든 trigger 인식.")]
        [SerializeField] private string playerTag = "Player";
        [Tooltip("(옵션) 'F 누르세요' placeholder GameObject. in-range 시 활성, 이탈 시 비활성. null 허용.")]
        [SerializeField] private GameObject promptObject;

        [Header("Debug")]
        [SerializeField] private bool logInteraction = true;

        private bool _playerInRange;

        // 첫 visit 에서만 generate, 이후 재사용 — 토글로 닫고 다시 열어도 같은 슬롯/sold-out 상태 유지.
        private ShopData _cachedShopData;

        private void Awake()
        {
            if (promptObject != null) promptObject.SetActive(false);
        }

        private void Update()
        {
            if (!_playerInRange) return;
            if (!Input.GetKeyDown(interactKey)) return;

            if (mysteryShopController == null)
            {
                Debug.LogError("[MysteryShopNpc] mysteryShopController null. wiring 확인.", this);
                return;
            }

            // 닫혀있을 때만 ShopData 결정. Toggle 이 Close 만 하면 데이터 불필요.
            ShopData runtimeShopData = mysteryShopController.IsOpen ? null : ResolveShopData();
            if (!mysteryShopController.IsOpen && runtimeShopData == null)
            {
                return;
            }

            if (logInteraction) Debug.Log($"[MysteryShopNpc] '{interactKey}' → Toggle.");
            mysteryShopController.Toggle(runtimeShopData);
        }

        private ShopData ResolveShopData()
        {
            if (_cachedShopData != null)
            {
                if (logInteraction)
                    Debug.Log($"[MysteryShopNpc] 캐시된 ShopData 재사용 — items={_cachedShopData.Items?.Length ?? 0}");
                return _cachedShopData;
            }

            if (mysteryShopConfig == null || rewardPool == null || playerRelicInventory == null)
            {
                Debug.LogError(
                    "[MysteryShopNpc] generation refs 누락 — mysteryShopConfig/rewardPool/playerRelicInventory 확인.",
                    this);
                return null;
            }

            _cachedShopData = MysteryShopGenerator.Generate(mysteryShopConfig, rewardPool, playerRelicInventory);
            if (logInteraction)
                Debug.Log($"[MysteryShopNpc] Generate 완료 — items={_cachedShopData?.Items?.Length ?? 0}");
            return _cachedShopData;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = true;
            if (promptObject != null) promptObject.SetActive(true);
            if (logInteraction) Debug.Log("[MysteryShopNpc] Player in range.", this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = false;
            if (promptObject != null) promptObject.SetActive(false);
            if (logInteraction) Debug.Log("[MysteryShopNpc] Player out of range.", this);
        }

        private bool IsPlayer(Collider2D other)
        {
            if (string.IsNullOrEmpty(playerTag)) return true;
            return other.CompareTag(playerTag);
        }
    }
}

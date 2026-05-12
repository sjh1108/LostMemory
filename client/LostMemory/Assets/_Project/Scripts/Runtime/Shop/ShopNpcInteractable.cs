using LostMemory.Relics;
using LostMemory.Rewards;
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
    /// ShopData 결정 우선순위 (ResolveShopData):
    ///   1. 동적 — shopConfig + rewardPool + playerRelicInventory 모두 wired 시
    ///      ShopGenerator.Generate() 로 매번 새 ShopData 생성 (보유 필터 + 등급 가중치 + 소모품 1개 보장)
    ///   2. 정적 fallback — 위 3 ref 중 하나라도 비면 shopData asset 사용 (회귀 안전)
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
        [Tooltip("정적 fallback. 동적 3 ref 가 모두 wired 면 무시됨.")]
        [SerializeField] private ShopData shopData;

        [Header("Dynamic Generation (옵션 — 3 ref 모두 wired 시 ShopGenerator 사용)")]
        [Tooltip("등급 가중치 / 소모품 가격 / 폴백 설정. ShopGenerator 입력.")]
        [SerializeField] private ShopConfig shopConfig;
        [Tooltip("유물 추첨 풀. 보유 필터 + 등급 가중 추첨 대상.")]
        [SerializeField] private RewardPool rewardPool;
        [Tooltip("MP 대비: 인스턴스마다 다른 player 인스턴스. SP 에서는 씬의 단일 PlayerRelicInventory 드래그.")]
        [SerializeField] private PlayerRelicInventory playerRelicInventory;

        [Header("Config")]
        [SerializeField] private KeyCode interactKey = KeyCode.F;
        [Tooltip("플레이어 검증용 tag. 비워두면 모든 trigger 인식.")]
        [SerializeField] private string playerTag = "Player";
        [Tooltip("(선택) 'F 누르세요' placeholder GameObject. in-range 시 활성, 이탈 시 비활성. null 허용.")]
        [SerializeField] private GameObject promptObject;

        [Header("Debug")]
        [SerializeField] private bool logInteraction = true;

        private bool _playerInRange;

        // Phase A 확장: 동적 ShopData 캐시. 인스턴스마다 1회만 Generate, 이후 재사용.
        // 컴포넌트 lifetime = 1F_SHOP 프리팹 인스턴스 lifetime = 1 run 의 1번 visit
        // → 새 런 = 새 컴포넌트 = 자연 초기화. 별도 reset 코드 불필요.
        private ShopData _cachedDynamicShopData;

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

                // CL-113: 닫혀있을 때만 ShopData 재생성. 열림 → Toggle 은 Close 만 호출하므로 새 데이터 불필요.
                ShopData runtimeShopData = shopController.IsOpen ? null : ResolveShopData();
                if (!shopController.IsOpen && runtimeShopData == null)
                {
                    // ResolveShopData 가 에러 로그 찍었음. 추가 로그 X.
                    return;
                }

                if (logInteraction) Debug.Log($"[ShopNpcInteractable] '{interactKey}' 입력 → ShopController.Toggle.");
                shopController.Toggle(runtimeShopData);
            }
        }

        /// <summary>
        /// 동적 3 ref 모두 wired 면 ShopGenerator 호출. 아니면 정적 shopData 폴백.
        /// 둘 다 안 되면 null + 에러 로그.
        ///
        /// Phase A: 동적 경로는 *첫 호출에서만* Generate, 이후 캐시 재사용 → 리롤 차단.
        /// 캐시 invalidation 은 컴포넌트 lifetime 에 묶임 (새 런 = 새 인스턴스 = 새 캐시).
        /// 정적 경로는 어차피 같은 asset 참조 반환이라 캐시 불필요.
        /// </summary>
        private ShopData ResolveShopData()
        {
            bool dynamicReady = shopConfig != null && rewardPool != null && playerRelicInventory != null;
            if (dynamicReady)
            {
                if (_cachedDynamicShopData == null)
                {
                    _cachedDynamicShopData = ShopGenerator.Generate(playerRelicInventory, rewardPool, shopConfig);
                    if (logInteraction)
                        Debug.Log($"[ShopNpcInteractable] ShopGenerator 로 동적 생성 (첫 visit) — items={_cachedDynamicShopData?.Items?.Length ?? 0}");
                }
                else if (logInteraction)
                {
                    Debug.Log($"[ShopNpcInteractable] 캐시된 ShopData 재사용 — items={_cachedDynamicShopData.Items?.Length ?? 0}");
                }
                return _cachedDynamicShopData;
            }

            if (shopData != null)
            {
                if (logInteraction) Debug.Log($"[ShopNpcInteractable] 정적 ShopData 사용 (fallback).");
                return shopData;
            }

            Debug.LogError(
                "[ShopNpcInteractable] 상점 데이터 결정 실패 — 동적 3 ref (shopConfig/rewardPool/playerRelicInventory) 도 비었고 정적 shopData 도 비었음.",
                this);
            return null;
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

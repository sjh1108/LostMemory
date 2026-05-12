using LostMemory.Relics;
using LostMemory.Stage;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// CL-113 — Shop 방 안 ShopPanelView 외부 오케스트레이터.
    ///
    /// 책임:
    ///   1. Open(ShopData) — 패널 활성 + Init + 플레이어 조작 봉쇄
    ///   2. Close() — 패널 비활성 + 플레이어 조작 복원
    ///   3. HandleItemPurchased — ShopPanelView 의 OnItemPurchased 구독.
    ///      ShopPanelView 가 *내부 _gold 차감 + Inventory.TryAdd* 까지 했음.
    ///      본 controller 는 *GoldWallet 와 sync* 만 (Spend + UpdateGold).
    ///
    /// timeScale 변경 X — Shop 방은 적 없어 정지 불필요. 이동/조준만 봉쇄.
    /// 닫기 메커니즘 = Open 시 등록한 NPC 의 F 키 토글 (ShopNpcInteractable 에서 호출).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Shop/Shop Controller")]
    public sealed class ShopController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ShopPanelView panel;
        [Tooltip("CL-113: Shop 열림과 동시에 표시할 인벤토리 패널 (null 허용 — 표시 안 함). 구매 시 자동 Refresh.")]
        [SerializeField] private InventoryPanelView inventoryPanel;
        [Tooltip("Shop 열림과 동시에 표시할 단축키바 (null 허용 — 표시 안 함). 소모품 구매 후 자동 Refresh.")]
        [SerializeField] private ShortcutBarView shortcutBar;
        [SerializeField] private PlayerRelicInventory playerRelicInventory;
        [Tooltip("ShortcutBar 표시용 소모품 인벤토리. null 허용 — null 이면 ShortcutBar 도 표시 안 됨. 라우팅 자체는 PlayerRelicInventory.TryAdd 가 자동 처리.")]
        [SerializeField] private PlayerConsumableInventory playerConsumableInventory;
        [SerializeField] private GoldWallet goldWallet;
        [Tooltip("CL-113: 패널 떠있는 동안 마우스 조준 차단. (참고: KhiPlayerAim 자체엔 Update 가 없어 enabled 토글 효과 없음 — 실제 칼 회전 차단은 playerWeaponPresenter 슬롯)")]
        [SerializeField] private KhiPlayerAim playerAim;
        [Tooltip("CL-113: 패널 떠있는 동안 이동 차단. CharacterMovement.MovementForbidden 토글.")]
        [SerializeField] private CharacterMovement playerMovement;
        [Tooltip("CL-113: 패널 떠있는 동안 칼이 마우스를 따라 회전하지 않도록 차단. KhiWeaponPresenter.Update 가 매 프레임 GetAimDirection 으로 칼을 갱신하므로 이 컴포넌트 enabled 토글이 실제 차단점.")]
        [SerializeField] private KhiWeaponPresenter playerWeaponPresenter;
        [Tooltip("CL-113: 패널 떠있는 동안 공격 input 차단. ExternalBlock = true 로 토글.")]
        [SerializeField] private KhiMeleeComboController playerMeleeCombo;
        [Tooltip("CL-113: 패널 떠있는 동안 대쉬 input 차단. PermitAbility(false) 로 토글.")]
        [SerializeField] private KhiDashController playerDash;
        [Tooltip("CL-113: 패널 떠있는 동안 패링 input 차단. ExternalBlock = true 로 토글.")]
        [SerializeField] private KhiParryController playerParry;

        [Header("Debug")]
        [SerializeField] private bool logShopFlow = true;

        public bool IsOpen { get; private set; }

        private void OnEnable()
        {
            if (panel != null)
            {
                panel.OnItemPurchased += HandleItemPurchased;
            }
        }

        private void OnDisable()
        {
            if (panel != null)
            {
                panel.OnItemPurchased -= HandleItemPurchased;
            }
            // panic restore — 패널 떠있는 채 disable 시 조작 봉쇄 잠금 방지.
            if (IsOpen)
            {
                RestorePlayerControls();
                IsOpen = false;
            }
        }

        // CL-115 D: ESC 일괄 닫기. 열림 상태에서만 동작 — Close() 가 인벤토리 패널까지 같이 닫음.
        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (logShopFlow) Debug.Log("[ShopController] ESC → Close.");
                Close();
            }
        }

        /// <summary>NPC 가까이 + F 키 입력 시 ShopNpcInteractable 가 호출.</summary>
        public void Open(ShopData shopData)
        {
            if (IsOpen)
            {
                if (logShopFlow) Debug.Log("[ShopController] Open 무시 — 이미 열림.");
                return;
            }

            // ShopController 가 RunManager 같은 DontDestroyOnLoad GameObject 옆에 부착돼 있으면
            // 인스펙터 reference (panel/inventoryPanel/shortcutBar/playerRelicInventory/playerConsumableInventory/
            // goldWallet 등) 가 이전 씬의 객체를 가리키다 stale null 이 됨.
            // 매 진입 시 활성 씬의 새 객체로 재 wiring (RewardController.ResolveRewardPanelView 와 동일 패턴).
            ResolveSceneLocalRefs();

            if (panel == null || playerRelicInventory == null || goldWallet == null)
            {
                Debug.LogError("[ShopController] Refs 누락 — panel/inventory/goldWallet wiring 확인.", this);
                return;
            }
            if (shopData == null)
            {
                Debug.LogError("[ShopController] shopData == null. Open 무시.", this);
                return;
            }

            IsOpen = true;
            panel.gameObject.SetActive(true);
            panel.Init(shopData, playerRelicInventory, goldWallet.Current);
            // 인벤토리 패널 동시 표시 — 구매 시 슬롯 갱신 즉시 확인 가능.
            if (inventoryPanel != null)
            {
                inventoryPanel.gameObject.SetActive(true);
                inventoryPanel.Init(playerRelicInventory, goldWallet.Current);
            }
            // 단축키바 동시 표시 — 소모품(포션) 구매 즉시 슬롯에 추가됨을 확인 가능.
            if (shortcutBar != null && playerConsumableInventory != null)
            {
                shortcutBar.gameObject.SetActive(true);
                shortcutBar.Init(playerConsumableInventory);
            }
            SuppressPlayerControls();

            if (logShopFlow) Debug.Log($"[ShopController] Opened. gold={goldWallet.Current}");
        }

        /// <summary>F 토글 닫기 / ESC / 외부 (Run 종료 등) 가 호출.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            panel.gameObject.SetActive(false);
            // 인벤토리 패널도 같이 닫음. 단독 토글 (InventoryToggleController) 가 다시 열 수 있음.
            if (inventoryPanel != null)
            {
                inventoryPanel.gameObject.SetActive(false);
            }
            // 단축키바도 같이 닫음.
            if (shortcutBar != null)
            {
                shortcutBar.gameObject.SetActive(false);
            }
            RestorePlayerControls();

            if (logShopFlow) Debug.Log("[ShopController] Closed.");
        }

        /// <summary>NPC F 키 토글 — ShopNpcInteractable 가 호출. 열림이면 닫고, 닫힘이면 연다.</summary>
        public void Toggle(ShopData shopData)
        {
            if (IsOpen) Close();
            else Open(shopData);
        }

        /// <summary>
        /// 씬 전환 시 stale null 이 된 씬 로컬 reference 를 활성 씬 객체로 재 wiring.
        /// ShopController 가 DontDestroyOnLoad GameObject 옆에 있을 때만 의미 있고,
        /// 씬 로컬 컴포넌트라면 reference 들이 정상이므로 no-op.
        /// 활성 씬 우선 → fallback FindAnyObjectByType.
        /// </summary>
        private void ResolveSceneLocalRefs()
        {
            UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            panel = ResolveInActiveScene<ShopPanelView>(panel, active);
            inventoryPanel = ResolveInActiveScene<InventoryPanelView>(inventoryPanel, active);
            shortcutBar = ResolveInActiveScene<ShortcutBarView>(shortcutBar, active);
            playerRelicInventory = ResolveInActiveScene<PlayerRelicInventory>(playerRelicInventory, active);
            playerConsumableInventory = ResolveInActiveScene<PlayerConsumableInventory>(playerConsumableInventory, active);
            goldWallet = ResolveInActiveScene<GoldWallet>(goldWallet, active);
            playerAim = ResolveInActiveScene<KhiPlayerAim>(playerAim, active);
            playerMovement = ResolveInActiveScene<CharacterMovement>(playerMovement, active);
            playerWeaponPresenter = ResolveInActiveScene<KhiWeaponPresenter>(playerWeaponPresenter, active);
            playerMeleeCombo = ResolveInActiveScene<KhiMeleeComboController>(playerMeleeCombo, active);
            playerDash = ResolveInActiveScene<KhiDashController>(playerDash, active);
            playerParry = ResolveInActiveScene<KhiParryController>(playerParry, active);
        }

        private static T ResolveInActiveScene<T>(T current, UnityEngine.SceneManagement.Scene active) where T : Component
        {
            // 현재 reference 가 살아있고 활성 씬 소속이면 그대로 사용
            if (current != null && current.gameObject.scene == active)
            {
                return current;
            }

            T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.scene == active)
                {
                    return all[i];
                }
            }
            return all.Length > 0 ? all[0] : null;
        }

        // ──────────────────────────────────────────────────────────
        // ShopPanelView OnItemPurchased 동기화
        // ──────────────────────────────────────────────────────────

        private void HandleItemPurchased(ShopItemData item)
        {
            if (item == null)
            {
                Debug.LogWarning("[ShopController] HandleItemPurchased: item == null", this);
                return;
            }

            // ShopPanelView.TryBuy 가 이미 _gold (내부 복사본) 차감 + Inventory.TryAdd 까지 함.
            // 본 controller 는 *canonical GoldWallet* 차감 + 패널의 _gold 재동기화.
            if (goldWallet != null)
            {
                bool ok = goldWallet.Spend(item.Price);
                if (!ok)
                {
                    Debug.LogError(
                        $"[ShopController] GoldWallet.Spend({item.Price}) 실패. 패널과 desync 가능.", this);
                }
                if (panel != null)
                {
                    panel.UpdateGold(goldWallet.Current);
                }
            }
            // 인벤토리 패널 슬롯 갱신 — 새로 추가된 유물이 즉시 슬롯에 보임.
            if (inventoryPanel != null && inventoryPanel.gameObject.activeSelf)
            {
                inventoryPanel.Refresh(playerRelicInventory, goldWallet != null ? goldWallet.Current : 0);
            }
            // 단축키바 갱신 — 소모품 구매 시 PlayerRelicInventory.TryAdd 가 자동으로
            // PlayerConsumableInventory.TryAdd 로 라우팅하므로, UI 만 강제 Refresh.
            // 유물 구매에도 호출되지만 ShortcutBar 슬롯은 비어있는 채라 노옵에 가깝다.
            if (shortcutBar != null && shortcutBar.gameObject.activeSelf)
            {
                shortcutBar.Refresh();
            }

            if (logShopFlow) Debug.Log($"[ShopController] Purchased '{item.Relic?.DisplayName}' price={item.Price}");
        }

        // ──────────────────────────────────────────────────────────
        // 플레이어 조작 봉쇄 / 복원
        // ──────────────────────────────────────────────────────────

        private void SuppressPlayerControls()
        {
            if (playerMovement != null) playerMovement.MovementForbidden = true;
            if (playerAim != null) playerAim.enabled = false;
            // 공격 / 대쉬 / 패링 input + 칼 회전 일괄 차단 — RewardController.SetCombatInputsBlocked 와 동일 패턴.
            // Update 기반 input 은 timeScale 무관하게 동작하므로 명시적 차단 필요. Shop 은 timeScale 변경 X 라 더더욱.
            SetCombatInputsBlocked(true);
        }

        private void RestorePlayerControls()
        {
            if (playerMovement != null) playerMovement.MovementForbidden = false;
            if (playerAim != null) playerAim.enabled = true;
            SetCombatInputsBlocked(false);
        }

        // 보상 패널 패턴과 동일. 향후 RewardController 와 공통 추출 시 별도 리팩토링 ticket.
        private void SetCombatInputsBlocked(bool block)
        {
            if (playerMeleeCombo != null) playerMeleeCombo.ExternalBlock = block;
            if (playerDash != null) playerDash.PermitAbility(!block);
            if (playerParry != null) playerParry.ExternalBlock = block;
            // 칼이 마우스 따라가는 Update 차단 — enabled=false 면 Update 가 안 돌아 마지막 프레임 위치/회전 그대로 freeze.
            if (playerWeaponPresenter != null) playerWeaponPresenter.enabled = !block;
        }
    }
}

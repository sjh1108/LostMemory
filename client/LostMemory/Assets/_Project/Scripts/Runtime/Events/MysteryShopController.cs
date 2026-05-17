using LostMemory.Relics;
using LostMemory.Shop;
using LostMemory.Stage;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-228 — 미스터리 구매 패널 외부 오케스트레이터.
    /// ShopController ([Shop/ShopController.cs]) 의 80% 패턴 복제 + 차이점:
    ///   - 인벤토리 동시 표시 동일
    ///   - GoldWallet sync 동일
    ///   - 봉쇄/복원 동일 (timeScale 변경 X)
    ///   - 차이점: Skip 버튼 처리, 첫 구매 시 출구 해제 (RoomEntry 연계)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Events/Mystery Shop Controller")]
    public sealed class MysteryShopController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private MysteryShopPanelView panel;
        [Tooltip("패널 열림과 동시에 표시할 인벤토리 패널 (null 허용). 구매 시 자동 Refresh.")]
        [SerializeField] private InventoryPanelView inventoryPanel;
        [Tooltip("패널 열림과 동시에 표시할 단축키바 (null 허용). 소모품 구매 후 자동 Refresh.")]
        [SerializeField] private ShortcutBarView shortcutBar;
        [SerializeField] private PlayerRelicInventory playerRelicInventory;
        [Tooltip("ShortcutBar 표시용 소모품 인벤토리. null 허용 — 라우팅 자체는 PlayerRelicInventory.TryAdd 가 자동 처리.")]
        [SerializeField] private PlayerConsumableInventory playerConsumableInventory;
        [SerializeField] private GoldWallet goldWallet;

        [Header("Combat suppression (Shop 동일 패턴)")]
        [SerializeField] private KhiPlayerAim playerAim;
        [SerializeField] private CharacterMovement playerMovement;
        [SerializeField] private KhiWeaponPresenter playerWeaponPresenter;
        [SerializeField] private KhiMeleeComboController playerMeleeCombo;
        [SerializeField] private KhiDashController playerDash;
        [SerializeField] private KhiParryController playerParry;

        [Header("Room integration")]
        [Tooltip("이 이벤트 방의 RoomEntryRuntimeController. 첫 구매 또는 Skip 시 OpenExits + NotifyCustomRoomCleared 호출.")]
        [SerializeField] private RoomEntryRuntimeController roomController;

        [Header("Debug")]
        [SerializeField] private bool logFlow = true;

        public bool IsOpen { get; private set; }

        private bool _hasResolvedRoomCleared;

        private void OnEnable()
        {
            if (panel != null)
            {
                panel.OnSlotPurchased += HandleSlotPurchased;
                panel.OnSkipPressed   += HandleSkipPressed;
            }
        }

        private void OnDisable()
        {
            if (panel != null)
            {
                panel.OnSlotPurchased -= HandleSlotPurchased;
                panel.OnSkipPressed   -= HandleSkipPressed;
            }
            // panic restore — 패널 떠있는 채 disable 시 봉쇄 잠금 방지.
            if (IsOpen)
            {
                RestorePlayerControls();
                IsOpen = false;
            }
        }

        // ESC 일괄 닫기 — Shop 과 동일 정책. 단 출구 해제는 첫 구매 또는 Skip 에만.
        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (logFlow) Debug.Log("[MysteryShop] ESC → Close.");
                Close();
            }
        }

        /// <summary>NPC F 키 입력 시 MysteryShopNpcInteractable 가 호출.</summary>
        public void Open(ShopData shopData)
        {
            if (IsOpen)
            {
                if (logFlow) Debug.Log("[MysteryShop] Open 무시 — 이미 열림.");
                return;
            }

            ResolveSceneLocalRefs();

            if (panel == null || playerRelicInventory == null || goldWallet == null)
            {
                Debug.LogError("[MysteryShop] Refs 누락 — panel/inventory/goldWallet wiring 확인.", this);
                return;
            }
            if (shopData == null)
            {
                Debug.LogError("[MysteryShop] shopData == null. Open 무시.", this);
                return;
            }

            IsOpen = true;
            panel.gameObject.SetActive(true);
            panel.Init(shopData, playerRelicInventory, goldWallet.Current);

            if (inventoryPanel != null)
            {
                inventoryPanel.gameObject.SetActive(true);
                inventoryPanel.Init(playerRelicInventory, goldWallet.Current);
            }
            if (shortcutBar != null && playerConsumableInventory != null)
            {
                shortcutBar.gameObject.SetActive(true);
                shortcutBar.Init(playerConsumableInventory);
            }
            SuppressPlayerControls();

            if (logFlow) Debug.Log($"[MysteryShop] Opened. gold={goldWallet.Current}");
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            panel.gameObject.SetActive(false);
            if (inventoryPanel != null) inventoryPanel.gameObject.SetActive(false);
            if (shortcutBar != null) shortcutBar.gameObject.SetActive(false);
            RestorePlayerControls();

            if (logFlow) Debug.Log("[MysteryShop] Closed.");
        }

        /// <summary>NPC F 키 토글.</summary>
        public void Toggle(ShopData shopData)
        {
            if (IsOpen) Close();
            else Open(shopData);
        }

        // ──────────────────────────────────────────────────────────
        // 패널 이벤트 처리
        // ──────────────────────────────────────────────────────────

        private void HandleSlotPurchased(ShopItemData item)
        {
            if (item == null) return;

            // PanelView.HandleSlotClicked 가 이미 _gold 차감 + Inventory.TryAdd 까지 함.
            // 본 controller 는 *canonical GoldWallet* 차감 + 패널 sync.
            bool ok = goldWallet.Spend(item.Price);
            if (!ok)
            {
                Debug.LogError($"[MysteryShop] GoldWallet.Spend({item.Price}) 실패. 패널과 desync 가능.", this);
            }
            if (panel != null) panel.UpdateGold(goldWallet.Current);
            if (inventoryPanel != null && inventoryPanel.gameObject.activeSelf)
            {
                inventoryPanel.Refresh(playerRelicInventory, goldWallet.Current);
            }
            if (shortcutBar != null && shortcutBar.gameObject.activeSelf)
            {
                shortcutBar.Refresh();
            }

            if (logFlow) Debug.Log($"[MysteryShop] Purchased '{item.Relic?.DisplayName}' price={item.Price}");

            // 첫 구매 시 출구 해제 + 메타 RoomCleared 발화 (idempotent — 이후 구매는 skip).
            ResolveRoomCleared();
        }

        private void HandleSkipPressed()
        {
            if (logFlow) Debug.Log("[MysteryShop] Skip pressed.");
            Close();
            ResolveRoomCleared();
        }

        private void ResolveRoomCleared()
        {
            if (_hasResolvedRoomCleared) return;
            _hasResolvedRoomCleared = true;

            if (roomController != null)
            {
                roomController.OpenExits();
                roomController.NotifyCustomRoomCleared();
                if (logFlow) Debug.Log("[MysteryShop] Room cleared (exits opened).");
            }
            else if (logFlow)
            {
                Debug.LogWarning("[MysteryShop] roomController null — 출구 해제 skip. 단독 검증 씬이면 무시 가능.", this);
            }
        }

        // ──────────────────────────────────────────────────────────
        // 봉쇄 / 복원 — Shop 과 동일 패턴
        // ──────────────────────────────────────────────────────────

        private void SuppressPlayerControls()
        {
            if (playerMovement != null) playerMovement.MovementForbidden = true;
            if (playerAim != null) playerAim.enabled = false;
            SetCombatInputsBlocked(true);
        }

        private void RestorePlayerControls()
        {
            if (playerMovement != null) playerMovement.MovementForbidden = false;
            if (playerAim != null) playerAim.enabled = true;
            SetCombatInputsBlocked(false);
        }

        private void SetCombatInputsBlocked(bool block)
        {
            if (playerMeleeCombo != null) playerMeleeCombo.ExternalBlock = block;
            if (playerDash != null) playerDash.PermitAbility(!block);
            if (playerParry != null) playerParry.ExternalBlock = block;
            if (playerWeaponPresenter != null) playerWeaponPresenter.enabled = !block;
        }

        // ──────────────────────────────────────────────────────────
        // 씬 전환 stale ref 재 wiring — Shop 과 동일 패턴
        // ──────────────────────────────────────────────────────────

        private void ResolveSceneLocalRefs()
        {
            UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            panel                     = ResolveInActiveScene(panel, active);
            inventoryPanel            = ResolveInActiveScene(inventoryPanel, active);
            shortcutBar               = ResolveInActiveScene(shortcutBar, active);
            playerRelicInventory      = ResolveInActiveScene(playerRelicInventory, active);
            playerConsumableInventory = ResolveInActiveScene(playerConsumableInventory, active);
            goldWallet                = ResolveInActiveScene(goldWallet, active);
            playerAim                 = ResolveInActiveScene(playerAim, active);
            playerMovement            = ResolveInActiveScene(playerMovement, active);
            playerWeaponPresenter     = ResolveInActiveScene(playerWeaponPresenter, active);
            playerMeleeCombo          = ResolveInActiveScene(playerMeleeCombo, active);
            playerDash                = ResolveInActiveScene(playerDash, active);
            playerParry               = ResolveInActiveScene(playerParry, active);
            roomController            = ResolveInActiveScene(roomController, active);
        }

        private static T ResolveInActiveScene<T>(T current, UnityEngine.SceneManagement.Scene active) where T : Component
        {
            if (current != null && current.gameObject.scene == active) return current;

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
    }
}

using LostMemory.Networking.Player;
using LostMemory.Relics;
using LostMemory.Shop;
using LostMemory.Stage;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-228 (A 옵션) — 1 아이템 vending machine 오케스트레이터.
    /// MysteryShopController 와 동일한 봉쇄/복원/씬 ref 패턴, 단순화된 구매 흐름.
    /// 책임: NPC F → Open(VendingMachineConfig) → 클릭 시 GoldWallet.Spend + Inventory.TryAdd + 출구 해제.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Events/Vending Machine Controller")]
    public sealed class VendingMachineController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private VendingMachinePanelView panel;
        [SerializeField] private InventoryPanelView inventoryPanel;
        [SerializeField] private ShortcutBarView shortcutBar;
        [SerializeField] private PlayerRelicInventory playerRelicInventory;
        [SerializeField] private PlayerConsumableInventory playerConsumableInventory;
        [SerializeField] private GoldWallet goldWallet;

        [Header("Combat suppression")]
        [SerializeField] private KhiPlayerAim playerAim;
        [SerializeField] private CharacterMovement playerMovement;
        [SerializeField] private KhiWeaponPresenter playerWeaponPresenter;
        [SerializeField] private KhiMeleeComboController playerMeleeCombo;
        [SerializeField] private KhiDashController playerDash;
        [SerializeField] private KhiParryController playerParry;

        [Header("Room integration (옵션 — fallback)")]
        [Tooltip("이벤트 방의 RoomEntryRuntimeController. NPC 가 Open() 호출 시 인자로 넘긴 room 이 우선. " +
                 "이 슬롯은 NPC 가 room 을 못 찾았을 때 fallback. " +
                 "한 씬에 vending machine 방이 1개뿐이면 여기 직접 wire 도 OK.")]
        [SerializeField] private RoomEntryRuntimeController roomController;

        [Header("Debug")]
        [SerializeField] private bool logFlow = false;

        public bool IsOpen { get; private set; }

        private VendingMachineConfig _currentConfig;
        private RoomEntryRuntimeController _activeRoomController;   // Open() 마다 갱신 — procedural 다중 방 지원
        private bool _hasResolvedRoomCleared;

        private void OnEnable()
        {
            if (panel != null)
            {
                panel.OnBuyPressed  += HandleBuyPressed;
                panel.OnSkipPressed += HandleSkipPressed;
            }
        }

        private void OnDisable()
        {
            if (panel != null)
            {
                panel.OnBuyPressed  -= HandleBuyPressed;
                panel.OnSkipPressed -= HandleSkipPressed;
            }
            if (IsOpen)
            {
                RestorePlayerControls();
                IsOpen = false;
            }
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (logFlow) Debug.Log("[VendingMachine] ESC → Close.");
                Close();
            }
        }

        // 호환 오버로드 — forRoom null 이면 인스펙터 fallback 사용.
        public void Open(VendingMachineConfig config) => Open(config, null);

        /// <summary>
        /// NPC 가 호출. forRoom 은 *NPC 가 자기 방에서 찾아 넘긴 RoomEntryRuntimeController*.
        /// procedural 던전에서 같은 Controller 가 여러 방 NPC 의 호출을 처리할 때 핵심.
        /// forRoom == null 이면 인스펙터 fallback (`roomController` 슬롯) 사용.
        /// </summary>
        public void Open(VendingMachineConfig config, RoomEntryRuntimeController forRoom)
        {
            if (IsOpen) return;
            ResolveSceneLocalRefs();

            if (panel == null || playerRelicInventory == null || goldWallet == null)
            {
                Debug.LogError("[VendingMachine] Refs 누락 — panel/inventory/goldWallet wiring 확인.", this);
                return;
            }
            if (config == null)
            {
                Debug.LogError("[VendingMachine] config == null. Open 무시.", this);
                return;
            }

            _currentConfig = config;
            _activeRoomController = forRoom != null ? forRoom : roomController;
            _hasResolvedRoomCleared = false;   // 매 Open 마다 reset — 다음 방에서도 한 번 clear 가능

            IsOpen = true;
            panel.gameObject.SetActive(true);
            panel.Init(config, goldWallet.Current);

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

            if (logFlow) Debug.Log($"[VendingMachine] Opened. item='{config.Item?.DisplayName}' price={config.Price} gold={goldWallet.Current} room='{_activeRoomController?.name ?? "(none)"}'");
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            panel.gameObject.SetActive(false);
            if (inventoryPanel != null) inventoryPanel.gameObject.SetActive(false);
            if (shortcutBar != null) shortcutBar.gameObject.SetActive(false);
            RestorePlayerControls();
            if (logFlow) Debug.Log("[VendingMachine] Closed.");
        }

        public void Toggle(VendingMachineConfig config) => Toggle(config, null);

        public void Toggle(VendingMachineConfig config, RoomEntryRuntimeController forRoom)
        {
            if (IsOpen) Close();
            else Open(config, forRoom);
        }

        // ──────────────────────────────────────────────────────────
        // 패널 이벤트 처리
        // ──────────────────────────────────────────────────────────

        private void HandleBuyPressed()
        {
            if (_currentConfig == null || _currentConfig.Item == null)
            {
                Debug.LogWarning("[VendingMachine] HandleBuy: config / item null.", this);
                return;
            }

            RelicData item = _currentConfig.Item;
            int price = _currentConfig.Price;

            if (goldWallet.Current < price)
            {
                Debug.Log($"[VendingMachine] 골드 부족 (보유 {goldWallet.Current} / 필요 {price})");
                return;
            }
            if (!playerRelicInventory.TryAdd(item))
            {
                Debug.Log($"[VendingMachine] 인벤토리 추가 실패: {item.DisplayName}");
                return;
            }

            bool ok = goldWallet.Spend(price);
            if (!ok)
            {
                Debug.LogError($"[VendingMachine] GoldWallet.Spend({price}) 실패 — desync 가능.", this);
            }
            if (panel != null)
            {
                panel.UpdateGold(goldWallet.Current);
                panel.MarkSoldOut();
            }
            if (inventoryPanel != null && inventoryPanel.gameObject.activeSelf)
            {
                inventoryPanel.Refresh(playerRelicInventory, goldWallet.Current);
            }
            if (shortcutBar != null && shortcutBar.gameObject.activeSelf)
            {
                shortcutBar.Refresh();
            }

            if (logFlow) Debug.Log($"[VendingMachine] Purchased '{item.DisplayName}' for {price}G.");

            ResolveRoomCleared();
        }

        private void HandleSkipPressed()
        {
            if (logFlow) Debug.Log("[VendingMachine] Skip pressed.");
            Close();
            ResolveRoomCleared();
        }

        private void ResolveRoomCleared()
        {
            if (_hasResolvedRoomCleared) return;
            _hasResolvedRoomCleared = true;

            // Open() 시점에 결정된 _activeRoomController (NPC 가 넘겨준 방) 우선,
            // 없으면 인스펙터 fallback. 둘 다 null 이면 단독 검증 씬으로 간주.
            RoomEntryRuntimeController target = _activeRoomController != null ? _activeRoomController : roomController;
            if (target != null)
            {
                target.OpenExits();
                target.NotifyCustomRoomCleared();
                if (logFlow) Debug.Log($"[VendingMachine] Room cleared: '{target.name}' (exits opened).");
            }
            else if (logFlow)
            {
                Debug.LogWarning("[VendingMachine] roomController null — 출구 해제 skip. 단독 검증 씬이면 무시 가능.", this);
            }
        }

        // ──────────────────────────────────────────────────────────
        // 봉쇄 / 복원 — Shop 패턴
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
        // 씬 전환 stale ref 재 wiring — Shop 패턴
        // ──────────────────────────────────────────────────────────

        private void ResolveSceneLocalRefs()
        {
            UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            // scene-placed UI / room
            panel                     = ResolveInActiveScene(panel, active);
            inventoryPanel            = ResolveInActiveScene(inventoryPanel, active);
            shortcutBar               = ResolveInActiveScene(shortcutBar, active);
            roomController            = ResolveInActiveScene(roomController, active);
            // player-side — 멀티 본인 player 우선
            playerRelicInventory      = ResolvePreferLocalPlayer(playerRelicInventory, active);
            playerConsumableInventory = ResolvePreferLocalPlayer(playerConsumableInventory, active);
            goldWallet                = ResolvePreferLocalPlayer(goldWallet, active);
            playerAim                 = ResolvePreferLocalPlayer(playerAim, active);
            playerMovement            = ResolvePreferLocalPlayer(playerMovement, active);
            playerWeaponPresenter     = ResolvePreferLocalPlayer(playerWeaponPresenter, active);
            playerMeleeCombo          = ResolvePreferLocalPlayer(playerMeleeCombo, active);
            playerDash                = ResolvePreferLocalPlayer(playerDash, active);
            playerParry               = ResolvePreferLocalPlayer(playerParry, active);
        }

        private static T ResolveInActiveScene<T>(T current, UnityEngine.SceneManagement.Scene active) where T : Component
        {
            if (current != null && current.gameObject.scene == active) return current;

            T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.scene == active) return all[i];
            }
            return all.Length > 0 ? all[0] : null;
        }

        /// <summary>
        /// player-side 컴포넌트 resolve — LocalPlayerResolver.LocalCharacter 우선, fallback 으로 활성 씬 첫 매치.
        /// 멀티 환경에서 host 의 PlayerRelicInventory 가 잡히지 않도록 guest 본인 player 우선 lookup.
        /// </summary>
        private static T ResolvePreferLocalPlayer<T>(T current, UnityEngine.SceneManagement.Scene active) where T : Component
        {
            if (current != null && current.gameObject.scene == active) return current;

            var localChar = LocalPlayerResolver.LocalCharacter;
            if (localChar != null)
            {
                T onLocal = localChar.GetComponentInChildren<T>(includeInactive: true);
                if (onLocal != null) return onLocal;
            }

            return ResolveInActiveScene<T>(null, active);
        }
    }
}

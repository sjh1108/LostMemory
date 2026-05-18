using LostMemory.Relics;
using LostMemory.Shop;
using LostMemory.Stage;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-227 V2 — 카드 뽑기 도박 패널 오케스트레이터.
    /// VendingMachineController 와 동일 패턴 (Open/Close + 봉쇄 + sceneLocalRefs + per-call room).
    /// 차이점: 골드 차감 (entryCost) + 결과 골드 입금 + 1.5초 후 자동 닫힘.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Events/Card Draw Controller")]
    public sealed class CardDrawController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private CardDrawPanelView panel;
        [Tooltip("패널 떠있는 동안 인벤토리도 함께 표시 (선택). null 허용.")]
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
        [Tooltip("NPC 가 Open() 호출 시 인자로 넘긴 room 이 우선. 한 씬에 카드 뽑기 방 1개뿐이면 여기 직접 wire 가능.")]
        [SerializeField] private RoomEntryRuntimeController roomController;

        [Header("Debug")]
        [SerializeField] private bool logFlow = true;

        public bool IsOpen { get; private set; }

        private CardDrawConfig _currentConfig;
        private RoomEntryRuntimeController _activeRoomController;
        private bool _hasResolvedRoomCleared;

        private void OnEnable()
        {
            if (panel != null)
            {
                panel.OnCardPicked   += HandleCardPicked;
                panel.OnSkipPressed  += HandleClosePressed;   // Skip = 닫기
                panel.OnClosePressed += HandleClosePressed;   // X 버튼 = 닫기
            }
        }

        private void OnDisable()
        {
            if (panel != null)
            {
                panel.OnCardPicked   -= HandleCardPicked;
                panel.OnSkipPressed  -= HandleClosePressed;
                panel.OnClosePressed -= HandleClosePressed;
            }
            if (IsOpen)
            {
                RestorePlayerControls();
                IsOpen = false;
            }
        }

        private void Update()
        {
            // ESC = 닫기 (픽 전/후 어디서든)
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (logFlow) Debug.Log("[CardDraw] ESC → Close.");
                HandleClosePressed();
            }
        }

        public void Open(CardDrawConfig config) => Open(config, null);

        public void Open(CardDrawConfig config, RoomEntryRuntimeController forRoom)
        {
            if (IsOpen) return;
            ResolveSceneLocalRefs();

            if (panel == null || playerRelicInventory == null || goldWallet == null)
            {
                Debug.LogError("[CardDraw] Refs 누락 — panel/inventory/goldWallet wiring 확인.", this);
                return;
            }
            if (config == null)
            {
                Debug.LogError("[CardDraw] config == null. Open 무시.", this);
                return;
            }

            _currentConfig = config;
            _activeRoomController = forRoom != null ? forRoom : roomController;
            _hasResolvedRoomCleared = false;

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

            if (logFlow) Debug.Log($"[CardDraw] Opened. entryCost={config.EntryCost} gold={goldWallet.Current} room='{_activeRoomController?.name ?? "(none)"}'");
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            panel.gameObject.SetActive(false);
            if (inventoryPanel != null) inventoryPanel.gameObject.SetActive(false);
            if (shortcutBar != null) shortcutBar.gameObject.SetActive(false);
            RestorePlayerControls();

            if (logFlow) Debug.Log("[CardDraw] Closed.");
        }

        // ──────────────────────────────────────────────────────────
        // 패널 이벤트
        // ──────────────────────────────────────────────────────────

        private void HandleCardPicked(CardDrawOutcomeEntry outcome)
        {
            if (_currentConfig == null) return;

            // 입장료 차감 — PanelView 가 사전 검증했지만 desync 방지.
            bool ok = goldWallet.Spend(_currentConfig.EntryCost);
            if (!ok)
            {
                Debug.LogError($"[CardDraw] Spend({_currentConfig.EntryCost}) 실패 — desync 가능.", this);
            }

            // 결과 보상 입금 (0 이면 skip)
            if (outcome.ReturnGold > 0)
            {
                goldWallet.Add(outcome.ReturnGold);
            }

            int net = outcome.ReturnGold - _currentConfig.EntryCost;
            if (logFlow) Debug.Log($"[CardDraw] {outcome.Kind} — return={outcome.ReturnGold}G net={(net >= 0 ? "+" : "")}{net}G. Wallet={goldWallet.Current}");

            if (panel != null) panel.UpdateGold(goldWallet.Current);
            if (inventoryPanel != null && inventoryPanel.gameObject.activeSelf)
            {
                inventoryPanel.Refresh(playerRelicInventory, goldWallet.Current);
            }

            // 자동 닫힘 X — 플레이어가 X 또는 ESC 로 직접 닫을 때까지 패널 유지.
            // 단 출구 해제는 미리 — 픽 했으면 방 클리어로 간주.
            ResolveRoomCleared();
        }

        private void HandleClosePressed()
        {
            if (logFlow) Debug.Log("[CardDraw] Close pressed (X / ESC / Skip).");
            Close();
            // 픽 안 한 채 닫아도 방은 클리어 (Skip 효과).
            ResolveRoomCleared();
        }

        private void ResolveRoomCleared()
        {
            if (_hasResolvedRoomCleared) return;
            _hasResolvedRoomCleared = true;

            RoomEntryRuntimeController target = _activeRoomController != null ? _activeRoomController : roomController;
            if (target != null)
            {
                target.OpenExits();
                target.NotifyCustomRoomCleared();
                if (logFlow) Debug.Log($"[CardDraw] Room cleared: '{target.name}'.");
            }
            else if (logFlow)
            {
                Debug.LogWarning("[CardDraw] roomController null — 출구 해제 skip. 단독 검증 씬이면 무시 가능.", this);
            }
        }

        // ──────────────────────────────────────────────────────────
        // 봉쇄 / 복원
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
        // 씬 ref 재 wiring
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
                if (all[i] != null && all[i].gameObject.scene == active) return all[i];
            }
            return all.Length > 0 ? all[0] : null;
        }
    }
}

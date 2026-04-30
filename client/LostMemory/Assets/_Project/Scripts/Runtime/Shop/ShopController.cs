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
        [SerializeField] private PlayerRelicInventory playerRelicInventory;
        [SerializeField] private GoldWallet goldWallet;
        [Tooltip("CL-113: 패널 떠있는 동안 마우스 조준 차단. RewardController 와 동일 패턴.")]
        [SerializeField] private KhiPlayerAim playerAim;
        [Tooltip("CL-113: 패널 떠있는 동안 이동 차단. CharacterMovement.MovementForbidden 토글.")]
        [SerializeField] private CharacterMovement playerMovement;

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

        /// <summary>NPC 가까이 + F 키 입력 시 ShopNpcInteractable 가 호출.</summary>
        public void Open(ShopData shopData)
        {
            if (IsOpen)
            {
                if (logShopFlow) Debug.Log("[ShopController] Open 무시 — 이미 열림.");
                return;
            }
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
            RestorePlayerControls();

            if (logShopFlow) Debug.Log("[ShopController] Closed.");
        }

        /// <summary>NPC F 키 토글 — ShopNpcInteractable 가 호출. 열림이면 닫고, 닫힘이면 연다.</summary>
        public void Toggle(ShopData shopData)
        {
            if (IsOpen) Close();
            else Open(shopData);
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

            if (logShopFlow) Debug.Log($"[ShopController] Purchased '{item.Relic?.DisplayName}' price={item.Price}");
        }

        // ──────────────────────────────────────────────────────────
        // 플레이어 조작 봉쇄 / 복원
        // ──────────────────────────────────────────────────────────

        private void SuppressPlayerControls()
        {
            if (playerMovement != null) playerMovement.MovementForbidden = true;
            if (playerAim != null) playerAim.enabled = false;
        }

        private void RestorePlayerControls()
        {
            if (playerMovement != null) playerMovement.MovementForbidden = false;
            if (playerAim != null) playerAim.enabled = true;
        }
    }
}

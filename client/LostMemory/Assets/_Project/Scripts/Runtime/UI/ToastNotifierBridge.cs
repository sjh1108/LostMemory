using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// CL-152: PlayerRelicInventory.OnTryAddRejected 이벤트 → ToastNotifier 호출 어댑터.
    ///
    /// 노소연 코드 (Shop/InventoryPanelView.TryBuy) / 본인 코드 (Rewards/RewardPanelView.OnCardSelected)
    /// 양쪽이 inventory.TryAdd 호출. 본 컴포넌트는 양쪽 모두에 대해 자동 토스트 발화 (중복 코드 X).
    ///
    /// wiring: Stage 또는 UI Canvas 하위 GameObject 에 부착, Inventory 슬롯에 PlayerRelicInventory 드래그.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Toast Notifier Bridge")]
    public class ToastNotifierBridge : MonoBehaviour
    {
        [Tooltip("PlayerRelicInventory — TryAdd 실패 시 토스트 발화 대상.")]
        [SerializeField] private PlayerRelicInventory inventory;

        private void OnEnable()
        {
            if (inventory == null)
            {
                Debug.LogWarning("[ToastNotifierBridge] inventory null — wiring 필요.", this);
                return;
            }
            inventory.OnTryAddRejected += HandleRejected;
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.OnTryAddRejected -= HandleRejected;
        }

        private void HandleRejected(RelicData relic, string reason)
        {
            string name = relic != null ? relic.DisplayName : "(null)";
            ToastNotifier.Show($"인벤토리 추가 실패: {name} ({reason})");
        }
    }
}

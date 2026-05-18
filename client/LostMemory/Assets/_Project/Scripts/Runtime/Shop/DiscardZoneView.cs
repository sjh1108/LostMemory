using System;
using LostMemory.Relics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostMemory.Shop
{
    /// <summary>
    /// 인벤토리 패널 하단의 유물 버림 영역.
    /// InventorySlotView를 이 영역에 드롭하면 OnRelicDiscarded 이벤트가 발생한다.
    ///
    /// 확장 포인트: 실제 Remove 로직은 InventoryPanelView가 담당하고,
    /// 이 클래스는 "드롭 감지 + 시각 피드백"만 맡는다.
    /// → 나중에 코옵 트레이드 영역(TradeZoneView)으로 동일 패턴 재활용 가능.
    /// </summary>
    public class DiscardZoneView : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _background;

        private static readonly Color ColNormal = new(0.35f, 0.12f, 0.12f);
        private static readonly Color ColHover  = new(0.70f, 0.25f, 0.25f);

        /// <summary>유물이 이 영역에 드롭됐을 때 발생. 인자: 버려질 RelicData</summary>
        public event Action<RelicData> OnRelicDiscarded;

        private void Reset() => _background = GetComponent<Image>();

        private void Awake()
        {
            if (_background != null) _background.color = ColNormal;
        }

        // ── IDropHandler ─────────────────────────────────────────────

        public void OnDrop(PointerEventData eventData)
        {
            // pointerDrag = 드래그를 시작한 오브젝트 (InventorySlotView의 GameObject)
            var slot = eventData.pointerDrag?.GetComponent<InventorySlotView>();
            if (slot == null || slot.CurrentRelic == null) return;

            OnRelicDiscarded?.Invoke(slot.CurrentRelic);
            if (_background != null) _background.color = ColNormal;
        }

        // ── 드래그 중 호버 하이라이트 ─────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_background != null) _background.color = ColHover;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_background != null) _background.color = ColNormal;
        }
    }
}

using System;
using LostMemory.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostMemory.Shop
{
    /// <summary>
    /// 단축키바의 소모품 슬롯 1칸을 담당하는 뷰 컴포넌트.
    /// SetConsumable() / Clear()로 내용을 갱신하고,
    /// 드래그 앤 드롭으로 다른 슬롯과 위치를 교환할 수 있다.
    /// </summary>
    public class ConsumableSlotView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image           _iconImage;
        [SerializeField] private TextMeshProUGUI _keyLabel;   // "1" "2" "3" "4"

        /// <summary>현재 슬롯에 들어있는 소모품. 비어있으면 null.</summary>
        public RelicData CurrentConsumable { get; private set; }

        /// <summary>다른 슬롯이 이 슬롯에 드롭됐을 때 발생 — ShortcutBarView가 구독해 스왑 처리</summary>
        public event Action<ConsumableSlotView, ConsumableSlotView> OnSlotDropReceived;

        // ── 드래그 전용 임시 상태 ────────────────────────────────────
        private GameObject    _ghostGO;
        private Canvas        _canvas;
        private RectTransform _canvasRT;

        // ── 호버 하이라이트 ──────────────────────────────────────────
        private Image _bgImage;
        private Color _normalColor;
        private static readonly Color HoverColor = new(0.40f, 0.40f, 0.55f);

        private void Reset()
        {
            _iconImage = GetComponentInChildren<Image>();
            _keyLabel  = GetComponentInChildren<TextMeshProUGUI>();
        }

        private void Awake()
        {
            _bgImage     = GetComponent<Image>();
            _normalColor = _bgImage != null ? _bgImage.color : Color.clear;
        }

        // ── 슬롯 표시 API ────────────────────────────────────────────

        /// <summary>소모품 데이터를 채워 슬롯을 갱신한다.</summary>
        public void SetConsumable(RelicData consumable, int slotIndex)
        {
            CurrentConsumable  = consumable;
            _iconImage.sprite  = consumable.Icon;
            _iconImage.enabled = true;
            if (_keyLabel != null) _keyLabel.text = (slotIndex + 1).ToString();
        }

        /// <summary>슬롯을 비운다.</summary>
        public void Clear(int slotIndex)
        {
            CurrentConsumable  = null;
            _iconImage.sprite  = null;
            _iconImage.enabled = false;
            if (_keyLabel != null) _keyLabel.text = (slotIndex + 1).ToString();
        }

        // ── IBeginDragHandler ────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (CurrentConsumable == null) return;

            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null) return;
            _canvasRT = _canvas.GetComponent<RectTransform>();

            _ghostGO = new GameObject("ConsumableDragGhost");
            _ghostGO.transform.SetParent(_canvas.transform, false);
            _ghostGO.transform.SetAsLastSibling();

            var rt       = _ghostGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(55f, 55f);
            MoveGhostTo(eventData.position);

            var ghostImg           = _ghostGO.AddComponent<Image>();
            ghostImg.sprite        = CurrentConsumable.Icon;
            ghostImg.color         = new Color(1f, 1f, 1f, 0.75f);
            ghostImg.raycastTarget = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_ghostGO == null) return;
            MoveGhostTo(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghostGO != null)
            {
                Destroy(_ghostGO);
                _ghostGO  = null;
                _canvasRT = null;
                _canvas   = null;
            }
        }

        // ── IDropHandler (슬롯 간 위치 교환) ─────────────────────────

        public void OnDrop(PointerEventData eventData)
        {
            var fromSlot = eventData.pointerDrag?.GetComponent<ConsumableSlotView>();
            if (fromSlot == null || fromSlot == this) return;
            if (fromSlot.CurrentConsumable == null) return;

            OnSlotDropReceived?.Invoke(fromSlot, this);
        }

        // ── 드래그 중 호버 하이라이트 ─────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!eventData.dragging) return;
            // ConsumableSlotView에서 드래그 중일 때만 하이라이트 (일반 유물 드래그는 무시)
            if (eventData.pointerDrag?.GetComponent<ConsumableSlotView>() == null) return;
            if (_bgImage != null) _bgImage.color = HoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_bgImage != null) _bgImage.color = _normalColor;
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────

        private void MoveGhostTo(Vector2 screenPos)
        {
            if (_ghostGO == null || _canvasRT == null) return;
            Camera cam = (_canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRT, screenPos, cam, out Vector2 localPos);
            _ghostGO.GetComponent<RectTransform>().localPosition = localPos;
        }
    }
}

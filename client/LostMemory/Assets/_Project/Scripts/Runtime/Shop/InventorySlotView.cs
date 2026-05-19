using System;
using System.Linq;
using LostMemory.Relics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostMemory.Shop
{
    /// <summary>
    /// 인벤토리 그리드에서 슬롯 1칸을 담당하는 뷰 컴포넌트.
    /// - 다른 슬롯으로 드래그 → 위치 교환 (OnSlotDropReceived)
    /// - 인벤토리 패널 밖으로 드래그 → 버리기 (OnSlotDiscardRequested) + 빨간 ghost 피드백
    /// </summary>
    public class InventorySlotView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private Image      _iconImage;

        [Tooltip("비어있을 때 표시할 오브젝트 (없으면 무시)")]
        [SerializeField] private GameObject _emptyIndicator;

        /// <summary>현재 슬롯에 들어있는 유물. 비어있으면 null.</summary>
        public RelicData CurrentRelic { get; private set; }

        /// <summary>다른 슬롯에 드롭됐을 때 발생 — 스왑 처리용</summary>
        public event Action<InventorySlotView, InventorySlotView> OnSlotDropReceived;

        /// <summary>인벤토리 패널 밖에 드롭됐을 때 발생 — 버리기 처리용</summary>
        public event Action<InventorySlotView> OnSlotDiscardRequested;

        // ── 드래그 전용 임시 상태 ────────────────────────────────────
        private GameObject    _ghostGO;
        private Image         _ghostImage;
        private Canvas        _canvas;
        private RectTransform _canvasRT;
        private RectTransform _inventoryPanelRT;  // 인벤토리 밖 감지용

        private static readonly Color GhostNormal  = new(1f, 1f,   1f,   0.75f);
        private static readonly Color GhostDiscard = new(1f, 0.25f, 0.25f, 0.75f); // 빨간색

        // ── 호버 하이라이트 ──────────────────────────────────────────
        private Image _bgImage;
        private Color _normalColor;

        private void Reset()
        {
            _iconImage = (transform.Find("IconImage") ?? transform.Find("Icon"))
                         ?.GetComponent<Image>()
                         ?? GetComponentsInChildren<Image>()
                             .FirstOrDefault(img => img.gameObject != gameObject);
        }

        private void Awake()
        {
            _bgImage     = GetComponent<Image>();
            _normalColor = RelicRarityColors.Empty;
            if (_bgImage != null) _bgImage.color = _normalColor;
        }

        // ── 슬롯 표시 API ────────────────────────────────────────────

        public void SetRelic(RelicData relic)
        {
            CurrentRelic       = relic;
            _iconImage.sprite  = relic.Icon;
            _iconImage.color   = Color.white;
            _iconImage.enabled = relic.Icon != null;
            if (_emptyIndicator != null) _emptyIndicator.SetActive(false);

            if (_bgImage != null)
            {
                _normalColor   = RelicRarityColors.Slot(relic);
                _bgImage.color = _normalColor;
            }
        }

        public void Clear()
        {
            CurrentRelic       = null;
            _iconImage.sprite  = null;
            _iconImage.enabled = false;
            if (_emptyIndicator != null) _emptyIndicator.SetActive(true);

            if (_bgImage != null)
            {
                _normalColor   = RelicRarityColors.Empty;
                _bgImage.color = _normalColor;
            }
        }

        // ── IBeginDragHandler ────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (CurrentRelic == null) return;

            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null) return;
            _canvasRT = _canvas.GetComponent<RectTransform>();

            // 인벤토리 패널 RectTransform — 밖으로 나갔는지 판단에 사용
            _inventoryPanelRT = GetComponentInParent<InventoryPanelView>()
                                    ?.GetComponent<RectTransform>();

            // Ghost 생성
            _ghostGO = new GameObject("InventoryDragGhost");
            _ghostGO.transform.SetParent(_canvas.transform, false);
            _ghostGO.transform.SetAsLastSibling();

            var rt       = _ghostGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(55f, 55f);
            MoveGhostTo(eventData.position);

            _ghostImage                = _ghostGO.AddComponent<Image>();
            _ghostImage.sprite         = CurrentRelic.Icon;
            _ghostImage.color          = GhostNormal;
            _ghostImage.raycastTarget  = false; // ghost가 드롭 대상의 레이캐스트를 가리면 안 됨
        }

        // ── IDragHandler ─────────────────────────────────────────────

        public void OnDrag(PointerEventData eventData)
        {
            if (_ghostGO == null) return;
            MoveGhostTo(eventData.position);

            // 인벤토리 밖이면 ghost를 빨간색으로 → "버려진다" 피드백
            if (_ghostImage != null)
                _ghostImage.color = IsOutsideInventory(eventData.position)
                    ? GhostDiscard
                    : GhostNormal;
        }

        // ── IEndDragHandler ──────────────────────────────────────────

        public void OnEndDrag(PointerEventData eventData)
        {
            bool outside = IsOutsideInventory(eventData.position);

            if (_ghostGO != null)
            {
                Destroy(_ghostGO);
                _ghostGO          = null;
                _ghostImage       = null;
            }
            _canvasRT         = null;
            _canvas           = null;
            _inventoryPanelRT = null;

            // 인벤토리 밖에서 드롭됐고 유물이 있으면 → 버리기
            // (슬롯에 드롭된 경우 OnDrop이 먼저 처리하므로 outside=false)
            if (outside && CurrentRelic != null)
                OnSlotDiscardRequested?.Invoke(this);
        }

        // ── IDropHandler (슬롯 간 위치 교환) ─────────────────────────

        public void OnDrop(PointerEventData eventData)
        {
            var fromSlot = eventData.pointerDrag?.GetComponent<InventorySlotView>();
            if (fromSlot == null || fromSlot == this) return;
            if (fromSlot.CurrentRelic == null) return;

            OnSlotDropReceived?.Invoke(fromSlot, this);
        }

        // ── 호버 하이라이트 / 툴팁 ───────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.dragging)
            {
                if (_bgImage != null)
                    _bgImage.color = CurrentRelic != null
                        ? RelicRarityColors.SlotHover(CurrentRelic)
                        : new Color(0.40f, 0.40f, 0.55f);
            }
            else if (CurrentRelic != null)
            {
                TooltipView tip = TooltipView.Instance ?? TooltipView.EnsureInstance();
                tip?.Show(CurrentRelic, eventData.position);
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!eventData.dragging && CurrentRelic != null && TooltipView.Instance != null)
                TooltipView.Instance.UpdatePosition(eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_bgImage != null) _bgImage.color = _normalColor;
            TooltipView.Instance?.Hide();
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────

        private bool IsOutsideInventory(Vector2 screenPos)
        {
            if (_inventoryPanelRT == null) return false;
            Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _canvas.worldCamera : null;
            return !RectTransformUtility.RectangleContainsScreenPoint(
                _inventoryPanelRT, screenPos, cam);
        }

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

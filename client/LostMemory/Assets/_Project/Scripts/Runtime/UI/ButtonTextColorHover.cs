using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LostMemory.UI
{
    /// <summary>
    /// 버튼 호버/노멀 상태에 따라 자식 TextMeshPro 색상을 전환한다.
    /// 버튼 GameObject 에 추가하고 Inspector 에서 색상을 지정한다.
    /// </summary>
    [AddComponentMenu("Lost Memory/UI/Button Text Color Hover")]
    public sealed class ButtonTextColorHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Color _normalColor  = Color.white;
        [SerializeField] private Color _hoverColor   = Color.yellow;

        private void Awake()
        {
            if (_label == null)
                _label = GetComponentInChildren<TextMeshProUGUI>();

            ApplyColor(_normalColor);
        }

        public void OnPointerEnter(PointerEventData _) => ApplyColor(_hoverColor);
        public void OnPointerExit(PointerEventData _)  => ApplyColor(_normalColor);

        private void OnDisable() => ApplyColor(_normalColor);

        private void ApplyColor(Color color)
        {
            if (_label != null) _label.color = color;
        }
    }
}

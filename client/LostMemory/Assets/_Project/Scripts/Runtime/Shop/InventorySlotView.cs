using LostMemory.Relics;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Shop
{
    /// <summary>
    /// 인벤토리 그리드에서 슬롯 1칸을 담당하는 뷰 컴포넌트.
    /// InventoryPanelView가 Refresh() 시 SetRelic() / Clear()를 호출한다.
    /// </summary>
    public class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Image      _iconImage;

        [Tooltip("비어있을 때 표시할 오브젝트 (없으면 무시)")]
        [SerializeField] private GameObject _emptyIndicator;

        private void Reset()
        {
            _iconImage = GetComponentInChildren<Image>();
        }

        /// <summary>유물 아이콘을 슬롯에 표시한다.</summary>
        public void SetRelic(RelicData relic)
        {
            _iconImage.sprite  = relic.Icon;
            _iconImage.enabled = true; // 배경색은 항상 표시

            if (_emptyIndicator != null)
                _emptyIndicator.SetActive(false);
        }

        /// <summary>슬롯을 비어있는 상태로 초기화한다.</summary>
        public void Clear()
        {
            _iconImage.sprite  = null;
            _iconImage.enabled = true; // sprite=null이면 배경색만 보임 — 슬롯이 사라지면 안 됨

            if (_emptyIndicator != null)
                _emptyIndicator.SetActive(true);
        }
    }
}

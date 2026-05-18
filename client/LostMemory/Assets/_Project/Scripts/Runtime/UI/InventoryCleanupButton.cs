using LostMemory.Relics;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// CL-151: 인벤토리 "정리" 버튼 컨트롤러.
    ///
    /// Button.onClick → PlayerRelicInventory.Sort(mode) 호출.
    /// 노소연이 InventoryPanel.prefab 의 정리 버튼 GameObject 에 부착하고 슬롯 wiring.
    ///
    /// Mode 디폴트 = RarityThenSize (등급 ↓ 후 사이즈 ↓). 후속 ticket 에서 토글 UI.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Inventory Cleanup Button")]
    public class InventoryCleanupButton : MonoBehaviour
    {
        [Tooltip("정리 대상 인벤토리. 보통 Player 의 PlayerRelicInventory.")]
        [SerializeField] private PlayerRelicInventory inventory;

        [Tooltip("정렬 기준. 디폴트 RarityThenSize (등급 ↓, 같은 등급 내 사이즈 ↓).")]
        [SerializeField] private InventorySortMode mode = InventorySortMode.RarityThenSize;

        [Tooltip("연결된 Button. Reset 시 자동 검색.")]
        [SerializeField] private Button button;

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            if (inventory == null)
            {
                Debug.LogWarning("[InventoryCleanupButton] inventory null — wiring 필요.");
                return;
            }
            inventory.Sort(mode);
        }
    }
}

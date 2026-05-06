using LostMemory.Relics;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.Relics
{
    /// <summary>
    /// CL-151: 인벤토리 그리드 상태 콘솔 출력 Editor 메뉴 (Play Mode 검증 도구).
    ///
    /// PlayerRelicInventory 의 ContextMenu 와 동일 기능이지만 메뉴바에서 빠르게 호출 가능.
    /// Play Mode 에서만 의미 있음 (Awake 에서 그리드 초기화).
    /// </summary>
    public static class InventoryGridDebugMenu
    {
        [MenuItem("LostMemory/Relics/Debug — Print Inventory Grid")]
        public static void PrintGrid()
        {
            var inv = Object.FindFirstObjectByType<PlayerRelicInventory>();
            if (inv == null)
            {
                Debug.LogWarning("[CL-151] PlayerRelicInventory not in scene (Play Mode 진입 필요)");
                return;
            }
            // ContextMenu 메서드 사용 — 리플렉션으로 호출
            var mi = typeof(PlayerRelicInventory).GetMethod(
                "DebugPrintGrid",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (mi == null)
            {
                Debug.LogError("[CL-151] DebugPrintGrid 메서드를 찾을 수 없음");
                return;
            }
            mi.Invoke(inv, null);
        }
    }
}

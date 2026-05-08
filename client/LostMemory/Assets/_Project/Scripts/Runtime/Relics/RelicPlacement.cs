using System;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// CL-151: 인벤토리 5×5 그리드에 배치된 유물 1개의 위치 정보.
    ///
    /// `Origin` = 좌상단 셀 좌표 (x=column, y=row).
    /// `W`/`H` 는 RelicData.Width/Height 그대로 — 별도 캐시 X (RelicData 가 사이즈 source-of-truth).
    ///
    /// PlayerRelicInventory 가 List&lt;RelicPlacement&gt; 형태로 보유, UI (노소연 InventoryPanelView) 가 직접 순회.
    /// </summary>
    [Serializable]
    public struct RelicPlacement
    {
        public RelicData  Relic;
        public Vector2Int Origin;   // 좌상단 셀 (x=col, y=row)

        public int X => Origin.x;
        public int Y => Origin.y;
        public int W => Relic != null ? Relic.Width  : 0;
        public int H => Relic != null ? Relic.Height : 0;
    }
}

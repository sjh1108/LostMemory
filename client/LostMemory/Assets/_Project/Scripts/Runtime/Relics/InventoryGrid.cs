using System;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// CL-151: 2D 셀 점유 그리드 + Top-left first fit 배치 알고리즘.
    ///
    /// 컨벤션: x = column (가로 인덱스), y = row (세로 인덱스). 원점 = 좌상단 (0,0).
    /// 5×5 인벤토리에서는 x∈[0,4], y∈[0,4].
    ///
    /// 복잡도: 5×5 그리드 + 2×2 아이템 → 25×4 = 100 ops max per item. 무시 가능.
    /// </summary>
    public class InventoryGrid
    {
        public int Cols { get; }
        public int Rows { get; }
        private readonly bool[,] _occupied;

        public InventoryGrid(int cols, int rows)
        {
            if (cols <= 0 || rows <= 0)
                throw new ArgumentException($"InventoryGrid 크기 양수여야 함 (cols={cols}, rows={rows})");
            Cols = cols;
            Rows = rows;
            _occupied = new bool[cols, rows];
        }

        /// <summary>모든 셀을 비점유로 초기화. Sort / Clear 시 사용.</summary>
        public void Reset() => Array.Clear(_occupied, 0, _occupied.Length);

        /// <summary>지정 영역의 점유 상태를 일괄 설정. value=true 점유, false 해제.</summary>
        public void Mark(int x, int y, int w, int h, bool value)
        {
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _occupied[x + dx, y + dy] = value;
        }

        /// <summary>(x,y) 좌상단 기준 w×h 영역이 그리드 내 + 비점유인지 검사.</summary>
        public bool CanFit(int x, int y, int w, int h)
        {
            if (x < 0 || y < 0 || x + w > Cols || y + h > Rows) return false;
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    if (_occupied[x + dx, y + dy]) return false;
            return true;
        }

        /// <summary>
        /// Top-left first fit — y=0..Rows-H, x=0..Cols-W 순서로 첫 fit 위치를 찾고 자동 Mark.
        /// 성공 시 origin 에 좌상단 좌표 반환, 실패 시 default + false.
        /// </summary>
        public bool TryPlace(RelicData relic, out Vector2Int origin)
        {
            if (relic == null) { origin = default; return false; }
            int W = relic.Width, H = relic.Height;
            if (W <= 0 || H <= 0) { origin = default; return false; }

            for (int y = 0; y <= Rows - H; y++)
            for (int x = 0; x <= Cols - W; x++)
            {
                if (CanFit(x, y, W, H))
                {
                    Mark(x, y, W, H, true);
                    origin = new Vector2Int(x, y);
                    return true;
                }
            }
            origin = default;
            return false;
        }

        /// <summary>디버그용 — 셀 점유 여부 직접 조회. 일반 사용은 CanFit/TryPlace 권장.</summary>
        public bool IsOccupied(int x, int y) =>
            x >= 0 && y >= 0 && x < Cols && y < Rows && _occupied[x, y];
    }
}

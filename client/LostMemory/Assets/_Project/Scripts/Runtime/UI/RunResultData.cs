namespace LostMemory.UI
{
    /// <summary>
    /// 런 결산 창에 표시할 데이터 묶음.
    /// 런 종료 시점에 채워서 RunResultPanelView.Show()에 넘긴다.
    /// </summary>
    public struct RunResultData
    {
        /// <summary>처치한 일반 적 수</summary>
        public int KillCount;

        /// <summary>처치한 보스 수</summary>
        public int BossKillCount;

        /// <summary>런 전체에서 가한 총 데미지</summary>
        public int TotalDamage;

        /// <summary>플레이 시간 (초 단위)</summary>
        public float PlayTime;

        /// <summary>이번 런에서 획득한 기억의 파편</summary>
        public int MemoryFragments;
    }
}

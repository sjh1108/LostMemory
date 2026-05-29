using System.Collections.Generic;

namespace LostMemory.UI.Status
{
    /// <summary>
    /// 플레이어 상태창 패널이 표시할 데이터 모델. Presenter 가 매번 새로 만들어 View 에 넘긴다.
    /// MonoBehaviour 아님 — 순수 POCO. 직렬화 대상 X.
    /// </summary>
    public sealed class StatusPanelViewModel
    {
        public readonly List<StatRow> Stats = new();
        public readonly List<EffectRow> OnHits = new();

        public void Clear()
        {
            Stats.Clear();
            OnHits.Clear();
        }

        /// <summary>합산 스탯 한 행. 예: ("공격력", "+27%", "공격 시 데미지 배율")</summary>
        public readonly struct StatRow
        {
            public readonly string Name;
            public readonly string TotalDisplay;
            public readonly string Description;

            public StatRow(string name, string totalDisplay, string description)
            {
                Name = name;
                TotalDisplay = totalDisplay;
                Description = description;
            }
        }

        /// <summary>OnHit 효과 한 행. 예: ("적중 시 화상", "공격 시 적에게 ATK 10% 화상 5초")</summary>
        public readonly struct EffectRow
        {
            public readonly string Name;
            public readonly string Description;

            public EffectRow(string name, string description)
            {
                Name = name;
                Description = description;
            }
        }
    }
}

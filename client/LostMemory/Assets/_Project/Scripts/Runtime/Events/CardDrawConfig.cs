using UnityEngine;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-227 V2 — 카드 뽑기 도박 설정 SO.
    /// 입장료 + 3가지 결과 (유지/증가/감소) 정의. 3장 카드에 1개씩 배정 후 위치 셔플.
    /// </summary>
    [CreateAssetMenu(fileName = "CardDrawConfig", menuName = "LostMemory/Events/Card Draw Config")]
    public sealed class CardDrawConfig : ScriptableObject
    {
        [Header("입장료")]
        [SerializeField, Min(1)] private int entryCost = 100;

        [Header("결과 (3개 — 1유지 + 1증가 + 1감소)")]
        [SerializeField] private CardDrawOutcomeEntry keepOutcome = new CardDrawOutcomeEntry
        {
            Kind = CardDrawOutcomeKind.Keep,
            ReturnGold = 100,
            DisplayLabel = "유지",
            LabelColor = new Color(0.9f, 0.9f, 0.9f)
        };

        [SerializeField] private CardDrawOutcomeEntry increaseOutcome = new CardDrawOutcomeEntry
        {
            Kind = CardDrawOutcomeKind.Increase,
            ReturnGold = 250,
            DisplayLabel = "증가!",
            LabelColor = new Color(0.96f, 0.77f, 0.26f)
        };

        [SerializeField] private CardDrawOutcomeEntry decreaseOutcome = new CardDrawOutcomeEntry
        {
            Kind = CardDrawOutcomeKind.Decrease,
            ReturnGold = 0,
            DisplayLabel = "감소...",
            LabelColor = new Color(0.85f, 0.30f, 0.30f)
        };

        public int EntryCost => entryCost;
        public CardDrawOutcomeEntry KeepOutcome => keepOutcome;
        public CardDrawOutcomeEntry IncreaseOutcome => increaseOutcome;
        public CardDrawOutcomeEntry DecreaseOutcome => decreaseOutcome;

        /// <summary>3개 outcome 을 배열로 반환 (셔플 입력용).</summary>
        public CardDrawOutcomeEntry[] GetAllOutcomes()
        {
            return new[] { keepOutcome, increaseOutcome, decreaseOutcome };
        }
    }
}

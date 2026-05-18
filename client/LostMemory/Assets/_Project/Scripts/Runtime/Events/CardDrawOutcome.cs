using UnityEngine;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-227 V2 — 카드 뽑기 결과 종류.
    /// 3장 카드 중 정확히 1장씩 배정 (1유지 + 1증가 + 1감소).
    /// </summary>
    public enum CardDrawOutcomeKind
    {
        Keep = 0,       // 입장료 본전 회수 — net 0
        Increase = 1,   // 잭팟 — net positive
        Decrease = 2    // 손실 — net negative
    }

    /// <summary>
    /// 결과 1개의 데이터 묶음. CardDrawConfig 에서 3개 (유지/증가/감소) 정의.
    /// </summary>
    [System.Serializable]
    public struct CardDrawOutcomeEntry
    {
        [Tooltip("결과 종류 — 분류용. 실제 보상 금액은 returnGold.")]
        public CardDrawOutcomeKind Kind;

        [Tooltip("입장료 포함 *총 회수액*. 100 = 본전, 0 = 전액 손실, 250 = 잭팟. net = returnGold - entryCost.")]
        public int ReturnGold;

        [Tooltip("UI 표시 라벨. 예: '유지', '증가!', '감소...'")]
        public string DisplayLabel;

        [Tooltip("라벨 색상 (UI 강조용). 보통 흰색/금색/빨강.")]
        public Color LabelColor;
    }
}

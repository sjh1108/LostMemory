using LostMemory.Shop;
using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// 미소녀 GameObject에 부착. 마우스 호버 시 visual별 숨은 능력 설명을 TooltipView로 노출.
    /// MagicalGirlSpawner.AddGirlByVisual 이 spawn 시점에 자동 부착.
    /// </summary>
    [DisallowMultipleComponent]
    public class MagicalGirlHoverTooltip : MonoBehaviour
    {
        [SerializeField] private MagicalGirlVisual _visual = MagicalGirlVisual.Default;

        public void SetVisual(MagicalGirlVisual v) => _visual = v;

        private void OnMouseEnter()
        {
            TooltipView tip = TooltipView.EnsureInstance();
            if (tip == null) return;
            tip.ShowMagicalGirl(_visual, Input.mousePosition);
        }

        private void OnMouseOver()
        {
            if (TooltipView.Instance != null)
                TooltipView.Instance.UpdatePosition(Input.mousePosition);
        }

        private void OnMouseExit()
        {
            if (TooltipView.Instance != null)
                TooltipView.Instance.Hide();
        }

        private void OnDisable()
        {
            if (TooltipView.Instance != null)
                TooltipView.Instance.Hide();
        }
    }

    /// <summary>visual별 표시명/숨은 능력 설명 매핑. UI 텍스트 변경은 여기만 손대면 됨.</summary>
    public static class MagicalGirlTooltipText
    {
        public static (string name, string desc) For(MagicalGirlVisual v) => v switch
        {
            MagicalGirlVisual.Fire => (
                "불의 미소녀",
                "화염 투사체로 적을 지속적으로 태웁니다.\n" +
                "<color=#FFB060>숨은 능력</color>: 명중 시 추가 화상 도트(시간당 피해)."),
            MagicalGirlVisual.Ice => (
                "얼음의 미소녀",
                "얼음 장판을 깔아 적의 이동을 둔화시킵니다.\n" +
                "<color=#9AD8FF>숨은 능력</color>: 장판 중첩 시 둔화 효과가 누적됩니다."),
            MagicalGirlVisual.Star => (
                "별의 미소녀",
                "별 투사체로 원거리 적을 견제합니다.\n" +
                "<color=#FFE070>숨은 능력</color>: 명중 시 주변에 별이 잔류해 추가 피해."),
            MagicalGirlVisual.Blackhole => (
                "어둠의 미소녀",
                "블랙홀 장판을 생성해 적을 끌어당깁니다.\n" +
                "<color=#C080FF>숨은 능력</color>: 흡인된 적은 일정 시간 정지 상태가 됩니다."),
            MagicalGirlVisual.Arrow => (
                "빛의 미소녀",
                "화살 형태의 빛/번개 투사체를 발사합니다.\n" +
                "<color=#80FFD0>숨은 능력</color>: 합류한 미소녀들이 많을수록 발사 속도가 증가합니다."),
            _ => ("마법소녀", "(설명 없음)"),
        };
    }
}

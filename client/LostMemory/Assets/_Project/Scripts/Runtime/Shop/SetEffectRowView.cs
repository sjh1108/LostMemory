using LostMemory.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Shop
{
    /// <summary>
    /// 세트효과 패널의 1행 — 한 BuildSetData 의 이름/카운트/효과를 표시한다.
    /// 활성/비활성 시각 강조는 배경 알파 + 텍스트 색으로 구분.
    /// 데이터 바인딩은 SetEffectPanelView 가 매 Refresh 마다 Bind() 호출로 갱신.
    /// </summary>
    public class SetEffectRowView : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private TextMeshProUGUI _effectText;

        [Header("배경 강조 (활성/비활성)")]
        [SerializeField] private Image _backgroundImage;

        [Header("색상")]
        [Tooltip("활성 티어가 1개 이상 발동 중일 때 배경/텍스트 알파.")]
        [SerializeField] private float _activeAlpha = 1f;
        [Tooltip("미발동 세트의 배경/텍스트 알파.")]
        [SerializeField] private float _inactiveAlpha = 0.4f;

        /// <summary>
        /// 1세트 정보 바인딩.
        /// </summary>
        /// <param name="set">표시할 BuildSetData (DisplayName / Tiers 등 사용).</param>
        /// <param name="count">현재 보유 카운트 (BuildManager.GetTagCount).</param>
        /// <param name="activeTier">현재 활성 티어 인덱스. -1 = 미발동.</param>
        public void Bind(BuildSetData set, int count, int activeTier)
        {
            if (set == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);

            bool isActive = activeTier >= 0;

            if (_nameText != null)
                _nameText.text = set.DisplayName;

            if (_countText != null)
                _countText.text = BuildCountLabel(set, count, activeTier);

            if (_effectText != null)
                _effectText.text = BuildEffectLabel(set, activeTier);

            float alpha = isActive ? _activeAlpha : _inactiveAlpha;
            ApplyAlpha(alpha);
        }

        private void ApplyAlpha(float alpha)
        {
            if (_backgroundImage != null)
            {
                Color c = _backgroundImage.color;
                c.a = alpha;
                _backgroundImage.color = c;
            }
            ApplyTextAlpha(_nameText, alpha);
            ApplyTextAlpha(_countText, alpha);
            ApplyTextAlpha(_effectText, alpha);
        }

        private static void ApplyTextAlpha(TextMeshProUGUI text, float alpha)
        {
            if (text == null) return;
            Color c = text.color;
            c.a = alpha;
            text.color = c;
        }

        // ── 라벨 빌더 ──────────────────────────────────────────────

        /// <summary>
        /// 카운트 라벨. set.Tiers 가 RequiredCount 오름차순이라는 CL-138 규약 가정.
        ///   미발동       → "{count}/{Tiers[0].RequiredCount}"
        ///   중간 티어    → "{count}/{Tiers[next].RequiredCount}"
        ///   최고 티어    → "{count}/MAX"
        /// Tiers 비어 있으면 카운트만 표시.
        /// </summary>
        private static string BuildCountLabel(BuildSetData set, int count, int activeTier)
        {
            var tiers = set.Tiers;
            if (tiers == null || tiers.Count == 0)
                return count.ToString();

            if (activeTier < 0)
                return $"{count}/{tiers[0].RequiredCount}";

            int nextIdx = activeTier + 1;
            if (nextIdx >= tiers.Count)
                return $"{count}/MAX";

            return $"{count}/{tiers[nextIdx].RequiredCount}";
        }

        /// <summary>
        /// 효과 라벨. 활성 티어가 있으면 그 Description, 없으면 "미발동".
        /// </summary>
        private static string BuildEffectLabel(BuildSetData set, int activeTier)
        {
            if (activeTier < 0) return "미발동";
            var tiers = set.Tiers;
            if (tiers == null || activeTier >= tiers.Count) return "";
            string desc = tiers[activeTier].Description;
            return string.IsNullOrEmpty(desc) ? "(설명 없음)" : desc;
        }
    }
}

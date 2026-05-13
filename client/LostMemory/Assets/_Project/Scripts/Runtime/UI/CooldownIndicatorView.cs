using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// 단일 능력의 쿨다운을 라디얼(원형) 진행률 + 카운트다운 텍스트로 표시하는 뷰.
    /// 패링/대시/스킬 등 어느 능력이든 재사용 가능. 로직은 Presenter 가 담당하고
    /// 이 컴포넌트는 setter 만 노출 — HealthBarView 와 동일한 톤.
    ///
    /// 사용:
    ///   - `fillImage.Image type=Filled, Method=Radial360, Origin=Top, Clockwise=true` 권장.
    ///   - readyAlpha/cooldownAlpha 로 dim overlay 효과 (cooldown 중 아이콘 어둡게).
    /// </summary>
    [AddComponentMenu("Lost Memory/UI/Cooldown Indicator View")]
    public class CooldownIndicatorView : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("쿨다운 진행률을 표시할 Image (Filled / Radial360 권장).")]
        [SerializeField] private Image fillImage;
        [Tooltip("능력 아이콘 (cooldown 중 어둡게 처리됨).")]
        [SerializeField] private Image iconImage;
        [Tooltip("남은 시간 텍스트 (소수점 1자리). 비워두면 표시 안 함.")]
        [SerializeField] private TextMeshProUGUI remainingText;
        [Tooltip("전체 보임/숨김 토글용. 비워두면 GameObject.SetActive 로 fallback.")]
        [SerializeField] private CanvasGroup rootGroup;

        [Header("Style")]
        [Range(0f, 1f)] [SerializeField] private float readyAlpha = 1f;
        [Range(0f, 1f)] [SerializeField] private float cooldownAlpha = 0.4f;
        [Tooltip("Ready 상태일 때 fill 의 색 (보통 채워진 채 살짝 강조).")]
        [SerializeField] private Color readyFillColor = new Color(1f, 1f, 1f, 0.2f);
        [Tooltip("Cooldown 상태일 때 fill 의 색.")]
        [SerializeField] private Color cooldownFillColor = new Color(0f, 0f, 0f, 0.6f);

        /// <summary>Ready 상태로 표시 (fill 숨김, text 숨김, 아이콘 풀 컬러).</summary>
        public void SetReady()
        {
            if (fillImage != null)
            {
                // Ready 시엔 fill Image 렌더링 자체를 끔. SetCooldown 에서 다시 켬.
                fillImage.enabled = false;
            }

            if (iconImage != null)
            {
                Color c = iconImage.color;
                c.a = readyAlpha;
                iconImage.color = c;
            }

            if (remainingText != null)
            {
                remainingText.text = string.Empty;
            }
        }

        /// <summary>
        /// 쿨다운 진행 중 표시.
        /// </summary>
        /// <param name="progress01">0=막 시작, 1=곧 끝남. fill 이 0→1 로 차오름.</param>
        /// <param name="remainingSeconds">남은 시간 (초). 0 이하면 텍스트 숨김.</param>
        public void SetCooldown(float progress01, float remainingSeconds)
        {
            float clamped = Mathf.Clamp01(progress01);

            if (fillImage != null)
            {
                // SetReady 에서 비활성화했던 fill 다시 켬.
                fillImage.enabled = true;
                // fill 은 "남은 쿨다운" 시각화 — 1에서 progress 만큼 빠짐 (반시계로 비워지는 느낌).
                // 여기선 "쿨다운 게이지가 줄어드는" 직관적 표현 = 1 - progress.
                fillImage.fillAmount = 1f - clamped;
                fillImage.color = cooldownFillColor;
            }

            if (iconImage != null)
            {
                Color c = iconImage.color;
                c.a = cooldownAlpha;
                iconImage.color = c;
            }

            if (remainingText != null)
            {
                remainingText.text = remainingSeconds > 0.05f
                    ? remainingSeconds.ToString("F1")
                    : string.Empty;
            }
        }

        /// <summary>전체 표시/숨김. CanvasGroup 있으면 alpha, 없으면 GameObject.SetActive.</summary>
        public void SetVisible(bool visible)
        {
            if (rootGroup != null)
            {
                rootGroup.alpha = visible ? 1f : 0f;
                rootGroup.interactable = visible;
                rootGroup.blocksRaycasts = visible;
                return;
            }

            gameObject.SetActive(visible);
        }
    }
}

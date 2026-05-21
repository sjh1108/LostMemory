using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// CL-234 (A-10): 피격 시 화면 가장자리에 선혈 vignette 효과.
    ///
    /// 사용법:
    /// 1) Screen Space - Overlay Canvas 위 풀스크린 Image 에 본 컴포넌트 부착.
    /// 2) Image 의 스프라이트는 가장자리만 빨간 그라데이션 (또는 단순 빨강 + 라디얼 마스크).
    ///    임시로 단색(빨강 + 알파) 도 동작. 디자이너 자산은 후속에서 교체.
    /// 3) 외부에서 <see cref="Pulse"/> 호출 시 fadeIn → hold → fadeOut 으로 알파 애니메이션.
    ///
    /// 본 컴포넌트는 입력 차단 없이 화면 위에만 표시되므로 Image.raycastTarget = false 권장.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Hit Vignette Overlay")]
    [RequireComponent(typeof(Image))]
    public sealed class HitVignetteOverlay : MonoBehaviour
    {
        [SerializeField] private Image image;

        [Header("Default Pulse Timing (Pulse() 인자 미지정 시 사용)")]
        [SerializeField, Min(0f)] private float defaultFadeIn = 0.12f;
        [SerializeField, Min(0f)] private float defaultHold = 0.10f;
        [SerializeField, Min(0f)] private float defaultFadeOut = 0.45f;
        [SerializeField, Range(0f, 1f)] private float defaultPeakAlpha = 0.55f;

        [Header("Color")]
        [Tooltip("스프라이트가 단색일 경우 이 색상이 곱해짐. RGB 만 사용, A 는 런타임 계산.")]
        [SerializeField] private Color tint = new Color(0.85f, 0.05f, 0.05f, 1f);

        private Coroutine _routine;

        private void Reset()
        {
            image = GetComponent<Image>();
            if (image != null) image.raycastTarget = false;
        }

        private void Awake()
        {
            if (image == null) image = GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
                SetAlpha(0f);
            }
        }

        /// <summary>기본 타이밍으로 1회 vignette 펄스.</summary>
        public void Pulse()
        {
            Pulse(defaultPeakAlpha, defaultFadeIn, defaultHold, defaultFadeOut);
        }

        /// <summary>커스텀 타이밍으로 1회 vignette 펄스. 이전 펄스가 진행 중이면 즉시 중단 후 새 펄스.</summary>
        public void Pulse(float peakAlpha, float fadeIn, float hold, float fadeOut)
        {
            if (image == null || !isActiveAndEnabled) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(PulseCoroutine(peakAlpha, fadeIn, hold, fadeOut));
        }

        private IEnumerator PulseCoroutine(float peakAlpha, float fadeIn, float hold, float fadeOut)
        {
            // Fade in
            float t = 0f;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(0f, peakAlpha, fadeIn > 0f ? t / fadeIn : 1f));
                yield return null;
            }
            SetAlpha(peakAlpha);

            // Hold
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

            // Fade out
            t = 0f;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(peakAlpha, 0f, fadeOut > 0f ? t / fadeOut : 1f));
                yield return null;
            }
            SetAlpha(0f);
            _routine = null;
        }

        private void SetAlpha(float a)
        {
            Color c = tint;
            c.a = Mathf.Clamp01(a);
            image.color = c;
        }
    }
}

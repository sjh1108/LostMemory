using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Intro.Phase0
{
    /// <summary>
    /// 인트로 시작 후 지정한 시간에 배경을 페이드인/아웃하는 단순 타이머.
    /// 스프라이트 배열을 지정하면 8프레임 애니메이션도 함께 재생.
    /// </summary>
    [AddComponentMenu("Lost Memory/Intro/Phase0 Timed Backdrop")]
    public sealed class Phase0TimedBackdrop : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image image;

        [Header("스프라이트 루프 (8프레임 애니메이션)")]
        [Tooltip("재생할 스프라이트 배열. 비워두면 Image 그대로 사용.")]
        [SerializeField] private Sprite[] frames;
        [Tooltip("초당 프레임 수")]
        [SerializeField] private float fps = 1f;

        [Header("페이드인")]
        [Tooltip("씬 시작 후 몇 초 뒤에 등장할지")]
        [SerializeField] private float fadeInDelay = 10f;
        [Tooltip("페이드인에 걸리는 시간(초)")]
        [SerializeField] private float fadeInDuration = 1f;

        [Header("페이드아웃")]
        [SerializeField] private bool useFadeOut = false;
        [Tooltip("씬 시작 후 몇 초 뒤에 사라질지 (fadeInDelay보다 커야 함)")]
        [SerializeField] private float fadeOutDelay = 20f;
        [Tooltip("페이드아웃에 걸리는 시간(초)")]
        [SerializeField] private float fadeOutDuration = 1f;

        private Coroutine _loopRoutine;

        private void Start()
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            // 페이드인 대기
            if (fadeInDelay > 0f)
                yield return new WaitForSecondsRealtime(fadeInDelay);

            // 스프라이트 루프 시작 (프레임이 있을 때만)
            if (frames != null && frames.Length > 0 && image != null)
                _loopRoutine = StartCoroutine(LoopFrames());

            // 페이드인
            yield return StartCoroutine(Fade(0f, 1f, fadeInDuration));

            // 페이드아웃 (선택)
            if (useFadeOut)
            {
                float wait = fadeOutDelay - fadeInDelay - fadeInDuration;
                if (wait > 0f)
                    yield return new WaitForSecondsRealtime(wait);

                yield return StartCoroutine(Fade(1f, 0f, fadeOutDuration));

                // 페이드아웃 완료 후 루프 중단
                if (_loopRoutine != null)
                {
                    StopCoroutine(_loopRoutine);
                    _loopRoutine = null;
                }
            }
        }

        private IEnumerator LoopFrames()
        {
            float interval = 1f / Mathf.Max(0.01f, fps);
            int index = 0;
            while (true)
            {
                image.sprite = frames[index];
                index = (index + 1) % frames.Length;
                yield return new WaitForSecondsRealtime(interval);
            }
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (canvasGroup == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            canvasGroup.alpha = to;
        }
    }
}

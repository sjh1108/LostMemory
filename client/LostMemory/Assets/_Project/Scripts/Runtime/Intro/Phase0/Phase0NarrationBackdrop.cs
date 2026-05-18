using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Intro.Phase0
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    [AddComponentMenu("Lost Memory/Intro/Phase0 Narration Backdrop")]
    public sealed class Phase0NarrationBackdrop : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private CanvasGroup canvasGroup;

        private Coroutine _swapRoutine;
        private Coroutine _fadeRoutine;

        private void Reset()
        {
            image = GetComponent<Image>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Awake()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        public IEnumerator FadeIn(float duration)
        {
            if (canvasGroup == null) yield break;
            StopFade();
            _fadeRoutine = StartCoroutine(FadeTo(1f, duration));
            yield return _fadeRoutine;
        }

        public IEnumerator FadeOut(float duration)
        {
            if (canvasGroup == null) yield break;
            StopFade();
            _fadeRoutine = StartCoroutine(FadeTo(0f, duration));
            yield return _fadeRoutine;
        }

        public void StartLoop(Sprite[] frames, float fps)
        {
            StopLoop();
            if (image == null || frames == null || frames.Length == 0) return;
            _swapRoutine = StartCoroutine(LoopFrames(frames, fps));
        }

        public void StopLoop()
        {
            if (_swapRoutine != null)
            {
                StopCoroutine(_swapRoutine);
                _swapRoutine = null;
            }
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float start = canvasGroup.alpha;
            if (duration <= 0f)
            {
                canvasGroup.alpha = target;
                _fadeRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            canvasGroup.alpha = target;
            _fadeRoutine = null;
        }

        private void StopFade()
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }
        }

        private IEnumerator LoopFrames(Sprite[] frames, float fps)
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
    }
}

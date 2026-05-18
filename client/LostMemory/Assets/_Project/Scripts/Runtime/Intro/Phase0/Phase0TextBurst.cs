using System.Collections;
using TMPro;
using UnityEngine;

namespace LostMemory.Intro.Phase0
{
    /// <summary>
    /// JPop 뮤비 스타일 텍스트 연출용.
    /// TMP 오브젝트를 씬에 원하는 위치/회전/크기로 배치한 뒤 이 컴포넌트를 붙이면
    /// 지정한 시간에 등장하고 선택적으로 사라짐.
    /// </summary>
    [AddComponentMenu("Lost Memory/Intro/Phase0 Text Burst")]
    public sealed class Phase0TextBurst : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("타이밍")]
        [Tooltip("씬 시작 후 몇 초 뒤에 등장할지")]
        [SerializeField] private float appearDelay = 5f;

        [Header("등장 방식")]
        [SerializeField] private AppearMode appearMode = AppearMode.Instant;
        [Tooltip("FadeIn 선택 시 페이드인 시간(초)")]
        [SerializeField] private float fadeInDuration = 0.3f;

        [Header("퇴장 (Use Auto Hide 체크 시)")]
        [SerializeField] private bool useAutoHide = false;
        [Tooltip("등장 후 몇 초 뒤에 사라질지")]
        [SerializeField] private float hideAfter = 2f;
        [SerializeField] private HideMode hideMode = HideMode.Instant;
        [Tooltip("FadeOut 선택 시 페이드아웃 시간(초)")]
        [SerializeField] private float fadeOutDuration = 0.3f;

        public enum AppearMode { Instant, FadeIn }
        public enum HideMode   { Instant, FadeOut }

        private void Start()
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            // 대기
            if (appearDelay > 0f)
                yield return new WaitForSecondsRealtime(appearDelay);

            // 등장
            switch (appearMode)
            {
                case AppearMode.Instant:
                    if (canvasGroup != null) canvasGroup.alpha = 1f;
                    break;
                case AppearMode.FadeIn:
                    yield return StartCoroutine(Fade(0f, 1f, fadeInDuration));
                    break;
            }

            // 자동 숨김
            if (useAutoHide)
            {
                yield return new WaitForSecondsRealtime(hideAfter);

                switch (hideMode)
                {
                    case HideMode.Instant:
                        if (canvasGroup != null) canvasGroup.alpha = 0f;
                        break;
                    case HideMode.FadeOut:
                        yield return StartCoroutine(Fade(1f, 0f, fadeOutDuration));
                        break;
                }
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

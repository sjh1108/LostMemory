using System.Collections;
using TMPro;
using UnityEngine;

namespace LostMemory.Intro.Phase0
{
    /// <summary>
    /// 지정한 시간에 챕터 타이틀을 페이드인 → 유지 → 페이드아웃.
    /// Canvas 안에 별도 TMP 오브젝트를 만들고 이 컴포넌트를 붙여서 사용.
    /// </summary>
    [AddComponentMenu("Lost Memory/Intro/Phase0 Timed Title")]
    public sealed class Phase0TimedTitle : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("텍스트")]
        [Tooltip("화면에 표시할 타이틀 문구")]
        [SerializeField] private string titleText = "Chapter 1.  n번째 기억";

        [Header("타이밍")]
        [Tooltip("씬 시작 후 몇 초 뒤에 등장할지")]
        [SerializeField] private float appearDelay = 5f;
        [Tooltip("페이드인에 걸리는 시간(초)")]
        [SerializeField] private float fadeInDuration = 0.8f;
        [Tooltip("완전히 보인 상태로 유지하는 시간(초)")]
        [SerializeField] private float holdDuration = 2.0f;
        [Tooltip("페이드아웃에 걸리는 시간(초)")]
        [SerializeField] private float fadeOutDuration = 0.8f;

        private void Start()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (label != null) label.text = titleText;
            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            if (appearDelay > 0f)
                yield return new WaitForSecondsRealtime(appearDelay);

            // 페이드인
            yield return StartCoroutine(Fade(0f, 1f, fadeInDuration));

            // 유지
            yield return new WaitForSecondsRealtime(holdDuration);

            // 페이드아웃
            yield return StartCoroutine(Fade(1f, 0f, fadeOutDuration));
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

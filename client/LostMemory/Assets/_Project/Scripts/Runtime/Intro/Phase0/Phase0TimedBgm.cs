using System.Collections;
using UnityEngine;

namespace LostMemory.Intro.Phase0
{
    /// <summary>
    /// 지정한 시간에 BGM을 페이드인하고, 선택적으로 페이드아웃.
    /// BGM을 씬 중간에 전환하고 싶을 때 AudioSource마다 하나씩 붙여서 사용.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Lost Memory/Intro/Phase0 Timed BGM")]
    public sealed class Phase0TimedBgm : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private AudioSource audioSource;

        [Header("타이밍")]
        [Tooltip("씬 시작 후 몇 초 뒤에 재생을 시작할지")]
        [SerializeField] private float startDelay = 0f;
        [Tooltip("페이드인에 걸리는 시간(초). 0이면 즉시 최대 볼륨.")]
        [SerializeField] private float fadeInDuration = 2f;
        [Tooltip("목표 볼륨 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float targetVolume = 0.5f;

        [Header("페이드아웃 (Use Fade Out 체크 시 활성화)")]
        [SerializeField] private bool useFadeOut = false;
        [Tooltip("씬 시작 후 몇 초 뒤에 페이드아웃을 시작할지")]
        [SerializeField] private float fadeOutDelay = 60f;
        [Tooltip("페이드아웃에 걸리는 시간(초)")]
        [SerializeField] private float fadeOutDuration = 2f;

        private void Reset()
        {
            audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            if (audioSource != null)
            {
                audioSource.volume = 0f;
                audioSource.loop = true;
                audioSource.playOnAwake = false;
            }
            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            if (startDelay > 0f)
                yield return new WaitForSecondsRealtime(startDelay);

            if (audioSource != null)
                audioSource.Play();

            // 페이드인
            yield return StartCoroutine(FadeVolume(0f, targetVolume, fadeInDuration));

            // 페이드아웃 (선택)
            if (useFadeOut)
            {
                float wait = fadeOutDelay - startDelay - fadeInDuration;
                if (wait > 0f)
                    yield return new WaitForSecondsRealtime(wait);

                yield return StartCoroutine(FadeVolume(targetVolume, 0f, fadeOutDuration));

                if (audioSource != null)
                    audioSource.Stop();
            }
        }

        private IEnumerator FadeVolume(float from, float to, float duration)
        {
            if (audioSource == null) yield break;
            if (duration <= 0f) { audioSource.volume = to; yield break; }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                audioSource.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            audioSource.volume = to;
        }
    }
}

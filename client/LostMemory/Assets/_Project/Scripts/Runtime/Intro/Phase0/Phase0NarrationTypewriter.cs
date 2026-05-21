using System.Collections;
using TMPro;
using UnityEngine;

namespace LostMemory.Intro.Phase0
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Intro/Phase0 Narration Typewriter")]
    public sealed class Phase0NarrationTypewriter : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private AudioSource sfxSource;
        [Tooltip("타이핑음 재생 후 자동으로 끊는 시간(초). 연속음 클립에서 첫 번째 소리만 뽑을 때 사용.")]
        [SerializeField] private float sfxCutoffSeconds = 0.12f;

        private Coroutine _stopSfxCoroutine;

        private void Reset()
        {
            label = GetComponentInChildren<TMP_Text>();
            sfxSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            // 타이핑 SFX AudioSource 를 SFX mixer group 으로 라우팅 → 옵션 메뉴 SFX 슬라이더 영향.
            if (sfxSource != null
                && LostMemory.Audio.GameAudioSettings.Instance != null
                && LostMemory.Audio.GameAudioSettings.Instance.SfxGroup != null)
            {
                sfxSource.outputAudioMixerGroup =
                    LostMemory.Audio.GameAudioSettings.Instance.SfxGroup;
            }
        }

        public void Clear()
        {
            if (label == null) return;
            label.text = string.Empty;
            label.maxVisibleCharacters = 0;
        }

        public IEnumerator AppendTyped(string text, float charsPerSecond, Phase0IntroSequenceData data)
        {
            if (label == null || string.IsNullOrEmpty(text))
            {
                yield break;
            }

            string newline = label.text.Length == 0 ? string.Empty : "\n";

            // 추가 전에 현재 글자 수를 기록 — rich text 태그를 TMP가 알아서 제외하므로 태그 포함 text도 안전
            int startVisible;
            if (label.text.Length == 0)
            {
                startVisible = 0;
            }
            else
            {
                label.ForceMeshUpdate();
                startVisible = label.textInfo.characterCount + 1; // +1 은 \n 한 글자
            }

            label.text += newline + text;
            label.ForceMeshUpdate();
            int targetVisible = label.textInfo.characterCount;
            label.maxVisibleCharacters = startVisible;

            float interval = 1f / Mathf.Max(1f, charsPerSecond);
            int charsSinceSfx = 0;
            int sfxEvery = Mathf.Max(1, data != null ? data.PlayEveryNCharacters : 1);

            while (label.maxVisibleCharacters < targetVisible)
            {
                label.maxVisibleCharacters++;
                charsSinceSfx++;

                if (data != null && data.TypeClip != null && charsSinceSfx >= sfxEvery)
                {
                    PlayTypeClick(data);
                    charsSinceSfx = 0;
                }

                yield return new WaitForSecondsRealtime(interval);
            }
        }

        public void AppendInstant(string text)
        {
            if (label == null || string.IsNullOrEmpty(text)) return;

            string newline = label.text.Length == 0 ? string.Empty : "\n";
            label.text += newline + text;
            label.maxVisibleCharacters = label.text.Length;
        }

        private void PlayTypeClick(Phase0IntroSequenceData data)
        {
            if (sfxSource == null || data.TypeClip == null) return;

            // 이전 자동-중지 예약이 있으면 취소
            if (_stopSfxCoroutine != null)
                StopCoroutine(_stopSfxCoroutine);

            float jitter = data.TypePitchJitter;
            sfxSource.pitch = jitter > 0f ? 1f + Random.Range(-jitter, jitter) : 1f;
            sfxSource.clip = data.TypeClip;
            sfxSource.volume = data.TypeVolume;
            sfxSource.Play();

            // sfxCutoffSeconds 후 자동으로 끊어 첫 번째 "두" 하나만 재생
            _stopSfxCoroutine = StartCoroutine(StopSfxAfter(sfxCutoffSeconds));
        }

        private IEnumerator StopSfxAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            sfxSource.Stop();
            _stopSfxCoroutine = null;
        }
    }
}

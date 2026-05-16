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

        private void Reset()
        {
            label = GetComponentInChildren<TMP_Text>();
            sfxSource = GetComponent<AudioSource>();
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
            int startVisible = label.text.Length + newline.Length;
            label.text += newline + text;
            int targetVisible = label.text.Length;
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

            float jitter = data.TypePitchJitter;
            sfxSource.pitch = jitter > 0f ? 1f + Random.Range(-jitter, jitter) : 1f;
            sfxSource.PlayOneShot(data.TypeClip, data.TypeVolume);
        }
    }
}

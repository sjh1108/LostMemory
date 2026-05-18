using System.Collections;
using UnityEngine;
using TMPro;

namespace LostMemory.UI
{
    public class BlinkText : MonoBehaviour
    {
        [SerializeField] private float _fadeInDuration  = 0.5f;  // 페이드 인 시간
        [SerializeField] private float _onDuration      = 1.0f;  // 완전히 보이는 시간
        [SerializeField] private float _fadeOutDuration = 0.3f;  // 페이드 아웃 시간
        [SerializeField] private float _offDuration     = 0.1f;  // 완전히 사라진 시간

        private TextMeshProUGUI _text;

        void Start()
        {
            _text = GetComponent<TextMeshProUGUI>();
            StartCoroutine(Blink());
        }

        private IEnumerator Blink()
        {
            while (true)
            {
                // 페이드 인
                yield return Fade(0f, 1f, _fadeInDuration);

                // 완전히 보이는 구간
                yield return new WaitForSeconds(_onDuration);

                // 페이드 아웃
                yield return Fade(1f, 0f, _fadeOutDuration);

                // 완전히 사라진 구간
                yield return new WaitForSeconds(_offDuration);
            }
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                _text.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            _text.alpha = to;
        }
    }
}
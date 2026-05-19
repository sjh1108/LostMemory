using System.Collections;
using UnityEngine;

namespace LostMemory.Rewards
{
    /// <summary>
    /// 카드 등장 시 scale punch ("띠용") 효과.
    /// OnEnable 마다 scale 을 startScale → overshootScale → settleScale → 1.0 으로 보간.
    /// Back-out easing 으로 자연스러운 오버슈트.
    ///
    /// 사용:
    /// - RewardPanel 의 각 카드 GameObject 에 부착
    /// - 별도 셋업 불필요 (Inspector 기본값으로 동작)
    ///
    /// timeScale=0 환경(RewardController 일시정지) 호환 위해 unscaledDeltaTime 사용.
    /// </summary>
    [AddComponentMenu("Lost Memory/Rewards/Reward Card Entrance Punch")]
    [DisallowMultipleComponent]
    public class RewardCardEntrancePunch : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("효과 전체 지속 시간 (초, realtime).")]
        [SerializeField, Min(0.05f)] private float duration = 0.35f;

        [Tooltip("등장 시작 시 지연 (초). 슬롯별로 다른 값 주면 stagger 효과.")]
        [SerializeField, Min(0f)] private float startDelay = 0f;

        [Header("Scale")]
        [Tooltip("시작 scale (0 → 작은 점에서 튀어나옴).")]
        [SerializeField, Min(0f)] private float startScale = 0.01f;

        [Tooltip("오버슈트 강도. 1.7 정도가 적당한 boing, 더 크면 더 과장됨.")]
        [SerializeField, Min(0f)] private float overshoot = 1.7f;

        private Vector3 _baseScale = Vector3.one;
        private Coroutine _punchCoroutine;

        private void Awake()
        {
            // 부착 시점의 scale 을 최종 settle 값으로 캐시.
            _baseScale = transform.localScale;
            if (_baseScale == Vector3.zero) _baseScale = Vector3.one;
        }

        private void OnEnable()
        {
            if (_punchCoroutine != null) StopCoroutine(_punchCoroutine);
            _punchCoroutine = StartCoroutine(PunchCoroutine());
        }

        private void OnDisable()
        {
            if (_punchCoroutine != null)
            {
                StopCoroutine(_punchCoroutine);
                _punchCoroutine = null;
            }
            // 다음 활성화 때 깨끗하게 시작하도록 baseScale 로 복귀.
            transform.localScale = _baseScale;
        }

        private IEnumerator PunchCoroutine()
        {
            if (startDelay > 0f)
            {
                transform.localScale = _baseScale * startScale;
                yield return new WaitForSecondsRealtime(startDelay);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutBack(t, overshoot);
                float s = Mathf.LerpUnclamped(startScale, 1f, eased);
                transform.localScale = _baseScale * s;
                yield return null;
            }

            transform.localScale = _baseScale;
            _punchCoroutine = null;
        }

        /// <summary>
        /// Back-out easing — 1.0 직전에 살짝 오버슈트 후 정착.
        /// s=1.70158 이 표준 back ease (약 10% 오버슈트).
        /// </summary>
        private static float EaseOutBack(float t, float s)
        {
            float c3 = s + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + s * u * u;
        }
    }
}

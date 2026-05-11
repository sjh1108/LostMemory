using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-204: 단순 sin 펄스 — Awake 시점의 localScale 기준으로 ±pulseAmount 진폭 호흡.
    /// Blackhole / Ice / Star 등 정적 sprite 에 *살아 있는 느낌* 부여.
    ///
    /// 사용: GameObject 에 부착 → Inspector 에서 amount/speed 조정.
    /// 작동: Update 마다 transform.localScale = baseScale × (1 + sin(t × speed × 2π) × amount)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PulseScale : MonoBehaviour
    {
        [Tooltip("펄스 진폭. 0.1 = ±10% 크기 변화. 0 = 변화 없음.")]
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.1f;

        [Tooltip("펄스 속도 (사이클/초). 2 = 1초에 2번 호흡.")]
        [SerializeField, Min(0.01f)] private float pulseSpeed = 1.5f;

        [Tooltip("위상 오프셋 (0~1). 여러 PulseScale 가 *반대 위상* 으로 호흡하게 할 때 0.5 사용.")]
        [SerializeField, Range(0f, 1f)] private float phaseOffset = 0f;

        private Vector3 _baseScale;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            float t = Time.time * pulseSpeed + phaseOffset;
            float k = 1f + Mathf.Sin(t * Mathf.PI * 2f) * pulseAmount;
            transform.localScale = _baseScale * k;
        }
    }
}

using UnityEngine;

namespace LostMemory.VFX
{
    /// <summary>
    /// CL-202: 얼음 효과의 IceBlock 자식만 미세하게 떨리는 효과.
    /// FreezeStatusVFX 의 IceBlockGroup (Back/Front 묶은 빈 부모) 에 부착.
    /// 매 프레임 random 수직/수평 offset 으로 "지직" 거리는 결빙 진동 표현.
    /// 부모(루트)와 형제 ParticleSystem 은 영향 받지 않음 — 떨림과 부유가 분리됨.
    /// </summary>
    [AddComponentMenu("Lost Memory/VFX/Ice Shivering")]
    public sealed class IceShivering : MonoBehaviour
    {
        [Tooltip("최대 흔들림 진폭 (유닛). 너무 크면 멀미 — 0.02~0.04 권장.")]
        [SerializeField, Min(0f)] private float _amplitude = 0.03f;

        [Tooltip("초당 흔들림 빈도 (Hz). 25~35 권장 (빠른 떨림).")]
        [SerializeField, Min(1f)] private float _frequency = 30f;

        [Tooltip("켜면 unscaled time 사용 — hitstop / freeze 중에도 떨림 유지.")]
        [SerializeField] private bool _useUnscaledTime = true;

        private Vector3 _basePosition;
        private float _nextSampleAt;

        private void Awake()
        {
            _basePosition = transform.localPosition;
        }

        private void OnDisable()
        {
            transform.localPosition = _basePosition;
        }

        private void Update()
        {
            float now = _useUnscaledTime ? Time.unscaledTime : Time.time;
            if (now < _nextSampleAt) return;

            _nextSampleAt = now + (1f / _frequency);

            float ox = Random.Range(-_amplitude, _amplitude);
            float oy = Random.Range(-_amplitude, _amplitude);
            transform.localPosition = _basePosition + new Vector3(ox, oy, 0f);
        }
    }
}

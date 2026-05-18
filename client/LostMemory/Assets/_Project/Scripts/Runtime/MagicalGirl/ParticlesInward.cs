using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-204: ParticleSystem 의 모든 입자를 *중심 (local 0,0,0)* 으로 빨려들게 함.
    /// LateUpdate 에서 GetParticles → 각 입자의 velocity 를 중심 방향으로 설정 → SetParticles.
    ///
    /// 사용:
    /// 1. ParticleSystem 자식 GameObject 에 부착 (예: InwardFlow)
    /// 2. ParticleSystem 의 Shape 는 Sphere/Circle 로 외곽에서 spawn (예: Radius 1.5)
    /// 3. ParticleSystem 의 Simulation Space = Local (필수 — script 가 local 좌표 기준 빨려듦)
    /// 4. Lifetime 을 spawn 반경 ÷ pullSpeed 보다 약간 길게 두면 자연스럽게 코어 도달 후 사라짐
    ///
    /// 가속 옵션: useAcceleration = true 면 코어에 가까울수록 빠르게 (실제 중력 느낌).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ParticlesInward : MonoBehaviour
    {
        [Tooltip("입자가 코어로 향하는 속도 (유닛/초). 기본 2 = spawn 반경 1.5 라면 약 0.75초에 도달.")]
        [SerializeField, Min(0.1f)] private float pullSpeed = 2f;

        [Tooltip("ON 시 코어에 가까울수록 가속 (1/r^2 같은 효과의 단순 근사). 더 dramatic 한 빨려듦.")]
        [SerializeField] private bool useAcceleration = true;

        [Tooltip("가속 강도 (useAcceleration=true 일 때만). 4 = 거리 0.25 시점에 4배 속도.")]
        [SerializeField, Range(1f, 10f)] private float accelerationFactor = 4f;

        [Tooltip("코어 도달 임계 (유닛). 이 거리 안에 들어오면 입자 즉시 kill (사라짐).")]
        [SerializeField, Min(0f)] private float coreThreshold = 0.05f;

        private ParticleSystem _ps;
        private ParticleSystem.Particle[] _buf;

        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
        }

        private void LateUpdate()
        {
            if (_ps == null) return;

            int max = _ps.main.maxParticles;
            if (_buf == null || _buf.Length < max)
                _buf = new ParticleSystem.Particle[max];

            int n = _ps.GetParticles(_buf);
            for (int i = 0; i < n; i++)
            {
                Vector3 toCenter = -_buf[i].position;  // Simulation Space=Local 기준 (0,0,0) = 코어
                float dist = toCenter.magnitude;

                if (dist < coreThreshold)
                {
                    // 코어 도달 → 입자 kill
                    _buf[i].remainingLifetime = 0f;
                    continue;
                }

                Vector3 dir = toCenter / dist;  // normalized
                float speed = pullSpeed;
                if (useAcceleration)
                {
                    // 거리 1 = base speed, 거리 0.25 = ×accelerationFactor
                    float boost = Mathf.Lerp(1f, accelerationFactor, 1f - Mathf.Clamp01(dist));
                    speed *= boost;
                }
                _buf[i].velocity = dir * speed;
            }
            _ps.SetParticles(_buf, n);
        }
    }
}

using UnityEngine;

namespace LostMemory.VFX
{
    /// <summary>
    /// CL-201: LineRenderer 의 vertex 를 지그재그로 배치해 번개 모양을 낸다.
    /// Init(from, to) 호출 시 시작/끝점 사이를 (_segments+1) 개 점으로 분할,
    /// 중간 점들에 수직 방향 랜덤 jitter 를 부여.
    /// _refreshInterval &gt; 0 이면 매 N초마다 새 노이즈로 재생성 — 전기 깜빡임 효과.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [AddComponentMenu("Lost Memory/VFX/Jagged Lightning Line")]
    public sealed class JaggedLightningLine : MonoBehaviour
    {
        [Tooltip("선분 분할 수. 8~12 권장. 클수록 더 잘게 꺾임.")]
        [SerializeField, Min(2)] private int _segments = 8;

        [Tooltip("중간점 수직 오프셋 최대치 (유닛). 클수록 들쭉날쭉.")]
        [SerializeField, Min(0f)] private float _jitter = 0.3f;

        [Tooltip("재생성 간격 (초). 0 = 1회만 (정지). 0.05 정도면 깜빡이는 전기 느낌.")]
        [SerializeField, Min(0f)] private float _refreshInterval = 0.05f;

        [Tooltip("켜면 unscaled time 사용 — hitstop 중에도 깜빡임 진행.")]
        [SerializeField] private bool _useUnscaledTime = false;

        private LineRenderer _lr;
        private Vector3 _from;
        private Vector3 _to;
        private float _nextRefreshAt;
        private bool _initialized;

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
        }

        /// <summary>
        /// 외부(예: OnHitEffectRegistry.SpawnChainVFX)에서 시작/끝점 지정 후 호출.
        /// 즉시 1회 지그재그 생성 후, _refreshInterval 에 따라 주기 재생성.
        /// </summary>
        public void Init(Vector3 from, Vector3 to)
        {
            _from = from;
            _to = to;
            _initialized = true;
            Regenerate();
            ScheduleNextRefresh();
        }

        private void Update()
        {
            if (!_initialized) return;
            if (_refreshInterval <= 0f) return;

            float now = _useUnscaledTime ? Time.unscaledTime : Time.time;
            if (now >= _nextRefreshAt)
            {
                Regenerate();
                ScheduleNextRefresh();
            }
        }

        private void ScheduleNextRefresh()
        {
            float now = _useUnscaledTime ? Time.unscaledTime : Time.time;
            _nextRefreshAt = now + _refreshInterval;
        }

        private void Regenerate()
        {
            if (_lr == null) return;

            _lr.useWorldSpace = true;
            _lr.positionCount = _segments + 1;

            Vector3 dir = _to - _from;
            // 2D 평면(z=0) 기준 수직 단위 벡터
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f).normalized;

            for (int i = 0; i <= _segments; i++)
            {
                float t = i / (float)_segments;
                Vector3 basePos = Vector3.Lerp(_from, _to, t);
                // 끝점은 그대로 두고 중간 점만 흔든다
                if (i > 0 && i < _segments)
                {
                    float offset = Random.Range(-_jitter, _jitter);
                    basePos += perp * offset;
                }
                _lr.SetPosition(i, basePos);
            }
        }
    }
}

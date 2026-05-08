using UnityEngine;

namespace LostMemory.VFX
{
    /// <summary>
    /// CL-201: LineRenderer 의 vertex 를 지그재그로 배치해 번개 모양을 낸다.
    /// 두 가지 Init 모드:
    /// - Init(Vector3, Vector3): 정적 위치 (기존)
    /// - Init(Transform, Transform): 매 프레임 두 Transform 위치를 추적 — 적/플레이어 이동 시에도 라인이 따라옴
    /// _refreshInterval &gt; 0 이면 매 N초마다 새 노이즈 — 전기 깜빡임. 그 사이엔 동일 노이즈로 양 끝점만 이동.
    /// Transform 이 destroy 되면 마지막 위치를 유지 (graceful fallback).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [AddComponentMenu("Lost Memory/VFX/Jagged Lightning Line")]
    public sealed class JaggedLightningLine : MonoBehaviour
    {
        [Tooltip("선분 분할 수. 8~12 권장. 클수록 더 잘게 꺾임.")]
        [SerializeField, Min(2)] private int _segments = 8;

        [Tooltip("중간점 수직 오프셋 최대치 (유닛). 클수록 들쭉날쭉.")]
        [SerializeField, Min(0f)] private float _jitter = 0.3f;

        [Tooltip("노이즈 재생성 간격 (초). 0 = 1회만 (정지된 지그재그). 0.05 정도면 깜빡이는 전기 느낌.")]
        [SerializeField, Min(0f)] private float _refreshInterval = 0.05f;

        [Tooltip("켜면 unscaled time 사용 — hitstop 중에도 깜빡임 진행.")]
        [SerializeField] private bool _useUnscaledTime = false;

        private LineRenderer _lr;
        private Vector3 _from;
        private Vector3 _to;
        private Transform _fromTransform;
        private Transform _toTransform;
        private bool _trackTransforms;
        private float _nextRefreshAt;
        private bool _initialized;
        private float[] _perpOffsets;

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
        }

        /// <summary>정적 위치. 양 끝이 움직이지 않을 때 사용.</summary>
        public void Init(Vector3 from, Vector3 to)
        {
            _from = from;
            _to = to;
            _trackTransforms = false;
            _fromTransform = null;
            _toTransform = null;
            _initialized = true;
            Regenerate();
            ScheduleNextRefresh();
        }

        /// <summary>
        /// Transform 추적 모드. 매 프레임 fromTransform/toTransform 위치를 읽어 라인 양 끝점 갱신.
        /// 한쪽 또는 양쪽이 destroy 되면 마지막 알려진 위치 유지.
        /// </summary>
        public void Init(Transform fromTransform, Transform toTransform)
        {
            _fromTransform = fromTransform;
            _toTransform = toTransform;
            _trackTransforms = true;
            _from = fromTransform != null ? fromTransform.position : Vector3.zero;
            _to = toTransform != null ? toTransform.position : Vector3.zero;
            _from.z = 0f;
            _to.z = 0f;
            _initialized = true;
            Regenerate();
            ScheduleNextRefresh();
        }

        private void Update()
        {
            if (!_initialized) return;

            // Transform 추적 — 매 프레임 위치 갱신 (Transform null 이면 마지막 값 유지)
            bool positionsChanged = false;
            if (_trackTransforms)
            {
                if (_fromTransform != null)
                {
                    Vector3 p = _fromTransform.position;
                    p.z = 0f;
                    if (p != _from) { _from = p; positionsChanged = true; }
                }
                if (_toTransform != null)
                {
                    Vector3 p = _toTransform.position;
                    p.z = 0f;
                    if (p != _to) { _to = p; positionsChanged = true; }
                }
            }

            // 노이즈 재생성 (interval 단위)
            bool noiseRefreshed = false;
            if (_refreshInterval > 0f)
            {
                float now = _useUnscaledTime ? Time.unscaledTime : Time.time;
                if (now >= _nextRefreshAt)
                {
                    Regenerate();
                    ScheduleNextRefresh();
                    noiseRefreshed = true;
                }
            }

            // 위치만 바뀌고 노이즈는 그대로 — 라인만 다시 그리기 (perpOffsets 재사용)
            if (positionsChanged && !noiseRefreshed)
                ApplyToLineRenderer();
        }

        private void ScheduleNextRefresh()
        {
            float now = _useUnscaledTime ? Time.unscaledTime : Time.time;
            _nextRefreshAt = now + _refreshInterval;
        }

        private void Regenerate()
        {
            if (_perpOffsets == null || _perpOffsets.Length != _segments + 1)
                _perpOffsets = new float[_segments + 1];

            // 양 끝(0, _segments)은 0, 중간만 random jitter
            for (int i = 1; i < _segments; i++)
                _perpOffsets[i] = Random.Range(-_jitter, _jitter);

            ApplyToLineRenderer();
        }

        private void ApplyToLineRenderer()
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
                if (i > 0 && i < _segments && _perpOffsets != null && i < _perpOffsets.Length)
                    basePos += perp * _perpOffsets[i];
                _lr.SetPosition(i, basePos);
            }
        }
    }
}

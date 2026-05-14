using UnityEngine;

namespace LostMemory.Town
{
    /// <summary>
    /// 플레이어 머리 위에 떠 있으면서 페이지 목적지 방향을 가리키는 길안내 마커.
    /// 플레이어가 목적지 일정 반경 안에 들어가면 자체적으로 SpriteRenderer 를 끔 (페이지 진행은 사용자가 Next 로).
    /// 화살표 sprite 는 "위쪽이 진행 방향" 이라고 가정하고 transform.up 을 회전시킴.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Town/Tutorial World Marker")]
    public class TutorialWorldMarker : MonoBehaviour
    {
        [Header("Bobbing")]
        [Tooltip("위아래 진폭 (월드 단위).")]
        [SerializeField] private float _amplitude = 0.15f;
        [Tooltip("주기 (초).")]
        [SerializeField] private float _period = 1f;

        [Header("Follow")]
        [Tooltip("플레이어 머리 위 오프셋.")]
        [SerializeField] private float _playerOffsetY = 1.5f;

        [Header("Arrival")]
        [Tooltip("플레이어와 목적지 거리가 이 값 이하면 마커가 사라짐.")]
        [SerializeField] private float _arriveRadius = 1.5f;

        [Header("Rotation")]
        [Tooltip("sprite 가 위쪽이 진행방향이면 0, 오른쪽이 진행방향이면 -90.")]
        [SerializeField] private float _rotationOffsetDegrees = 0f;

        private Transform _player;
        private Vector3 _targetWorld;
        private Transform _targetTransform;
        private bool _hasTarget;
        private float _phase;
        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>플레이어 Transform 을 한 번 잡아둠. 컨트롤러 Spawn 직후 호출.</summary>
        public void SetPlayer(Transform player)
        {
            _player = player;
        }

        /// <summary>이번 페이지의 목적지 좌표.</summary>
        public void SetTarget(Vector3 worldTarget)
        {
            _targetWorld = worldTarget;
            _targetTransform = null;
            _hasTarget = true;
            _phase = 0f;
            if (_spriteRenderer != null) _spriteRenderer.enabled = true;
        }

        /// <summary>
        /// 던전용: 매 프레임 target.position 을 추적. 적/포탈/출구처럼 위치가 바뀌거나 즉시 알 수 없는 대상에 사용.
        /// target == null 이면 ClearTarget 과 동일 동작.
        /// </summary>
        public void SetTargetTransform(Transform target)
        {
            _targetTransform = target;
            _hasTarget = target != null;
            _phase = 0f;
            if (_spriteRenderer != null) _spriteRenderer.enabled = _hasTarget;
        }

        /// <summary>마커를 숨기고 추적 해제. None 모드용.</summary>
        public void ClearTarget()
        {
            _targetTransform = null;
            _hasTarget = false;
            if (_spriteRenderer != null) _spriteRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (_player == null || !_hasTarget) return;

            // 동적 Transform 추적 모드면 매 프레임 위치 갱신. target 이 사라지면 마커도 끔.
            if (_targetTransform != null)
            {
                _targetWorld = _targetTransform.position;
            }
            else if (object.ReferenceEquals(_targetTransform, null) == false)
            {
                // Unity-null (Destroy 된 Transform) — _hasTarget 해제하고 숨김.
                _hasTarget = false;
                if (_spriteRenderer != null) _spriteRenderer.enabled = false;
                return;
            }

            Vector3 playerPos = _player.position;
            Vector3 toTarget = _targetWorld - playerPos;
            // z 는 무시 (2D 게임 가정)
            toTarget.z = 0f;
            float dist = toTarget.magnitude;

            // 도착 — 마커 숨김 (위치 추적은 계속해도 무방하지만 그림만 끔)
            if (dist <= _arriveRadius)
            {
                if (_spriteRenderer != null && _spriteRenderer.enabled)
                    _spriteRenderer.enabled = false;
                return;
            }

            if (_spriteRenderer != null && !_spriteRenderer.enabled)
                _spriteRenderer.enabled = true;

            // 위치: 플레이어 머리 위 + 위아래 bob
            _phase += Time.deltaTime / Mathf.Max(_period, 0.001f);
            float bobY = Mathf.Sin(_phase * Mathf.PI * 2f) * _amplitude;
            transform.position = playerPos + new Vector3(0f, _playerOffsetY + bobY, 0f);

            // 회전: 화살표가 목적지 방향을 가리키도록
            float angleDeg = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg + _rotationOffsetDegrees;
            // 위쪽 기본 sprite → -90 보정해서 transform.up 이 dir 향하게
            // (atan2 는 +x 기준 각도라, sprite up 이 +y 기본이면 -90 추가)
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg - 90f);
        }
    }
}

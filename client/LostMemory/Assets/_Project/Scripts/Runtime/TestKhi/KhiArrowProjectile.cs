using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 한 발의 화살. Rigidbody2D(Kinematic) 로 직선 비행하며 trigger 충돌 시 Health.Damage 호출.
    /// KhiBowController 가 Instantiate → Launch(...) 호출로 생성.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Lost Memory/Test Khi/Khi Arrow Projectile")]
    public class KhiArrowProjectile : MonoBehaviour
    {
        [Header("Lifetime")]
        [SerializeField, Min(0.1f)] private float maxLifetime = 3f;

        [Header("Hit Detection")]
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField, Min(0f)] private float sweepRadius = 0.18f;
        [SerializeField] private bool acceptBossTaggedHealthOutsideTargetLayers = true;
        [SerializeField, Min(0f)] private float targetFlickerDuration = 0f;
        [SerializeField, Min(0f)] private float targetInvincibilityDuration = 0f;
        [Tooltip("적과 충돌 시 화살을 즉시 파괴.")]
        [SerializeField] private bool destroyOnHit = true;

        [Header("Visual")]
        [Tooltip("화살 sprite 가 기본 +X 방향(오른쪽)을 향하면 0. 위쪽을 향하면 -90.")]
        [SerializeField] private float spriteAngleOffsetDeg = 0f;

        [Header("Impact Animation (Optional — destroyOnHit=true 일 때만 적용)")]
        [Tooltip("hit 시 state 전환할 Animator. 비워두면 즉시 Destroy.")]
        [SerializeField] private Animator impactAnimator;
        [Tooltip("hit 시 Animator.Play 호출할 state 이름 (예: 'fireball-destroy').")]
        [SerializeField] private string impactStateName = "fireball-destroy";
        [Tooltip("impact animation 재생 후 GameObject Destroy 까지 대기 시간.")]
        [SerializeField, Min(0.05f)] private float impactHoldDuration = 0.5f;

        [Header("Homing (Optional — 0 = 직선, 90 = 약한 유도)")]
        [Tooltip("초당 회전 가능한 최대 각도. 0 = 직선, 60~90 = 약한 유도, 180+ = 강한 유도.")]
        [SerializeField, Min(0f)] private float homingTurnRateDegPerSec = 0f;
        [Tooltip("감지 반경 안의 가장 가까운 적을 추적. 0 보다 커야 작동.")]
        [SerializeField, Min(0f)] private float homingDetectionRadius = 8f;
        [SerializeField] private LayerMask homingTargetLayers = ~0;
        [Tooltip("발사 직후 N 초 동안은 직선 (조준 의도 유지). 그 후 유도 시작.")]
        [SerializeField, Min(0f)] private float homingDelay = 0.05f;

        [Header("Debug")]
        [Tooltip("OnTriggerEnter2D 단계별 진단 로그. 검증 끝나면 끄세요.")]
        [SerializeField] private bool logProjectileEvents = false;

        private Rigidbody2D _rigidbody;
        private Collider2D _collider;
        private static readonly Collider2D[] _homingScanBuffer = new Collider2D[16];
        private static readonly RaycastHit2D[] _sweepHitBuffer = new RaycastHit2D[32];
        private float _damage;
        private Vector2 _direction;
        private float _speed;
        private GameObject _attacker;
        private float _spawnedAt;
        private bool _launched;
        private bool _hasHit;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _rigidbody.gravityScale = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _collider = GetComponent<Collider2D>();
        }

        /// <summary>
        /// 화살 발사. 위치는 호출 전 Instantiate 시 결정. 방향은 정규화 입력.
        /// </summary>
        public void Launch(Vector2 direction, float speed, float damage, GameObject attacker)
        {
            _direction = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector2.right;
            _speed = Mathf.Max(0f, speed);
            _damage = Mathf.Max(0f, damage);
            _attacker = attacker;
            _spawnedAt = Time.time;
            _launched = true;
            _hasHit = false;

            float angleDeg = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg + spriteAngleOffsetDeg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        }

        /// <summary>
        /// 유도 파라미터 외부 override. KhiStaffController 등에서 launch 직후 호출해 발사체별로 유도 설정.
        /// turnRateDegPerSec=0 이면 직선. 양수면 유도 활성.
        /// </summary>
        public void SetHoming(float turnRateDegPerSec, float detectionRadius)
        {
            homingTurnRateDegPerSec = Mathf.Max(0f, turnRateDegPerSec);
            homingDetectionRadius = Mathf.Max(0f, detectionRadius);
        }

        private void FixedUpdate()
        {
            if (!_launched) return;

            if (homingTurnRateDegPerSec > 0f
                && homingDetectionRadius > 0f
                && Time.time - _spawnedAt >= homingDelay)
            {
                ApplyHoming();
            }

            Vector2 currentPosition = _rigidbody.position;
            Vector2 movement = _direction * (_speed * Time.fixedDeltaTime);
            if (TryHitAlongPath(currentPosition, movement))
            {
                return;
            }

            _rigidbody.MovePosition(currentPosition + movement);
        }

        private void ApplyHoming()
        {
            int count = Physics2D.OverlapCircleNonAlloc(
                _rigidbody.position, homingDetectionRadius, _homingScanBuffer, homingTargetLayers);
            if (count <= 0) return;

            Transform nearest = null;
            float bestSqr = float.MaxValue;
            Vector2 myPos = _rigidbody.position;
            for (int i = 0; i < count; i++)
            {
                Collider2D c = _homingScanBuffer[i];
                if (c == null) continue;
                Health h = c.GetComponentInParent<Health>();
                if (h == null) continue;
                if (_attacker != null && IsOwnedByAttacker(h, _attacker)) continue;
                float sqr = ((Vector2)c.transform.position - myPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = c.transform;
                }
            }
            if (nearest == null) return;

            Vector2 toTarget = ((Vector2)nearest.position - myPos).normalized;
            float currentAngle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            float maxStep = homingTurnRateDegPerSec * Time.fixedDeltaTime;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxStep);
            float newAngleRad = newAngle * Mathf.Deg2Rad;
            _direction = new Vector2(Mathf.Cos(newAngleRad), Mathf.Sin(newAngleRad));

            transform.rotation = Quaternion.Euler(0f, 0f, newAngle + spriteAngleOffsetDeg);
        }

        private void Update()
        {
            if (!_launched) return;
            if (Time.time - _spawnedAt >= maxLifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryApplyHit(other);
        }

        private bool TryHitAlongPath(Vector2 currentPosition, Vector2 movement)
        {
            float distance = movement.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return false;
            }

            int count = Physics2D.CircleCastNonAlloc(
                currentPosition,
                GetSweepRadius(),
                _direction,
                _sweepHitBuffer,
                distance,
                GetSweepLayerMask());

            RaycastHit2D nearestHit = default;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _sweepHitBuffer[i];
                Collider2D hitCollider = hit.collider;
                if (hitCollider == null || hitCollider == _collider)
                {
                    continue;
                }

                if (hit.distance < nearestDistance && CanDamageCollider(hitCollider))
                {
                    nearestHit = hit;
                    nearestDistance = hit.distance;
                }
            }

            if (nearestHit.collider == null)
            {
                return false;
            }

            _rigidbody.position = nearestHit.centroid;
            transform.position = nearestHit.centroid;
            return TryApplyHit(nearestHit.collider);
        }

        private bool TryApplyHit(Collider2D other)
        {
            if (!_launched || _hasHit || other == null) return false;

            int layer = other.gameObject.layer;
            string lname = LayerMask.LayerToName(layer);

            if (!IsColliderAcceptedByLayerOrBossTag(other, layer))
            {
                if (logProjectileEvents) Debug.Log($"[Projectile {name}] hit {other.name}(L:{lname}) → blocked by layerMask");
                return false;
            }

            Health health = other.GetComponentInParent<Health>();
            if (health == null)
            {
                if (logProjectileEvents) Debug.Log($"[Projectile {name}] hit {other.name}(L:{lname}) → no Health in parent chain");
                return false;
            }
            if (_attacker != null && IsOwnedByAttacker(health, _attacker))
            {
                if (logProjectileEvents) Debug.Log($"[Projectile {name}] hit {other.name}(L:{lname}) → owned by attacker");
                return false;
            }
            if (!health.CanTakeDamageThisFrame())
            {
                if (logProjectileEvents) Debug.Log($"[Projectile {name}] hit {other.name}(L:{lname}) → CanTakeDamageThisFrame=false");
                return false;
            }

            _hasHit = true;
            health.Damage(_damage, _attacker, targetFlickerDuration, targetInvincibilityDuration, _direction);
            if (logProjectileEvents) Debug.Log($"[Projectile {name}] hit {other.name}(L:{lname}) → damage {_damage} APPLIED");

            if (destroyOnHit)
            {
                PlayImpactAndDestroy();
            }

            return true;
        }

        private bool CanDamageCollider(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            Health health = other.GetComponentInParent<Health>();
            if (health == null)
            {
                return false;
            }

            int layer = other.gameObject.layer;
            if (!IsColliderAcceptedByLayerOrBossTag(other, layer))
            {
                return false;
            }

            return (_attacker == null || !IsOwnedByAttacker(health, _attacker))
                   && health.CanTakeDamageThisFrame();
        }

        private float GetSweepRadius()
        {
            if (sweepRadius > 0f)
            {
                return sweepRadius;
            }

            return _collider != null
                ? Mathf.Max(0.05f, Mathf.Min(_collider.bounds.extents.x, _collider.bounds.extents.y))
                : 0.05f;
        }

        private int GetSweepLayerMask()
        {
            int mask = targetLayers.value;
            if (acceptBossTaggedHealthOutsideTargetLayers)
            {
                int defaultLayer = LayerMask.NameToLayer("Default");
                if (defaultLayer >= 0)
                {
                    mask |= 1 << defaultLayer;
                }
            }

            return mask;
        }

        private bool IsColliderAcceptedByLayerOrBossTag(Collider2D other, int layer)
        {
            if (IsLayerAccepted(layer))
            {
                return true;
            }

            if (!acceptBossTaggedHealthOutsideTargetLayers || other == null)
            {
                return false;
            }

            Health health = other.GetComponentInParent<Health>();
            return IsBossTagged(health);
        }

        private bool IsLayerAccepted(int layer)
        {
            return ((1 << layer) & targetLayers.value) != 0;
        }

        private static bool IsBossTagged(Health health)
        {
            if (health == null)
            {
                return false;
            }

            Transform healthTransform = health.transform;
            return health.CompareTag("Boss")
                   || (healthTransform.root != null && healthTransform.root.CompareTag("Boss"));
        }

        /// <summary>
        /// impactAnimator 가 있으면 state 전환 + Collider/이동 정지 + impactHoldDuration 후 Destroy.
        /// 없으면 즉시 Destroy.
        /// </summary>
        private void PlayImpactAndDestroy()
        {
            if (impactAnimator != null && !string.IsNullOrEmpty(impactStateName))
            {
                impactAnimator.Play(impactStateName);
                if (_collider != null) _collider.enabled = false;
                _launched = false;
                Destroy(gameObject, impactHoldDuration);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private static bool IsOwnedByAttacker(Health health, GameObject attacker)
        {
            if (attacker == null || health == null) return false;
            return health.gameObject == attacker || health.transform.IsChildOf(attacker.transform);
        }
    }
}

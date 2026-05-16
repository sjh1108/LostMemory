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
        [SerializeField, Min(0f)] private float targetFlickerDuration = 0f;
        [SerializeField, Min(0f)] private float targetInvincibilityDuration = 0f;
        [Tooltip("적과 충돌 시 화살을 즉시 파괴.")]
        [SerializeField] private bool destroyOnHit = true;

        [Header("Visual")]
        [Tooltip("화살 sprite 가 기본 +X 방향(오른쪽)을 향하면 0. 위쪽을 향하면 -90.")]
        [SerializeField] private float spriteAngleOffsetDeg = 0f;

        private Rigidbody2D _rigidbody;
        private float _damage;
        private Vector2 _direction;
        private float _speed;
        private GameObject _attacker;
        private float _spawnedAt;
        private bool _launched;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _rigidbody.gravityScale = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
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

            float angleDeg = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg + spriteAngleOffsetDeg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        }

        private void FixedUpdate()
        {
            if (!_launched) return;
            _rigidbody.MovePosition(_rigidbody.position + _direction * (_speed * Time.fixedDeltaTime));
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
            if (!_launched) return;
            if (((1 << other.gameObject.layer) & targetLayers.value) == 0) return;

            Health health = other.GetComponentInParent<Health>();
            if (health == null) return;
            if (_attacker != null && IsOwnedByAttacker(health, _attacker)) return;
            if (!health.CanTakeDamageThisFrame()) return;

            health.Damage(_damage, _attacker, targetFlickerDuration, targetInvincibilityDuration, _direction);

            if (destroyOnHit)
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

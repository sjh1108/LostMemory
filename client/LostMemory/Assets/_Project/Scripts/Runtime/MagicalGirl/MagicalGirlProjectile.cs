using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-204: 1·3·5번 미소녀 발사체. 직선 이동 + 첫 Character.AI 적중 시 데미지 1회 후 destroy.
    ///
    /// MagicalGirlAI.Attack() 가 catalog 의 vfxPrefab 을 Instantiate → Init(direction, damage, speed, lifetime) 호출.
    /// 본체에 Collider2D (Trigger) 가 붙어 있어야 적과 접촉 가능. 시각은 자식 ParticleSystem 으로 분리.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagicalGirlProjectile : MonoBehaviour
    {
        private Vector2 _velocity;
        private float _damage;
        private float _expiresAt;
        private bool _hasHit;
        private GameObject _hitVfxPrefab;

        public void Init(Vector2 direction, float damage, float speed, float lifetime, GameObject hitVfxPrefab = null)
        {
            _velocity = direction.normalized * speed;
            _damage = damage;
            _expiresAt = Time.time + lifetime;
            _hitVfxPrefab = hitVfxPrefab;
            // 회전: 진행 방향으로 sprite 정렬 (오른쪽 = 0도 기준)
            float angleDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        }

        private void SpawnHitVfx()
        {
            if (_hitVfxPrefab == null) return;
            Instantiate(_hitVfxPrefab, transform.position, Quaternion.identity);
        }

        private void Update()
        {
            if (_hasHit) return;
            transform.position += (Vector3)(_velocity * Time.deltaTime);
            if (Time.time >= _expiresAt)
            {
                // 만료 시에도 폭발 (공중 폭발) — 적중 외 시각 일관성. catalog hitVfxPrefab=null 이면 no-op.
                SpawnHitVfx();
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasHit) return;
            Health h = other.GetComponentInParent<Health>();
            if (h == null || h.CurrentHealth <= 0f) return;
            // CL-143 패턴: Character.AI 만 — 발사체끼리 충돌 / 플레이어 자체 / 트리거 zone 제외
            Character ch = h.GetComponentInParent<Character>();
            if (ch == null || ch.CharacterType != Character.CharacterTypes.AI) return;

            _hasHit = true;
            h.Damage(_damage, gameObject, 0f, 0f, Vector3.zero);
            SpawnHitVfx();  // CL-204 B8: 적중 시 폭발 VFX
            Destroy(gameObject);
        }
    }
}

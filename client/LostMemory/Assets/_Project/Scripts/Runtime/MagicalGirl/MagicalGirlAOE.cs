using LostMemory.Combat;
using LostMemory.Enemies;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-204: 2·4번 미소녀 AOE.
    ///
    /// 2 (Ice / AOEFollow): spawn 직후 가장 가까운 적에 부착, duration 동안 매 tick OverlapCircle 데미지.
    /// 4 (Blackhole / AOEStationary): spawn 위치에 정지, duration 동안 tick 데미지 + 적을 코어로 끌어당김.
    ///
    /// MagicalGirlAI.Attack() 가 catalog 의 vfxPrefab 을 Instantiate → Init(kind, damage, radius, duration, tickInterval, pullSpeed, slowMag, slowDur) 호출.
    /// slowMagnitude > 0 이면 매 tick 적에 EnemyStatusEffect.ApplySlow 호출 → sprite 푸르게 + 이속 감소.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagicalGirlAOE : MonoBehaviour
    {
        private MagicalGirlAttackCatalog.AttackKind _kind;
        private float _damagePerTick;
        private float _radius;
        private float _expiresAt;
        private float _nextTickAt;
        private float _tickInterval;
        private float _pullSpeed;
        private float _slowMagnitude;
        private float _slowDuration;

        // 추적용
        private Transform _followTarget;

        // 검색 임시 버퍼 (heap alloc 방지)
        private static readonly Collider2D[] _hitBuf = new Collider2D[24];

        public void Init(
            MagicalGirlAttackCatalog.AttackKind kind,
            float damagePerTick,
            float radius,
            float duration,
            float tickInterval,
            float pullSpeed,
            float slowMagnitude,
            float slowDuration)
        {
            _kind = kind;
            _damagePerTick = damagePerTick;
            _radius = radius;
            _expiresAt = Time.time + duration;
            _tickInterval = Mathf.Max(0.05f, tickInterval);
            _nextTickAt = Time.time;  // 즉시 첫 tick
            _pullSpeed = pullSpeed;
            _slowMagnitude = Mathf.Clamp01(slowMagnitude);
            _slowDuration = Mathf.Max(0f, slowDuration);

            if (_kind == MagicalGirlAttackCatalog.AttackKind.AOEFollow)
            {
                _followTarget = FindClosestEnemyTransform();
                if (_followTarget != null) transform.position = _followTarget.position;
            }
        }

        private void Update()
        {
            if (Time.time >= _expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            // AOEFollow: 매 프레임 적 위치 추적
            if (_kind == MagicalGirlAttackCatalog.AttackKind.AOEFollow)
            {
                if (_followTarget == null || !_followTarget.gameObject.activeInHierarchy)
                    _followTarget = FindClosestEnemyTransform();
                if (_followTarget != null) transform.position = _followTarget.position;
            }

            if (Time.time < _nextTickAt) return;
            _nextTickAt = Time.time + _tickInterval;
            DoTick();
        }

        private void DoTick()
        {
            if (_damagePerTick <= 0f && _pullSpeed <= 0f && _slowMagnitude <= 0f) return;
            int hits = Physics2D.OverlapCircleNonAlloc(transform.position, _radius, _hitBuf);
            for (int i = 0; i < hits; i++)
            {
                Collider2D col = _hitBuf[i];
                if (col == null) continue;
                Health h = col.GetComponentInParent<Health>();
                if (h == null || h.CurrentHealth <= 0f) continue;
                if (!CombatTargetable.CanBeTargeted(h)) continue;
                Character ch = h.GetComponentInParent<Character>();
                if (ch == null || ch.CharacterType != Character.CharacterTypes.AI) continue;

                if (_damagePerTick > 0f)
                    h.Damage(_damagePerTick, gameObject, 0f, 0f, Vector3.zero);

                // CL-204: Slow status (sprite 푸르게 + 이속 감소). CL-202 EnemyStatusEffect 위임.
                if (_slowMagnitude > 0f && _slowDuration > 0f)
                {
                    EnemyStatusEffect status = h.gameObject.GetComponent<EnemyStatusEffect>()
                                            ?? h.gameObject.GetComponentInParent<EnemyStatusEffect>();
                    if (status == null) status = h.gameObject.AddComponent<EnemyStatusEffect>();
                    status.ApplySlow(_slowMagnitude, _slowDuration);
                }

                // Blackhole 끌어당김 (AOEStationary 전용)
                if (_pullSpeed > 0f && _kind == MagicalGirlAttackCatalog.AttackKind.AOEStationary)
                {
                    Transform et = h.transform;
                    Vector3 toCore = transform.position - et.position;
                    float dist = toCore.magnitude;
                    if (dist > 0.05f)
                    {
                        Vector3 step = toCore.normalized * Mathf.Min(_pullSpeed * _tickInterval, dist);
                        et.position += step;
                    }
                }
            }
        }

        private Transform FindClosestEnemyTransform()
        {
            // 8유닛 반경 내에서 가장 가까운 Character.AI Health 검색
            const float searchRadius = 8f;
            int hits = Physics2D.OverlapCircleNonAlloc(transform.position, searchRadius, _hitBuf);
            Transform closest = null;
            float minDistSq = float.MaxValue;
            for (int i = 0; i < hits; i++)
            {
                Collider2D col = _hitBuf[i];
                if (col == null) continue;
                Health h = col.GetComponentInParent<Health>();
                if (h == null || h.CurrentHealth <= 0f) continue;
                if (!CombatTargetable.CanBeTargeted(h)) continue;
                Character ch = h.GetComponentInParent<Character>();
                if (ch == null || ch.CharacterType != Character.CharacterTypes.AI) continue;
                float dSq = ((Vector2)(h.transform.position - transform.position)).sqrMagnitude;
                if (dSq < minDistSq)
                {
                    minDistSq = dSq;
                    closest = h.transform;
                }
            }
            return closest;
        }
    }
}

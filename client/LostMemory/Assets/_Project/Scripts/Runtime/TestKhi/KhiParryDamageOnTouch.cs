using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// Parry 대응 피해 소스.
    /// 피해 적용 직전에 대상의 KhiParryController를 확인해 패링 성공 시 피해를 스킵하고
    /// 패링 실패 후딜 중이면 감쇠 피해만 적용한다.
    /// 패링과 무관한 대상이면 기본 DamageOnTouch 흐름을 그대로 유지한다.
    ///
    /// NOTE: 감쇠 경로는 base.OnCollideWithDamageable(Health)의 로직(TDE DamageOnTouch.cs 라인 634-676)과
    /// 동일 순서로 수동 재현한다. TDE 버전 업그레이드 시 base 구현을 확인해야 한다.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Parry Damage On Touch")]
    public class KhiParryDamageOnTouch : DamageOnTouch
    {
        [SerializeField] private bool logParryInteraction = false;

        protected override void OnCollideWithDamageable(Health health)
        {
            if (health == null)
            {
                if (logParryInteraction) Debug.Log("[KhiParryDoT] health == null, base path");
                base.OnCollideWithDamageable(health);
                return;
            }

            KhiParryController parry = ResolveParryController(health);
            if (parry == null)
            {
                if (logParryInteraction) Debug.Log($"[KhiParryDoT] no KhiParryController on {health.name}, base path");
                base.OnCollideWithDamageable(health);
                return;
            }

            float randomDamage = Random.Range(MinDamageCaused, Mathf.Max(MaxDamageCaused, MinDamageCaused));
            Vector2 incomingDir = (Vector2)(health.transform.position - transform.position);

            if (logParryInteraction)
            {
                Debug.Log($"[KhiParryDoT] calling TryResolve, state={parry.CurrentState}, damage={randomDamage}");
            }

            if (!parry.TryResolveIncomingDamage(gameObject, incomingDir, randomDamage, out float resolved))
            {
                if (logParryInteraction) Debug.Log($"[KhiParryDoT] TryResolve returned false, base path");
                base.OnCollideWithDamageable(health);
                return;
            }

            if (resolved <= 0f)
            {
                // 패링 성공: 피해 적용 스킵. 피드백/이벤트도 호출하지 않음.
                if (logParryInteraction) Debug.Log($"[KhiParryDoT] parry SUCCESS, skip damage");
                return;
            }

            // 실패 후딜 중 감쇠 피해 적용 (base 로직 수동 재현)
            if (logParryInteraction) Debug.Log($"[KhiParryDoT] parry REDUCED, damage={resolved}");
            ApplyReducedDamage(health, resolved);
        }

        private static KhiParryController ResolveParryController(Health health)
        {
            if (health == null)
            {
                return null;
            }

            KhiParryController comp = health.gameObject.GetComponent<KhiParryController>();
            if (comp == null)
            {
                comp = health.gameObject.GetComponentInParent<KhiParryController>();
            }

            return comp;
        }

        private void ApplyReducedDamage(Health health, float reducedDamage)
        {
            _collidingHealth = health;
            _colliderHealth = health;

            if (!health.CanTakeDamageThisFrame())
            {
                return;
            }

            _colliderTopDownController = health.gameObject.GetComponent<TopDownController>();
            if (_colliderTopDownController == null)
            {
                _colliderTopDownController = health.gameObject.GetComponentInParent<TopDownController>();
            }

            HitDamageableFeedback?.PlayFeedbacks(this.transform.position);
            HitDamageableEvent?.Invoke(_colliderHealth);

            ApplyKnockback(reducedDamage, TypedDamages);

            DetermineDamageDirection();

            if (RepeatDamageOverTime)
            {
                _colliderHealth.DamageOverTime(
                    reducedDamage,
                    gameObject,
                    InvincibilityDuration,
                    InvincibilityDuration,
                    _damageDirectionVector,
                    TypedDamages,
                    AmountOfRepeats,
                    DurationBetweenRepeats,
                    DamageOverTimeInterruptible,
                    RepeatedDamageType);
            }
            else
            {
                _colliderHealth.Damage(
                    reducedDamage,
                    gameObject,
                    InvincibilityDuration,
                    InvincibilityDuration,
                    _damageDirectionVector,
                    TypedDamages);
            }

            if (DamageTakenEveryTime + DamageTakenDamageable > 0f && !_colliderHealth.PreventTakeSelfDamage)
            {
                // base의 SelfDamage는 private virtual이 아닌 protected virtual이라 직접 호출 가능
                SelfDamage(DamageTakenEveryTime + DamageTakenDamageable);
            }
        }

        /// <summary>
        /// base._damageDirection은 protected지만 필드명 노출 여부가 버전마다 다를 수 있어
        /// DetermineDamageDirection이 설정하는 값을 우회 없이 사용하기 위한 래퍼.
        /// base에서는 _damageDirection이라는 protected 필드를 쓰지만, 여기서는 같은 필드를 그대로 참조한다.
        /// </summary>
        private Vector3 _damageDirectionVector => _damageDirection;
    }
}

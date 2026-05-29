using System.Collections.Generic;
using LostMemory.Combat;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-019 follow-up: TDE Health subclass — 모든 데미지 진입점에서 패링/보호막 인터셉트.
    ///
    /// 배경:
    /// 기존 <see cref="KhiParryDamageOnTouch"/> 는 *적의 DamageOnTouch* 를 교체해야 작동 →
    /// 적이 <c>health.Damage()</c> 를 직접 호출하는 케이스 (Chobomb 폭발, BerthaDashAttack 등) 미커버.
    ///
    /// 본 클래스는 Player 의 Health 컴포넌트를 *교체* 해서 base.Damage() 직전에:
    /// 1. KhiParryController.TryResolveIncomingDamage — 패링 성공 시 데미지 스킵, 후딜 시 감쇠
    /// 2. PlayerShield.TryAbsorb — 보호막 차감
    /// 3. 남은 데미지로 base.Damage 호출
    ///
    /// → 어떤 데미지 소스 (DamageOnTouch / 커스텀 enemy script / projectile) 든 패링/보호막 통과.
    ///
    /// 적용: Player 프리팹의 Health 컴포넌트 → KhiParryHealth 로 교체 (Inspector → 우클릭 → Edit Script,
    /// 또는 컴포넌트 삭제 후 KhiParryHealth Add Component).
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Parry Health")]
    public class KhiParryHealth : Health
    {
        [Header("Khi Parry Integration")]
        [SerializeField, Tooltip("Player 의 KhiParryController. 비어있으면 Awake / 첫 Damage 호출 시 GetComponent 자동 탐색.")]
        private KhiParryController parryController;

        [SerializeField, Tooltip("Player 의 PlayerShield. 비어있으면 자동 탐색. CL-108 보호막 차감 진입점.")]
        private PlayerShield playerShield;

        [SerializeField, Tooltip("패링/보호막 흡수 시 디버그 로그.")]
        private bool logIntercepts = false;

        protected override void Awake()
        {
            base.Awake();
            ResolveLocalRefs();
        }

        private void ResolveLocalRefs()
        {
            if (parryController == null) parryController = GetComponent<KhiParryController>();
            if (parryController == null) parryController = GetComponentInParent<KhiParryController>();
            if (playerShield == null) playerShield = GetComponent<PlayerShield>();
            if (playerShield == null) playerShield = GetComponentInParent<PlayerShield>();
        }

        public override void Damage(
            float damage,
            GameObject instigator,
            float flickerDuration,
            float invincibilityDuration,
            Vector3 damageDirection,
            List<TypedDamage> typedDamages = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 시연 디버그 F6 — God Mode ON 이면 데미지 path 전체 skip (패링/실드 처리도 안 함).
            // TDE Invulnerable check 가 OnHit/HitStun 등 일부 event 발화 후 가드라서 진입 차단으로 안전망.
            if (LostMemory.Networking.Player.PlayerHealthSync.GodModeActive) return;
#endif

            if (damage <= 0f || !CanTakeDamageThisFrame())
            {
                base.Damage(damage, instigator, flickerDuration, invincibilityDuration, damageDirection, typedDamages);
                return;
            }

            // 안전: PlayMode 진입 후 ref 가 비어있으면 1회 재시도 (DontDestroyOnLoad / 스폰 타이밍 race).
            if (parryController == null || playerShield == null) ResolveLocalRefs();

            // 1) 패링 인터셉트.
            float remaining = damage;
            if (parryController != null && parryController.TryResolveIncomingDamage(
                instigator, (Vector2)damageDirection, damage, out float resolved))
            {
                if (resolved <= 0f)
                {
                    // 패링 성공 — 데미지 스킵. 보호막도 차감 안 함 (KhiParryDamageOnTouch 와 동일 정책).
                    if (logIntercepts) Debug.Log($"[KhiParryHealth] parry SUCCESS, skip damage={damage:F1}");
                    return;
                }
                // 패링 실패 후딜: 감쇠 데미지로 계속 진행.
                if (logIntercepts) Debug.Log($"[KhiParryHealth] parry REDUCED, damage={damage:F1} -> {resolved:F1}");
                remaining = resolved;
            }

            // 2) 보호막 차감.
            if (playerShield != null)
            {
                float afterShield = playerShield.TryAbsorb(remaining);
                if (afterShield <= 0f)
                {
                    if (logIntercepts) Debug.Log($"[KhiParryHealth] shield fully absorbed {remaining:F1}");
                    return;
                }
                remaining = afterShield;
            }

            // 3) 남은 데미지 적용.
            base.Damage(remaining, instigator, flickerDuration, invincibilityDuration, damageDirection, typedDamages);
        }
    }
}

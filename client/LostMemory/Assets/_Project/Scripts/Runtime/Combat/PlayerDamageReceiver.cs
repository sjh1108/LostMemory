using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// CL-146: Player 피격 시 회피 / 방어 어댑터.
    ///
    /// TDE Health.OnHit 이벤트 (파라미터 없는 delegate) 구독 → 후처리로 데미지 환불 패턴.
    /// `Health.LastDamage` 프로퍼티에서 직전 데미지 값 조회.
    ///
    /// 적용 순서:
    /// 1. 회피 roll (DodgeChancePercent stat) → 성공 시 데미지 전체 환불 (ReceiveHealth)
    /// 2. 방어 적용 (DefenseFlat stat, GetTotalFlat) → defense 만큼 환불
    ///
    /// 한계 (§위험): OnHit 가 데미지 후 발화하므로 HP 가 잠깐 깎였다가 ReceiveHealth 로 복구
    /// → 0.1초 미만 시각적 flicker. 후속 시각 ticket 에서 OnHit 타이밍 조정 또는 PreviousHealth 캐시.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDamageReceiver : MonoBehaviour
    {
        private const float DodgeChanceCap = 0.15f;

        [Tooltip("같은 (또는 자식) GameObject 의 Health. 비워두면 GetComponentInChildren 자동 검색.")]
        [SerializeField] private Health health;

        [Tooltip("같은 GameObject 의 PlayerStatModifierContainer. Dodge/Defense 조회용.")]
        [SerializeField] private PlayerStatModifierContainer container;

        [Tooltip("회피/방어 적용 로그 — 디버그용.")]
        [SerializeField] private bool _logDamageMod = true;

        private void Awake()
        {
            if (health == null)
                health = GetComponent<Health>() ?? GetComponentInChildren<Health>();
            if (container == null)
                container = GetComponent<PlayerStatModifierContainer>();
        }

        private void OnEnable()
        {
            if (health == null)
            {
                Debug.LogError($"[PlayerDamageReceiver] health null. Inspector wiring 필요. host={gameObject.name}", this);
                return;
            }
            if (container == null)
            {
                Debug.LogError($"[PlayerDamageReceiver] container null. Inspector wiring 필요. host={gameObject.name}", this);
                return;
            }
            string healthInfo = $"OK (host={health.gameObject.name})";
            string containerInfo = $"OK (host={container.gameObject.name})";
            Debug.Log($"[PlayerDamageReceiver] OnEnable — wiring: health={healthInfo}, container={containerInfo}", this);
            health.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            if (health != null)
                health.OnHit -= HandleHit;
        }

        private void HandleHit()
        {
            if (health == null || container == null) return;
            float damageReceived = health.LastDamage;
            if (damageReceived <= 0f) return;

            // 1) 회피 roll — DodgeChancePercent 합산 (1.05 = 5% 회피).
            // 상한 15% 적용 — 합산이 초과해도 최대 15%.
            float dodgeChance = Mathf.Clamp(container.GetTotalMultiplier(StatId.Dodge) - 1f, 0f, DodgeChanceCap);
            if (dodgeChance > 0f && UnityEngine.Random.value < dodgeChance)
            {
                health.ReceiveHealth(damageReceived, gameObject);
                if (_logDamageMod)
                    Debug.Log($"[DamageReceiver] DODGE — refunded {damageReceived:F0} (chance={dodgeChance:P0})");
                return;
            }

            // 2) 방어 적용 — DefenseFlat 합산 (정수 환불)
            float defense = container.GetTotalFlat(StatId.Defense);
            if (defense > 0f)
            {
                float refund = Mathf.Min(defense, damageReceived);
                if (refund > 0f)
                {
                    health.ReceiveHealth(refund, gameObject);
                    if (_logDamageMod)
                        Debug.Log($"[DamageReceiver] DEFENSE -{refund:F0} (raw={damageReceived:F0}, def={defense:F0})");
                }
            }
        }
    }
}

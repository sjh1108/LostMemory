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

        [Tooltip("OnEnable wiring 진단 로그 ([PlayerDamageReceiver] OnEnable — wiring: ...). 평소 OFF, wiring 누락 의심 시만 ON.")]
        [SerializeField] private bool verboseLog = false;

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
            if (verboseLog)
            {
                string healthInfo = $"OK (host={health.gameObject.name})";
                string containerInfo = $"OK (host={container.gameObject.name})";
                Debug.Log($"[PlayerDamageReceiver] OnEnable — wiring: health={healthInfo}, container={containerInfo}", this);
            }
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

            // 2) 방어 적용 — CL-234 로그 기반 다이미니싱 공식:
            //      피해감소율(%) = 102.5 × Log10(1 + 0.025 × 방어력)
            //      최종 피해     = round(max(raw × (1 - 피해감소율/100), 1))
            //      환불          = raw - 최종 피해   (0 미만 clamp)
            //
            //   - 0 데미지 케이스 제거: max(_, 1) 로 최소 1 데미지 보장 (raw>=1 일 때).
            //   - 방어력 400 부근에서 피해감소율이 100% 도달하므로 Clamp(0, 0.99) 안전캡.
            //   - 음수 방어력 입력은 0 으로 무력화.
            float defense = container.GetTotalFlat(StatId.Defense);
            if (_logDamageMod)
            {
                // 방어력 0 케이스도 진단 가능하도록 항상 로그 (raw / defense / 결과).
                float ratePctDbg = 102.5f * Mathf.Log10(1f + 0.025f * Mathf.Max(defense, 0f));
                float rateDbg = Mathf.Clamp(ratePctDbg / 100f, 0f, 0.99f);
                float reducedDbg = damageReceived * (1f - rateDbg);
                int finalDbg = defense > 0f ? Mathf.Max(Mathf.RoundToInt(reducedDbg), 1) : Mathf.RoundToInt(damageReceived);
                Debug.Log($"[DamageReceiver] HIT — raw={damageReceived:F1} | def={defense:F0} | rate={ratePctDbg:F2}% (clamp→{rateDbg * 100f:F2}%) | reduced={reducedDbg:F2} | final={finalDbg} | refund={Mathf.Max(damageReceived - finalDbg, 0f):F1}");
            }
            if (defense > 0f)
            {
                float finalDamage = ComputeDefenseFinalDamage(damageReceived, defense);
                float refund = Mathf.Max(damageReceived - finalDamage, 0f);
                if (refund > 0f)
                {
                    health.ReceiveHealth(refund, gameObject);
                }
            }
        }

        /// <summary>
        /// CL-234 로그 기반 방어 공식. raw damage 와 방어력으로 최종 피해 계산.
        /// 최소 1 데미지 보장 (raw &gt;= 1 일 때). 방어력 400 ≈ 99% 감소 캡.
        /// </summary>
        public static float ComputeDefenseFinalDamage(float rawDamage, float defense)
        {
            if (rawDamage <= 0f) return 0f;
            float def = Mathf.Max(defense, 0f);
            float ratePercent = 102.5f * Mathf.Log10(1f + 0.025f * def);
            float rate = Mathf.Clamp(ratePercent / 100f, 0f, 0.99f);
            float reduced = rawDamage * (1f - rate);
            return Mathf.Max(Mathf.RoundToInt(reduced), 1);
        }
    }
}

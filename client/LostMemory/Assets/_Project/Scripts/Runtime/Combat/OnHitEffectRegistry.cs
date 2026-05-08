using System.Collections.Generic;
using System.Linq;
using LostMemory.Data;
using LostMemory.Enemies;
using LostMemory.Relics;
using LostMemory.TestKhi;
using LostMemory.VFX;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// CL-142: 평타 적중 시 발동하는 OnHit 효과들의 registry.
    ///
    /// SetEffectApplicator(CL-140) 가 SlowOnHit/FreezeOnHit/ChainOnHit 등을 Register 로 등록 →
    /// KhiMeleeComboController.TargetHit 이벤트에서 모든 등록 효과 순회 적용.
    ///
    /// CL-143 에서 BurnOnHit, WindAOE 추가 case 만 채우면 됨.
    ///
    /// 적 측 Status (Slow/Freeze) 는 EnemyStatusEffect 컴포넌트가 처리. OnHit 시 victim 의
    /// EnemyStatusEffect 를 GetOrAdd (없으면 자동 부착) 후 위임.
    ///
    /// 체인 무한 루프 방지: combat.TargetHit 만 구독, 체인은 Health.Damage 직접 호출
    /// (TargetHit 안 발화) → 자체 트리거 X.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/On Hit Effect Registry")]
    public sealed class OnHitEffectRegistry : MonoBehaviour
    {
        [Tooltip("같은 GameObject 의 KhiMeleeComboController. TargetHit 구독 대상.")]
        [SerializeField] private KhiMeleeComboController combat;

        [Tooltip("같은 GameObject 의 PlayerStatModifierContainer. 체인 데미지 = BaseDamage × AttackPower 합산.")]
        [SerializeField] private PlayerStatModifierContainer statContainer;

        [Header("Chain (CL-142 사용자 결정: 3마리)")]
        [Tooltip("체인 검색 반경 (유닛). 첫 hit 위치 기준.")]
        [SerializeField, Min(0.1f)] private float _chainRadius = 3f;

        [Tooltip("체인 동시 타격 최대 마리수.")]
        [SerializeField, Min(1)] private int _chainMaxTargets = 3;

        [Tooltip("체인 발동 후 글로벌 쿨다운 (초). 평타 마다 매번 발동 방지.")]
        [SerializeField, Min(0f)] private float _chainCooldown = 0.5f;

        [Header("Slow")]
        [Tooltip("얼음 슬로우 지속시간 (초). 새로 hit 마다 갱신.")]
        [SerializeField, Min(0f)] private float _slowDuration = 2f;

        [Header("Wind Blade (CL-143)")]
        [Tooltip("바람 검기 길이 (유닛). victim 위치에서 공격 방향으로.")]
        [SerializeField, Min(0.1f)] private float _windBladeLength = 2f;

        [Tooltip("바람 검기 너비 (유닛). 좁을수록 직선상 적만 맞음.")]
        [SerializeField, Min(0.1f)] private float _windBladeWidth = 0.6f;

        [Header("VFX Prefabs (CL-201)")]
        [SerializeField] private GameObject _chainHitVFXPrefab;
        [SerializeField] private GameObject _windAOEVFXPrefab;
        [Tooltip("Wind VFX 인스턴스 자동 삭제 시간(초). 파티클 라이프타임보다 길어야 잘림 없음.")]
        [SerializeField, Min(0.1f)] private float _windVFXLifetime = 1.2f;

        [Header("SFX (CL-201)")]
        [SerializeField] private AudioClip _chainHitSfx;
        [SerializeField] private AudioClip _windBladeSfx;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.7f;

        [Header("Hit Stop (CL-201)")]
        [Tooltip("Chain 적중 1회 hitstop 지속(unscaled). 0이면 비활성.")]
        [SerializeField, Min(0f)] private float _chainHitStopDuration = 0.05f;
        [Tooltip("Wind 적중 1회 hitstop 지속(unscaled). 0이면 비활성.")]
        [SerializeField, Min(0f)] private float _windHitStopDuration = 0.08f;
        [Tooltip("Hit stop 동안 Time.timeScale 값. 0=완전 정지, 0.05=5% 슬로우, 1=정상속도(효과 없음).")]
        [SerializeField, Range(0f, 1f)] private float _hitStopFrozenScale = 0f;

        [Header("Debug")]
        [SerializeField] private bool _logOnHitDispatch = false;

        private readonly IRelicEffectAuthority _authority = new NetworkRelicEffectAuthority();

        private struct OnHitEntry
        {
            public RelicEffectType Type;
            public float Magnitude;
            public float Duration;
            public object Source;
        }

        private readonly List<OnHitEntry> _entries = new();
        private float _nextChainAllowedAt;

        // 체인 검색 임시 버퍼 (heap alloc 방지)
        private static readonly Collider2D[] _chainBuf = new Collider2D[16];

        private void OnEnable()
        {
            if (combat == null)
            {
                Debug.LogError($"[OnHitEffectRegistry] combat null. Inspector wiring 필요. host={gameObject.name}", this);
                return;
            }
            combat.TargetHit += HandleHit;
        }

        private void OnDisable()
        {
            if (combat == null) return;
            combat.TargetHit -= HandleHit;
        }

        // ── 등록 API (SetEffectApplicator 가 호출) ──────────

        public void Register(RelicEffectType type, float magnitude, float duration, object source)
        {
            _entries.Add(new OnHitEntry { Type = type, Magnitude = magnitude, Duration = duration, Source = source });
            if (_logOnHitDispatch)
                Debug.Log($"[OnHit] Register {type} mag={magnitude} dur={duration} src={source}");
        }

        public void UnregisterBySource(object source)
        {
            int removed = _entries.RemoveAll(e => Equals(e.Source, source));
            if (_logOnHitDispatch && removed > 0)
                Debug.Log($"[OnHit] Unregister {removed} entries by src={source}");
        }

        // ── 메인 디스패처 ───────────────────────────────────

        private void HandleHit(KhiAttackRequest req, AttackStepData step, Health victim)
        {
            if (!_authority.IsAuthority) return;
            if (victim == null) return;

            foreach (OnHitEntry e in _entries)
            {
                switch (e.Type)
                {
                    case RelicEffectType.SlowOnHit:    ApplySlow(victim, e.Magnitude); break;
                    case RelicEffectType.FreezeOnHit:  ApplyFreeze(victim, e.Magnitude); break;
                    case RelicEffectType.ChainOnHit:   ApplyChain(req, step, victim, e.Magnitude); break;
                    case RelicEffectType.BurnOnHit:    ApplyBurn(victim, e.Magnitude, e.Duration); break;
                    case RelicEffectType.WindAOE:      ApplyWindBlade(victim, e.Magnitude); break;
                }
            }
        }

        // ── 효과 처리 ───────────────────────────────────────

        private void ApplySlow(Health victim, float magnitude)
        {
            EnemyStatusEffect status = GetOrAddStatus(victim);
            if (status == null) return;
            status.ApplySlow(magnitude, _slowDuration);
            if (_logOnHitDispatch)
                Debug.Log($"[OnHit] Slow {magnitude:P0} for {_slowDuration}s → {victim.name}");
        }

        private void ApplyFreeze(Health victim, float magnitudeSeconds)
        {
            EnemyStatusEffect status = GetOrAddStatus(victim);
            if (status == null) return;
            status.ApplyFreeze(magnitudeSeconds);
            if (_logOnHitDispatch)
                Debug.Log($"[OnHit] Freeze for {magnitudeSeconds}s → {victim.name}");
        }

        private void ApplyChain(KhiAttackRequest req, AttackStepData step, Health victim, float magnitude)
        {
            if (Time.time < _nextChainAllowedAt) return;
            _nextChainAllowedAt = Time.time + _chainCooldown;

            // 본인 공격력 (StatModifier 합산 적용)
            float playerAttack = combat.WeaponData != null ? combat.WeaponData.BaseDamage : 0f;
            if (statContainer != null)
                playerAttack *= statContainer.GetTotalMultiplier(StatId.AttackPower);
            float chainDamage = playerAttack * magnitude;
            if (chainDamage <= 0f) return;

            // CL-146: Range multiplier 적용 — 체인 검색 반경 확장. 상한 200%.
            float rangeMul = statContainer != null ? statContainer.GetCappedMultiplier(StatId.Range, 1f) : 1f;
            List<Health> targets = FindNearbyEnemies(victim.transform.position, victim, _chainRadius * rangeMul, _chainMaxTargets);
            if (targets.Count == 0) return;

            // 즉발 광역 — 모든 타겟에 동시 타격. 같은 적 중복은 FindNearbyEnemies 의 dedup 으로 차단.
            Vector3 origin = victim.transform.position;
            int hitCount = 0;
            foreach (Health t in targets)
            {
                if (t == null) continue;
                t.Damage(chainDamage, gameObject, 0f, 0f, Vector3.zero);
                SpawnChainVFX(origin, t.transform.position);
                hitCount++;
            }

            if (hitCount > 0)
            {
                if (_chainHitSfx != null)
                    AudioSource.PlayClipAtPoint(_chainHitSfx, origin, _sfxVolume);

                if (_chainHitStopDuration > 0f && KhiHitStopController.Instance != null)
                    KhiHitStopController.Instance.RequestFreeze(_chainHitStopDuration, _hitStopFrozenScale);
            }

            if (_logOnHitDispatch)
                Debug.Log($"[OnHit] Chain {magnitude:P0} → {targets.Count} targets, {chainDamage:F1} each");
        }

        private void ApplyBurn(Health victim, float magnitude, float duration)
        {
            // magnitude 의미: ATK 비례 계수 (0.1 = 초당 ATK 의 10%).
            // 화상 발동 시점의 ATK 를 캐시 → 도트가 끝날 때까지 그대로 사용.
            if (duration <= 0f || magnitude <= 0f) return;
            EnemyStatusEffect status = GetOrAddStatus(victim);
            if (status == null) return;

            float playerAttack = combat != null && combat.WeaponData != null ? combat.WeaponData.BaseDamage : 0f;
            if (statContainer != null)
                playerAttack *= statContainer.GetTotalMultiplier(StatId.AttackPower);
            float damagePerTick = playerAttack * magnitude;
            if (damagePerTick <= 0f) return;

            status.ApplyBurn(damagePerTick, duration, gameObject);
            if (_logOnHitDispatch)
                Debug.Log($"[OnHit] Burn {magnitude:P0}×ATK={damagePerTick:F1}/tick for {duration}s → {victim.name}");
        }

        private void ApplyWindBlade(Health victim, float magnitude)
        {
            if (combat == null) return;

            Vector3 victimPos = victim.transform.position;
            Vector3 playerPos = combat.transform.position;
            Vector2 dir = (Vector2)(victimPos - playerPos);
            // 겹쳤을 때 0벡터 fallback — player.right
            if (dir.sqrMagnitude < 0.0001f)
                dir = (Vector2)combat.transform.right;
            dir.Normalize();

            // CL-146: Range multiplier 적용 — Wind 검기 길이 확장 (너비는 유지). 상한 200%.
            float rangeMul = statContainer != null ? statContainer.GetCappedMultiplier(StatId.Range, 1f) : 1f;
            float effectiveLength = _windBladeLength * rangeMul;
            // 박스 영역: victim 위치에서 dir 방향으로 length/2 만큼 이동한 지점이 박스 중심
            Vector2 boxCenter = (Vector2)victimPos + dir * (effectiveLength * 0.5f);
            Vector2 boxSize = new Vector2(effectiveLength, _windBladeWidth);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // 본인 공격력 (StatModifier 합산)
            float playerAttack = combat.WeaponData != null ? combat.WeaponData.BaseDamage : 0f;
            if (statContainer != null)
                playerAttack *= statContainer.GetTotalMultiplier(StatId.AttackPower);
            float bladeDamage = playerAttack * magnitude;
            if (bladeDamage <= 0f) return;

            int hitCount = 0;
            Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, angle);
            foreach (Collider2D col in hits)
            {
                if (col == null) continue;
                Health h = col.GetComponentInParent<Health>();
                if (h == null) continue;
                if (h == victim) continue;        // 본인 제외 (이중 데미지 방지)
                if (h.CurrentHealth <= 0f) continue;
                Character ch = h.GetComponentInParent<Character>();
                if (ch != null && ch.CharacterType == Character.CharacterTypes.Player) continue;

                h.Damage(bladeDamage, gameObject, 0f, 0f, Vector3.zero);
                hitCount++;
            }

            // VFX — victim 위치에서 dir 방향으로 슬래시 + Whirlwind 광역 표시 (Range 반영된 length)
            SpawnWindVFX(victimPos, dir, effectiveLength);

            if (hitCount > 0)
            {
                if (_windBladeSfx != null)
                    AudioSource.PlayClipAtPoint(_windBladeSfx, victimPos, _sfxVolume);

                if (_windHitStopDuration > 0f && KhiHitStopController.Instance != null)
                    KhiHitStopController.Instance.RequestFreeze(_windHitStopDuration, _hitStopFrozenScale);
            }

            if (_logOnHitDispatch)
                Debug.Log($"[OnHit] WindBlade {magnitude:P0} dir={dir} → {hitCount} hits, {bladeDamage:F1} each");
        }

        // ── VFX 스폰 (CL-201) ──────────────────────────────

        private void SpawnChainVFX(Vector3 from, Vector3 to)
        {
            if (_chainHitVFXPrefab == null) return;
            from.z = 0f;
            to.z = 0f;

            GameObject go = VFXSpawner.Spawn(_chainHitVFXPrefab, from, Quaternion.identity, 0.3f);
            if (go == null) return;

            // 지그재그 번개 컴포넌트가 있으면 그쪽이 positionCount/SetPosition 다중 처리.
            JaggedLightningLine jagged = go.GetComponentInChildren<JaggedLightningLine>();
            if (jagged != null)
            {
                jagged.Init(from, to);
                return;
            }

            // 폴백: 단순 2점 직선
            LineRenderer lr = go.GetComponentInChildren<LineRenderer>();
            if (lr != null)
            {
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
            }
        }

        private void SpawnWindVFX(Vector3 origin, Vector2 dir, float length)
        {
            if (_windAOEVFXPrefab == null) return;
            origin.z = 0f;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            GameObject go = VFXSpawner.Spawn(
                _windAOEVFXPrefab,
                origin,
                Quaternion.Euler(0f, 0f, angle),
                _windVFXLifetime);
            if (go == null) return;

            LineRenderer lr = go.GetComponentInChildren<LineRenderer>();
            if (lr != null)
            {
                Vector3 endPoint = origin + (Vector3)(dir * length);
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, origin);
                lr.SetPosition(1, endPoint);
            }

            // Whirlwind 자식 ParticleSystem 의 Shape.radius 동적 조정 (Range 스탯 반영)
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                if (ps.gameObject.name == "Whirlwind")
                {
                    ParticleSystem.ShapeModule shape = ps.shape;
                    shape.radius = _windBladeWidth * 1.5f;
                    break;
                }
            }
        }

        private static EnemyStatusEffect GetOrAddStatus(Health victim)
        {
            if (victim == null) return null;
            // Health 가 적 root 또는 하위 노드에 있을 수 있음 → root 부터 검색
            GameObject host = victim.gameObject;
            EnemyStatusEffect status = host.GetComponent<EnemyStatusEffect>()
                                    ?? host.GetComponentInParent<EnemyStatusEffect>();
            if (status == null)
                status = host.AddComponent<EnemyStatusEffect>();
            return status;
        }

        private static List<Health> FindNearbyEnemies(Vector3 origin, Health exclude, float radius, int maxCount)
        {
            int hits = Physics2D.OverlapCircleNonAlloc(origin, radius, _chainBuf);
            var candidates = new List<(Health h, float distSq)>();
            var seen = new HashSet<Health>();
            for (int i = 0; i < hits; i++)
            {
                Collider2D col = _chainBuf[i];
                if (col == null) continue;
                Health h = col.GetComponentInParent<Health>();
                if (h == null) continue;
                if (h == exclude) continue;
                if (h.CurrentHealth <= 0f) continue;
                // 같은 Health 의 여러 collider 가 잡혀도 1회만 카운트.
                if (!seen.Add(h)) continue;
                // Player 자신 제외 — Player Health 도 잡힐 수 있으니 Character.CharacterType 체크
                Character ch = h.GetComponentInParent<Character>();
                if (ch != null && ch.CharacterType == Character.CharacterTypes.Player) continue;

                float dSq = (h.transform.position - origin).sqrMagnitude;
                candidates.Add((h, dSq));
            }
            candidates.Sort((a, b) => a.distSq.CompareTo(b.distSq));
            return candidates.Take(maxCount).Select(t => t.h).ToList();
        }
    }
}

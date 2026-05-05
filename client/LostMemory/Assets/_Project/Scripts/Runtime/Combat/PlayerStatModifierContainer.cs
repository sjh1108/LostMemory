using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// Player 가 보유한 영구/임시/조건부 stat modifier 를 단일 보관 + 합산 조회.
    /// 후속 Wave (B/C/D) 의 hook 들이 GetTotalMultiplier(StatId) 로 조회.
    /// 합산 정책: total = 1 + Σ(percents). 영구·임시·조건부 모두 동일 누적 (CL-106 결정).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Player Stat Modifier Container")]
    public sealed class PlayerStatModifierContainer : MonoBehaviour
    {
        // 영구 — Wave D 에서 RelicEffectRegistry 가 등록
        private readonly List<PermanentMod> _permanent = new();
        // 임시 — Update() 에서 expiry 체크, 만료 시 제거
        private readonly List<TimedMod> _timed = new();
        // 조건부 — 매 GetTotalMultiplier 호출 시 predicate 평가
        private readonly List<ConditionalMod> _conditional = new();

        [Header("Debug")]
        [SerializeField] private bool logModifierChanges = false;

        public event Action<StatId> ModifiersChanged;

        // ── 등록 API ─────────────────────────────────────

        public void AddPermanent(StatId stat, float magnitude, object source = null)
        {
            _permanent.Add(new PermanentMod { Stat = stat, Magnitude = magnitude, Source = source });
            Notify(stat, $"AddPermanent {stat} {magnitude:+0.0%;-0.0%;0%} src={source}");
        }

        public void AddTimed(StatId stat, float magnitude, float duration, object source = null)
        {
            _timed.Add(new TimedMod
            {
                Stat = stat,
                Magnitude = magnitude,
                ExpiresAt = Time.time + duration,
                Source = source,
            });
            Notify(stat, $"AddTimed {stat} {magnitude:+0.0%;-0.0%;0%} for {duration}s src={source}");
        }

        public void AddConditional(StatId stat, float magnitude, Func<bool> predicate, object source = null)
        {
            _conditional.Add(new ConditionalMod
            {
                Stat = stat,
                Magnitude = magnitude,
                Predicate = predicate,
                Source = source,
            });
            Notify(stat, $"AddConditional {stat} {magnitude:+0.0%;-0.0%;0%} src={source}");
        }

        public void RemoveBySource(object source)
        {
            int removed = _permanent.RemoveAll(m => Equals(m.Source, source))
                        + _timed.RemoveAll(m => Equals(m.Source, source))
                        + _conditional.RemoveAll(m => Equals(m.Source, source));
            if (removed > 0)
            {
                ModifiersChanged?.Invoke(default);
                if (logModifierChanges)
                {
                    Debug.Log($"[StatModifier] Removed {removed} mods by source={source}");
                }
            }
        }

        public void ClearAll()
        {
            _permanent.Clear();
            _timed.Clear();
            _conditional.Clear();
            ModifiersChanged?.Invoke(default);
        }

        // ── 조회 API ─────────────────────────────────────

        /// <summary>
        /// 합연산 multiplier 반환. 1 + Σ(percents).
        /// 예: 영구 0.05 + 임시 0.12 + 조건부 0.10 → 1.27
        /// </summary>
        public float GetTotalMultiplier(StatId stat)
        {
            float sum = 0f;
            foreach (PermanentMod m in _permanent)
            {
                if (m.Stat == stat) sum += m.Magnitude;
            }
            foreach (TimedMod m in _timed)
            {
                if (m.Stat == stat) sum += m.Magnitude;
            }
            foreach (ConditionalMod m in _conditional)
            {
                if (m.Stat == stat && m.Predicate != null && m.Predicate())
                {
                    sum += m.Magnitude;
                }
            }
            return 1f + sum;
        }

        /// <summary>
        /// CL-146: flat 합산 반환 (multiplier 와 별도). DefenseFlat 등 flat 성격 stat 용.
        /// 예: Defense 등록 +4 + +8 → 12 반환. multiplier 적용은 따로.
        /// 다른 곳에서 GetTotalMultiplier(Defense) 호출하지 않도록 주의 — 의미 다름.
        /// </summary>
        public float GetTotalFlat(StatId stat)
        {
            float sum = 0f;
            foreach (PermanentMod m in _permanent)
            {
                if (m.Stat == stat) sum += m.Magnitude;
            }
            foreach (TimedMod m in _timed)
            {
                if (m.Stat == stat) sum += m.Magnitude;
            }
            foreach (ConditionalMod m in _conditional)
            {
                if (m.Stat == stat && m.Predicate != null && m.Predicate())
                {
                    sum += m.Magnitude;
                }
            }
            return sum;
        }

        /// <summary>디버그용. 현재 등록된 modifier 수 (조건부의 평가 결과 무관).</summary>
        public int GetActiveModifierCount(StatId stat)
        {
            int n = 0;
            foreach (PermanentMod m in _permanent)
            {
                if (m.Stat == stat) n++;
            }
            foreach (TimedMod m in _timed)
            {
                if (m.Stat == stat) n++;
            }
            foreach (ConditionalMod m in _conditional)
            {
                if (m.Stat == stat) n++;
            }
            return n;
        }

        // ── 임시 만료 처리 ─────────────────────────────────

        private void Update()
        {
            if (_timed.Count == 0) return;
            float now = Time.time;
            int removed = _timed.RemoveAll(m => m.ExpiresAt <= now);
            if (removed > 0)
            {
                ModifiersChanged?.Invoke(default);
                if (logModifierChanges)
                {
                    Debug.Log($"[StatModifier] Expired {removed} timed mods at t={now:F2}");
                }
            }
        }

        // ── 디버그 ContextMenu ─────────────────────────────

        [ContextMenu("Debug — Add +5% AttackPower (permanent)")]
        private void DebugAddAttackPower()
        {
            AddPermanent(StatId.AttackPower, 0.05f, "DebugContextMenu");
            Debug.Log($"[StatModifier] AttackPower total = {GetTotalMultiplier(StatId.AttackPower):F3}");
        }

        [ContextMenu("Debug — Add +12% AttackSpeed for 3s (timed)")]
        private void DebugAddAttackSpeedTimed()
        {
            AddTimed(StatId.AttackSpeed, 0.12f, 3f, "DebugContextMenu");
            Debug.Log($"[StatModifier] AttackSpeed total = {GetTotalMultiplier(StatId.AttackSpeed):F3}");
        }

        [ContextMenu("Debug — Log all totals")]
        private void DebugLogAllTotals()
        {
            foreach (StatId s in Enum.GetValues(typeof(StatId)))
            {
                Debug.Log($"[StatModifier] {s} = {GetTotalMultiplier(s):F3} (count={GetActiveModifierCount(s)})");
            }
        }

        [ContextMenu("Debug — Clear all modifiers")]
        private void DebugClearAll()
        {
            ClearAll();
            Debug.Log("[StatModifier] Cleared all modifiers.");
        }

        // ── 내부 ─────────────────────────────────────────

        private void Notify(StatId stat, string log)
        {
            ModifiersChanged?.Invoke(stat);
            if (logModifierChanges)
            {
                Debug.Log($"[StatModifier] {log}");
            }
        }

        private struct PermanentMod
        {
            public StatId Stat;
            public float Magnitude;
            public object Source;
        }

        private struct TimedMod
        {
            public StatId Stat;
            public float Magnitude;
            public float ExpiresAt;
            public object Source;
        }

        private struct ConditionalMod
        {
            public StatId Stat;
            public float Magnitude;
            public Func<bool> Predicate;
            public object Source;
        }
    }
}

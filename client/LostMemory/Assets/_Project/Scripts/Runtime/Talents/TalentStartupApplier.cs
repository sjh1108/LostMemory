using LostMemory.Combat;
using LostMemory.Data;
using LostMemory.Memory;
using LostMemory.Relics;
using LostMemory.Stage;
using UnityEngine;

namespace LostMemory.Talents
{
    /// <summary>
    /// 런 시작 시(DungeonBuilt) 재능 스탯과 기억 영구 보상을 PlayerStatModifierContainer 에 등록.
    ///
    /// 적용 항목:
    ///   1. 재능(Talent) 투자 스탯 — TalentSaveService 로드 → TalentCalculator 계산 → container 등록
    ///   2. 기억 영구 스탯 부스트  — MemorySaveData.PermanentBoosts → container 등록
    ///   3. 유물 슬롯 확장        — MemorySaveData.PermanentBonusRelicSlots → PlayerRelicInventory.AddSlots()
    ///
    /// 런 재시작 안전: Apply() 시작 시 RemoveBySource(this) 로 이전 런 modifier 를 초기화.
    /// 유물 슬롯은 _appliedRelicSlots 추적으로 중복 누적 방지.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Talents/Talent Startup Applier")]
    public sealed class TalentStartupApplier : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TalentData[] _talentDatas;
        [SerializeField, Min(1)] private int _totalPoints = 10;
        [SerializeField] private PlayerStatModifierContainer _container;
        [SerializeField] private DungeonRunBootstrap _bootstrap;
        [SerializeField] private PlayerRelicInventory _relicInventory;

        [Header("Debug")]
        [SerializeField] private bool _logApply = true;

        // 이전 런에서 AddSlots() 로 적용한 보너스 슬롯 수. 재실행 시 차감 후 재적용.
        private int _appliedRelicSlots;

        private void OnEnable()
        {
            if (_bootstrap != null)
                _bootstrap.DungeonBuilt += Apply;
        }

        private void OnDisable()
        {
            if (_bootstrap != null)
                _bootstrap.DungeonBuilt -= Apply;
        }

        // ── 적용 진입점 ──────────────────────────────────────

        private void Apply()
        {
            if (_container == null)
            {
                Debug.LogWarning("[TalentStartupApplier] PlayerStatModifierContainer 미연결.", this);
                return;
            }

            // 이전 런 modifier 전부 초기화 (런 재시작 안전)
            _container.RemoveBySource(this);

            ApplyTalentStats();

            MemorySaveData save = MemoryMetaService.Load();
            ApplyMemoryBoosts(save);
            ApplyMemoryRelicSlots(save);
        }

        // ── 재능 스탯 ────────────────────────────────────────

        private void ApplyTalentStats()
        {
            var saved = TalentSaveService.Load();
            var model = new TalentModel(_talentDatas, _totalPoints, saved);
            RunStartStats stats = TalentCalculator.Calculate(model);

            if (stats.CriticalRate != 0f)
                _container.AddPermanent(StatId.Critical, stats.CriticalRate, this);
            if (stats.AttackSpeed != 0f)
                _container.AddPermanent(StatId.AttackSpeed, stats.AttackSpeed, this);
            if (stats.Defense != 0f)
                _container.AddPermanent(StatId.Defense, stats.Defense, this);
            if (stats.MaxHealth != 0f)
                _container.AddPermanent(StatId.MaxHealth, stats.MaxHealth, this);
            if (stats.ManaRegen != 0f)
                _container.AddPermanent(StatId.ManaRegen, stats.ManaRegen, this);

            if (_logApply)
            {
                Debug.Log($"[TalentStartupApplier] 재능 스탯 적용 — " +
                          $"Critical={stats.CriticalRate:+0.0%;-0.0%;0%} " +
                          $"AttackSpeed={stats.AttackSpeed:+0.0%;-0.0%;0%} " +
                          $"Defense={stats.Defense:F2} " +
                          $"MaxHealth={stats.MaxHealth:+0.0%;-0.0%;0%}", this);
            }
        }

        // ── 기억 영구 스탯 부스트 ─────────────────────────────

        private void ApplyMemoryBoosts(MemorySaveData save)
        {
            foreach (var boost in save.PermanentBoosts)
                _container.AddPermanent(boost.Stat, boost.Magnitude, this);

            if (_logApply && save.PermanentBoosts.Count > 0)
            {
                Debug.Log($"[TalentStartupApplier] 기억 영구 스탯 {save.PermanentBoosts.Count}건 적용", this);
            }
        }

        // ── 유물 슬롯 확장 ────────────────────────────────────

        private void ApplyMemoryRelicSlots(MemorySaveData save)
        {
            if (_relicInventory == null) return;

            int target = save.PermanentBonusRelicSlots;

            // 이전 적용분 먼저 차감 (런 재시작 시 중복 누적 방지)
            if (_appliedRelicSlots != 0)
                _relicInventory.AddSlots(-_appliedRelicSlots);

            if (target > 0)
                _relicInventory.AddSlots(target);

            _appliedRelicSlots = target;

            if (_logApply && target > 0)
            {
                Debug.Log($"[TalentStartupApplier] 유물 슬롯 +{target} 적용", this);
            }
        }

        // ── 디버그 ContextMenu ────────────────────────────────

        [ContextMenu("Debug — Apply now")]
        private void DebugApplyNow() => Apply();
    }
}

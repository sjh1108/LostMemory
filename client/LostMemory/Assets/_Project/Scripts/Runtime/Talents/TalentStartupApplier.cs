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

        private void Awake()
        {
            ResolveRefsIfNeeded();
        }

        private void OnEnable()
        {
            // Awake 시점에 다른 컴포넌트가 늦게 spawn 됐을 수 있어 한 번 더 resolve.
            if (_bootstrap == null || _container == null || _relicInventory == null)
            {
                ResolveRefsIfNeeded();
            }

            if (_bootstrap != null)
                _bootstrap.DungeonBuilt += Apply;
            else
                Debug.LogWarning("[TalentStartupApplier] DungeonRunBootstrap 미연결 — 자동 resolve 실패. " +
                                 "씬에 Bootstrap 이 있는지 확인하거나 Inspector 에서 직접 드래그.", this);
        }

        private void OnDisable()
        {
            if (_bootstrap != null)
                _bootstrap.DungeonBuilt -= Apply;
        }

        private void Start()
        {
            // DungeonArchitect 로 build 되지 않는 사전 제작 씬(1F-2R 등)에선 DungeonBuilt 이벤트가
            // 발화되지 않아 Apply 가 호출 안 됨. Start 시점에 명시적으로 한 번 호출해 보장.
            // DA 씬(1F-1R 등)에선 OnEnable 구독으로 Apply 가 별도 호출되지만,
            // Apply 내부에서 _container.RemoveBySource(this) 로 이전 modifier 를 클리어하므로 중복 누적 X.
            Apply();
        }

        /// <summary>
        /// Inspector 에 직접 할당되지 않은 참조를 씬에서 자동 검색.
        /// 던전 씬마다 수동 와이어링 부담을 줄임 (할당돼 있으면 그것 우선).
        /// </summary>
        private void ResolveRefsIfNeeded()
        {
            if (_container == null)
            {
                _container = FindAnyObjectByType<PlayerStatModifierContainer>(FindObjectsInactive.Include);
            }
            if (_bootstrap == null)
            {
                _bootstrap = FindAnyObjectByType<DungeonRunBootstrap>(FindObjectsInactive.Include);
            }
            if (_relicInventory == null)
            {
                _relicInventory = FindAnyObjectByType<PlayerRelicInventory>(FindObjectsInactive.Include);
            }
        }

        // ── 적용 진입점 ──────────────────────────────────────

        private void Apply()
        {
            // 던전 빌드 시점에 플레이어가 늦게 spawn 된 케이스 — 마지막 한 번 더 resolve 시도.
            if (_container == null || _relicInventory == null)
            {
                ResolveRefsIfNeeded();
            }

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
            if (stats.MoveSpeed != 0f)
                _container.AddPermanent(StatId.MoveSpeed, stats.MoveSpeed, this);

            if (_logApply)
            {
                Debug.Log($"[TalentStartupApplier] 재능 스탯 적용 — " +
                          $"Critical={stats.CriticalRate:+0.0%;-0.0%;0%} " +
                          $"AttackSpeed={stats.AttackSpeed:+0.0%;-0.0%;0%} " +
                          $"Defense={stats.Defense:F2} " +
                          $"MaxHealth={stats.MaxHealth:+0.0%;-0.0%;0%} " +
                          $"MoveSpeed={stats.MoveSpeed:+0.0%;-0.0%;0%}", this);
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

using LostMemory.Combat;
using LostMemory.Data;
using LostMemory.Memory;
using LostMemory.Networking.Player;
using LostMemory.Relics;
using LostMemory.Stage;
using LostMemory.TestKhi;
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
        /// <summary>
        /// 재능 stat 의 공통 source identifier. TalentStartupApplier 와 TalentPanelView 가
        /// 같은 source 로 등록/제거해야 중복 누적이 발생하지 않는다.
        /// </summary>
        public static readonly object Source = new object();

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
            // 마을 씬에는 DungeonRunBootstrap 이 없는 게 정상이라 경고 생략 (Start 에서 Apply 호출됨).

            // 재능 저장 직후 즉시 stat 반영 — 마을에서 HP 늘려도 HUD/캐릭터에 곧바로 적용.
            TalentSaveService.Saved += HandleTalentSaved;

            // 멀티 fix: 게스트는 dungeon scene 로드 직후 LocalPlayer spawn 이 늦을 수 있고,
            // inspector-wire 된 scene-placed Player 의 _container 는 NGO 활성 시 EditorTestCharacterMarker 가 destroy.
            // LocalPlayerReady 발화 시 정확한 player 측 container 로 재bind + 재적용.
            LocalPlayerResolver.LocalPlayerReady += HandleLocalPlayerReady;
        }

        private void OnDisable()
        {
            if (_bootstrap != null)
                _bootstrap.DungeonBuilt -= Apply;
            TalentSaveService.Saved -= HandleTalentSaved;
            LocalPlayerResolver.LocalPlayerReady -= HandleLocalPlayerReady;
        }

        private void HandleTalentSaved()
        {
            Debug.Log("[TalentStartupApplier] TalentSaveService.Saved 수신 → 즉시 재적용", this);
            Apply();
        }

        private void HandleLocalPlayerReady(KhiPlayerStateAggregator _)
        {
            // refs 무효화 → 다음 Apply 의 ResolveRefsIfNeeded 가 LocalPlayer 측 새 container/inventory 조회.
            _container = null;
            _relicInventory = null;
            Debug.Log("[TalentStartupApplier] LocalPlayerReady 수신 → refs 재조회 + 재적용", this);
            Apply();
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
        ///
        /// 멀티 fix: dungeon scene 에 inspector-wire 된 _container 는 scene-placed Player 의 컴포넌트인데,
        /// NGO 활성 시 EditorTestCharacterMarker 가 즉시 Destroy 함 → Unity pseudo-null →
        /// FindAnyObjectByType 의 owner 비구분으로 host/guest race. LocalPlayer 측에서 명시 조회.
        /// </summary>
        private void ResolveRefsIfNeeded()
        {
            // player-side: LocalPlayer 의 컴포넌트 우선. 솔로/Editor 단일 씬은 LocalPlayerResolver 가
            // fallback 으로 첫 Character 잡아 기존 동작 유지. LocalPlayer 가 아직 spawn 전이면 null —
            // OnEnable 의 LocalPlayerReady 구독이 spawn 후 재시도.
            if (_container == null || !IsLocalPlayerComponent(_container))
            {
                _container = LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerStatModifierContainer>();
            }
            if (_relicInventory == null || !IsLocalPlayerComponent(_relicInventory))
            {
                _relicInventory = LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerRelicInventory>();
            }

            // DungeonRunBootstrap 은 scene-level singleton (player 측 아님) — 기존 fallback 유지.
            if (_bootstrap == null)
            {
                _bootstrap = FindAnyObjectByType<DungeonRunBootstrap>(FindObjectsInactive.Include);
            }
        }

        /// <summary>주어진 컴포넌트가 현재 LocalCharacter 의 자식(또는 본체)인지 확인. 잘못된 player 잡혔는지 self-heal 가드.</summary>
        private static bool IsLocalPlayerComponent(Component c)
        {
            if (c == null) return false;
            var localChar = LocalPlayerResolver.LocalCharacter;
            if (localChar == null) return false;
            return c.transform == localChar.transform || c.transform.IsChildOf(localChar.transform);
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

            // 이전 modifier 전부 초기화 (런 재시작 안전 + 마을에서 TalentPanelView 가 등록한 것도 정리).
            _container.RemoveBySource(Source);

            MemorySaveData save = MemoryMetaService.Load();
            ApplyTalentStats(save);
            ApplyMemoryBoosts(save);
            ApplyMemoryRelicSlots(save);
        }

        // ── 재능 스탯 ────────────────────────────────────────

        private void ApplyTalentStats(MemorySaveData memorySave)
        {
            var saved = TalentSaveService.Load();
            int effectiveTotal = _totalPoints + memorySave.BonusTalentPoints;
            var model = new TalentModel(_talentDatas, effectiveTotal, saved);
            RunStartStats stats = TalentCalculator.Calculate(model);

            if (stats.CriticalRate != 0f)
                _container.AddPermanent(StatId.Critical, stats.CriticalRate, Source);
            if (stats.AttackSpeed != 0f)
                _container.AddPermanent(StatId.AttackSpeed, stats.AttackSpeed, Source);
            if (stats.Defense != 0f)
                _container.AddPermanent(StatId.Defense, stats.Defense, Source);
            // CL-234: 재능 MaxHealth 는 flat track 으로 등록 (multiplier 합산 폭주 방지).
            //         IncreasePerPoint=1 + 10포인트 = +10 HP. PlayerHealthStatApplier 가 (base+flat)*mul 로 합성.
            if (stats.MaxHealth != 0f)
                _container.AddPermanent(StatId.MaxHealthFlat, stats.MaxHealth, Source);
            if (stats.MoveSpeed != 0f)
                _container.AddPermanent(StatId.MoveSpeed, stats.MoveSpeed, Source);

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
            {
                _container.AddPermanent(boost.Stat, boost.Magnitude, Source);
                if (_logApply)
                    Debug.Log($"[TalentStartupApplier] 기억 보스트 적용: {boost.Stat} +{boost.Magnitude:F2} (source={boost.SourcePieceId})", this);
            }

            if (_logApply)
            {
                Debug.Log($"[TalentStartupApplier] ApplyMemoryBoosts 완료 — {save.PermanentBoosts.Count}건", this);
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

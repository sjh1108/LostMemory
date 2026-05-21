using System;
using System.Collections;
using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Relics;
using LostMemory.TestKhi;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-204 (CL-144/145 후속): 미소녀 spawner — Player 에 부착.
    ///
    /// 책임:
    /// - PlayerRelicInventory.OnRelicAcquired hook → RelicData _effects 검사:
    ///     Type=26 (MagicalGirlElementalAttack) → 해당 visual 미소녀 추가 (없으면 spawn)
    ///     Type=27 (MagicalGirlElementalEnhanced) → 해당 visual 미소녀 enhanced 플래그
    /// - 5종 visual 별 1명씩 최대 cap (5명).
    /// - SetEffectApplicator (CL-140) 의 MagicalGirlSummon/Fusion case 가 SetCount(tierCount) 호출:
    ///     N>=5 → ultimate available + 5세트 강화 broadcast
    ///     N<5  → ultimate off
    ///     본 메서드는 미소녀 spawn/despawn 안 함 (HandleRelicAcquired 가 주도, OnCleared 가 일괄 정리).
    /// - T/Y 키 입력 → ultimate 발동: 5명 hide → MagicalGirlFusion 임시 spawn + 발화 → burst 종료 후 5명 복귀.
    ///   T = laser burst (5s 지속), Y = AOE pulse (즉발).
    /// - Fusion 공유 cooldown (NotifyBurstStarted) 그대로 활용.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagicalGirlSpawner : MonoBehaviour
    {
        [Tooltip("미소녀가 따라다닐 기준 transform. 비워두면 self transform.")]
        [SerializeField] private Transform anchor;

        [Tooltip("같은 GameObject 의 PlayerStatModifierContainer. 데미지 계산용.")]
        [SerializeField] private PlayerStatModifierContainer playerStat;

        [Tooltip("같은 GameObject 의 KhiMeleeComboController. WeaponData.BaseDamage 조회.")]
        [SerializeField] private KhiMeleeComboController playerCombat;

        [Tooltip("같은 GameObject 의 PlayerRelicInventory. 미소녀 visual hook.")]
        [SerializeField] private PlayerRelicInventory inventory;

        [Tooltip("같은 GameObject 의 KhiPlayerAim. ultimate 레이저 마우스 조준 hook.")]
        [SerializeField] private KhiPlayerAim playerAim;
        [SerializeField] private KhiDownController downController;

        [Tooltip("CL-204: visual → sprite/공격 패턴 매핑 카탈로그. 미설정 시 fallback (placeholder sprite + 즉시 데미지).")]
        [SerializeField] private MagicalGirlAttackCatalog attackCatalog;

        [Tooltip("원형 배치 반경 (유닛).")]
        [SerializeField, Min(0.1f)] private float ringRadius = 1.5f;

        [Tooltip("Ultimate 발동 시 미소녀 fade out/in 시간 (초).")]
        [SerializeField, Min(0f)] private float ultimateFadeSeconds = 0.3f;

        [Tooltip("Ultimate burst (T 레이저) 지속 시간 (초). 끝나면 5명 복귀.")]
        [SerializeField, Min(0.1f)] private float ultimateLaserBurstDuration = 5f;

        [Tooltip("Ultimate AOE (Y 펄스) 후 5명 복귀까지 대기 (초). 화면 플래시·카메라 흔들림 자연 종료.")]
        [SerializeField, Min(0f)] private float ultimateAOERestoreDelay = 0.5f;

        [Tooltip("Ultimate cooldown (초). T/Y 한 번 사용하면 둘 다 막힘.")]
        [SerializeField, Min(0f)] private float ultimateCooldown = 25f;

        [Tooltip("spawn / despawn / 시각 변화 / fusion 진입·종료 로그.")]
        [SerializeField] private bool _logSpawn = false;

        [Header("Follow Polish (CL-204)")]
        [Tooltip("미소녀가 Player 를 따라가는 부드러움 (초). 작을수록 즉각, 클수록 lag.")]
        [SerializeField, Min(0.01f)] private float followSmoothTime = 0.3f;

        [Tooltip("미소녀 위아래 부유 진폭 (유닛). 0 = 부유 없음.")]
        [SerializeField, Min(0f)] private float followBobAmplitude = 0.1f;

        [Tooltip("미소녀 부유 속도 (사이클/초).")]
        [SerializeField, Min(0.01f)] private float followBobSpeed = 1.5f;

        [Tooltip("Cluster 전체 거리 multiplier. 1.0 = 기본, 1.5 = 50% 더 멀리, 0.5 = 절반 가깝게. 모든 미소녀 동시 적용.")]
        [SerializeField, Range(0.1f, 3.0f)] private float clusterScale = 0.66f;

        [Tooltip("Cluster 전체 평행이동 (facing-right 기준). 모든 미소녀 동시 적용. X 양수 = player 방향, Y 양수 = 위.")]
        [SerializeField] private Vector2 clusterCenter = Vector2.zero;

        [Header("Fusion Visual (CL-204 B10)")]
        [Tooltip("Fusion 미소녀 sprite (5세트 ultimate 발동 시 등장). null 이면 sprite 없이 entity 만 spawn.")]
        [SerializeField] private Sprite fusionSprite;

        [Tooltip("Fusion sprite 의 Order in Layer. 미소녀 5명보다 위로 배치 (기본 130).")]
        [SerializeField] private int fusionSortingOrder = 130;

        [Tooltip("Fusion sprite scale (transform.localScale 적용). girl.png 와 동일 픽셀 아트면 5~10 권장 (girl 1명과 비슷한 크기).")]
        [SerializeField, Min(0.1f)] private float fusionScale = 5f;

        [Tooltip("Fusion sprite 애니메이션 frames. 비어있으면 fusionSprite 단일 sprite 만 사용. 채워져 있으면 frameInterval 마다 순환.")]
        [SerializeField] private Sprite[] fusionAnimationFrames;

        [Tooltip("애니메이션 frame 간 시간 (초). 0.1 = 빠른 애니메이션, 0.3 = 느린 호흡감.")]
        [SerializeField, Min(0.01f)] private float fusionAnimationFrameInterval = 0.15f;

        [Tooltip("Fusion 등장 시 alpha 0 → 1 페이드 시간 (초). 0 이면 즉시 등장.")]
        [SerializeField, Min(0f)] private float fusionFadeInDuration = 0.4f;

        [Tooltip("Fusion 소멸 시 alpha 1 → 0 페이드 시간 (초). 페이드 완료 후 GameObject destroy. 0 이면 즉시 소멸.")]
        [SerializeField, Min(0f)] private float fusionFadeOutDuration = 0.4f;

        [Tooltip("셰이더 기반 수직 그라데이션 — 이 값 아래 UV.y 부분은 alpha 0 (안 보임). 0=완전 sprite 바닥, 1=완전 sprite 꼭대기. 기본 0 = 꺼짐.")]
        [SerializeField, Range(0f, 1f)] private float fusionGradientStart = 0.0f;

        [Tooltip("셰이더 기반 수직 그라데이션 — 이 값 위 UV.y 부분은 alpha 1 (완전 보임). Start < End 여야 의미 있음. 기본 0 = 꺼짐.")]
        [SerializeField, Range(0f, 1f)] private float fusionGradientEnd = 0.0f;

        [Tooltip("Fusion 최종 위치 조정 — formation center 에 더해지는 추가 offset. (0,0) = formation center 그대로. X 는 facing 따라 mirror.")]
        [SerializeField] private Vector2 fusionPositionOffset = Vector2.zero;

        [Tooltip("Fusion 등장 시 시작 높이 (units, 최종 위치 기준). 양수 = 위에서 내려옴. fadeInDuration 동안 0 으로 수렴.")]
        [SerializeField, Min(0f)] private float fusionDescentHeight = 3f;

        [Tooltip("Fusion 강림 시작점의 수평(X) offset. 0 = 수직 강림. 양수 = facing 방향 *앞쪽* 에서 내려옴 (왼쪽 조준 시 자동 mirror, OFF 일 시 그대로). 어색한 사이드 효과 보정용.")]
        [SerializeField] private float fusionDescentHorizontalOffset = 0f;

        [Tooltip("Fusion 강림 (descent) 시간 (초). 페이드와 독립. 0 = 즉시 도착, 큰 값 = 천천히 내려옴.")]
        [SerializeField, Min(0f)] private float fusionDescentDuration = 0.5f;

        [Tooltip("Fusion 승천 (ascent) 시간 (초). 페이드와 독립. 0 = 즉시 사라짐, 큰 값 = 천천히 올라감.")]
        [SerializeField, Min(0f)] private float fusionAscentDuration = 0.5f;

        [Tooltip("Fusion 의 X 위치를 facing 방향에 따라 mirror 할지. ON (기본) = 일반 미소녀와 동일하게 좌/우 대칭. OFF = mirror 무시, X 고정 위치 (player 의 왼쪽 또는 오른쪽 한 방향).")]
        [SerializeField] private bool fusionMirrorXOnFacing = true;

        [Tooltip("Fusion 의 위아래 부유 진폭 (둥실둥실). 0 = 부유 없음 (강림 후 정지). 일반 미소녀의 followBobAmplitude 와 별도.")]
        [SerializeField, Min(0f)] private float fusionBobAmplitude = 0f;

        [Header("Fusion AOE Charge (CL-204 후속 — Y 빌드업 → 폭발)")]
        [Tooltip("Y 키 발동 후 데미지가 fire 되기까지 빌드업 시간 (초). 이 시간 동안 charge VFX 가 크기 lerp.")]
        [SerializeField, Min(0f)] private float aoeChargeDuration = 0.5f;

        [Tooltip("Charge VFX 의 시작 크기 (작게). lerp 시작값.")]
        [SerializeField, Min(0.01f)] private float aoeChargeStartScale = 0.3f;

        [Tooltip("Charge VFX 의 끝 크기 (펑 직전). lerp 끝값. 크게 설정할수록 \"많이 모인 후 터지는\" 느낌.")]
        [SerializeField, Min(0.01f)] private float aoeChargeEndScale = 2.0f;

        public float AoeChargeDuration => aoeChargeDuration;
        public float AoeChargeStartScale => aoeChargeStartScale;
        public float AoeChargeEndScale => aoeChargeEndScale;

        [Header("Fusion VFX (CL-204 후속 — T/Y 궁극 시각)")]
        [Tooltip("T 레이저 시작점 muzzle burst / 빔 영역 trail / Y AOE 폭발 / Y AOE 링 shockwave 4종 prefab. null 슬롯은 시각 생략 (데미지·LineRenderer 빔은 정상).")]
        [SerializeField] private MagicalGirlFusion.FusionVfxBundle fusionVfx;

        [Header("Debug (개발 편의 — 빌드 전 OFF 권장)")]
        [Tooltip("체크 시 5세트 ultimate 의 25초 공유 쿨다운 무시. T/Y 키 연타 가능 (T burst 5s 진행 중에는 막힘). VFX 반복 검증용.")]
        [SerializeField] private bool debugSkipCooldown;

        [Tooltip("Trail VFX 부모 GameObject 의 transform Z 회전 오프셋 (도). 빔 방향 + offset. 양수=왼쪽(CCW), 음수=오른쪽(CW). emission box 영역의 방향을 결정. 파티클 sprite 회전은 prefab 의 Renderer Alignment=Local 에 위임.")]
        [SerializeField, Range(-180f, 180f)] private float trailRotationOffset = -45f;

        [Tooltip("Trail VFX spawn 위치 보정 (빔 local 좌표 기준). X = 빔 진행 방향 (+: 앞, -: 뒤), Y = 빔 수직 방향 (+: 왼쪽, -: 오른쪽). boxCenter 에서부터 offset 만큼 이동.")]
        [SerializeField] private Vector2 trailSpawnOffset = Vector2.zero;

        public float TrailRotationOffset => trailRotationOffset;
        public Vector2 TrailSpawnOffset => trailSpawnOffset;

        [Tooltip("미소녀 별 facing-right 기준 local offset. facing-left 시 X 자동 mirror. List 의 visual 키로 lookup.")]
        [SerializeField] private List<FormationEntry> formationOffsets = new()
        {
            new FormationEntry { visual = MagicalGirlVisual.Fire,      offset = new Vector2(-2.5f, 0.5f) },
            new FormationEntry { visual = MagicalGirlVisual.Ice,       offset = new Vector2(-2.7f, 1.5f) },
            new FormationEntry { visual = MagicalGirlVisual.Star,      offset = new Vector2(-1.3f, 1.5f) },
            new FormationEntry { visual = MagicalGirlVisual.Blackhole, offset = new Vector2(-2.0f, 2.5f) },
            new FormationEntry { visual = MagicalGirlVisual.Arrow,     offset = new Vector2(-1.5f, 0.5f) },
        };

        /// <summary>visual 별 formation offset 한 줄. Inspector 에서 List 로 편집.</summary>
        [Serializable]
        public struct FormationEntry
        {
            public MagicalGirlVisual visual;
            public Vector2 offset;
        }

        // visual → AI 1:1 매핑. 5명 cap.
        private readonly Dictionary<MagicalGirlVisual, MagicalGirlAI> _girlsByVisual = new();
        private readonly HashSet<MagicalGirlVisual> _enhancedVisuals = new();

        // CL-204 Follow Polish: formation offsets 는 위 SerializeField formationOffsets 가 보유.
        // 아래 헬퍼는 visual → offset (clusterScale 곱) lookup.

        private bool _setBonusActive;             // 5세트(BuildSet T5) 도달 여부
        private bool _ultimateAvailable;          // 5세트 시 true
        private bool _ultimateActive;             // T/Y 발동 ~ burst 종료 사이 true
        private float _ultimateCooldownEndsAt;    // 공유 쿨다운 시점
        private GameObject _fusionInstance;        // ultimate 임시 인스턴스
        private Coroutine _fusionAnimCoroutine;    // CL-204 후속: 애니메이션 frame cycle 코루틴 참조 (despawn 시 stop)
        private SpriteRenderer _fusionSpriteRenderer;  // CL-204 후속: 그라데이션 값 push 용 캐시
        private MaterialPropertyBlock _fusionMPB;      // CL-204 후속: 셰이더 _GradientStart/_End 동적 갱신 (Material 인스턴스화 회피)
        private Vector2 _fusionExtraOffset;            // CL-204 후속: 강림 중 baseOffset 에 더해지는 추가 Y offset (descent 동안 fusionDescentHeight → 0 으로 감소)
        private static readonly int FusionGradientStartId = Shader.PropertyToID("_GradientStart");
        private static readonly int FusionGradientEndId = Shader.PropertyToID("_GradientEnd");

        public float FusionCooldownEndsAt => _ultimateCooldownEndsAt;

        /// <summary>HUD presenter 폴링용. 5인 합체 + 비활성 + 쿨다운 종료 모두 충족 시 true.</summary>
        public bool IsUltimateReady => _setBonusActive && !_ultimateActive && Time.time >= _ultimateCooldownEndsAt;

        /// <summary>HUD presenter 폴링용. 5인 합체 상태 자체 (쿨다운/발동 무관).</summary>
        public bool IsSetBonusActive => _setBonusActive;

        private const int MaxGirls = 5;

        private void OnEnable()
        {
            ResolveDownController();

            string anchorWiring = anchor != null ? "OK" : "self";
            string statWiring = playerStat != null ? "OK" : "❌ NULL";
            string combatWiring = playerCombat != null ? "OK" : "❌ NULL";
            string inventoryWiring = inventory != null ? "OK" : "❌ NULL (visual hook 동작 X)";
            string aimWiring = playerAim != null ? "OK" : "⚠ NULL (ultimate 레이저 fallback=right)";
            string catalogWiring = attackCatalog != null ? "OK" : "⚠ NULL (placeholder 즉시 데미지)";
            Debug.Log($"[MagicalGirlSpawner] OnEnable — wiring: anchor={anchorWiring}, playerStat={statWiring}, playerCombat={combatWiring}, inventory={inventoryWiring}, playerAim={aimWiring}, catalog={catalogWiring}", this);

            if (inventory != null)
            {
                inventory.OnRelicAcquired += HandleRelicAcquired;
                // CL-204: Run 종료 시 모든 미소녀 + 강화 플래그 정리.
                inventory.OnCleared += HandleInventoryCleared;

                // 씬 전환 시 Player 가 새로 스폰되어 본 Spawner 도 함께 새로 생성되므로,
                // PlayerRunState 로 복구된 인벤토리에 이미 들어있는 미소녀 유물에 대해 visual 재 spawn.
                // 첫 게임 시작 시 OwnedRelics 가 비어 있으면 무동작.
                IReadOnlyList<RelicData> owned = inventory.OwnedRelics;
                for (int i = 0; i < owned.Count; i++)
                {
                    RelicData r = owned[i];
                    if (r != null) HandleRelicAcquired(r);
                }
            }
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.OnRelicAcquired -= HandleRelicAcquired;
                inventory.OnCleared -= HandleInventoryCleared;
            }
        }

        private void HandleInventoryCleared()
        {
            ClearAllGirls();
            _setBonusActive = false;
            _ultimateAvailable = false;
            BroadcastSetBonus();
            if (_logSpawn) Debug.Log("[MagicalGirl] Inventory cleared — all girls + flags reset");
        }

        private void Update()
        {
            // CL-204 Follow Polish: Inspector 의 follow tuning 값 → 모든 spawned follower 에 매 프레임 push.
            // 5명 × ~6 setter = trivial 비용. Play 모드 중 Inspector 변경 즉시 반영.
            SyncFollowTuningToAll();

            // CL-204 후속: Fusion 그라데이션 셰이더 _GradientStart/_End 값 매 프레임 push (Inspector 슬라이더 실시간 반영)
            SyncFusionGradientToShader();

            // CL-204: ultimate 입력 처리 (5세트 + cooldown ready 시)
            if (Time.timeScale == 0f) return;
            if (KhiPlayerActionGate.IsBlocked(ResolveDownController())) return;
            if (!_ultimateAvailable || _ultimateActive) return;
            // CL-204 후속: debugSkipCooldown 체크 시 쿨다운 우회 — VFX 반복 검증용.
            if (!debugSkipCooldown && Time.time < _ultimateCooldownEndsAt) return;
            if (Keyboard.current == null) return;
            if (Keyboard.current.tKey.wasPressedThisFrame) StartCoroutine(UltimateLaserCoroutine());
            else if (Keyboard.current.yKey.wasPressedThisFrame) StartCoroutine(UltimateAOECoroutine());
        }

        private KhiDownController ResolveDownController()
        {
            if (downController != null)
            {
                return downController;
            }

            KhiPlayerActionGate.TryResolveDownController(this, out downController);
            return downController;
        }

        // ── public API (SetEffectApplicator 호출) ───────────

        /// <summary>
        /// CL-204: SetEffectApplicator (BuildSet tier 변화) 가 호출.
        /// 미소녀 *수* 는 HandleRelicAcquired 가 visual 단위로 관리 — 본 메서드는 *flag* 만 갱신:
        ///   N>=5 → setBonus + ultimate available
        ///   N<5  → setBonus / ultimate off
        /// 미소녀 destroy 는 inventory.OnCleared (Run 종료) 가 일괄 처리.
        /// </summary>
        public void SetCount(int newCount)
        {
            newCount = Mathf.Max(0, newCount);

            bool wasSet = _setBonusActive;
            _setBonusActive = newCount >= 5;
            _ultimateAvailable = _setBonusActive;

            if (wasSet != _setBonusActive)
            {
                BroadcastSetBonus();
                if (_logSpawn)
                    Debug.Log($"[MagicalGirl] SetBonus={_setBonusActive} (newCount={newCount}, ultimateAvailable={_ultimateAvailable})");
            }

            EnforceCap();
        }

        /// <summary>CL-145 호환: fusion burst 시작 시 cooldown 예약.</summary>
        public void NotifyBurstStarted(float burstDuration, float cooldown)
        {
            _ultimateCooldownEndsAt = Time.time + burstDuration + cooldown;
            if (_logSpawn)
                Debug.Log($"[MagicalGirl] Ultimate cooldown 예약 — ends at {_ultimateCooldownEndsAt:F1} (now+{burstDuration + cooldown:F1}s)");
        }

        /// <summary>CL-204: visual 별 미소녀 1명 spawn (이미 있으면 no-op). 5명 cap.</summary>
        public void AddGirlByVisual(MagicalGirlVisual visual)
        {
            if (visual == MagicalGirlVisual.Default) return;
            if (_girlsByVisual.ContainsKey(visual)) return;
            if (_girlsByVisual.Count >= MaxGirls)
            {
                if (_logSpawn) Debug.Log($"[MagicalGirl] cap={MaxGirls} reached, skip {visual}");
                return;
            }

            var go = new GameObject($"MagicalGirl_{visual}");
            // CL-204 Follow Polish: 부모-자식 parenting 제거 — world space 독립.
            // 위치는 MagicalGirlFollower 가 매 LateUpdate 에서 SmoothDamp 로 anchor + offset 따라감.
            Transform anchorT = anchor != null ? anchor : transform;
            go.transform.position = anchorT.position;  // 첫 프레임 즉시 점프 회피용 초기 위치
            var ai = go.AddComponent<MagicalGirlAI>();
            ai.Init(playerStat, playerCombat, attackCatalog, ResolveDownController());
            ai.SetVisual(visual);
            ai.SetSetBonusActive(_setBonusActive);
            ai.SetEnhanced(_enhancedVisuals.Contains(visual));
            _girlsByVisual[visual] = ai;

            // CL-204 Follow Polish: 부드럽게 따라오는 follower + behind+above formation offset
            var follower = go.AddComponent<MagicalGirlFollower>();
            Vector2 off = GetFormationOffset(visual);
            follower.Init(anchorT, playerAim, off);
            // Spawner Inspector 에서 tuning 한 값을 follower 에 push (영구 저장 가능)
            follower.SmoothTime = followSmoothTime;
            follower.BobAmplitude = followBobAmplitude;
            follower.BobSpeed = followBobSpeed;

            // 호버 툴팁(숨은 능력 설명). Collider2D + MagicalGirlHoverTooltip 자동 부착.
            var hoverCol = go.AddComponent<CircleCollider2D>();
            hoverCol.isTrigger = true;
            hoverCol.radius = 0.6f;
            var tooltip = go.AddComponent<MagicalGirlHoverTooltip>();
            tooltip.SetVisual(visual);

            if (_logSpawn) Debug.Log($"[MagicalGirl] +{visual} (count={_girlsByVisual.Count})");
        }

        /// <summary>CL-204: Type=27 강화 적용. visual 미소녀가 spawn 되어 있으면 즉시 반영, 없어도 플래그 보관 후 spawn 시 적용.</summary>
        public void MarkEnhanced(MagicalGirlVisual visual)
        {
            if (visual == MagicalGirlVisual.Default) return;
            _enhancedVisuals.Add(visual);
            if (_girlsByVisual.TryGetValue(visual, out var ai) && ai != null)
                ai.SetEnhanced(true);
            if (_logSpawn) Debug.Log($"[MagicalGirl] Enhanced+ {visual}");
        }

        // ── 인벤토리 hook ──────────────────────────────────

        private void HandleRelicAcquired(RelicData r)
        {
            if (r == null) return;
            // 미소녀 태그 가진 RelicData 만 처리
            bool primaryIsGirl = r.TagPrimary == RelicTag.MagicalGirl;
            bool secondaryIsGirl = r.TagSecondary == RelicTag.MagicalGirl;
            if (!primaryIsGirl && !secondaryIsGirl) return;

            // 미소녀가 아닌 쪽 태그가 속성. 양쪽 다 미소녀면 (TagPrimary == TagSecondary) Default 유지.
            RelicTag elementTag = primaryIsGirl ? r.TagSecondary : r.TagPrimary;
            if (elementTag == RelicTag.MagicalGirl || elementTag == RelicTag.None)
                return;

            MagicalGirlVisual visual = MagicalGirlVisualPalette.FromTag(elementTag);
            if (visual == MagicalGirlVisual.Default) return;

            // _effects 검사: Summon/Elemental 모두 visual 미소녀 spawn 트리거.
            // Type=24 (Summon) — "어둠의 미소녀", "빛의 미소녀", "분홍 리본" 등 다수 RelicData 가 사용
            // Type=26 (Elemental) — "별 모양 단추", "얼음 결정" 등 명시적 속성 공격
            // Type=27 (Enhanced) — 강화 (visual 보장 spawn 후 강화 플래그)
            // 5명 cap 은 visual 단위라, 같은 visual 의 추가 RelicData 는 no-op (보조 아이템 = stat 만).
            bool hasSummonOrElemental = false;
            bool hasEnhanced = false;
            if (r.Effects != null)
            {
                foreach (EffectEntry e in r.Effects)
                {
                    if (e.Type == RelicEffectType.MagicalGirlSummon ||
                        e.Type == RelicEffectType.MagicalGirlElementalAttack)
                        hasSummonOrElemental = true;
                    else if (e.Type == RelicEffectType.MagicalGirlElementalEnhanced)
                        hasEnhanced = true;
                }
            }

            // CL-204: Summon/Elemental/Enhanced 모두 visual 미소녀 보장 spawn.
            // Enhanced 만 있고 visual 미소녀 없으면 → 미소녀 먼저 spawn 후 강화.
            if (hasSummonOrElemental || hasEnhanced)
                AddGirlByVisual(visual);
            if (hasEnhanced)
                MarkEnhanced(visual);
        }

        // ── 내부 ────────────────────────────────────────────

        private void EnforceCap()
        {
            // visual 단위 cap. 6+ 가 들어올 일은 없지만 safeguard.
            if (_girlsByVisual.Count <= MaxGirls) return;
            // 마지막 N - MaxGirls 개를 제거 (insertion order 유지 안 됨, Dictionary 순서 의존 X — 임시 list 통해 가장 최근 추가된 것 제거)
            // 단순화: 그냥 임의 visual 제거.
            int over = _girlsByVisual.Count - MaxGirls;
            var toRemove = new List<MagicalGirlVisual>();
            foreach (var kv in _girlsByVisual)
            {
                if (toRemove.Count >= over) break;
                toRemove.Add(kv.Key);
            }
            foreach (var v in toRemove) RemoveGirl(v);
        }

        private void RemoveGirl(MagicalGirlVisual visual)
        {
            if (!_girlsByVisual.TryGetValue(visual, out var ai)) return;
            _girlsByVisual.Remove(visual);
            if (ai != null) Destroy(ai.gameObject);
            // CL-204 Follow Polish: RebalancePositions() 호출 제거 — Follower 가 visual 단위 고정 offset 사용, 재배치 불필요
        }

        private void ClearAllGirls()
        {
            foreach (var kv in _girlsByVisual)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _girlsByVisual.Clear();
            _enhancedVisuals.Clear();
        }

        private void BroadcastSetBonus()
        {
            foreach (var kv in _girlsByVisual)
                if (kv.Value != null) kv.Value.SetSetBonusActive(_setBonusActive);
        }

        // CL-204 Follow Polish: RebalancePositions 폐기. 각 미소녀의 위치는 MagicalGirlFollower 가
        // visual 별 offset (formationOffsets List × clusterScale) + facing mirror + bob 으로 매 프레임 계산.
        // ringRadius 필드는 이제 사용되지 않음 (Inspector 에서 보일 수 있으나 무시 — 후속 cleanup 가능).

        /// <summary>visual 의 formation offset (facing-right 기준) × clusterScale + clusterCenter.</summary>
        private Vector2 GetFormationOffset(MagicalGirlVisual visual)
        {
            for (int i = 0; i < formationOffsets.Count; i++)
            {
                if (formationOffsets[i].visual == visual)
                    return formationOffsets[i].offset * clusterScale + clusterCenter;
            }
            return clusterCenter;  // visual 미등록 시에도 center 만 반영
        }

        /// <summary>
        /// CL-204 B10.1: 5명 미소녀 offsets 평균 × clusterScale + clusterCenter = formation 의 시각적 중심.
        /// Fusion 등장 시 spawn 위치로 사용 (5명이 모이는 자리 = 합체 트로프).
        /// </summary>
        private Vector2 GetFormationCenter()
        {
            if (formationOffsets == null || formationOffsets.Count == 0) return clusterCenter;
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (var entry in formationOffsets)
            {
                sum += entry.offset;
                count++;
            }
            if (count == 0) return clusterCenter;
            return (sum / count) * clusterScale + clusterCenter;
        }

        /// <summary>
        /// 매 Update 마다 spawner 의 tuning 값을 모든 follower 에 push.
        /// Play 모드 중 Inspector 에서 값 변경 시 *실시간* 반영.
        /// </summary>
        private void SyncFollowTuningToAll()
        {
            // 5명 미소녀
            foreach (var kv in _girlsByVisual)
            {
                if (kv.Value == null) continue;
                var follower = kv.Value.GetComponent<MagicalGirlFollower>();
                if (follower == null) continue;
                follower.SmoothTime = followSmoothTime;
                follower.BobAmplitude = followBobAmplitude;
                follower.BobSpeed = followBobSpeed;
                follower.SetBaseOffset(GetFormationOffset(kv.Key));
            }

            // CL-204 B10.1 / CL-204 후속: Fusion 은 *전용* bob amplitude 사용 (강림 후 정지 연출용 0)
            // baseOffset 에 _fusionExtraOffset 더해서 강림 중에는 위에서 출발 → 0 으로 수렴
            if (_fusionInstance != null)
            {
                var fusionFollower = _fusionInstance.GetComponent<MagicalGirlFollower>();
                if (fusionFollower != null)
                {
                    fusionFollower.SmoothTime = followSmoothTime;
                    fusionFollower.BobAmplitude = fusionBobAmplitude;  // fusion 전용 (기본 0)
                    fusionFollower.BobSpeed = followBobSpeed;
                    fusionFollower.SetBaseOffset(GetFormationCenter() + fusionPositionOffset + _fusionExtraOffset);
                }
            }
        }

        // ── Ultimate (T/Y) ─────────────────────────────────

        private IEnumerator UltimateLaserCoroutine()
        {
            _ultimateActive = true;
            yield return FadeGirls(active: false);
            EnsureFusion();
            var fusion = _fusionInstance != null ? _fusionInstance.GetComponent<MagicalGirlFusion>() : null;
            if (fusion != null) fusion.TriggerLaserBurst();
            // burst 종료 + cooldown 시작은 fusion 측에서 NotifyBurstStarted 가 처리.
            yield return new WaitForSeconds(ultimateLaserBurstDuration);
            DespawnFusion();
            yield return FadeGirls(active: true);
            _ultimateActive = false;
        }

        private IEnumerator UltimateAOECoroutine()
        {
            _ultimateActive = true;
            yield return FadeGirls(active: false);
            EnsureFusion();
            var fusion = _fusionInstance != null ? _fusionInstance.GetComponent<MagicalGirlFusion>() : null;
            if (fusion != null) fusion.TriggerAOEPulse();
            yield return new WaitForSeconds(ultimateAOERestoreDelay);
            DespawnFusion();
            yield return FadeGirls(active: true);
            _ultimateActive = false;
        }

        private IEnumerator FadeGirls(bool active)
        {
            float duration = ultimateFadeSeconds;
            if (duration <= 0f)
            {
                foreach (var kv in _girlsByVisual)
                    if (kv.Value != null) kv.Value.gameObject.SetActive(active);
                yield break;
            }
            // 5명 sprite alpha 페이드
            float t = 0f;
            // 캐싱
            var renderers = new List<SpriteRenderer>();
            foreach (var kv in _girlsByVisual)
            {
                if (kv.Value == null) continue;
                var sr = kv.Value.GetComponent<SpriteRenderer>();
                if (sr != null) renderers.Add(sr);
            }
            if (active)
            {
                // 활성화 후 fade in
                foreach (var sr in renderers)
                {
                    if (sr.gameObject != null) sr.gameObject.SetActive(true);
                    sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0f);
                }
            }
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float alpha = active ? k : 1f - k;
                foreach (var sr in renderers)
                {
                    if (sr == null) continue;
                    Color c = sr.color;
                    sr.color = new Color(c.r, c.g, c.b, alpha);
                }
                yield return null;
            }
            // fade out 종료 후 비활성화
            if (!active)
            {
                foreach (var sr in renderers)
                    if (sr != null && sr.gameObject != null) sr.gameObject.SetActive(false);
            }
        }

        private void EnsureFusion()
        {
            if (_fusionInstance != null) return;
            _fusionInstance = new GameObject("MagicalGirlFusion");
            // CL-204 B10.1: parent-child 제거. world-space 로 spawn 후 Follower 가 위치 관리.
            Transform anchorT = anchor != null ? anchor : transform;
            _fusionInstance.transform.position = anchorT.position;
            _fusionInstance.transform.localScale = Vector3.one * fusionScale;

            // CL-204 B10 / CL-204 후속: Fusion 미소녀 sprite — 단일 fusionSprite 또는 fusionAnimationFrames[0] 으로 초기화
            SpriteRenderer fusionSR = null;
            Sprite initialSprite = (fusionAnimationFrames != null && fusionAnimationFrames.Length > 0)
                ? fusionAnimationFrames[0]
                : fusionSprite;
            if (initialSprite != null)
            {
                fusionSR = _fusionInstance.AddComponent<SpriteRenderer>();
                fusionSR.sprite = initialSprite;
                fusionSR.sortingLayerName = "Foreground";
                fusionSR.sortingOrder = fusionSortingOrder;

                // CL-204 후속: 셰이더 기반 그라데이션 — Start/End 둘 다 0 이 아니면 (= 사용자가 설정함) 셰이더 적용
                if (fusionGradientEnd > 0f)
                {
                    var gradientShader = Shader.Find("LostMemory/Sprites/Alpha Gradient");
                    if (gradientShader != null)
                    {
                        // .material 접근하면 instance 가 생성됨 — 1 fusion = 1 material 이라 OK.
                        var mat = new Material(gradientShader);
                        fusionSR.material = mat;
                    }
                    else if (_logSpawn)
                    {
                        Debug.LogWarning("[MagicalGirlSpawner] Shader 'LostMemory/Sprites/Alpha Gradient' 못 찾음. 그라데이션 비활성. shader 파일 import 됐는지 확인.");
                    }
                }
            }

            _fusionSpriteRenderer = fusionSR;

            var fusion = _fusionInstance.AddComponent<MagicalGirlFusion>();
            fusion.Init(playerStat, playerCombat, playerAim, this, fusionVfx, ResolveDownController());

            // CL-204 B10.1 / CL-204 후속: Follower 부착 — formation 중앙 + 강림 시작 높이 만큼 위로 elevated
            // 매 frame SyncFollowTuningToAll 가 (GetFormationCenter + _fusionExtraOffset) 으로 baseOffset 갱신
            // DescendFusion 코루틴이 _fusionExtraOffset.y 를 fusionDescentHeight → 0 으로 감소시켜 내려오는 연출
            var follower = _fusionInstance.AddComponent<MagicalGirlFollower>();
            Vector2 fusionOffset = GetFormationCenter() + fusionPositionOffset;
            _fusionExtraOffset = new Vector2(fusionDescentHorizontalOffset, fusionDescentHeight);  // 시작은 위 (+ 수평 offset)
            // CL-204 후속: fusionMirrorXOnFacing OFF 시 playerAim null 로 전달 → Follower 가 X mirror 안 함 → 고정 위치
            KhiPlayerAim followerAim = fusionMirrorXOnFacing ? playerAim : null;
            follower.Init(anchorT, followerAim, fusionOffset + _fusionExtraOffset);  // 초기 위치 elevated

            follower.SmoothTime = followSmoothTime;
            follower.BobAmplitude = fusionBobAmplitude;  // fusion 전용 (기본 0)
            follower.BobSpeed = followBobSpeed;

            // CL-204 후속: Fade In + Animation 코루틴 시작 (SpriteRenderer 가 있을 때만)
            if (fusionSR != null)
            {
                if (fusionFadeInDuration > 0f)
                    StartCoroutine(FadeInSprite(fusionSR, fusionFadeInDuration));

                if (fusionAnimationFrames != null && fusionAnimationFrames.Length > 1)
                    _fusionAnimCoroutine = StartCoroutine(AnimateFusionSprite(fusionSR, fusionAnimationFrames, fusionAnimationFrameInterval));
            }

            // CL-204 후속: 강림 연출 — 별도 fusionDescentDuration 으로 속도 제어 (페이드와 독립)
            if (fusionDescentHeight > 0f && fusionDescentDuration > 0f)
                StartCoroutine(DescendFusion(fusionDescentDuration));
            else
                _fusionExtraOffset = Vector2.zero;  // 강림 X → 즉시 정상 위치
        }

        private void DespawnFusion()
        {
            if (_fusionInstance == null) return;

            // CL-204 후속: 애니메이션 stop + 승천 + fade out 후 destroy
            if (_fusionAnimCoroutine != null)
            {
                StopCoroutine(_fusionAnimCoroutine);
                _fusionAnimCoroutine = null;
            }

            // 승천 — fusionInstance 의 follower 를 직접 참조해서 baseOffset 을 위로 lerp.
            // _fusionInstance = null 후에는 SyncFollowTuningToAll 가 fusion 건너뛰므로 충돌 없음.
            // 별도 fusionAscentDuration 으로 속도 제어 (페이드와 독립)
            if (fusionDescentHeight > 0f && fusionAscentDuration > 0f)
            {
                var ascendFollower = _fusionInstance.GetComponent<MagicalGirlFollower>();
                if (ascendFollower != null)
                    StartCoroutine(AscendFusion(ascendFollower, fusionAscentDuration));
            }

            if (fusionFadeOutDuration > 0f)
                StartCoroutine(FadeOutAndDestroy(_fusionInstance, fusionFadeOutDuration));
            else
                Destroy(_fusionInstance);

            _fusionInstance = null;          // 참조 즉시 클리어 (재진입 방지)
            _fusionSpriteRenderer = null;    // 그라데이션 push 대상도 클리어
        }

        // CL-204 후속: Fade In — alpha 0 → 1
        private IEnumerator FadeInSprite(SpriteRenderer sr, float duration)
        {
            if (sr == null) yield break;
            Color c = sr.color;
            c.a = 0f;
            sr.color = c;

            float t = 0f;
            while (t < duration && sr != null)
            {
                t += Time.deltaTime;
                c.a = Mathf.Clamp01(t / duration);
                sr.color = c;
                yield return null;
            }
            if (sr != null)
            {
                c.a = 1f;
                sr.color = c;
            }
        }

        // CL-204 후속: Fade Out 후 destroy — fusion despawn 시 사용
        private IEnumerator FadeOutAndDestroy(GameObject go, float duration)
        {
            if (go == null) yield break;
            var sr = go.GetComponent<SpriteRenderer>();
            float startAlpha = sr != null ? sr.color.a : 1f;
            float t = 0f;
            while (t < duration && go != null)
            {
                t += Time.deltaTime;
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(startAlpha, 0f, t / duration);
                    sr.color = c;
                }
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // CL-204 후속: 강림 — _fusionExtraOffset.y 를 fusionDescentHeight → 0 으로 lerp (EaseOutCubic).
        // SyncFollowTuningToAll 가 매 frame _fusionExtraOffset 를 baseOffset 에 적용 → 자연스럽게 내려옴.
        private IEnumerator DescendFusion(float duration)
        {
            Vector2 start = new Vector2(fusionDescentHorizontalOffset, fusionDescentHeight);
            float t = 0f;
            while (t < duration && _fusionInstance != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - k, 3f);  // EaseOutCubic — 빠르게 시작, 천천히 도착
                _fusionExtraOffset = Vector2.Lerp(start, Vector2.zero, eased);
                yield return null;
            }
            _fusionExtraOffset = Vector2.zero;
        }

        // CL-204 후속: 승천 — follower 의 baseOffset 을 0 → fusionDescentHeight 위로 lerp (EaseInCubic).
        // _fusionInstance 가 이미 null 이라 SyncFollowTuningToAll 가 건드리지 않으므로 직접 setBaseOffset.
        private IEnumerator AscendFusion(MagicalGirlFollower follower, float duration)
        {
            if (follower == null || duration <= 0f) yield break;
            Vector2 baseOffset = GetFormationCenter() + fusionPositionOffset;
            Vector2 end = new Vector2(fusionDescentHorizontalOffset, fusionDescentHeight);
            float t = 0f;
            while (t < duration && follower != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = Mathf.Pow(k, 3f);  // EaseInCubic — 천천히 시작, 빠르게 상승
                follower.SetBaseOffset(baseOffset + Vector2.Lerp(Vector2.zero, end, eased));
                yield return null;
            }
        }

        // CL-204 후속: 셰이더 _GradientStart/_End 값 push — MaterialPropertyBlock 으로 인스턴스화 최소화.
        // Inspector 슬라이더가 매 프레임 반영되도록 Update 에서 호출.
        private void SyncFusionGradientToShader()
        {
            if (_fusionSpriteRenderer == null) return;
            if (_fusionMPB == null) _fusionMPB = new MaterialPropertyBlock();

            _fusionSpriteRenderer.GetPropertyBlock(_fusionMPB);
            _fusionMPB.SetFloat(FusionGradientStartId, fusionGradientStart);
            _fusionMPB.SetFloat(FusionGradientEndId, fusionGradientEnd);
            _fusionSpriteRenderer.SetPropertyBlock(_fusionMPB);
        }

        // CL-204 후속: Sprite frame cycling 애니메이션 (KhiSlashAnimator.PlayFrames 패턴 재사용)
        private IEnumerator AnimateFusionSprite(SpriteRenderer sr, Sprite[] frames, float interval)
        {
            if (sr == null || frames == null || frames.Length == 0) yield break;
            int idx = 0;
            var wait = new WaitForSeconds(interval);
            while (sr != null)
            {
                sr.sprite = frames[idx];
                idx = (idx + 1) % frames.Length;
                yield return wait;
            }
        }
    }
}

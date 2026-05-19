using System;
using System.Collections;
using System.Collections.Generic;
using LostMemory.Rendering;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Rena
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Rena/Rena Boss Spell Combat Controller")]
    public sealed class RenaBossSpellCombatController : MonoBehaviour
    {
        private enum CastKind
        {
            Fireball,
            Inferno,
            IceSweep,
            ThunderStrike,
            Thunderbolt
        }

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Health health;
        [SerializeField] private RenaBossWanderController wanderController;
        [SerializeField] private Transform projectileSpawnOrigin;
        [SerializeField] private RenaBossBalanceData balanceData;

        [Header("Animation States")]
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string fastCastStateName = "fastCast";
        [SerializeField] private string heavyCastStateName = "HeavyCast";
        [SerializeField] private string thunderStrikeStateName = "thunderStrike";
        [SerializeField] private string thunderboltStateName = "thunderbolt";
        [SerializeField, Min(0)] private int animationLayer;
        [SerializeField] private bool flipVisualByDirection = true;
        [SerializeField] private bool invertFlipX;

        [Header("Targeting")]
        [SerializeField, Min(0f)] private float detectionRadius = 16f;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField] private LayerMask obstacleLayerMask = (1 << 8) | (1 << 11) | (1 << 24);
        [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.2f;

        [Header("Attack Pacing")]
        [SerializeField, Min(0f)] private float attackRecoveryDuration = 0.85f;
        [SerializeField, Min(0f)] private float phaseTwoAttackRecoveryDuration = 1.05f;

        [Header("Fireball")]
        [SerializeField, Min(0f)] private float firstFireballDelay = 1.2f;
        [SerializeField, Min(0f)] private float fireballCooldown = 2.85f;
        [SerializeField, Min(0f)] private float fireballRange = 14f;
        [SerializeField, Min(0.01f)] private float fireballCastDuration = 0.85f;
        [SerializeField, Min(0f)] private float fireballReleaseTime = 0.32f;
        [SerializeField, Min(0f)] private float fireballDamage = 10f;
        [SerializeField, Min(0.01f)] private float fireballSpeed = 7.5f;
        [SerializeField, Min(0.01f)] private float fireballLifetime = 3.2f;
        [SerializeField, Min(0.05f)] private float fireballHitRadius = 0.32f;
        [SerializeField, Min(0.01f)] private float fireballVisualScale = 1.15f;
        [SerializeField, Min(1)] private int fireballMaximumHits = 1;
        [SerializeField] private bool fireballDestroyOnHit = true;
        [SerializeField, Min(1)] private int fireballProjectileCount = 5;
        [SerializeField, Range(0f, 120f)] private float fireballTotalSpreadAngle = 40f;
        [SerializeField] private bool fireballHomingEnabled = true;
        [SerializeField, Min(0f)] private float fireballHomingTurnRateDegrees = 120f;
        [SerializeField, Min(0f)] private float fireballHomingStartDelay = 0.12f;
        [SerializeField, Min(0f)] private float fireballHomingEndDistance = 0.65f;
        [SerializeField, Min(0f)] private float fireballHomingLaneSpacing = 0.65f;
        [SerializeField, Min(0f)] private float fireballHomingForwardSpacing = 0.18f;
        [SerializeField] private Sprite[] fireballCastedFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] fireballFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] fireballHitFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] fireballCastedGlowFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] fireballGlowFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] fireballHitGlowFrames = Array.Empty<Sprite>();

        [Header("Inferno")]
        [SerializeField, Min(0f)] private float firstInfernoDelay = 4f;
        [SerializeField, Min(0f)] private float infernoCooldown = 9f;
        [SerializeField, Min(0f)] private float infernoRange = 13f;
        [SerializeField, Min(0.01f)] private float infernoCastDuration = 1.65f;
        [SerializeField, Min(0f)] private float infernoReleaseTime = 0.95f;
        [SerializeField, Min(0f)] private float infernoDamage = 16f;
        [SerializeField, Min(0.01f)] private float infernoSpeed = 5.8f;
        [SerializeField, Min(0.01f)] private float infernoLifetime = 2.8f;
        [SerializeField, Min(0.05f)] private float infernoHitRadius = 0.48f;
        [SerializeField, Min(0.01f)] private float infernoVisualScale = 1.35f;
        [SerializeField, Min(1)] private int infernoMaximumHits = 4;
        [SerializeField] private bool infernoDestroyOnHit;
        [SerializeField, Min(1)] private int infernoProjectileCount = 3;
        [SerializeField, Range(0f, 120f)] private float infernoTotalSpreadAngle = 36f;
        [SerializeField] private Sprite[] infernoCastedFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] infernoFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] infernoHitFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] infernoCastedGlowFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] infernoGlowFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] infernoHitGlowFrames = Array.Empty<Sprite>();

        [Header("Ice Sweep")]
        [SerializeField, Min(0f)] private float firstIceSweepDelay = 6f;
        [SerializeField, Min(0f)] private float iceSweepCooldown = 21f;
        [SerializeField] private bool iceSweepUseHealthThresholds = true;
        [SerializeField, Range(0.01f, 1f)] private float iceSweepFirstHealthThreshold = 0.7f;
        [SerializeField, Range(0.01f, 1f)] private float iceSweepSecondHealthThreshold = 0.4f;
        [SerializeField, Range(0.01f, 1f)] private float iceSweepThirdHealthThreshold = 0.1f;
        [SerializeField, Min(1)] private int iceSweepFirstThresholdAttackCount = 4;
        [SerializeField, Min(1)] private int iceSweepSecondThresholdAttackCount = 6;
        [SerializeField, Min(1)] private int iceSweepThirdThresholdAttackCount = 6;
        [SerializeField, Min(0f)] private float iceSweepRange = 16f;
        [SerializeField, Min(0.01f)] private float iceSweepCastDuration = 1.55f;
        [SerializeField, Min(0f)] private float iceSweepReleaseTime = 1.55f;
        [SerializeField, Min(0f)] private float iceSweepDamage = 18f;
        [SerializeField] private bool iceSweepCenterFollowsBoss = true;
        [SerializeField] private Vector2 iceSweepAreaCenter = Vector2.zero;
        [SerializeField] private Vector2 iceSweepAreaSize = new Vector2(24f, 12f);
        [SerializeField, Min(1)] private int iceSweepAttackCount = 6;
        [SerializeField, Min(1)] private int phaseTwoIceSweepAttackCount = 6;
        [SerializeField, Min(1)] private int iceSweepColumnCount = 28;
        [SerializeField, Min(2)] private int iceSweepRowCount = 4;
        [SerializeField, Min(0f)] private float iceSweepCellOverlap = 0.25f;
        [SerializeField, Min(0f)] private float iceSweepRowGap = 0f;
        [SerializeField, Min(0f)] private float iceSweepWarningDuration = 0.85f;
        [SerializeField] private bool iceSweepFinalThresholdThunderStrikeEnabled = true;
        [SerializeField, Min(1)] private int iceSweepFinalThresholdThunderStrikeCount = 24;
        [SerializeField, Min(0f)] private float iceSweepFinalThresholdThunderStrikeWarningDuration = 0.45f;
        [SerializeField, Min(0f)] private float iceSweepFinalThresholdThunderStrikeStunDuration = 0.35f;
        [SerializeField, Min(0f)] private float iceSweepPillarSpawnInterval = 0.02f;
        [SerializeField, Min(0f)] private float iceSweepWaveInterval = 0.45f;
        [SerializeField, Min(0f)] private float iceSweepCellDamageDelay = 0.08f;
        [SerializeField] private bool iceSweepSkipBlockedCells = true;
        [SerializeField, Min(0.01f)] private float iceSweepVisualScale = 1.4f;
        [SerializeField] private Vector2 iceSweepPillarVisualJitter = new Vector2(0.22f, 0.32f);
        [SerializeField, Min(1)] private int iceSweepPillarVisualRows = 2;
        [SerializeField, Min(0f)] private float iceSweepAnimationFrameRate = 18f;
        [SerializeField] private string iceSweepSortingLayerName = "Foreground";
        [SerializeField] private int iceSweepSortingOrder = 18;
        [SerializeField] private Transform iceSweepShieldRoot;
        [SerializeField] private Sprite[] iceSweepShieldFrames = Array.Empty<Sprite>();
        [SerializeField, Min(1)] private int iceSweepShieldLoopStartFrame = 10;
        [SerializeField, Min(1)] private int iceSweepShieldLoopEndFrame = 12;
        [SerializeField, Min(0.01f)] private float iceSweepShieldFrameRate = 18f;
        [SerializeField, Min(0.01f)] private float iceSweepShieldVisualScale = 1f;
        [SerializeField] private string iceSweepShieldSortingLayerName = "Foreground";
        [SerializeField] private int iceSweepShieldSortingOrder = 22;
        [SerializeField] private Sprite[] iceSweepFrames = Array.Empty<Sprite>();

        [Header("Phase 2")]
        [SerializeField, Range(0.01f, 1f)] private float phaseTwoHealthThreshold = 0.5f;
        [SerializeField] private string phaseTwoIntroStateName = "Rest";
        [SerializeField] private string phaseTwoFallbackIntroStateName = "Entry";
        [SerializeField, Min(0f)] private float phaseTwoAnimatorIntroDuration = 2.25f;
        [SerializeField] private bool makeInvulnerableDuringPhaseTransition = true;
        [SerializeField, Min(0f)] private float phaseTwoOpeningInfernoDelay = 0.9f;
        [SerializeField, Min(1)] private int phaseTwoInfernoProjectileCount = 6;
        [SerializeField, Range(0f, 180f)] private float phaseTwoInfernoTotalSpreadAngle = 72f;
        [SerializeField] private string phaseTwoInfernoStateName = "Pose2";

        [Header("Thunder Strike")]
        [SerializeField, Min(0f)] private float firstThunderStrikeDelay = 5f;
        [SerializeField, Min(0f)] private float thunderStrikeCooldown = 13f;
        [SerializeField, Min(0f)] private float thunderStrikeRange = 14f;
        [SerializeField, Min(0.01f)] private float thunderStrikeCastDuration = 1.55f;
        [SerializeField, Min(0f)] private float thunderStrikeReleaseTime = 0.6f;
        [SerializeField, Min(0f)] private float thunderStrikeDamage = 13f;
        [SerializeField, Min(1)] private int thunderStrikeAreaCount = 10;
        [SerializeField, Min(0f)] private float thunderStrikeSpawnInterval = 0.12f;
        [SerializeField, Min(0f)] private float thunderStrikeSpawnRadiusAroundTarget = 3.5f;
        [SerializeField, Min(0.05f)] private float thunderStrikeRadius = 0.85f;
        [SerializeField, Min(0f)] private float thunderStrikeWarningDuration = 0.85f;
        [SerializeField, Min(0.01f)] private float thunderStrikeVisualScale = 1.2f;
        [SerializeField, Min(0f)] private float thunderStrikeAnimationFrameRate = 18f;
        [SerializeField] private Sprite[] thunderStrikeFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] thunderStrikeGlowFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] phaseTwoThunderStrikeFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] phaseTwoThunderStrikeGlowFrames = Array.Empty<Sprite>();
        [SerializeField] private bool phaseTwoThunderStrikeHitStun = true;
        [SerializeField, Min(0f)] private float phaseTwoThunderStrikeHitStunDuration = 0.35f;

        [Header("Thunderbolt")]
        [SerializeField, Min(0f)] private float firstThunderboltDelay = 8f;
        [SerializeField, Min(0f)] private float thunderboltCooldown = 16f;
        [SerializeField, Min(0f)] private float thunderboltRange = 13f;
        [SerializeField, Min(0.01f)] private float thunderboltCastDuration = 3.4f;
        [SerializeField, Min(0f)] private float thunderboltReleaseTime = 0.45f;
        [SerializeField, Min(0f)] private float thunderboltDamage = 6f;
        [SerializeField, Min(0f)] private float thunderboltBuildDuration = 0.55f;
        [SerializeField, Min(0.05f)] private float thunderboltDuration = 2.4f;
        [SerializeField, Min(0.01f)] private float phaseTwoThunderboltCastDuration = 8.2f;
        [SerializeField, Min(0.05f)] private float phaseTwoThunderboltDuration = 7.2f;
        [SerializeField, Min(0.1f)] private float thunderboltLength = 15f;
        [SerializeField, Min(0.05f)] private float thunderboltWidth = 1.05f;
        [SerializeField] private float thunderboltRotationDegrees = 360f;
        [SerializeField] private bool phaseTwoThunderboltSpawnOppositeBeam = true;
        [SerializeField] private float phaseTwoThunderboltRotationDegrees = 1080f;
        [SerializeField, Min(0f)] private float thunderboltDamageInterval = 0.35f;
        [SerializeField, Min(0f)] private float thunderboltAnimationFrameRate = 18f;
        [SerializeField] private Sprite[] thunderboltFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] thunderboltGlowFrames = Array.Empty<Sprite>();

        [Header("Projectile Visuals")]
        [SerializeField] private string projectileSortingLayerName = "Foreground";
        [SerializeField] private int projectileSortingOrder = 24;
        [SerializeField, Min(0f)] private float projectileAnimationFrameRate = 12f;
        [SerializeField, Min(0.01f)] private float projectileGlowScaleMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float projectileGlowAlpha = 0.75f;
        [SerializeField, Min(0f)] private float targetInvincibilityDuration = 0.35f;
        [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.75f, 0.25f);

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        private Transform _target;
        private Health _targetHealth;
        private Vector2 _lastFacingDirection = Vector2.right;
        private CastKind _currentCast;
        private float _nextTargetRefreshTime;
        private float _nextFireballAllowedAt;
        private float _nextInfernoAllowedAt;
        private float _nextIceSweepAllowedAt;
        private float _nextThunderStrikeAllowedAt;
        private float _nextThunderboltAllowedAt;
        private float _nextAnyCastAllowedAt;
        private float _castStartedAt;
        private float _castEndsAt;
        private float _releaseAt;
        private bool _isCasting;
        private bool _released;
        private Coroutine _thunderStrikeSequence;
        private Coroutine _iceSweepSequence;
        private Coroutine _phaseTransitionRoutine;
        private bool _isPhaseTwo;
        private bool _isPhaseTransitioning;
        private bool _phaseTransitionChangedInvulnerability;
        private bool _phaseTransitionPreviousInvulnerable;
        private int _nextIceSweepHealthThresholdIndex;
        private int _pendingIceSweepHealthThresholdIndex = -1;
        private int _activeIceSweepHealthThresholdIndex = -1;
        private Coroutine _iceSweepShieldRoutine;
        private SpriteRenderer _iceSweepShieldRenderer;
        private bool _iceSweepChangedInvulnerability;
        private bool _iceSweepPreviousInvulnerable;
        private bool _iceSweepShieldBreaking;

        private readonly List<SpriteRenderer> _iceSweepRowWarnings = new List<SpriteRenderer>();

        private const int IceSweepWarningTextureSize = 16;
        private const int IceSweepHealthThresholdCount = 3;
        private static Sprite _iceSweepWarningSprite;

        public bool IsPhaseTwoActive => _isPhaseTwo;
        public bool IsPhaseTransitioning => _isPhaseTransitioning;

        private void Reset()
        {
            RefreshReferences();
        }

        private void Awake()
        {
            RefreshReferences();
            ApplyBalanceDataIfAvailable(resetCurrentHealth: false);
        }

        private void OnEnable()
        {
            RefreshReferences();
            RenaBossBalanceData.ValuesChanged += HandleBalanceDataChanged;
            ApplyBalanceDataIfAvailable(resetCurrentHealth: true);
            _isCasting = false;
            _released = false;
            _isPhaseTwo = false;
            _isPhaseTransitioning = false;
            _phaseTransitionRoutine = null;
            _phaseTransitionChangedInvulnerability = false;
            _nextFireballAllowedAt = Time.time + firstFireballDelay;
            _nextInfernoAllowedAt = Time.time + firstInfernoDelay;
            _nextIceSweepAllowedAt = Time.time + firstIceSweepDelay;
            _nextThunderStrikeAllowedAt = Time.time + firstThunderStrikeDelay;
            _nextThunderboltAllowedAt = Time.time + firstThunderboltDelay;
            _nextAnyCastAllowedAt = Time.time;
            _nextTargetRefreshTime = 0f;
            ResetIceSweepHealthThresholdState();
        }

        private void OnDisable()
        {
            RenaBossBalanceData.ValuesChanged -= HandleBalanceDataChanged;
            _isCasting = false;
            _released = false;

            if (_phaseTransitionRoutine != null)
            {
                StopCoroutine(_phaseTransitionRoutine);
                _phaseTransitionRoutine = null;
            }

            StopThunderStrikeSequence();
            StopIceSweepSequence();
            RestorePhaseTransitionProtection();
            _isPhaseTransitioning = false;

            if (wanderController != null && Application.isPlaying)
            {
                wanderController.enabled = false;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || IsDead())
            {
                return;
            }

            RefreshTargetIfNeeded();

            if (TryStartPhaseTwoTransition())
            {
                return;
            }

            UpdateIceSweepHealthThresholdState();

            if (_isCasting)
            {
                TickCast();
                return;
            }

            TryStartNextCast();
        }

        public void RefreshReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(includeInactive: true);
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);
            }

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (wanderController == null)
            {
                wanderController = GetComponent<RenaBossWanderController>();
            }

            if (projectileSpawnOrigin == null)
            {
                projectileSpawnOrigin = transform;
            }
        }

        private void TryStartNextCast()
        {
            if (_iceSweepSequence != null)
            {
                return;
            }

            if (Time.time < _nextAnyCastAllowedAt)
            {
                return;
            }

            if (!IsTargetUsable())
            {
                return;
            }

            Vector2 toTarget = _target.position - transform.position;
            float sqrDistance = toTarget.sqrMagnitude;

            if (CanStartPendingIceSweep())
            {
                StartCast(CastKind.IceSweep);
                return;
            }

            if (CanCastAtRange(_nextThunderboltAllowedAt, thunderboltRange, sqrDistance))
            {
                StartCast(CastKind.Thunderbolt);
                return;
            }

            if (CanCastAtRange(_nextThunderStrikeAllowedAt, thunderStrikeRange, sqrDistance))
            {
                StartCast(CastKind.ThunderStrike);
                return;
            }

            if (!iceSweepUseHealthThresholds && CanCastAtRange(_nextIceSweepAllowedAt, iceSweepRange, sqrDistance))
            {
                StartCast(CastKind.IceSweep);
                return;
            }

            if (CanCastAtRange(_nextInfernoAllowedAt, infernoRange, sqrDistance))
            {
                StartCast(CastKind.Inferno);
                return;
            }

            if (CanCastAtRange(_nextFireballAllowedAt, fireballRange, sqrDistance))
            {
                StartCast(CastKind.Fireball);
            }
        }

        private static bool CanCastAtRange(float allowedAt, float range, float sqrDistance)
        {
            return Time.time >= allowedAt && sqrDistance <= range * range;
        }

        private bool CanStartPendingIceSweep()
        {
            return iceSweepUseHealthThresholds
                   && _pendingIceSweepHealthThresholdIndex >= 0
                   && HasFrames(iceSweepFrames);
        }

        private void StartCast(CastKind kind)
        {
            _currentCast = kind;
            _isCasting = true;
            _released = false;
            _castStartedAt = Time.time;

            float duration = ResolveCastDuration(kind);
            float releaseDelay = ResolveReleaseDelay(kind);
            _castEndsAt = Time.time + duration;
            _releaseAt = Time.time + Mathf.Min(releaseDelay, duration);

            ScheduleCooldown(kind);
            ConsumePendingIceSweepIfNeeded(kind);

            SetWanderEnabled(false);
            FaceTarget();
            PlayAnimation(ResolveCastStateName(kind));
            Log(kind + " cast started.");
        }

        private void ConsumePendingIceSweepIfNeeded(CastKind kind)
        {
            if (kind != CastKind.IceSweep)
            {
                return;
            }

            _activeIceSweepHealthThresholdIndex = _pendingIceSweepHealthThresholdIndex;
            _pendingIceSweepHealthThresholdIndex = -1;
        }

        private float ResolveCastDuration(CastKind kind)
        {
            return kind switch
            {
                CastKind.Inferno => infernoCastDuration,
                CastKind.IceSweep => iceSweepCastDuration,
                CastKind.ThunderStrike => thunderStrikeCastDuration,
                CastKind.Thunderbolt => ResolveThunderboltCastDuration(),
                _ => fireballCastDuration
            };
        }

        private float ResolveReleaseDelay(CastKind kind)
        {
            return kind switch
            {
                CastKind.Inferno => infernoReleaseTime,
                CastKind.IceSweep => iceSweepReleaseTime,
                CastKind.ThunderStrike => thunderStrikeReleaseTime,
                CastKind.Thunderbolt => thunderboltReleaseTime,
                _ => fireballReleaseTime
            };
        }

        private string ResolveCastStateName(CastKind kind)
        {
            return kind switch
            {
                CastKind.Inferno => ResolveInfernoCastStateName(),
                CastKind.IceSweep => heavyCastStateName,
                CastKind.ThunderStrike => thunderStrikeStateName,
                CastKind.Thunderbolt => thunderboltStateName,
                _ => fastCastStateName
            };
        }

        private string ResolveInfernoCastStateName()
        {
            if (_isPhaseTwo
                && !string.IsNullOrWhiteSpace(phaseTwoInfernoStateName)
                && AnimatorHasState(phaseTwoInfernoStateName))
            {
                return phaseTwoInfernoStateName;
            }

            return heavyCastStateName;
        }

        private void ScheduleCooldown(CastKind kind)
        {
            switch (kind)
            {
                case CastKind.Inferno:
                    _nextInfernoAllowedAt = Time.time + infernoCooldown;
                    _nextFireballAllowedAt = Mathf.Max(_nextFireballAllowedAt, Time.time + 0.45f);
                    break;
                case CastKind.IceSweep:
                    _nextIceSweepAllowedAt = Time.time + iceSweepCooldown;
                    _nextInfernoAllowedAt = Mathf.Max(_nextInfernoAllowedAt, Time.time + 0.9f);
                    _nextFireballAllowedAt = Mathf.Max(_nextFireballAllowedAt, Time.time + 0.9f);
                    break;
                case CastKind.ThunderStrike:
                    _nextThunderStrikeAllowedAt = Time.time + thunderStrikeCooldown;
                    _nextIceSweepAllowedAt = Mathf.Max(_nextIceSweepAllowedAt, Time.time + 0.75f);
                    _nextInfernoAllowedAt = Mathf.Max(_nextInfernoAllowedAt, Time.time + 0.55f);
                    _nextFireballAllowedAt = Mathf.Max(_nextFireballAllowedAt, Time.time + 0.55f);
                    break;
                case CastKind.Thunderbolt:
                    _nextThunderboltAllowedAt = Time.time + thunderboltCooldown;
                    _nextThunderStrikeAllowedAt = Mathf.Max(_nextThunderStrikeAllowedAt, Time.time + 1f);
                    _nextIceSweepAllowedAt = Mathf.Max(_nextIceSweepAllowedAt, Time.time + 1f);
                    _nextInfernoAllowedAt = Mathf.Max(_nextInfernoAllowedAt, Time.time + 0.75f);
                    _nextFireballAllowedAt = Mathf.Max(_nextFireballAllowedAt, Time.time + 0.75f);
                    break;
                default:
                    _nextFireballAllowedAt = Time.time + fireballCooldown;
                    break;
            }
        }

        private void TickCast()
        {
            FaceTarget();

            if (!_released && Time.time >= _releaseAt)
            {
                _released = true;
                switch (_currentCast)
                {
                    case CastKind.Inferno:
                        SpawnInferno();
                        break;
                    case CastKind.IceSweep:
                        StartIceSweepSequence();
                        break;
                    case CastKind.ThunderStrike:
                        StartThunderStrikeSequence();
                        break;
                    case CastKind.Thunderbolt:
                        SpawnThunderbolt();
                        break;
                    default:
                        SpawnFireball();
                        break;
                }
            }

            if (Time.time >= _castEndsAt)
            {
                EndCast();
            }
        }

        private void EndCast()
        {
            _isCasting = false;
            _released = false;
            PlayAnimation(idleStateName);
            ApplyAttackRecovery();
            if (_currentCast != CastKind.IceSweep || _iceSweepSequence == null)
            {
                SetWanderEnabled(true);
            }

            Log(_currentCast + " cast ended.");
        }

        private void SpawnFireball()
        {
            SpawnProjectileFan(
                "RenaFireball",
                ResolveAttackDirection(),
                fireballProjectileCount,
                fireballTotalSpreadAngle,
                fireballCastedFrames,
                fireballFrames,
                fireballHitFrames,
                fireballCastedGlowFrames,
                fireballGlowFrames,
                fireballHitGlowFrames,
                fireballSpeed,
                fireballLifetime,
                fireballDamage,
                fireballHitRadius,
                fireballMaximumHits,
                fireballDestroyOnHit,
                fireballVisualScale,
                true,
                true,
                fireballHomingEnabled ? _target : null,
                fireballHomingTurnRateDegrees,
                fireballHomingStartDelay,
                fireballHomingEndDistance,
                fireballHomingLaneSpacing,
                fireballHomingForwardSpacing);
        }

        private void SpawnInferno()
        {
            SpawnProjectileFan(
                "RenaInferno",
                ResolveAttackDirection(),
                ResolveInfernoProjectileCount(),
                ResolveInfernoTotalSpreadAngle(),
                infernoCastedFrames,
                infernoFrames,
                infernoHitFrames,
                infernoCastedGlowFrames,
                infernoGlowFrames,
                infernoHitGlowFrames,
                infernoSpeed,
                infernoLifetime,
                infernoDamage,
                infernoHitRadius,
                infernoMaximumHits,
                infernoDestroyOnHit,
                infernoVisualScale,
                false,
                false);
        }

        private int ResolveInfernoProjectileCount()
        {
            return _isPhaseTwo ? phaseTwoInfernoProjectileCount : infernoProjectileCount;
        }

        private float ResolveInfernoTotalSpreadAngle()
        {
            return _isPhaseTwo ? phaseTwoInfernoTotalSpreadAngle : infernoTotalSpreadAngle;
        }

        private void StartThunderStrikeSequence()
        {
            if (_thunderStrikeSequence != null)
            {
                StopCoroutine(_thunderStrikeSequence);
            }

            _thunderStrikeSequence = StartCoroutine(SpawnThunderStrikeSequenceRoutine());
        }

        private void StopThunderStrikeSequence()
        {
            if (_thunderStrikeSequence == null)
            {
                return;
            }

            StopCoroutine(_thunderStrikeSequence);
            _thunderStrikeSequence = null;
        }

        private void StartIceSweepSequence()
        {
            if (!HasFrames(iceSweepFrames))
            {
                _activeIceSweepHealthThresholdIndex = -1;
                return;
            }

            StopIceSweepSequence(resetActiveThreshold: false);
            SetWanderEnabled(false);
            BeginIceSweepProtection();
            StartIceSweepShield();
            _iceSweepSequence = StartCoroutine(SpawnIceSweepSequenceRoutine());
        }

        private IEnumerator SpawnIceSweepSequenceRoutine()
        {
            Vector2 areaCenter = ResolveIceSweepAreaCenter();
            Vector2 areaSize = new Vector2(
                Mathf.Max(0.1f, iceSweepAreaSize.x),
                Mathf.Max(0.1f, iceSweepAreaSize.y));
            int attackCount = ResolveIceSweepAttackCount();
            int columnCount = Mathf.Max(1, iceSweepColumnCount);
            int rowCount = Mathf.Max(2, iceSweepRowCount);
            float cellWidth = areaSize.x / columnCount;
            float cellHeight = areaSize.y / rowCount;
            float rowGap = Mathf.Min(Mathf.Max(0f, iceSweepRowGap), cellHeight * 0.5f);
            Vector2 cellSize = new Vector2(
                cellWidth + iceSweepCellOverlap,
                Mathf.Max(0.01f, cellHeight - rowGap));
            Vector2 damageRowSize = new Vector2(
                Mathf.Max(0.01f, (columnCount - 1) * cellWidth + cellSize.x),
                cellSize.y);
            float left = areaCenter.x - areaSize.x * 0.5f + cellWidth * 0.5f;
            float bottom = areaCenter.y - areaSize.y * 0.5f + cellHeight * 0.5f;
            float warningDuration = Mathf.Max(0f, iceSweepWarningDuration);
            float pillarSpawnInterval = Mathf.Max(0f, iceSweepPillarSpawnInterval);
            float waveInterval = Mathf.Max(0f, iceSweepWaveInterval);
            bool spawnFinalThresholdThunderStrikes = ShouldSpawnIceSweepFinalThresholdThunderStrikes();
            int previousSafeRow = -1;

            for (int attackIndex = 0; attackIndex < attackCount; attackIndex++)
            {
                if (IsDead() || _isPhaseTransitioning)
                {
                    break;
                }

                int safeRow = ResolveIceSweepSafeRow(rowCount, previousSafeRow);
                previousSafeRow = safeRow;
                bool sweepLeftToRight = UnityEngine.Random.value < 0.5f;
                yield return SpawnIceSweepWaveRoutine(
                    areaCenter,
                    areaSize,
                    damageRowSize,
                    left,
                    bottom,
                    cellWidth,
                    cellHeight,
                    cellSize,
                    columnCount,
                    rowCount,
                    safeRow,
                    sweepLeftToRight,
                    spawnFinalThresholdThunderStrikes,
                    warningDuration,
                    pillarSpawnInterval);

                if (IsIceSweepSequenceInterrupted())
                {
                    break;
                }

                if (waveInterval > 0f)
                {
                    yield return new WaitForSeconds(waveInterval);
                }
                else if (attackIndex < attackCount - 1)
                {
                    yield return null;
                }
            }

            _activeIceSweepHealthThresholdIndex = -1;
            if (!IsIceSweepSequenceInterrupted())
            {
                yield return CompleteIceSweepShieldRoutine();
            }
            else
            {
                StopIceSweepShield();
            }

            RestoreIceSweepProtection();
            ApplyAttackRecovery();
            if (!IsDead() && !_isCasting && !_isPhaseTransitioning)
            {
                SetWanderEnabled(true);
            }

            _iceSweepSequence = null;
        }

        private void HandleBalanceDataChanged(RenaBossBalanceData changedData)
        {
            if (changedData == balanceData)
            {
                ApplyBalanceDataIfAvailable(resetCurrentHealth: false);
            }
        }

        private void ApplyBalanceDataIfAvailable(bool resetCurrentHealth)
        {
            if (balanceData == null)
            {
                return;
            }

            phaseTwoHealthThreshold = Mathf.Clamp(balanceData.Phase2ThresholdNormalized, 0.01f, 1f);
            fireballDamage = balanceData.FireballDamage;
            infernoDamage = balanceData.InfernoDamage;
            iceSweepDamage = balanceData.IceSweepDamage;
            thunderStrikeDamage = balanceData.ThunderStrikeDamage;
            thunderboltDamage = balanceData.ThunderboltDamage;

            if (health == null)
            {
                return;
            }

            float maxHealth = Mathf.Max(0f, balanceData.MaxHealth);
            health.InitialHealth = maxHealth;
            health.MaximumHealth = maxHealth;
            if (!Application.isPlaying)
            {
                return;
            }

            if (resetCurrentHealth)
            {
                health.SetHealth(maxHealth);
            }
            else if (health.CurrentHealth > maxHealth)
            {
                health.SetHealth(maxHealth);
            }
        }

        private IEnumerator SpawnIceSweepWaveRoutine(
            Vector2 areaCenter,
            Vector2 areaSize,
            Vector2 damageRowSize,
            float left,
            float bottom,
            float cellWidth,
            float cellHeight,
            Vector2 cellSize,
            int columnCount,
            int rowCount,
            int safeRow,
            bool sweepLeftToRight,
            bool spawnWarningThunderStrikes,
            float warningDuration,
            float pillarSpawnInterval)
        {
            List<SpriteRenderer> warnings = SpawnIceSweepRowWarnings(areaCenter, bottom, cellHeight, damageRowSize, rowCount, safeRow);
            Coroutine thunderStrikeWarningRoutine = null;
            if (spawnWarningThunderStrikes && warningDuration > 0f)
            {
                thunderStrikeWarningRoutine = StartCoroutine(
                    SpawnIceSweepWarningThunderStrikeRoutine(areaCenter, areaSize, warningDuration));
            }

            yield return RunIceSweepRowWarnings(warnings, warningDuration);
            if (thunderStrikeWarningRoutine != null)
            {
                StopCoroutine(thunderStrikeWarningRoutine);
            }

            DestroyIceSweepRowWarnings(warnings);

            if (IsIceSweepSequenceInterrupted())
            {
                yield break;
            }

            for (int step = 0; step < columnCount; step++)
            {
                int column = sweepLeftToRight ? step : columnCount - 1 - step;
                SpawnIceSweepColumn(left, bottom, cellWidth, cellHeight, cellSize, column, rowCount, safeRow);

                if (step < columnCount - 1)
                {
                    if (pillarSpawnInterval > 0f)
                    {
                        yield return new WaitForSeconds(pillarSpawnInterval);
                    }
                    else
                    {
                        yield return null;
                    }
                }

                if (IsIceSweepSequenceInterrupted())
                {
                    yield break;
                }
            }
        }

        private void SpawnIceSweepColumn(
            float left,
            float bottom,
            float cellWidth,
            float cellHeight,
            Vector2 cellSize,
            int column,
            int rowCount,
            int safeRow)
        {
            for (int row = 0; row < rowCount; row++)
            {
                if (row == safeRow)
                {
                    continue;
                }

                float x = left + column * cellWidth;
                float y = bottom + row * cellHeight;
                Vector2 cellCenter = new Vector2(x, y);
                if (!IsIceSweepCellBlocked(cellCenter, cellSize))
                {
                    SpawnIcePillarCell(cellCenter, cellSize);
                }
            }
        }

        private List<SpriteRenderer> SpawnIceSweepRowWarnings(
            Vector2 areaCenter,
            float bottom,
            float cellHeight,
            Vector2 damageRowSize,
            int rowCount,
            int safeRow)
        {
            DestroyIceSweepRowWarnings(_iceSweepRowWarnings);
            List<SpriteRenderer> warnings = _iceSweepRowWarnings;
            Vector2 warningSize = new Vector2(
                Mathf.Max(0.01f, damageRowSize.x),
                Mathf.Max(0.01f, damageRowSize.y));
            string sortingLayer = string.IsNullOrWhiteSpace(iceSweepSortingLayerName)
                ? projectileSortingLayerName
                : iceSweepSortingLayerName;

            for (int row = 0; row < rowCount; row++)
            {
                if (row == safeRow)
                {
                    continue;
                }

                float y = bottom + row * cellHeight;
                GameObject warningObject = new GameObject("RenaIceSweepRowWarning");
                warningObject.transform.position = new Vector3(areaCenter.x, y, transform.position.z);
                warningObject.transform.localScale = new Vector3(warningSize.x, warningSize.y, 1f);

                SpriteRenderer renderer = warningObject.AddComponent<SpriteRenderer>();
                RuntimeSpriteMaterialUtility.ApplySpriteMaterial(renderer);
                renderer.sprite = GetOrCreateIceSweepWarningSprite();
                renderer.sortingLayerName = sortingLayer;
                renderer.sortingOrder = iceSweepSortingOrder - 2;
                warnings.Add(renderer);
            }

            UpdateIceSweepRowWarnings(warnings, 0f);
            return warnings;
        }

        private IEnumerator RunIceSweepRowWarnings(List<SpriteRenderer> warnings, float warningDuration)
        {
            if (warnings.Count == 0 || warningDuration <= 0f)
            {
                yield break;
            }

            float startedAt = Time.time;
            while (Time.time - startedAt < warningDuration)
            {
                if (IsIceSweepSequenceInterrupted())
                {
                    yield break;
                }

                float progress = Mathf.Clamp01((Time.time - startedAt) / warningDuration);
                UpdateIceSweepRowWarnings(warnings, progress);
                yield return null;
            }

            UpdateIceSweepRowWarnings(warnings, 1f);
        }

        private static void UpdateIceSweepRowWarnings(List<SpriteRenderer> warnings, float progress)
        {
            float pulse = (Mathf.Sin(Time.time * 18f) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(0.18f, 0.62f, progress) * Mathf.Lerp(0.72f, 1f, pulse);
            Color color = new Color(0.25f, 0.85f, 1f, alpha);

            for (int i = 0; i < warnings.Count; i++)
            {
                SpriteRenderer renderer = warnings[i];
                if (renderer != null)
                {
                    renderer.color = color;
                }
            }
        }

        private static void DestroyIceSweepRowWarnings(List<SpriteRenderer> warnings)
        {
            for (int i = 0; i < warnings.Count; i++)
            {
                SpriteRenderer renderer = warnings[i];
                if (renderer != null)
                {
                    UnityEngine.Object.Destroy(renderer.gameObject);
                }
            }

            warnings.Clear();
        }

        private static int ResolveIceSweepSafeRow(int rowCount, int previousSafeRow)
        {
            if (rowCount <= 1)
            {
                return -1;
            }

            int safeRow = UnityEngine.Random.Range(0, rowCount);
            if (safeRow == previousSafeRow)
            {
                safeRow = (safeRow + UnityEngine.Random.Range(1, rowCount)) % rowCount;
            }

            return safeRow;
        }

        private Vector2 ResolveIceSweepAreaCenter()
        {
            return iceSweepCenterFollowsBoss
                ? (Vector2)transform.position + iceSweepAreaCenter
                : iceSweepAreaCenter;
        }

        private void SpawnIcePillarCell(Vector2 center, Vector2 size)
        {
            GameObject areaObject = new GameObject("RenaIcePillarArea");
            RenaBossIcePillarArea area = areaObject.AddComponent<RenaBossIcePillarArea>();
            area.Configure(
                gameObject,
                health,
                targetLayerMask,
                center,
                size,
                iceSweepDamage,
                iceSweepCellDamageDelay,
                targetInvincibilityDuration,
                ResolveIceSweepPillarVisualOffset(size),
                iceSweepPillarVisualRows,
                iceSweepVisualScale,
                iceSweepFrames,
                iceSweepAnimationFrameRate,
                string.IsNullOrWhiteSpace(iceSweepSortingLayerName) ? projectileSortingLayerName : iceSweepSortingLayerName,
                iceSweepSortingOrder,
                debugLogging);
        }

        private Vector2 ResolveIceSweepPillarVisualOffset(Vector2 damageSize)
        {
            float maxX = Mathf.Min(Mathf.Max(0f, iceSweepPillarVisualJitter.x), damageSize.x * 0.35f);
            float maxY = Mathf.Min(Mathf.Max(0f, iceSweepPillarVisualJitter.y), damageSize.y * 0.35f);
            return new Vector2(
                UnityEngine.Random.Range(-maxX, maxX),
                UnityEngine.Random.Range(-maxY, maxY));
        }

        private bool IsIceSweepCellBlocked(Vector2 center, Vector2 size)
        {
            return iceSweepSkipBlockedCells
                   && obstacleLayerMask.value != 0
                   && Physics2D.OverlapBox(center, size * 0.9f, 0f, obstacleLayerMask) != null;
        }

        private int ResolveIceSweepAttackCount()
        {
            if (iceSweepUseHealthThresholds && _activeIceSweepHealthThresholdIndex >= 0)
            {
                int thresholdCount = _activeIceSweepHealthThresholdIndex switch
                {
                    0 => iceSweepFirstThresholdAttackCount,
                    1 => iceSweepSecondThresholdAttackCount,
                    2 => iceSweepThirdThresholdAttackCount,
                    _ => iceSweepAttackCount
                };

                return Mathf.Max(1, thresholdCount);
            }

            return Mathf.Max(1, _isPhaseTwo ? phaseTwoIceSweepAttackCount : iceSweepAttackCount);
        }

        private bool ShouldSpawnIceSweepFinalThresholdThunderStrikes()
        {
            return iceSweepFinalThresholdThunderStrikeEnabled
                   && iceSweepUseHealthThresholds
                   && _activeIceSweepHealthThresholdIndex == IceSweepHealthThresholdCount - 1;
        }

        private bool IsIceSweepSequenceInterrupted()
        {
            return IsDead() || _isPhaseTransitioning;
        }

        private static Sprite GetOrCreateIceSweepWarningSprite()
        {
            if (_iceSweepWarningSprite != null)
            {
                return _iceSweepWarningSprite;
            }

            Texture2D texture = new Texture2D(IceSweepWarningTextureSize, IceSweepWarningTextureSize, TextureFormat.RGBA32, false)
            {
                name = "RenaIceSweepRowWarningTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[IceSweepWarningTextureSize * IceSweepWarningTextureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            _iceSweepWarningSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, IceSweepWarningTextureSize, IceSweepWarningTextureSize),
                new Vector2(0.5f, 0.5f),
                IceSweepWarningTextureSize);
            _iceSweepWarningSprite.name = "RenaIceSweepRowWarningSprite";
            return _iceSweepWarningSprite;
        }

        private void BeginIceSweepProtection()
        {
            if (health == null || _iceSweepChangedInvulnerability)
            {
                return;
            }

            _iceSweepPreviousInvulnerable = health.Invulnerable;
            health.Invulnerable = true;
            _iceSweepChangedInvulnerability = true;
        }

        private void RestoreIceSweepProtection()
        {
            if (!_iceSweepChangedInvulnerability || health == null)
            {
                _iceSweepChangedInvulnerability = false;
                return;
            }

            health.Invulnerable = _iceSweepPreviousInvulnerable;
            _iceSweepChangedInvulnerability = false;
        }

        private void StartIceSweepShield()
        {
            if (!HasFrames(iceSweepShieldFrames))
            {
                return;
            }

            EnsureIceSweepShieldRenderer();
            if (_iceSweepShieldRenderer == null)
            {
                return;
            }

            if (_iceSweepShieldRoutine != null)
            {
                StopCoroutine(_iceSweepShieldRoutine);
            }

            _iceSweepShieldBreaking = false;
            _iceSweepShieldRenderer.gameObject.SetActive(true);
            _iceSweepShieldRenderer.enabled = true;
            _iceSweepShieldRoutine = StartCoroutine(PlayIceSweepShieldRoutine());
        }

        private IEnumerator CompleteIceSweepShieldRoutine()
        {
            if (_iceSweepShieldRoutine == null)
            {
                HideIceSweepShield();
                yield break;
            }

            Coroutine shieldRoutine = _iceSweepShieldRoutine;
            _iceSweepShieldBreaking = true;
            yield return shieldRoutine;
        }

        private IEnumerator PlayIceSweepShieldRoutine()
        {
            Sprite[] frames = iceSweepShieldFrames;
            int frameCount = frames != null ? frames.Length : 0;
            if (frameCount == 0)
            {
                HideIceSweepShield();
                _iceSweepShieldRoutine = null;
                yield break;
            }

            int loopStart = Mathf.Clamp(iceSweepShieldLoopStartFrame - 1, 0, frameCount - 1);
            int loopEnd = Mathf.Clamp(iceSweepShieldLoopEndFrame - 1, loopStart, frameCount - 1);

            for (int i = 0; i < loopStart && !_iceSweepShieldBreaking; i++)
            {
                ApplyIceSweepShieldFrame(frames, i);
                yield return CreateIceSweepShieldFrameDelay();
            }

            int currentFrame = loopStart;
            while (!_iceSweepShieldBreaking)
            {
                ApplyIceSweepShieldFrame(frames, currentFrame);
                yield return CreateIceSweepShieldFrameDelay();
                currentFrame = currentFrame >= loopEnd ? loopStart : currentFrame + 1;
            }

            for (int i = loopEnd + 1; i < frameCount; i++)
            {
                ApplyIceSweepShieldFrame(frames, i);
                yield return CreateIceSweepShieldFrameDelay();
            }

            HideIceSweepShield();
            _iceSweepShieldRoutine = null;
            _iceSweepShieldBreaking = false;
        }

        private WaitForSeconds CreateIceSweepShieldFrameDelay()
        {
            return new WaitForSeconds(1f / Mathf.Max(0.01f, iceSweepShieldFrameRate));
        }

        private void ApplyIceSweepShieldFrame(Sprite[] frames, int frameIndex)
        {
            if (_iceSweepShieldRenderer == null
                || frames == null
                || frameIndex < 0
                || frameIndex >= frames.Length)
            {
                return;
            }

            Sprite frame = frames[frameIndex];
            if (frame != null)
            {
                _iceSweepShieldRenderer.sprite = frame;
            }
        }

        private void EnsureIceSweepShieldRenderer()
        {
            if (_iceSweepShieldRenderer != null)
            {
                ConfigureIceSweepShieldRenderer(_iceSweepShieldRenderer);
                return;
            }

            Transform parent = iceSweepShieldRoot != null
                ? iceSweepShieldRoot
                : spriteRenderer != null ? spriteRenderer.transform : transform;
            if (parent == null)
            {
                return;
            }

            GameObject shieldObject = new GameObject("RenaIceSweepMagicShield");
            shieldObject.transform.SetParent(parent, false);
            shieldObject.transform.localPosition = Vector3.zero;
            shieldObject.transform.localRotation = Quaternion.identity;
            shieldObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, iceSweepShieldVisualScale);

            _iceSweepShieldRenderer = shieldObject.AddComponent<SpriteRenderer>();
            RuntimeSpriteMaterialUtility.ApplySpriteMaterial(_iceSweepShieldRenderer);
            ConfigureIceSweepShieldRenderer(_iceSweepShieldRenderer);
            HideIceSweepShield();
        }

        private void ConfigureIceSweepShieldRenderer(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sortingLayerName = string.IsNullOrWhiteSpace(iceSweepShieldSortingLayerName)
                ? projectileSortingLayerName
                : iceSweepShieldSortingLayerName;
            renderer.sortingOrder = iceSweepShieldSortingOrder;
        }

        private void StopIceSweepShield()
        {
            if (_iceSweepShieldRoutine != null)
            {
                StopCoroutine(_iceSweepShieldRoutine);
                _iceSweepShieldRoutine = null;
            }

            _iceSweepShieldBreaking = false;
            HideIceSweepShield();
        }

        private void HideIceSweepShield()
        {
            if (_iceSweepShieldRenderer == null)
            {
                return;
            }

            _iceSweepShieldRenderer.enabled = false;
            _iceSweepShieldRenderer.gameObject.SetActive(false);
        }

        private void StopIceSweepSequence(bool resetActiveThreshold = true)
        {
            if (_iceSweepSequence != null)
            {
                StopCoroutine(_iceSweepSequence);
                _iceSweepSequence = null;
            }

            DestroyIceSweepRowWarnings(_iceSweepRowWarnings);
            if (resetActiveThreshold)
            {
                _activeIceSweepHealthThresholdIndex = -1;
            }

            StopIceSweepShield();
            RestoreIceSweepProtection();
        }

        private void ResetIceSweepHealthThresholdState()
        {
            _pendingIceSweepHealthThresholdIndex = -1;
            _activeIceSweepHealthThresholdIndex = -1;
            _nextIceSweepHealthThresholdIndex = 0;

            if (!iceSweepUseHealthThresholds
                || !TryResolveObservedHealthNormalized(out float normalizedHealth))
            {
                return;
            }

            while (_nextIceSweepHealthThresholdIndex < IceSweepHealthThresholdCount
                   && normalizedHealth <= ResolveIceSweepHealthThreshold(_nextIceSweepHealthThresholdIndex))
            {
                _nextIceSweepHealthThresholdIndex++;
            }
        }

        private void UpdateIceSweepHealthThresholdState()
        {
            if (!iceSweepUseHealthThresholds
                || _nextIceSweepHealthThresholdIndex >= IceSweepHealthThresholdCount
                || !TryResolveObservedHealthNormalized(out float normalizedHealth))
            {
                return;
            }

            int reachedIndex = -1;
            for (int i = _nextIceSweepHealthThresholdIndex; i < IceSweepHealthThresholdCount; i++)
            {
                if (normalizedHealth > ResolveIceSweepHealthThreshold(i))
                {
                    break;
                }

                reachedIndex = i;
            }

            if (reachedIndex < 0)
            {
                return;
            }

            _pendingIceSweepHealthThresholdIndex = reachedIndex;
            _nextIceSweepHealthThresholdIndex = reachedIndex + 1;
            Log("Ice sweep health threshold queued at "
                + Mathf.RoundToInt(ResolveIceSweepHealthThreshold(reachedIndex) * 100f)
                + "%.");
        }

        private float ResolveIceSweepHealthThreshold(int index)
        {
            float threshold = index switch
            {
                0 => iceSweepFirstHealthThreshold,
                1 => iceSweepSecondHealthThreshold,
                2 => iceSweepThirdHealthThreshold,
                _ => 0f
            };

            return Mathf.Clamp(threshold, 0.01f, 1f);
        }

        private bool TryStartPhaseTwoTransition()
        {
            if (_isPhaseTwo || _isPhaseTransitioning)
            {
                return _isPhaseTransitioning;
            }

            if (!IsHealthAtOrBelowPhaseTwoThreshold())
            {
                return false;
            }

            _phaseTransitionRoutine = StartCoroutine(PhaseTwoTransitionRoutine());
            return true;
        }

        private IEnumerator PhaseTwoTransitionRoutine()
        {
            _isPhaseTransitioning = true;
            _isCasting = false;
            _released = false;
            StopThunderStrikeSequence();
            StopIceSweepSequence();
            SetWanderEnabled(false);
            BeginPhaseTransitionProtection();
            FaceTarget();
            Log("Phase 2 transition started.");

            PlayPhaseTwoIntroAnimationState();
            if (phaseTwoAnimatorIntroDuration > 0f)
            {
                yield return new WaitForSeconds(phaseTwoAnimatorIntroDuration);
            }
            else
            {
                yield return null;
            }

            _isPhaseTwo = true;
            _isPhaseTransitioning = false;
            _phaseTransitionRoutine = null;
            RestorePhaseTransitionProtection();
            SchedulePhaseTwoOpening();
            PlayAnimation(idleStateName);
            SetWanderEnabled(true);
            Log("Phase 2 transition completed.");
        }

        private bool IsHealthAtOrBelowPhaseTwoThreshold()
        {
            return TryResolveObservedHealthNormalized(out float normalizedHealth)
                   && normalizedHealth <= phaseTwoHealthThreshold;
        }

        private Health ResolveObservedHealth()
        {
            if (health == null)
            {
                return null;
            }

            return health.MasterHealth != null ? health.MasterHealth : health;
        }

        private bool TryResolveObservedHealthNormalized(out float normalizedHealth)
        {
            normalizedHealth = 1f;
            Health observedHealth = ResolveObservedHealth();
            if (observedHealth == null)
            {
                return false;
            }

            float maximumHealth = observedHealth.MaximumHealth > 0f
                ? observedHealth.MaximumHealth
                : observedHealth.InitialHealth;
            if (maximumHealth <= 0f)
            {
                return false;
            }

            normalizedHealth = Mathf.Clamp01(observedHealth.CurrentHealth / maximumHealth);
            return true;
        }

        private void BeginPhaseTransitionProtection()
        {
            if (!makeInvulnerableDuringPhaseTransition || health == null || _phaseTransitionChangedInvulnerability)
            {
                return;
            }

            _phaseTransitionPreviousInvulnerable = health.Invulnerable;
            health.Invulnerable = true;
            _phaseTransitionChangedInvulnerability = true;
        }

        private void RestorePhaseTransitionProtection()
        {
            if (!_phaseTransitionChangedInvulnerability || health == null)
            {
                _phaseTransitionChangedInvulnerability = false;
                return;
            }

            health.Invulnerable = _phaseTransitionPreviousInvulnerable;
            _phaseTransitionChangedInvulnerability = false;
        }

        private void SchedulePhaseTwoOpening()
        {
            float now = Time.time;
            float openingInfernoDelay = Mathf.Max(0f, phaseTwoOpeningInfernoDelay);
            float otherSpellDelay = openingInfernoDelay + Mathf.Max(0.75f, ResolveAttackRecoveryDuration());
            _nextAnyCastAllowedAt = Mathf.Max(_nextAnyCastAllowedAt, now + openingInfernoDelay);
            _nextInfernoAllowedAt = now + openingInfernoDelay;
            _nextFireballAllowedAt = Mathf.Max(_nextFireballAllowedAt, now + otherSpellDelay);
            _nextIceSweepAllowedAt = now + otherSpellDelay;
            _nextThunderStrikeAllowedAt = Mathf.Max(_nextThunderStrikeAllowedAt, now + otherSpellDelay);
            _nextThunderboltAllowedAt = Mathf.Max(_nextThunderboltAllowedAt, now + otherSpellDelay);
        }

        private void PlayPhaseTwoIntroAnimationState()
        {
            if (TryPlayAnimationStateIfPresent(phaseTwoIntroStateName))
            {
                return;
            }

            if (TryPlayAnimationStateIfPresent(phaseTwoFallbackIntroStateName))
            {
                return;
            }

            PlayAnimation(phaseTwoIntroStateName);
        }

        private bool TryPlayAnimationStateIfPresent(string stateName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            if (!AnimatorHasState(stateName))
            {
                return false;
            }

            animator.Play(stateName, animationLayer, 0f);
            return true;
        }

        private bool AnimatorHasState(string stateName)
        {
            return animator != null
                   && (animator.HasState(animationLayer, Animator.StringToHash(stateName))
                       || animator.HasState(animationLayer, Animator.StringToHash("Base Layer." + stateName)));
        }

        private void ApplyAttackRecovery()
        {
            float recoveryDuration = ResolveAttackRecoveryDuration();
            if (recoveryDuration <= 0f)
            {
                return;
            }

            _nextAnyCastAllowedAt = Mathf.Max(_nextAnyCastAllowedAt, Time.time + recoveryDuration);
        }

        private float ResolveAttackRecoveryDuration()
        {
            return _isPhaseTwo && phaseTwoAttackRecoveryDuration > 0f
                ? phaseTwoAttackRecoveryDuration
                : attackRecoveryDuration;
        }

        private static bool HasFrames(Sprite[] frames)
        {
            if (frames == null)
            {
                return false;
            }

            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator SpawnThunderStrikeSequenceRoutine()
        {
            int areaCount = Mathf.Max(1, thunderStrikeAreaCount);
            for (int i = 0; i < areaCount; i++)
            {
                SpawnThunderStrikeArea(ResolveThunderStrikePosition());

                if (i < areaCount - 1)
                {
                    if (thunderStrikeSpawnInterval > 0f)
                    {
                        yield return new WaitForSeconds(thunderStrikeSpawnInterval);
                    }
                    else
                    {
                        yield return null;
                    }
                }
            }

            _thunderStrikeSequence = null;
        }

        private IEnumerator SpawnIceSweepWarningThunderStrikeRoutine(
            Vector2 areaCenter,
            Vector2 areaSize,
            float warningDuration)
        {
            int strikeCount = Mathf.Max(1, iceSweepFinalThresholdThunderStrikeCount);
            float spawnInterval = strikeCount > 1
                ? Mathf.Max(0f, warningDuration) / strikeCount
                : 0f;
            float strikeWarningDuration = Mathf.Max(0f, iceSweepFinalThresholdThunderStrikeWarningDuration);
            float strikeStunDuration = Mathf.Max(0f, iceSweepFinalThresholdThunderStrikeStunDuration);

            for (int i = 0; i < strikeCount; i++)
            {
                if (IsIceSweepSequenceInterrupted())
                {
                    yield break;
                }

                Vector2 position = ResolveIceSweepThunderStrikePosition(areaCenter, areaSize);
                SpawnThunderStrikeArea(
                    position,
                    strikeWarningDuration,
                    applyHitStun: true,
                    hitStunDuration: strikeStunDuration);

                if (i < strikeCount - 1)
                {
                    if (spawnInterval > 0f)
                    {
                        yield return new WaitForSeconds(spawnInterval);
                    }
                    else
                    {
                        yield return null;
                    }
                }
            }
        }

        private void SpawnThunderStrikeArea(
            Vector2 position,
            float warningDurationOverride = -1f,
            bool applyHitStun = false,
            float hitStunDuration = 0f)
        {
            GameObject areaObject = new GameObject("RenaThunderStrikeArea");
            RenaBossThunderStrikeArea area = areaObject.AddComponent<RenaBossThunderStrikeArea>();
            float resolvedWarningDuration = warningDurationOverride >= 0f
                ? warningDurationOverride
                : thunderStrikeWarningDuration;
            bool phaseHitStun = _isPhaseTwo && phaseTwoThunderStrikeHitStun;
            bool resolvedApplyHitStun = applyHitStun || phaseHitStun;
            float resolvedHitStunDuration = Mathf.Max(
                applyHitStun ? hitStunDuration : 0f,
                phaseHitStun ? phaseTwoThunderStrikeHitStunDuration : 0f);
            area.Configure(
                gameObject,
                health,
                targetLayerMask,
                position,
                thunderStrikeRadius,
                thunderStrikeDamage,
                resolvedWarningDuration,
                targetInvincibilityDuration,
                thunderStrikeVisualScale,
                ResolveThunderStrikeFrames(),
                ResolveThunderStrikeGlowFrames(),
                thunderStrikeAnimationFrameRate,
                projectileSortingLayerName,
                projectileSortingOrder + 6,
                debugLogging,
                resolvedApplyHitStun,
                resolvedHitStunDuration);
        }

        private Sprite[] ResolveThunderStrikeFrames()
        {
            return _isPhaseTwo && HasFrames(phaseTwoThunderStrikeFrames)
                ? phaseTwoThunderStrikeFrames
                : thunderStrikeFrames;
        }

        private Sprite[] ResolveThunderStrikeGlowFrames()
        {
            return _isPhaseTwo && HasFrames(phaseTwoThunderStrikeGlowFrames)
                ? phaseTwoThunderStrikeGlowFrames
                : thunderStrikeGlowFrames;
        }

        private Vector2 ResolveThunderStrikePosition()
        {
            Vector2 targetPosition = ResolveThunderStrikeTargetPosition();
            Vector2 fallbackPosition = targetPosition;

            const int attempts = 12;
            for (int i = 0; i < attempts; i++)
            {
                Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * thunderStrikeSpawnRadiusAroundTarget;
                Vector2 candidate = targetPosition + randomOffset;
                if (!IsAreaBlocked(candidate, thunderStrikeRadius * 0.6f))
                {
                    return candidate;
                }
            }

            return IsAreaBlocked(fallbackPosition, thunderStrikeRadius * 0.6f)
                ? (Vector2)transform.position + ResolveAttackDirection() * 2f
                : fallbackPosition;
        }

        private Vector2 ResolveThunderStrikeTargetPosition()
        {
            if (IsTargetUsable())
            {
                return _target.position;
            }

            return (Vector2)transform.position + ResolveAttackDirection() * 3f;
        }

        private Vector2 ResolveIceSweepThunderStrikePosition(Vector2 areaCenter, Vector2 areaSize)
        {
            float halfWidth = Mathf.Max(0.1f, areaSize.x) * 0.5f;
            float halfHeight = Mathf.Max(0.1f, areaSize.y) * 0.5f;

            const int attempts = 16;
            for (int i = 0; i < attempts; i++)
            {
                Vector2 candidate = new Vector2(
                    UnityEngine.Random.Range(areaCenter.x - halfWidth, areaCenter.x + halfWidth),
                    UnityEngine.Random.Range(areaCenter.y - halfHeight, areaCenter.y + halfHeight));
                if (!IsAreaBlocked(candidate, thunderStrikeRadius * 0.6f))
                {
                    return candidate;
                }
            }

            return IsAreaBlocked(areaCenter, thunderStrikeRadius * 0.6f)
                ? (Vector2)transform.position
                : areaCenter;
        }

        private bool IsAreaBlocked(Vector2 center, float probeRadius)
        {
            return obstacleLayerMask.value != 0
                   && Physics2D.OverlapCircle(center, Mathf.Max(0.05f, probeRadius), obstacleLayerMask) != null;
        }

        private void SpawnThunderbolt()
        {
            Vector2 direction = ResolveAttackDirection();
            float rotationDegrees = ResolveThunderboltRotationDegrees();
            float duration = ResolveThunderboltDuration();

            SpawnThunderboltBeam("RenaThunderboltBeam", direction, duration, rotationDegrees, 7);
            if (_isPhaseTwo && phaseTwoThunderboltSpawnOppositeBeam)
            {
                SpawnThunderboltBeam("RenaThunderboltBeamOpposite", -direction, duration, rotationDegrees, 8);
            }
        }

        private void SpawnThunderboltBeam(
            string objectName,
            Vector2 direction,
            float duration,
            float rotationDegrees,
            int sortingOrderOffset)
        {
            Vector3 origin = projectileSpawnOrigin != null ? projectileSpawnOrigin.position : transform.position;
            GameObject beamObject = new GameObject(objectName);
            beamObject.transform.position = new Vector3(origin.x, origin.y, transform.position.z);

            RenaBossThunderboltBeam beam = beamObject.AddComponent<RenaBossThunderboltBeam>();
            beam.Configure(
                gameObject,
                health,
                projectileSpawnOrigin != null ? projectileSpawnOrigin : transform,
                targetLayerMask,
                direction,
                thunderboltDamage,
                thunderboltBuildDuration,
                duration,
                thunderboltLength,
                thunderboltWidth,
                rotationDegrees,
                targetInvincibilityDuration,
                thunderboltDamageInterval,
                thunderboltFrames,
                thunderboltGlowFrames,
                thunderboltAnimationFrameRate,
                projectileSortingLayerName,
                projectileSortingOrder + sortingOrderOffset,
                debugLogging);
        }

        private float ResolveThunderboltCastDuration()
        {
            return _isPhaseTwo && phaseTwoThunderboltCastDuration > 0f
                ? phaseTwoThunderboltCastDuration
                : thunderboltCastDuration;
        }

        private float ResolveThunderboltDuration()
        {
            return _isPhaseTwo && phaseTwoThunderboltDuration > 0f
                ? phaseTwoThunderboltDuration
                : thunderboltDuration;
        }

        private float ResolveThunderboltRotationDegrees()
        {
            return _isPhaseTwo ? phaseTwoThunderboltRotationDegrees : thunderboltRotationDegrees;
        }

        private void SpawnProjectileFan(
            string objectNamePrefix,
            Vector2 baseDirection,
            int projectileCount,
            float totalSpreadAngle,
            Sprite[] castedFrames,
            Sprite[] frames,
            Sprite[] hitFrames,
            Sprite[] castedGlowFrames,
            Sprite[] glowFrames,
            Sprite[] hitGlowFrames,
            float configuredSpeed,
            float configuredLifetime,
            float configuredDamage,
            float configuredHitRadius,
            int configuredMaximumHits,
            bool configuredDestroyOnHit,
            float configuredVisualScale,
            bool rotateVisualToDirection,
            bool moveDuringCastedAnimation,
            Transform homingTarget = null,
            float homingTurnRateDegrees = 0f,
            float homingStartDelay = 0f,
            float homingEndDistance = 0f,
            float homingLaneSpacing = 0f,
            float homingForwardSpacing = 0f)
        {
            int count = Mathf.Max(1, projectileCount);
            for (int i = 0; i < count; i++)
            {
                float angleOffset = ResolveFanAngleOffset(i, count, totalSpreadAngle);
                Vector2 projectileDirection = Rotate(baseDirection, angleOffset);
                Vector2 homingTargetOffset = ResolveFanHomingTargetOffset(
                    baseDirection,
                    i,
                    count,
                    homingLaneSpacing,
                    homingForwardSpacing);
                SpawnProjectile(
                    objectNamePrefix + "_" + (i + 1),
                    projectileDirection,
                    castedFrames,
                    frames,
                    hitFrames,
                    castedGlowFrames,
                    glowFrames,
                    hitGlowFrames,
                    configuredSpeed,
                    configuredLifetime,
                    configuredDamage,
                    configuredHitRadius,
                    configuredMaximumHits,
                    configuredDestroyOnHit,
                    configuredVisualScale,
                    rotateVisualToDirection,
                    moveDuringCastedAnimation,
                    homingTarget,
                    homingTargetOffset,
                    homingTurnRateDegrees,
                    homingStartDelay,
                    homingEndDistance);
            }
        }

        private void SpawnProjectile(
            string objectName,
            Vector2 direction,
            Sprite[] castedFrames,
            Sprite[] frames,
            Sprite[] hitFrames,
            Sprite[] castedGlowFrames,
            Sprite[] glowFrames,
            Sprite[] hitGlowFrames,
            float configuredSpeed,
            float configuredLifetime,
            float configuredDamage,
            float configuredHitRadius,
            int configuredMaximumHits,
            bool configuredDestroyOnHit,
            float configuredVisualScale,
            bool rotateVisualToDirection,
            bool moveDuringCastedAnimation,
            Transform homingTarget,
            Vector2 homingTargetOffset,
            float homingTurnRateDegrees,
            float homingStartDelay,
            float homingEndDistance)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 origin = projectileSpawnOrigin != null ? projectileSpawnOrigin.position : transform.position;
            Vector2 spawnPosition = origin
                                    + direction * projectileSpawnOffset.x
                                    + perpendicular * projectileSpawnOffset.y;

            GameObject projectileObject = new GameObject(objectName);
            projectileObject.transform.position = new Vector3(spawnPosition.x, spawnPosition.y, transform.position.z);

            SpriteRenderer projectileRenderer = projectileObject.AddComponent<SpriteRenderer>();
            RuntimeSpriteMaterialUtility.ApplySpriteMaterial(projectileRenderer);
            projectileRenderer.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            projectileRenderer.sortingLayerName = projectileSortingLayerName;
            projectileRenderer.sortingOrder = projectileSortingOrder;

            SpriteRenderer glowRenderer = CreateGlowRenderer(projectileObject.transform, castedGlowFrames, glowFrames, hitGlowFrames);

            RenaBossProjectile projectile = projectileObject.AddComponent<RenaBossProjectile>();
            projectile.Configure(
                gameObject,
                projectileRenderer,
                direction,
                targetLayerMask,
                obstacleLayerMask,
                configuredSpeed,
                configuredLifetime,
                configuredDamage,
                targetInvincibilityDuration,
                configuredHitRadius,
                configuredMaximumHits,
                configuredDestroyOnHit,
                rotateVisualToDirection,
                moveDuringCastedAnimation,
                castedFrames,
                frames,
                hitFrames,
                projectileAnimationFrameRate,
                configuredVisualScale,
                glowRenderer,
                castedGlowFrames,
                glowFrames,
                hitGlowFrames,
                projectileGlowScaleMultiplier,
                debugLogging);

            if (homingTarget != null && homingTurnRateDegrees > 0f)
            {
                projectile.ConfigureHoming(
                    homingTarget,
                    homingTargetOffset,
                    homingTurnRateDegrees,
                    homingStartDelay,
                    homingEndDistance);
            }

            Log("Spawned " + objectName + ".");
        }

        private SpriteRenderer CreateGlowRenderer(
            Transform parent,
            Sprite[] castedGlowFrames,
            Sprite[] glowFrames,
            Sprite[] hitGlowFrames)
        {
            Sprite initialGlowFrame = ResolveInitialGlowFrame(castedGlowFrames, glowFrames, hitGlowFrames);
            if (parent == null || initialGlowFrame == null)
            {
                return null;
            }

            GameObject glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(parent, false);

            SpriteRenderer glowRenderer = glowObject.AddComponent<SpriteRenderer>();
            RuntimeSpriteMaterialUtility.ApplyAdditiveSpriteMaterial(glowRenderer);
            glowRenderer.sprite = initialGlowFrame;
            glowRenderer.sortingLayerName = projectileSortingLayerName;
            glowRenderer.sortingOrder = projectileSortingOrder - 1;
            glowRenderer.color = new Color(1f, 1f, 1f, projectileGlowAlpha);
            return glowRenderer;
        }

        private static Sprite ResolveInitialGlowFrame(params Sprite[][] frameSets)
        {
            for (int i = 0; i < frameSets.Length; i++)
            {
                Sprite[] frames = frameSets[i];
                if (frames != null && frames.Length > 0)
                {
                    return frames[0];
                }
            }

            return null;
        }

        private void RefreshTargetIfNeeded()
        {
            if (Time.time < _nextTargetRefreshTime && IsTargetUsable())
            {
                return;
            }

            _nextTargetRefreshTime = Time.time + targetRefreshInterval;
            FindClosestTarget();
        }

        private void FindClosestTarget()
        {
            _target = null;
            _targetHealth = null;

            if (targetLayerMask.value == 0 || detectionRadius <= 0f)
            {
                return;
            }

            Collider2D[] candidates = Physics2D.OverlapCircleAll(transform.position, detectionRadius, targetLayerMask);
            float bestSqrDistance = float.PositiveInfinity;
            Vector3 origin = transform.position;

            for (int i = 0; i < candidates.Length; i++)
            {
                Health candidateHealth = candidates[i] != null ? candidates[i].GetComponentInParent<Health>() : null;
                if (!CanTarget(candidateHealth))
                {
                    continue;
                }

                float sqrDistance = (candidateHealth.transform.position - origin).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    _targetHealth = candidateHealth;
                    _target = candidateHealth.transform;
                }
            }

            if (_target == null)
            {
                FindClosestCharacterTarget(origin, ref bestSqrDistance);
            }
        }

        private void FindClosestCharacterTarget(Vector3 origin, ref float bestSqrDistance)
        {
            Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null
                    || character.CharacterType != Character.CharacterTypes.Player
                    || !character.gameObject.activeInHierarchy
                    || !IsLayerInMask(character.gameObject.layer, targetLayerMask))
                {
                    continue;
                }

                Health candidateHealth = character.GetComponent<Health>();
                if (!CanTarget(candidateHealth))
                {
                    continue;
                }

                float sqrDistance = (candidateHealth.transform.position - origin).sqrMagnitude;
                if (sqrDistance > detectionRadius * detectionRadius || sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                _targetHealth = candidateHealth;
                _target = candidateHealth.transform;
            }
        }

        private bool CanTarget(Health candidateHealth)
        {
            return candidateHealth != null
                   && candidateHealth.gameObject.activeInHierarchy
                   && candidateHealth.CurrentHealth > 0f
                   && candidateHealth.gameObject != gameObject
                   && !candidateHealth.transform.IsChildOf(transform);
        }

        private bool IsTargetUsable()
        {
            return _target != null && CanTarget(_targetHealth);
        }

        private Vector2 ResolveAttackDirection()
        {
            if (IsTargetUsable())
            {
                Vector2 direction = _target.position - transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    _lastFacingDirection = direction.normalized;
                    return _lastFacingDirection;
                }
            }

            return _lastFacingDirection.sqrMagnitude > 0.0001f ? _lastFacingDirection.normalized : Vector2.right;
        }

        private void FaceTarget()
        {
            Vector2 direction = ResolveAttackDirection();
            ApplyFacing(direction);
        }

        private void ApplyFacing(Vector2 direction)
        {
            if (!flipVisualByDirection || spriteRenderer == null || Mathf.Abs(direction.x) <= 0.01f)
            {
                return;
            }

            bool faceLeft = direction.x < 0f;
            spriteRenderer.flipX = invertFlipX ? !faceLeft : faceLeft;
        }

        private void PlayAnimation(string stateName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            animator.Play(stateName, animationLayer, 0f);
        }

        private void SetWanderEnabled(bool isEnabled)
        {
            if (wanderController == null || wanderController.enabled == isEnabled)
            {
                return;
            }

            wanderController.enabled = isEnabled;
        }

        private bool IsDead()
        {
            return health != null && health.CurrentHealth <= 0f;
        }

        private static Vector2 Rotate(Vector2 direction, float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos).normalized;
        }

        private static float ResolveFanAngleOffset(int index, int count, float totalSpreadAngle)
        {
            if (count <= 1 || totalSpreadAngle <= 0f)
            {
                return 0f;
            }

            float normalizedIndex = Mathf.Clamp(index, 0, count - 1) / (float)(count - 1);
            return Mathf.Lerp(-totalSpreadAngle * 0.5f, totalSpreadAngle * 0.5f, normalizedIndex);
        }

        private static Vector2 ResolveFanHomingTargetOffset(
            Vector2 baseDirection,
            int index,
            int count,
            float laneSpacing,
            float forwardSpacing)
        {
            if (count <= 1 || (laneSpacing <= 0f && forwardSpacing <= 0f))
            {
                return Vector2.zero;
            }

            Vector2 direction = baseDirection.sqrMagnitude > 0.0001f ? baseDirection.normalized : Vector2.right;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float centeredIndex = index - (count - 1) * 0.5f;
            float forwardOffset = Mathf.Abs(centeredIndex) * forwardSpacing;
            return perpendicular * (centeredIndex * laneSpacing) + direction * forwardOffset;
        }

        private static bool IsLayerInMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[RenaBossSpellCombat] " + message, this);
            }
        }
    }
}

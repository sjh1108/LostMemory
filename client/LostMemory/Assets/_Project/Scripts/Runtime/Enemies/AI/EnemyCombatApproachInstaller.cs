using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.AI
{
    public static class EnemyCombatApproachInstaller
    {
        private const string OrcEnemyId = "enemy_orc";
        private const string ChobombEnemyId = "enemy_chobomb";
        private const string MooseEnemyId = "enemy_moose1";
        private const string OrcRiderEnemyId = "enemy_orcrider";
        private const string SkeletonArcherEnemyId = "enemy_skeletonarcher";
        private const string StoneGolemEnemyId = "enemy_stonegolem";
        private const string DetectingStateName = "Detecting";
        private const string IdleStateName = "Idle";
        private const string MovingStateName = "Moving";
        private const string ApproachStateName = "Approach";
        private const string ObstaclesLayerName = "Obstacles";
        private const string DungeonWallLayerName = "DungeonWall";

        public static void ApplySpawnOverrides(string enemyId, GameObject enemyInstance)
        {
            if (enemyInstance == null || !TryGetProfile(enemyId, out EnemyCombatApproachProfile profile))
            {
                return;
            }

            TryInstallCombatApproach(enemyInstance, profile);
        }

        public static bool TryGetProfile(string enemyId, out EnemyCombatApproachProfile profile)
        {
            switch (enemyId)
            {
                case OrcEnemyId:
                    profile = CreateOrcProfile();
                    return true;
                case ChobombEnemyId:
                    profile = new EnemyCombatApproachProfile(
                        ChobombEnemyId,
                        MovingStateName,
                        DetectingStateName,
                        "Chobomb",
                        preferredDistance: 1f,
                        distanceTolerance: 0.12f,
                        emergencyRetreatDistance: 0.45f,
                        strafeWeight: 0.2f);
                    return true;
                case MooseEnemyId:
                    profile = new EnemyCombatApproachProfile(
                        MooseEnemyId,
                        MovingStateName,
                        DetectingStateName,
                        "Moose",
                        preferredDistance: 1.35f,
                        distanceTolerance: 0.15f,
                        emergencyRetreatDistance: 0.75f,
                        strafeWeight: 0.35f);
                    return true;
                case OrcRiderEnemyId:
                    profile = new EnemyCombatApproachProfile(
                        OrcRiderEnemyId,
                        MovingStateName,
                        DetectingStateName,
                        "Orc Rider",
                        preferredDistance: 4.25f,
                        distanceTolerance: 0.25f,
                        emergencyRetreatDistance: 2.2f,
                        strafeWeight: 0.15f);
                    return true;
                case SkeletonArcherEnemyId:
                    profile = new EnemyCombatApproachProfile(
                        SkeletonArcherEnemyId,
                        ApproachStateName,
                        IdleStateName,
                        "Skeleton Archer",
                        preferredDistance: 5.35f,
                        distanceTolerance: 0.25f,
                        emergencyRetreatDistance: 3.15f,
                        strafeWeight: 0.25f);
                    return true;
                case StoneGolemEnemyId:
                    profile = new EnemyCombatApproachProfile(
                        StoneGolemEnemyId,
                        MovingStateName,
                        DetectingStateName,
                        "Stone Golem",
                        preferredDistance: 1.35f,
                        distanceTolerance: 0.15f,
                        emergencyRetreatDistance: 0.8f,
                        strafeWeight: 0.25f);
                    return true;
                default:
                    profile = default(EnemyCombatApproachProfile);
                    return false;
            }
        }

        public static bool TryGetProfileForPrefabName(string prefabName, out EnemyCombatApproachProfile profile)
        {
            string normalized = prefabName?.ToLowerInvariant() ?? string.Empty;
            if (normalized.Contains("chobomb"))
            {
                return TryGetProfile(ChobombEnemyId, out profile);
            }

            if (normalized.Contains("moose"))
            {
                return TryGetProfile(MooseEnemyId, out profile);
            }

            if (normalized.Contains("orcrider"))
            {
                return TryGetProfile(OrcRiderEnemyId, out profile);
            }

            if (normalized.Contains("skeletonarcher"))
            {
                return TryGetProfile(SkeletonArcherEnemyId, out profile);
            }

            if (normalized.Contains("stonegolem"))
            {
                return TryGetProfile(StoneGolemEnemyId, out profile);
            }

            if (normalized.Contains("orc"))
            {
                return TryGetProfile(OrcEnemyId, out profile);
            }

            profile = default(EnemyCombatApproachProfile);
            return false;
        }

        public static EnemyCombatApproachProfile CreateDefaultMeleeProfile(string label)
        {
            return new EnemyCombatApproachProfile(
                string.Empty,
                MovingStateName,
                DetectingStateName,
                label,
                preferredDistance: 1.2f,
                distanceTolerance: 0.15f,
                emergencyRetreatDistance: 0.65f,
                strafeWeight: 0.3f);
        }

        public static bool TryInstallCombatApproach(GameObject enemyInstance, EnemyCombatApproachProfile profile)
        {
            if (enemyInstance == null)
            {
                return false;
            }

            string stateName = string.IsNullOrEmpty(profile.StateName) ? MovingStateName : profile.StateName;
            string acquisitionStateName = string.IsNullOrEmpty(profile.AcquisitionStateName)
                ? DetectingStateName
                : profile.AcquisitionStateName;
            string label = string.IsNullOrEmpty(profile.Label) ? enemyInstance.name : profile.Label;

            return TryInstallCombatApproach(
                enemyInstance,
                stateName,
                acquisitionStateName,
                label,
                profile.PreferredDistance,
                profile.DistanceTolerance,
                profile.EmergencyRetreatDistance,
                profile.StrafeWeight);
        }

        public static bool TryInstallOrcCombatApproach(
            GameObject enemyInstance,
            string stateName,
            float preferredDistance,
            float distanceTolerance,
            float emergencyRetreatDistance,
            float strafeWeight)
        {
            return TryInstallCombatApproach(
                enemyInstance,
                stateName,
                DetectingStateName,
                "Orc",
                preferredDistance,
                distanceTolerance,
                emergencyRetreatDistance,
                strafeWeight);
        }

        private static bool TryInstallCombatApproach(
            GameObject enemyInstance,
            string stateName,
            string acquisitionStateName,
            string label,
            float preferredDistance,
            float distanceTolerance,
            float emergencyRetreatDistance,
            float strafeWeight)
        {
            if (enemyInstance == null)
            {
                return false;
            }

            AIBrain brain = enemyInstance.GetComponentInChildren<AIBrain>();
            if (brain == null || brain.States == null)
            {
                return false;
            }

            AIState state = FindState(brain, stateName);
            if (state == null || state.Actions == null)
            {
                return false;
            }

            CharacterMovement movement = enemyInstance.GetComponentInParent<Character>()?.FindAbility<CharacterMovement>();
            AIDecisionDetectTargetRadius2D detector = enemyInstance.GetComponentInChildren<AIDecisionDetectTargetRadius2D>();
            LayerMask obstacleMask = ResolveRuntimeObstacleMask(detector != null ? detector.ObstacleMask : default(LayerMask));
            bool useObstacleAvoidance = obstacleMask.value != 0;

            if (detector != null)
            {
                detector.ObstacleMask = obstacleMask;
                detector.ObstacleDetection = false;
            }

            AIDecisionCurrentTargetIsValid keepCurrentTarget = EnsureKeepCurrentTargetDecision(enemyInstance);
            ReplaceDetectionRangeChecksAfterAcquisition(brain, detector, keepCurrentTarget, acquisitionStateName);
            ReplaceTooFarResetChecksAfterAcquisition(brain, keepCurrentTarget, acquisitionStateName);

            int moveActionIndex = FindMoveActionIndex(state);
            if (moveActionIndex < 0)
            {
                return false;
            }

            AIActionCombatApproach2D combatApproach = enemyInstance.GetComponent<AIActionCombatApproach2D>();
            if (combatApproach == null)
            {
                combatApproach = enemyInstance.AddComponent<AIActionCombatApproach2D>();
            }

            combatApproach.Label = $"Runtime {label} Combat Approach";
            combatApproach.Configure(
                movement,
                preferredDistance,
                distanceTolerance,
                emergencyRetreatDistance,
                strafeWeight,
                obstacleMask,
                useObstacleAvoidance);
            combatApproach.Initialization();

            AIActionPathfindToCombatRange2D pathfindToCombatRange =
                enemyInstance.GetComponent<AIActionPathfindToCombatRange2D>();
            if (pathfindToCombatRange == null)
            {
                pathfindToCombatRange = enemyInstance.AddComponent<AIActionPathfindToCombatRange2D>();
            }

            pathfindToCombatRange.Label = $"Runtime {label} Pathfind To Combat Range";
            pathfindToCombatRange.Configure(
                movement,
                combatApproach,
                obstacleMask,
                preferredDistance,
                distanceTolerance);
            pathfindToCombatRange.Initialization();

            state.Actions[moveActionIndex] = pathfindToCombatRange;
            return true;
        }

        private static LayerMask ResolveRuntimeObstacleMask(LayerMask configuredMask)
        {
            int resolvedMask = configuredMask.value;
            int obstaclesMask = LayerMask.GetMask(ObstaclesLayerName);
            int dungeonWallMask = LayerMask.GetMask(DungeonWallLayerName);

            if (resolvedMask == 0)
            {
                resolvedMask = obstaclesMask;
            }

            if ((resolvedMask & obstaclesMask) != 0 && dungeonWallMask != 0)
            {
                resolvedMask |= dungeonWallMask;
            }

            return new LayerMask { value = resolvedMask };
        }

        private static AIDecisionCurrentTargetIsValid EnsureKeepCurrentTargetDecision(GameObject enemyInstance)
        {
            AIDecisionCurrentTargetIsValid decision = enemyInstance.GetComponent<AIDecisionCurrentTargetIsValid>();
            if (decision == null)
            {
                decision = enemyInstance.AddComponent<AIDecisionCurrentTargetIsValid>();
            }

            decision.Label = "Runtime Keep Current Target";
            decision.Initialization();
            return decision;
        }

        private static void ReplaceDetectionRangeChecksAfterAcquisition(
            AIBrain brain,
            AIDecisionDetectTargetRadius2D detector,
            AIDecisionCurrentTargetIsValid keepCurrentTarget,
            string acquisitionStateName)
        {
            if (brain == null || detector == null || keepCurrentTarget == null || brain.States == null)
            {
                return;
            }

            for (int i = 0; i < brain.States.Count; i++)
            {
                AIState state = brain.States[i];
                if (state == null || state.StateName == acquisitionStateName || state.Transitions == null)
                {
                    continue;
                }

                for (int j = 0; j < state.Transitions.Count; j++)
                {
                    AITransition transition = state.Transitions[j];
                    if (transition == null || transition.Decision != detector)
                    {
                        continue;
                    }

                    transition.Decision = keepCurrentTarget;
                }
            }
        }

        private static void ReplaceTooFarResetChecksAfterAcquisition(
            AIBrain brain,
            AIDecisionCurrentTargetIsValid keepCurrentTarget,
            string acquisitionStateName)
        {
            if (brain == null || keepCurrentTarget == null || brain.States == null)
            {
                return;
            }

            for (int i = 0; i < brain.States.Count; i++)
            {
                AIState state = brain.States[i];
                if (state == null || state.StateName == acquisitionStateName || state.Transitions == null)
                {
                    continue;
                }

                for (int j = 0; j < state.Transitions.Count; j++)
                {
                    AITransition transition = state.Transitions[j];
                    if (!IsTooFarResetTransition(transition, acquisitionStateName))
                    {
                        continue;
                    }

                    string resetState = transition.TrueState;
                    transition.Decision = keepCurrentTarget;
                    transition.TrueState = string.Empty;
                    transition.FalseState = resetState;
                }
            }
        }

        private static bool IsTooFarResetTransition(AITransition transition, string acquisitionStateName)
        {
            if (transition == null
                || string.IsNullOrEmpty(transition.TrueState)
                || !string.IsNullOrEmpty(transition.FalseState))
            {
                return false;
            }

            AIDecisionDistanceToTarget distanceDecision = transition.Decision as AIDecisionDistanceToTarget;
            if (distanceDecision == null)
            {
                return false;
            }

            bool transitionsToReset = transition.TrueState == "Reset" || transition.TrueState == acquisitionStateName;
            if (!transitionsToReset)
            {
                return false;
            }

            return distanceDecision.ComparisonMode == AIDecisionDistanceToTarget.ComparisonModes.GreaterThan
                   || distanceDecision.ComparisonMode == AIDecisionDistanceToTarget.ComparisonModes.StrictlyGreaterThan;
        }

        private static EnemyCombatApproachProfile CreateOrcProfile()
        {
            return new EnemyCombatApproachProfile(
                OrcEnemyId,
                MovingStateName,
                DetectingStateName,
                "Orc",
                preferredDistance: 1f,
                distanceTolerance: 0.1f,
                emergencyRetreatDistance: 0.55f,
                strafeWeight: 0.45f);
        }

        private static AIState FindState(AIBrain brain, string stateName)
        {
            for (int i = 0; i < brain.States.Count; i++)
            {
                AIState state = brain.States[i];
                if (state != null && state.StateName == stateName)
                {
                    return state;
                }
            }

            return null;
        }

        private static int FindMoveActionIndex(AIState state)
        {
            for (int i = 0; i < state.Actions.Count; i++)
            {
                if (state.Actions[i] is AIActionPathfindToCombatRange2D
                    || state.Actions[i] is AIActionCombatApproach2D
                    || state.Actions[i] is AIActionMoveTowardsTarget2D)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    public struct EnemyCombatApproachProfile
    {
        public EnemyCombatApproachProfile(
            string enemyId,
            string stateName,
            string acquisitionStateName,
            string label,
            float preferredDistance,
            float distanceTolerance,
            float emergencyRetreatDistance,
            float strafeWeight)
        {
            EnemyId = enemyId;
            StateName = stateName;
            AcquisitionStateName = acquisitionStateName;
            Label = label;
            PreferredDistance = preferredDistance;
            DistanceTolerance = distanceTolerance;
            EmergencyRetreatDistance = emergencyRetreatDistance;
            StrafeWeight = strafeWeight;
        }

        public string EnemyId;
        public string StateName;
        public string AcquisitionStateName;
        public string Label;
        public float PreferredDistance;
        public float DistanceTolerance;
        public float EmergencyRetreatDistance;
        public float StrafeWeight;
    }

    [AddComponentMenu("Lost Memory/Enemies/AI/AI Action Wander Around Spawn 2D")]
    public sealed class AIActionWanderAroundSpawn2D : AIAction
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;

        [Header("References")]
        [SerializeField] private CharacterMovement characterMovement;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private Health health;

        [Header("Wander")]
        [SerializeField, Min(0f)] private float wanderRadius = 2.5f;
        [SerializeField, Min(0f)] private float wanderSpeedMultiplier = 0.45f;
        [SerializeField] private Vector2 moveDurationRange = new Vector2(1.1f, 2.2f);
        [SerializeField] private Vector2 pauseDurationRange = new Vector2(0.35f, 0.9f);
        [SerializeField, Range(0f, 1f)] private float returnToSpawnBias = 0.65f;

        [Header("Obstacle Probe")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField, Min(0f)] private float obstacleProbeDistance = 0.45f;
        [SerializeField, Min(0.01f)] private float obstacleProbeRadius = 0.18f;

        private Vector2 _spawnPosition;
        private Vector2 _wanderDirection = Vector2.right;
        private float _nextDecisionTime;
        private bool _hasSpawnPosition;
        private bool _moving;
        private bool _contextSpeedApplied;

        protected override void Awake()
        {
            base.Awake();
            AutoAssignReferences();
            StoreSpawnPosition();
        }

        private void OnEnable()
        {
            AutoAssignReferences();
            StoreSpawnPosition();
        }

        private void OnDisable()
        {
            StopMovement();
            ReleaseWanderSpeed();
        }

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            wanderRadius = Mathf.Max(0f, wanderRadius);
            wanderSpeedMultiplier = Mathf.Max(0f, wanderSpeedMultiplier);
            moveDurationRange = NormalizeRange(moveDurationRange, 0.05f);
            pauseDurationRange = NormalizeRange(pauseDurationRange, 0f);
            returnToSpawnBias = Mathf.Clamp01(returnToSpawnBias);
            obstacleProbeDistance = Mathf.Max(0f, obstacleProbeDistance);
            obstacleProbeRadius = Mathf.Max(0.01f, obstacleProbeRadius);
        }

        public override void Initialization()
        {
            if (!ShouldInitialize)
            {
                return;
            }

            base.Initialization();
            AutoAssignReferences();
            StoreSpawnPosition();
        }

        public override void OnEnterState()
        {
            base.OnEnterState();
            StoreSpawnPosition();
            ApplyWanderSpeed();
            BeginMove();
        }

        public override void PerformAction()
        {
            Wander();
        }

        public override void OnExitState()
        {
            base.OnExitState();
            StopMovement();
            ReleaseWanderSpeed();
        }

        public void Configure(
            CharacterMovement configuredMovement,
            LayerMask configuredObstacleMask,
            float configuredWanderRadius,
            float configuredWanderSpeedMultiplier,
            Vector2 configuredMoveDurationRange,
            Vector2 configuredPauseDurationRange,
            float configuredReturnToSpawnBias,
            float configuredObstacleProbeDistance,
            float configuredObstacleProbeRadius)
        {
            characterMovement = configuredMovement;
            obstacleMask = configuredObstacleMask;
            wanderRadius = Mathf.Max(0f, configuredWanderRadius);
            wanderSpeedMultiplier = Mathf.Max(0f, configuredWanderSpeedMultiplier);
            moveDurationRange = NormalizeRange(configuredMoveDurationRange, 0.05f);
            pauseDurationRange = NormalizeRange(configuredPauseDurationRange, 0f);
            returnToSpawnBias = Mathf.Clamp01(configuredReturnToSpawnBias);
            obstacleProbeDistance = Mathf.Max(0f, configuredObstacleProbeDistance);
            obstacleProbeRadius = Mathf.Max(0.01f, configuredObstacleProbeRadius);
            AutoAssignReferences();
            StoreSpawnPosition();
        }

        private void Wander()
        {
            if (_brain == null || _brain.Target != null || characterMovement == null || !IsAlive())
            {
                StopMovement();
                return;
            }

            if (Time.time >= _nextDecisionTime)
            {
                if (_moving)
                {
                    BeginPause();
                }
                else
                {
                    BeginMove();
                }
            }

            if (!_moving)
            {
                StopMovement();
                return;
            }

            if (_wanderDirection.sqrMagnitude <= MinDirectionSqrMagnitude || IsBlocked(_wanderDirection))
            {
                BeginMove();
            }

            if (_wanderDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                StopMovement();
                return;
            }

            characterMovement.SetMovement(_wanderDirection.normalized);
        }

        private void BeginMove()
        {
            if (!TryPickDirection(out _wanderDirection))
            {
                BeginPause();
                return;
            }

            _moving = true;
            _nextDecisionTime = Time.time + Random.Range(moveDurationRange.x, moveDurationRange.y);
            ApplyWanderSpeed();
        }

        private void BeginPause()
        {
            _moving = false;
            _wanderDirection = Vector2.zero;
            _nextDecisionTime = Time.time + Random.Range(pauseDurationRange.x, pauseDurationRange.y);
            StopMovement();
        }

        private bool TryPickDirection(out Vector2 direction)
        {
            Vector2 origin = ResolveBodyCenter();
            Vector2 toSpawn = _spawnPosition - origin;
            bool outsideRadius = wanderRadius > 0f && toSpawn.magnitude > wanderRadius;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 candidate = Random.insideUnitCircle;
                if (candidate.sqrMagnitude <= MinDirectionSqrMagnitude)
                {
                    continue;
                }

                candidate.Normalize();
                if (outsideRadius && toSpawn.sqrMagnitude > MinDirectionSqrMagnitude)
                {
                    candidate = (candidate * (1f - returnToSpawnBias)
                                 + toSpawn.normalized * returnToSpawnBias).normalized;
                }

                if (!IsBlocked(candidate) && StaysNearSpawn(origin, candidate))
                {
                    direction = candidate;
                    return true;
                }
            }

            if (toSpawn.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                direction = toSpawn.normalized;
                return !IsBlocked(direction);
            }

            direction = Vector2.zero;
            return false;
        }

        private bool StaysNearSpawn(Vector2 origin, Vector2 direction)
        {
            if (wanderRadius <= 0f)
            {
                return true;
            }

            Vector2 samplePosition = origin + direction.normalized * Mathf.Max(0.1f, obstacleProbeDistance);
            return Vector2.Distance(samplePosition, _spawnPosition) <= wanderRadius + 0.25f;
        }

        private bool IsBlocked(Vector2 direction)
        {
            if (obstacleMask.value == 0
                || obstacleProbeDistance <= 0f
                || direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                return false;
            }

            RaycastHit2D hit = Physics2D.CircleCast(
                ResolveBodyCenter(),
                obstacleProbeRadius,
                direction.normalized,
                obstacleProbeDistance,
                obstacleMask);
            return hit.collider != null;
        }

        private Vector2 ResolveBodyCenter()
        {
            return bodyCollider != null ? bodyCollider.bounds.center : transform.position;
        }

        private void StopMovement()
        {
            characterMovement?.SetMovement(Vector2.zero);
        }

        private void ApplyWanderSpeed()
        {
            if (characterMovement == null || _contextSpeedApplied)
            {
                return;
            }

            characterMovement.SetContextSpeedMultiplier(wanderSpeedMultiplier);
            _contextSpeedApplied = true;
        }

        private void ReleaseWanderSpeed()
        {
            if (characterMovement == null || !_contextSpeedApplied)
            {
                return;
            }

            characterMovement.ResetContextSpeedMultiplier();
            _contextSpeedApplied = false;
        }

        private void AutoAssignReferences()
        {
            Character character = gameObject.GetComponentInParent<Character>();
            characterMovement ??= character != null ? character.FindAbility<CharacterMovement>() : null;
            bodyCollider ??= gameObject.GetComponentInParent<Collider2D>();
            health ??= gameObject.GetComponentInParent<Health>();
        }

        private bool IsAlive()
        {
            return health == null || health.CurrentHealth > 0f;
        }

        private void StoreSpawnPosition()
        {
            if (_hasSpawnPosition)
            {
                return;
            }

            _spawnPosition = transform.position;
            _hasSpawnPosition = true;
        }

        private static Vector2 NormalizeRange(Vector2 range, float minimum)
        {
            range.x = Mathf.Max(minimum, range.x);
            range.y = Mathf.Max(range.x, range.y);
            return range;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/AI/Enemy Damage Aggro Target")]
    public sealed class EnemyDamageAggroTarget : MonoBehaviour, MMEventListener<MMDamageTakenEvent>
    {
        private const string PlayerTag = "Player";
        private const string DetectingStateName = "Detecting";
        private const string IdleStateName = "Idle";

        [Header("References")]
        [SerializeField] private Health health;
        [SerializeField] private AIBrain brain;

        [Header("State")]
        [SerializeField] private string acquisitionStateName = DetectingStateName;
        [SerializeField] private string combatStateName = "Moving";
        [SerializeField] private bool transitionToCombatFromAcquisition = true;

        [Header("Aggro")]
        [SerializeField, Min(0f)] private float aggroLockDuration = 1.5f;
        [SerializeField, Min(0f)] private float targetSwitchBreakDistance = 12f;

        private Transform _lockedTarget;
        private float _lockedUntil;

        public static EnemyDamageAggroTarget EnsureOn(
            GameObject enemyInstance,
            EnemyStageOneBehaviorProfile profile)
        {
            if (enemyInstance == null)
            {
                return null;
            }

            EnemyDamageAggroTarget aggro = enemyInstance.GetComponent<EnemyDamageAggroTarget>();
            if (aggro == null)
            {
                aggro = enemyInstance.AddComponent<EnemyDamageAggroTarget>();
            }

            aggro.Configure(profile);
            return aggro;
        }

        public void Configure(EnemyStageOneBehaviorProfile profile)
        {
            acquisitionStateName = string.IsNullOrEmpty(profile.AcquisitionStateName)
                ? DetectingStateName
                : profile.AcquisitionStateName;
            combatStateName = string.IsNullOrEmpty(profile.CombatStateName)
                ? "Moving"
                : profile.CombatStateName;
            aggroLockDuration = Mathf.Max(0f, profile.AggroLockDuration);
            targetSwitchBreakDistance = Mathf.Max(0f, profile.TargetSwitchBreakDistance);
            AutoAssignReferences();
        }

        private void Awake()
        {
            AutoAssignReferences();
        }

        private void OnEnable()
        {
            AutoAssignReferences();
            this.MMEventStartListening<MMDamageTakenEvent>();
        }

        private void OnDisable()
        {
            this.MMEventStopListening<MMDamageTakenEvent>();
        }

        private void OnValidate()
        {
            aggroLockDuration = Mathf.Max(0f, aggroLockDuration);
            targetSwitchBreakDistance = Mathf.Max(0f, targetSwitchBreakDistance);
        }

        public void OnMMEvent(MMDamageTakenEvent damageEvent)
        {
            if (!isActiveAndEnabled
                || damageEvent.AffectedHealth == null
                || damageEvent.DamageCaused <= 0f)
            {
                return;
            }

            AutoAssignReferences();
            if (!IsOwnedHealth(damageEvent.AffectedHealth))
            {
                return;
            }

            Transform attacker = ResolvePlayerTarget(damageEvent.Instigator);
            if (attacker == null || !IsLivingPlayer(attacker))
            {
                return;
            }

            if (!CanSwitchTarget(attacker))
            {
                return;
            }

            Acquire(attacker);
        }

        private void Acquire(Transform target)
        {
            if (brain == null || target == null)
            {
                return;
            }

            brain.Target = target;
            brain._lastKnownTargetPosition = target.position;
            _lockedTarget = target;
            _lockedUntil = Time.time + aggroLockDuration;

            if (transitionToCombatFromAcquisition
                && IsInAcquisitionState()
                && HasState(combatStateName))
            {
                brain.TransitionToState(combatStateName);
            }
        }

        private bool CanSwitchTarget(Transform attacker)
        {
            if (brain == null)
            {
                return false;
            }

            if (attacker == _lockedTarget || attacker == brain.Target)
            {
                return true;
            }

            if (!IsLivingPlayer(brain.Target))
            {
                return true;
            }

            if (IsCurrentTargetTooFar())
            {
                return true;
            }

            return Time.time >= _lockedUntil;
        }

        private bool IsCurrentTargetTooFar()
        {
            if (targetSwitchBreakDistance <= 0f || brain == null || brain.Target == null)
            {
                return false;
            }

            float maxSqrDistance = targetSwitchBreakDistance * targetSwitchBreakDistance;
            return ((Vector2)(brain.Target.position - transform.position)).sqrMagnitude > maxSqrDistance;
        }

        private bool IsInAcquisitionState()
        {
            if (brain == null || brain.CurrentState == null)
            {
                return true;
            }

            string stateName = brain.CurrentState.StateName;
            return string.IsNullOrEmpty(stateName)
                || stateName == acquisitionStateName
                || stateName == DetectingStateName
                || stateName == IdleStateName;
        }

        private bool HasState(string stateName)
        {
            if (brain == null || brain.States == null || string.IsNullOrEmpty(stateName))
            {
                return false;
            }

            for (int i = 0; i < brain.States.Count; i++)
            {
                AIState state = brain.States[i];
                if (state != null && state.StateName == stateName)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsOwnedHealth(Health affectedHealth)
        {
            if (affectedHealth == null)
            {
                return false;
            }

            if (affectedHealth == health)
            {
                return true;
            }

            if (health != null && health.MasterHealth == affectedHealth)
            {
                return true;
            }

            Transform affectedTransform = affectedHealth.transform;
            return affectedTransform == transform
                || affectedTransform.IsChildOf(transform)
                || transform.IsChildOf(affectedTransform);
        }

        private static Transform ResolvePlayerTarget(GameObject instigator)
        {
            if (instigator == null)
            {
                return null;
            }

            Character character = instigator.GetComponentInParent<Character>();
            if (character != null && character.CharacterType == Character.CharacterTypes.Player)
            {
                return character.transform;
            }

            return instigator.CompareTag(PlayerTag) ? instigator.transform : null;
        }

        private static bool IsLivingPlayer(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            Character character = target.GetComponentInParent<Character>();
            if (character != null)
            {
                if (character.CharacterType != Character.CharacterTypes.Player)
                {
                    return false;
                }

                Health characterHealth = character.CharacterHealth != null
                    ? character.CharacterHealth
                    : character.GetComponent<Health>();
                return characterHealth == null || characterHealth.CurrentHealth > 0f;
            }

            Health targetHealth = target.GetComponentInParent<Health>();
            return target.CompareTag(PlayerTag) && (targetHealth == null || targetHealth.CurrentHealth > 0f);
        }

        private void AutoAssignReferences()
        {
            health ??= GetComponent<Health>() ?? GetComponentInParent<Health>();
            brain ??= GetComponentInChildren<AIBrain>();
        }
    }

    public static class StageOneEnemyBehaviorInstaller
    {
        private const string OrcEnemyId = "enemy_orc";
        private const string ChobombEnemyId = "enemy_chobomb";
        private const string MooseEnemyId = "enemy_moose1";
        private const string OrcRiderEnemyId = "enemy_orcrider";
        private const string StoneGolemEnemyId = "enemy_stonegolem";
        private const string DetectingStateName = "Detecting";
        private const string MovingStateName = "Moving";
        private const string ObstaclesLayerName = "Obstacles";
        private const string DungeonWallLayerName = "DungeonWall";

        public static void ApplySpawnOverrides(string enemyId, GameObject enemyInstance)
        {
            if (enemyInstance == null || !TryGetProfile(enemyId, out EnemyStageOneBehaviorProfile profile))
            {
                return;
            }

            EnemyCombatApproachInstaller.ApplySpawnOverrides(enemyId, enemyInstance);
            TryInstallWander(enemyInstance, profile);
            EnemyDamageAggroTarget.EnsureOn(enemyInstance, profile);
        }

        public static bool TryGetProfile(string enemyId, out EnemyStageOneBehaviorProfile profile)
        {
            switch (enemyId)
            {
                case OrcEnemyId:
                    profile = CreateProfile(OrcEnemyId, wanderRadius: 2.7f, wanderSpeedMultiplier: 0.45f);
                    return true;
                case ChobombEnemyId:
                    profile = CreateProfile(ChobombEnemyId, wanderRadius: 2.35f, wanderSpeedMultiplier: 0.38f);
                    return true;
                case MooseEnemyId:
                    profile = CreateProfile(MooseEnemyId, wanderRadius: 3f, wanderSpeedMultiplier: 0.45f);
                    return true;
                case OrcRiderEnemyId:
                    profile = CreateProfile(OrcRiderEnemyId, wanderRadius: 3.25f, wanderSpeedMultiplier: 0.5f);
                    return true;
                case StoneGolemEnemyId:
                    profile = CreateProfile(StoneGolemEnemyId, wanderRadius: 2.15f, wanderSpeedMultiplier: 0.32f);
                    return true;
                default:
                    profile = default;
                    return false;
            }
        }

        private static EnemyStageOneBehaviorProfile CreateProfile(
            string enemyId,
            float wanderRadius,
            float wanderSpeedMultiplier)
        {
            return new EnemyStageOneBehaviorProfile(
                enemyId,
                DetectingStateName,
                MovingStateName,
                wanderRadius,
                wanderSpeedMultiplier,
                new Vector2(1.1f, 2.2f),
                new Vector2(0.35f, 0.9f),
                returnToSpawnBias: 0.65f,
                obstacleProbeDistance: 0.45f,
                obstacleProbeRadius: 0.18f,
                obstacleMask: ResolveRuntimeObstacleMask(default),
                aggroLockDuration: 1.5f,
                targetSwitchBreakDistance: 12f);
        }

        private static void TryInstallWander(GameObject enemyInstance, EnemyStageOneBehaviorProfile profile)
        {
            AIBrain brain = enemyInstance.GetComponentInChildren<AIBrain>();
            AIState state = FindState(brain, profile.AcquisitionStateName);
            if (brain == null || state?.Actions == null)
            {
                return;
            }

            AIActionWanderAroundSpawn2D wander = enemyInstance.GetComponent<AIActionWanderAroundSpawn2D>();
            if (wander == null)
            {
                wander = enemyInstance.AddComponent<AIActionWanderAroundSpawn2D>();
            }

            CharacterMovement movement = enemyInstance.GetComponentInParent<Character>()?.FindAbility<CharacterMovement>();
            wander.Label = $"Runtime {profile.EnemyId} Wander Around Spawn";
            wander.Configure(
                movement,
                profile.ObstacleMask,
                profile.WanderRadius,
                profile.WanderSpeedMultiplier,
                profile.MoveDurationRange,
                profile.PauseDurationRange,
                profile.ReturnToSpawnBias,
                profile.ObstacleProbeDistance,
                profile.ObstacleProbeRadius);
            wander.Initialization();

            if (!state.Actions.Contains(wander))
            {
                state.Actions.Add(wander);
            }
        }

        private static AIState FindState(AIBrain brain, string stateName)
        {
            if (brain == null || brain.States == null)
            {
                return null;
            }

            for (int i = 0; i < brain.States.Count; i++)
            {
                AIState state = brain.States[i];
                if (state != null && state.StateName == stateName)
                {
                    return state;
                }
            }

            return null;
        }

        private static LayerMask ResolveRuntimeObstacleMask(LayerMask configuredMask)
        {
            int resolvedMask = configuredMask.value;
            int obstaclesMask = LayerMask.GetMask(ObstaclesLayerName);
            int dungeonWallMask = LayerMask.GetMask(DungeonWallLayerName);

            if (resolvedMask == 0)
            {
                resolvedMask = obstaclesMask;
            }

            if (dungeonWallMask != 0)
            {
                resolvedMask |= dungeonWallMask;
            }

            return new LayerMask { value = resolvedMask };
        }
    }

    public readonly struct EnemyStageOneBehaviorProfile
    {
        public EnemyStageOneBehaviorProfile(
            string enemyId,
            string acquisitionStateName,
            string combatStateName,
            float wanderRadius,
            float wanderSpeedMultiplier,
            Vector2 moveDurationRange,
            Vector2 pauseDurationRange,
            float returnToSpawnBias,
            float obstacleProbeDistance,
            float obstacleProbeRadius,
            LayerMask obstacleMask,
            float aggroLockDuration,
            float targetSwitchBreakDistance)
        {
            EnemyId = enemyId;
            AcquisitionStateName = acquisitionStateName;
            CombatStateName = combatStateName;
            WanderRadius = wanderRadius;
            WanderSpeedMultiplier = wanderSpeedMultiplier;
            MoveDurationRange = moveDurationRange;
            PauseDurationRange = pauseDurationRange;
            ReturnToSpawnBias = returnToSpawnBias;
            ObstacleProbeDistance = obstacleProbeDistance;
            ObstacleProbeRadius = obstacleProbeRadius;
            ObstacleMask = obstacleMask;
            AggroLockDuration = aggroLockDuration;
            TargetSwitchBreakDistance = targetSwitchBreakDistance;
        }

        public string EnemyId { get; }
        public string AcquisitionStateName { get; }
        public string CombatStateName { get; }
        public float WanderRadius { get; }
        public float WanderSpeedMultiplier { get; }
        public Vector2 MoveDurationRange { get; }
        public Vector2 PauseDurationRange { get; }
        public float ReturnToSpawnBias { get; }
        public float ObstacleProbeDistance { get; }
        public float ObstacleProbeRadius { get; }
        public LayerMask ObstacleMask { get; }
        public float AggroLockDuration { get; }
        public float TargetSwitchBreakDistance { get; }
    }
}

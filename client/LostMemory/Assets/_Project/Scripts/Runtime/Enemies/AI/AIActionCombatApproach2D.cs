using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.AI
{
    [AddComponentMenu("Lost Memory/Enemies/AI/AI Action Combat Approach 2D")]
    public sealed class AIActionCombatApproach2D : AIAction
    {
        [Header("References")]
        [SerializeField] private CharacterMovement characterMovement;
        [SerializeField] private Collider2D bodyCollider;

        [Header("Distance")]
        [SerializeField, Min(0f)] private float preferredDistance = 1f;
        [SerializeField, Min(0f)] private float distanceTolerance = 0.1f;
        [SerializeField, Min(0f)] private float emergencyRetreatDistance = 0.55f;

        [Header("Movement")]
        [SerializeField, Range(0f, 1f)] private float approachWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float strafeWeight = 0.45f;
        [SerializeField] private bool strafeWhileApproaching = true;
        [SerializeField] private bool randomizeInitialStrafeDirection = true;
        [SerializeField, Min(0.05f)] private float strafeSwitchInterval = 1.25f;
        [SerializeField, Range(0f, 1f)] private float strafeSwitchChance = 0.35f;

        [Header("Prediction")]
        [SerializeField, Min(0f)] private float targetPredictionTime = 0.15f;

        [Header("Obstacle Avoidance")]
        [SerializeField] private bool useObstacleAvoidance = true;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private bool useBodyColliderClearance = true;
        [SerializeField, Min(0f)] private float obstacleClearancePadding = 0.05f;
        [SerializeField, Min(0.01f)] private float obstacleProbeRadius = 0.18f;
        [SerializeField, Min(0.01f)] private float obstacleProbeDistance = 0.6f;
        [SerializeField, Range(30f, 180f)] private float obstacleSweepAngle = 140f;
        [SerializeField, Range(3, 15)] private int obstacleSweepSteps = 9;
        [SerializeField, Min(0f)] private float wallFollowDuration = 0.65f;
        [SerializeField, Range(0f, 1f)] private float wallFollowTargetBias = 0.25f;

        [Header("Stuck Recovery")]
        [SerializeField] private bool useStuckRecovery = true;
        [SerializeField, Min(0.05f)] private float stuckCheckInterval = 0.35f;
        [SerializeField, Min(0f)] private float stuckMinimumProgress = 0.04f;
        [SerializeField, Min(0.05f)] private float stuckRecoveryDuration = 0.45f;
        [SerializeField, Range(0f, 1f)] private float stuckRecoveryStrafeBias = 0.7f;

        private const float MinDirectionSqrMagnitude = 0.0001f;

        private int _strafeDirection = 1;
        private int _wallFollowDirection = 1;
        private float _nextStrafeSwitchTime;
        private float _wallFollowUntil;
        private float _nextStuckCheckTime;
        private float _stuckRecoveryUntil;
        private int _stuckRecoverySide = 1;
        private Vector2 _lastProgressPosition;
        private Vector2 _stuckRecoveryDirection;
        private Rigidbody2D _targetBody;
        private Transform _cachedTarget;

        protected override void Awake()
        {
            base.Awake();
            AutoAssignReferences();
        }

        private void OnEnable()
        {
            AutoAssignReferences();
        }

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            preferredDistance = Mathf.Max(0f, preferredDistance);
            distanceTolerance = Mathf.Max(0f, distanceTolerance);
            emergencyRetreatDistance = Mathf.Max(0f, emergencyRetreatDistance);
            strafeSwitchInterval = Mathf.Max(0.05f, strafeSwitchInterval);
            obstacleClearancePadding = Mathf.Max(0f, obstacleClearancePadding);
            obstacleProbeRadius = Mathf.Max(0.01f, obstacleProbeRadius);
            obstacleProbeDistance = Mathf.Max(0.01f, obstacleProbeDistance);
            obstacleSweepSteps = Mathf.Max(3, obstacleSweepSteps);
            wallFollowDuration = Mathf.Max(0f, wallFollowDuration);
            stuckCheckInterval = Mathf.Max(0.05f, stuckCheckInterval);
            stuckMinimumProgress = Mathf.Max(0f, stuckMinimumProgress);
            stuckRecoveryDuration = Mathf.Max(0.05f, stuckRecoveryDuration);
            AutoAssignReferences();
        }

        public override void Initialization()
        {
            if (!ShouldInitialize)
            {
                return;
            }

            base.Initialization();
            AutoAssignReferences();
        }

        public override void OnEnterState()
        {
            base.OnEnterState();
            PickInitialStrafeDirection();
            ScheduleNextStrafeSwitch();
            ResetStuckTracking();
        }

        public override void PerformAction()
        {
            Move();
        }

        public override void OnExitState()
        {
            base.OnExitState();
            StopMovement();
        }

        public void Configure(
            CharacterMovement configuredCharacterMovement,
            float configuredPreferredDistance,
            float configuredDistanceTolerance,
            float configuredEmergencyRetreatDistance,
            float configuredStrafeWeight)
        {
            Configure(
                configuredCharacterMovement,
                configuredPreferredDistance,
                configuredDistanceTolerance,
                configuredEmergencyRetreatDistance,
                configuredStrafeWeight,
                obstacleMask,
                useObstacleAvoidance);
        }

        public void Configure(
            CharacterMovement configuredCharacterMovement,
            float configuredPreferredDistance,
            float configuredDistanceTolerance,
            float configuredEmergencyRetreatDistance,
            float configuredStrafeWeight,
            LayerMask configuredObstacleMask,
            bool configuredUseObstacleAvoidance)
        {
            characterMovement = configuredCharacterMovement;
            preferredDistance = Mathf.Max(0f, configuredPreferredDistance);
            distanceTolerance = Mathf.Max(0f, configuredDistanceTolerance);
            emergencyRetreatDistance = Mathf.Max(0f, configuredEmergencyRetreatDistance);
            strafeWeight = Mathf.Clamp01(configuredStrafeWeight);
            obstacleMask = configuredObstacleMask;
            useObstacleAvoidance = configuredUseObstacleAvoidance;
        }

        private void Move()
        {
            if (_brain == null || _brain.Target == null || characterMovement == null)
            {
                StopMovement();
                return;
            }

            UpdateStrafeDirection();

            Vector2 selfPosition = transform.position;
            Vector2 targetPosition = ResolveTargetPosition();
            Vector2 toTarget = targetPosition - selfPosition;

            if (toTarget.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                StopMovement();
                return;
            }

            float distance = toTarget.magnitude;
            Vector2 targetDirection = toTarget / distance;
            Vector2 strafeDirection = new Vector2(-targetDirection.y, targetDirection.x) * _strafeDirection;

            Vector2 movement = ResolveRadialMovement(targetDirection, distance);
            movement += ResolveStrafeMovement(strafeDirection, distance);
            Vector2 intendedMovement = movement;
            movement = AdjustForObstacles(movement, targetDirection, strafeDirection);
            movement = ApplyStuckRecovery(movement, intendedMovement, targetDirection, strafeDirection);

            if (movement.sqrMagnitude > 1f)
            {
                movement.Normalize();
            }

            characterMovement.SetMovement(movement);
        }

        private void AutoAssignReferences()
        {
            characterMovement ??= gameObject.GetComponentInParent<Character>()?.FindAbility<CharacterMovement>();
            bodyCollider ??= ResolveBodyCollider();
        }

        private Vector2 ResolveTargetPosition()
        {
            Transform target = _brain.Target;
            CacheTargetBody(target);

            Vector2 targetPosition = target.position;
            if (_targetBody != null && targetPredictionTime > 0f)
            {
                targetPosition += _targetBody.linearVelocity * targetPredictionTime;
            }

            return targetPosition;
        }

        private Vector2 ResolveRadialMovement(Vector2 targetDirection, float distance)
        {
            float innerDistance = Mathf.Max(emergencyRetreatDistance, preferredDistance - distanceTolerance);
            float outerDistance = preferredDistance + distanceTolerance;

            if (distance < innerDistance)
            {
                return -targetDirection * approachWeight;
            }

            if (distance > outerDistance)
            {
                return targetDirection * approachWeight;
            }

            return Vector2.zero;
        }

        private Vector2 ApplyStuckRecovery(
            Vector2 movement,
            Vector2 intendedMovement,
            Vector2 targetDirection,
            Vector2 strafeDirection)
        {
            if (!useStuckRecovery || intendedMovement.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                return movement;
            }

            Vector2 origin = ResolveObstacleProbeOrigin();
            if (Time.time <= _stuckRecoveryUntil)
            {
                if (!IsBlocked(origin, _stuckRecoveryDirection))
                {
                    return _stuckRecoveryDirection;
                }

                if (TryResolveStuckRecoveryDirection(origin, targetDirection, strafeDirection, out Vector2 replacementDirection))
                {
                    _stuckRecoveryDirection = replacementDirection;
                    return replacementDirection;
                }

                return movement;
            }

            if (Time.time < _nextStuckCheckTime)
            {
                return movement;
            }

            float progress = Vector2.Distance(origin, _lastProgressPosition);
            _lastProgressPosition = origin;
            _nextStuckCheckTime = Time.time + stuckCheckInterval;

            if (progress >= stuckMinimumProgress)
            {
                return movement;
            }

            if (!TryResolveStuckRecoveryDirection(origin, targetDirection, strafeDirection, out _stuckRecoveryDirection))
            {
                return movement;
            }

            _stuckRecoveryUntil = Time.time + stuckRecoveryDuration;
            SetWallFollowDirection(_stuckRecoverySide);
            return _stuckRecoveryDirection;
        }

        private bool TryResolveStuckRecoveryDirection(
            Vector2 origin,
            Vector2 targetDirection,
            Vector2 strafeDirection,
            out Vector2 recoveryDirection)
        {
            recoveryDirection = Vector2.zero;
            float bestScore = float.NegativeInfinity;
            int preferredSide = _stuckRecoverySide * -1;

            EvaluateStuckRecoveryCandidate(
                origin,
                (-targetDirection + strafeDirection * stuckRecoveryStrafeBias * preferredSide).normalized,
                1f,
                ref recoveryDirection,
                ref bestScore);
            EvaluateStuckRecoveryCandidate(
                origin,
                strafeDirection * preferredSide,
                0.85f,
                ref recoveryDirection,
                ref bestScore);
            EvaluateStuckRecoveryCandidate(
                origin,
                Rotate(-targetDirection, 45f * preferredSide),
                0.75f,
                ref recoveryDirection,
                ref bestScore);
            EvaluateStuckRecoveryCandidate(
                origin,
                -targetDirection,
                0.65f,
                ref recoveryDirection,
                ref bestScore);
            EvaluateStuckRecoveryCandidate(
                origin,
                -strafeDirection * preferredSide,
                0.5f,
                ref recoveryDirection,
                ref bestScore);

            if (recoveryDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                return false;
            }

            float cross = targetDirection.x * recoveryDirection.y - targetDirection.y * recoveryDirection.x;
            _stuckRecoverySide = cross < 0f ? -1 : 1;
            return true;
        }

        private void EvaluateStuckRecoveryCandidate(
            Vector2 origin,
            Vector2 direction,
            float score,
            ref Vector2 bestDirection,
            ref float bestScore)
        {
            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude || IsBlocked(origin, direction))
            {
                return;
            }

            if (score <= bestScore)
            {
                return;
            }

            bestScore = score;
            bestDirection = direction.normalized;
        }

        private Vector2 ResolveStrafeMovement(Vector2 strafeDirection, float distance)
        {
            if (strafeWeight <= 0f)
            {
                return Vector2.zero;
            }

            if (!strafeWhileApproaching && distance > preferredDistance + distanceTolerance)
            {
                return Vector2.zero;
            }

            float closeRangeMultiplier = distance < emergencyRetreatDistance ? 0.35f : 1f;
            return strafeDirection * strafeWeight * closeRangeMultiplier;
        }

        private Vector2 AdjustForObstacles(Vector2 movement, Vector2 targetDirection, Vector2 strafeDirection)
        {
            if (!useObstacleAvoidance || obstacleMask.value == 0 || movement.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                return movement;
            }

            Vector2 origin = ResolveObstacleProbeOrigin();
            Vector2 moveDirection = movement.normalized;

            if (!IsBlocked(origin, moveDirection))
            {
                return movement;
            }

            Vector2 wallFollowDirection = ResolveWallFollowDirection(targetDirection);
            if (!IsBlocked(origin, wallFollowDirection))
            {
                return wallFollowDirection;
            }

            Vector2 oppositeWallFollowDirection = ResolveWallFollowDirection(targetDirection, -_wallFollowDirection);
            if (!IsBlocked(origin, oppositeWallFollowDirection))
            {
                SetWallFollowDirection(-_wallFollowDirection);
                return oppositeWallFollowDirection;
            }

            if (TryFindOpenSweepDirection(origin, moveDirection, strafeDirection, out Vector2 sweepDirection))
            {
                return sweepDirection;
            }

            Vector2 retreatDirection = -targetDirection;
            if (!IsBlocked(origin, retreatDirection))
            {
                return retreatDirection;
            }

            return Vector2.zero;
        }

        private Vector2 ResolveWallFollowDirection(Vector2 targetDirection)
        {
            return ResolveWallFollowDirection(targetDirection, ResolveWallFollowSide());
        }

        private Vector2 ResolveWallFollowDirection(Vector2 targetDirection, int side)
        {
            Vector2 perpendicular = new Vector2(-targetDirection.y, targetDirection.x) * side;
            Vector2 biasedDirection = perpendicular + targetDirection * wallFollowTargetBias;
            return biasedDirection.sqrMagnitude > MinDirectionSqrMagnitude
                ? biasedDirection.normalized
                : perpendicular;
        }

        private int ResolveWallFollowSide()
        {
            if (Time.time <= _wallFollowUntil)
            {
                return _wallFollowDirection;
            }

            SetWallFollowDirection(_strafeDirection);
            return _wallFollowDirection;
        }

        private void SetWallFollowDirection(int direction)
        {
            _wallFollowDirection = direction < 0 ? -1 : 1;
            _strafeDirection = _wallFollowDirection;
            _wallFollowUntil = Time.time + wallFollowDuration;
        }

        private bool TryFindOpenSweepDirection(
            Vector2 origin,
            Vector2 baseDirection,
            Vector2 strafeDirection,
            out Vector2 openDirection)
        {
            openDirection = Vector2.zero;

            int steps = Mathf.Max(3, obstacleSweepSteps);
            float angleStep = obstacleSweepAngle / steps;
            float bestScore = float.NegativeInfinity;
            int preferredSide = ResolveWallFollowSide();

            for (int i = 1; i <= steps; i++)
            {
                float angle = angleStep * i;
                EvaluateSweepCandidate(origin, baseDirection, strafeDirection, angle * preferredSide, ref openDirection, ref bestScore);
                EvaluateSweepCandidate(origin, baseDirection, strafeDirection, -angle * preferredSide, ref openDirection, ref bestScore);
            }

            if (openDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                return false;
            }

            float cross = baseDirection.x * openDirection.y - baseDirection.y * openDirection.x;
            SetWallFollowDirection(cross < 0f ? -1 : 1);
            return true;
        }

        private void EvaluateSweepCandidate(
            Vector2 origin,
            Vector2 baseDirection,
            Vector2 strafeDirection,
            float angle,
            ref Vector2 bestDirection,
            ref float bestScore)
        {
            Vector2 candidate = Rotate(baseDirection, angle);
            if (IsBlocked(origin, candidate))
            {
                return;
            }

            float score = Vector2.Dot(candidate, baseDirection)
                          + Mathf.Max(0f, Vector2.Dot(candidate, strafeDirection.normalized)) * 0.2f
                          - Mathf.Abs(angle) / 180f * 0.1f;

            if (score <= bestScore)
            {
                return;
            }

            bestScore = score;
            bestDirection = candidate;
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos).normalized;
        }

        private bool IsBlocked(Vector2 origin, Vector2 direction)
        {
            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                return false;
            }

            RaycastHit2D hit = Physics2D.CircleCast(
                origin,
                ResolveObstacleProbeRadius(),
                direction.normalized,
                obstacleProbeDistance,
                obstacleMask);

            return hit.collider != null;
        }

        private Vector2 ResolveObstacleProbeOrigin()
        {
            return bodyCollider != null ? bodyCollider.bounds.center : transform.position;
        }

        private float ResolveObstacleProbeRadius()
        {
            if (!useBodyColliderClearance || bodyCollider == null)
            {
                return obstacleProbeRadius;
            }

            Bounds bounds = bodyCollider.bounds;
            float bodyRadius = Mathf.Max(bounds.extents.x, bounds.extents.y) + obstacleClearancePadding;
            return Mathf.Max(obstacleProbeRadius, bodyRadius);
        }

        private Collider2D ResolveBodyCollider()
        {
            Collider2D[] colliders = GetComponentsInParent<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D candidate = colliders[i];
                if (candidate != null && candidate.enabled && !candidate.isTrigger)
                {
                    return candidate;
                }
            }

            colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D candidate = colliders[i];
                if (candidate != null && candidate.enabled && !candidate.isTrigger)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void CacheTargetBody(Transform target)
        {
            if (_cachedTarget == target)
            {
                return;
            }

            _cachedTarget = target;
            _targetBody = target != null ? target.GetComponentInParent<Rigidbody2D>() : null;
        }

        private void PickInitialStrafeDirection()
        {
            if (!randomizeInitialStrafeDirection)
            {
                _strafeDirection = 1;
                return;
            }

            _strafeDirection = Random.value < 0.5f ? -1 : 1;
        }

        private void UpdateStrafeDirection()
        {
            if (Time.time < _nextStrafeSwitchTime)
            {
                return;
            }

            if (Random.value <= strafeSwitchChance)
            {
                _strafeDirection *= -1;
            }

            ScheduleNextStrafeSwitch();
        }

        private void ScheduleNextStrafeSwitch()
        {
            _nextStrafeSwitchTime = Time.time + strafeSwitchInterval;
        }

        private void ResetStuckTracking()
        {
            _lastProgressPosition = ResolveObstacleProbeOrigin();
            _nextStuckCheckTime = Time.time + stuckCheckInterval;
            _stuckRecoveryUntil = 0f;
            _stuckRecoveryDirection = Vector2.zero;
        }

        private void StopMovement()
        {
            characterMovement?.SetMovement(Vector2.zero);
        }
    }
}

using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Rena
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Rena/Rena Boss Wander Controller")]
    public sealed class RenaBossWanderController : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;

        [Header("References")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private RenaBossSpellCombatController phaseController;

        [Header("Animation")]
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string moveStateName = "Move";
        [SerializeField] private string phaseTwoMoveStateName = "SprintMove";
        [SerializeField] private string phaseTwoStopStateName = "SprintBreak";
        [SerializeField, Min(0)] private int animationLayer;
        [SerializeField] private bool flipVisualByDirection = true;
        [SerializeField] private bool invertFlipX;

        [Header("Wander")]
        [SerializeField, Min(0f)] private float moveSpeed = 3.3f;
        [SerializeField, Min(0f)] private float phaseTwoMoveSpeed = 4.9f;
        [SerializeField, Min(0f)] private float phaseTwoStopMinDuration = 0.5f;
        [SerializeField, Min(0f)] private float phaseTwoStopAnimationMinMoveDuration = 3f;
        [SerializeField, Min(0f)] private float phaseTwoStopAnimationCooldown = 3f;
        [SerializeField, Range(0f, 1f)] private float phaseTwoStopAnimationChance = 0.35f;
        [SerializeField, Min(0f)] private float wanderRadius = 5.25f;
        [SerializeField] private Vector2 moveDurationRange = new Vector2(3.5f, 5.2f);
        [SerializeField] private Vector2 pauseDurationRange = new Vector2(2.4f, 4.2f);
        [SerializeField, Range(0f, 1f)] private float moveStartChanceAfterPause = 0.35f;
        [SerializeField] private Vector2 moveSkippedPauseDurationRange = new Vector2(2.5f, 5f);
        [SerializeField, Range(0f, 1f)] private float pauseExtensionChance = 0.2f;
        [SerializeField] private Vector2 pauseExtensionDurationRange = new Vector2(1.2f, 2.4f);
        [SerializeField, Min(0)] private int maxPauseExtensionsBeforeMove = 1;
        [SerializeField, Min(1)] private int directionAttempts = 10;
        [SerializeField, Range(0f, 1f)] private float centerPullWhenOutsideRadius = 0.75f;

        [Header("Target Spacing")]
        [SerializeField] private bool keepDistanceFromTargets = true;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField, Min(0f)] private float targetDetectionRadius = 16f;
        [SerializeField, Min(0f)] private float preferredTargetDistance = 9f;
        [SerializeField, Min(0f)] private float targetDistanceTolerance = 2.2f;
        [SerializeField, Min(0.05f)] private float targetSpacingRefreshInterval = 0.6f;
        [SerializeField, Min(0f)] private float targetSpacingBlockedPauseDuration = 0.45f;
        [SerializeField, Min(0f)] private float targetSpacingCooldown = 10f;

        [Header("Collision Probe")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField, Min(0f)] private float obstacleProbeRadius = 0.35f;
        [SerializeField, Min(0f)] private float obstacleProbeDistance = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        private Vector2 _origin;
        private Vector2 _direction = Vector2.right;
        private float _stateTimer;
        private float _moveStartedAt;
        private float _nextPhaseTwoStopAnimationAllowedAt;
        private float _nextTargetSpacingRefreshTime;
        private float _nextTargetSpacingAllowedAt;
        private float _targetSpacingMoveEndsAt;
        private float _nextWanderMoveAllowedAt;
        private bool _hasOrigin;
        private bool _isPaused = true;
        private bool _isTargetSpacingMoveActive;
        private int _consecutivePauseExtensions;
        private bool _hasTargetSpacingDirection;
        private bool _targetSpacingNeeded;
        private Vector2 _targetSpacingDirection;
        private string _currentAnimationStateName;

        private void Reset()
        {
            RefreshReferences();
            SetWanderOrigin(transform.position);
        }

        private void Awake()
        {
            RefreshReferences();
            if (!_hasOrigin)
            {
                SetWanderOrigin(transform.position);
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            RefreshReferences();
            if (!_hasOrigin)
            {
                SetWanderOrigin(transform.position);
            }

            ResumeFromCurrentPosition();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                PlayAnimation(idleStateName);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_stateTimer > 0f)
            {
                _stateTimer -= Time.deltaTime;
            }

            if (_isTargetSpacingMoveActive && Time.time >= _targetSpacingMoveEndsAt)
            {
                EndTargetSpacingMove();
            }

            bool canAttemptMovement = IsWanderMoveAttemptReady();
            if (TryGetTargetSpacingDirection(out Vector2 targetSpacingDirection, out bool targetSpacingNeeded) &&
                (_isTargetSpacingMoveActive || (canAttemptMovement && CanStartTargetSpacingMove())))
            {
                if (!_isTargetSpacingMoveActive)
                {
                    BeginTargetSpacingMove();
                }

                _isPaused = false;
                _direction = targetSpacingDirection;
                if (!Move(strictDirection: true))
                {
                    EndTargetSpacingMove();
                }
                return;
            }

            if (_isTargetSpacingMoveActive)
            {
                EndTargetSpacingMove();
            }

            if (targetSpacingNeeded && canAttemptMovement && CanStartTargetSpacingMove())
            {
                BeginTargetSpacingBlockedPause();
                return;
            }

            if (_isPaused)
            {
                if (_stateTimer <= 0f || IsWanderMoveAttemptReady())
                {
                    _stateTimer = 0f;
                    if (!TryDelayMoveStart() && !TryExtendPauseBeforeMove())
                    {
                        BeginMove();
                    }
                }
                return;
            }

            if (!Move())
            {
                return;
            }

            if (_stateTimer <= 0f)
            {
                BeginPause();
            }
        }

        public void SetWanderOrigin(Vector2 origin)
        {
            _origin = origin;
            _hasOrigin = true;
        }

        public void RestartFromCurrentPosition()
        {
            SetWanderOrigin(transform.position);
            BeginMove();
            ResetTargetSpacingCache();
        }

        public void ResumeFromCurrentPosition()
        {
            SetWanderOrigin(transform.position);
            ClearTargetSpacingMove();
            ClearTargetSpacingDirection();

            if (_isPaused)
            {
                ResumePausedFromCurrentPosition();
                return;
            }

            if (_stateTimer <= 0f)
            {
                BeginPause();
                return;
            }

            if (_direction.sqrMagnitude <= MinDirectionSqrMagnitude && !TryPickDirection(out _direction))
            {
                BeginPause();
                return;
            }

            ApplyFacing(_direction);
            PlayAnimation(ResolveMoveStateName());
            Log("Wander resumed moving.");
        }

        public void ResumePausedFromCurrentPosition()
        {
            SetWanderOrigin(transform.position);
            _isPaused = true;
            _consecutivePauseExtensions = 0;
            ClearTargetSpacingMove();
            ClearTargetSpacingDirection();

            if (_nextWanderMoveAllowedAt <= 0f)
            {
                ScheduleNextWanderMoveAttempt(pauseDurationRange, 0f);
            }
            else
            {
                _stateTimer = Mathf.Max(0f, _nextWanderMoveAllowedAt - Time.time);
            }

            PlayAnimation(idleStateName);
            Log("Wander resumed paused.");
        }

        public void RefreshReferences()
        {
            if (visualRoot == null)
            {
                Transform visual = transform.Find("Visual");
                visualRoot = visual != null ? visual : transform;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(includeInactive: true);
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);
            }

            if (phaseController == null)
            {
                phaseController = GetComponent<RenaBossSpellCombatController>();
            }
        }

        private void BeginMove()
        {
            _isPaused = false;
            _stateTimer = RandomRange(moveDurationRange, 1f);
            _moveStartedAt = Time.time;
            _nextWanderMoveAllowedAt = 0f;
            _consecutivePauseExtensions = 0;

            if (!TryPickDirection(out _direction))
            {
                _direction = Vector2.right;
            }

            ApplyFacing(_direction);
            PlayAnimation(ResolveMoveStateName());
            Log("Wander move started.");
        }

        private void BeginPause()
        {
            _isPaused = true;
            ScheduleNextWanderMoveAttempt(pauseDurationRange, 0f);
            bool playPhaseTwoStopAnimation = TryReservePhaseTwoStopAnimation(allowStopAnimation: true);
            if (playPhaseTwoStopAnimation)
            {
                _stateTimer = Mathf.Max(_stateTimer, phaseTwoStopMinDuration);
            }

            PlayAnimation(playPhaseTwoStopAnimation ? phaseTwoStopStateName : idleStateName);
            Log("Wander pause started.");
        }

        private void BeginBlockedPause()
        {
            _isPaused = true;
            ScheduleNextWanderMoveAttempt(pauseDurationRange, 0f);
            PlayAnimation(idleStateName);
            Log("Wander blocked pause started.");
        }

        private bool TryExtendPauseBeforeMove()
        {
            if (pauseExtensionChance <= 0f ||
                _consecutivePauseExtensions >= maxPauseExtensionsBeforeMove ||
                Random.value > pauseExtensionChance)
            {
                return false;
            }

            _consecutivePauseExtensions++;
            ScheduleNextWanderMoveAttempt(pauseExtensionDurationRange, 0f);
            PlayAnimation(idleStateName);
            Log("Wander pause extended.");
            return true;
        }

        private bool TryDelayMoveStart()
        {
            if (moveStartChanceAfterPause >= 1f || Random.value <= moveStartChanceAfterPause)
            {
                return false;
            }

            ScheduleNextWanderMoveAttempt(moveSkippedPauseDurationRange, 1f);
            PlayAnimation(idleStateName);
            Log("Wander move delayed.");
            return true;
        }

        private void BeginTargetSpacingBlockedPause()
        {
            bool wasPaused = _isPaused;
            _isPaused = true;
            _stateTimer = Mathf.Max(_stateTimer, targetSpacingBlockedPauseDuration);
            StartTargetSpacingCooldown();

            if (!wasPaused)
            {
                PlayAnimation(idleStateName);
            }
            else if (!IsPhaseTwoActive() || _currentAnimationStateName == phaseTwoStopStateName)
            {
                PlayAnimation(idleStateName);
            }

            Log("Target spacing blocked.");
        }

        private bool Move(bool strictDirection = false)
        {
            if (ResolveMoveSpeed() <= 0f)
            {
                return false;
            }

            if (_direction.sqrMagnitude <= MinDirectionSqrMagnitude || IsBlocked(_direction))
            {
                if (strictDirection || !TryPickDirection(out _direction))
                {
                    BeginBlockedPause();
                    return false;
                }
            }

            Vector2 normalized = _direction.normalized;
            float distance = ResolveMoveSpeed() * Time.deltaTime;
            transform.position += (Vector3)(normalized * distance);
            ApplyFacing(normalized);
            PlayAnimation(ResolveMoveStateName());
            return true;
        }

        private void ResetTargetSpacingCache(bool preserveCooldown = false)
        {
            _nextTargetSpacingRefreshTime = 0f;
            if (!preserveCooldown)
            {
                _nextTargetSpacingAllowedAt = 0f;
            }

            ClearTargetSpacingMove();
            ClearTargetSpacingDirection();
        }

        private void ClearTargetSpacingMove()
        {
            _targetSpacingMoveEndsAt = 0f;
            _isTargetSpacingMoveActive = false;
        }

        private void ClearTargetSpacingDirection()
        {
            _hasTargetSpacingDirection = false;
            _targetSpacingNeeded = false;
            _targetSpacingDirection = Vector2.zero;
        }

        private bool IsWanderMoveAttemptReady()
        {
            return _nextWanderMoveAllowedAt <= 0f || Time.time >= _nextWanderMoveAllowedAt;
        }

        private void ScheduleNextWanderMoveAttempt(Vector2 durationRange, float fallback)
        {
            _stateTimer = RandomRange(durationRange, fallback);
            _nextWanderMoveAllowedAt = Time.time + _stateTimer;
        }

        private void BeginTargetSpacingMove()
        {
            _isTargetSpacingMoveActive = true;
            _targetSpacingMoveEndsAt = Time.time + RandomRange(moveDurationRange, 1f);
            _moveStartedAt = Time.time;
            _nextWanderMoveAllowedAt = 0f;
        }

        private void EndTargetSpacingMove()
        {
            if (!_isTargetSpacingMoveActive)
            {
                return;
            }

            _isTargetSpacingMoveActive = false;
            _targetSpacingMoveEndsAt = 0f;
            StartTargetSpacingCooldown();
            if (!_isPaused)
            {
                BeginPause();
            }
        }

        private bool CanStartTargetSpacingMove()
        {
            return targetSpacingCooldown <= 0f || Time.time >= _nextTargetSpacingAllowedAt;
        }

        private void StartTargetSpacingCooldown()
        {
            if (targetSpacingCooldown > 0f)
            {
                _nextTargetSpacingAllowedAt = Time.time + targetSpacingCooldown;
            }
        }

        private bool TryGetTargetSpacingDirection(out Vector2 direction, out bool spacingNeeded)
        {
            direction = Vector2.zero;
            spacingNeeded = false;

            if (!keepDistanceFromTargets || targetLayerMask.value == 0 || targetDetectionRadius <= 0f)
            {
                _hasTargetSpacingDirection = false;
                _targetSpacingNeeded = false;
                return false;
            }

            if (Time.time >= _nextTargetSpacingRefreshTime)
            {
                _nextTargetSpacingRefreshTime = Time.time + targetSpacingRefreshInterval;
                _hasTargetSpacingDirection =
                    TryCalculateTargetSpacingDirection(out _targetSpacingDirection, out _targetSpacingNeeded);
            }

            spacingNeeded = _targetSpacingNeeded;
            if (!_hasTargetSpacingDirection)
            {
                return false;
            }

            direction = _targetSpacingDirection;
            return true;
        }

        private bool TryCalculateTargetSpacingDirection(out Vector2 direction, out bool spacingNeeded)
        {
            direction = Vector2.zero;
            spacingNeeded = false;

            Vector2 currentPosition = transform.position;
            Collider2D[] candidates =
                Physics2D.OverlapCircleAll(currentPosition, targetDetectionRadius, targetLayerMask);
            if (candidates.Length == 0)
            {
                return false;
            }

            float minDistance = Mathf.Max(0f, preferredTargetDistance - targetDistanceTolerance);
            float maxDistance = preferredTargetDistance + targetDistanceTolerance;
            float closestSqrDistance = float.PositiveInfinity;
            Vector2 closestOffset = Vector2.zero;
            Vector2 awayFromCrowd = Vector2.zero;
            bool foundTarget = false;

            for (int i = 0; i < candidates.Length; i++)
            {
                if (!TryResolveTargetPosition(candidates[i], out Vector2 targetPosition))
                {
                    continue;
                }

                Vector2 offset = targetPosition - currentPosition;
                float sqrDistance = offset.sqrMagnitude;
                if (sqrDistance <= MinDirectionSqrMagnitude)
                {
                    awayFromCrowd += RandomDirection();
                    foundTarget = true;
                    continue;
                }

                float distance = Mathf.Sqrt(sqrDistance);
                Vector2 directionToTarget = offset / distance;
                foundTarget = true;

                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestOffset = offset;
                }

                if (minDistance > 0f && distance < minDistance)
                {
                    float weight = (minDistance - distance) / Mathf.Max(minDistance, 0.01f);
                    awayFromCrowd -= directionToTarget * Mathf.Max(weight, 0.25f);
                }
            }

            if (!foundTarget)
            {
                return false;
            }

            Vector2 desiredDirection = Vector2.zero;
            if (awayFromCrowd.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                desiredDirection = awayFromCrowd.normalized;
                spacingNeeded = true;
            }
            else if (closestSqrDistance > maxDistance * maxDistance &&
                     closestOffset.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                desiredDirection = closestOffset.normalized;
                spacingNeeded = true;
            }

            if (!spacingNeeded)
            {
                return false;
            }

            return TryFindUnblockedDirection(desiredDirection, out direction);
        }

        private bool TryFindUnblockedDirection(Vector2 desiredDirection, out Vector2 direction)
        {
            direction = Vector2.zero;
            if (desiredDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                return false;
            }

            Vector2 normalized = desiredDirection.normalized;
            if (!IsBlocked(normalized))
            {
                direction = normalized;
                return true;
            }

            const int maxSideSteps = 6;
            const float angleStep = 15f;
            for (int i = 1; i <= maxSideSteps; i++)
            {
                float angle = angleStep * i;
                Vector2 clockwise = Rotate(normalized, angle);
                if (!IsBlocked(clockwise))
                {
                    direction = clockwise;
                    return true;
                }

                Vector2 counterClockwise = Rotate(normalized, -angle);
                if (!IsBlocked(counterClockwise))
                {
                    direction = counterClockwise;
                    return true;
                }
            }

            return false;
        }

        private bool TryPickDirection(out Vector2 direction)
        {
            Vector2 currentPosition = transform.position;
            Vector2 toOrigin = _origin - currentPosition;
            bool outsideRadius = wanderRadius > 0f && toOrigin.magnitude > wanderRadius;

            for (int i = 0; i < directionAttempts; i++)
            {
                Vector2 candidate = RandomDirection();
                if (outsideRadius && toOrigin.sqrMagnitude > MinDirectionSqrMagnitude)
                {
                    candidate = Vector2.Lerp(candidate, toOrigin.normalized, centerPullWhenOutsideRadius).normalized;
                }

                if (candidate.sqrMagnitude <= MinDirectionSqrMagnitude || IsBlocked(candidate))
                {
                    continue;
                }

                if (!outsideRadius || MovesCloserToOrigin(currentPosition, candidate))
                {
                    direction = candidate;
                    return true;
                }
            }

            if (outsideRadius && toOrigin.sqrMagnitude > MinDirectionSqrMagnitude && !IsBlocked(toOrigin.normalized))
            {
                direction = toOrigin.normalized;
                return true;
            }

            direction = Vector2.zero;
            return false;
        }

        private bool MovesCloserToOrigin(Vector2 currentPosition, Vector2 candidate)
        {
            Vector2 nextPosition = currentPosition + candidate.normalized;
            return Vector2.SqrMagnitude(nextPosition - _origin) < Vector2.SqrMagnitude(currentPosition - _origin);
        }

        private bool IsBlocked(Vector2 direction)
        {
            if (obstacleMask.value == 0 ||
                direction.sqrMagnitude <= MinDirectionSqrMagnitude ||
                obstacleProbeRadius <= 0f)
            {
                return false;
            }

            float distance = Mathf.Max(ResolveMoveSpeed() * Time.deltaTime + obstacleProbeDistance, obstacleProbeDistance);
            return Physics2D.CircleCast(transform.position, obstacleProbeRadius, direction.normalized, distance, obstacleMask).collider != null;
        }

        private float ResolveMoveSpeed()
        {
            return IsPhaseTwoActive() && phaseTwoMoveSpeed > 0f ? phaseTwoMoveSpeed : moveSpeed;
        }

        private string ResolveMoveStateName()
        {
            return IsPhaseTwoActive() && !string.IsNullOrWhiteSpace(phaseTwoMoveStateName)
                ? phaseTwoMoveStateName
                : moveStateName;
        }

        private bool TryReservePhaseTwoStopAnimation(bool allowStopAnimation)
        {
            if (!allowStopAnimation ||
                !IsPhaseTwoActive() ||
                string.IsNullOrWhiteSpace(phaseTwoStopStateName) ||
                phaseTwoStopAnimationChance <= 0f)
            {
                return false;
            }

            if (Time.time < _nextPhaseTwoStopAnimationAllowedAt)
            {
                return false;
            }

            if (phaseTwoStopAnimationMinMoveDuration > 0f &&
                Time.time - _moveStartedAt < phaseTwoStopAnimationMinMoveDuration)
            {
                return false;
            }

            if (phaseTwoStopAnimationChance < 1f && Random.value > phaseTwoStopAnimationChance)
            {
                return false;
            }

            _nextPhaseTwoStopAnimationAllowedAt = Time.time + phaseTwoStopAnimationCooldown;
            return true;
        }

        private bool IsPhaseTwoActive()
        {
            return phaseController != null && phaseController.IsPhaseTwoActive;
        }

        private static bool TryResolveTargetPosition(Collider2D candidate, out Vector2 position)
        {
            position = Vector2.zero;
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
            {
                return false;
            }

            Character character = candidate.GetComponentInParent<Character>();
            if (character != null && character.CharacterType != Character.CharacterTypes.Player)
            {
                return false;
            }

            Health targetHealth = candidate.GetComponentInParent<Health>();
            if (targetHealth != null &&
                (!targetHealth.gameObject.activeInHierarchy || targetHealth.CurrentHealth <= 0f))
            {
                return false;
            }

            Transform targetTransform = character != null
                ? character.transform
                : targetHealth != null
                    ? targetHealth.transform
                    : candidate.transform;
            if (targetTransform == null)
            {
                return false;
            }

            position = targetTransform.position;
            return true;
        }

        private static Vector2 Rotate(Vector2 vector, float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                vector.x * cos - vector.y * sin,
                vector.x * sin + vector.y * cos);
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

            if (_currentAnimationStateName == stateName &&
                animator.GetCurrentAnimatorStateInfo(animationLayer).IsName(stateName))
            {
                return;
            }

            animator.Play(stateName, animationLayer, 0f);
            _currentAnimationStateName = stateName;
        }

        private static Vector2 RandomDirection()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static float RandomRange(Vector2 range, float fallback)
        {
            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);
            if (max <= 0f)
            {
                return fallback;
            }

            return Random.Range(Mathf.Max(0f, min), max);
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[RenaBossWander] " + message, this);
            }
        }
    }
}

using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat.Telegraph
{
    [AddComponentMenu("Lost Memory/Combat/Telegraph/AI Brain Dash Telegraph Driver")]
    public class AIBrainDashTelegraphDriver : MonoBehaviour, MMEventListener<AIStateEvent>
    {
        [SerializeField] private string driverKey = "DashTelegraph";
        [SerializeField] private AIBrain brain;
        [SerializeField] private Character character;
        [SerializeField] private CharacterDash2D dashAbility;
        [SerializeField] private CharacterOrientation2D orientationAbility;
        [SerializeField] private Animator animator;
        [SerializeField] private AIActionDash dashAction;
        [SerializeField] private BoxCollider2D chargeDamageArea;
        [SerializeField] private Transform telegraphOrigin;
        [SerializeField] private AttackTelegraph2DView telegraphView;
        [SerializeField] private bool rotateChargeDamageAreaWithDash = true;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.34f, 0.08f, 0.3f);
        [SerializeField] private float telegraphLengthPadding = 0f;
        [SerializeField] private float telegraphWidthPadding = 0.1f;
        [SerializeField] private float horizontalFacingThreshold = 0.05f;
        [SerializeField] private bool playTelegraphAnimation = true;
        [SerializeField] private string telegraphAnimationStateName = "OrcRider_Blcok";
        [SerializeField] private int telegraphAnimationLayer = 0;
        [SerializeField] private bool playChargeAnimation;
        [SerializeField] private string chargeAnimationStateName = "Dash";
        [SerializeField] private int chargeAnimationLayer;
        [SerializeField] private bool restoreLocomotionAnimation = true;
        [SerializeField] private string idleAnimationStateName = "OrcRider_Idle";
        [SerializeField] private string walkAnimationStateName = "OrcRider_Walk";
        [SerializeField] private string walkingAnimatorParameterName = "Walking";
        [SerializeField] private float locomotionTransitionDuration = 0.05f;
        [SerializeField] private string telegraphStateName = "Telegraph";
        [SerializeField] private string chargeStateName = "Charge";
        [SerializeField] private bool useLastKnownTargetPosition = true;
        // 게스트 측 1회 broadcast clone 의 visual 유지 시간. Telegraph state 평균 체류 길이로 튜닝.
        // 0 이면 MonsterAttackBroadcast.SpawnVisualClone 의 effectiveWarning clamp(0.01)로 한 프레임만 깜빡임.
        [SerializeField, Min(0.05f)] private float telegraphStateExpectedDuration = 1.0f;

        private Vector2 _previewDirection = Vector2.right;
        private float _previewDashDistance = 0.01f;
        private Vector2 _lockedDirection = Vector2.right;
        private float _lockedDashDistance = 0.01f;
        private Quaternion _chargeDamageAreaDefaultLocalRotation = Quaternion.identity;
        private bool _chargeDamageAreaDefaultCached;
        private bool _hasActiveDashPlan;

        public string DriverKey => driverKey;

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
        }

        private void Awake()
        {
            AutoAssignReferences();
            ForceScriptDrivenDash();
        }

        private void OnEnable()
        {
            this.MMEventStartListening<AIStateEvent>();
        }

        private void OnDisable()
        {
            this.MMEventStopListening<AIStateEvent>();
            telegraphView?.Hide();
            RestoreChargeDamageAreaRotationIfNeeded();
            RestoreLocomotionAnimationIfNeeded();
            ClearDashPlan();
        }

        private void Update()
        {
            if (IsDead())
            {
                telegraphView?.Hide();
                RestoreChargeDamageAreaRotationIfActive();
                ClearDashPlan();
                return;
            }

            if (IsInState(telegraphStateName))
            {
                UpdatePreviewFromTarget();
                ApplyPreviewDashPlan();
                ApplyFacing(_previewDirection);

                if (telegraphView != null && telegraphView.IsVisible)
                {
                    RefreshTelegraph();
                }

                return;
            }

            if (IsInState(chargeStateName))
            {
                ApplyLockedDashPlanToDash();
                ApplyFacing(_lockedDirection);
                return;
            }

            if (!ShouldMaintainLock() && _hasActiveDashPlan)
            {
                RestoreChargeDamageAreaRotationIfNeeded();
                RestoreLocomotionAnimationIfNeeded();
                ClearDashPlan();
                return;
            }
        }

        public void OnMMEvent(AIStateEvent stateEvent)
        {
            if (stateEvent.Brain != brain)
            {
                return;
            }

            ForceScriptDrivenDash();

            if (IsDead())
            {
                telegraphView?.Hide();
                RestoreChargeDamageAreaRotationIfActive();
                ClearDashPlan();
                return;
            }

            string enteringState = stateEvent.EnterState != null ? stateEvent.EnterState.StateName : string.Empty;
            string exitingState = stateEvent.ExitState != null ? stateEvent.ExitState.StateName : string.Empty;

            if (enteringState == telegraphStateName)
            {
                _hasActiveDashPlan = true;
                UpdatePreviewFromTarget();
                ApplyPreviewDashPlan();
                ApplyFacing(_previewDirection);
                PlayTelegraphAnimation();
                telegraphView?.Show(BuildTelegraphRequest());
                return;
            }

            if (enteringState == chargeStateName)
            {
                _hasActiveDashPlan = true;
                LockDashPlanFromPreview();
                ApplyLockedDashPlanToDash();
                ApplyFacing(_lockedDirection);
                PlayChargeAnimation();
                telegraphView?.Hide();
                return;
            }

            if (exitingState == telegraphStateName)
            {
                telegraphView?.Hide();
            }

            if (exitingState == chargeStateName)
            {
                RestoreChargeDamageAreaRotationIfNeeded();
            }
        }

        private void AutoAssignReferences()
        {
            brain ??= GetComponent<AIBrain>();
            character ??= GetComponent<Character>();
            dashAbility ??= GetComponent<CharacterDash2D>();
            orientationAbility ??= GetComponent<CharacterOrientation2D>();
            animator ??= ResolveAnimator();
            dashAction ??= GetComponent<AIActionDash>();
            telegraphOrigin ??= transform;
            telegraphView ??= GetComponent<AttackTelegraph2DView>();
            chargeDamageArea ??= FindChargeDamageArea();
            CacheChargeDamageAreaDefaults();
        }

        private void ForceScriptDrivenDash()
        {
            if (dashAbility != null)
            {
                dashAbility.DashMode = CharacterDash2D.DashModes.Script;
            }

            if (dashAction != null)
            {
                dashAction.Mode = AIActionDash.Modes.None;
            }
        }

        private void UpdatePreviewFromTarget()
        {
            Vector2 origin = ResolveOriginPosition();
            Vector2 targetPosition = ResolveTargetPosition(origin);
            Vector2 targetDirection = targetPosition - origin;
            float targetDistance = targetDirection.magnitude;

            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                targetDirection = ResolveFallbackDirection();
                targetDistance = 0.01f;
            }

            _previewDirection = targetDirection.sqrMagnitude > 0.0001f
                ? targetDirection.normalized
                : Vector2.right;
            _previewDashDistance = Mathf.Max(0.01f, targetDistance);
        }

        private void LockDashPlanFromPreview()
        {
            _lockedDirection = _previewDirection.sqrMagnitude > 0.0001f ? _previewDirection.normalized : Vector2.right;
            _lockedDashDistance = Mathf.Max(0.01f, _previewDashDistance);
        }

        private void ApplyPreviewDashPlan()
        {
            ApplyDashPlanToDash(_previewDirection, _previewDashDistance);
        }

        private void ApplyLockedDashPlanToDash()
        {
            ApplyDashPlanToDash(_lockedDirection, _lockedDashDistance);
        }

        private void ApplyDashPlanToDash(Vector2 dashDirection, float dashDistance)
        {
            if (dashAbility == null)
            {
                return;
            }

            Vector2 normalizedDirection = dashDirection.sqrMagnitude > 0.0001f ? dashDirection.normalized : Vector2.right;
            dashAbility.DashMode = CharacterDash2D.DashModes.Script;
            dashAbility.DashDirection = new Vector3(normalizedDirection.x, normalizedDirection.y, 0f);
            dashAbility.DashDistance = Mathf.Max(0.01f, dashDistance);
            RotateChargeDamageAreaToDirection(normalizedDirection);
        }

        private void ApplyFacing(Vector2 facingDirection)
        {
            if (orientationAbility == null)
            {
                return;
            }

            if (Mathf.Abs(facingDirection.x) < horizontalFacingThreshold)
            {
                return;
            }

            orientationAbility.FaceDirection(facingDirection.x >= 0f ? 1 : -1);
        }

        private void PlayTelegraphAnimation()
        {
            if (!playTelegraphAnimation
                || animator == null
                || string.IsNullOrWhiteSpace(telegraphAnimationStateName)
                || IsDead())
            {
                return;
            }

            animator.Play(telegraphAnimationStateName, telegraphAnimationLayer, 0f);
        }

        private void PlayChargeAnimation()
        {
            if (!playChargeAnimation
                || animator == null
                || string.IsNullOrWhiteSpace(chargeAnimationStateName)
                || IsDead())
            {
                return;
            }

            animator.Play(chargeAnimationStateName, chargeAnimationLayer, 0f);
        }

        private void RestoreLocomotionAnimationIfNeeded()
        {
            if (!restoreLocomotionAnimation
                || animator == null
                || string.IsNullOrWhiteSpace(telegraphAnimationStateName)
                || IsDead())
            {
                return;
            }

            if (animator.IsInTransition(telegraphAnimationLayer))
            {
                return;
            }

            if (!animator.GetCurrentAnimatorStateInfo(telegraphAnimationLayer).IsName(telegraphAnimationStateName))
            {
                return;
            }

            string locomotionStateName = ResolveLocomotionStateName();
            if (string.IsNullOrWhiteSpace(locomotionStateName))
            {
                return;
            }

            animator.CrossFadeInFixedTime(locomotionStateName, locomotionTransitionDuration, telegraphAnimationLayer, 0f);
        }

        private string ResolveLocomotionStateName()
        {
            if (animator == null)
            {
                return idleAnimationStateName;
            }

            if (HasAnimatorBoolParameter(walkingAnimatorParameterName) && animator.GetBool(walkingAnimatorParameterName))
            {
                return walkAnimationStateName;
            }

            return idleAnimationStateName;
        }

        private bool HasAnimatorBoolParameter(string parameterName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Bool
                    && parameters[i].name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshTelegraph()
        {
            if (telegraphView == null || dashAbility == null)
            {
                return;
            }

            telegraphView.Refresh(BuildTelegraphRequest());
        }

        private AttackTelegraphRequest2D BuildTelegraphRequest()
        {
            Vector2 direction = IsInState(telegraphStateName)
                ? (_previewDirection.sqrMagnitude > 0.0001f ? _previewDirection.normalized : Vector2.right)
                : (_lockedDirection.sqrMagnitude > 0.0001f ? _lockedDirection.normalized : Vector2.right);
            Vector2 startCenter = ResolveDamageAreaCenter();
            Vector2 colliderWorldSize = ResolveChargeDamageAreaSize();
            Vector2 localRight = chargeDamageArea != null ? (Vector2)chargeDamageArea.transform.right : Vector2.right;
            Vector2 localUp = chargeDamageArea != null ? (Vector2)chargeDamageArea.transform.up : Vector2.up;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);

            float halfExtentAlongDash = ResolveProjectedHalfExtent(colliderWorldSize, localRight, localUp, direction);
            float halfExtentAcrossDash = ResolveProjectedHalfExtent(colliderWorldSize, localRight, localUp, perpendicular);
            float dashDistance = IsInState(telegraphStateName)
                ? Mathf.Max(0.01f, _previewDashDistance)
                : Mathf.Max(0.01f, _lockedDashDistance);

            return new AttackTelegraphRequest2D
            {
                Shape = AttackTelegraphShape2D.Box,
                Center = startCenter + direction * (dashDistance * 0.5f),
                Direction = direction,
                Size = new Vector2(
                    dashDistance + (halfExtentAlongDash * 2f) + telegraphLengthPadding,
                    (halfExtentAcrossDash * 2f) + telegraphWidthPadding),
                Color = telegraphColor,
                Duration = telegraphStateExpectedDuration
            };
        }

        private Vector2 ResolveOriginPosition()
        {
            return telegraphOrigin != null ? telegraphOrigin.position : transform.position;
        }

        private Vector2 ResolveDamageAreaCenter()
        {
            if (chargeDamageArea == null)
            {
                return ResolveOriginPosition();
            }

            return chargeDamageArea.transform.TransformPoint(chargeDamageArea.offset);
        }

        private Vector2 ResolveChargeDamageAreaSize()
        {
            if (chargeDamageArea == null)
            {
                return new Vector2(0.75f, 0.75f);
            }

            Vector3 lossyScale = chargeDamageArea.transform.lossyScale;
            return new Vector2(
                Mathf.Abs(chargeDamageArea.size.x * lossyScale.x),
                Mathf.Abs(chargeDamageArea.size.y * lossyScale.y));
        }

        private Vector2 ResolveTargetPosition(Vector2 origin)
        {
            if (brain != null && brain.Target != null)
            {
                return brain.Target.position;
            }

            if (brain != null && useLastKnownTargetPosition && brain._lastKnownTargetPosition != Vector3.zero)
            {
                return brain._lastKnownTargetPosition;
            }

            return origin + ResolveFallbackDirection();
        }

        private Vector2 ResolveFallbackDirection()
        {
            if (dashAbility != null)
            {
                Vector2 dashDirection = new Vector2(dashAbility.DashDirection.x, dashAbility.DashDirection.y);
                if (dashDirection.sqrMagnitude > 0.0001f)
                {
                    return dashDirection.normalized;
                }
            }

            Vector2 transformRight = transform.right;
            return transformRight.sqrMagnitude > 0.0001f ? transformRight.normalized : Vector2.right;
        }

        private bool IsInState(string stateName)
        {
            return brain != null
                && brain.CurrentState != null
                && brain.CurrentState.StateName == stateName;
        }

        private bool ShouldMaintainLock()
        {
            return IsInState(telegraphStateName) || IsInState(chargeStateName);
        }

        private void CacheChargeDamageAreaDefaults()
        {
            if (chargeDamageArea == null || _chargeDamageAreaDefaultCached)
            {
                return;
            }

            _chargeDamageAreaDefaultLocalRotation = chargeDamageArea.transform.localRotation;
            _chargeDamageAreaDefaultCached = true;
        }

        private void RotateChargeDamageAreaToDirection(Vector2 dashDirection)
        {
            if (!rotateChargeDamageAreaWithDash || chargeDamageArea == null)
            {
                return;
            }

            CacheChargeDamageAreaDefaults();

            if (dashDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(dashDirection.y, dashDirection.x) * Mathf.Rad2Deg;
            chargeDamageArea.transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        private void RestoreChargeDamageAreaRotationIfNeeded()
        {
            if (!rotateChargeDamageAreaWithDash || chargeDamageArea == null)
            {
                return;
            }

            CacheChargeDamageAreaDefaults();
            chargeDamageArea.transform.localRotation = _chargeDamageAreaDefaultLocalRotation;
        }

        private void RestoreChargeDamageAreaRotationIfActive()
        {
            if (_hasActiveDashPlan)
            {
                RestoreChargeDamageAreaRotationIfNeeded();
            }
        }

        private bool IsDead()
        {
            return character != null
                && character.ConditionState != null
                && character.ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead;
        }

        private BoxCollider2D FindChargeDamageArea()
        {
            BoxCollider2D[] colliders = GetComponentsInChildren<BoxCollider2D>(true);
            BoxCollider2D fallback = null;

            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider2D candidate = colliders[i];
                if (candidate == null || candidate.transform == transform)
                {
                    continue;
                }

                if (candidate.isTrigger)
                {
                    return candidate;
                }

                fallback ??= candidate;
            }

            return fallback;
        }

        private static float ResolveProjectedHalfExtent(
            Vector2 colliderWorldSize,
            Vector2 localRight,
            Vector2 localUp,
            Vector2 projectionAxis)
        {
            Vector2 axis = projectionAxis.sqrMagnitude > 0.0001f ? projectionAxis.normalized : Vector2.right;
            Vector2 right = localRight.sqrMagnitude > 0.0001f ? localRight.normalized : Vector2.right;
            Vector2 up = localUp.sqrMagnitude > 0.0001f ? localUp.normalized : Vector2.up;
            float halfWidth = colliderWorldSize.x * 0.5f;
            float halfHeight = colliderWorldSize.y * 0.5f;

            return Mathf.Abs(Vector2.Dot(axis, right)) * halfWidth
                 + Mathf.Abs(Vector2.Dot(axis, up)) * halfHeight;
        }

        public void RefreshReferences()
        {
            AutoAssignReferences();
        }

        public void Configure(
            AIBrain configuredBrain,
            Character configuredCharacter,
            CharacterDash2D configuredDashAbility,
            CharacterOrientation2D configuredOrientationAbility,
            Animator configuredAnimator,
            AIActionDash configuredDashAction,
            BoxCollider2D configuredChargeDamageArea,
            Transform configuredTelegraphOrigin,
            AttackTelegraph2DView configuredTelegraphView,
            string configuredTelegraphStateName,
            string configuredChargeStateName,
            bool configuredPlayTelegraphAnimation,
            bool configuredRestoreLocomotionAnimation,
            string configuredTelegraphAnimationStateName,
            string configuredIdleAnimationStateName,
            string configuredWalkAnimationStateName,
            int configuredTelegraphAnimationLayer = 0,
            bool configuredPlayChargeAnimation = false,
            string configuredChargeAnimationStateName = "Dash",
            int configuredChargeAnimationLayer = 0,
            string configuredDriverKey = null)
        {
            if (!string.IsNullOrWhiteSpace(configuredDriverKey))
            {
                driverKey = configuredDriverKey;
            }

            brain = configuredBrain;
            character = configuredCharacter;
            dashAbility = configuredDashAbility;
            orientationAbility = configuredOrientationAbility;
            animator = configuredAnimator;
            dashAction = configuredDashAction;
            chargeDamageArea = configuredChargeDamageArea;
            telegraphOrigin = configuredTelegraphOrigin;
            telegraphView = configuredTelegraphView;
            telegraphStateName = configuredTelegraphStateName;
            chargeStateName = configuredChargeStateName;
            playTelegraphAnimation = configuredPlayTelegraphAnimation;
            restoreLocomotionAnimation = configuredRestoreLocomotionAnimation;
            telegraphAnimationStateName = configuredTelegraphAnimationStateName;
            idleAnimationStateName = configuredIdleAnimationStateName;
            walkAnimationStateName = configuredWalkAnimationStateName;
            telegraphAnimationLayer = configuredTelegraphAnimationLayer;
            playChargeAnimation = configuredPlayChargeAnimation;
            chargeAnimationStateName = configuredChargeAnimationStateName;
            chargeAnimationLayer = configuredChargeAnimationLayer;
            AutoAssignReferences();
        }

        public void SetAnimationPlayback(bool shouldPlayTelegraphAnimation, bool shouldRestoreLocomotionAnimation)
        {
            playTelegraphAnimation = shouldPlayTelegraphAnimation;
            restoreLocomotionAnimation = shouldRestoreLocomotionAnimation;
        }

        public void SetDriverKey(string configuredDriverKey)
        {
            if (!string.IsNullOrWhiteSpace(configuredDriverKey))
            {
                driverKey = configuredDriverKey;
            }
        }

        private void ClearDashPlan()
        {
            _previewDirection = Vector2.right;
            _previewDashDistance = 0.01f;
            _lockedDirection = Vector2.right;
            _lockedDashDistance = 0.01f;
            _hasActiveDashPlan = false;
        }

        private Animator ResolveAnimator()
        {
            Transform visualRoot = transform.Find("Visual");
            if (visualRoot != null)
            {
                Transform spriteChild = visualRoot.Find("BerthaSprite");
                if (spriteChild != null)
                {
                    Animator spriteAnimator = spriteChild.GetComponent<Animator>();
                    if (spriteAnimator != null)
                    {
                        return spriteAnimator;
                    }
                }

                Animator visualAnimator = visualRoot.GetComponentInChildren<Animator>(true);
                if (visualAnimator != null)
                {
                    return visualAnimator;
                }
            }

            return GetComponentInChildren<Animator>(true);
        }
    }
}

using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat.Telegraph
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Telegraph/AI Brain Dash Telegraph Driver")]
    public class AIBrainDashTelegraphDriver : MonoBehaviour, MMEventListener<AIStateEvent>
    {
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
        [SerializeField] private bool restoreLocomotionAnimation = true;
        [SerializeField] private string idleAnimationStateName = "OrcRider_Idle";
        [SerializeField] private string walkAnimationStateName = "OrcRider_Walk";
        [SerializeField] private string walkingAnimatorParameterName = "Walking";
        [SerializeField] private float locomotionTransitionDuration = 0.05f;
        [SerializeField] private string telegraphStateName = "Telegraph";
        [SerializeField] private string chargeStateName = "Charge";
        [SerializeField] private bool useLastKnownTargetPosition = true;

        private Vector2 _lockedDirection = Vector2.right;
        private Quaternion _chargeDamageAreaDefaultLocalRotation = Quaternion.identity;
        private bool _chargeDamageAreaDefaultCached;

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
        }

        private void Update()
        {
            if (IsDead())
            {
                telegraphView?.Hide();
                RestoreChargeDamageAreaRotationIfNeeded();
                return;
            }

            if (!ShouldMaintainLock())
            {
                RestoreChargeDamageAreaRotationIfNeeded();
                RestoreLocomotionAnimationIfNeeded();
                return;
            }

            ApplyLockedDirectionToDash();
            ApplyLockedFacing();

            if (IsInState(telegraphStateName) && telegraphView != null && telegraphView.IsVisible)
            {
                RefreshTelegraph();
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
                RestoreChargeDamageAreaRotationIfNeeded();
                return;
            }

            string enteringState = stateEvent.EnterState != null ? stateEvent.EnterState.StateName : string.Empty;
            string exitingState = stateEvent.ExitState != null ? stateEvent.ExitState.StateName : string.Empty;

            if (enteringState == telegraphStateName)
            {
                LockDirectionFromTarget();
                ApplyLockedFacing();
                PlayTelegraphAnimation();
                RefreshTelegraph();
                return;
            }

            if (enteringState == chargeStateName)
            {
                ApplyLockedDirectionToDash();
                ApplyLockedFacing();
                telegraphView?.Hide();
                return;
            }

            if (exitingState == telegraphStateName)
            {
                telegraphView?.Hide();
            }
        }

        private void AutoAssignReferences()
        {
            brain ??= GetComponent<AIBrain>();
            character ??= GetComponent<Character>();
            dashAbility ??= GetComponent<CharacterDash2D>();
            orientationAbility ??= GetComponent<CharacterOrientation2D>();
            animator ??= GetComponentInChildren<Animator>();
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

        private void LockDirectionFromTarget()
        {
            Vector2 origin = ResolveOriginPosition();
            Vector2 targetPosition = ResolveTargetPosition(origin);
            Vector2 targetDirection = targetPosition - origin;

            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                targetDirection = ResolveFallbackDirection();
            }

            _lockedDirection = targetDirection.sqrMagnitude > 0.0001f
                ? targetDirection.normalized
                : Vector2.right;

            ApplyLockedDirectionToDash();
        }

        private void ApplyLockedDirectionToDash()
        {
            if (dashAbility == null)
            {
                return;
            }

            dashAbility.DashMode = CharacterDash2D.DashModes.Script;
            dashAbility.DashDirection = new Vector3(_lockedDirection.x, _lockedDirection.y, 0f);
            RotateChargeDamageAreaToLockedDirection();
        }

        private void ApplyLockedFacing()
        {
            if (orientationAbility == null)
            {
                return;
            }

            if (Mathf.Abs(_lockedDirection.x) < horizontalFacingThreshold)
            {
                return;
            }

            orientationAbility.FaceDirection(_lockedDirection.x >= 0f ? 1 : -1);
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
            Vector2 direction = _lockedDirection.sqrMagnitude > 0.0001f ? _lockedDirection.normalized : Vector2.right;
            Vector2 startCenter = ResolveDamageAreaCenter();
            Vector2 colliderWorldSize = ResolveChargeDamageAreaSize();
            Vector2 localRight = chargeDamageArea != null ? (Vector2)chargeDamageArea.transform.right : Vector2.right;
            Vector2 localUp = chargeDamageArea != null ? (Vector2)chargeDamageArea.transform.up : Vector2.up;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);

            float halfExtentAlongDash = ResolveProjectedHalfExtent(colliderWorldSize, localRight, localUp, direction);
            float halfExtentAcrossDash = ResolveProjectedHalfExtent(colliderWorldSize, localRight, localUp, perpendicular);
            float dashDistance = Mathf.Max(0.01f, dashAbility.DashDistance);

            return new AttackTelegraphRequest2D
            {
                Shape = AttackTelegraphShape2D.Box,
                Center = startCenter + direction * (dashDistance * 0.5f),
                Direction = direction,
                Size = new Vector2(
                    dashDistance + (halfExtentAlongDash * 2f) + telegraphLengthPadding,
                    (halfExtentAcrossDash * 2f) + telegraphWidthPadding),
                Color = telegraphColor,
                Duration = 0f
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

        private void RotateChargeDamageAreaToLockedDirection()
        {
            if (!rotateChargeDamageAreaWithDash || chargeDamageArea == null)
            {
                return;
            }

            CacheChargeDamageAreaDefaults();

            if (_lockedDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(_lockedDirection.y, _lockedDirection.x) * Mathf.Rad2Deg;
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
    }
}

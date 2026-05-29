using System;
using System.Collections;
using System.Collections.Generic;
using LostMemory.Rendering;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostMemory.Combat.Telegraph
{
    [AddComponentMenu("Lost Memory/Combat/Telegraph/Telegraphed Area Attack Controller")]
    public class TelegraphedAreaAttackController : MonoBehaviour,
        MMEventListener<AIStateEvent>,
        ICancelableEnemyAttack
    {
        [SerializeField] private AIBrain brain;
        [SerializeField] private Character character;
        [SerializeField] private CharacterMovement movementAbility;
        [SerializeField] private CharacterOrientation2D orientationAbility;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform telegraphOrigin;
        [SerializeField] private AttackTelegraph2DView telegraphView;
        [SerializeField] private string telegraphStateName = "AttackTelegraph";
        [SerializeField] private string attackStateName = "Attack";
        [SerializeField] private string recoverStateName = "Recover";
        [SerializeField] private bool transitionToCancelStateOnCancel = true;
        [SerializeField] private string cancelStateName = "Detecting";
        [SerializeField] private string attackAnimationStateName = "Attack";
        [SerializeField, Min(0)] private int attackAnimationLayer;
        [SerializeField] private float impactTime = -1f;
        [SerializeField] private bool hideTelegraphOnAttackStart;
        [SerializeField, Min(0f)] private float telegraphHideLeadTime = 0.03f;
        [SerializeField] private bool lockMovementDuringAttackSequence = true;
        [SerializeField] private bool lockMovementDuringRecover = true;
        [SerializeField] private bool restoreMovementForbiddenStateOnUnlock;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.34f, 0.08f, 0.32f);
        [SerializeField] private AttackTelegraphShape2D attackShape = AttackTelegraphShape2D.Box;
        [SerializeField] private Vector2 attackOffset = Vector2.zero;
        [SerializeField] private Vector2 attackSize = new Vector2(1f, 1f);
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField, Min(1)] private int maximumHits = 8;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float targetInvincibilityDuration = 0.5f;
        [SerializeField] private float horizontalFacingThreshold = 0.05f;
        [SerializeField] private bool lockAttackDirectionToTarget = true;
        [SerializeField] private bool limitAttackDirectionToHorizontal;
        [SerializeField] private bool driveAnimatorDirectionParameters = true;
        [SerializeField] private bool applyModelFacingImmediately = true;
        [SerializeField] private bool followUpAreaAttackEnabled;
        [SerializeField, Min(0f)] private float followUpAreaDelay = 0.1f;
        [SerializeField, Min(0f)] private float followUpAreaTelegraphDuration = 0.6f;
        [SerializeField] private Color followUpAreaTelegraphColor = new Color(1f, 0.12f, 0.05f, 0.38f);
        [SerializeField] private Vector2 followUpAreaOffset = Vector2.zero;
        [SerializeField] private AttackTelegraphShape2D followUpAreaShape = AttackTelegraphShape2D.Box;
        [SerializeField] private Vector2 followUpAreaSize = new Vector2(2f, 2f);
        [SerializeField] private float followUpAreaDamage = 10f;
        [SerializeField] private float followUpAreaTargetInvincibilityDuration = 0.5f;
        [SerializeField] private Sprite[] followUpAreaImpactSprites = System.Array.Empty<Sprite>();
        [SerializeField, Min(0.01f)] private float followUpAreaImpactFrameRate = 16f;
        [SerializeField] private Vector2 followUpAreaImpactSize = Vector2.zero;
        [SerializeField] private Color followUpAreaImpactColor = Color.white;
        [SerializeField] private Vector3 followUpAreaImpactOffset = Vector3.zero;
        [SerializeField] private Transform followUpAreaImpactParent;
        [SerializeField] private SortingGroup followUpAreaImpactSortingGroupReference;
        [SerializeField] private int followUpAreaImpactSortingOrderOffset = 1;
        [SerializeField] private string debugName = "TelegraphedAreaAttack";
        [SerializeField] private bool debugLogging;

        private readonly HashSet<Health> _hitTargetsThisAttack = new HashSet<Health>();
        private readonly HashSet<Health> _hitTargetsThisFollowUpArea = new HashSet<Health>();
        private readonly List<GameObject> _activeFollowUpImpactEffects = new List<GameObject>();
        private Collider2D[] _overlapBuffer;
        private Vector2 _lockedDirection = Vector2.right;
        private Vector2 _lockedCenter;
        private Vector2 _followUpAreaCenter;
        private float _attackStateElapsed;
        private float _followUpAreaElapsed;
        private bool _hasLockedAttack;
        private bool _hasExecutedImpact;
        private bool _hasPendingAttackDamage;
        private bool _telegraphHiddenForImpact;
        private bool _hasPendingFollowUpAreaAttack;
        private bool _followUpAreaTelegraphShown;
        private bool _movementLockedByAttack;
        private bool _movementForbiddenBeforeLock;

        public string TelegraphStateName => telegraphStateName;
        public string AttackStateName => attackStateName;
        public Vector2 CurrentAttackCenter => _lockedCenter;
        public Vector2 CurrentAttackSize => attackSize;

        // Keep monster-specific aiming and visual behavior in companion components.
        // Examples: TrackingTelegraphedMeleeAttackController handles Moose-style retargeting,
        // and TelegraphedAttackAreaEffectPlayer/AreaAttackEffectPlayer handle impact visuals.
        public bool CanCancelAttack =>
            _hasLockedAttack
            || _hasPendingAttackDamage
            || _hasPendingFollowUpAreaAttack
            || IsInState(telegraphStateName)
            || IsInState(attackStateName);

        public event Action<TelegraphedAreaAttackController, Vector2, Vector2> PrimaryImpactExecuted;

        private static readonly int FacingDirection2DAnimatorParameter = Animator.StringToHash("FacingDirection2D");
        private static readonly int HorizontalDirectionAnimatorParameter = Animator.StringToHash("HorizontalDirection");
        private static readonly int VerticalDirectionAnimatorParameter = Animator.StringToHash("VerticalDirection");

        private void Reset()
        {
            RefreshReferences();
        }

        private void OnValidate()
        {
            maximumHits = Mathf.Max(1, maximumHits);
            RefreshReferences();
            EnsureBuffer();
        }

        private void Awake()
        {
            RefreshReferences();
            EnsureBuffer();
        }

        private void OnEnable()
        {
            this.MMEventStartListening<AIStateEvent>();
        }

        private void OnDisable()
        {
            this.MMEventStopListening<AIStateEvent>();
            telegraphView?.Hide();
            ClearFollowUpAreaAttack(false);
            ClearFollowUpAreaImpactEffects();
            ReleaseMovementLock();
            ClearLockedAttack();
        }

        private void OnDestroy()
        {
            ClearFollowUpAreaImpactEffects();
        }

        private void Update()
        {
            if (IsDead())
            {
                telegraphView?.Hide();
                ClearFollowUpAreaAttack(false);
                ReleaseMovementLock();
                ClearLockedAttack();
                return;
            }

            UpdateMovementLock();

            if (_hasPendingAttackDamage && !_hasExecutedImpact && impactTime >= 0f)
            {
                _attackStateElapsed += Time.deltaTime;
                HideTelegraphBeforeImpactIfNeeded();
                if (_attackStateElapsed >= impactTime)
                {
                    ExecuteImpactNow();
                }
            }

            UpdateFollowUpAreaAttack();

            if (telegraphView == null || !_hasLockedAttack)
            {
                return;
            }

            if (IsInState(telegraphStateName)
                || (IsInState(attackStateName) && !hideTelegraphOnAttackStart && !_telegraphHiddenForImpact))
            {
                telegraphView.Refresh(BuildTelegraphRequest());
            }
        }

        private void LateUpdate()
        {
            if (!_hasLockedAttack || IsDead())
            {
                return;
            }

            if (IsInState(telegraphStateName)
                || IsInState(attackStateName)
                || (lockMovementDuringRecover && IsInState(recoverStateName)))
            {
                ApplyLockedFacing();
            }
        }

        public void OnMMEvent(AIStateEvent stateEvent)
        {
            if (stateEvent.Brain != brain)
            {
                return;
            }

            if (IsDead())
            {
                telegraphView?.Hide();
                ClearFollowUpAreaAttack(false);
                ReleaseMovementLock();
                ClearLockedAttack();
                return;
            }

            string enteringState = stateEvent.EnterState != null ? stateEvent.EnterState.StateName : string.Empty;
            string exitingState = stateEvent.ExitState != null ? stateEvent.ExitState.StateName : string.Empty;

            if (IsMovementLockStateName(enteringState))
            {
                ApplyMovementLock();
            }

            if (enteringState == telegraphStateName)
            {
                ClearFollowUpAreaAttack(false);
                LockAttackFromFacing();
                ApplyLockedFacing();
                telegraphView?.Show(BuildTelegraphRequest());
                return;
            }

            if (enteringState == attackStateName)
            {
                if (hideTelegraphOnAttackStart)
                {
                    HideTelegraphForImpact();
                }
                else if (_hasLockedAttack)
                {
                    telegraphView?.Refresh(BuildTelegraphRequest());
                }

                ApplyLockedFacing();
                PlayAttackAnimation();
                QueueAttackDamage();
                _attackStateElapsed = 0f;
                _hasExecutedImpact = false;

                if (impactTime <= 0f && impactTime >= 0f)
                {
                    ExecuteImpactNow();
                }

                return;
            }

            if (exitingState == telegraphStateName)
            {
                telegraphView?.Hide();
            }

            if (exitingState == attackStateName)
            {
                ExecuteQueuedAttackDamage();
                ClearLockedAttack();
            }

            if (IsMovementLockStateName(exitingState) && !IsMovementLockStateName(enteringState))
            {
                ReleaseMovementLock();
            }
        }

        public void RefreshReferences()
        {
            brain ??= GetComponent<AIBrain>();
            character ??= GetComponent<Character>();
            movementAbility ??= GetComponent<CharacterMovement>();
            orientationAbility ??= GetComponent<CharacterOrientation2D>();
            telegraphView ??= GetComponent<AttackTelegraph2DView>();
            telegraphOrigin ??= transform;
            animator ??= ResolveAnimator();
            followUpAreaImpactSortingGroupReference ??= GetComponentInParent<SortingGroup>();
        }

        public void Configure(
            AIBrain configuredBrain,
            Character configuredCharacter,
            CharacterMovement configuredMovement,
            CharacterOrientation2D configuredOrientation,
            Animator configuredAnimator,
            Transform configuredTelegraphOrigin,
            AttackTelegraph2DView configuredTelegraphView,
            string configuredTelegraphStateName,
            string configuredAttackStateName,
            string configuredAttackAnimationStateName,
            int configuredAttackAnimationLayer,
            Color configuredTelegraphColor,
            LayerMask configuredTargetLayerMask,
            Vector2 configuredAttackOffset,
            Vector2 configuredAttackSize,
            float configuredDamage,
            float configuredTargetInvincibilityDuration,
            string configuredDebugName = null)
        {
            Configure(
                configuredBrain,
                configuredCharacter,
                configuredMovement,
                configuredOrientation,
                configuredAnimator,
                configuredTelegraphOrigin,
                configuredTelegraphView,
                configuredTelegraphStateName,
                configuredAttackStateName,
                configuredAttackAnimationStateName,
                configuredAttackAnimationLayer,
                configuredTelegraphColor,
                configuredTargetLayerMask,
                configuredAttackOffset,
                configuredAttackSize,
                configuredDamage,
                configuredTargetInvincibilityDuration,
                -1f,
                configuredDebugName);
        }

        public void Configure(
            AIBrain configuredBrain,
            Character configuredCharacter,
            CharacterMovement configuredMovement,
            CharacterOrientation2D configuredOrientation,
            Animator configuredAnimator,
            Transform configuredTelegraphOrigin,
            AttackTelegraph2DView configuredTelegraphView,
            string configuredTelegraphStateName,
            string configuredAttackStateName,
            string configuredAttackAnimationStateName,
            int configuredAttackAnimationLayer,
            Color configuredTelegraphColor,
            LayerMask configuredTargetLayerMask,
            Vector2 configuredAttackOffset,
            Vector2 configuredAttackSize,
            float configuredDamage,
            float configuredTargetInvincibilityDuration,
            float configuredImpactTime,
            string configuredDebugName = null)
        {
            brain = configuredBrain;
            character = configuredCharacter;
            movementAbility = configuredMovement;
            orientationAbility = configuredOrientation;
            animator = configuredAnimator;
            telegraphOrigin = configuredTelegraphOrigin;
            telegraphView = configuredTelegraphView;
            telegraphStateName = configuredTelegraphStateName;
            attackStateName = configuredAttackStateName;
            attackAnimationStateName = configuredAttackAnimationStateName;
            attackAnimationLayer = configuredAttackAnimationLayer;
            telegraphColor = configuredTelegraphColor;
            targetLayerMask = configuredTargetLayerMask;
            attackOffset = configuredAttackOffset;
            attackSize = configuredAttackSize;
            damage = configuredDamage;
            targetInvincibilityDuration = configuredTargetInvincibilityDuration;
            SetImpactTime(configuredImpactTime);
            if (!string.IsNullOrWhiteSpace(configuredDebugName))
            {
                debugName = configuredDebugName;
            }

            RefreshReferences();
            EnsureBuffer();
        }

        public void SetImpactTime(float configuredImpactTime)
        {
            impactTime = configuredImpactTime;
        }

        public void SetDamageValues(float configuredDamage, float configuredFollowUpAreaDamage)
        {
            damage = Mathf.Max(0f, configuredDamage);
            followUpAreaDamage = Mathf.Max(0f, configuredFollowUpAreaDamage);
        }

        public void CancelAttack()
        {
            if (!CanCancelAttack)
            {
                return;
            }

            telegraphView?.Hide();
            ClearFollowUpAreaAttack(false);
            ClearLockedAttack();

            if (transitionToCancelStateOnCancel
                && brain != null
                && !string.IsNullOrWhiteSpace(cancelStateName)
                && !IsInState(cancelStateName))
            {
                brain.TransitionToState(cancelStateName);
            }

            ReleaseMovementLock();
            Log("Canceled attack.");
        }

        private Animator ResolveAnimator()
        {
            Animator ownAnimator = GetComponent<Animator>();
            if (ownAnimator != null)
            {
                return ownAnimator;
            }

            Transform visualChild = transform.Find("Visual");
            if (visualChild != null)
            {
                Animator visualAnimator = visualChild.GetComponentInChildren<Animator>(true);
                if (visualAnimator != null)
                {
                    return visualAnimator;
                }
            }

            return GetComponentInChildren<Animator>(true);
        }

        private void EnsureBuffer()
        {
            if (_overlapBuffer == null || _overlapBuffer.Length != maximumHits)
            {
                _overlapBuffer = new Collider2D[Mathf.Max(1, maximumHits)];
            }
        }

        private void UpdateMovementLock()
        {
            if (!lockMovementDuringAttackSequence)
            {
                ReleaseMovementLock();
                return;
            }

            if (IsInMovementLockState())
            {
                ApplyMovementLock();
                return;
            }

            ReleaseMovementLock();
        }

        private void ApplyMovementLock()
        {
            movementAbility ??= GetComponent<CharacterMovement>();
            if (movementAbility == null)
            {
                return;
            }

            if (!_movementLockedByAttack)
            {
                _movementForbiddenBeforeLock = movementAbility.MovementForbidden;
                _movementLockedByAttack = true;
            }

            movementAbility.SetMovement(Vector2.zero);
            movementAbility.MovementForbidden = true;
        }

        private void ReleaseMovementLock()
        {
            if (!_movementLockedByAttack || movementAbility == null)
            {
                _movementLockedByAttack = false;
                _movementForbiddenBeforeLock = false;
                return;
            }

            movementAbility.MovementForbidden = restoreMovementForbiddenStateOnUnlock
                && _movementForbiddenBeforeLock;
            _movementLockedByAttack = false;
            _movementForbiddenBeforeLock = false;
        }

        private bool IsInMovementLockState()
        {
            return IsInState(telegraphStateName)
                || IsInState(attackStateName)
                || (lockMovementDuringRecover && IsInState(recoverStateName));
        }

        private bool IsMovementLockStateName(string stateName)
        {
            if (!lockMovementDuringAttackSequence || string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            return stateName == telegraphStateName
                || stateName == attackStateName
                || (lockMovementDuringRecover && stateName == recoverStateName);
        }

        private void LockAttackFromFacing()
        {
            Vector2 origin = ResolveOriginPosition();
            _lockedDirection = ResolveAttackDirection(origin);
            float angle = Mathf.Atan2(_lockedDirection.y, _lockedDirection.x) * Mathf.Rad2Deg;
            _lockedCenter = origin + (Vector2)(Quaternion.Euler(0f, 0f, angle) * (Vector3)attackOffset);
            _hasLockedAttack = true;
        }

        public void TrackLockedAttackToTarget(
            bool trackTargetX,
            bool trackTargetY,
            bool trackAttackDirection)
        {
            if ((!trackTargetX && !trackTargetY && !trackAttackDirection)
                || brain == null
                || brain.Target == null)
            {
                return;
            }

            Vector2 origin = ResolveOriginPosition();
            Vector2 targetPosition = brain.Target.transform.position;

            if (trackAttackDirection)
            {
                _lockedDirection = ResolveAttackDirection(origin);
                float angle = Mathf.Atan2(_lockedDirection.y, _lockedDirection.x) * Mathf.Rad2Deg;
                Vector2 trackedCenter = origin + (Vector2)(Quaternion.Euler(0f, 0f, angle) * (Vector3)attackOffset);
                if (!trackTargetX)
                {
                    _lockedCenter.x = trackedCenter.x;
                }

                if (!trackTargetY)
                {
                    _lockedCenter.y = trackedCenter.y;
                }
            }

            if (trackTargetX)
            {
                _lockedCenter.x = targetPosition.x;
            }

            if (trackTargetY)
            {
                _lockedCenter.y = targetPosition.y + attackOffset.y;
            }
        }

        private void ApplyLockedFacing()
        {
            Vector2 direction = _lockedDirection.sqrMagnitude > 0.0001f ? _lockedDirection.normalized : Vector2.right;

            if (orientationAbility != null)
            {
                Character.FacingDirections facingDirection = ResolveFacingDirectionFromVector(direction);
                orientationAbility.Face(facingDirection);

                if (Mathf.Abs(direction.x) >= horizontalFacingThreshold)
                {
                    orientationAbility.FaceDirection(direction.x >= 0f ? 1 : -1);
                }
            }

            ApplyImmediateModelFacing(direction);
            ApplyLockedAnimatorDirectionParameters(direction);
        }

        private void PlayAttackAnimation()
        {
            if (animator == null || string.IsNullOrWhiteSpace(attackAnimationStateName))
            {
                return;
            }

            animator.Play(attackAnimationStateName, attackAnimationLayer, 0f);
        }

        private void ExecuteAttack()
        {
            EnsureBuffer();
            _hitTargetsThisAttack.Clear();

            if (!_hasLockedAttack)
            {
                LockAttackFromFacing();
            }

            float angle = Mathf.Atan2(_lockedDirection.y, _lockedDirection.x) * Mathf.Rad2Deg;
            int hitCount = OverlapAttackAreaNonAlloc(_lockedCenter, attackSize, angle, attackShape);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = _overlapBuffer[i];
                if (hitCollider == null)
                {
                    continue;
                }

                Health health = hitCollider.GetComponentInParent<Health>();
                if (health == null
                    || _hitTargetsThisAttack.Contains(health)
                    || IsOwnedByAttacker(health, gameObject)
                    || !health.CanTakeDamageThisFrame())
                {
                    continue;
                }

                _hitTargetsThisAttack.Add(health);
                health.Damage(
                    damage,
                    gameObject,
                    targetInvincibilityDuration,
                    targetInvincibilityDuration,
                    new Vector3(_lockedDirection.x, _lockedDirection.y, 0f));
            }

            Log("Executed " + attackStateName + ".");
        }

        private void QueueAttackDamage()
        {
            _hasPendingAttackDamage = true;
        }

        private void ExecuteImpactNow()
        {
            if (!_hasPendingAttackDamage || _hasExecutedImpact)
            {
                return;
            }

            HideTelegraphForImpact();
            PrimaryImpactExecuted?.Invoke(this, _lockedCenter, attackSize);
            _hasExecutedImpact = true;
            ExecuteQueuedAttackDamage();
            QueueFollowUpAreaAttack();
        }

        private void HideTelegraphBeforeImpactIfNeeded()
        {
            if (hideTelegraphOnAttackStart || _telegraphHiddenForImpact || impactTime < 0f)
            {
                return;
            }

            float hideTime = Mathf.Max(0f, impactTime - telegraphHideLeadTime);
            if (_attackStateElapsed >= hideTime)
            {
                HideTelegraphForImpact();
            }
        }

        private void HideTelegraphForImpact()
        {
            if (_telegraphHiddenForImpact)
            {
                return;
            }

            _telegraphHiddenForImpact = true;
            telegraphView?.Hide();
        }

        private void ExecuteQueuedAttackDamage()
        {
            if (!_hasPendingAttackDamage)
            {
                return;
            }

            _hasPendingAttackDamage = false;

            if (IsDead())
            {
                return;
            }

            ExecuteAttack();
        }

        private void QueueFollowUpAreaAttack()
        {
            if (!followUpAreaAttackEnabled || IsDead())
            {
                return;
            }

            _followUpAreaCenter = ResolveFollowUpAreaCenter();
            _followUpAreaElapsed = 0f;
            _hasPendingFollowUpAreaAttack = true;
            _followUpAreaTelegraphShown = false;
            _hitTargetsThisFollowUpArea.Clear();
        }

        private void UpdateFollowUpAreaAttack()
        {
            if (!_hasPendingFollowUpAreaAttack)
            {
                return;
            }

            _followUpAreaElapsed += Time.deltaTime;

            if (_followUpAreaElapsed < followUpAreaDelay)
            {
                return;
            }

            if (!_followUpAreaTelegraphShown)
            {
                _followUpAreaTelegraphShown = true;
                telegraphView?.Show(BuildFollowUpAreaTelegraphRequest());
            }
            else
            {
                telegraphView?.Refresh(BuildFollowUpAreaTelegraphRequest());
            }

            if (_followUpAreaElapsed >= followUpAreaDelay + followUpAreaTelegraphDuration)
            {
                ExecuteFollowUpAreaAttack();
            }
        }

        private void ExecuteFollowUpAreaAttack()
        {
            if (!_hasPendingFollowUpAreaAttack)
            {
                return;
            }

            telegraphView?.Hide();
            PlayFollowUpAreaImpactEffect();
            EnsureBuffer();
            _hitTargetsThisFollowUpArea.Clear();

            int hitCount = OverlapAttackAreaNonAlloc(_followUpAreaCenter, followUpAreaSize, 0f, followUpAreaShape);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = _overlapBuffer[i];
                if (hitCollider == null)
                {
                    continue;
                }

                Health health = hitCollider.GetComponentInParent<Health>();
                if (health == null
                    || _hitTargetsThisFollowUpArea.Contains(health)
                    || IsOwnedByAttacker(health, gameObject)
                    || !health.CanTakeDamageThisFrame())
                {
                    continue;
                }

                _hitTargetsThisFollowUpArea.Add(health);
                Vector2 knockbackDirection = ResolveFollowUpAreaKnockbackDirection(hitCollider);
                health.Damage(
                    followUpAreaDamage,
                    gameObject,
                    followUpAreaTargetInvincibilityDuration,
                    followUpAreaTargetInvincibilityDuration,
                    new Vector3(knockbackDirection.x, knockbackDirection.y, 0f));
            }

            Log("Executed follow-up area attack.");
            ClearFollowUpAreaAttack(false);
        }

        private void PlayFollowUpAreaImpactEffect()
        {
            Sprite firstSprite = ResolveFirstImpactSprite();
            if (firstSprite == null)
            {
                return;
            }

            GameObject effectObject = CreateAreaEffectObject(
                "FollowUpAreaImpactEffect",
                firstSprite,
                _followUpAreaCenter,
                ResolveFollowUpAreaImpactSize());
            if (effectObject == null)
            {
                return;
            }

            Transform effectTransform = effectObject.transform;
            SpriteRenderer spriteRenderer = effectObject.GetComponent<SpriteRenderer>();
            _activeFollowUpImpactEffects.Add(effectObject);
            StartCoroutine(PlayAreaImpactSequence(
                effectObject,
                effectTransform,
                spriteRenderer,
                ResolveFollowUpAreaImpactSize()));
        }

        private GameObject CreateAreaEffectObject(
            string objectName,
            Sprite firstSprite,
            Vector2 center,
            Vector2 areaSize)
        {
            if (firstSprite == null)
            {
                return null;
            }

            GameObject effectObject = new GameObject(objectName);
            effectObject.layer = gameObject.layer;

            Transform effectTransform = effectObject.transform;
            effectTransform.SetParent(ResolveFollowUpAreaImpactParent(), false);
            effectTransform.position = (Vector3)center + followUpAreaImpactOffset;

            SpriteRenderer spriteRenderer = effectObject.AddComponent<SpriteRenderer>();
            RuntimeSpriteMaterialUtility.ApplySpriteMaterial(spriteRenderer);
            spriteRenderer.sprite = firstSprite;
            spriteRenderer.color = followUpAreaImpactColor;

            ApplyFollowUpAreaImpactSorting(spriteRenderer);
            effectTransform.localScale = ResolveAreaImpactScale(firstSprite, effectTransform.parent, areaSize);
            return effectObject;
        }

        private IEnumerator PlayAreaImpactSequence(
            GameObject effectObject,
            Transform effectTransform,
            SpriteRenderer spriteRenderer,
            Vector2 areaSize)
        {
            float frameDuration = 1f / Mathf.Max(0.01f, followUpAreaImpactFrameRate);

            for (int i = 0; i < followUpAreaImpactSprites.Length; i++)
            {
                Sprite frame = followUpAreaImpactSprites[i];
                if (frame == null)
                {
                    continue;
                }

                if (effectObject == null || spriteRenderer == null)
                {
                    yield break;
                }

                spriteRenderer.sprite = frame;
                effectTransform.localScale = ResolveAreaImpactScale(frame, effectTransform.parent, areaSize);
                yield return new WaitForSeconds(frameDuration);
            }

            if (effectObject != null)
            {
                _activeFollowUpImpactEffects.Remove(effectObject);
                Destroy(effectObject);
            }
        }

        private Sprite ResolveFirstImpactSprite()
        {
            if (followUpAreaImpactSprites == null)
            {
                return null;
            }

            for (int i = 0; i < followUpAreaImpactSprites.Length; i++)
            {
                if (followUpAreaImpactSprites[i] != null)
                {
                    return followUpAreaImpactSprites[i];
                }
            }

            return null;
        }

        private Transform ResolveFollowUpAreaImpactParent()
        {
            if (followUpAreaImpactParent != null)
            {
                return followUpAreaImpactParent;
            }

            SortingGroup sortingGroup = ResolveFollowUpAreaImpactSortingGroup();
            if (sortingGroup != null)
            {
                return sortingGroup.transform.parent;
            }

            return transform.parent;
        }

        private void ApplyFollowUpAreaImpactSorting(SpriteRenderer spriteRenderer)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (telegraphView != null
                && telegraphView.TryGetRenderSorting(out int telegraphSortingLayerId, out int telegraphSortingOrder))
            {
                spriteRenderer.sortingLayerID = telegraphSortingLayerId;
                spriteRenderer.sortingOrder = telegraphSortingOrder + Mathf.Max(1, followUpAreaImpactSortingOrderOffset);
                return;
            }

            SortingGroup sortingGroup = ResolveFollowUpAreaImpactSortingGroup();
            if (sortingGroup != null)
            {
                spriteRenderer.sortingLayerID = sortingGroup.sortingLayerID;
                spriteRenderer.sortingOrder = sortingGroup.sortingOrder + followUpAreaImpactSortingOrderOffset;
                return;
            }

            SpriteRenderer referenceRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (referenceRenderer != null)
            {
                spriteRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
                spriteRenderer.sortingOrder = referenceRenderer.sortingOrder + followUpAreaImpactSortingOrderOffset;
                return;
            }

            spriteRenderer.sortingOrder = followUpAreaImpactSortingOrderOffset;
        }

        private SortingGroup ResolveFollowUpAreaImpactSortingGroup()
        {
            if (followUpAreaImpactSortingGroupReference != null)
            {
                return followUpAreaImpactSortingGroupReference;
            }

            if (telegraphOrigin != null)
            {
                SortingGroup group = telegraphOrigin.GetComponentInParent<SortingGroup>();
                if (group != null)
                {
                    followUpAreaImpactSortingGroupReference = group;
                    return group;
                }
            }

            followUpAreaImpactSortingGroupReference = GetComponentInParent<SortingGroup>();
            return followUpAreaImpactSortingGroupReference;
        }

        private Vector2 ResolveFollowUpAreaImpactSize()
        {
            return followUpAreaImpactSize.sqrMagnitude > 0.0001f
                ? followUpAreaImpactSize
                : followUpAreaSize;
        }

        private Vector3 ResolveAreaImpactScale(Sprite sprite, Transform effectParent, Vector2 targetSize)
        {
            if (sprite == null)
            {
                return Vector3.one;
            }

            Vector2 spriteSize = sprite.bounds.size;
            float safeSpriteWidth = Mathf.Abs(spriteSize.x) > 0.0001f ? Mathf.Abs(spriteSize.x) : 1f;
            float safeSpriteHeight = Mathf.Abs(spriteSize.y) > 0.0001f ? Mathf.Abs(spriteSize.y) : 1f;

            Vector3 parentScale = effectParent != null ? effectParent.lossyScale : Vector3.one;
            float safeParentX = Mathf.Abs(parentScale.x) > 0.0001f ? Mathf.Abs(parentScale.x) : 1f;
            float safeParentY = Mathf.Abs(parentScale.y) > 0.0001f ? Mathf.Abs(parentScale.y) : 1f;

            return new Vector3(
                targetSize.x / safeSpriteWidth / safeParentX,
                targetSize.y / safeSpriteHeight / safeParentY,
                1f);
        }

        private void ClearFollowUpAreaImpactEffects()
        {
            for (int i = _activeFollowUpImpactEffects.Count - 1; i >= 0; i--)
            {
                if (_activeFollowUpImpactEffects[i] != null)
                {
                    Destroy(_activeFollowUpImpactEffects[i]);
                }
            }

            _activeFollowUpImpactEffects.Clear();
        }

        private AttackTelegraphRequest2D BuildTelegraphRequest()
        {
            Vector2 direction = _lockedDirection.sqrMagnitude > 0.0001f ? _lockedDirection.normalized : Vector2.right;
            return new AttackTelegraphRequest2D
            {
                Shape = attackShape,
                Center = _lockedCenter,
                Direction = direction,
                Size = ResolveShapeDisplaySize(attackSize, attackShape),
                Color = telegraphColor,
                // 게스트 측 1회 broadcast clone 의 visual 유지 시간 = warning → impact 까지 시간.
                // impactTime < 0 (impact 미사용) 이면 fallback 1.0s. 0 이면 한 프레임만 깜빡이는 버그 방지.
                Duration = impactTime > 0f ? impactTime : 1.0f
            };
        }

        private AttackTelegraphRequest2D BuildFollowUpAreaTelegraphRequest()
        {
            return new AttackTelegraphRequest2D
            {
                Shape = followUpAreaShape,
                Center = _followUpAreaCenter,
                Direction = Vector2.right,
                Size = ResolveShapeDisplaySize(followUpAreaSize, followUpAreaShape),
                Color = followUpAreaTelegraphColor,
                // 기존 inspector 필드 followUpAreaTelegraphDuration 재활용 (기본값 0.6s).
                Duration = followUpAreaTelegraphDuration > 0f ? followUpAreaTelegraphDuration : 1.0f
            };
        }

        private int OverlapAttackAreaNonAlloc(
            Vector2 center,
            Vector2 size,
            float angle,
            AttackTelegraphShape2D shape)
        {
            if (shape == AttackTelegraphShape2D.Circle)
            {
                return Physics2D.OverlapCircle(
                    center,
                    ResolveCircleRadius(size),
                    BuildTargetContactFilter(),
                    _overlapBuffer);
            }

            return Physics2D.OverlapBox(
                center,
                size,
                angle,
                BuildTargetContactFilter(),
                _overlapBuffer);
        }

        private ContactFilter2D BuildTargetContactFilter()
        {
            ContactFilter2D contactFilter = new ContactFilter2D();
            contactFilter.SetLayerMask(targetLayerMask);
            contactFilter.useTriggers = true;
            return contactFilter;
        }

        private static float ResolveCircleRadius(Vector2 size)
        {
            return Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.5f;
        }

        private static Vector2 ResolveShapeDisplaySize(Vector2 size, AttackTelegraphShape2D shape)
        {
            if (shape != AttackTelegraphShape2D.Circle)
            {
                return size;
            }

            float diameter = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y));
            return new Vector2(diameter, diameter);
        }

        private Vector2 ResolveFollowUpAreaCenter()
        {
            Vector2 center = brain != null && brain.Target != null
                ? (Vector2)brain.Target.transform.position
                : _lockedCenter;
            return center + followUpAreaOffset;
        }

        private Vector2 ResolveFollowUpAreaKnockbackDirection(Collider2D hitCollider)
        {
            Vector2 delta = (Vector2)hitCollider.bounds.center - _followUpAreaCenter;
            if (delta.sqrMagnitude > 0.0001f)
            {
                return delta.normalized;
            }

            return _lockedDirection.sqrMagnitude > 0.0001f ? _lockedDirection.normalized : Vector2.right;
        }

        private Vector2 ResolveOriginPosition()
        {
            return telegraphOrigin != null ? telegraphOrigin.position : transform.position;
        }

        private Vector2 ResolveFacingDirection()
        {
            if (orientationAbility != null)
            {
                switch (orientationAbility.CurrentFacingDirection)
                {
                    case Character.FacingDirections.West:
                        return Vector2.left;
                    case Character.FacingDirections.North:
                        return Vector2.up;
                    case Character.FacingDirections.South:
                        return Vector2.down;
                    default:
                        return Vector2.right;
                }
            }

            return ResolveFallbackDirection();
        }

        private Vector2 ResolveAttackDirection(Vector2 origin)
        {
            if (lockAttackDirectionToTarget && TryResolveTargetDirection(origin, out Vector2 targetDirection))
            {
                return limitAttackDirectionToHorizontal
                    ? ResolveHorizontalAttackDirection(targetDirection)
                    : targetDirection;
            }

            Vector2 facingDirection = ResolveFacingDirection();
            return limitAttackDirectionToHorizontal
                ? ResolveHorizontalAttackDirection(facingDirection)
                : facingDirection;
        }

        private bool TryResolveTargetDirection(Vector2 origin, out Vector2 targetDirection)
        {
            targetDirection = Vector2.zero;

            if (brain == null || brain.Target == null)
            {
                return false;
            }

            Vector2 targetDelta = (Vector2)brain.Target.transform.position - origin;
            if (targetDelta.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            targetDirection = ResolveDominantCardinalDirection(targetDelta);
            return true;
        }

        private Vector2 ResolveHorizontalAttackDirection(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) >= horizontalFacingThreshold)
            {
                return direction.x >= 0f ? Vector2.right : Vector2.left;
            }

            return ResolveCurrentHorizontalFacingDirection();
        }

        private Vector2 ResolveCurrentHorizontalFacingDirection()
        {
            if (orientationAbility != null && orientationAbility.CurrentFacingDirection == Character.FacingDirections.West)
            {
                return Vector2.left;
            }

            if (orientationAbility != null && orientationAbility.CurrentFacingDirection == Character.FacingDirections.East)
            {
                return Vector2.right;
            }

            return _lockedDirection.x < 0f ? Vector2.left : Vector2.right;
        }

        private void ApplyLockedAnimatorDirectionParameters(Vector2 direction)
        {
            if (!driveAnimatorDirectionParameters || animator == null)
            {
                return;
            }

            Character.FacingDirections facingDirection = ResolveFacingDirectionFromVector(direction);
            SetAnimatorFloatIfExists(FacingDirection2DAnimatorParameter, ToFacingDirection2DValue(facingDirection));
            SetAnimatorFloatIfExists(HorizontalDirectionAnimatorParameter, direction.x);
            SetAnimatorFloatIfExists(VerticalDirectionAnimatorParameter, direction.y);
        }

        private void ApplyImmediateModelFacing(Vector2 direction)
        {
            if (!applyModelFacingImmediately
                || orientationAbility == null
                || character == null
                || character.CharacterModel == null
                || Mathf.Abs(direction.x) < horizontalFacingThreshold)
            {
                return;
            }

            Transform modelTransform = character.CharacterModel.transform;
            bool shouldFaceRight = direction.x >= 0f;

            if (orientationAbility.ModelShouldRotate)
            {
                Vector3 targetRotation = shouldFaceRight
                    ? orientationAbility.ModelRotationValueRight
                    : orientationAbility.ModelRotationValueLeft;
                targetRotation.x %= 360f;
                targetRotation.y %= 360f;
                targetRotation.z %= 360f;
                modelTransform.localEulerAngles = targetRotation;
            }

            if (orientationAbility.ModelShouldFlip)
            {
                modelTransform.localScale = shouldFaceRight
                    ? orientationAbility.ModelFlipValueRight
                    : orientationAbility.ModelFlipValueLeft;
            }
        }

        private void SetAnimatorFloatIfExists(int parameterHash, float value)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == parameterHash && parameter.type == AnimatorControllerParameterType.Float)
                {
                    animator.SetFloat(parameterHash, value);
                    return;
                }
            }
        }

        private static Vector2 ResolveDominantCardinalDirection(Vector2 direction)
        {
            if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
            {
                return direction.y >= 0f ? Vector2.up : Vector2.down;
            }

            return direction.x >= 0f ? Vector2.right : Vector2.left;
        }

        private static Character.FacingDirections ResolveFacingDirectionFromVector(Vector2 direction)
        {
            if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
            {
                return direction.y >= 0f
                    ? Character.FacingDirections.North
                    : Character.FacingDirections.South;
            }

            return direction.x >= 0f
                ? Character.FacingDirections.East
                : Character.FacingDirections.West;
        }

        private static float ToFacingDirection2DValue(Character.FacingDirections facingDirection)
        {
            switch (facingDirection)
            {
                case Character.FacingDirections.West:
                    return 0f;
                case Character.FacingDirections.North:
                    return 1f;
                case Character.FacingDirections.East:
                    return 2f;
                case Character.FacingDirections.South:
                    return 3f;
                default:
                    return 2f;
            }
        }

        private Vector2 ResolveFallbackDirection()
        {
            Vector2 transformRight = transform.right;
            return transformRight.sqrMagnitude > 0.0001f ? transformRight.normalized : Vector2.right;
        }

        private bool IsDead()
        {
            return character != null
                && character.ConditionState != null
                && character.ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead;
        }

        private bool IsInState(string stateName)
        {
            return brain != null
                && brain.CurrentState != null
                && brain.CurrentState.StateName == stateName;
        }

        private void ClearLockedAttack()
        {
            _attackStateElapsed = 0f;
            _hasLockedAttack = false;
            _hasExecutedImpact = false;
            _hasPendingAttackDamage = false;
            _telegraphHiddenForImpact = false;
            _hitTargetsThisAttack.Clear();
        }

        private void ClearFollowUpAreaAttack(bool hideTelegraph)
        {
            _followUpAreaElapsed = 0f;
            _hasPendingFollowUpAreaAttack = false;
            _followUpAreaTelegraphShown = false;
            _hitTargetsThisFollowUpArea.Clear();

            if (hideTelegraph)
            {
                telegraphView?.Hide();
            }
        }

        private static bool IsOwnedByAttacker(Health health, GameObject attacker)
        {
            if (health == null || attacker == null)
            {
                return false;
            }

            return health.gameObject == attacker || health.transform.IsChildOf(attacker.transform);
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[" + debugName + "] " + message, this);
            }
        }
    }
}

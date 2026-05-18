using System.Collections;
using System.Collections.Generic;
using LostMemory.Combat.Telegraph;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Chobomb Self Destruct Controller")]
    public sealed class ChobombSelfDestructController : MonoBehaviour
    {
        private const string WalkingParameterName = "Walking";
        private const string AttackParameterName = "Attack";
        private const string DamageParameterName = "Damage";
        private const string DeathParameterName = "Death";
        private const string WarningStateName = "Chobomb_Attack";
        private const string ExplosionStateName = "Chobomb_Death";

        [Header("References")]
        [SerializeField] private AIBrain brain;
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private CharacterOrientation2D orientation;
        [SerializeField] private CharacterHandleWeapon handleWeapon;
        [SerializeField] private Animator animator;
        [SerializeField] private Health health;
        [SerializeField] private AttackTelegraph2DView telegraphView;

        [Header("Self Destruct")]
        [SerializeField, Min(0.1f)] private float armingRadius = 1.35f;
        [SerializeField, Min(0.01f)] private float warningDuration = 0.58f;
        [SerializeField, Min(0.1f)] private float explosionRadius = 1.8f;
        [SerializeField, Min(0f)] private float explosionDamage = 20f;
        [SerializeField, Min(0f)] private float targetInvincibilityDuration = 0.45f;
        [SerializeField, Min(1)] private int maximumHits = 8;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField] private Color warningColor = new Color(1f, 0.2f, 0.05f, 0.38f);
        [SerializeField, Min(0.05f)] private float selfDestroyDelay = 0.75f;

        [Header("SFX")]
        [SerializeField] private AudioClip warningSfx;
        [SerializeField] private AudioClip explosionSfx;
        [SerializeField, Range(0f, 1f)] private float warningSfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float explosionSfxVolume = 1f;
        [SerializeField, Range(0.1f, 3f)] private float minPitch = 1f;
        [SerializeField, Range(0.1f, 3f)] private float maxPitch = 1f;
        [SerializeField] private bool fallbackWithoutSoundManager = true;

        private readonly HashSet<Health> _hitTargets = new HashSet<Health>();
        private Collider2D[] _overlapBuffer;
        private Coroutine _selfDestructRoutine;
        private Transform _armedTarget;
        private bool _isSelfDestructing;

        public void Configure(
            Animator configuredAnimator,
            AttackTelegraph2DView configuredTelegraphView,
            AIBrain configuredBrain,
            CharacterMovement configuredMovement,
            CharacterOrientation2D configuredOrientation,
            CharacterHandleWeapon configuredHandleWeapon,
            Health configuredHealth)
        {
            animator = configuredAnimator;
            telegraphView = configuredTelegraphView;
            brain = configuredBrain;
            movement = configuredMovement;
            orientation = configuredOrientation;
            handleWeapon = configuredHandleWeapon;
            health = configuredHealth;
            EnsureBuffer();
        }

        public void ConfigureTuning(
            float configuredArmingRadius,
            float configuredWarningDuration,
            float configuredExplosionRadius,
            float configuredExplosionDamage,
            float configuredTargetInvincibilityDuration,
            float configuredSelfDestroyDelay,
            LayerMask configuredTargetLayerMask)
        {
            armingRadius = Mathf.Max(0.1f, configuredArmingRadius);
            warningDuration = Mathf.Max(0.01f, configuredWarningDuration);
            explosionRadius = Mathf.Max(0.1f, configuredExplosionRadius);
            explosionDamage = Mathf.Max(0f, configuredExplosionDamage);
            targetInvincibilityDuration = Mathf.Max(0f, configuredTargetInvincibilityDuration);
            selfDestroyDelay = Mathf.Max(0.05f, configuredSelfDestroyDelay);
            targetLayerMask = configuredTargetLayerMask;
        }

        public void SetExplosionDamage(float configuredExplosionDamage)
        {
            explosionDamage = Mathf.Max(0f, configuredExplosionDamage);
        }

        private void Reset()
        {
            RefreshReferences();
            EnsureBuffer();
        }

        private void Awake()
        {
            RefreshReferences();
            EnsureBuffer();
        }

        private void OnValidate()
        {
            armingRadius = Mathf.Max(0.1f, armingRadius);
            warningDuration = Mathf.Max(0.01f, warningDuration);
            explosionRadius = Mathf.Max(0.1f, explosionRadius);
            explosionDamage = Mathf.Max(0f, explosionDamage);
            targetInvincibilityDuration = Mathf.Max(0f, targetInvincibilityDuration);
            maximumHits = Mathf.Max(1, maximumHits);
            selfDestroyDelay = Mathf.Max(0.05f, selfDestroyDelay);
            minPitch = Mathf.Max(0.1f, minPitch);
            maxPitch = Mathf.Max(0.1f, maxPitch);
            if (maxPitch < minPitch)
            {
                maxPitch = minPitch;
            }

            EnsureBuffer();
        }

        private void OnDisable()
        {
            telegraphView?.Hide();
            if (_selfDestructRoutine != null)
            {
                StopCoroutine(_selfDestructRoutine);
                _selfDestructRoutine = null;
            }
        }

        private void Update()
        {
            if (_isSelfDestructing || IsDead())
            {
                return;
            }

            if (TryFindArmingTarget(out Transform target))
            {
                _armedTarget = target;
                _selfDestructRoutine = StartCoroutine(SelfDestructSequence());
            }
        }

        private void RefreshReferences()
        {
            brain ??= GetComponent<AIBrain>();
            movement ??= GetComponent<CharacterMovement>();
            orientation ??= GetComponent<CharacterOrientation2D>();
            handleWeapon ??= GetComponent<CharacterHandleWeapon>();
            animator ??= GetComponentInChildren<Animator>(includeInactive: true);
            health ??= GetComponent<Health>();
            telegraphView ??= GetComponent<AttackTelegraph2DView>();
        }

        private void EnsureBuffer()
        {
            int safeSize = Mathf.Max(1, maximumHits);
            if (_overlapBuffer == null || _overlapBuffer.Length != safeSize)
            {
                _overlapBuffer = new Collider2D[safeSize];
            }
        }

        private bool TryFindArmingTarget(out Transform target)
        {
            target = null;

            if (brain != null && brain.Target != null && IsTransformInArmingRange(brain.Target))
            {
                target = brain.Target;
                return true;
            }

            int hitCount = OverlapTargets(transform.position, armingRadius);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = _overlapBuffer[i];
                if (hitCollider == null)
                {
                    continue;
                }

                Health targetHealth = hitCollider.GetComponentInParent<Health>();
                if (targetHealth == null || IsOwnedBySelf(targetHealth))
                {
                    continue;
                }

                target = targetHealth.transform;
                return true;
            }

            return false;
        }

        private IEnumerator SelfDestructSequence()
        {
            _isSelfDestructing = true;
            LockEnemy();
            FaceTarget(_armedTarget);
            PlayWarningAnimation();

            float elapsed = 0f;
            while (elapsed < warningDuration)
            {
                if (IsDead())
                {
                    telegraphView?.Hide();
                    _selfDestructRoutine = null;
                    yield break;
                }

                FaceTarget(_armedTarget);
                RefreshTelegraph();
                elapsed += Time.deltaTime;
                yield return null;
            }

            telegraphView?.Hide();
            Explode();
            _selfDestructRoutine = null;
        }

        private void LockEnemy()
        {
            if (brain != null)
            {
                brain.BrainActive = false;
            }

            if (handleWeapon != null)
            {
                handleWeapon.PermitAbility(false);
            }

            if (movement != null)
            {
                movement.SetMovement(Vector2.zero);
                movement.MovementForbidden = true;
            }

            SetAnimatorBoolIfPresent(WalkingParameterName, false);
        }

        private void PlayWarningAnimation()
        {
            PlaySfx(warningSfx, warningSfxVolume);
            SetAnimatorTriggerResetIfPresent(DamageParameterName);
            SetAnimatorBoolIfPresent(AttackParameterName, true);
            PlayAnimatorStateIfPresent(WarningStateName);
        }

        private void Explode()
        {
            Vector2 center = transform.position;
            PlaySfx(explosionSfx, explosionSfxVolume);
            DealExplosionDamage(center);

            SetAnimatorBoolIfPresent(AttackParameterName, false);
            SetAnimatorTriggerIfPresent(DeathParameterName);
            PlayAnimatorStateIfPresent(ExplosionStateName);

            if (health != null && health.CurrentHealth > 0f)
            {
                health.DestroyOnDeath = true;
                health.DisableModelOnDeath = false;
                health.DelayBeforeDestruction = Mathf.Max(health.DelayBeforeDestruction, selfDestroyDelay);
                health.Kill();
                return;
            }

            Destroy(gameObject, selfDestroyDelay);
        }

        private void DealExplosionDamage(Vector2 center)
        {
            EnsureBuffer();
            _hitTargets.Clear();

            int hitCount = OverlapTargets(center, explosionRadius);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = _overlapBuffer[i];
                if (hitCollider == null)
                {
                    continue;
                }

                Health targetHealth = hitCollider.GetComponentInParent<Health>();
                if (targetHealth == null
                    || _hitTargets.Contains(targetHealth)
                    || IsOwnedBySelf(targetHealth)
                    || !targetHealth.CanTakeDamageThisFrame())
                {
                    continue;
                }

                _hitTargets.Add(targetHealth);
                Vector2 knockbackDirection = ResolveKnockbackDirection(center, hitCollider);
                targetHealth.Damage(
                    explosionDamage,
                    gameObject,
                    targetInvincibilityDuration,
                    targetInvincibilityDuration,
                    new Vector3(knockbackDirection.x, knockbackDirection.y, 0f));
            }
        }

        private void PlaySfx(AudioClip clip, float volume)
        {
            if (clip == null)
            {
                return;
            }

            float pitch = Mathf.Approximately(minPitch, maxPitch)
                ? minPitch
                : Random.Range(minPitch, maxPitch);

            if (MMSoundManager.HasInstance && MMSoundManager.Current != null)
            {
                MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
                options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
                options.Location = transform.position;
                options.Volume = volume;
                options.Pitch = pitch;
                options.Loop = false;

                MMSoundManagerSoundPlayEvent.Trigger(clip, options);
                return;
            }

            if (fallbackWithoutSoundManager)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, volume);
            }
        }

        private int OverlapTargets(Vector2 center, float radius)
        {
            ContactFilter2D contactFilter = new ContactFilter2D();
            contactFilter.SetLayerMask(targetLayerMask);
            contactFilter.useTriggers = true;
            return Physics2D.OverlapCircle(center, radius, contactFilter, _overlapBuffer);
        }

        private void RefreshTelegraph()
        {
            if (telegraphView == null)
            {
                return;
            }

            telegraphView.Refresh(new AttackTelegraphRequest2D
            {
                Shape = AttackTelegraphShape2D.Circle,
                Center = transform.position,
                Direction = Vector2.right,
                Size = Vector2.one * explosionRadius * 2f,
                Color = warningColor,
                Duration = 0f
            });
        }

        private void FaceTarget(Transform target)
        {
            if (target == null)
            {
                return;
            }

            Vector2 direction = (Vector2)(target.position - transform.position);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            direction.Normalize();
            Character.FacingDirections facingDirection = ResolveFacingDirection(direction);

            if (orientation != null)
            {
                orientation.Face(facingDirection);
                if (Mathf.Abs(direction.x) >= 0.05f)
                {
                    orientation.FaceDirection(direction.x >= 0f ? 1 : -1);
                }
            }

            SetAnimatorFloatIfPresent("FacingDirection2D", ToFacingDirection2DValue(facingDirection));
            SetAnimatorFloatIfPresent("HorizontalDirection", direction.x);
            SetAnimatorFloatIfPresent("VerticalDirection", direction.y);
        }

        private static Character.FacingDirections ResolveFacingDirection(Vector2 direction)
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

        private Vector2 ResolveKnockbackDirection(Vector2 center, Collider2D hitCollider)
        {
            Vector2 delta = (Vector2)hitCollider.bounds.center - center;
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        }

        private bool IsTransformInArmingRange(Transform target)
        {
            return target != null
                && Vector2.Distance(transform.position, target.position) <= armingRadius;
        }

        private bool IsOwnedBySelf(Health targetHealth)
        {
            return targetHealth == health
                || targetHealth.gameObject == gameObject
                || targetHealth.transform.IsChildOf(transform);
        }

        private bool IsDead()
        {
            return health != null && health.CurrentHealth <= 0f;
        }

        private void SetAnimatorBoolIfPresent(string parameterName, bool value)
        {
            if (TryGetAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool, out int hash))
            {
                animator.SetBool(hash, value);
            }
        }

        private void SetAnimatorFloatIfPresent(string parameterName, float value)
        {
            if (TryGetAnimatorParameter(parameterName, AnimatorControllerParameterType.Float, out int hash))
            {
                animator.SetFloat(hash, value);
            }
        }

        private void SetAnimatorTriggerIfPresent(string parameterName)
        {
            if (TryGetAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger, out int hash))
            {
                animator.SetTrigger(hash);
            }
        }

        private void SetAnimatorTriggerResetIfPresent(string parameterName)
        {
            if (TryGetAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger, out int hash))
            {
                animator.ResetTrigger(hash);
            }
        }

        private bool TryGetAnimatorParameter(
            string parameterName,
            AnimatorControllerParameterType parameterType,
            out int parameterHash)
        {
            parameterHash = 0;
            if (animator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == parameterType && parameter.name == parameterName)
                {
                    parameterHash = parameter.nameHash;
                    return true;
                }
            }

            return false;
        }

        private void PlayAnimatorStateIfPresent(string stateName)
        {
            if (animator == null)
            {
                return;
            }

            int stateHash = Animator.StringToHash("Base Layer." + stateName);
            if (!animator.HasState(0, stateHash))
            {
                stateHash = Animator.StringToHash(stateName);
                if (!animator.HasState(0, stateHash))
                {
                    return;
                }
            }

            animator.Play(stateHash, 0, 0f);
            animator.Update(0f);
        }
    }
}

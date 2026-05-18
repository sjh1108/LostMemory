using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostMemory.Combat.Telegraph
{
    [AddComponentMenu("Lost Memory/Combat/Telegraph/AI Action Telegraphed Area Barrage")]
    public sealed class AIActionTelegraphedAreaBarrage : AIAction
    {
        [Header("References")]
        [SerializeField] private Character character;
        [SerializeField] private CharacterMovement movementAbility;
        [SerializeField] private CharacterOrientation2D orientationAbility;
        [SerializeField] private Animator animator;
        [SerializeField] private AttackTelegraph2DView sortingReferenceTelegraph;
        [SerializeField] private AreaAttackEffectPlayer effectPlayer;
        [SerializeField] private Transform telegraphParent;
        [SerializeField] private SortingGroup sortingGroupReference;

        [Header("Start Range")]
        [SerializeField, Min(0f)] private float minimumStartDistance = 3.2f;
        [SerializeField] private bool useMaximumStartDistance = true;
        [SerializeField, Min(0f)] private float maximumStartDistance = 8.5f;

        [Header("Barrage")]
        [SerializeField, Min(1)] private int strikeCount = 3;
        [SerializeField, Min(0f)] private float strikeInterval = 0.8f;
        [SerializeField, Min(0f)] private float impactDelay = 0.7f;
        [SerializeField, Min(0f)] private float cooldownDuration = 3f;
        [SerializeField, Min(0f)] private float targetPredictionTime = 0.35f;
        [SerializeField] private bool continueSequenceWithoutTarget = true;

        [Header("Animation")]
        [SerializeField] private bool playBarrageAnimation = true;
        [SerializeField] private string barrageAnimationStateName = "Moose1_RangedBarrage";
        [SerializeField, Min(0)] private int barrageAnimationLayer;
        [SerializeField, Range(0f, 1f)] private float barrageAnimationNormalizedTime;
        [SerializeField] private bool waitForBarrageAnimationBeforeComplete = true;
        [SerializeField, Min(0f)] private float barrageAnimationDuration = 2.6f;

        [Header("Area")]
        [SerializeField] private AttackTelegraphShape2D attackShape = AttackTelegraphShape2D.Circle;
        [SerializeField] private Vector2 attackSize = new Vector2(2.7f, 2.7f);
        [SerializeField] private Vector2 attackOffset = Vector2.zero;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField, Min(1)] private int maximumHits = 8;
        [SerializeField, Min(0f)] private float damage = 8f;
        [SerializeField, Min(0f)] private float targetInvincibilityDuration = 0.25f;

        [Header("Telegraph")]
        [SerializeField] private Color telegraphColor = new Color(1f, 0.12f, 0.05f, 0.38f);
        [SerializeField] private Vector3 telegraphPositionOffset = new Vector3(0f, 0.02f, 0f);
        [SerializeField] private int telegraphSortingOrderOffset;
        [SerializeField, Min(0f)] private float pulseSpeed = 8f;
        [SerializeField, Range(0f, 1f)] private float pulseAlphaStrength = 0.18f;

        [Header("Facing And Movement")]
        [SerializeField] private bool lockMovementDuringBarrage = true;
        [SerializeField] private bool restoreMovementForbiddenStateOnUnlock;
        [SerializeField] private bool faceTargetOnStrike = true;
        [SerializeField, Min(0f)] private float horizontalFacingThreshold = 0.05f;

        [Header("Debug")]
        [SerializeField] private string debugName = "TelegraphedAreaBarrage";
        [SerializeField] private bool debugLogging;

        private const int TelegraphTextureSize = 64;

        private static Sprite _boxTelegraphSprite;
        private static Sprite _circleTelegraphSprite;

        private readonly HashSet<Health> _hitTargetsThisStrike = new HashSet<Health>();
        private readonly List<GameObject> _activeTelegraphs = new List<GameObject>();
        private Collider2D[] _overlapBuffer;
        private Vector2 _lastTargetPosition;
        private float _lastTargetSampleTime;
        private float _nextReadyTime;
        private bool _hasTargetSample;
        private bool _barrageStarted;
        private bool _sequenceCompleted;
        private bool _movementLockedByBarrage;
        private bool _movementForbiddenBeforeLock;

        public bool IsReady => Time.time >= _nextReadyTime && !_barrageStarted;
        public bool HasCompletedBarrage => _sequenceCompleted;
        public float RemainingCooldown => Mathf.Max(0f, _nextReadyTime - Time.time);

        protected override void Awake()
        {
            base.Awake();
            RefreshReferences();
            EnsureBuffer();
        }

        private void Reset()
        {
            RefreshReferences();
        }

        private void OnValidate()
        {
            strikeCount = Mathf.Max(1, strikeCount);
            maximumHits = Mathf.Max(1, maximumHits);
            maximumStartDistance = Mathf.Max(minimumStartDistance, maximumStartDistance);
            RefreshReferences();
            EnsureBuffer();
        }

        private void OnDisable()
        {
            StopBarrage(true);
        }

        public override void Initialization()
        {
            if (!ShouldInitialize)
            {
                return;
            }

            base.Initialization();
            RefreshReferences();
            EnsureBuffer();
        }

        public override void OnEnterState()
        {
            base.OnEnterState();
            RefreshReferences();
            EnsureBuffer();

            if (!CanStartFromCurrentTarget())
            {
                return;
            }

            _barrageStarted = true;
            _sequenceCompleted = false;
            _hasTargetSample = false;
            ApplyMovementLock();
            StartCoroutine(BarrageSequence());
            Log("Started barrage.");
        }

        public override void PerformAction()
        {
            if (!_barrageStarted)
            {
                return;
            }

            if (_sequenceCompleted)
            {
                ApplyMovementLock();
                return;
            }

            if (IsDead())
            {
                StopBarrage(true);
                return;
            }

            ApplyMovementLock();
        }

        public override void OnExitState()
        {
            bool started = _barrageStarted;
            StopBarrage(true);
            if (started)
            {
                _nextReadyTime = Time.time + cooldownDuration;
            }

            base.OnExitState();
        }

        public bool CanStartFromCurrentTarget()
        {
            if (!IsReady || IsDead() || _brain == null || _brain.Target == null)
            {
                return false;
            }

            float distance = Vector2.Distance(transform.position, _brain.Target.position);
            if (distance < minimumStartDistance)
            {
                return false;
            }

            return !useMaximumStartDistance || distance <= maximumStartDistance;
        }

        private IEnumerator BarrageSequence()
        {
            float sequenceStartedAt = Time.time;
            ApplyFacingToCurrentTarget();
            PlayBarrageAnimation();

            for (int i = 0; i < strikeCount; i++)
            {
                if (IsDead())
                {
                    yield break;
                }

                if (TryResolveStrikeCenter(out Vector2 center))
                {
                    Vector2 knockbackDirection = ResolveKnockbackDirection(center);
                    ApplyFacing(knockbackDirection);
                    StartCoroutine(StrikeSequence(center, knockbackDirection));
                }

                if (i < strikeCount - 1)
                {
                    yield return new WaitForSeconds(ResolveDelayBeforeNextStrike());
                }
            }

            float completionDelay = ResolveSequenceCompletionDuration() - (Time.time - sequenceStartedAt);
            if (completionDelay > 0f)
            {
                yield return new WaitForSeconds(completionDelay);
            }

            _sequenceCompleted = true;
            ActionInProgress = false;
            Log("Completed barrage.");
        }

        private IEnumerator StrikeSequence(Vector2 center, Vector2 knockbackDirection)
        {
            GameObject telegraphObject = CreateTelegraphObject(center);
            float elapsed = 0f;

            while (elapsed < impactDelay)
            {
                if (IsDead())
                {
                    DestroyTelegraph(telegraphObject);
                    yield break;
                }

                ApplyTelegraphPulse(telegraphObject);
                elapsed += Time.deltaTime;
                yield return null;
            }

            DestroyTelegraph(telegraphObject);

            if (!IsDead())
            {
                effectPlayer?.PlayOnce(center, attackSize);
                ExecuteStrike(center, knockbackDirection);
            }
        }

        private void ExecuteStrike(Vector2 center, Vector2 knockbackDirection)
        {
            EnsureBuffer();
            _hitTargetsThisStrike.Clear();

            int hitCount = OverlapAttackAreaNonAlloc(center);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = _overlapBuffer[i];
                if (hitCollider == null)
                {
                    continue;
                }

                Health health = hitCollider.GetComponentInParent<Health>();
                if (health == null
                    || _hitTargetsThisStrike.Contains(health)
                    || IsOwnedByAttacker(health, gameObject)
                    || !health.CanTakeDamageThisFrame())
                {
                    continue;
                }

                _hitTargetsThisStrike.Add(health);
                health.Damage(
                    damage,
                    gameObject,
                    targetInvincibilityDuration,
                    targetInvincibilityDuration,
                    new Vector3(knockbackDirection.x, knockbackDirection.y, 0f));
            }

            Log("Executed strike at " + center + ".");
        }

        private int OverlapAttackAreaNonAlloc(Vector2 center)
        {
            if (attackShape == AttackTelegraphShape2D.Circle)
            {
                return Physics2D.OverlapCircle(
                    center,
                    ResolveCircleRadius(attackSize),
                    BuildTargetContactFilter(),
                    _overlapBuffer);
            }

            return Physics2D.OverlapBox(
                center,
                attackSize,
                0f,
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

        private bool TryResolveStrikeCenter(out Vector2 center)
        {
            center = default;

            if (_brain != null && _brain.Target != null)
            {
                Vector2 currentPosition = _brain.Target.position;
                Vector2 velocity = Vector2.zero;
                float now = Time.time;

                if (_hasTargetSample)
                {
                    float deltaTime = now - _lastTargetSampleTime;
                    if (deltaTime > 0.0001f)
                    {
                        velocity = (currentPosition - _lastTargetPosition) / deltaTime;
                    }
                }

                _lastTargetPosition = currentPosition;
                _lastTargetSampleTime = now;
                _hasTargetSample = true;
                center = currentPosition + velocity * targetPredictionTime + attackOffset;
                return true;
            }

            if (continueSequenceWithoutTarget && _hasTargetSample)
            {
                center = _lastTargetPosition + attackOffset;
                return true;
            }

            return false;
        }

        private GameObject CreateTelegraphObject(Vector2 center)
        {
            Sprite sprite = GetOrCreateTelegraphSprite(attackShape);
            if (sprite == null)
            {
                return null;
            }

            GameObject telegraphObject = new GameObject("AreaBarrageTelegraph");
            telegraphObject.layer = gameObject.layer;

            Transform telegraphTransform = telegraphObject.transform;
            telegraphTransform.SetParent(ResolveTelegraphParent(), false);
            telegraphTransform.position = (Vector3)center + telegraphPositionOffset;
            telegraphTransform.rotation = Quaternion.identity;
            telegraphTransform.localScale = ResolveTelegraphScale(ResolveShapeDisplaySize(attackSize, attackShape), telegraphTransform.parent);

            SpriteRenderer renderer = telegraphObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = telegraphColor;
            TelegraphSpriteRendererUtility.ApplyTelegraphMaterial(renderer);
            ApplyTelegraphSorting(renderer);

            _activeTelegraphs.Add(telegraphObject);
            return telegraphObject;
        }

        private void ApplyTelegraphPulse(GameObject telegraphObject)
        {
            if (telegraphObject == null)
            {
                return;
            }

            SpriteRenderer renderer = telegraphObject.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                return;
            }

            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float alphaMultiplier = Mathf.Lerp(1f - pulseAlphaStrength, 1f, pulse);
            renderer.color = new Color(
                telegraphColor.r,
                telegraphColor.g,
                telegraphColor.b,
                telegraphColor.a * alphaMultiplier);
        }

        private void DestroyTelegraph(GameObject telegraphObject)
        {
            if (telegraphObject == null)
            {
                return;
            }

            _activeTelegraphs.Remove(telegraphObject);
            Destroy(telegraphObject);
        }

        private void StopBarrage(bool releaseMovementLock)
        {
            StopAllCoroutines();
            ClearTelegraphs();
            _hitTargetsThisStrike.Clear();
            _barrageStarted = false;
            _sequenceCompleted = false;

            if (releaseMovementLock)
            {
                ReleaseMovementLock();
            }
        }

        private void ClearTelegraphs()
        {
            for (int i = _activeTelegraphs.Count - 1; i >= 0; i--)
            {
                if (_activeTelegraphs[i] != null)
                {
                    Destroy(_activeTelegraphs[i]);
                }
            }

            _activeTelegraphs.Clear();
        }

        private void ApplyMovementLock()
        {
            if (!lockMovementDuringBarrage || movementAbility == null)
            {
                return;
            }

            if (!_movementLockedByBarrage)
            {
                _movementForbiddenBeforeLock = movementAbility.MovementForbidden;
                _movementLockedByBarrage = true;
            }

            movementAbility.SetMovement(Vector2.zero);
            movementAbility.MovementForbidden = true;
        }

        private void ReleaseMovementLock()
        {
            if (!_movementLockedByBarrage || movementAbility == null)
            {
                _movementLockedByBarrage = false;
                _movementForbiddenBeforeLock = false;
                return;
            }

            movementAbility.MovementForbidden = restoreMovementForbiddenStateOnUnlock
                && _movementForbiddenBeforeLock;
            _movementLockedByBarrage = false;
            _movementForbiddenBeforeLock = false;
        }

        private void ApplyFacing(Vector2 direction)
        {
            if (!faceTargetOnStrike || orientationAbility == null)
            {
                return;
            }

            if (Mathf.Abs(direction.x) >= horizontalFacingThreshold)
            {
                orientationAbility.FaceDirection(direction.x >= 0f ? 1 : -1);
            }

            Character.FacingDirections facingDirection = ResolveFacingDirectionFromVector(direction);
            orientationAbility.Face(facingDirection);
        }

        private void PlayBarrageAnimation()
        {
            if (!playBarrageAnimation
                || animator == null
                || string.IsNullOrWhiteSpace(barrageAnimationStateName))
            {
                return;
            }

            animator.Play(barrageAnimationStateName, barrageAnimationLayer, barrageAnimationNormalizedTime);
        }

        private float ResolveDelayBeforeNextStrike()
        {
            return Mathf.Max(0f, strikeInterval);
        }

        private float ResolveSequenceCompletionDuration()
        {
            float duration = ResolveAreaSequenceDuration();
            if (waitForBarrageAnimationBeforeComplete && playBarrageAnimation)
            {
                duration = Mathf.Max(duration, ResolveBarrageAnimationDuration());
            }

            return duration;
        }

        private float ResolveAreaSequenceDuration()
        {
            return Mathf.Max(0, strikeCount - 1) * ResolveDelayBeforeNextStrike()
                   + Mathf.Max(0f, impactDelay);
        }

        private float ResolveBarrageAnimationDuration()
        {
            if (barrageAnimationDuration > 0f)
            {
                return barrageAnimationDuration;
            }

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return 0f;
            }

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null && clip.name == barrageAnimationStateName)
                {
                    return clip.length;
                }
            }

            return 0f;
        }

        private void ApplyFacingToCurrentTarget()
        {
            if (_brain == null || _brain.Target == null)
            {
                return;
            }

            Vector2 direction = (Vector2)_brain.Target.position - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            ApplyFacing(direction.normalized);
        }

        private Vector2 ResolveKnockbackDirection(Vector2 center)
        {
            Vector2 delta = center - (Vector2)transform.position;
            if (delta.sqrMagnitude > 0.0001f)
            {
                return delta.normalized;
            }

            if (orientationAbility != null && orientationAbility.CurrentFacingDirection == Character.FacingDirections.West)
            {
                return Vector2.left;
            }

            return Vector2.right;
        }

        private Transform ResolveTelegraphParent()
        {
            if (telegraphParent != null)
            {
                return telegraphParent;
            }

            sortingGroupReference ??= GetComponentInParent<SortingGroup>();
            if (sortingGroupReference != null)
            {
                return sortingGroupReference.transform.parent;
            }

            return transform.parent;
        }

        private void ApplyTelegraphSorting(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (sortingReferenceTelegraph != null
                && sortingReferenceTelegraph.TryGetRenderSorting(out int layerId, out int sortingOrder))
            {
                renderer.sortingLayerID = layerId;
                renderer.sortingOrder = sortingOrder + telegraphSortingOrderOffset;
                return;
            }

            sortingGroupReference ??= GetComponentInParent<SortingGroup>();
            if (sortingGroupReference != null)
            {
                renderer.sortingLayerID = sortingGroupReference.sortingLayerID;
                renderer.sortingOrder = sortingGroupReference.sortingOrder + telegraphSortingOrderOffset;
                return;
            }

            renderer.sortingOrder = telegraphSortingOrderOffset;
        }

        private Vector3 ResolveTelegraphScale(Vector2 worldSize, Transform renderParent)
        {
            Vector3 parentScale = renderParent != null ? renderParent.lossyScale : Vector3.one;
            float safeScaleX = Mathf.Abs(parentScale.x) > 0.0001f ? Mathf.Abs(parentScale.x) : 1f;
            float safeScaleY = Mathf.Abs(parentScale.y) > 0.0001f ? Mathf.Abs(parentScale.y) : 1f;
            return new Vector3(worldSize.x / safeScaleX, worldSize.y / safeScaleY, 1f);
        }

        public void RefreshReferences()
        {
            _brain ??= GetComponentInParent<AIBrain>();
            character ??= GetComponentInParent<Character>();
            movementAbility ??= character?.FindAbility<CharacterMovement>();
            orientationAbility ??= character?.FindAbility<CharacterOrientation2D>();
            animator ??= ResolveAnimator();
            sortingReferenceTelegraph ??= GetComponent<AttackTelegraph2DView>();
            effectPlayer ??= GetComponent<AreaAttackEffectPlayer>();
            sortingGroupReference ??= GetComponentInParent<SortingGroup>();
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

        private bool IsDead()
        {
            return character != null
                   && character.ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead;
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

        private static float ResolveCircleRadius(Vector2 size)
        {
            return Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.5f;
        }

        private static bool IsOwnedByAttacker(Health health, GameObject attacker)
        {
            if (health == null || attacker == null)
            {
                return false;
            }

            return health.gameObject == attacker || health.transform.IsChildOf(attacker.transform);
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

        private static Sprite GetOrCreateTelegraphSprite(AttackTelegraphShape2D shape)
        {
            return shape == AttackTelegraphShape2D.Circle
                ? GetOrCreateCircleTelegraphSprite()
                : GetOrCreateBoxTelegraphSprite();
        }

        private static Sprite GetOrCreateBoxTelegraphSprite()
        {
            if (_boxTelegraphSprite != null)
            {
                return _boxTelegraphSprite;
            }

            Texture2D texture = new Texture2D(TelegraphTextureSize, TelegraphTextureSize, TextureFormat.RGBA32, false)
            {
                name = "AreaBarrageTelegraphBoxTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[TelegraphTextureSize * TelegraphTextureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            _boxTelegraphSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TelegraphTextureSize, TelegraphTextureSize),
                new Vector2(0.5f, 0.5f),
                TelegraphTextureSize);
            _boxTelegraphSprite.name = "AreaBarrageTelegraphBoxSprite";
            return _boxTelegraphSprite;
        }

        private static Sprite GetOrCreateCircleTelegraphSprite()
        {
            if (_circleTelegraphSprite != null)
            {
                return _circleTelegraphSprite;
            }

            Texture2D texture = new Texture2D(TelegraphTextureSize, TelegraphTextureSize, TextureFormat.RGBA32, false)
            {
                name = "AreaBarrageTelegraphCircleTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[TelegraphTextureSize * TelegraphTextureSize];
            float radius = TelegraphTextureSize * 0.5f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < TelegraphTextureSize; y++)
            {
                for (int x = 0; x < TelegraphTextureSize; x++)
                {
                    Vector2 pixelCenter = new Vector2(x + 0.5f, y + 0.5f);
                    float distance = Vector2.Distance(pixelCenter, center);
                    pixels[y * TelegraphTextureSize + x] = distance <= radius ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            _circleTelegraphSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TelegraphTextureSize, TelegraphTextureSize),
                new Vector2(0.5f, 0.5f),
                TelegraphTextureSize);
            _circleTelegraphSprite.name = "AreaBarrageTelegraphCircleSprite";
            return _circleTelegraphSprite;
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

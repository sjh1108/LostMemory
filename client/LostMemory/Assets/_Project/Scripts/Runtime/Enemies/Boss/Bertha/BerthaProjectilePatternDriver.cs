using System.Collections;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Projectile Pattern Driver")]
    public sealed class BerthaProjectilePatternDriver : MonoBehaviour, MMEventListener<AIStateEvent>
    {
        public enum BurstPatternMode
        {
            Radial = 0,
            Fan = 1
        }

        [System.Serializable]
        public struct BurstInstruction
        {
            public BurstPatternMode Mode;
            [Min(0f)] public float Delay;
            [Min(1)] public int ProjectileCount;
            [Min(0f)] public float SpreadAngle;
            [Min(0.01f)] public float Speed;
            [Min(0.01f)] public float Lifetime;
            [Min(0f)] public float Damage;
            [Min(0f)] public float TargetInvincibilityDuration;
            [Min(0.05f)] public float HitRadius;
            public float AngleOffsetDegrees;
            [Min(0f)] public float SpawnDistance;
        }

        [SerializeField] private string driverKey = "ProjectilePattern";
        [SerializeField] private AIBrain brain;
        [SerializeField] private Character character;
        [SerializeField] private CharacterOrientation2D orientationAbility;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private BerthaProjectileBurstEmitter burstEmitter;
        [SerializeField] private string attackStateName = "HeavyAttack";
        [SerializeField] private BurstInstruction[] burstSequence = System.Array.Empty<BurstInstruction>();
        [SerializeField] private bool debugLogging;

        private Coroutine _burstRoutine;
        private Vector2 _lockedDirection = Vector2.right;

        public string DriverKey => driverKey;

        private void Reset()
        {
            RefreshReferences();
        }

        private void OnValidate()
        {
            RefreshReferences();
            NormalizeBurstSequence();
        }

        private void Awake()
        {
            RefreshReferences();
            NormalizeBurstSequence();
        }

        private void OnEnable()
        {
            this.MMEventStartListening<AIStateEvent>();
        }

        private void OnDisable()
        {
            this.MMEventStopListening<AIStateEvent>();
            StopBurstRoutine();
        }

        public void OnMMEvent(AIStateEvent stateEvent)
        {
            if (stateEvent.Brain != brain)
            {
                return;
            }

            if (IsDead())
            {
                StopBurstRoutine();
                return;
            }

            string enteringState = stateEvent.EnterState != null ? stateEvent.EnterState.StateName : string.Empty;
            string exitingState = stateEvent.ExitState != null ? stateEvent.ExitState.StateName : string.Empty;

            if (enteringState == attackStateName)
            {
                LockDirection();
                StopBurstRoutine();
                _burstRoutine = StartCoroutine(RunBurstSequence());
                return;
            }

            if (exitingState == attackStateName)
            {
                StopBurstRoutine();
            }
        }

        public void Configure(
            string configuredDriverKey,
            AIBrain configuredBrain,
            Character configuredCharacter,
            CharacterOrientation2D configuredOrientationAbility,
            Transform configuredProjectileOrigin,
            BerthaProjectileBurstEmitter configuredBurstEmitter,
            string configuredAttackStateName,
            BurstInstruction[] configuredBurstSequence,
            bool configuredDebugLogging)
        {
            driverKey = configuredDriverKey;
            brain = configuredBrain;
            character = configuredCharacter;
            orientationAbility = configuredOrientationAbility;
            projectileOrigin = configuredProjectileOrigin;
            burstEmitter = configuredBurstEmitter;
            attackStateName = configuredAttackStateName;
            burstSequence = configuredBurstSequence != null
                ? (BurstInstruction[])configuredBurstSequence.Clone()
                : System.Array.Empty<BurstInstruction>();
            debugLogging = configuredDebugLogging;
            RefreshReferences();
            NormalizeBurstSequence();
        }

        private IEnumerator RunBurstSequence()
        {
            if (burstEmitter == null || burstSequence == null || burstSequence.Length == 0)
            {
                yield break;
            }

            for (int i = 0; i < burstSequence.Length; i++)
            {
                BurstInstruction burst = burstSequence[i];
                if (burst.Delay > 0f)
                {
                    yield return new WaitForSeconds(burst.Delay);
                }

                if (!CanEmitBurst())
                {
                    yield break;
                }

                EmitBurst(burst);
            }

            _burstRoutine = null;
        }

        private void EmitBurst(BurstInstruction burst)
        {
            float baseAngle = Mathf.Atan2(_lockedDirection.y, _lockedDirection.x) * Mathf.Rad2Deg + burst.AngleOffsetDegrees;
            Vector3 originalPosition = transform.position;

            if (projectileOrigin != null)
            {
                originalPosition = projectileOrigin.position;
            }

            switch (burst.Mode)
            {
                case BurstPatternMode.Fan:
                    burstEmitter.EmitFan(
                        gameObject,
                        _lockedDirection,
                        burst.ProjectileCount,
                        burst.SpreadAngle,
                        burst.Speed,
                        burst.Lifetime,
                        burst.Damage,
                        burst.TargetInvincibilityDuration,
                        burst.HitRadius,
                        burst.AngleOffsetDegrees,
                        burst.SpawnDistance);
                    break;

                default:
                    burstEmitter.EmitRadial(
                        gameObject,
                        burst.ProjectileCount,
                        burst.Speed,
                        burst.Lifetime,
                        burst.Damage,
                        burst.TargetInvincibilityDuration,
                        burst.HitRadius,
                        baseAngle,
                        burst.SpawnDistance);
                    break;
            }

            Log("Emitted burst from " + originalPosition + ".");
        }

        private void RefreshReferences()
        {
            brain ??= GetComponent<AIBrain>();
            character ??= GetComponent<Character>();
            orientationAbility ??= GetComponent<CharacterOrientation2D>();
            burstEmitter ??= GetComponent<BerthaProjectileBurstEmitter>();
            projectileOrigin ??= transform;
        }

        private void NormalizeBurstSequence()
        {
            if (burstSequence == null)
            {
                burstSequence = System.Array.Empty<BurstInstruction>();
                return;
            }

            for (int i = 0; i < burstSequence.Length; i++)
            {
                BurstInstruction burst = burstSequence[i];
                burst.Delay = Mathf.Max(0f, burst.Delay);
                burst.ProjectileCount = Mathf.Max(1, burst.ProjectileCount);
                burst.SpreadAngle = Mathf.Max(0f, burst.SpreadAngle);
                burst.Speed = Mathf.Max(0.01f, burst.Speed);
                burst.Lifetime = Mathf.Max(0.01f, burst.Lifetime);
                burst.Damage = Mathf.Max(0f, burst.Damage);
                burst.TargetInvincibilityDuration = Mathf.Max(0f, burst.TargetInvincibilityDuration);
                burst.HitRadius = Mathf.Max(0.05f, burst.HitRadius);
                burst.SpawnDistance = Mathf.Max(0f, burst.SpawnDistance);
                burstSequence[i] = burst;
            }
        }

        private void LockDirection()
        {
            _lockedDirection = ResolveFacingDirection();
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

            Vector2 transformRight = transform.right;
            return transformRight.sqrMagnitude > 0.0001f ? transformRight.normalized : Vector2.right;
        }

        private bool CanEmitBurst()
        {
            return burstEmitter != null
                && !IsDead()
                && brain != null
                && brain.CurrentState != null
                && brain.CurrentState.StateName == attackStateName;
        }

        private bool IsDead()
        {
            return character != null
                && character.ConditionState != null
                && character.ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead;
        }

        private void StopBurstRoutine()
        {
            if (_burstRoutine == null)
            {
                return;
            }

            StopCoroutine(_burstRoutine);
            _burstRoutine = null;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BerthaProjectilePatternDriver:" + driverKey + "] " + message, this);
            }
        }
    }
}

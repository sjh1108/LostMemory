using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat.Telegraph
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Telegraph/AI Brain Ranged Telegraph Driver")]
    public class AIBrainRangedTelegraphDriver : MonoBehaviour, MMEventListener<AIStateEvent>
    {
        [SerializeField] private AIBrain brain;
        [SerializeField] private Character character;
        [SerializeField] private CharacterHandleWeapon handleWeapon;
        [SerializeField] private Transform telegraphOrigin;
        [SerializeField] private AttackTelegraph2DView telegraphView;
        [SerializeField] private string telegraphStateName = "AimReady";
        [SerializeField] private Color telegraphColor = new Color(1f, 0.34f, 0.08f, 0.32f);
        [SerializeField] private float fallbackLength = 5.5f;
        [SerializeField] private float maximumLength = 6.5f;
        [SerializeField] private float lengthPadding = 0.35f;
        [SerializeField] private float width = 0.35f;
        [SerializeField] private float startOffset = 0.1f;
        [SerializeField] private bool useLastKnownTargetPosition = true;
        [SerializeField] private bool lockDirectionOnTelegraphStart;
        // 게스트 측 1회 broadcast clone 의 visual 유지 시간. AimReady state 평균 체류 길이로 튜닝.
        // 0 이면 MonsterAttackBroadcast.SpawnVisualClone 의 effectiveWarning clamp(0.01)로 한 프레임만 깜빡임.
        [SerializeField, Min(0.05f)] private float aimStateExpectedDuration = 1.0f;

        private Vector2 _lockedDirection = Vector2.right;
        private float _lockedLength;
        private bool _hasLockedAim;

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
            EnsureTelegraphView();
        }

        private void OnEnable()
        {
            this.MMEventStartListening<AIStateEvent>();
        }

        private void OnDisable()
        {
            this.MMEventStopListening<AIStateEvent>();
            HideTelegraph();
        }

        private void Update()
        {
            if (brain == null)
            {
                return;
            }

            if (IsDead())
            {
                HideTelegraph();
                return;
            }

            if (!IsInState(telegraphStateName))
            {
                HideTelegraph();
                return;
            }

            EnsureTelegraphView();
            if (telegraphView == null)
            {
                return;
            }

            telegraphView.Refresh(BuildTelegraphRequest());
        }

        public void OnMMEvent(AIStateEvent stateEvent)
        {
            if (stateEvent.Brain != brain)
            {
                return;
            }

            if (IsDead())
            {
                HideTelegraph();
                return;
            }

            string enteringState = stateEvent.EnterState != null ? stateEvent.EnterState.StateName : string.Empty;
            string exitingState = stateEvent.ExitState != null ? stateEvent.ExitState.StateName : string.Empty;

            if (enteringState == telegraphStateName)
            {
                if (lockDirectionOnTelegraphStart)
                {
                    LockAim();
                }

                EnsureTelegraphView();
                telegraphView?.Show(BuildTelegraphRequest());
                return;
            }

            if (exitingState == telegraphStateName)
            {
                HideTelegraph();
            }
        }

        private void AutoAssignReferences()
        {
            brain ??= GetComponent<AIBrain>();
            character ??= GetComponent<Character>();
            handleWeapon ??= GetComponent<CharacterHandleWeapon>();
            telegraphView ??= GetComponent<AttackTelegraph2DView>();

            if (telegraphOrigin == null && handleWeapon != null && handleWeapon.ProjectileSpawn != null)
            {
                telegraphOrigin = handleWeapon.ProjectileSpawn;
            }

            telegraphOrigin ??= transform;
        }

        private void EnsureTelegraphView()
        {
            telegraphView ??= GetComponent<AttackTelegraph2DView>();

            if (telegraphView == null)
            {
                telegraphView = gameObject.AddComponent<AttackTelegraph2DView>();
            }
        }

        private AttackTelegraphRequest2D BuildTelegraphRequest()
        {
            Vector2 origin = ResolveOriginPosition();

            if (lockDirectionOnTelegraphStart)
            {
                if (!_hasLockedAim)
                {
                    LockAim();
                }

                return CreateRequest(origin, _lockedDirection, _lockedLength);
            }

            ResolveAimPreview(origin, out Vector2 direction, out float length);
            return CreateRequest(origin, direction, length);
        }

        private void LockAim()
        {
            Vector2 origin = ResolveOriginPosition();
            ResolveAimPreview(origin, out _lockedDirection, out _lockedLength);
            _hasLockedAim = true;
        }

        private void ResolveAimPreview(Vector2 origin, out Vector2 direction, out float length)
        {
            Vector2 targetPosition = ResolveTargetPosition(origin);
            Vector2 targetOffset = targetPosition - origin;

            if (targetOffset.sqrMagnitude <= 0.0001f)
            {
                direction = ResolveFallbackDirection();
                length = fallbackLength;
                return;
            }

            float rawLength = targetOffset.magnitude + lengthPadding;
            direction = targetOffset.normalized;
            length = maximumLength > 0f ? Mathf.Min(rawLength, maximumLength) : rawLength;
        }

        private AttackTelegraphRequest2D CreateRequest(Vector2 origin, Vector2 direction, float length)
        {
            Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float safeLength = Mathf.Max(0.1f, length);
            float safeWidth = Mathf.Max(0.05f, width);

            return new AttackTelegraphRequest2D
            {
                Shape = AttackTelegraphShape2D.Box,
                Center = origin + safeDirection * (startOffset + (safeLength * 0.5f)),
                Direction = safeDirection,
                Size = new Vector2(safeLength, safeWidth),
                Color = telegraphColor,
                Duration = aimStateExpectedDuration
            };
        }

        private Vector2 ResolveOriginPosition()
        {
            return telegraphOrigin != null ? telegraphOrigin.position : transform.position;
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

            return origin + ResolveFallbackDirection() * fallbackLength;
        }

        private Vector2 ResolveFallbackDirection()
        {
            if (_hasLockedAim && _lockedDirection.sqrMagnitude > 0.0001f)
            {
                return _lockedDirection.normalized;
            }

            if (handleWeapon != null && handleWeapon.CurrentWeapon != null)
            {
                Vector2 weaponRight = handleWeapon.CurrentWeapon.transform.right;
                if (weaponRight.sqrMagnitude > 0.0001f)
                {
                    return weaponRight.normalized;
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

        private bool IsDead()
        {
            return character != null
                && character.ConditionState != null
                && character.ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead;
        }

        private void HideTelegraph()
        {
            telegraphView?.Hide();
            _hasLockedAim = false;
        }
    }
}

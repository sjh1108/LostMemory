using System;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-015 플레이어 상태 단일 진실 소스.
    /// 4개 Khi 컨트롤러(Parry/HitStun/Dash/Down/Melee)와 Character.MovementState/ConditionState를
    /// 매 프레임 쿼리해 현재 KhiPlayerState를 결정한다. 상태는 보관하지 않고 쿼리 결과만 노출.
    ///
    /// 구독자 생명주기: 반드시 OnEnable에서 subscribe, OnDisable에서 unsubscribe.
    /// 플레이어 Defeated 시 Aggregator의 OnDisable도 호출되므로 Respawn 후 재구독 고려.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Player State Aggregator")]
    [DefaultExecutionOrder(200)]
    public class KhiPlayerStateAggregator : MonoBehaviour
    {
        [Header("Refs (optional, auto-resolved in Awake)")]
        [SerializeField] private Character character;
        [SerializeField] private KhiParryController parryController;
        [SerializeField] private KhiHitStunController hitStunController;
        [SerializeField] private KhiDashController dashController;
        [SerializeField] private KhiDownController downController;
        [SerializeField] private KhiMeleeComboController meleeCombo;

        [Header("Debug")]
        [SerializeField] private bool logStateChanges = false;

        private KhiPlayerState _currentState = KhiPlayerState.Idle;
        private KhiPlayerState _previousState = KhiPlayerState.Idle;
        private float _enteredAt;

        public event Action<KhiPlayerState, KhiPlayerState> StateChanged;

        public KhiPlayerState CurrentState => _currentState;
        public KhiPlayerState PreviousState => _previousState;
        public float TimeInCurrentState => Mathf.Max(0f, Time.time - _enteredAt);

        public float ParryRemaining => parryController != null ? parryController.CurrentStateRemaining : 0f;
        public float HurtRemaining => hitStunController != null ? hitStunController.CurrentStateRemaining : 0f;
        public float DownRemaining => downController != null ? downController.DownTimeRemaining : 0f;

        public KhiParryState RawParryState => parryController != null ? parryController.CurrentState : KhiParryState.Idle;
        public KhiHitStunState RawHitStunState => hitStunController != null ? hitStunController.CurrentState : KhiHitStunState.Idle;
        public KhiDownState RawDownState => downController != null ? downController.CurrentState : KhiDownState.Normal;
        public bool RawIsAttacking => meleeCombo != null && meleeCombo.IsAttacking;
        public bool RawIsInAttackRecovery => meleeCombo != null && meleeCombo.IsInAttackRecovery;
        public bool RawIsDashing => dashController != null && dashController.IsDashing;

        public CharacterStates.MovementStates RawMovementState =>
            (character != null && character.MovementState != null)
                ? character.MovementState.CurrentState
                : CharacterStates.MovementStates.Null;

        public CharacterStates.CharacterConditions RawConditionState =>
            (character != null && character.ConditionState != null)
                ? character.ConditionState.CurrentState
                : CharacterStates.CharacterConditions.Normal;

        private void Awake()
        {
            character ??= GetComponent<Character>() ?? GetComponentInChildren<Character>();
            parryController ??= GetComponent<KhiParryController>();
            hitStunController ??= GetComponent<KhiHitStunController>();
            dashController ??= GetComponent<KhiDashController>();
            downController ??= GetComponent<KhiDownController>();
            meleeCombo ??= GetComponent<KhiMeleeComboController>();

            _enteredAt = Time.time;
        }

        private void Update()
        {
            KhiPlayerState next = ResolveState();
            if (next == _currentState)
            {
                return;
            }

            _previousState = _currentState;
            _currentState = next;
            _enteredAt = Time.time;

            if (logStateChanges)
            {
                Debug.Log($"[KhiPlayerState] {_previousState} -> {_currentState} t={Time.time:F3}");
            }

            StateChanged?.Invoke(_previousState, _currentState);
        }

        private KhiPlayerState ResolveState()
        {
            // 1. Defeated
            if (downController != null && downController.IsDefeated)
            {
                return KhiPlayerState.Defeated;
            }

            if (character != null
                && character.ConditionState != null
                && character.ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead)
            {
                return KhiPlayerState.Defeated;
            }

            // 2. Down
            if (downController != null && downController.IsDown)
            {
                return KhiPlayerState.Down;
            }

            // 3. Hurt (HitStun만, PostHitIFrame 제외)
            if (hitStunController != null && hitStunController.IsStunned)
            {
                return KhiPlayerState.Hurt;
            }

            // 4. Parry (Window + FailureRecovery만, Cooldown 제외)
            if (parryController != null
                && (parryController.IsParryWindowActive || parryController.IsParryRecovering))
            {
                return KhiPlayerState.Parry;
            }

            // 5. Dash
            if (dashController != null && dashController.IsDashing)
            {
                return KhiPlayerState.Dash;
            }

            // 6. Attack (startup/active/recovery 모두 포함)
            if (meleeCombo != null && (meleeCombo.IsAttacking || meleeCombo.IsInAttackRecovery))
            {
                return KhiPlayerState.Attack;
            }

            // 7. Move (TDE MovementState 기준)
            if (character != null && character.MovementState != null)
            {
                switch (character.MovementState.CurrentState)
                {
                    case CharacterStates.MovementStates.Walking:
                    case CharacterStates.MovementStates.Running:
                    case CharacterStates.MovementStates.Jumping:
                    case CharacterStates.MovementStates.Falling:
                    case CharacterStates.MovementStates.Crawling:
                    case CharacterStates.MovementStates.Crouching:
                        return KhiPlayerState.Move;
                }
            }

            // 8. 폴백
            return KhiPlayerState.Idle;
        }
    }
}

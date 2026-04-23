using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    [AddComponentMenu("Lost Memory/Test Khi/Khi Dash Controller")]
    public class KhiDashController : CharacterDash2D
    {
        [SerializeField] private KhiPlayerAim aim;
        [SerializeField] private KhiMeleeComboController meleeCombo;
        [SerializeField] private bool allowDashDuringAttack = false;
        [SerializeField] private bool allowDashDuringAttackRecovery = false;
        [SerializeField] private bool logBlockedDashToConsole = false;

        public bool IsDashing => _dashing;
        public bool AllowDashDuringAttack => allowDashDuringAttack;
        public bool AllowDashDuringAttackRecovery => allowDashDuringAttackRecovery;

        protected override void Initialization()
        {
            base.Initialization();
            aim ??= GetComponent<KhiPlayerAim>();
            meleeCombo ??= GetComponent<KhiMeleeComboController>();
            DashMode = DashModes.Script;
        }

        protected override void HandleInput()
        {
            if (!AbilityAuthorized
                || !Cooldown.Ready()
                || (_condition.CurrentState != CharacterStates.CharacterConditions.Normal))
            {
                return;
            }

            if (_inputManager.DashButton.State.CurrentState != MMInput.ButtonStates.ButtonDown)
            {
                return;
            }

            if (ShouldBlockDash())
            {
                if (logBlockedDashToConsole)
                {
                    Debug.Log("[KhiDash] Dash input ignored while attack is active.");
                }

                return;
            }

            DashStart();
        }

        public override void DashStart()
        {
            DashMode = DashModes.Script;
            DashDirection = ResolveDashDirection();
            base.DashStart();
        }

        private bool ShouldBlockDash()
        {
            if (meleeCombo == null)
            {
                return false;
            }

            if (!allowDashDuringAttack && meleeCombo.IsAttacking)
            {
                return true;
            }

            if (!allowDashDuringAttackRecovery && meleeCombo.IsInAttackRecovery)
            {
                return true;
            }

            return false;
        }

        private Vector3 ResolveDashDirection()
        {
            Vector2 movement = _inputManager != null ? _inputManager.PrimaryMovement : Vector2.zero;
            if (movement.sqrMagnitude > Mathf.Epsilon)
            {
                return movement.normalized;
            }

            Vector2 aimDirection = aim != null ? aim.GetAimDirection() : Vector2.right;
            if (aimDirection.sqrMagnitude > Mathf.Epsilon)
            {
                return aimDirection.normalized;
            }

            return Vector3.right;
        }
    }
}

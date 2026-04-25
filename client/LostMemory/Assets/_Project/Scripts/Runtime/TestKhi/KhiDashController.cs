using System.Collections;
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

        /// <summary>
        /// CL-067: Finisher lunge 용. DashStart()를 호출하지 않아 Cooldown/Feedback/무적 등 side effect 없음.
        /// 이동 로직만 TDE `_controller.MovePosition`으로 재사용 → 물리 충돌 안전.
        /// </summary>
        public void PerformLunge(Vector2 direction, float distance, float duration)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon || distance <= 0f || duration <= 0f)
            {
                return;
            }

            StartCoroutine(LungeCoroutine(direction.normalized, distance, duration));
        }

        private IEnumerator LungeCoroutine(Vector2 direction, float distance, float duration)
        {
            Vector3 origin = transform.position;
            Vector3 destination = origin + (Vector3)(direction * distance);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // ease-out: 빠르게 시작 → 감속하며 도착
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                Vector3 newPos = Vector3.Lerp(origin, destination, eased);
                if (_controller != null)
                {
                    _controller.MovePosition(newPos);
                }
                else
                {
                    transform.position = newPos;
                }
                yield return null;
            }
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

using System;
using System.Collections;
using LostMemory.Combat;
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
        [Tooltip("CL-108: DashCooldown multiplier 조회용 (질풍 장화). 같은 GameObject 의 컴포넌트.")]
        [SerializeField] private PlayerStatModifierContainer statContainer;

        // CL-108: Initialization 시점의 base cooldown 캐시. multiplier 는 매 DashStart 마다 적용.
        private float _baseCooldownDuration;
        private bool _wasDashingLastFrame;

        public bool IsDashing => _dashing;
        public bool AllowDashDuringAttack => allowDashDuringAttack;
        public bool AllowDashDuringAttackRecovery => allowDashDuringAttackRecovery;

        /// <summary>CL-108: 대시 종료 직후 발화 (LateUpdate IsDashing 변화 감지). 추적자의 망토 등이 구독.</summary>
        public event Action OnDashEnded;

        protected override void Initialization()
        {
            base.Initialization();
            aim ??= GetComponent<KhiPlayerAim>();
            meleeCombo ??= GetComponent<KhiMeleeComboController>();
            if (statContainer == null) statContainer = GetComponent<PlayerStatModifierContainer>();
            DashMode = DashModes.Script;
            _baseCooldownDuration = Cooldown != null ? Cooldown.ConsumptionDuration : 0f;
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

            // CL-108: 질풍 장화 (DashCooldown multiplier). 음수 magnitude 면 단축.
            if (statContainer != null && Cooldown != null)
            {
                float mul = statContainer.GetTotalMultiplier(StatId.DashCooldown);
                Cooldown.ConsumptionDuration = _baseCooldownDuration * mul;
            }

            base.DashStart();
        }

        // CL-108: 대시 종료 감지. TDE CharacterDash2D 의 DashStop() virtual 미확인이라
        // LateUpdate IsDashing 변화 감지로 안전하게 처리.
        private void LateUpdate()
        {
            if (_wasDashingLastFrame && !IsDashing)
            {
                OnDashEnded?.Invoke();
            }
            _wasDashingLastFrame = IsDashing;
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

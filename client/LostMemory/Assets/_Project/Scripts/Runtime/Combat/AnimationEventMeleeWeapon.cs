using System.Collections;
using LostMemory.Combat.Telegraph;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    [AddComponentMenu("Lost Memory/Combat/Animation Event Melee Weapon")]
    public sealed class AnimationEventMeleeWeapon : MeleeWeapon
    {
        [SerializeField] private bool requireActiveWeaponState = true;
        [SerializeField] private bool lockControlsUntilRecovery = true;
        [SerializeField] private float controlLockDuration = 3.5f;
        [SerializeField] private bool showTelegraphBeforeHit = true;
        [SerializeField] private AttackTelegraph2DView telegraphView;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.18f, 0.04f, 0.34f);
        [SerializeField] private float telegraphDuration;
        [SerializeField] private Vector2 telegraphSizePadding = Vector2.zero;

        private Coroutine animationEventDamageCoroutine;
        private bool controlLockActive;
        private float controlLockUntil;

        public override void WeaponUse()
        {
            EnsureAnimationEventReceiver();
            StartControlLock();
            ShowTelegraph();
            ApplyRecoil();
            TriggerWeaponUsedFeedback();
        }

        public override void SetOwner(Character newOwner, CharacterHandleWeapon handleWeapon)
        {
            base.SetOwner(newOwner, handleWeapon);
            EnsureAnimationEventReceiver();
        }

        public void TriggerDamageFromAnimationEvent()
        {
            HideTelegraph();

            if (!CanAcceptAnimationEvent() || _attackInProgress)
            {
                return;
            }

            animationEventDamageCoroutine = StartCoroutine(DamageFromAnimationEvent());
        }

        public void EnableDamageFromAnimationEvent()
        {
            HideTelegraph();

            if (!CanAcceptAnimationEvent() || _attackInProgress)
            {
                return;
            }

            _attackInProgress = true;
            EnableDamageArea();
        }

        public void DisableDamageFromAnimationEvent()
        {
            StopAnimationEventDamage();
        }

        public override void Interrupt()
        {
            base.Interrupt();
            StopAnimationEventDamage();
            ReleaseControlLock();
            HideTelegraph();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopAnimationEventDamage();
            ReleaseControlLock();
            HideTelegraph();
        }

        protected override void Update()
        {
            base.Update();
            UpdateControlLock();
        }

        private IEnumerator DamageFromAnimationEvent()
        {
            _attackInProgress = true;
            EnableDamageArea();
            yield return new WaitForSeconds(ActiveDuration);
            DisableDamageArea();
            _attackInProgress = false;
            animationEventDamageCoroutine = null;
        }

        private bool CanAcceptAnimationEvent()
        {
            if (!isActiveAndEnabled || !WeaponCurrentlyActive)
            {
                return false;
            }

            if (!requireActiveWeaponState || WeaponState == null)
            {
                return true;
            }

            WeaponStates state = WeaponState.CurrentState;
            return state == WeaponStates.WeaponDelayBeforeUse
                   || state == WeaponStates.WeaponUse
                   || state == WeaponStates.WeaponDelayBetweenUses;
        }

        private void StopAnimationEventDamage()
        {
            if (animationEventDamageCoroutine != null)
            {
                StopCoroutine(animationEventDamageCoroutine);
                animationEventDamageCoroutine = null;
            }

            DisableDamageArea();
            _attackInProgress = false;
        }

        private void StartControlLock()
        {
            if (!lockControlsUntilRecovery || controlLockDuration <= 0f)
            {
                return;
            }

            controlLockActive = true;
            controlLockUntil = Time.time + controlLockDuration;
            ApplyControlLock();
        }

        private void UpdateControlLock()
        {
            if (!controlLockActive)
            {
                return;
            }

            if (Time.time >= controlLockUntil)
            {
                ReleaseControlLock();
                HideTelegraph();
                return;
            }

            ApplyControlLock();
        }

        private void ApplyControlLock()
        {
            if (_characterMovement != null)
            {
                _characterMovement.SetMovement(Vector2.zero);
                _characterMovement.MovementForbidden = true;
            }

            if (_weaponAim != null)
            {
                _weaponAim.AimControlActive = false;
            }
        }

        private void ReleaseControlLock()
        {
            if (!controlLockActive)
            {
                return;
            }

            controlLockActive = false;

            if (_characterMovement != null)
            {
                _characterMovement.MovementForbidden = false;
            }

            if (_weaponAim != null)
            {
                _weaponAim.AimControlActive = true;
            }
        }

        private void ShowTelegraph()
        {
            if (!showTelegraphBeforeHit)
            {
                return;
            }

            EnsureTelegraphView();
            telegraphView?.Show(BuildTelegraphRequest());
        }

        private void HideTelegraph()
        {
            telegraphView?.Hide();
        }

        private void EnsureTelegraphView()
        {
            if (telegraphView != null)
            {
                return;
            }

            GameObject telegraphHost = Owner != null ? Owner.gameObject : gameObject;
            telegraphView = telegraphHost.GetComponent<AttackTelegraph2DView>();
            if (telegraphView == null)
            {
                telegraphView = telegraphHost.AddComponent<AttackTelegraph2DView>();
            }
        }

        private AttackTelegraphRequest2D BuildTelegraphRequest()
        {
            Vector2 worldSize = ResolveTelegraphSize();

            return new AttackTelegraphRequest2D
            {
                Shape = AttackTelegraphShape2D.Box,
                Center = transform.TransformPoint(AreaOffset),
                Direction = ResolveTelegraphDirection(),
                Size = worldSize + telegraphSizePadding,
                Color = telegraphColor,
                Duration = ResolveTelegraphDuration()
            };
        }

        private Vector2 ResolveTelegraphDirection()
        {
            Vector2 direction = transform.TransformVector(Vector3.right);
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        }

        private Vector2 ResolveTelegraphSize()
        {
            Vector3 scale = transform.lossyScale;
            return new Vector2(
                Mathf.Abs(AreaSize.x * scale.x),
                Mathf.Abs(AreaSize.y * scale.y));
        }

        private float ResolveTelegraphDuration()
        {
            if (telegraphDuration > 0f)
            {
                return telegraphDuration;
            }

            if (controlLockDuration > 0f)
            {
                return controlLockDuration;
            }

            return Mathf.Max(0.01f, TimeBetweenUses);
        }

        private void EnsureAnimationEventReceiver()
        {
            if (_ownerAnimator == null)
            {
                return;
            }

            MeleeAnimationEventRelay relay = _ownerAnimator.GetComponent<MeleeAnimationEventRelay>();
            if (relay == null)
            {
                relay = _ownerAnimator.gameObject.AddComponent<MeleeAnimationEventRelay>();
            }

            relay.Configure(this, CharacterHandleWeapon);
        }
    }
}

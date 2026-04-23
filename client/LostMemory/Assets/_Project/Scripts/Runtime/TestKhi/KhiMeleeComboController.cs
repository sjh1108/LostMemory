using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    [DefaultExecutionOrder(50)]
    public class KhiMeleeComboController : MonoBehaviour
    {
        [SerializeField] private KhiPlayerAim aim;
        [SerializeField] private KhiMeleeHitbox hitbox;
        [SerializeField] private KhiAttackVisualPresenter visualPresenter;
        [SerializeField] private KhiDashController dash;
        [SerializeField] private float baseDamage = 10f;
        [SerializeField, Min(0f)] private float comboInputWindow = 0.6f;
        [SerializeField, Min(0f)] private float minimumChainInputDelay = 0.12f;
        [SerializeField, Min(0f)] private float inputBufferDuration = 0.25f;
        [SerializeField] private bool allowInputDuringRecovery = true;
        [SerializeField] private bool allowAttackDuringDash = false;
        [SerializeField] private bool bufferAttackDuringDash = false;
        [SerializeField] private bool disableTdeHandleWeapon = true;
        [SerializeField] private bool logHitsToConsole = false;
        [SerializeField] private KhiMeleeAttackStep[] attackSteps;

        private readonly HashSet<Health> _alreadyHitThisSwing = new HashSet<Health>();
        private readonly List<Health> _hitsThisSample = new List<Health>(8);

        private CharacterHandleWeapon _tdeHandleWeapon;
        private bool _isAttacking;
        private bool _isInAttackRecovery;
        private int _nextComboStep = 1;
        private int _currentComboStep;
        private int _sequenceId;
        private float _comboExpiresAt = -1f;
        private float _chainInputAllowedAt = -1f;
        private float _bufferedAttackExpiresAt = -1f;

        public event Action<KhiAttackRequest, KhiMeleeAttackStep> AttackStarted;
        public event Action<KhiAttackRequest, KhiMeleeAttackStep> AttackActiveStarted;
        public event Action<KhiAttackRequest, KhiMeleeAttackStep> AttackActiveEnded;
        public event Action<KhiAttackRequest, KhiMeleeAttackStep, Health> TargetHit;
        public event Action<KhiAttackRequest, KhiMeleeAttackStep> FinisherHit;

        public bool IsAttacking => _isAttacking;
        public bool IsInAttackRecovery => _isInAttackRecovery;
        public bool BlocksDash => _isAttacking;

        private void Awake()
        {
            aim ??= GetComponent<KhiPlayerAim>();
            hitbox ??= GetComponent<KhiMeleeHitbox>();
            visualPresenter ??= GetComponent<KhiAttackVisualPresenter>();
            dash ??= GetComponent<KhiDashController>();
            _tdeHandleWeapon = GetComponent<CharacterHandleWeapon>();
            EnsureDefaultSteps();
        }

        private IEnumerator Start()
        {
            if (!disableTdeHandleWeapon)
            {
                yield break;
            }

            yield return null;
            DisableTdeWeaponHandling();
        }

        private void Update()
        {
            if (WasAttackPressedThisFrame())
            {
                RequestAttack();
            }

            if (!_isAttacking && _nextComboStep != 1 && Time.time > _comboExpiresAt)
            {
                _nextComboStep = 1;
            }
        }

        public void RequestAttack()
        {
            if (ShouldBlockAttackForDash())
            {
                return;
            }

            if (_isAttacking)
            {
                TryBufferAttack();
                return;
            }

            if (_nextComboStep != 1 && Time.time > _comboExpiresAt)
            {
                _nextComboStep = 1;
            }

            StartCoroutine(RunAttack(_nextComboStep));
        }

        private IEnumerator RunAttack(int comboStep)
        {
            _isAttacking = true;
            _isInAttackRecovery = false;
            ClearBufferedAttack();
            _alreadyHitThisSwing.Clear();

            KhiMeleeAttackStep step = GetStep(comboStep);
            _currentComboStep = step.ComboStep;
            KhiAttackDirection direction = aim != null ? aim.GetCardinalDirection() : KhiAttackDirection.Right;
            Vector2 directionVector = KhiPlayerAim.ToVector(direction);
            KhiAttackRequest request = new KhiAttackRequest
            {
                SequenceId = ++_sequenceId,
                ComboStep = step.ComboStep,
                Direction = direction,
                DirectionVector = directionVector,
                Origin = transform.position,
                StartedAt = Time.time,
                Attacker = gameObject
            };

            AttackStarted?.Invoke(request, step);
            _chainInputAllowedAt = Mathf.Max(
                request.StartedAt + minimumChainInputDelay,
                request.StartedAt + step.StartupDuration + step.ActiveDuration * 0.5f);

            if (step.StartupDuration > 0f)
            {
                yield return new WaitForSeconds(step.StartupDuration);
            }

            bool hitAnyTarget = false;
            float activeEndsAt = Time.time + step.ActiveDuration;
            AttackActiveStarted?.Invoke(request, step);

            while (Time.time < activeEndsAt)
            {
                KhiAttackRequest sampleRequest = request;
                sampleRequest.Origin = transform.position;
                int sampledHitCount = hitbox != null ? hitbox.Sample(sampleRequest, step, baseDamage * step.DamageMultiplier, _alreadyHitThisSwing, _hitsThisSample) : 0;
                hitAnyTarget |= sampledHitCount > 0;

                for (int i = 0; i < _hitsThisSample.Count; i++)
                {
                    TargetHit?.Invoke(sampleRequest, step, _hitsThisSample[i]);
                }

                yield return null;
            }

            AttackActiveEnded?.Invoke(request, step);
            hitbox?.HideRuntimePreview();

            if (hitAnyTarget && step.ComboStep == 3)
            {
                FinisherHit?.Invoke(request, step);
                if (logHitsToConsole)
                {
                    Debug.Log($"[KhiMelee] Finisher hit sequence={request.SequenceId}");
                }
            }

            _isInAttackRecovery = true;
            float recoveryEndsAt = Time.time + step.RecoveryDuration;
            while (Time.time < recoveryEndsAt)
            {
                yield return null;
            }

            bool shouldChainBufferedAttack = HasValidBufferedAttack(step.ComboStep);
            ClearBufferedAttack();
            AdvanceCombo(step.ComboStep);
            _isAttacking = false;
            _isInAttackRecovery = false;
            _currentComboStep = 0;

            if (shouldChainBufferedAttack)
            {
                RequestAttack();
            }
        }

        private bool ShouldBlockAttackForDash()
        {
            if (dash == null || !dash.IsDashing || allowAttackDuringDash)
            {
                return false;
            }

            if (bufferAttackDuringDash && _isAttacking)
            {
                TryBufferAttack();
            }

            return true;
        }

        private void TryBufferAttack()
        {
            if (!allowInputDuringRecovery || Time.time < _chainInputAllowedAt)
            {
                return;
            }

            if (_currentComboStep >= 3)
            {
                return;
            }

            _bufferedAttackExpiresAt = Time.time + inputBufferDuration;
        }

        private bool HasValidBufferedAttack(int completedStep)
        {
            return completedStep < 3 && _bufferedAttackExpiresAt >= Time.time;
        }

        private void ClearBufferedAttack()
        {
            _bufferedAttackExpiresAt = -1f;
        }

        private void AdvanceCombo(int completedStep)
        {
            if (completedStep >= 3)
            {
                _nextComboStep = 1;
                _comboExpiresAt = -1f;
                return;
            }

            _nextComboStep = completedStep + 1;
            _comboExpiresAt = Time.time + comboInputWindow;
        }

        private KhiMeleeAttackStep GetStep(int comboStep)
        {
            EnsureDefaultSteps();

            for (int i = 0; i < attackSteps.Length; i++)
            {
                if (attackSteps[i] != null && attackSteps[i].ComboStep == comboStep)
                {
                    return attackSteps[i];
                }
            }

            return attackSteps[0];
        }

        private void DisableTdeWeaponHandling()
        {
            _tdeHandleWeapon ??= GetComponent<CharacterHandleWeapon>();
            if (_tdeHandleWeapon == null)
            {
                return;
            }

            _tdeHandleWeapon.ChangeWeapon(null, string.Empty);
            _tdeHandleWeapon.InitialWeapon = null;
            _tdeHandleWeapon.PermitAbility(false);
        }

        private static bool WasAttackPressedThisFrame()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonWest.wasPressedThisFrame;
        }

        private void EnsureDefaultSteps()
        {
            if (attackSteps != null && attackSteps.Length == 3 && attackSteps[0] != null && attackSteps[1] != null && attackSteps[2] != null)
            {
                return;
            }

            attackSteps = new[]
            {
                CreateStep1(),
                CreateStep2(),
                CreateStep3()
            };
        }

        private static KhiMeleeAttackStep CreateStep1()
        {
            return new KhiMeleeAttackStep
            {
                ComboStep = 1,
                DamageMultiplier = 1f,
                StartupDuration = 0.1f,
                ActiveDuration = 0.1f,
                RecoveryDuration = 0.15f,
                AnimatorTrigger = "Attack_1",
                Right = new KhiDirectionalHitbox(new Vector2(1.05f, -0.2f), new Vector2(1.7f, 1.1f)),
                Up = new KhiDirectionalHitbox(new Vector2(0.2f, 1.05f), new Vector2(1.1f, 1.7f)),
                Left = new KhiDirectionalHitbox(new Vector2(-1.05f, 0.2f), new Vector2(1.7f, 1.1f)),
                Down = new KhiDirectionalHitbox(new Vector2(-0.2f, -1.05f), new Vector2(1.1f, 1.7f))
            };
        }

        private static KhiMeleeAttackStep CreateStep2()
        {
            return new KhiMeleeAttackStep
            {
                ComboStep = 2,
                DamageMultiplier = 1.1f,
                StartupDuration = 0.12f,
                ActiveDuration = 0.12f,
                RecoveryDuration = 0.2f,
                AnimatorTrigger = "Attack_2",
                Right = new KhiDirectionalHitbox(new Vector2(1.05f, 0.2f), new Vector2(1.8f, 1.2f)),
                Up = new KhiDirectionalHitbox(new Vector2(-0.2f, 1.05f), new Vector2(1.2f, 1.8f)),
                Left = new KhiDirectionalHitbox(new Vector2(-1.05f, -0.2f), new Vector2(1.8f, 1.2f)),
                Down = new KhiDirectionalHitbox(new Vector2(0.2f, -1.05f), new Vector2(1.2f, 1.8f))
            };
        }

        private static KhiMeleeAttackStep CreateStep3()
        {
            return new KhiMeleeAttackStep
            {
                ComboStep = 3,
                DamageMultiplier = 1.5f,
                StartupDuration = 0.2f,
                ActiveDuration = 0.2f,
                RecoveryDuration = 0.3f,
                AnimatorTrigger = "Attack_3",
                Right = new KhiDirectionalHitbox(new Vector2(1.25f, 0f), new Vector2(2.5f, 1.8f)),
                Up = new KhiDirectionalHitbox(new Vector2(0f, 1.25f), new Vector2(1.8f, 2.5f)),
                Left = new KhiDirectionalHitbox(new Vector2(-1.25f, 0f), new Vector2(2.5f, 1.8f)),
                Down = new KhiDirectionalHitbox(new Vector2(0f, -1.25f), new Vector2(1.8f, 2.5f))
            };
        }
    }
}

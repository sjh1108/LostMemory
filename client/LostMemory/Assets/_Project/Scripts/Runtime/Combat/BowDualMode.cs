using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// 활 — 좌클릭(ShootButton, CharacterHandleWeapon 경유)은 단발 / 우클릭(SecondaryShootButton,
    /// 이 컴포넌트가 직접 폴링)을 누르고 있는 동안 TriggerMode 를 Auto + TimeBetweenUses 짧게로 전환.
    /// 우클릭이 떼지면 단발 모드로 복귀한다.
    /// 마나 소모(연속샷): 기본 0 — 밸런싱 시 RapidShotManaCostPerShot 만 키우면 활성화된다.
    /// </summary>
    [RequireComponent(typeof(ProjectileWeapon))]
    [AddComponentMenu("Lost Memory/Combat/Bow Dual Mode")]
    public sealed class BowDualMode : MonoBehaviour
    {
        [Header("Single Shot (Left Click)")]
        [SerializeField, Min(0.05f)] private float singleShotInterval = 0.5f;

        [Header("Rapid Shot (Right Click Hold)")]
        [SerializeField, Min(0.02f)] private float rapidShotInterval = 0.12f;
        [SerializeField, Min(0)] private int rapidShotManaCostPerShot = 0;

        [Header("Input")]
        [SerializeField] private string playerId = "Player1";

        private ProjectileWeapon _weapon;
        private InputManager _inputManager;
        private PlayerMana _playerMana;
        private bool _rapidActive;
        private float _nextRapidCostTime;

        private void Awake()
        {
            _weapon = GetComponent<ProjectileWeapon>();
            ApplyMode(rapid: false);
        }

        private void OnDisable()
        {
            StopRapidIfActive();
        }

        private void Update()
        {
            if (_weapon == null) return;
            EnsureInputManager();
            if (_inputManager == null) return;

            MMInput.IMButton btn = _inputManager.SecondaryShootButton;
            if (btn == null) return;

            MMInput.ButtonStates state = btn.State.CurrentState;
            bool pressed = state == MMInput.ButtonStates.ButtonDown
                           || state == MMInput.ButtonStates.ButtonPressed;

            if (pressed && !_rapidActive)
            {
                StartRapid();
            }
            else if (!pressed && _rapidActive)
            {
                StopRapidIfActive();
            }

            if (_rapidActive && rapidShotManaCostPerShot > 0)
            {
                TickManaCost();
            }
        }

        private void StartRapid()
        {
            _rapidActive = true;
            ApplyMode(rapid: true);
            _weapon.WeaponInputStart();
            _nextRapidCostTime = Time.time;
        }

        private void StopRapidIfActive()
        {
            if (!_rapidActive) return;
            _rapidActive = false;
            if (_weapon != null) _weapon.WeaponInputStop();
            ApplyMode(rapid: false);
        }

        private void TickManaCost()
        {
            if (Time.time < _nextRapidCostTime) return;
            EnsurePlayerMana();
            if (_playerMana == null) return;
            if (!_playerMana.Consume(rapidShotManaCostPerShot))
            {
                StopRapidIfActive();
                return;
            }
            _nextRapidCostTime = Time.time + rapidShotInterval;
        }

        private void ApplyMode(bool rapid)
        {
            if (_weapon == null) return;
            _weapon.TriggerMode = rapid ? Weapon.TriggerModes.Auto : Weapon.TriggerModes.SemiAuto;
            _weapon.TimeBetweenUses = rapid ? rapidShotInterval : singleShotInterval;
        }

        private void EnsureInputManager()
        {
            if (_inputManager != null) return;
            InputManager[] managers = FindObjectsByType<InputManager>(FindObjectsSortMode.None);
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i].PlayerID == playerId)
                {
                    _inputManager = managers[i];
                    return;
                }
            }
            if (managers.Length > 0) _inputManager = managers[0];
        }

        private void EnsurePlayerMana()
        {
            if (_playerMana != null) return;
            _playerMana = GetComponentInParent<PlayerMana>();
        }
    }
}

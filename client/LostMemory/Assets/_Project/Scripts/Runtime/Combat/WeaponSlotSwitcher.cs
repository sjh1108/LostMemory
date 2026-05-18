using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// SwitchWeaponButton(기본: Q) 을 ButtonDown 감지하여 보유 무기 사이를 순환 전환.
    /// CharacterHandleWeapon 하나의 활성 무기를 ChangeWeapon 으로 교체하므로 한 번에 하나만 활성된다.
    /// </summary>
    [RequireComponent(typeof(CharacterHandleWeapon))]
    [AddComponentMenu("Lost Memory/Combat/Weapon Slot Switcher")]
    public sealed class WeaponSlotSwitcher : MonoBehaviour
    {
        [Tooltip("보유 무기 prefab 들. 첫 번째 항목이 시작 무기로 장착된다.")]
        [SerializeField] private List<Weapon> weapons = new List<Weapon>();

        [SerializeField] private string playerId = "Player1";
        [SerializeField] private string weaponIdPrefix = "Slot";

        [Tooltip("Start 시점에 첫 무기로 자동 장착. 끄면 CharacterHandleWeapon 의 InitialWeapon 사용.")]
        [SerializeField] private bool applyOnStart = true;

        private CharacterHandleWeapon _handle;
        private InputManager _inputManager;
        private int _currentIndex;

        private void Awake()
        {
            _handle = GetComponent<CharacterHandleWeapon>();
        }

        private void Start()
        {
            if (!applyOnStart || weapons.Count == 0 || weapons[0] == null) return;
            _currentIndex = 0;
            _handle.ChangeWeapon(weapons[0], BuildWeaponId(0));
        }

        private void Update()
        {
            EnsureInputManager();
            if (_inputManager == null) return;

            MMInput.IMButton btn = _inputManager.SwitchWeaponButton;
            if (btn == null) return;

            if (btn.State.CurrentState == MMInput.ButtonStates.ButtonDown)
            {
                Cycle();
            }
        }

        private void Cycle()
        {
            if (weapons.Count <= 1) return;
            int next = (_currentIndex + 1) % weapons.Count;
            if (weapons[next] == null) return;
            _currentIndex = next;
            _handle.ChangeWeapon(weapons[_currentIndex], BuildWeaponId(_currentIndex));
        }

        private string BuildWeaponId(int index) => $"{weaponIdPrefix}{index}";

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
    }
}

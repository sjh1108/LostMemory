using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    [DefaultExecutionOrder(-1000)]
    public class TestKhiWeaponRuntimeFix : MonoBehaviour
    {
        [SerializeField] private bool initializeComboWeapon = true;
        [SerializeField] private bool disableReticle = true;

        private ComboWeapon _comboWeapon;
        private WeaponAim _weaponAim;

        private void Awake()
        {
            ApplyFixes();
        }

        private void OnEnable()
        {
            ApplyFixes();
        }

        private void Start()
        {
            ApplyFixes();
        }

        private void LateUpdate()
        {
            if (_comboWeapon != null && (_comboWeapon.Weapons == null || _comboWeapon.Weapons.Length == 0))
            {
                ApplyFixes();
            }
        }

        private void ApplyFixes()
        {
            if (disableReticle)
            {
                DisableReticle();
            }

            if (initializeComboWeapon)
            {
                InitializeComboWeapon();
            }
        }

        private void DisableReticle()
        {
            _weaponAim ??= GetComponent<WeaponAim>();
            if (_weaponAim == null)
            {
                return;
            }

            _weaponAim.ReticleType = WeaponAim.ReticleTypes.None;
            _weaponAim.Reticle = null;
            _weaponAim.ReticleAtMousePosition = false;
            _weaponAim.ReplaceMousePointer = false;
            _weaponAim.DisplayReticle = false;
            _weaponAim.MoveCameraTargetTowardsReticle = false;
        }

        private void InitializeComboWeapon()
        {
            _comboWeapon ??= GetComponent<ComboWeapon>();
            if (_comboWeapon == null)
            {
                return;
            }

            Weapon[] weapons = GetComponents<Weapon>();
            if (weapons.Length == 0)
            {
                return;
            }

            _comboWeapon.Weapons = weapons;
            foreach (Weapon weapon in weapons)
            {
                if (weapon == null)
                {
                    continue;
                }

                if (weapon.WeaponState == null)
                {
                    weapon.Initialization();
                }

                if (weapon.CharacterHandleWeapon != null)
                {
                    _comboWeapon.OwnerCharacterHandleWeapon = weapon.CharacterHandleWeapon;
                    break;
                }
            }
        }
    }
}

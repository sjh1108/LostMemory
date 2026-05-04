using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Melee Animation Event Relay")]
    public sealed class MeleeAnimationEventRelay : MonoBehaviour
    {
        [SerializeField] private AnimationEventMeleeWeapon targetWeapon;
        [SerializeField] private CharacterHandleWeapon characterHandleWeapon;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Reset()
        {
            ResolveReferences();
        }

        public void Configure(AnimationEventMeleeWeapon configuredTargetWeapon, CharacterHandleWeapon configuredHandleWeapon)
        {
            targetWeapon = configuredTargetWeapon;
            characterHandleWeapon = configuredHandleWeapon;
        }

        public void LM_MeleeHit()
        {
            ResolveReferences();
            targetWeapon?.TriggerDamageFromAnimationEvent();
        }

        public void LM_MeleeDamageOn()
        {
            ResolveReferences();
            targetWeapon?.EnableDamageFromAnimationEvent();
        }

        public void LM_MeleeDamageOff()
        {
            ResolveReferences();
            targetWeapon?.DisableDamageFromAnimationEvent();
        }

        private void ResolveReferences()
        {
            if (targetWeapon != null)
            {
                return;
            }

            if (characterHandleWeapon == null)
            {
                characterHandleWeapon = GetComponentInParent<CharacterHandleWeapon>();
            }

            if (characterHandleWeapon != null
                && characterHandleWeapon.CurrentWeapon is AnimationEventMeleeWeapon currentWeapon)
            {
                targetWeapon = currentWeapon;
                return;
            }

            targetWeapon = GetComponentInParent<AnimationEventMeleeWeapon>();
        }
    }
}

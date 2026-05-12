using MoreMountains.Tools;
using UnityEngine;

namespace LostMemory.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Animation Event SFX Relay")]
    public sealed class AnimationEventSfxRelay : MonoBehaviour
    {
        [SerializeField] private AudioClip attackSwingSfx;
        [SerializeField, Range(0f, 1f)] private float attackSwingVolume = 1f;
        [SerializeField, Range(0.1f, 3f)] private float minPitch = 1f;
        [SerializeField, Range(0.1f, 3f)] private float maxPitch = 1f;
        [SerializeField] private bool fallbackWithoutSoundManager = true;

        public void LM_AttackSwingSfx()
        {
            if (attackSwingSfx == null)
            {
                return;
            }

            float pitch = Mathf.Approximately(minPitch, maxPitch)
                ? minPitch
                : Random.Range(minPitch, maxPitch);

            if (MMSoundManager.HasInstance && MMSoundManager.Current != null)
            {
                MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
                options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
                options.Location = transform.position;
                options.Volume = attackSwingVolume;
                options.Pitch = pitch;
                options.Loop = false;

                MMSoundManagerSoundPlayEvent.Trigger(attackSwingSfx, options);
                return;
            }

            if (fallbackWithoutSoundManager)
            {
                AudioSource.PlayClipAtPoint(attackSwingSfx, transform.position, attackSwingVolume);
            }
        }
    }
}

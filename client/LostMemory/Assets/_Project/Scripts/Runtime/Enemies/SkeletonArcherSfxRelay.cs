using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Skeleton Archer SFX Relay")]
    public sealed class SkeletonArcherSfxRelay : MonoBehaviour, MMEventListener<AIStateEvent>
    {
        [SerializeField] private AIBrain brain;
        [SerializeField] private string bowPullStateName = "AimReady";
        [SerializeField] private string bowShootStateName = "Attack";
        [SerializeField] private AudioClip bowPullSfx;
        [SerializeField] private AudioClip bowShootSfx;
        [SerializeField, Range(0f, 1f)] private float bowPullVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float bowShootVolume = 1f;
        [SerializeField, Range(0.1f, 3f)] private float minPitch = 1f;
        [SerializeField, Range(0.1f, 3f)] private float maxPitch = 1f;
        [SerializeField] private bool fallbackWithoutSoundManager = true;

        private void Reset()
        {
            RefreshReferences();
        }

        private void OnValidate()
        {
            minPitch = Mathf.Max(0.1f, minPitch);
            maxPitch = Mathf.Max(0.1f, maxPitch);
            if (maxPitch < minPitch)
            {
                maxPitch = minPitch;
            }

            RefreshReferences();
        }

        private void Awake()
        {
            RefreshReferences();
        }

        private void OnEnable()
        {
            RefreshReferences();
            this.MMEventStartListening<AIStateEvent>();
        }

        private void OnDisable()
        {
            this.MMEventStopListening<AIStateEvent>();
        }

        public void OnMMEvent(AIStateEvent stateEvent)
        {
            if (stateEvent.Brain != brain || stateEvent.EnterState == null)
            {
                return;
            }

            string enteringStateName = stateEvent.EnterState.StateName;
            if (enteringStateName == bowPullStateName)
            {
                PlaySfx(bowPullSfx, bowPullVolume);
                return;
            }

            if (enteringStateName == bowShootStateName)
            {
                PlaySfx(bowShootSfx, bowShootVolume);
            }
        }

        private void RefreshReferences()
        {
            brain ??= GetComponent<AIBrain>();
        }

        private void PlaySfx(AudioClip clip, float volume)
        {
            if (clip == null)
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
                options.Volume = volume;
                options.Pitch = pitch;
                options.Loop = false;

                MMSoundManagerSoundPlayEvent.Trigger(clip, options);
                return;
            }

            if (fallbackWithoutSoundManager)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, volume);
            }
        }
    }
}

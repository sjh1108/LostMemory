using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Stage 1 BGM Controller")]
    public sealed class Stage1BgmController : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] private AudioClip _stage1DungeonClip;
        [SerializeField] private AudioClip _stage1BossClip;

        [Header("Sound IDs")]
        [SerializeField] private int _stage1DungeonId = 1101;
        [SerializeField] private int _stage1BossId = 1102;

        [Header("Debug")]
        [SerializeField] private bool _logChanges;

        private static Stage1BgmController _instance;

        private AudioSource _fallbackSource;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                _instance.AbsorbConfigFrom(this);
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
            }
        }

        private void Start()
        {
            if (_instance == this)
            {
                ApplyForScene(SceneManager.GetActiveScene().name);
            }
        }

        private void OnDisable()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyForScene(scene.name);
        }

        private void ApplyForScene(string sceneName)
        {
            if (IsStage1BossScene(sceneName))
            {
                PlayManagedMusic(_stage1BossClip, _stage1BossId, sceneName);
                return;
            }

            if (IsStage1DungeonScene(sceneName))
            {
                PlayManagedMusic(_stage1DungeonClip, _stage1DungeonId, sceneName);
                return;
            }

            StopManagedMusic();
        }

        private static bool IsStage1BossScene(string sceneName)
        {
            return sceneName == "Dungeon_1F_Boss";
        }

        private static bool IsStage1DungeonScene(string sceneName)
        {
            switch (sceneName)
            {
                case "Dungeon_1F_1R":
                case "Dungeon_1F_2R":
                case "Dungeon_1F_3R":
                case "Dungeon_1F_4R":
                case "Dungeon_1F_Shop":
                    return true;
                default:
                    return false;
            }
        }

        private void PlayManagedMusic(AudioClip clip, int id, string sceneName)
        {
            if (clip == null)
            {
                StopManagedMusic();
                Debug.LogWarning($"[Stage1BgmController] Missing BGM clip for scene '{sceneName}'.", this);
                return;
            }

            if (IsAlreadyPlaying(clip, id))
            {
                return;
            }

            StopManagedMusic();

            if (MMSoundManager.HasInstance && MMSoundManager.Current != null)
            {
                MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
                options.ID = id;
                options.Loop = true;
                options.Location = Vector3.zero;
                options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Music;
                options.Persistent = true;

                MMSoundManagerSoundPlayEvent.Trigger(clip, options);

                if (_logChanges)
                {
                    Debug.Log($"[Stage1BgmController] Playing '{clip.name}' for '{sceneName}'.", this);
                }

                return;
            }

            PlayFallback(clip);
        }

        private bool IsAlreadyPlaying(AudioClip clip, int id)
        {
            if (_fallbackSource != null && _fallbackSource.clip == clip && _fallbackSource.isPlaying)
            {
                return true;
            }

            if (!MMSoundManager.HasInstance || MMSoundManager.Current == null)
            {
                return false;
            }

            AudioSource source = MMSoundManager.Current.FindByID(id);
            return source != null && source.clip == clip && source.isPlaying;
        }

        private void StopManagedMusic()
        {
            StopFallback();

            if (MMSoundManager.HasInstance && MMSoundManager.Current != null)
            {
                FreeSoundById(_stage1DungeonId);
                FreeSoundById(_stage1BossId);
            }
        }

        private void FreeSoundById(int id)
        {
            for (int i = 0; i < 4; i++)
            {
                AudioSource source = MMSoundManager.Current.FindByID(id);
                if (source == null)
                {
                    return;
                }

                MMSoundManager.Current.FreeSound(source);
            }
        }

        private void PlayFallback(AudioClip clip)
        {
            if (_fallbackSource == null)
            {
                _fallbackSource = gameObject.AddComponent<AudioSource>();
                _fallbackSource.playOnAwake = false;
                _fallbackSource.spatialBlend = 0f;
            }

            _fallbackSource.clip = clip;
            _fallbackSource.loop = true;
            _fallbackSource.Play();
        }

        private void StopFallback()
        {
            if (_fallbackSource == null)
            {
                return;
            }

            _fallbackSource.Stop();
            _fallbackSource.clip = null;
        }

        private void AbsorbConfigFrom(Stage1BgmController other)
        {
            if (_stage1DungeonClip == null)
            {
                _stage1DungeonClip = other._stage1DungeonClip;
            }

            if (_stage1BossClip == null)
            {
                _stage1BossClip = other._stage1BossClip;
            }

            _stage1DungeonId = other._stage1DungeonId;
            _stage1BossId = other._stage1BossId;
            _logChanges |= other._logChanges;
        }
    }
}

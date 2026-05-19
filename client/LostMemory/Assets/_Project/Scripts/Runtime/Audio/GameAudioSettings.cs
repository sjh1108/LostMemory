using System;
using System.Collections;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace LostMemory.Audio
{
    /// <summary>
    /// 게임 전체 오디오 카테고리 볼륨 설정 (Master / SFX / Music) 의 단일 진입점.
    ///
    /// 정책:
    /// - DontDestroyOnLoad 싱글톤. `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` 로
    ///   **자동 부트스트랩** — 씬에 GameObject 배치 / Inspector wiring 불필요.
    ///   첫 씬 로드 전에 자체적으로 GameObject 생성 + `Resources/MainMixer` 로드 + 그룹 자동 검색.
    /// - AudioMixer 의 exposed parameter 를 dB 로 설정 → AudioSource 가 mixer group 으로 라우팅되면
    ///   per-clip volume × mixer category 볼륨이 DSP 레벨에서 자동 곱셈.
    /// - PlayerPrefs 영구 저장 (게임 재시작 후에도 유지).
    /// - 슬라이더 값은 0..1 linear, 내부 변환 후 mixer 는 dB 로 받음.
    ///   사용자 청각이 log scale 이라 linear → dB(20·log10) 변환이 자연스러움.
    ///
    /// 사용:
    /// - 개발자 (Editor): `LostMemory → Audio Settings` 메뉴 → `AudioSettingsWindow` 로 슬라이더 조정
    /// - 사용자 UI (출시 빌드, 추후): 옵션 메뉴 슬라이더에 `AudioVolumeSliderBinder` 부착
    /// - 직접 코드 호출: `GameAudioSettings.Instance.SetSfxVolume(0..1)`
    /// - AudioSource 라우팅: KhiSfxBinder / UltimateReadyHud / UltimateCooldown 자동 적용됨
    ///
    /// Mixer 자산 위치: `Assets/_Project/Audio/Resources/MainMixer.mixer`.
    /// `Resources.Load<AudioMixer>("MainMixer")` 로 자동 로드.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Audio/Game Audio Settings")]
    public sealed class GameAudioSettings : MonoBehaviour
    {
        public static GameAudioSettings Instance { get; private set; }

        [Header("Mixer (Inspector 미설정 시 Resources/MainMixer 자동 로드)")]
        [Tooltip("씬에 미리 배치하는 경우 Inspector 에서 드래그 가능. " +
                 "비워두면 Awake 에서 Resources.Load<AudioMixer>(\"MainMixer\") 로 자동 로드.")]
        [SerializeField] private AudioMixer mixer;

        [Header("Mixer Groups (비우면 mixer.FindMatchingGroups 로 자동 검색)")]
        [Tooltip("SFX 그룹. 비워두면 mixer 의 'SFX' 이름으로 자동 검색.")]
        [SerializeField] private AudioMixerGroup sfxGroup;
        [Tooltip("Music 그룹. 비워두면 mixer 의 'Music' 이름으로 자동 검색.")]
        [SerializeField] private AudioMixerGroup musicGroup;

        [Header("Exposed Parameter Names (mixer 에서 expose 한 이름과 동일)")]
        [SerializeField] private string masterParam = "MasterVol";
        [SerializeField] private string sfxParam = "SfxVol";
        [SerializeField] private string musicParam = "MusicVol";

        [Header("Defaults (PlayerPrefs 가 없을 때 초기값. 0..1 linear)")]
        [SerializeField, Range(0f, 1f)] private float defaultMaster = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultSfx = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultMusic = 0.7f;

        [Header("Debug")]
        [SerializeField] private bool logVolumeChanges = false;

        // PlayerPrefs 키.
        private const string PrefKeyMaster = "Audio.Master";
        private const string PrefKeySfx = "Audio.Sfx";
        private const string PrefKeyMusic = "Audio.Music";

        // 0..1 linear (UI 슬라이더 값). Backing field 로 보관 (ref 전달 위해 property 사용 X).
        private float _master = 1f;
        private float _sfx = 1f;
        private float _music = 1f;
        public float MasterVolume => _master;
        public float SfxVolume => _sfx;
        public float MusicVolume => _music;

        /// <summary>SFX AudioSource 라우팅용. null 가능 (Inspector 미설정 시).</summary>
        public AudioMixerGroup SfxGroup => sfxGroup;
        /// <summary>Music AudioSource 라우팅용.</summary>
        public AudioMixerGroup MusicGroup => musicGroup;

        /// <summary>볼륨이 바뀔 때마다 발화 (linear 0..1). UI 양방향 바인딩 / 분석 / 로그용.</summary>
        public event Action<float> MasterVolumeChanged;
        public event Action<float> SfxVolumeChanged;
        public event Action<float> MusicVolumeChanged;

        /// <summary>
        /// 첫 씬 로드 전 자동 부트스트랩. 씬에 GameObject 배치 / Inspector wiring 필요 없음.
        /// 이미 인스턴스가 있으면 (씬에 미리 배치된 경우) skip.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("GameAudioSettings (Auto)");
            go.AddComponent<GameAudioSettings>();
            // Awake 가 곧 실행 → mixer Resources 로드, PlayerPrefs 로드, mixer 적용.
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Inspector 미설정 시 Resources 에서 자동 로드. 동적 부트스트랩 케이스에서 항상 발생.
            if (mixer == null)
            {
                mixer = Resources.Load<AudioMixer>("MainMixer");
                if (mixer == null)
                {
                    Debug.LogWarning("[GameAudioSettings] Resources/MainMixer.mixer 를 찾을 수 없습니다. " +
                                     "Assets/_Project/Audio/Resources/MainMixer.mixer 경로 확인하세요.", this);
                }
            }

            // mixer 가 살아있으면 그룹도 자동 검색.
            if (mixer != null)
            {
                if (sfxGroup == null)
                {
                    AudioMixerGroup[] sfxMatches = mixer.FindMatchingGroups("SFX");
                    if (sfxMatches != null && sfxMatches.Length > 0) sfxGroup = sfxMatches[0];
                }
                if (musicGroup == null)
                {
                    AudioMixerGroup[] musicMatches = mixer.FindMatchingGroups("Music");
                    if (musicMatches != null && musicMatches.Length > 0) musicGroup = musicMatches[0];
                }
            }

            LoadFromPlayerPrefs();
            ApplyAllToMixer();

            // CL-234: MMSoundManager 가 BeforeSceneLoad 시점엔 아직 초기화 안 됐을 가능성.
            // 초기화 완료를 기다린 후 PlayerPrefs 값을 MM 트랙에도 강제 재적용.
            // (MMSoundManager 가 자체 PlayerPrefs 로 트랙 볼륨 관리 → 우리 값이 게임 시작 시 덮어쓰여지는 문제 fix)
            StartCoroutine(EnsureMMSoundManagerSynced());
        }

        private void OnEnable()
        {
            // 씬 로드 후 MMSoundManager 가 새로 활성화되면 재동기화 필요.
            // BeforeSceneLoad 부트스트랩 시점엔 MMSoundManager 가 아직 없어서 ApplyAllToMixer 의
            // MM sync 가 무시됨 → 첫 씬 (또는 MM 가 있는 씬) 진입 후 보정.
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 매 씬 전환마다 mixer + MMSoundManager 양쪽에 우리 값 재적용.
            // 안전망 — audition mute leak / 외부에서 mixer 값 변경 등이 있어도 복원됨.
            ApplyAllToMixer();

            // CL-234: 씬 로드 후 MMSoundManager 가 자체 PlayerPrefs 로 트랙 볼륨을 복원하며
            // 우리 값을 덮어쓰는 케이스 대응. 다음 프레임에 강제 재적용.
            StartCoroutine(EnsureMMSoundManagerSynced());
        }

        /// <summary>
        /// CL-234: MMSoundManager 가 초기화 + 자체 PlayerPrefs 로드를 끝낸 뒤에 우리 PlayerPrefs 값을
        /// MM 트랙에 강제 재적용. 사용자가 옵션 메뉴 열기 전부터 저장된 사운드 값이 반영되도록.
        ///
        /// 흐름: 부팅·씬 로드 직후엔 MM 인스턴스가 없거나 직후에 자체 PlayerPrefs 로 트랙 볼륨 복원 →
        /// 우리 ApplyAllToMixer 의 MM sync 가 무시 또는 덮어쓰여짐. 본 코루틴이 MM 준비 + 추가 1프레임
        /// 대기 후 재적용으로 마지막 값을 보장.
        /// </summary>
        private IEnumerator EnsureMMSoundManagerSynced()
        {
            // MMSoundManager.Current 가 살아날 때까지 (최대 30프레임 ≈ 0.5초) wait.
            int safetyFrames = 30;
            while (safetyFrames-- > 0)
            {
                if (MMSoundManager.HasInstance && MMSoundManager.Current != null) break;
                yield return null;
            }

            // MM 자체 초기화가 마무리되도록 추가 프레임 wait — Initialization 코루틴이 같은 프레임에 끝난다는 보장이 없음.
            yield return null;
            yield return null;

            // 우리 PlayerPrefs 값을 mixer + MM 양쪽에 다시 강제 적용.
            ApplyAllToMixer();

            if (logVolumeChanges)
            {
                Debug.Log($"[GameAudioSettings] EnsureMMSoundManagerSynced 완료 — Master={_master:F2} Sfx={_sfx:F2} Music={_music:F2}", this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Public API ──────────────────────────────────────────────────────

        /// <summary>마스터 볼륨 설정 (0..1 linear). PlayerPrefs 저장 + mixer 반영 + MMSoundManager sync + 이벤트.</summary>
        public void SetMasterVolume(float linear01) => SetCategory(ref _master, linear01, masterParam, PrefKeyMaster, MasterVolumeChanged, "Master", MMSoundManager.MMSoundManagerTracks.Master);

        /// <summary>SFX 볼륨 설정.</summary>
        public void SetSfxVolume(float linear01) => SetCategory(ref _sfx, linear01, sfxParam, PrefKeySfx, SfxVolumeChanged, "SFX", MMSoundManager.MMSoundManagerTracks.Sfx);

        /// <summary>Music 볼륨 설정.</summary>
        public void SetMusicVolume(float linear01) => SetCategory(ref _music, linear01, musicParam, PrefKeyMusic, MusicVolumeChanged, "Music", MMSoundManager.MMSoundManagerTracks.Music);

        // ─── Internal ────────────────────────────────────────────────────────

        private void SetCategory(ref float field, float linear01, string paramName, string prefKey, Action<float> evt, string label,
            MMSoundManager.MMSoundManagerTracks mmTrack)
        {
            float clamped = Mathf.Clamp01(linear01);
            if (Mathf.Approximately(field, clamped)) return;

            field = clamped;
            PlayerPrefs.SetFloat(prefKey, clamped);
            ApplyToMixer(paramName, clamped);
            ApplyToMMSoundManager(mmTrack, clamped);
            evt?.Invoke(clamped);

            if (logVolumeChanges)
            {
                Debug.Log($"[GameAudioSettings] {label} = {clamped:F2} ({LinearToDb(clamped):F1} dB)", this);
            }
        }

        private void LoadFromPlayerPrefs()
        {
            _master = PlayerPrefs.GetFloat(PrefKeyMaster, defaultMaster);
            _sfx    = PlayerPrefs.GetFloat(PrefKeySfx,    defaultSfx);
            _music  = PlayerPrefs.GetFloat(PrefKeyMusic,  defaultMusic);
        }

        private void ApplyAllToMixer()
        {
            ApplyToMixer(masterParam, MasterVolume);
            ApplyToMixer(sfxParam,    SfxVolume);
            ApplyToMixer(musicParam,  MusicVolume);

            // MMSoundManager 도 sync — 부팅 시 PlayerPrefs 값이 MM 트랙 볼륨에도 반영.
            // MMSoundManager 인스턴스가 아직 없을 수도 있어 (BeforeSceneLoad 부트스트랩이라) 시도만.
            ApplyToMMSoundManager(MMSoundManager.MMSoundManagerTracks.Master, MasterVolume);
            ApplyToMMSoundManager(MMSoundManager.MMSoundManagerTracks.Sfx,    SfxVolume);
            ApplyToMMSoundManager(MMSoundManager.MMSoundManagerTracks.Music,  MusicVolume);
        }

        private void ApplyToMixer(string paramName, float linear01)
        {
            if (mixer == null || string.IsNullOrEmpty(paramName)) return;
            if (!mixer.SetFloat(paramName, LinearToDb(linear01)))
            {
                Debug.LogWarning($"[GameAudioSettings] Mixer parameter '{paramName}' 을 찾을 수 없습니다. mixer 에서 expose 했는지 확인하세요.", this);
            }
        }

        /// <summary>
        /// MoreMountains MMSoundManager 의 트랙 볼륨 sync.
        /// Town / Title (TDE BackgroundMusic) + Dungeon (Stage1BgmController) 모두 MMSoundManager 통과하므로
        /// 우리 슬라이더 ↔ MMSoundManager 양방향 동기화 필요.
        /// MMSoundManager.SetTrackVolume 은 linear 0..1 받음 (내부에서 dB 변환).
        /// </summary>
        private void ApplyToMMSoundManager(MMSoundManager.MMSoundManagerTracks track, float linear01)
        {
            if (!MMSoundManager.HasInstance || MMSoundManager.Current == null) return;
            // 0 은 mute 로 해석되어 MMSoundManager 가 _minimalVolume (-80dB) 처리. 안전.
            MMSoundManager.Current.SetTrackVolume(track, linear01);
        }

        /// <summary>
        /// 0..1 linear → dB. 0 → -80dB (실질적 무음), 1 → 0dB (mixer 기본).
        /// log scale 이라 사용자 청각 변화가 자연스러움 (선형 multiply 는 작은 값에서 너무 큰 변화).
        /// </summary>
        public static float LinearToDb(float linear01)
        {
            if (linear01 <= 0.0001f) return -80f;
            return Mathf.Log10(linear01) * 20f;
        }
    }
}

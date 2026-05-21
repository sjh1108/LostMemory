using System.Collections;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Audio
{
    /// <summary>
    /// CL-234: 게임 시작 시 / 씬 전환 시 PlayerPrefs 에 저장된 BGM·SFX 볼륨을 MMSoundManager 에
    /// 자동 적용. <see cref="LostMemory.UI.SettingsPanelView"/> 와 동일한 PlayerPrefs 키 사용.
    ///
    /// 기존 문제:
    /// - <c>SettingsPanelView</c> 가 자체 PlayerPrefs 키(<c>Settings_BGMVolume</c>/<c>Settings_SFXVolume</c>)로 저장.
    /// - 적용 path 는 <c>SettingsPanelView.Awake()</c> 한 곳뿐 — 옵션 메뉴 GameObject 가 활성화될 때만 발생.
    /// - 게임 시작 직후엔 옵션 메뉴가 비활성이라 Awake 가 호출되지 않아 PlayerPrefs 값이 MM 에 안 적용됨.
    /// - 사용자가 옵션 메뉴를 한 번 열어야 그제야 적용 → 신고 "환경설정 열어야 소리 적용".
    ///
    /// 해결:
    /// - 부팅 시 <see cref="RuntimeInitializeOnLoadMethod"/> 로 인스턴스 자동 생성.
    /// - <c>MMSoundManager.Current</c> 가 살아날 때까지 wait 후 PlayerPrefs 값을 MM SetTrackVolume 으로 적용.
    /// - 매 씬 전환마다 동일 sync (씬 안 MMSoundManager 자체 PlayerPrefs 로드 후 우리 값으로 보정).
    ///
    /// 우리 <see cref="GameAudioSettings"/> 와는 별개 — 그쪽은 MainMixer (별도 mixer) 대상. MM 의 BGM/SFX
    /// 트랙은 본 컴포넌트가 책임.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Audio/Settings Auto Applier")]
    public sealed class SettingsAutoApplier : MonoBehaviour
    {
        // SettingsPanelView 와 동일한 PlayerPrefs 키. 둘이 일치해야 옵션 슬라이더 변경 ↔ 부팅 자동 적용
        // 이 같은 source of truth 를 공유.
        private const string BGM_KEY = "Settings_BGMVolume";
        private const string SFX_KEY = "Settings_SFXVolume";
        private const float DefaultBgm = 0.8f;
        private const float DefaultSfx = 0.8f;

        private static SettingsAutoApplier _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("SettingsAutoApplier (Auto)");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SettingsAutoApplier>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Start()
        {
            StartCoroutine(ApplyWhenMMReady());
            StartCoroutine(PollAndReapply());
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 씬마다 MMSoundManager 가 새로 활성화되며 자체 PlayerPrefs 로 볼륨 복원 → 우리 값으로 보정.
            StartCoroutine(ApplyWhenMMReady());
        }

        /// <summary>
        /// CL-234: MM Initialization 이 우리 SetTrackVolume 후 자체 PlayerPrefs 로드로 덮어쓰는
        /// 케이스 + BGM AudioSource 가 늦게 활성화되는 케이스 안전망. 매 0.5초 PlayerPrefs 값을
        /// MM 트랙에 강제 재적용. 같은 값이면 사실상 no-op (CPU 부담 거의 없음).
        ///
        /// 사용자가 옵션 슬라이더로 변경한 직후엔 PlayerPrefs 와 MM 값이 같으므로 영향 없음.
        /// 옵션 메뉴 열기 전부터 저장된 값이 BGM 에 반영되도록 보장.
        /// </summary>
        private IEnumerator PollAndReapply()
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            while (true)
            {
                yield return wait;
                if (!MMSoundManager.HasInstance || MMSoundManager.Current == null) continue;
                if (MMSoundManager.Current.settingsSo == null
                    || MMSoundManager.Current.settingsSo.Settings == null
                    || MMSoundManager.Current.settingsSo.TargetAudioMixer == null) continue;

                float bgm = PlayerPrefs.GetFloat(BGM_KEY, DefaultBgm);
                float sfx = PlayerPrefs.GetFloat(SFX_KEY, DefaultSfx);
                MMSoundManager.Current.SetTrackVolume(MMSoundManager.MMSoundManagerTracks.Music, bgm);
                MMSoundManager.Current.SetTrackVolume(MMSoundManager.MMSoundManagerTracks.Sfx,   sfx);
            }
        }

        private IEnumerator ApplyWhenMMReady()
        {
            Debug.Log("[SettingsAutoApplier] ApplyWhenMMReady 시작 — MMSoundManager 대기.", this);

            // MMSoundManager 인스턴스 + settingsSo 준비될 때까지 최대 5초 (300프레임 @60fps) wait.
            // 씬 안 MMSoundManager 가 Awake 늦거나, settingsSo Initialization 코루틴이 늦을 수 있음.
            int safetyFrames = 300;
            while (safetyFrames-- > 0)
            {
                if (MMSoundManager.HasInstance
                    && MMSoundManager.Current != null
                    && MMSoundManager.Current.settingsSo != null)
                {
                    break;
                }
                yield return null;
            }

            // MM 자체 Initialization 코루틴이 같은 프레임에 끝난다는 보장 없음 — 추가 2프레임 wait.
            yield return null;
            yield return null;

            if (!MMSoundManager.HasInstance || MMSoundManager.Current == null)
            {
                Debug.LogWarning("[SettingsAutoApplier] MMSoundManager 미발견 — 사운드 자동 적용 skip.", this);
                yield break;
            }

            float bgm = PlayerPrefs.GetFloat(BGM_KEY, DefaultBgm);
            float sfx = PlayerPrefs.GetFloat(SFX_KEY, DefaultSfx);

            // SettingsPanelView 가 wire 한 mixer / settingsSo 가 모두 살아있을 때만 적용 시도.
            if (MMSoundManager.Current.settingsSo == null
                || MMSoundManager.Current.settingsSo.Settings == null
                || MMSoundManager.Current.settingsSo.TargetAudioMixer == null)
            {
                Debug.LogWarning($"[SettingsAutoApplier] MM settingsSo 미연결 — 사운드 자동 적용 skip. BGM={bgm:F2} SFX={sfx:F2}", this);
                yield break;
            }

            MMSoundManager.Current.SetTrackVolume(MMSoundManager.MMSoundManagerTracks.Music, bgm);
            MMSoundManager.Current.SetTrackVolume(MMSoundManager.MMSoundManagerTracks.Sfx,   sfx);
            Debug.Log($"[SettingsAutoApplier] 적용 완료 — BGM={bgm:F2} SFX={sfx:F2}", this);
        }
    }
}

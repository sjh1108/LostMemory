using System.Collections;
using LostMemory.Audio;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// 환경설정 패널 UI를 담당하는 뷰 컴포넌트.
    ///
    /// 슬라이더 2개 구성:
    ///   - BGM 볼륨  → MMSoundManager.SetVolumeMusic()
    ///   - SFX 볼륨  → MMSoundManager.SetVolumeSfx()
    ///
    /// 설정값은 PlayerPrefs에 저장되어 다음 실행 시에도 유지됩니다.
    /// </summary>
    [AddComponentMenu("Lost Memory/UI/Settings Panel View")]
    public class SettingsPanelView : MonoBehaviour
    {
        [Header("슬라이더")]
        [SerializeField] private Slider _bgmSlider;
        [SerializeField] private Slider _sfxSlider;

        [Header("버튼")]
        [SerializeField] private Button _closeButton;

        // PlayerPrefs 저장 키
        private const string BGM_KEY = "Settings_BGMVolume";
        private const string SFX_KEY = "Settings_SFXVolume";
        private static bool _warnedSoundManagerUnavailable;

        // ── 라이프사이클 ──────────────────────────────────────────────

        private void Awake()
        {
            // 저장된 볼륨 불러오기 (처음 실행이면 기본값 0.8)
            float bgmVol = PlayerPrefs.GetFloat(BGM_KEY, 0.8f);
            float sfxVol = PlayerPrefs.GetFloat(SFX_KEY, 0.8f);

            if (_bgmSlider != null)
            {
                _bgmSlider.value = bgmVol;
                _bgmSlider.onValueChanged.AddListener(OnBGMChanged);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.value = sfxVol;
                _sfxSlider.onValueChanged.AddListener(OnSFXChanged);
            }

            if (_closeButton != null)
                _closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            // 저장값을 즉시 오디오에 반영. MM 미준비 케이스 → 코루틴으로 wait 후 재시도.
            ApplyBGM(bgmVol);
            ApplySFX(sfxVol);
            StartCoroutine(ReapplyWhenMMReady(bgmVol, sfxVol));
        }

        private void OnEnable()
        {
            // CL-234: 옵션 패널 매 활성화마다 PlayerPrefs 값 재적용. Awake 시점 MM 미준비
            // 케이스 안전망 + 외부에서 PlayerPrefs 변경됐을 때도 sync.
            float bgmVol = PlayerPrefs.GetFloat(BGM_KEY, 0.8f);
            float sfxVol = PlayerPrefs.GetFloat(SFX_KEY, 0.8f);
            ApplyBGM(bgmVol);
            ApplySFX(sfxVol);
        }

        private IEnumerator ReapplyWhenMMReady(float bgmVol, float sfxVol)
        {
            // 최대 5초 동안 MMSoundManager.settingsSo 가 살아날 때까지 재시도.
            int safetyFrames = 300;
            while (safetyFrames-- > 0)
            {
                if (MMSoundManager.HasInstance
                    && MMSoundManager.Current != null
                    && MMSoundManager.Current.settingsSo != null
                    && MMSoundManager.Current.settingsSo.Settings != null
                    && MMSoundManager.Current.settingsSo.TargetAudioMixer != null)
                {
                    // 2프레임 더 wait — Initialization 코루틴 완료 보장.
                    yield return null;
                    yield return null;
                    ApplyBGM(bgmVol);
                    ApplySFX(sfxVol);
                    yield break;
                }
                yield return null;
            }
        }

        // ── 슬라이더 콜백 ─────────────────────────────────────────────

        private void OnBGMChanged(float value)
        {
            ApplyBGM(value);
            PlayerPrefs.SetFloat(BGM_KEY, value);
            PlayerPrefs.Save();  // CL-234: 갑작스러운 게임 종료에도 저장 보장.
        }

        private void OnSFXChanged(float value)
        {
            ApplySFX(value);
            PlayerPrefs.SetFloat(SFX_KEY, value);
            PlayerPrefs.Save();
        }

        // ── MMSoundManager 적용 ───────────────────────────────────────

        private static void ApplyBGM(float value)
        {
            // GameAudioSettings 가 SfxGroup/MusicGroup mixer + MMSoundManager 트랙 양쪽 sync 처리.
            // KhiSfxBinder 등 GameAudioSettings.SfxGroup 라우팅 컴포넌트도 같이 영향 받음.
            if (GameAudioSettings.Instance != null)
            {
                GameAudioSettings.Instance.SetMusicVolume(value);
                return;
            }
            // fallback: GameAudioSettings 미초기화 시 기존 MMSoundManager 트랙만 조작.
            ApplyTrackVolume(MMSoundManager.MMSoundManagerTracks.Music, value);
        }

        private static void ApplySFX(float value)
        {
            if (GameAudioSettings.Instance != null)
            {
                GameAudioSettings.Instance.SetSfxVolume(value);
                return;
            }
            ApplyTrackVolume(MMSoundManager.MMSoundManagerTracks.Sfx, value);
        }

        private static void ApplyTrackVolume(MMSoundManager.MMSoundManagerTracks track, float value)
        {
            if (!MMSoundManager.HasInstance || MMSoundManager.Current == null)
            {
                WarnSoundManagerUnavailable();
                return;
            }

            MMSoundManager soundManager = MMSoundManager.Current;
            if (soundManager.settingsSo == null ||
                soundManager.settingsSo.Settings == null ||
                soundManager.settingsSo.TargetAudioMixer == null)
            {
                WarnSoundManagerUnavailable();
                return;
            }

            soundManager.SetTrackVolume(track, value);
        }

        private static void WarnSoundManagerUnavailable()
        {
            if (_warnedSoundManagerUnavailable)
            {
                return;
            }

            _warnedSoundManagerUnavailable = true;
            Debug.LogWarning("[SettingsPanelView] MMSoundManager is missing or not configured. Audio sliders were saved but not applied.");
        }
    }
}

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

            // 저장값을 즉시 오디오에 반영
            ApplyBGM(bgmVol);
            ApplySFX(sfxVol);
        }

        // ── 슬라이더 콜백 ─────────────────────────────────────────────

        private void OnBGMChanged(float value)
        {
            ApplyBGM(value);
            PlayerPrefs.SetFloat(BGM_KEY, value);
        }

        private void OnSFXChanged(float value)
        {
            ApplySFX(value);
            PlayerPrefs.SetFloat(SFX_KEY, value);
        }

        // ── MMSoundManager 적용 ───────────────────────────────────────

        private static void ApplyBGM(float value)
        {
            if (MMSoundManager.Instance != null)
                MMSoundManager.Instance.SetVolumeMusic(value);
        }

        private static void ApplySFX(float value)
        {
            if (MMSoundManager.Instance != null)
                MMSoundManager.Instance.SetVolumeSfx(value);
        }
    }
}

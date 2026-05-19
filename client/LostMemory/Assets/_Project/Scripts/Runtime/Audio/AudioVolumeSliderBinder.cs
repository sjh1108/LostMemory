using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Audio
{
    /// <summary>
    /// Settings 패널의 볼륨 슬라이더를 GameAudioSettings 의 카테고리 볼륨과 양방향 바인딩.
    ///
    /// 사용:
    /// - Settings 패널의 각 슬라이더 (Master / SFX / Music) GameObject 에 부착.
    /// - Inspector 에서 Target Category 선택 + 같은 GameObject 의 Slider 자동 wiring.
    /// - 슬라이더 값은 0..1 linear. GameAudioSettings 가 내부에서 dB 로 변환해 mixer 에 적용.
    ///
    /// 동작:
    /// - Awake/OnEnable: 현재 GameAudioSettings 값 → 슬라이더에 반영.
    /// - 슬라이더 onValueChanged: GameAudioSettings 의 setter 호출.
    /// - GameAudioSettings 이벤트: 외부 (예: hotkey) 가 볼륨 바꿔도 슬라이더가 따라감.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Slider))]
    [AddComponentMenu("Lost Memory/Audio/Audio Volume Slider Binder")]
    public sealed class AudioVolumeSliderBinder : MonoBehaviour
    {
        public enum Category { Master, Sfx, Music }

        [Header("Target")]
        [SerializeField] private Category category = Category.Sfx;
        [Tooltip("비워두면 같은 GameObject 의 Slider 자동 wiring.")]
        [SerializeField] private Slider slider;

        private void Reset()
        {
            slider = GetComponent<Slider>();
        }

        private void Awake()
        {
            if (slider == null) slider = GetComponent<Slider>();
            // 슬라이더 범위가 0..1 가 아니면 강제 (인스펙터 실수 방어).
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.wholeNumbers = false;
            }
        }

        private void OnEnable()
        {
            GameAudioSettings settings = GameAudioSettings.Instance;
            if (settings == null || slider == null) return;

            // 초기 값 sync.
            slider.SetValueWithoutNotify(GetCategoryValue(settings));

            // 슬라이더 → settings.
            slider.onValueChanged.AddListener(HandleSliderChanged);

            // settings → 슬라이더 (외부 변경 시 sync).
            SubscribeCategoryEvent(settings, true);
        }

        private void OnDisable()
        {
            GameAudioSettings settings = GameAudioSettings.Instance;
            if (slider != null) slider.onValueChanged.RemoveListener(HandleSliderChanged);
            if (settings != null) SubscribeCategoryEvent(settings, false);
        }

        private void HandleSliderChanged(float value)
        {
            GameAudioSettings settings = GameAudioSettings.Instance;
            if (settings == null) return;

            switch (category)
            {
                case Category.Master: settings.SetMasterVolume(value); break;
                case Category.Sfx:    settings.SetSfxVolume(value);    break;
                case Category.Music:  settings.SetMusicVolume(value);  break;
            }
        }

        private void HandleSettingsChanged(float value)
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(value);
        }

        private float GetCategoryValue(GameAudioSettings settings)
        {
            return category switch
            {
                Category.Master => settings.MasterVolume,
                Category.Sfx    => settings.SfxVolume,
                Category.Music  => settings.MusicVolume,
                _ => 1f
            };
        }

        private void SubscribeCategoryEvent(GameAudioSettings settings, bool subscribe)
        {
            switch (category)
            {
                case Category.Master:
                    if (subscribe) settings.MasterVolumeChanged += HandleSettingsChanged;
                    else settings.MasterVolumeChanged -= HandleSettingsChanged;
                    break;
                case Category.Sfx:
                    if (subscribe) settings.SfxVolumeChanged += HandleSettingsChanged;
                    else settings.SfxVolumeChanged -= HandleSettingsChanged;
                    break;
                case Category.Music:
                    if (subscribe) settings.MusicVolumeChanged += HandleSettingsChanged;
                    else settings.MusicVolumeChanged -= HandleSettingsChanged;
                    break;
            }
        }
    }
}

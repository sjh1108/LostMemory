using LostMemory.Audio;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.Audio
{
    /// <summary>
    /// 개발자용 오디오 볼륨 조정 EditorWindow.
    /// `LostMemory → Audio Settings` 메뉴로 호출. InventoryTestWindow 와 동일 패턴 (Editor only).
    ///
    /// 동작:
    /// - Play 모드에서만 동작 (GameAudioSettings 싱글톤이 부트스트랩된 이후).
    /// - 3개 슬라이더 (Master / SFX / Music) → GameAudioSettings.Set* 호출.
    /// - 값 변경은 PlayerPrefs 에 즉시 저장되어 다음 Play 세션에도 유지.
    /// - OnInspectorUpdate 로 외부 변경 자동 반영 (다른 코드/슬라이더가 값을 바꿔도 sync).
    ///
    /// 빌드에 포함되지 않음 (Editor 폴더 안). 출시 빌드용 UI 는 별도 (옵션 메뉴 슬라이더 + AudioVolumeSliderBinder).
    /// </summary>
    public class AudioSettingsWindow : EditorWindow
    {
        [MenuItem("LostMemory/Audio Settings")]
        public static void Open()
        {
            AudioSettingsWindow window = GetWindow<AudioSettingsWindow>("Audio Settings");
            window.minSize = new Vector2(280, 140);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Audio Volumes (Dev)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 모드에서만 동작합니다.\nGameAudioSettings 싱글톤은 첫 씬 로드 직전 자동 생성됩니다.", MessageType.Info);
                return;
            }

            GameAudioSettings settings = GameAudioSettings.Instance;
            if (settings == null)
            {
                EditorGUILayout.HelpBox("GameAudioSettings.Instance 가 null 입니다.\nBootstrap 이 동작했는지 확인하세요.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            float master = EditorGUILayout.Slider("Master", settings.MasterVolume, 0f, 1f);
            float sfx    = EditorGUILayout.Slider("SFX",    settings.SfxVolume,    0f, 1f);
            float music  = EditorGUILayout.Slider("Music",  settings.MusicVolume,  0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                if (!Mathf.Approximately(master, settings.MasterVolume)) settings.SetMasterVolume(master);
                if (!Mathf.Approximately(sfx,    settings.SfxVolume))    settings.SetSfxVolume(sfx);
                if (!Mathf.Approximately(music,  settings.MusicVolume))  settings.SetMusicVolume(music);
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset (1 / 1 / 0.7)"))
                {
                    settings.SetMasterVolume(1f);
                    settings.SetSfxVolume(1f);
                    settings.SetMusicVolume(0.7f);
                }
                if (GUILayout.Button("Mute All"))
                {
                    settings.SetMasterVolume(0f);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"dB: Master={GameAudioSettings.LinearToDb(settings.MasterVolume):F1}  SFX={GameAudioSettings.LinearToDb(settings.SfxVolume):F1}  Music={GameAudioSettings.LinearToDb(settings.MusicVolume):F1}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("값은 PlayerPrefs 에 자동 저장됨.", EditorStyles.miniLabel);
        }

        /// <summary>외부에서 볼륨이 바뀐 경우 (코드 / UI 슬라이더 등) 창도 자동 갱신.</summary>
        private void OnInspectorUpdate() => Repaint();
    }
}

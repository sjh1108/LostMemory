using System;
using System.Collections.Generic;
using LostMemory.Audio;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 시연용 사운드 조절 패널 — F10 으로 토글되는 IMGUI 윈도우.
    ///
    /// 기능:
    ///   1. 글로벌 Master / Music / SFX 슬라이더 (GameAudioSettings 재사용)
    ///   2. 씬 안 모든 AudioSource 를 자동 스캔 → 클립 단위로 그룹화 → 각 클립마다 개별 볼륨 슬라이더 + Mute
    ///   3. PlayerPrefs 자동 저장 (다음 실행에도 유지)
    ///
    /// 자동 부트스트랩 — Awake 가 RuntimeInitializeOnLoadMethod 로 자동 GameObject 생성.
    /// prefab 부착 작업 필요 없음. 빌드 실행 즉시 F10 으로 사용 가능.
    ///
    /// 증폭 (>1.0) 한계:
    ///   AudioSource.volume 은 [0,1] clamp (Unity 결정). 슬라이더 0~2.0 노출하지만 1.0 초과는 표시만 (⚠).
    ///   진짜 증폭은 AudioMixer 의 클립별 group 분리 필요 (mixer asset 수정 — follow-up).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Test Khi/Demo Sound Panel")]
    public sealed class DemoSoundPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F10;
        [SerializeField, Min(0.05f)] private float scanIntervalSeconds = 0.5f;
        [SerializeField, Min(1f)] private float volumeSliderMax = 2.0f;
        [SerializeField] private Vector2 panelSize = new Vector2(460f, 580f);
        [SerializeField] private string playerPrefsKeyPrefix = "DemoSoundPanel.Clip";

        [Header("Global Mute")]
        [Tooltip("이 키를 한 번 누르면 모든 사운드 차단 (AudioListener.pause=true), 다시 누르면 재개. " +
                 "재생 위치는 유지됨 — BGM 이 멈췄다가 같은 위치부터 이어짐.")]
        [SerializeField] private KeyCode globalMuteKey = KeyCode.F11;
        [Tooltip("mute 중일 때 화면 한쪽에 표시 (패널 닫혀 있어도). 시연 중 까먹지 않게. 기본 false — 깔끔한 시연 화면.")]
        [SerializeField] private bool showMuteIndicator = false;

        private sealed class ClipEntry
        {
            public string clipName;
            public float multiplier = 1f;
            public bool muted;
            public List<AudioSource> activeSources = new List<AudioSource>();
            public Dictionary<AudioSource, float> baseVolumes = new Dictionary<AudioSource, float>();
        }

        private readonly Dictionary<string, ClipEntry> _entries = new Dictionary<string, ClipEntry>();
        private float _nextScanAt;
        private bool _open;
        private Vector2 _scroll;
        private string _searchFilter = "";
        private Rect _windowRect;

        /// <summary>자동 부트스트랩 — prefab 부착 불필요. 어떤 씬이든 진입 후 F10 으로 토글.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (FindObjectOfType<DemoSoundPanel>() != null) return;
            GameObject go = new GameObject("[DemoSoundPanel]");
            go.AddComponent<DemoSoundPanel>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            // 글로벌 mute 토글 — 패널 열려있지 않아도 작동.
            if (Input.GetKeyDown(globalMuteKey))
            {
                AudioListener.pause = !AudioListener.pause;
                Debug.Log($"[DemoSoundPanel] global mute → {(AudioListener.pause ? "ON" : "OFF")}");
            }

            if (Input.GetKeyDown(toggleKey)) _open = !_open;
            if (!_open) return;

            if (Time.unscaledTime >= _nextScanAt)
            {
                _nextScanAt = Time.unscaledTime + scanIntervalSeconds;
                ScanAudioSources();
            }
        }

        private void LateUpdate()
        {
            // 매 프레임 등록된 모든 AudioSource 의 volume 을 multiplier * base 로 override.
            // [0,1] 클램프 — Unity 의 AudioSource.volume 한계.
            foreach (ClipEntry entry in _entries.Values)
            {
                float effective = entry.muted ? 0f : Mathf.Clamp(entry.multiplier, 0f, 1f);
                for (int i = entry.activeSources.Count - 1; i >= 0; i--)
                {
                    AudioSource src = entry.activeSources[i];
                    if (src == null)
                    {
                        entry.activeSources.RemoveAt(i);
                        continue;
                    }
                    if (!entry.baseVolumes.ContainsKey(src))
                    {
                        entry.baseVolumes[src] = src.volume;
                    }
                    src.volume = effective * entry.baseVolumes[src];
                }
            }
        }

        private void ScanAudioSources()
        {
            AudioSource[] all = FindObjectsOfType<AudioSource>(includeInactive: true);
            for (int i = 0; i < all.Length; i++)
            {
                AudioSource src = all[i];
                if (src == null || src.clip == null) continue;
                string clipName = src.clip.name;
                if (!_entries.TryGetValue(clipName, out ClipEntry entry))
                {
                    entry = new ClipEntry { clipName = clipName };
                    LoadFromPrefs(entry);
                    _entries[clipName] = entry;
                }
                if (!entry.activeSources.Contains(src)) entry.activeSources.Add(src);
            }
        }

        private void LoadFromPrefs(ClipEntry entry)
        {
            string volKey = $"{playerPrefsKeyPrefix}.{entry.clipName}.Volume";
            string muteKey = $"{playerPrefsKeyPrefix}.{entry.clipName}.Mute";
            if (PlayerPrefs.HasKey(volKey)) entry.multiplier = PlayerPrefs.GetFloat(volKey, 1f);
            if (PlayerPrefs.HasKey(muteKey)) entry.muted = PlayerPrefs.GetInt(muteKey, 0) == 1;
        }

        private void SaveToPrefs(ClipEntry entry)
        {
            PlayerPrefs.SetFloat($"{playerPrefsKeyPrefix}.{entry.clipName}.Volume", entry.multiplier);
            PlayerPrefs.SetInt($"{playerPrefsKeyPrefix}.{entry.clipName}.Mute", entry.muted ? 1 : 0);
        }

        private void ResetAllToOne()
        {
            foreach (ClipEntry entry in _entries.Values)
            {
                entry.multiplier = 1f;
                entry.muted = false;
                SaveToPrefs(entry);
            }
        }

        // ─── OnGUI ──────────────────────────────────────────────

        private void OnGUI()
        {
            // mute 인디케이터 — 패널 닫혀 있어도 표시. 시연 중 까먹지 않게.
            if (showMuteIndicator && AudioListener.pause)
            {
                GUIStyle style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white, background = Texture2D.whiteTexture }
                };
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.8f, 0.1f, 0.1f, 0.85f);
                GUI.Box(new Rect(Screen.width - 180f, 10f, 170f, 36f), $"🔇 MUTED  [{globalMuteKey}]", style);
                GUI.backgroundColor = prev;
            }

            if (!_open) return;
            if (_windowRect.width == 0) _windowRect = new Rect(20f, 20f, panelSize.x, panelSize.y);
            _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, $"Demo Sound Panel  [{toggleKey}]");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Global (GameAudioSettings)", GUI.skin.box);
            DrawGlobalRow("Master", GameAudioSettings.Instance?.MasterVolume ?? 1f,
                v => GameAudioSettings.Instance?.SetMasterVolume(v));
            DrawGlobalRow("Music ", GameAudioSettings.Instance?.MusicVolume ?? 1f,
                v => GameAudioSettings.Instance?.SetMusicVolume(v));
            DrawGlobalRow("SFX   ", GameAudioSettings.Instance?.SfxVolume ?? 1f,
                v => GameAudioSettings.Instance?.SetSfxVolume(v));

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50f));
            _searchFilter = GUILayout.TextField(_searchFilter ?? "", GUILayout.Width(220f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset All", GUILayout.Width(90f))) ResetAllToOne();
            GUILayout.EndHorizontal();

            GUILayout.Label($"Per-Clip ({_entries.Count})  ⚠ = amplify limited", GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (ClipEntry entry in _entries.Values)
            {
                if (!string.IsNullOrEmpty(_searchFilter)
                    && entry.clipName.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                DrawClipRow(entry);
            }
            GUILayout.EndScrollView();

            GUI.DragWindow();
        }

        private void DrawGlobalRow(string label, float current, Action<float> setter)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(60f));
            float v = GUILayout.HorizontalSlider(current, 0f, 1f);
            if (!Mathf.Approximately(v, current)) setter?.Invoke(v);
            GUILayout.Label($"{current:F2}", GUILayout.Width(50f));
            GUILayout.EndHorizontal();
        }

        private void DrawClipRow(ClipEntry entry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(entry.clipName, GUILayout.Width(180f));
            bool newMute = GUILayout.Toggle(entry.muted, "Mute", GUILayout.Width(55f));
            if (newMute != entry.muted)
            {
                entry.muted = newMute;
                SaveToPrefs(entry);
            }
            float newVol = GUILayout.HorizontalSlider(entry.multiplier, 0f, volumeSliderMax);
            if (!Mathf.Approximately(newVol, entry.multiplier))
            {
                entry.multiplier = newVol;
                SaveToPrefs(entry);
            }
            string display = entry.multiplier > 1f ? $"{entry.multiplier:F2} ⚠" : $"{entry.multiplier:F2}";
            GUILayout.Label(display, GUILayout.Width(60f));
            GUILayout.EndHorizontal();
        }
    }
}

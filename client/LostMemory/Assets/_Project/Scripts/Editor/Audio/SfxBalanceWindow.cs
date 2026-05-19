using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LostMemory.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace LostMemory.Editor.Audio
{
    /// <summary>
    /// SFX 클립별 볼륨 multiplier 조정 도구.
    /// `LostMemory → SFX Balance` 메뉴로 호출. InventoryTestWindow 와 동일 패턴 (Editor only).
    ///
    /// 동작:
    /// - 프로젝트 내 모든 prefab 에서 KhiSfxBinder / UltimateReadyHud / UltimateCooldown /
    ///   KhiParryFeedbackPresenter 컴포넌트 스캔 → AudioClip 필드 추출.
    /// - 각 클립마다 슬라이더 + Play(▶) / Stop(■) 버튼.
    /// - Save 클릭 시 SfxClipVolumeBalance.asset 에 저장 (Resources 폴더에 자동 생성).
    /// - Play 모드 / Edit 모드 둘 다 동작 — 게임 안 돌리고도 audition 가능.
    ///
    /// 빌드 비포함 (Editor 폴더).
    /// </summary>
    public class SfxBalanceWindow : EditorWindow
    {
        private const string BalanceAssetPath = "Assets/_Project/Audio/Resources/SfxClipVolumeBalance.asset";

        // 스캔 대상 컴포넌트 타입 이름 (어셈블리 로드 안 됐을 때 대비 string 으로).
        private static readonly string[] TargetTypeNames =
        {
            "LostMemory.TestKhi.KhiSfxBinder",
            "LostMemory.UI.UltimateReadyHudPresenter",
            "LostMemory.UI.UltimateCooldownPresenter",
            "LostMemory.TestKhi.KhiParryFeedbackPresenter",
            // Intro (Phase0):
            "LostMemory.Intro.Phase0.Phase0TimedBgm",
            "LostMemory.Intro.Phase0.Phase0NarrationTypewriter",
            "LostMemory.Intro.Phase0.Phase0IntroSequenceController",
            // MoreMountains TopDownEngine (Town BGM 등):
            "MoreMountains.TopDownEngine.BackgroundMusic",
        };

        // 스캔 대상 ScriptableObject 타입 이름 (SO 안에 AudioClip 필드가 있는 케이스).
        private static readonly string[] TargetSoTypeNames =
        {
            "LostMemory.Intro.Phase0.Phase0IntroSequenceData",
        };

        private SfxClipVolumeBalance _balanceAsset;
        private List<DiscoveredClip> _discovered = new List<DiscoveredClip>();
        private Dictionary<AudioClip, float> _workingGains = new Dictionary<AudioClip, float>();
        private bool _dirty = false;
        private string _searchFilter = "";
        private Vector2 _scroll;

        // 옵션.
        private bool _includeOpenScenes = true;
        private bool _scanAudioSources = true;
        private bool _scanScriptableObjects = true;
        private AudioClip _pendingAddClip;

        // 수동 추가된 클립 — 세션 동안 refresh 후에도 살아남음.
        private HashSet<AudioClip> _manualClips = new HashSet<AudioClip>();

        // Audition 임시 GameObject.
        private GameObject _auditionObject;
        private AudioClip _currentAuditionClip;

        // Audition 중 게임 audio mute 격리용.
        private AudioMixer _mixerForMute;
        private float _savedMasterDb;
        private bool _muteActive;

        // Scan Asset 슬롯.
        private UnityEngine.Object _pendingScanAsset;

        private struct DiscoveredClip
        {
            public AudioClip clip;
            public string componentTypeName;  // 예: "KhiSfxBinder"
            public string fieldPath;          // 예: "attackSwingClips[0]"
            public string prefabPath;         // 클립을 참조한 prefab 경로 (첫 번째만)
        }

        [MenuItem("LostMemory/SFX Balance")]
        public static void Open()
        {
            SfxBalanceWindow window = GetWindow<SfxBalanceWindow>("SFX Balance");
            window.minSize = new Vector2(540, 360);
        }

        private void OnEnable()
        {
            LoadOrCreateAsset();
            RefreshDiscovery();
        }

        private void OnDisable()
        {
            StopAudition();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawScanAssetRow();
            DrawManualAddRow();
            EditorGUILayout.Space(4);
            DrawList();
        }

        // ─── Toolbar ─────────────────────────────────────────────────────────

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("↻ Refresh", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    RefreshDiscovery();
                }
                if (GUILayout.Button("■ Stop", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    StopAudition();
                }

                // 씬 스캔 토글.
                bool newIncludeScenes = GUILayout.Toggle(_includeOpenScenes, "Scenes", EditorStyles.toolbarButton, GUILayout.Width(70));
                if (newIncludeScenes != _includeOpenScenes)
                {
                    _includeOpenScenes = newIncludeScenes;
                    RefreshDiscovery();
                }

                // AudioSource 스캔 토글 (씬·prefab 의 모든 AudioSource.clip 캡처).
                bool newScanAS = GUILayout.Toggle(_scanAudioSources, "AudioSrc", EditorStyles.toolbarButton, GUILayout.Width(80));
                if (newScanAS != _scanAudioSources)
                {
                    _scanAudioSources = newScanAS;
                    RefreshDiscovery();
                }

                // SO 스캔 토글.
                bool newScanSO = GUILayout.Toggle(_scanScriptableObjects, "SO", EditorStyles.toolbarButton, GUILayout.Width(40));
                if (newScanSO != _scanScriptableObjects)
                {
                    _scanScriptableObjects = newScanSO;
                    RefreshDiscovery();
                }

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
                _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(180));

                GUILayout.FlexibleSpace();

                GUI.enabled = _dirty;
                if (GUILayout.Button("💾 Save", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    SaveToAsset();
                }
                GUI.enabled = true;

                if (GUILayout.Button("↺ Reset All", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    if (EditorUtility.DisplayDialog("Reset all gains?",
                        "모든 클립의 gain 을 1.0 (변경 없음) 으로 되돌리고 SO 의 entries 도 초기화합니다.",
                        "Reset", "Cancel"))
                    {
                        ResetAll();
                    }
                }
            }
        }

        // ─── Scan Asset Row (Prefab / Scene 일괄 등록) ───────────────────────

        private void DrawScanAssetRow()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scan Asset:", GUILayout.Width(80));
                _pendingScanAsset = EditorGUILayout.ObjectField(_pendingScanAsset, typeof(UnityEngine.Object), false);

                GUI.enabled = _pendingScanAsset != null;
                if (GUILayout.Button("Scan & Register", GUILayout.Width(140)))
                {
                    ScanAndRegister(_pendingScanAsset);
                    _pendingScanAsset = null;
                }
                GUI.enabled = true;

                GUILayout.Label("Prefab/Scene 드래그 → 모든 클립 SO 등록 (gain 1.0)", EditorStyles.miniLabel);
            }
        }

        private void ScanAndRegister(UnityEngine.Object asset)
        {
            if (asset == null || _balanceAsset == null) return;
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[SfxBalance] '{asset.name}' 경로 resolve 실패.");
                return;
            }

            HashSet<AudioClip> foundClips = new HashSet<AudioClip>();

            if (asset is SceneAsset)
            {
                // 씬은 additive 로 잠깐 로드 → 스캔 → 닫기. 현재 씬 영향 X.
                Scene loaded = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    ScanRootsForClips(loaded.GetRootGameObjects(), $"[Scene] {loaded.name}", foundClips);
                }
                finally
                {
                    EditorSceneManager.CloseScene(loaded, true);
                }
            }
            else if (asset is GameObject prefab)
            {
                ScanRootsForClips(new[] { prefab }, path, foundClips);
            }
            else if (asset is ScriptableObject so)
            {
                // SO 안의 AudioClip 필드 추출 (Phase0IntroSequenceData 처럼).
                HashSet<AudioClip> tempSeen = new HashSet<AudioClip>();
                List<DiscoveredClip> tempDiscovered = _discovered;
                _discovered = new List<DiscoveredClip>(); // 임시 buffer
                ExtractClipsFromUnityObject(so, path, tempSeen);
                foreach (DiscoveredClip d in _discovered) foundClips.Add(d.clip);
                _discovered = tempDiscovered;
            }
            else
            {
                Debug.LogWarning($"[SfxBalance] '{asset.name}' 은 Prefab / Scene / SO 가 아님 (type={asset.GetType().Name}).");
                return;
            }

            // SO 에 등록 (gain 1.0, 이미 있으면 그 gain 유지).
            int registered = 0;
            foreach (AudioClip clip in foundClips)
            {
                if (clip == null) continue;
                if (TryGetExistingGain(clip, out _)) continue; // 이미 있으면 skip
                _balanceAsset.SetGain(clip, 1f);
                registered++;
            }

            if (registered > 0)
            {
                EditorUtility.SetDirty(_balanceAsset);
                AssetDatabase.SaveAssets();
                SfxClipVolumeBalance.InvalidateStaticCache();
                // SaveAssets 가 reimport 트리거할 수 있어서 reference reload.
                _balanceAsset = AssetDatabase.LoadAssetAtPath<SfxClipVolumeBalance>(BalanceAssetPath);
            }

            int totalEntries = _balanceAsset != null ? _balanceAsset.Entries.Count : 0;
            Debug.Log($"[SfxBalance] '{path}' 스캔 — 클립 {foundClips.Count}개 발견, 신규 등록 {registered}개. SO total entries={totalEntries}");

            // 다시 발견하도록 refresh.
            RefreshDiscovery();
        }

        private void ScanRootsForClips(GameObject[] roots, string sourceLabel, HashSet<AudioClip> output)
        {
            Type[] targetTypes = TargetTypeNames
                .Select(n => FindTypeInLoadedAssemblies(n))
                .Where(t => t != null)
                .ToArray();

            HashSet<AudioClip> seen = new HashSet<AudioClip>();
            // 우리 _discovered 를 일시적으로 가로채서 결과만 수집.
            List<DiscoveredClip> savedDiscovered = _discovered;
            _discovered = new List<DiscoveredClip>();
            try
            {
                foreach (GameObject root in roots)
                {
                    if (root == null) continue;

                    // 1. 지정 binder 타입.
                    foreach (Type t in targetTypes)
                    {
                        Component[] comps = root.GetComponentsInChildren(t, true);
                        foreach (Component c in comps)
                        {
                            if (c == null) continue;
                            ExtractClipsFromUnityObject(c, sourceLabel, seen);
                        }
                    }

                    // 2. AudioSource.
                    AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
                    foreach (AudioSource src in sources)
                    {
                        AddAudioSourceClip(src, sourceLabel, seen);
                    }
                }

                foreach (DiscoveredClip d in _discovered)
                {
                    if (d.clip != null) output.Add(d.clip);
                }
            }
            finally
            {
                _discovered = savedDiscovered;
            }
        }

        private bool TryGetExistingGain(AudioClip clip, out float gain)
        {
            gain = 1f;
            if (_balanceAsset == null || clip == null) return false;
            foreach (SfxClipVolumeBalance.Entry e in _balanceAsset.Entries)
            {
                if (e.clip == clip) { gain = e.gain; return true; }
            }
            return false;
        }

        // ─── Manual Add Row ──────────────────────────────────────────────────

        private void DrawManualAddRow()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Add Clip:", GUILayout.Width(70));
                _pendingAddClip = (AudioClip)EditorGUILayout.ObjectField(_pendingAddClip, typeof(AudioClip), false);

                GUI.enabled = _pendingAddClip != null;
                if (GUILayout.Button("+ Add", GUILayout.Width(70)))
                {
                    AddManualClip(_pendingAddClip);
                    _pendingAddClip = null;
                }
                GUI.enabled = true;

                if (_manualClips.Count > 0)
                {
                    GUILayout.Label($"Manual: {_manualClips.Count}", EditorStyles.miniLabel, GUILayout.Width(80));
                    if (GUILayout.Button("Clear Manual", EditorStyles.miniButton, GUILayout.Width(100)))
                    {
                        _manualClips.Clear();
                        RefreshDiscovery();
                    }
                }
            }
        }

        private void AddManualClip(AudioClip clip)
        {
            if (clip == null) return;
            if (_manualClips.Add(clip))
            {
                // Refresh 가 _manualClips 를 보존하면서 다시 빌드.
                RefreshDiscovery();
                // ping 으로 사용자에게 위치 알림.
                EditorGUIUtility.PingObject(clip);
            }
        }

        // ─── Main List ───────────────────────────────────────────────────────

        private void DrawList()
        {
            if (_balanceAsset == null)
            {
                EditorGUILayout.HelpBox("SfxClipVolumeBalance.asset 로드/생성 실패.", MessageType.Error);
                return;
            }

            if (_discovered.Count == 0)
            {
                EditorGUILayout.HelpBox("스캔된 SFX 클립이 없습니다.\n프로젝트 prefab 에 KhiSfxBinder / UltimateReadyHud / UltimateCooldown / KhiParryFeedbackPresenter 가 있는지 확인하세요.", MessageType.Info);
                return;
            }

            // 그룹핑: componentTypeName 으로.
            IEnumerable<IGrouping<string, DiscoveredClip>> groups = _discovered
                .Where(d => string.IsNullOrEmpty(_searchFilter) ||
                            d.clip.name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            d.fieldPath.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .GroupBy(d => d.componentTypeName)
                .OrderBy(g => g.Key);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (IGrouping<string, DiscoveredClip> group in groups)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                foreach (DiscoveredClip d in group)
                {
                    DrawClipRow(d);
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            int modifiedCount = _workingGains.Count(kv => !Mathf.Approximately(kv.Value, 1f));
            EditorGUILayout.LabelField($"총 클립 {_discovered.Count}개, 1.0 이 아닌 항목 {modifiedCount}개." +
                                       (_dirty ? "  ⚠ 미저장 변경 있음" : ""),
                                       EditorStyles.miniLabel);
        }

        private void DrawClipRow(DiscoveredClip d)
        {
            if (d.clip == null) return;
            float current = _workingGains.TryGetValue(d.clip, out float g) ? g : 1f;

            using (new EditorGUILayout.HorizontalScope())
            {
                // ▶ Play 버튼
                if (GUILayout.Button("▶", GUILayout.Width(26)))
                {
                    PlayAudition(d.clip, current);
                }
                // ■ Stop (현재 audition 중인 클립만 활성)
                bool isCurrent = _auditionObject != null && _currentAuditionClip == d.clip;
                GUI.enabled = isCurrent;
                if (GUILayout.Button("■", GUILayout.Width(26)))
                {
                    StopAudition();
                }
                GUI.enabled = true;

                // 클립 이름 (클릭 시 Project 창에서 ping)
                if (GUILayout.Button(d.clip.name, EditorStyles.label, GUILayout.Width(180)))
                {
                    EditorGUIUtility.PingObject(d.clip);
                }

                // 슬라이더 0..MaxGain (1 초과 = 증폭). 1.0 위치는 mid-point 가 아니라 50% 지점.
                float newVal = EditorGUILayout.Slider(current, 0f, SfxClipVolumeBalance.MaxGain);
                if (!Mathf.Approximately(newVal, current))
                {
                    _workingGains[d.clip] = newVal;
                    _dirty = true;
                }

                // gain 값에 따라 표시 색상 변경 (> 1 = 노란색 = 증폭, < 1 = 회색 = 감쇠).
                Color prev = GUI.color;
                if (current > 1.01f) GUI.color = new Color(1f, 0.85f, 0.4f);
                else if (current < 0.99f) GUI.color = new Color(0.7f, 0.7f, 0.7f);
                GUILayout.Label(d.fieldPath, EditorStyles.miniLabel, GUILayout.Width(160));
                GUI.color = prev;
            }
        }

        // ─── Audition ────────────────────────────────────────────────────────

        private void PlayAudition(AudioClip clip, float gain)
        {
            StopAudition();
            if (clip == null) return;

            // 게임 audio 격리 — Mixer Master 를 임시로 -80dB 로 mute.
            // audition AudioSource 는 mixer routing 안 거치니까 그대로 들림.
            MuteGameAudio();

            _auditionObject = new GameObject("SfxAudition") { hideFlags = HideFlags.HideAndDontSave };
            AudioSource src = _auditionObject.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            // PlayOneShot 의 volumeScale 은 > 1 도 허용 (AudioSource.volume 은 0..1 clamp 라 회피).
            src.PlayOneShot(clip, Mathf.Max(0f, gain));
            _currentAuditionClip = clip;

            // 클립 끝나면 자동 정리.
            EditorApplication.delayCall += CleanupAuditionWhenDone;
        }

        private void CleanupAuditionWhenDone()
        {
            if (_auditionObject == null) return;
            AudioSource src = _auditionObject.GetComponent<AudioSource>();
            if (src == null || !src.isPlaying)
            {
                StopAudition();
                return;
            }
            // 다음 프레임 체크 (Editor 코루틴 흉내).
            EditorApplication.delayCall += CleanupAuditionWhenDone;
        }

        private void StopAudition()
        {
            if (_auditionObject != null)
            {
                DestroyImmediate(_auditionObject);
                _auditionObject = null;
            }
            _currentAuditionClip = null;
            UnmuteGameAudio();
        }

        // ─── 게임 audio mute 격리 ────────────────────────────────────────────

        private void MuteGameAudio()
        {
            if (_muteActive) return;
            if (_mixerForMute == null) _mixerForMute = Resources.Load<AudioMixer>("MainMixer");
            if (_mixerForMute == null) return;
            if (!_mixerForMute.GetFloat("MasterVol", out _savedMasterDb)) return;
            _mixerForMute.SetFloat("MasterVol", -80f);
            _muteActive = true;
        }

        private void UnmuteGameAudio()
        {
            if (!_muteActive) return;
            if (_mixerForMute != null) _mixerForMute.SetFloat("MasterVol", _savedMasterDb);
            _muteActive = false;
        }

        // ─── Discovery (prefab 스캔) ──────────────────────────────────────────

        private void RefreshDiscovery()
        {
            // SaveAssets 후 reimport 등으로 stale 됐을 수 있어 항상 fresh reload.
            if (_balanceAsset == null) LoadOrCreateAsset();

            _discovered.Clear();
            HashSet<AudioClip> seen = new HashSet<AudioClip>();

            // 타입 이름으로 Type resolve.
            Type[] targetTypes = TargetTypeNames
                .Select(n => FindTypeInLoadedAssemblies(n))
                .Where(t => t != null)
                .ToArray();
            if (targetTypes.Length == 0)
            {
                Debug.LogWarning("[SfxBalance] 스캔 대상 타입을 찾지 못함. 어셈블리 로드 확인.");
            }

            // 1. 프로젝트 prefab 스캔.
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;

                // 1a. 지정 binder 타입.
                foreach (Type t in targetTypes)
                {
                    Component[] comps = root.GetComponentsInChildren(t, true);
                    foreach (Component c in comps)
                    {
                        if (c == null) continue;
                        ExtractClipsFromUnityObject(c, path, seen);
                    }
                }

                // 1b. AudioSource 컴포넌트 (toggle).
                if (_scanAudioSources)
                {
                    AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
                    foreach (AudioSource src in sources)
                    {
                        AddAudioSourceClip(src, path, seen);
                    }
                }
            }

            // 2. 열린 씬 스캔 (옵션).
            if (_includeOpenScenes)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;

                    GameObject[] roots = scene.GetRootGameObjects();
                    foreach (GameObject root in roots)
                    {
                        if (root == null) continue;
                        string srcLabel = $"[Scene] {scene.name}";

                        // 2a. 지정 binder 타입.
                        foreach (Type t in targetTypes)
                        {
                            Component[] comps = root.GetComponentsInChildren(t, true);
                            foreach (Component c in comps)
                            {
                                if (c == null) continue;
                                ExtractClipsFromUnityObject(c, srcLabel, seen);
                            }
                        }

                        // 2b. AudioSource 컴포넌트 (toggle).
                        if (_scanAudioSources)
                        {
                            AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
                            foreach (AudioSource src in sources)
                            {
                                AddAudioSourceClip(src, srcLabel, seen);
                            }
                        }
                    }
                }
            }

            // 2c. ScriptableObject 스캔 (지정 SO 타입, toggle).
            if (_scanScriptableObjects)
            {
                Type[] soTypes = TargetSoTypeNames
                    .Select(n => FindTypeInLoadedAssemblies(n))
                    .Where(t => t != null)
                    .ToArray();
                foreach (Type soType in soTypes)
                {
                    string[] soGuids = AssetDatabase.FindAssets($"t:{soType.Name}");
                    foreach (string guid in soGuids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        ScriptableObject so = AssetDatabase.LoadAssetAtPath(path, soType) as ScriptableObject;
                        if (so != null) ExtractClipsFromUnityObject(so, path, seen);
                    }
                }
            }

            // 3. 수동 추가된 클립 (세션 유지).
            foreach (AudioClip clip in _manualClips)
            {
                if (clip != null && seen.Add(clip))
                {
                    _discovered.Add(new DiscoveredClip
                    {
                        clip = clip,
                        componentTypeName = "(Manual)",
                        fieldPath = "",
                        prefabPath = ""
                    });
                }
            }

            // 4. SO 에 등록된 항목 중 스캔 안 된 것 (외부 등록, Scan & Register 로 추가된 것 포함).
            int orphanAdded = 0, orphanSkippedNull = 0, orphanSkippedSeen = 0;
            int soEntryCount = _balanceAsset != null ? _balanceAsset.Entries.Count : 0;
            System.Text.StringBuilder skippedList = new System.Text.StringBuilder();

            if (_balanceAsset != null)
            {
                foreach (SfxClipVolumeBalance.Entry e in _balanceAsset.Entries)
                {
                    if (e.clip == null)
                    {
                        orphanSkippedNull++;
                        skippedList.Append("[null] ");
                        continue;
                    }
                    if (seen.Contains(e.clip))
                    {
                        orphanSkippedSeen++;
                        skippedList.Append($"[in-seen: {e.clip.name}] ");
                        continue;
                    }
                    _discovered.Add(new DiscoveredClip
                    {
                        clip = e.clip,
                        componentTypeName = "zzz_Registered (외부 / 스캔 안 됨)",
                        fieldPath = "",
                        prefabPath = ""
                    });
                    seen.Add(e.clip);
                    orphanAdded++;
                }
            }
            int scannedCount = _discovered.Count - orphanAdded;
            Debug.Log($"[SfxBalance][진단] RefreshDiscovery 종료. " +
                      $"SO entries={soEntryCount}, " +
                      $"orphan added={orphanAdded}, " +
                      $"skipped(null)={orphanSkippedNull}, " +
                      $"skipped(이미 seen={scannedCount}개)={orphanSkippedSeen}, " +
                      $"_discovered.Count={_discovered.Count}, " +
                      $"seen.Count={seen.Count}. " +
                      $"skipped 상세: {skippedList}");

            // working gain 초기화 (SO 값으로).
            _workingGains.Clear();
            foreach (DiscoveredClip d in _discovered)
            {
                _workingGains[d.clip] = _balanceAsset != null ? _balanceAsset.GetGainFor(d.clip) : 1f;
            }
            _dirty = false;

            Repaint();
        }

        private void ExtractClipsFromUnityObject(UnityEngine.Object obj, string sourcePath, HashSet<AudioClip> seen)
        {
            if (obj == null) return;
            Type type = obj.GetType();
            string typeName = type.Name;

            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (FieldInfo f in fields)
            {
                if (!f.IsDefined(typeof(SerializeField), false) && !f.IsPublic) continue;

                if (f.FieldType == typeof(AudioClip))
                {
                    AudioClip clip = (AudioClip)f.GetValue(obj);
                    if (clip != null && seen.Add(clip))
                    {
                        _discovered.Add(new DiscoveredClip
                        {
                            clip = clip,
                            componentTypeName = typeName,
                            fieldPath = f.Name,
                            prefabPath = sourcePath
                        });
                    }
                }
                else if (f.FieldType == typeof(AudioClip[]))
                {
                    AudioClip[] arr = (AudioClip[])f.GetValue(obj);
                    if (arr == null) continue;
                    for (int i = 0; i < arr.Length; i++)
                    {
                        AudioClip clip = arr[i];
                        if (clip != null && seen.Add(clip))
                        {
                            _discovered.Add(new DiscoveredClip
                            {
                                clip = clip,
                                componentTypeName = typeName,
                                fieldPath = $"{f.Name}[{i}]",
                                prefabPath = sourcePath
                            });
                        }
                    }
                }
            }
        }

        private void AddAudioSourceClip(AudioSource src, string sourcePath, HashSet<AudioClip> seen)
        {
            if (src == null || src.clip == null) return;
            if (!seen.Add(src.clip)) return;
            _discovered.Add(new DiscoveredClip
            {
                clip = src.clip,
                componentTypeName = "AudioSource",
                fieldPath = $"on '{src.gameObject.name}'",
                prefabPath = sourcePath
            });
        }

        private static Type FindTypeInLoadedAssemblies(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        // ─── Asset I/O ───────────────────────────────────────────────────────

        private void LoadOrCreateAsset()
        {
            _balanceAsset = AssetDatabase.LoadAssetAtPath<SfxClipVolumeBalance>(BalanceAssetPath);
            if (_balanceAsset != null) return;

            // 폴더 보장.
            string dir = Path.GetDirectoryName(BalanceAssetPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string parent = Path.GetDirectoryName(dir);
                string leaf = Path.GetFileName(dir);
                AssetDatabase.CreateFolder(parent, leaf);
            }

            _balanceAsset = CreateInstance<SfxClipVolumeBalance>();
            AssetDatabase.CreateAsset(_balanceAsset, BalanceAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void SaveToAsset()
        {
            if (_balanceAsset == null) return;

            // working gains 를 SO 에 반영. 1.0 도 등록 유지 (Scan & Register 워크플로우 호환).
            // 항목이 SO 에 존재하면 = 등록된 클립 = 추적 대상. orphan 정리는 별도 버튼으로.
            foreach (KeyValuePair<AudioClip, float> kv in _workingGains)
            {
                _balanceAsset.SetGain(kv.Key, kv.Value);
            }

            EditorUtility.SetDirty(_balanceAsset);
            AssetDatabase.SaveAssets();

            // 런타임 static 캐시 무효화 → Play 모드 중이면 다음 SFX 부터 새 gain 반영.
            SfxClipVolumeBalance.InvalidateStaticCache();

            _dirty = false;
            Debug.Log($"[SfxBalance] Saved to {BalanceAssetPath}", _balanceAsset);
        }

        private void ResetAll()
        {
            foreach (DiscoveredClip d in _discovered)
            {
                _workingGains[d.clip] = 1f;
            }
            if (_balanceAsset != null)
            {
                _balanceAsset.ResetAll();
                EditorUtility.SetDirty(_balanceAsset);
                AssetDatabase.SaveAssets();
                SfxClipVolumeBalance.InvalidateStaticCache();
            }
            _dirty = false;
        }
    }
}

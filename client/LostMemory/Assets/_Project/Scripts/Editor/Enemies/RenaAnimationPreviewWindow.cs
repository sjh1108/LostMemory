using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.Enemies
{
    public sealed class RenaAnimationPreviewWindow : EditorWindow
    {
        private const string RootPath = "Assets/_Project/Art/Enemies/Boss/Rena";
        private const float MinFps = 1f;
        private const float MaxFps = 30f;
        private const float MinZoom = 1f;
        private const float MaxZoom = 8f;

        private readonly List<SequenceInfo> sequences = new List<SequenceInfo>();
        private readonly List<Texture2D> currentFrames = new List<Texture2D>();
        private readonly List<string> currentFramePaths = new List<string>();
        private readonly List<SequenceInfo> visibleSequences = new List<SequenceInfo>();

        private Vector2 sequenceScroll;
        private Vector2 previewScroll;
        private SequenceInfo selectedSequence;
        private string filter = string.Empty;
        private int currentFrameIndex;
        private float fps = 12f;
        private float zoom = 4f;
        private bool playing = true;
        private bool loop = true;
        private double lastFrameTime;

        [MenuItem("Lost Memory/Enemies/Rena Animation Preview")]
        private static void Open()
        {
            OpenWindow();
        }

        [MenuItem("Tools/LostMemory/Enemies/Rena Animation Preview")]
        private static void OpenFromToolsMenu()
        {
            OpenWindow();
        }

        private static void OpenWindow()
        {
            RenaAnimationPreviewWindow window = GetWindow<RenaAnimationPreviewWindow>("Rena Preview");
            window.minSize = new Vector2(760f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += HandleEditorUpdate;
            ScanSequences();
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                ScanSequences();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Filter", GUILayout.Width(36f));
            string nextFilter = GUILayout.TextField(filter, EditorStyles.toolbarTextField, GUILayout.MinWidth(160f));
            if (!string.Equals(nextFilter, filter, StringComparison.Ordinal))
            {
                filter = nextFilter;
                RebuildVisibleSequences();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(sequences.Count.ToString(CultureInfo.InvariantCulture) + " sequences");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawSequenceList();
            DrawPreviewPane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSequenceList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(280f));
            EditorGUILayout.LabelField("Sequences", EditorStyles.boldLabel);

            GUIStyle sequenceButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };

            sequenceScroll = EditorGUILayout.BeginScrollView(sequenceScroll);
            for (int i = 0; i < visibleSequences.Count; i++)
            {
                SequenceInfo sequence = visibleSequences[i];
                GUIContent content = new GUIContent(
                    sequence.DisplayName,
                    sequence.AssetFolderPath + "\n" + sequence.FramePaths.Count + " frames");

                bool selected = selectedSequence == sequence;
                if (GUILayout.Toggle(selected, content, sequenceButtonStyle))
                {
                    if (!selected)
                    {
                        SelectSequence(sequence);
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            if (visibleSequences.Count == 0)
            {
                EditorGUILayout.HelpBox("No PNG frame sequence found.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewPane()
        {
            EditorGUILayout.BeginVertical();

            if (selectedSequence == null)
            {
                EditorGUILayout.HelpBox("Select a sequence.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField(selectedSequence.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(selectedSequence.AssetFolderPath, EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(playing ? "Pause" : "Play", GUILayout.Width(70f)))
            {
                playing = !playing;
                lastFrameTime = EditorApplication.timeSinceStartup;
            }

            using (new EditorGUI.DisabledScope(currentFrames.Count == 0))
            {
                if (GUILayout.Button("Prev", GUILayout.Width(58f)))
                {
                    StepFrame(-1);
                }

                if (GUILayout.Button("Next", GUILayout.Width(58f)))
                {
                    StepFrame(1);
                }
            }

            loop = GUILayout.Toggle(loop, "Loop", GUILayout.Width(58f));
            GUILayout.Space(8f);
            GUILayout.Label("FPS", GUILayout.Width(28f));
            fps = EditorGUILayout.Slider(fps, MinFps, MaxFps, GUILayout.Width(180f));
            GUILayout.Label("Zoom", GUILayout.Width(40f));
            zoom = EditorGUILayout.Slider(zoom, MinZoom, MaxZoom, GUILayout.Width(180f));
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Select Folder", GUILayout.Width(100f)))
            {
                SelectFolderInProject(selectedSequence.AssetFolderPath);
            }

            EditorGUILayout.EndHorizontal();

            string frameLabel = currentFrames.Count > 0
                ? "Frame " + (currentFrameIndex + 1).ToString(CultureInfo.InvariantCulture) + " / " +
                  currentFrames.Count.ToString(CultureInfo.InvariantCulture) + "  " +
                  Path.GetFileName(currentFramePaths[currentFrameIndex])
                : "No frames loaded";
            EditorGUILayout.LabelField(frameLabel, EditorStyles.miniLabel);

            Rect previewRect = GUILayoutUtility.GetRect(
                240f,
                10000f,
                240f,
                10000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            DrawPreviewBackground(previewRect);

            if (currentFrames.Count > 0)
            {
                DrawCurrentFrame(previewRect);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.14f, 1f));
            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.05f);
            const float step = 32f;
            for (float x = rect.xMin; x < rect.xMax; x += step)
            {
                Handles.DrawLine(new Vector3(x, rect.yMin, 0f), new Vector3(x, rect.yMax, 0f));
            }
            for (float y = rect.yMin; y < rect.yMax; y += step)
            {
                Handles.DrawLine(new Vector3(rect.xMin, y, 0f), new Vector3(rect.xMax, y, 0f));
            }
            Handles.EndGUI();
        }

        private void DrawCurrentFrame(Rect previewRect)
        {
            Texture2D texture = currentFrames[currentFrameIndex];
            if (texture == null)
            {
                return;
            }

            float width = texture.width * zoom;
            float height = texture.height * zoom;
            float contentWidth = Mathf.Max(previewRect.width, width);
            float contentHeight = Mathf.Max(previewRect.height, height);
            Rect contentRect = new Rect(0f, 0f, contentWidth, contentHeight);
            Rect textureRect = new Rect(
                (contentWidth - width) * 0.5f,
                (contentHeight - height) * 0.5f,
                width,
                height);

            previewScroll = GUI.BeginScrollView(previewRect, previewScroll, contentRect);
            GUI.DrawTexture(textureRect, texture, ScaleMode.ScaleToFit, true, 0f);
            GUI.EndScrollView();
        }

        private void HandleEditorUpdate()
        {
            if (!playing || currentFrames.Count == 0 || selectedSequence == null)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            double frameDuration = 1d / Mathf.Max(MinFps, fps);
            if (now - lastFrameTime < frameDuration)
            {
                return;
            }

            int frameSteps = Math.Max(1, (int)((now - lastFrameTime) / frameDuration));
            currentFrameIndex += frameSteps;

            if (currentFrameIndex >= currentFrames.Count)
            {
                if (loop)
                {
                    currentFrameIndex %= currentFrames.Count;
                }
                else
                {
                    currentFrameIndex = currentFrames.Count - 1;
                    playing = false;
                }
            }

            lastFrameTime = now;
            Repaint();
        }

        private void ScanSequences()
        {
            sequences.Clear();

            if (!AssetDatabase.IsValidFolder(RootPath))
            {
                selectedSequence = null;
                currentFrames.Clear();
                currentFramePaths.Clear();
                RebuildVisibleSequences();
                return;
            }

            Dictionary<string, List<string>> groupedPaths = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { RootPath });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string folderPath = NormalizePath(Path.GetDirectoryName(assetPath));
                if (string.IsNullOrEmpty(folderPath))
                {
                    continue;
                }

                if (!groupedPaths.TryGetValue(folderPath, out List<string> paths))
                {
                    paths = new List<string>();
                    groupedPaths.Add(folderPath, paths);
                }

                paths.Add(assetPath);
            }

            foreach (KeyValuePair<string, List<string>> pair in groupedPaths)
            {
                List<string> paths = pair.Value;
                if (paths.Count < 2)
                {
                    continue;
                }

                paths.Sort(CompareAssetPathsNaturally);
                sequences.Add(new SequenceInfo(pair.Key, BuildDisplayName(pair.Key), paths));
            }

            sequences.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
            RebuildVisibleSequences();

            if (selectedSequence == null || !sequences.Contains(selectedSequence))
            {
                SelectSequence(FindPreferredInitialSequence());
            }
        }

        private void RebuildVisibleSequences()
        {
            visibleSequences.Clear();
            for (int i = 0; i < sequences.Count; i++)
            {
                SequenceInfo sequence = sequences[i];
                if (MatchesFilter(sequence))
                {
                    visibleSequences.Add(sequence);
                }
            }
        }

        private SequenceInfo FindPreferredInitialSequence()
        {
            SequenceInfo fallback = sequences.Count > 0 ? sequences[0] : null;
            for (int i = 0; i < sequences.Count; i++)
            {
                if (sequences[i].DisplayName.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return sequences[i];
                }
            }

            return fallback;
        }

        private bool MatchesFilter(SequenceInfo sequence)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            return sequence.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   sequence.AssetFolderPath.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SelectSequence(SequenceInfo sequence)
        {
            selectedSequence = sequence;
            currentFrames.Clear();
            currentFramePaths.Clear();
            currentFrameIndex = 0;
            previewScroll = Vector2.zero;
            lastFrameTime = EditorApplication.timeSinceStartup;

            if (sequence == null)
            {
                return;
            }

            for (int i = 0; i < sequence.FramePaths.Count; i++)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sequence.FramePaths[i]);
                if (texture != null)
                {
                    currentFrames.Add(texture);
                    currentFramePaths.Add(sequence.FramePaths[i]);
                }
            }
        }

        private void StepFrame(int offset)
        {
            if (currentFrames.Count == 0)
            {
                return;
            }

            currentFrameIndex += offset;
            if (currentFrameIndex < 0)
            {
                currentFrameIndex = loop ? currentFrames.Count - 1 : 0;
            }
            else if (currentFrameIndex >= currentFrames.Count)
            {
                currentFrameIndex = loop ? 0 : currentFrames.Count - 1;
            }

            lastFrameTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private static void SelectFolderInProject(string folderPath)
        {
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folderPath);
            if (folder == null)
            {
                return;
            }

            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        private static string BuildDisplayName(string folderPath)
        {
            string relative = folderPath.StartsWith(RootPath, StringComparison.Ordinal)
                ? folderPath.Substring(RootPath.Length).TrimStart('/')
                : folderPath;

            return string.IsNullOrEmpty(relative)
                ? "(root)"
                : relative.Replace("/", " / ");
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/');
        }

        private static int CompareAssetPathsNaturally(string leftPath, string rightPath)
        {
            string left = Path.GetFileNameWithoutExtension(leftPath);
            string right = Path.GetFileNameWithoutExtension(rightPath);
            return CompareNaturally(left, right);
        }

        private static int CompareNaturally(string left, string right)
        {
            int leftIndex = 0;
            int rightIndex = 0;

            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                char leftChar = left[leftIndex];
                char rightChar = right[rightIndex];

                if (char.IsDigit(leftChar) && char.IsDigit(rightChar))
                {
                    long leftNumber = ReadNumber(left, ref leftIndex);
                    long rightNumber = ReadNumber(right, ref rightIndex);
                    int numberCompare = leftNumber.CompareTo(rightNumber);
                    if (numberCompare != 0)
                    {
                        return numberCompare;
                    }

                    continue;
                }

                int charCompare = char.ToUpperInvariant(leftChar).CompareTo(char.ToUpperInvariant(rightChar));
                if (charCompare != 0)
                {
                    return charCompare;
                }

                leftIndex++;
                rightIndex++;
            }

            return left.Length.CompareTo(right.Length);
        }

        private static long ReadNumber(string value, ref int index)
        {
            long number = 0;
            while (index < value.Length && char.IsDigit(value[index]))
            {
                number = number * 10 + (value[index] - '0');
                index++;
            }

            return number;
        }

        private sealed class SequenceInfo
        {
            public SequenceInfo(string assetFolderPath, string displayName, List<string> framePaths)
            {
                AssetFolderPath = assetFolderPath;
                DisplayName = displayName;
                FramePaths = framePaths;
            }

            public string AssetFolderPath { get; }
            public string DisplayName { get; }
            public List<string> FramePaths { get; }
        }
    }
}

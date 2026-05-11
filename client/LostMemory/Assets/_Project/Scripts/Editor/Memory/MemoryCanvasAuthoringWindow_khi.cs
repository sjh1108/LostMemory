using System.Collections.Generic;
using System.IO;
using LostMemory.Memory;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.Memory
{
    /// <summary>
    /// MemoryData_khi(캔버스)의 그리드 × 조각 정합성 검증 + 누락 MemoryFragmentData 일괄 생성.
    /// 테스트용. 원본 MemoryData 와 동작 분리.
    ///
    /// 메뉴: LostMemory/Memory/Canvas Authoring Window (khi)
    ///
    /// 워크플로우:
    ///   1) MemoryData_khi SO 작성, GridWidth/GridHeight 설정.
    ///   2) Artwork 할당.
    ///   3) 이 윈도우에서 캔버스 선택 → 검증 → "Generate Missing Pieces" 클릭.
    ///   4) 같은 폴더의 _Pieces 서브폴더에 Piece_{Order:D2}.asset 자동 생성.
    ///   5) Fragments 배열이 자동 채워짐.
    /// </summary>
    public sealed class MemoryCanvasAuthoringWindow_khi : EditorWindow
    {
        private MemoryData_khi _canvas;

        [MenuItem("LostMemory/Memory/Canvas Authoring Window (khi)")]
        private static void Open()
        {
            GetWindow<MemoryCanvasAuthoringWindow_khi>("Memory Canvas Authoring (khi)");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Memory Canvas Authoring (khi)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _canvas = (MemoryData_khi)EditorGUILayout.ObjectField(
                "Target Canvas", _canvas, typeof(MemoryData_khi), false);

            if (_canvas == null)
            {
                EditorGUILayout.HelpBox("검사·생성할 MemoryData_khi(캔버스) SO 를 지정하세요.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            DrawValidation(_canvas);

            EditorGUILayout.Space();
            DrawActions(_canvas);

            EditorGUILayout.Space();
            DrawFragmentList(_canvas);
        }

        private static void DrawValidation(MemoryData_khi canvas)
        {
            int width = canvas.GridWidth;
            int height = canvas.GridHeight;
            int expected = width * height;
            int actual = canvas.Fragments?.Count ?? 0;

            EditorGUILayout.LabelField("검증 결과", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  Grid     : {width} × {height} = {expected}");
            EditorGUILayout.LabelField($"  Fragments: {actual}");

            if (actual != expected)
            {
                EditorGUILayout.HelpBox(
                    $"Fragments 수({actual}) 가 Grid 칸수({expected}) 와 다릅니다.",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox("Grid × Fragments 일치.", MessageType.Info);
            }

            var artwork = canvas.Artwork;
            if (artwork == null || artwork.texture == null)
            {
                EditorGUILayout.HelpBox("Artwork 가 비어있습니다.", MessageType.Warning);
            }
            else
            {
                int tw = artwork.texture.width;
                int th = artwork.texture.height;
                if (tw % width != 0 || th % height != 0)
                {
                    EditorGUILayout.HelpBox(
                        $"Artwork ({tw}×{th}) 가 Grid ({width}×{height}) 로 정확히 나뉘지 않습니다. 픽셀 정렬 깨질 수 있음.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField($"  Cell px  : {tw / width} × {th / height}");
                }
            }
        }

        private static void DrawActions(MemoryData_khi canvas)
        {
            EditorGUILayout.LabelField("작업", EditorStyles.boldLabel);

            int expected = canvas.GridWidth * canvas.GridHeight;
            int actual = canvas.Fragments?.Count ?? 0;
            int missing = Mathf.Max(0, expected - actual);

            using (new EditorGUI.DisabledScope(missing == 0))
            {
                if (GUILayout.Button($"Generate Missing Pieces ({missing})"))
                {
                    GenerateMissingPieces(canvas);
                }
            }

            using (new EditorGUI.DisabledScope(actual == 0))
            {
                if (GUILayout.Button("Re-link Pieces (parentCanvas + array order)"))
                {
                    RelinkPieces(canvas);
                }
            }
        }

        private static void DrawFragmentList(MemoryData_khi canvas)
        {
            EditorGUILayout.LabelField("Fragments (Order 순)", EditorStyles.boldLabel);
            if (canvas.Fragments == null) return;
            foreach (var p in canvas.Fragments)
            {
                if (p == null)
                {
                    EditorGUILayout.LabelField("  <NULL>");
                    continue;
                }
                EditorGUILayout.LabelField($"  [{p.Order:D2}] {p.FragmentId} — cost {p.ShardCost} — {p.RewardType}");
            }
        }

        // ── 작업 ─────────────────────────────────────────────

        private static void GenerateMissingPieces(MemoryData_khi canvas)
        {
            string canvasPath = AssetDatabase.GetAssetPath(canvas);
            string dir = Path.GetDirectoryName(canvasPath);
            string canvasName = Path.GetFileNameWithoutExtension(canvasPath);
            string piecesDir = $"{dir}/{canvasName}_Pieces".Replace('\\', '/');

            if (!AssetDatabase.IsValidFolder(piecesDir))
            {
                string parent = Path.GetDirectoryName(piecesDir)?.Replace('\\', '/');
                string leaf = Path.GetFileName(piecesDir);
                AssetDatabase.CreateFolder(parent, leaf);
            }

            int expected = canvas.GridWidth * canvas.GridHeight;
            var existingByOrder = new Dictionary<int, MemoryFragmentData>();
            if (canvas.Fragments != null)
            {
                foreach (var p in canvas.Fragments)
                {
                    if (p != null) existingByOrder[p.Order] = p;
                }
            }

            var so = new SerializedObject(canvas);
            SerializedProperty fragmentsProp = so.FindProperty("_fragments");
            fragmentsProp.arraySize = expected;

            int created = 0;
            for (int order = 0; order < expected; order++)
            {
                MemoryFragmentData piece;
                if (existingByOrder.TryGetValue(order, out piece))
                {
                    SerializedObject pso = new SerializedObject(piece);
                    pso.FindProperty("_parentCanvas").objectReferenceValue = canvas;
                    pso.FindProperty("_order").intValue = order;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    piece = ScriptableObject.CreateInstance<MemoryFragmentData>();
                    string assetPath = $"{piecesDir}/{canvasName}_Piece_{order:D2}.asset";
                    AssetDatabase.CreateAsset(piece, assetPath);

                    SerializedObject pso = new SerializedObject(piece);
                    pso.FindProperty("_fragmentId").stringValue = $"{canvasName}_Piece_{order:D2}";
                    pso.FindProperty("_order").intValue = order;
                    pso.FindProperty("_parentCanvas").objectReferenceValue = canvas;
                    pso.FindProperty("_shardCost").intValue = 1;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    created++;
                }

                fragmentsProp.GetArrayElementAtIndex(order).objectReferenceValue = piece;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(canvas);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MemoryCanvasAuthoring_khi] '{canvasName}' 에 누락 조각 {created}개 생성. 총 {expected}슬롯 채움.", canvas);
        }

        private static void RelinkPieces(MemoryData_khi canvas)
        {
            if (canvas.Fragments == null) return;

            int order = 0;
            foreach (var piece in canvas.Fragments)
            {
                if (piece == null) { order++; continue; }
                SerializedObject pso = new SerializedObject(piece);
                pso.FindProperty("_parentCanvas").objectReferenceValue = canvas;
                pso.FindProperty("_order").intValue = order;
                pso.ApplyModifiedPropertiesWithoutUndo();
                order++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MemoryCanvasAuthoring_khi] '{canvas.name}' Pieces relinked. count={order}", canvas);
        }
    }
}

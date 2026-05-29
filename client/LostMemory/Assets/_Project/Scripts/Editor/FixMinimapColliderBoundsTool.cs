using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LostMemory.EditorTools
{
    /// <summary>
    /// Map/Modules/ 하위 모든 prefab 일괄 검사 → 각 module 의 'Minimap' 자식 GameObject 에 붙은
    /// BoxCollider2D 의 size/offset 을 해당 module 의 실제 Tilemap bounds 에 맞춰 갱신.
    ///
    /// 배경: 2F module 들 (30개) 의 Minimap collider 가 모두 24x17 로 고정돼 있어 실제 방
    /// (34~47 × 27~29) 보다 작음 → MinimapRoomReveal 이 collider.bounds 를 그대로
    /// MinimapFog.RevealBounds() 에 넘기므로 미니맵에서 방이 잘려 보임. 1F 일반 module 은 적정 크기.
    ///
    /// 사용:
    /// 1. Tools > Lost Memory > Modules > [DRY RUN] List Minimap Collider Mismatch
    ///    → 처리 대상 prefab 목록을 콘솔에 출력만 (변경 없음).
    /// 2. 콘솔 확인 후 OK 면 git commit 으로 백업.
    /// 3. Tools > Lost Memory > Modules > [APPLY] Fix Minimap Collider Bounds
    ///    → 확인 대화상자 후 실제 prefab 수정.
    ///
    /// 안전성:
    /// - "차이 0.5 이하면 skip" → 이미 적절한 1F module 회귀 차단.
    /// - PrefabUtility.LoadPrefabContents / SaveAsPrefabAsset 사용.
    /// - 처리 중 실패하면 해당 prefab 만 스킵.
    /// </summary>
    public static class FixMinimapColliderBoundsTool
    {
        private const string ModulesFolder = "Assets/_Project/Map/Modules";
        private const string MinimapChildName = "Minimap";
        private const string MenuRoot = "Tools/Lost Memory/Modules/";
        private const float SkipThreshold = 0.5f;  // 차이가 이보다 작으면 이미 적절한 것으로 간주

        [MenuItem(MenuRoot + "[DRY RUN] List Minimap Collider Mismatch")]
        public static void DryRun()
        {
            Process(applyChanges: false);
        }

        [MenuItem(MenuRoot + "[APPLY] Fix Minimap Collider Bounds")]
        public static void Apply()
        {
            bool confirm = EditorUtility.DisplayDialog(
                title: "Fix Minimap Collider Bounds",
                message:
                    $"'{ModulesFolder}' 하위 모든 prefab 검사 후\n" +
                    "각 module 의 'Minimap' 자식의 BoxCollider2D 를 실제 Tilemap bounds 에 맞게 수정합니다.\n\n" +
                    "권장: 진행 전에 git commit 으로 백업하세요.\n\n" +
                    "진행할까요?",
                ok: "OK",
                cancel: "Cancel");
            if (!confirm) return;

            Process(applyChanges: true);
        }

        private static void Process(bool applyChanges)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ModulesFolder });
            if (guids == null || guids.Length == 0)
            {
                Debug.LogWarning($"[FixMinimapColliderBounds] '{ModulesFolder}' 에서 prefab 못 찾음. 경로 확인.");
                return;
            }

            List<string> toFix = new List<string>();      // 처리 대상 (size 변경 필요)
            List<string> alreadyOk = new List<string>();   // 이미 적절 (skip)
            List<string> noMinimap = new List<string>();   // Minimap 자식 없음 (skip)
            List<string> noCollider = new List<string>();  // BoxCollider2D 없음 (skip)
            List<string> noTilemap = new List<string>();   // Tilemap 없음 (skip)
            List<string> failed = new List<string>();
            int appliedCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar(
                        title: applyChanges ? "Applying Minimap Fix" : "Dry-run: Scanning Modules",
                        info: $"{i + 1}/{guids.Length}  {System.IO.Path.GetFileName(path)}",
                        progress: (float)(i + 1) / guids.Length);

                    GameObject root = null;
                    try
                    {
                        root = PrefabUtility.LoadPrefabContents(path);
                        if (root == null)
                        {
                            failed.Add($"{path} (LoadPrefabContents returned null)");
                            continue;
                        }

                        Transform minimapGo = FindChildRecursive(root.transform, MinimapChildName);
                        if (minimapGo == null)
                        {
                            noMinimap.Add(path);
                            continue;
                        }

                        BoxCollider2D box = minimapGo.GetComponent<BoxCollider2D>();
                        if (box == null)
                        {
                            noCollider.Add(path);
                            continue;
                        }

                        Tilemap[] tilemaps = root.GetComponentsInChildren<Tilemap>(includeInactive: true);
                        if (!TryComputeWorldBounds(tilemaps, out Bounds worldBounds))
                        {
                            noTilemap.Add(path);
                            continue;
                        }

                        // 현재 collider 의 world size 와 계산된 size 비교.
                        // BoxCollider2D.size 는 local — scale 영향 받음. lossyScale 곱해서 world size 추정.
                        Vector2 minimapLossyXY = new Vector2(
                            Mathf.Abs(minimapGo.lossyScale.x),
                            Mathf.Abs(minimapGo.lossyScale.y));
                        Vector2 currentWorldSize = box.size * minimapLossyXY;
                        Vector2 targetWorldSize = new Vector2(worldBounds.size.x, worldBounds.size.y);
                        float diff = Mathf.Max(
                            Mathf.Abs(currentWorldSize.x - targetWorldSize.x),
                            Mathf.Abs(currentWorldSize.y - targetWorldSize.y));

                        if (diff <= SkipThreshold)
                        {
                            alreadyOk.Add($"{path} (size {currentWorldSize.x:F1}x{currentWorldSize.y:F1})");
                            continue;
                        }

                        // 새 local size/offset 계산.
                        // world bounds 를 minimapGo local 로 변환 — scale, position 모두 고려.
                        Vector3 localCenter = minimapGo.InverseTransformPoint(worldBounds.center);
                        Vector2 newLocalSize = new Vector2(
                            minimapLossyXY.x > Mathf.Epsilon ? targetWorldSize.x / minimapLossyXY.x : targetWorldSize.x,
                            minimapLossyXY.y > Mathf.Epsilon ? targetWorldSize.y / minimapLossyXY.y : targetWorldSize.y);
                        Vector2 newLocalOffset = new Vector2(localCenter.x, localCenter.y);

                        toFix.Add($"{path}  ({currentWorldSize.x:F0}x{currentWorldSize.y:F0} → {targetWorldSize.x:F0}x{targetWorldSize.y:F0})");

                        if (applyChanges)
                        {
                            box.size = newLocalSize;
                            box.offset = newLocalOffset;
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            appliedCount++;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        failed.Add($"{path} ({ex.GetType().Name}: {ex.Message})");
                    }
                    finally
                    {
                        if (root != null) PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                if (applyChanges)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }

            // === 결과 출력 ===
            string mode = applyChanges ? "APPLIED" : "DRY-RUN";
            Debug.Log($"[FixMinimapColliderBounds] === 결과 ({mode}) ===");

            Debug.Log($"[FixMinimapColliderBounds] 수정 대상: {toFix.Count}개" +
                      (applyChanges ? $" → {appliedCount}개 적용 완료" : " (실제 변경 없음, dry-run)"));
            for (int i = 0; i < toFix.Count; i++)
            {
                Debug.Log($"  + {toFix[i]}");
            }

            Debug.Log($"[FixMinimapColliderBounds] 이미 적절한 크기 (건너뜀): {alreadyOk.Count}개");
            // 너무 많을 수 있어 첫 5개만 상세 출력.
            for (int i = 0; i < Mathf.Min(5, alreadyOk.Count); i++)
            {
                Debug.Log($"  = {alreadyOk[i]}");
            }
            if (alreadyOk.Count > 5) Debug.Log($"  ... ({alreadyOk.Count - 5} more)");

            if (noMinimap.Count > 0) Debug.Log($"[FixMinimapColliderBounds] 'Minimap' 자식 없음: {noMinimap.Count}개");
            if (noCollider.Count > 0) Debug.LogWarning($"[FixMinimapColliderBounds] Minimap 에 BoxCollider2D 없음: {noCollider.Count}개");
            for (int i = 0; i < noCollider.Count; i++) Debug.LogWarning($"  ! {noCollider[i]}");
            if (noTilemap.Count > 0) Debug.LogWarning($"[FixMinimapColliderBounds] Tilemap 없음/모두 빈 상태: {noTilemap.Count}개");
            for (int i = 0; i < noTilemap.Count; i++) Debug.LogWarning($"  ! {noTilemap[i]}");

            if (failed.Count > 0)
            {
                Debug.LogError($"[FixMinimapColliderBounds] 실패: {failed.Count}개");
                for (int i = 0; i < failed.Count; i++) Debug.LogError($"  ! {failed[i]}");
            }

            Debug.Log($"[FixMinimapColliderBounds] === 끝 ({mode}) ===");
        }

        /// <summary>이름이 일치하는 첫 descendant transform 반환 (DFS). 없으면 null.</summary>
        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 주어진 Tilemap 들의 world bounds 합 계산.
        /// 모두 비어있거나 0 개면 false. 빈 tilemap (cellBounds.size == zero) 은 skip.
        /// </summary>
        private static bool TryComputeWorldBounds(Tilemap[] tilemaps, out Bounds worldBounds)
        {
            worldBounds = default;
            bool any = false;
            if (tilemaps == null) return false;

            for (int i = 0; i < tilemaps.Length; i++)
            {
                Tilemap tm = tilemaps[i];
                if (tm == null) continue;

                // 빈 tilemap skip — cellBounds 가 (0,0,0,0,0,0) 이거나 localBounds 가 0 size.
                BoundsInt cb = tm.cellBounds;
                if (cb.size.x == 0 || cb.size.y == 0) continue;

                Bounds local = tm.localBounds;
                if (local.size.x <= Mathf.Epsilon && local.size.y <= Mathf.Epsilon) continue;

                // local → world: 8 corners 변환 후 encapsulate.
                Bounds wb = TransformBounds(tm.transform, local);
                if (!any)
                {
                    worldBounds = wb;
                    any = true;
                }
                else
                {
                    worldBounds.Encapsulate(wb);
                }
            }

            return any;
        }

        private static Bounds TransformBounds(Transform t, Bounds local)
        {
            Vector3 min = local.min;
            Vector3 max = local.max;
            // 2D 라 z 무시 가능하지만 호환성 위해 8 corners.
            Vector3 c0 = t.TransformPoint(new Vector3(min.x, min.y, min.z));
            Vector3 c1 = t.TransformPoint(new Vector3(max.x, min.y, min.z));
            Vector3 c2 = t.TransformPoint(new Vector3(min.x, max.y, min.z));
            Vector3 c3 = t.TransformPoint(new Vector3(max.x, max.y, min.z));
            Vector3 c4 = t.TransformPoint(new Vector3(min.x, min.y, max.z));
            Vector3 c5 = t.TransformPoint(new Vector3(max.x, min.y, max.z));
            Vector3 c6 = t.TransformPoint(new Vector3(min.x, max.y, max.z));
            Vector3 c7 = t.TransformPoint(new Vector3(max.x, max.y, max.z));

            Bounds b = new Bounds(c0, Vector3.zero);
            b.Encapsulate(c1);
            b.Encapsulate(c2);
            b.Encapsulate(c3);
            b.Encapsulate(c4);
            b.Encapsulate(c5);
            b.Encapsulate(c6);
            b.Encapsulate(c7);
            return b;
        }
    }
}

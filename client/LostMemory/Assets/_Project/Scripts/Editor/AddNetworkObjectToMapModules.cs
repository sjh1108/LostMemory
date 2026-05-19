using System.Collections.Generic;
using LostMemory.Stage;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace LostMemory.EditorTools
{
    /// <summary>
    /// Map/Modules/ 하위 모든 prefab 일괄 검사 → RoomEntryRuntimeController 가 있는데
    /// (자기 또는 부모 트리에) NetworkObject 가 없으면 module root 에 NetworkObject 추가.
    ///
    /// NGO 규칙: NetworkObject 는 spawn 단위 root 에 있어야 하고, 같은 트리의 NetworkBehaviour 들이
    /// 자동으로 그 NetworkObject 를 부모로 인식. 따라서 controller 가 root 든 child 든 NetworkObject 는 module root 에 둠.
    ///
    /// 사용:
    /// 1. Tools > Lost Memory > Modules > [DRY RUN] List Modules Needing NetworkObject
    ///    → 처리 대상 prefab 목록을 콘솔에 출력만 (변경 없음).
    /// 2. 콘솔 확인 후 OK 면:
    /// 3. Tools > Lost Memory > Modules > [APPLY] Add NetworkObject To Map Modules
    ///    → 확인 대화상자 후 실제 prefab 수정.
    ///
    /// 안전성:
    /// - PrefabUtility.LoadPrefabContents / SaveAsPrefabAsset 사용 → Unity 가 GlobalObjectIdHash 자동 생성.
    /// - 이미 NetworkObject 가 있는 prefab 은 건너뜀.
    /// - 처리 중 실패하면 해당 prefab 만 스킵 (전체 중단 X).
    /// - Git 으로 단일 커밋 백업 후 실행 권장.
    /// </summary>
    public static class AddNetworkObjectToMapModules
    {
        private const string ModulesFolder = "Assets/_Project/Map/Modules";
        private const string MenuRoot = "Tools/Lost Memory/Modules/";

        [MenuItem(MenuRoot + "[DRY RUN] List Modules Needing NetworkObject")]
        public static void DryRun()
        {
            Process(applyChanges: false);
        }

        [MenuItem(MenuRoot + "[APPLY] Add NetworkObject To Map Modules")]
        public static void Apply()
        {
            bool confirm = EditorUtility.DisplayDialog(
                title: "Add NetworkObject to Map Modules",
                message:
                    $"'{ModulesFolder}' 하위 모든 prefab 검사 후\n" +
                    "RoomEntryRuntimeController 가 있는데 NetworkObject 가 없는 module root 에 NetworkObject 추가합니다.\n\n" +
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
                Debug.LogWarning($"[AddNetworkObjectToMapModules] '{ModulesFolder}' 에서 prefab 못 찾음. 경로 확인.");
                return;
            }

            List<string> needsAdd = new List<string>();
            List<string> alreadyHas = new List<string>();
            List<string> noController = new List<string>();
            List<string> failed = new List<string>();
            int appliedCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar(
                        title: applyChanges ? "Applying NetworkObject" : "Dry-run: Scanning Modules",
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

                        // RoomEntryRuntimeController 가 있는지 (자식 포함).
                        RoomEntryRuntimeController controller =
                            root.GetComponentInChildren<RoomEntryRuntimeController>(includeInactive: true);
                        if (controller == null)
                        {
                            noController.Add(path);
                            continue;
                        }

                        // controller 의 부모 트리에 이미 NetworkObject 있나? (root 또는 중간 어딘가)
                        NetworkObject existing = controller.GetComponentInParent<NetworkObject>(includeInactive: true);
                        if (existing != null)
                        {
                            alreadyHas.Add($"{path} (on '{existing.gameObject.name}')");
                            continue;
                        }

                        // 추가 대상.
                        needsAdd.Add(path);

                        if (applyChanges)
                        {
                            // NGO 규칙: module root 에 NetworkObject 부착.
                            root.AddComponent<NetworkObject>();
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
            Debug.Log($"[AddNetworkObjectToMapModules] === 결과 ({mode}) ===");

            Debug.Log($"[AddNetworkObjectToMapModules] 처리 대상 (controller 있는데 NetworkObject 없음): {needsAdd.Count}개" +
                      (applyChanges ? $" → {appliedCount}개 추가 완료" : " (실제 변경 없음, dry-run)"));
            for (int i = 0; i < needsAdd.Count; i++)
            {
                Debug.Log($"  + {needsAdd[i]}");
            }

            Debug.Log($"[AddNetworkObjectToMapModules] 이미 NetworkObject 있음 (건너뜀): {alreadyHas.Count}개");
            for (int i = 0; i < alreadyHas.Count; i++)
            {
                Debug.Log($"  = {alreadyHas[i]}");
            }

            Debug.Log($"[AddNetworkObjectToMapModules] RoomEntryRuntimeController 없음 (건너뜀): {noController.Count}개");
            // controller 없는 prefab 은 너무 많을 수 있어 path 상세 출력은 생략. 필요하면 아래 주석 해제.
            // for (int i = 0; i < noController.Count; i++) Debug.Log($"  - {noController[i]}");

            if (failed.Count > 0)
            {
                Debug.LogWarning($"[AddNetworkObjectToMapModules] 실패: {failed.Count}개");
                for (int i = 0; i < failed.Count; i++)
                {
                    Debug.LogWarning($"  ! {failed[i]}");
                }
            }

            Debug.Log($"[AddNetworkObjectToMapModules] === 끝 ({mode}) ===");
        }
    }
}

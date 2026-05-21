using System.Collections.Generic;
using System.IO;
using LostMemory.Networking.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.EditorTools.Networking
{
    /// <summary>
    /// 던전/Town 씬에 scene-placed `TestKhi_MinimalCharacter2D` (PrefabInstance) 가
    /// EditorTestCharacterMarker 없이 배치되어 있으면 자동 부착 → NGO 시작 시 자동 Destroy 보장.
    ///
    /// 문제 배경: Town_solo_Copy 의 'Player' 에는 EditorTestCharacterMarker 가 있어서 NGO 시작 시 destroy 되지만,
    /// 던전 씬 14개+ 의 scene-placed TestKhi_MinimalCharacter2D 에는 마커가 없어 호스트 화면에 두 캐릭터가 공존.
    ///
    /// 사용:
    ///   1. 메뉴: Lost Memory > Sync > Add EditorTestCharacterMarker To Scene-Placed Players
    ///   2. 모든 _Project/Scenes/**/*.unity 자동 순회
    ///   3. TestKhi_MinimalCharacter2D 의 PrefabInstance 발견 시 마커 없으면 부착, 씬 저장
    ///   4. 다이얼로그에 결과 표시 (Idempotent — 이미 부착된 씬은 skip)
    /// </summary>
    public static class AddSceneCharacterMarkerMenu
    {
        private const string MenuPath = "Lost Memory/Sync/Add EditorTestCharacterMarker To Scene-Placed Players";

        // TestKhi_MinimalCharacter2D.prefab 의 GUID (이 게임의 표준 PlayerPrefab)
        private const string PlayerPrefabGuid = "4d290d1fc0f84526999c421c61df5d8f";

        [MenuItem(MenuPath)]
        private static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[AddSceneCharacterMarker] 사용자가 현재 씬 저장 취소 — 중단.");
                return;
            }

            string currentScenePath = SceneManager.GetActiveScene().path;

            // 프로젝트 내 모든 _Project/Scenes 하위 .unity 파일 검색
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" });

            int processedScenes = 0;
            int markersAdded = 0;
            int alreadyHadMarker = 0;
            List<string> updatedScenePaths = new List<string>();
            List<string> noPlayerScenePaths = new List<string>();

            string playerPrefabPath = AssetDatabase.GUIDToAssetPath(PlayerPrefabGuid);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (playerPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", $"PlayerPrefab 못 찾음. GUID={PlayerPrefabGuid}", "OK");
                return;
            }

            try
            {
                foreach (string guid in sceneGuids)
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(scenePath)) continue;

                    EditorUtility.DisplayProgressBar("Add Scene Character Marker",
                        $"검사 중: {Path.GetFileName(scenePath)}",
                        (float)processedScenes / sceneGuids.Length);

                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    processedScenes++;

                    bool sceneDirty = false;
                    bool foundPlayerInstance = false;

                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        // root + 자식 통합 검색 (PrefabInstance 가 root 가 아닌 경우도 있음)
                        Transform[] all = root.GetComponentsInChildren<Transform>(true);
                        foreach (Transform t in all)
                        {
                            GameObject go = t.gameObject;
                            // PrefabInstance source 가 PlayerPrefab 인지 확인
                            Object source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                            if (source == null) continue;
                            // GetCorrespondingObjectFromSource 는 가장 가까운 prefab 반환.
                            // PrefabInstance 의 root path 비교가 가장 신뢰성 있음.
                            string sourcePath = AssetDatabase.GetAssetPath(source);
                            if (sourcePath != playerPrefabPath) continue;
                            // PrefabInstance 의 root 만 처리 (자식 transform 은 skip)
                            if (PrefabUtility.GetNearestPrefabInstanceRoot(go) != go) continue;

                            foundPlayerInstance = true;

                            if (go.GetComponent<EditorTestCharacterMarker>() != null)
                            {
                                alreadyHadMarker++;
                                continue;
                            }

                            // AddComponent — PrefabInstance 의 add-component override 로 저장됨
                            Undo.AddComponent<EditorTestCharacterMarker>(go);
                            sceneDirty = true;
                            markersAdded++;
                            Debug.Log($"[AddSceneCharacterMarker] {Path.GetFileName(scenePath)} '{go.name}' 에 EditorTestCharacterMarker 부착.");
                        }
                    }

                    if (sceneDirty)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        updatedScenePaths.Add(scenePath);
                    }

                    if (!foundPlayerInstance)
                    {
                        noPlayerScenePaths.Add(scenePath);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                // 원래 열려있던 씬 복원
                if (!string.IsNullOrEmpty(currentScenePath) && File.Exists(currentScenePath))
                {
                    EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
                }
            }

            string msg =
                $"검사 씬: {processedScenes}\n" +
                $"마커 신규 부착: {markersAdded}\n" +
                $"이미 부착돼 있음: {alreadyHadMarker}\n" +
                $"PlayerPrefab 없는 씬: {noPlayerScenePaths.Count}\n\n" +
                $"수정된 씬:\n{string.Join("\n", updatedScenePaths)}";
            Debug.Log("[AddSceneCharacterMarker] 완료.\n" + msg);
            EditorUtility.DisplayDialog("Add EditorTestCharacterMarker", msg, "OK");
        }
    }
}

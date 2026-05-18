using LostMemory.Talents;
using LostMemory.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Editor
{
    public static class TopRightHUDSceneInstaller
    {
        private const string TopRightHUDPrefabPath = "Assets/_Project/Prefabs/UI/TopRightHUD.prefab";
        private const string TownTopRightHUDPrefabPath = "Assets/_Project/Prefabs/UI/TownTopRightHUD.prefab";
        private const string TopRightHUDName = "TopRightHUD";
        private const string TownTopRightHUDName = "TownTopRightHUD";
        private const string CanvasName = "Canvas";
        private const string TownCanvasName = "TownUICanvas";
        private const string TownSceneName = "Town";
        private const string TownScenePath = "Assets/_Project/Scenes/Town/Town.unity";

        private static readonly string[] DungeonScenePaths =
        {
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_2R.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_3R.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_4R.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_Boss.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon.unity",
        };

        [MenuItem("LostMemory/UI/Install Dungeon TopRightHUD")]
        public static void InstallDungeonTopRightHUD()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            int installedCount = InstallAll();

            EditorUtility.DisplayDialog(
                "Install Dungeon TopRightHUD",
                $"TopRightHUD checked for {installedCount}/{DungeonScenePaths.Length} dungeon scenes.",
                "OK");
        }

        public static void InstallDungeonTopRightHUDBatch()
        {
            InstallAll();
            EditorApplication.Exit(0);
        }

        [MenuItem("LostMemory/UI/Install Town TopRightHUD")]
        public static void InstallTownTopRightHUD()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            bool installed = InstallTown();

            EditorUtility.DisplayDialog(
                "Install Town TopRightHUD",
                installed ? "TownTopRightHUD checked for Town scene." : "TownTopRightHUD was not installed.",
                "OK");
        }

        public static void InstallTownTopRightHUDBatch()
        {
            InstallTown();
            EditorApplication.Exit(0);
        }

        private static int InstallAll()
        {
            int installedCount = 0;
            for (int i = 0; i < DungeonScenePaths.Length; i++)
            {
                if (InstallInScene(DungeonScenePaths[i]))
                {
                    installedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return installedCount;
        }

        private static bool InstallInScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Canvas canvas = FindSceneCanvas(scene, CanvasName);
            if (canvas == null)
            {
                throw new System.InvalidOperationException($"Canvas was not found in scene: {scenePath}");
            }

            GameObject hudObject = FindChildRecursive(canvas.transform, TopRightHUDName)?.gameObject;
            if (hudObject == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TopRightHUDPrefabPath);
                if (prefab == null)
                {
                    throw new System.InvalidOperationException($"TopRightHUD prefab was not found: {TopRightHUDPrefabPath}");
                }

                hudObject = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (hudObject == null)
                {
                    throw new System.InvalidOperationException("Failed to instantiate TopRightHUD prefab.");
                }

                hudObject.name = TopRightHUDName;
                hudObject.transform.SetParent(canvas.transform, false);
            }

            ConfigureTopRightHUD(hudObject);
            hudObject.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[TopRightHUDSceneInstaller] TopRightHUD installed in {scenePath}.");
            return true;
        }

        private static bool InstallTown()
        {
            Scene scene = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
            Canvas canvas = FindSceneCanvas(scene, TownCanvasName);
            if (canvas == null)
            {
                throw new System.InvalidOperationException($"Canvas '{TownCanvasName}' was not found in scene: {TownScenePath}");
            }

            GameObject hudObject = FindChildRecursive(canvas.transform, TownTopRightHUDName)?.gameObject;
            if (hudObject == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TownTopRightHUDPrefabPath);
                if (prefab == null)
                {
                    throw new System.InvalidOperationException($"TownTopRightHUD prefab was not found: {TownTopRightHUDPrefabPath}");
                }

                hudObject = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (hudObject == null)
                {
                    throw new System.InvalidOperationException("Failed to instantiate TownTopRightHUD prefab.");
                }

                hudObject.name = TownTopRightHUDName;
                hudObject.transform.SetParent(canvas.transform, false);
            }

            ConfigureTownTopRightHUD(scene, hudObject);
            hudObject.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TownScenePath);
            Debug.Log($"[TopRightHUDSceneInstaller] TownTopRightHUD installed in {TownScenePath}.");
            return true;
        }

        private static Canvas FindSceneCanvas(Scene scene, string canvasName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                Canvas[] canvases = root.GetComponentsInChildren<Canvas>(includeInactive: true);
                for (int j = 0; j < canvases.Length; j++)
                {
                    Canvas canvas = canvases[j];
                    if (canvas != null && canvas.name == canvasName)
                    {
                        return canvas;
                    }
                }
            }

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                Canvas canvas = root.GetComponentInChildren<Canvas>(includeInactive: true);
                if (canvas != null)
                {
                    return canvas;
                }
            }

            return null;
        }

        private static void ConfigureTopRightHUD(GameObject hudObject)
        {
            RectTransform rectTransform = hudObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(1f, 1f);
                rectTransform.anchorMax = new Vector2(1f, 1f);
                rectTransform.pivot = new Vector2(1f, 1f);
                rectTransform.anchoredPosition = new Vector2(-30f, -20f);
                rectTransform.localScale = Vector3.one;
            }

            TopRightHUDView view = hudObject.GetComponent<TopRightHUDView>();
            if (view == null)
            {
                throw new System.InvalidOperationException("TopRightHUD prefab has no TopRightHUDView component.");
            }

            SerializedObject serializedView = new SerializedObject(view);
            SetObject(serializedView, "_pausePanel", hudObject.GetComponentInChildren<PausePanelView>(includeInactive: true));
            SetString(serializedView, "_lobbySceneName", TownSceneName);
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        private static void ConfigureTownTopRightHUD(Scene scene, GameObject hudObject)
        {
            RectTransform rectTransform = hudObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.localScale = Vector3.one;
            }

            TopRightHUDView dungeonView = hudObject.GetComponent<TopRightHUDView>();
            if (dungeonView != null)
            {
                dungeonView.enabled = false;
                EditorUtility.SetDirty(dungeonView);
            }

            TownTopRightHUDView townView = hudObject.GetComponent<TownTopRightHUDView>();
            if (townView == null)
            {
                throw new System.InvalidOperationException("TownTopRightHUD prefab has no TownTopRightHUDView component.");
            }

            SettingsPanelView settingsPanel = hudObject.GetComponentInChildren<SettingsPanelView>(includeInactive: true);
            TalentPanelView talentPanel = FindSceneComponent<TalentPanelView>(scene);

            SerializedObject serializedTownView = new SerializedObject(townView);
            SetObject(serializedTownView, "_settingsPanel", settingsPanel);
            SetObject(serializedTownView, "_talentPanel", talentPanel);
            serializedTownView.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(townView);

            if (settingsPanel != null)
            {
                settingsPanel.gameObject.SetActive(false);
                EditorUtility.SetDirty(settingsPanel.gameObject);
            }

            if (talentPanel != null)
            {
                SerializedObject serializedTalentPanel = new SerializedObject(talentPanel);
                SetBool(serializedTalentPanel, "_autoOpenOnStart", false);
                SetBool(serializedTalentPanel, "_handleEscKey", false);
                serializedTalentPanel.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(talentPanel);
            }
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                T component = root.GetComponentInChildren<T>(includeInactive: true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }
    }
}

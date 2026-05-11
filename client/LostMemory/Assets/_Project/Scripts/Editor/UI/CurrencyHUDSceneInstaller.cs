using LostMemory.Memory;
using LostMemory.Stage;
using LostMemory.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Editor
{
    public static class CurrencyHUDSceneInstaller
    {
        private const string CurrencyHUDPrefabPath = "Assets/_Project/Prefabs/UI/CurrencyHUD.prefab";
        private const string TownCurrencyHUDPrefabPath = "Assets/_Project/Prefabs/UI/TownCurrencyHUD.prefab";
        private const string CurrencyHUDName = "CurrencyHUD";
        private const string TownCurrencyHUDName = "TownCurrencyHUD";
        private const string TownScenePath = "Assets/_Project/Scenes/Town/Town.unity";
        private const string TownCanvasName = "TownUICanvas";
        private const string DungeonCanvasName = "Canvas";

        private static readonly string[] DungeonScenePaths =
        {
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_2R.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_3R.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_4R.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_Boss.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon.unity",
        };

        [MenuItem("LostMemory/UI/Install CurrencyHUDs")]
        public static void InstallCurrencyHUDs()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            int installedCount = InstallAll();

            EditorUtility.DisplayDialog(
                "Install CurrencyHUDs",
                $"Currency HUDs checked for {installedCount}/{DungeonScenePaths.Length + 1} scenes.",
                "OK");
        }

        public static void InstallCurrencyHUDsBatch()
        {
            InstallAll();
            EditorApplication.Exit(0);
        }

        private static int InstallAll()
        {
            int installedCount = 0;
            if (InstallTownHUD())
            {
                installedCount++;
            }

            for (int i = 0; i < DungeonScenePaths.Length; i++)
            {
                if (InstallDungeonHUD(DungeonScenePaths[i]))
                {
                    installedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return installedCount;
        }

        private static bool InstallTownHUD()
        {
            Scene scene = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
            Canvas canvas = FindSceneCanvas(scene, TownCanvasName);
            if (canvas == null)
            {
                throw new System.InvalidOperationException($"Canvas '{TownCanvasName}' was not found in scene: {TownScenePath}");
            }

            GameObject hudObject = FindOrCreateHUD(scene, canvas, TownCurrencyHUDPrefabPath, TownCurrencyHUDName);
            ConfigureRect(hudObject);
            ConfigureTownHUD(hudObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TownScenePath);
            Debug.Log($"[CurrencyHUDSceneInstaller] TownCurrencyHUD installed in {TownScenePath}.");
            return true;
        }

        private static bool InstallDungeonHUD(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Canvas canvas = FindSceneCanvas(scene, DungeonCanvasName);
            if (canvas == null)
            {
                throw new System.InvalidOperationException($"Canvas '{DungeonCanvasName}' was not found in scene: {scenePath}");
            }

            GoldWallet goldWallet = null;
            ConfigureDungeonEconomy(scene, out goldWallet);

            GameObject hudObject = FindOrCreateHUD(scene, canvas, CurrencyHUDPrefabPath, CurrencyHUDName);
            ConfigureRect(hudObject);
            ConfigureDungeonHUD(hudObject, goldWallet);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[CurrencyHUDSceneInstaller] CurrencyHUD installed in {scenePath}.");
            return true;
        }

        private static GameObject FindOrCreateHUD(Scene scene, Canvas canvas, string prefabPath, string hudName)
        {
            GameObject hudObject = FindChildRecursive(canvas.transform, hudName)?.gameObject;
            if (hudObject != null)
            {
                return hudObject;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException($"HUD prefab was not found: {prefabPath}");
            }

            hudObject = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (hudObject == null)
            {
                throw new System.InvalidOperationException($"Failed to instantiate HUD prefab: {prefabPath}");
            }

            hudObject.name = hudName;
            hudObject.transform.SetParent(canvas.transform, false);
            return hudObject;
        }

        private static void ConfigureRect(GameObject hudObject)
        {
            RectTransform rectTransform = hudObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(1f, 0f);
            rectTransform.anchoredPosition = new Vector2(-30f, 30f);
            rectTransform.localScale = Vector3.one;
        }

        private static void ConfigureTownHUD(GameObject hudObject)
        {
            CurrencyHUDView view = RequireView(hudObject);
            TownCurrencyHUDPresenter presenter = hudObject.GetComponent<TownCurrencyHUDPresenter>();
            if (presenter == null)
            {
                presenter = hudObject.AddComponent<TownCurrencyHUDPresenter>();
            }

            SerializedObject serializedPresenter = new SerializedObject(presenter);
            SetObject(serializedPresenter, "_view", view);
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(presenter);
        }

        private static void ConfigureDungeonHUD(GameObject hudObject, GoldWallet goldWallet)
        {
            CurrencyHUDView view = RequireView(hudObject);
            DungeonCurrencyHUDPresenter presenter = hudObject.GetComponent<DungeonCurrencyHUDPresenter>();
            if (presenter == null)
            {
                presenter = hudObject.AddComponent<DungeonCurrencyHUDPresenter>();
            }

            SerializedObject serializedPresenter = new SerializedObject(presenter);
            SetObject(serializedPresenter, "_view", view);
            SetObject(serializedPresenter, "_goldWallet", goldWallet);
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(presenter);
        }

        private static void ConfigureDungeonEconomy(Scene scene, out GoldWallet goldWallet)
        {
            RunManager runManager = FindSceneComponent<RunManager>(scene);
            if (runManager == null)
            {
                throw new System.InvalidOperationException($"RunManager was not found in scene: {scene.path}");
            }

            goldWallet = runManager.GetComponent<GoldWallet>();
            if (goldWallet == null)
            {
                goldWallet = runManager.gameObject.AddComponent<GoldWallet>();
            }

            MemoryProgressTracker memoryProgressTracker = runManager.GetComponent<MemoryProgressTracker>();
            if (memoryProgressTracker == null)
            {
                memoryProgressTracker = runManager.gameObject.AddComponent<MemoryProgressTracker>();
            }

            SerializedObject serializedRunManager = new SerializedObject(runManager);
            SetObject(serializedRunManager, "goldWallet", goldWallet);
            SetObject(serializedRunManager, "memoryProgressTracker", memoryProgressTracker);
            serializedRunManager.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(runManager);
            EditorUtility.SetDirty(goldWallet);
            EditorUtility.SetDirty(memoryProgressTracker);
        }

        private static CurrencyHUDView RequireView(GameObject hudObject)
        {
            CurrencyHUDView view = hudObject.GetComponent<CurrencyHUDView>();
            if (view == null)
            {
                throw new System.InvalidOperationException($"{hudObject.name} has no CurrencyHUDView component.");
            }

            return view;
        }

        private static Canvas FindSceneCanvas(Scene scene, string canvasName)
        {
            Canvas namedCanvas = FindSceneComponent<Canvas>(scene, canvas => canvas.name == canvasName);
            if (namedCanvas != null)
            {
                return namedCanvas;
            }

            return FindSceneComponent<Canvas>(scene);
        }

        private static T FindSceneComponent<T>(Scene scene, System.Predicate<T> predicate = null) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T[] components = roots[i].GetComponentsInChildren<T>(includeInactive: true);
                for (int j = 0; j < components.Length; j++)
                {
                    T component = components[j];
                    if (component != null && (predicate == null || predicate(component)))
                    {
                        return component;
                    }
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
    }
}

using LostMemory.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Editor
{
    public static class ShortcutBarSceneInstaller
    {
        private const string ShortcutBarPrefabPath = "Assets/_Project/Prefabs/UI/ShortcutBar.prefab";
        private const string ShortcutBarName = "ShortcutBar";
        private const string CanvasName = "Canvas";
        private const float LeftMargin = 10f;
        private const float BottomMargin = 30f;

        private static readonly string[] DungeonScenePaths =
        {
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_2R.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_3R.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_4R.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon_1F_Boss.unity",
            "Assets/_Project/Scenes/Dungeon/Dungeon.unity",
        };

        [MenuItem("LostMemory/UI/Install Dungeon ShortcutBar")]
        public static void InstallDungeonShortcutBar()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            int installedCount = InstallAll();

            EditorUtility.DisplayDialog(
                "Install Dungeon ShortcutBar",
                $"ShortcutBar checked for {installedCount}/{DungeonScenePaths.Length} dungeon scenes.",
                "OK");
        }

        public static void InstallDungeonShortcutBarBatch()
        {
            InstallAll();
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
            Canvas canvas = FindSceneCanvas(scene);
            if (canvas == null)
            {
                throw new System.InvalidOperationException($"Canvas '{CanvasName}' was not found in scene: {scenePath}");
            }

            GameObject shortcutBarObject = FindChildRecursive(canvas.transform, ShortcutBarName)?.gameObject;
            if (shortcutBarObject == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShortcutBarPrefabPath);
                if (prefab == null)
                {
                    throw new System.InvalidOperationException($"ShortcutBar prefab was not found: {ShortcutBarPrefabPath}");
                }

                shortcutBarObject = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (shortcutBarObject == null)
                {
                    throw new System.InvalidOperationException("Failed to instantiate ShortcutBar prefab.");
                }

                shortcutBarObject.name = ShortcutBarName;
                shortcutBarObject.transform.SetParent(canvas.transform, false);
            }

            ConfigureShortcutBar(shortcutBarObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[ShortcutBarSceneInstaller] ShortcutBar installed in {scenePath}.");
            return true;
        }

        private static void ConfigureShortcutBar(GameObject shortcutBarObject)
        {
            RectTransform rectTransform = shortcutBarObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.zero;
                rectTransform.pivot = Vector2.zero;
                rectTransform.anchoredPosition = new Vector2(LeftMargin, BottomMargin);
                rectTransform.localScale = Vector3.one;
            }

            ShortcutBarView view = shortcutBarObject.GetComponent<ShortcutBarView>();
            if (view == null)
            {
                throw new System.InvalidOperationException("ShortcutBar prefab has no ShortcutBarView component.");
            }

            ShortcutBarPresenter presenter = shortcutBarObject.GetComponent<ShortcutBarPresenter>();
            if (presenter == null)
            {
                presenter = shortcutBarObject.AddComponent<ShortcutBarPresenter>();
            }

            SerializedObject serializedPresenter = new SerializedObject(presenter);
            SetObject(serializedPresenter, "_view", view);
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(presenter);
        }

        private static Canvas FindSceneCanvas(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Canvas[] canvases = roots[i].GetComponentsInChildren<Canvas>(includeInactive: true);
                for (int j = 0; j < canvases.Length; j++)
                {
                    Canvas canvas = canvases[j];
                    if (canvas != null && canvas.name == CanvasName)
                    {
                        return canvas;
                    }
                }
            }

            for (int i = 0; i < roots.Length; i++)
            {
                Canvas canvas = roots[i].GetComponentInChildren<Canvas>(includeInactive: true);
                if (canvas != null)
                {
                    return canvas;
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

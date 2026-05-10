using LostMemory.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Editor
{
    public static class PlayerHUDSceneInstaller
    {
        private const string PlayerHUDPrefabPath = "Assets/_Project/Prefabs/UI/PlayerHUD.prefab";
        private const string PlayerHUDName = "PlayerHUD";

        private static readonly SceneInstallTarget[] SceneTargets =
        {
            new SceneInstallTarget("Assets/_Project/Scenes/Town/Town.unity", "TownUICanvas"),
            new SceneInstallTarget("Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity", "Canvas"),
            new SceneInstallTarget("Assets/_Project/Scenes/Dungeon/Dungeon_1F_2R.unity", "Canvas"),
            new SceneInstallTarget("Assets/_Project/Scenes/Dungeon/Dungeon_1F_Boss.unity", "Canvas"),
            new SceneInstallTarget("Assets/_Project/Scenes/Dungeon/Dungeon.unity", "Canvas"),
        };

        [MenuItem("LostMemory/UI/Install PlayerHUD")]
        public static void InstallPlayerHUD()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            int installedCount = InstallAll();

            EditorUtility.DisplayDialog(
                "Install PlayerHUD",
                $"PlayerHUD checked for {installedCount}/{SceneTargets.Length} scenes.",
                "OK");
        }

        public static void InstallPlayerHUDBatch()
        {
            InstallAll();
            EditorApplication.Exit(0);
        }

        private static int InstallAll()
        {
            int installedCount = 0;
            for (int i = 0; i < SceneTargets.Length; i++)
            {
                if (InstallInScene(SceneTargets[i]))
                {
                    installedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return installedCount;
        }

        private static bool InstallInScene(SceneInstallTarget target)
        {
            Scene scene = EditorSceneManager.OpenScene(target.ScenePath, OpenSceneMode.Single);
            Canvas canvas = FindSceneCanvas(scene, target.CanvasName);
            if (canvas == null)
            {
                throw new System.InvalidOperationException($"Canvas '{target.CanvasName}' was not found in scene: {target.ScenePath}");
            }

            GameObject hudObject = FindChildRecursive(canvas.transform, PlayerHUDName)?.gameObject;
            if (hudObject == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHUDPrefabPath);
                if (prefab == null)
                {
                    throw new System.InvalidOperationException($"PlayerHUD prefab was not found: {PlayerHUDPrefabPath}");
                }

                hudObject = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (hudObject == null)
                {
                    throw new System.InvalidOperationException("Failed to instantiate PlayerHUD prefab.");
                }

                hudObject.name = PlayerHUDName;
                hudObject.transform.SetParent(canvas.transform, false);
            }

            ConfigurePlayerHUD(hudObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, target.ScenePath);
            Debug.Log($"[PlayerHUDSceneInstaller] PlayerHUD installed in {target.ScenePath}.");
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

        private static void ConfigurePlayerHUD(GameObject hudObject)
        {
            RectTransform rectTransform = hudObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0f, 1f);
                rectTransform.anchorMax = new Vector2(0f, 1f);
                rectTransform.pivot = new Vector2(0f, 1f);
                rectTransform.anchoredPosition = new Vector2(30f, -20f);
                rectTransform.localScale = Vector3.one;
            }

            HealthBarView view = hudObject.GetComponent<HealthBarView>();
            if (view == null)
            {
                throw new System.InvalidOperationException("PlayerHUD prefab has no HealthBarView component.");
            }

            PlayerHUDPresenter presenter = hudObject.GetComponent<PlayerHUDPresenter>();
            if (presenter == null)
            {
                presenter = hudObject.AddComponent<PlayerHUDPresenter>();
            }

            SerializedObject serializedPresenter = new SerializedObject(presenter);
            SetObject(serializedPresenter, "_healthBarView", view);
            SetBool(serializedPresenter, "_autoResolveLocalPlayer", true);
            SetBool(serializedPresenter, "_hideMPUntilManaSourceExists", true);
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(presenter);
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

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private readonly struct SceneInstallTarget
        {
            public SceneInstallTarget(string scenePath, string canvasName)
            {
                ScenePath = scenePath;
                CanvasName = canvasName;
            }

            public string ScenePath { get; }
            public string CanvasName { get; }
        }
    }
}

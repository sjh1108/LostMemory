using System.IO;
using LostMemory.Talents;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostMemory.Editor
{
    [InitializeOnLoad]
    public static class TownTalentSceneInstaller
    {
        private const string TownScenePath = "Assets/_Project/Scenes/Town/Town.unity";
        private const string TalentPanelPrefabPath = "Assets/_Project/Prefabs/UI/TalentPanel.prefab";
        private const string TownRootName = "TownRoot";
        private const string CanvasName = "TownUICanvas";
        private const string EventSystemName = "EventSystem";
        private const string TalentPanelName = "TalentPanel";
        private const string TalentNpcName = "NpcHouse_Placeholder";
        private const string TalentTriggerName = "TalentNpcTrigger";
        private const string TalentPromptName = "TalentInteractPrompt";
        private const string RequestFileName = "TownTalentSceneInstaller.request";
        private const float TalentTriggerRadius = 1.6f;

        static TownTalentSceneInstaller()
        {
            SetupTownTalentInteractionWhenRequested();
        }

        [InitializeOnLoadMethod]
        private static void SetupTownTalentInteractionWhenRequested()
        {
            if (!File.Exists(RequestFilePath))
            {
                return;
            }

            Debug.Log($"[TownTalentSceneInstaller] Setup request detected: {RequestFilePath}");
            EditorApplication.delayCall += TryRunRequestedSetup;
        }

        [MenuItem("Lost Memory/Scenes/Setup Town Talent Interaction")]
        public static void SetupTownTalentInteractionMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            SetupTownTalentInteraction();
            EditorUtility.DisplayDialog(
                "Setup Town Talent Interaction",
                "Town talent interaction was applied to:\n" + TownScenePath,
                "OK");
        }

        public static void SetupTownTalentInteractionBatch()
        {
            SetupTownTalentInteraction();
            EditorApplication.Exit(0);
        }

        private static void SetupTownTalentInteraction()
        {
            Scene scene = GetOrOpenTownScene();
            GameObject root = FindSceneObject(scene, TownRootName);
            if (root == null)
            {
                root = new GameObject(TownRootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            Canvas canvas = EnsureCanvas(scene, root.transform);
            EnsureEventSystem(scene, root.transform);
            TalentPanelView talentPanel = EnsureTalentPanel(scene, canvas.transform);
            EnsureTalentNpcTrigger(scene, talentPanel);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TownScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TownTalentSceneInstaller] Town talent interaction applied to {TownScenePath}.");
        }

        private static string RequestFilePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", RequestFileName));

        private static Scene GetOrOpenTownScene()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.path == TownScenePath)
            {
                return activeScene;
            }

            return EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
        }

        private static void TryRunRequestedSetup()
        {
            if (!File.Exists(RequestFilePath))
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[TownTalentSceneInstaller] Setup request skipped because the editor is entering or running Play Mode.");
                return;
            }

            File.Delete(RequestFilePath);
            Debug.Log("[TownTalentSceneInstaller] Running requested Town talent setup.");
            SetupTownTalentInteraction();
        }

        private static Canvas EnsureCanvas(Scene scene, Transform parent)
        {
            GameObject canvasObject = FindSceneObject(scene, CanvasName);
            if (canvasObject == null)
            {
                canvasObject = new GameObject(
                    CanvasName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                canvasObject.transform.SetParent(parent, false);
            }

            Canvas canvas = EnsureComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = EnsureComponent<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureComponent<GraphicRaycaster>(canvasObject);
            StretchToParent(canvasObject.GetComponent<RectTransform>());
            return canvas;
        }

        private static void EnsureEventSystem(Scene scene, Transform parent)
        {
            GameObject eventSystemObject = FindSceneObject(scene, EventSystemName);
            if (eventSystemObject == null)
            {
                eventSystemObject = new GameObject(EventSystemName);
                SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
                eventSystemObject.transform.SetParent(parent, false);
            }

            EnsureComponent<EventSystem>(eventSystemObject);
            EnsureComponent<StandaloneInputModule>(eventSystemObject);
        }

        private static TalentPanelView EnsureTalentPanel(Scene scene, Transform canvasTransform)
        {
            Transform existing = FindDirectChild(canvasTransform, TalentPanelName);
            GameObject panelObject = existing != null ? existing.gameObject : null;

            if (panelObject == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TalentPanelPrefabPath);
                if (prefab == null)
                {
                    throw new System.InvalidOperationException(
                        $"TalentPanel prefab was not found: {TalentPanelPrefabPath}");
                }

                panelObject = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (panelObject == null)
                {
                    throw new System.InvalidOperationException("Failed to instantiate TalentPanel prefab.");
                }

                panelObject.name = TalentPanelName;
                panelObject.transform.SetParent(canvasTransform, false);
            }

            panelObject.SetActive(true);
            RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                CenterRect(rectTransform, new Vector2(800f, 600f));
            }

            TalentPanelView talentPanel = panelObject.GetComponent<TalentPanelView>();
            if (talentPanel == null)
            {
                throw new System.InvalidOperationException("TalentPanel prefab has no TalentPanelView component.");
            }

            SerializedObject serializedPanel = new SerializedObject(talentPanel);
            SetBool(serializedPanel, "_autoOpenOnStart", false);
            SetInt(serializedPanel, "_debugTotalPoints", 10);
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(talentPanel);
            return talentPanel;
        }

        private static void EnsureTalentNpcTrigger(Scene scene, TalentPanelView talentPanel)
        {
            GameObject npcObject = FindSceneObject(scene, TalentNpcName);
            if (npcObject == null)
            {
                throw new System.InvalidOperationException(
                    $"Town talent NPC target was not found: {TalentNpcName}");
            }

            GameObject triggerObject = EnsureChild(scene, npcObject.transform, TalentTriggerName);
            triggerObject.transform.localPosition = Vector3.zero;

            CircleCollider2D trigger = EnsureComponent<CircleCollider2D>(triggerObject);
            trigger.isTrigger = true;
            trigger.radius = TalentTriggerRadius;

            DeletePromptIfPresent(triggerObject.transform);
            TalentNpcInteractable interactable = EnsureComponent<TalentNpcInteractable>(triggerObject);

            SerializedObject serializedInteractable = new SerializedObject(interactable);
            SetObject(serializedInteractable, "_talentPanel", talentPanel);
            SetInt(serializedInteractable, "_interactKey", (int)KeyCode.F);
            SetBool(serializedInteractable, "_requirePlayerInRange", false);
            SetBool(serializedInteractable, "_togglePanelOnInteract", true);
            SetString(serializedInteractable, "_playerTag", "Player");
            SetObject(serializedInteractable, "_promptObject", null);
            SetBool(serializedInteractable, "_logInteraction", true);
            serializedInteractable.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(interactable);
        }

        private static void DeletePromptIfPresent(Transform parent)
        {
            Transform prompt = FindDirectChild(parent, TalentPromptName);
            if (prompt == null)
            {
                return;
            }

            Object.DestroyImmediate(prompt.gameObject);
        }

        private static GameObject EnsureChild(Scene scene, Transform parent, string childName)
        {
            Transform existing = FindDirectChild(parent, childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(childName);
            SceneManager.MoveGameObjectToScene(child, scene);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    if (transforms[j].name == objectName)
                    {
                        return transforms[j].gameObject;
                    }
                }
            }

            return null;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }

        private static void CenterRect(RectTransform rectTransform, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = size;
            rectTransform.localScale = Vector3.one;
        }

        private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
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

        private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
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
    }
}

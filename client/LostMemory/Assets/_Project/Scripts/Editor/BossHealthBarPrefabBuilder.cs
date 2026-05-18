using System.IO;
using LostMemory.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Editor.UI
{
    public static class BossHealthBarPrefabBuilder
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/BossHealthBar.prefab";
        private const string RequestPath = "Assets/_Project/Prefabs/UI/BossHealthBar.prefab.create";
        private const string PortraitPath = "Assets/_Project/Art/Enemies/Boss/1_Bertha/Portraits/BerthaPortrait.png";
        private const string HpBarFolderPath = "Assets/_Project/Art/Enemies/Boss/HpBar";

        [InitializeOnLoadMethod]
        private static void CreateWhenRequested()
        {
            if (!File.Exists(ToAbsoluteAssetPath(RequestPath)))
            {
                return;
            }

            AssetDatabase.DeleteAsset(RequestPath);
            CreateBossHealthBarPrefab();
        }

        [MenuItem("LostMemory/UI/Create Boss Health Bar Prefab")]
        public static void CreateBossHealthBarPrefab()
        {
            ConfigureBossHealthBarSpriteImports();

            GameObject canvasObject = new GameObject(
                "BossHealthBarCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            StretchToParent(canvasRect);

            GameObject rootObject = new GameObject(
                "BossHealthBarRoot",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(BossHealthBarView));
            rootObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -24f);
            rootRect.sizeDelta = new Vector2(190f, 110f);

            CanvasGroup canvasGroup = rootObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Sprite portraitSprite = LoadSprite(PortraitPath);
            Sprite frameSprite = LoadSprite($"{HpBarFolderPath}/Frame.png");
            Sprite[] hpSprites = LoadHpSprites();

            Image portraitImage = CreateImage(
                "PortraitImage",
                rootObject.transform,
                portraitSprite,
                new Vector2(-56f, -44f),
                new Vector2(64f, 64f));

            Image hpFillImage = CreateImage(
                "HpFillImage",
                rootObject.transform,
                hpSprites[hpSprites.Length - 1],
                new Vector2(32f, -42f),
                new Vector2(100f, 100f));

            Image hpFrameImage = CreateImage(
                "HpFrameImage",
                rootObject.transform,
                frameSprite,
                new Vector2(32f, -42f),
                new Vector2(100f, 100f));

            BossHealthBarView view = rootObject.GetComponent<BossHealthBarView>();
            SerializedObject serializedView = new SerializedObject(view);
            SetObject(serializedView, "canvasGroup", canvasGroup);
            SetObject(serializedView, "portraitImage", portraitImage);
            SetObject(serializedView, "hpFillImage", hpFillImage);
            SetObject(serializedView, "hpFrameImage", hpFrameImage);
            SetObject(serializedView, "portraitSprite", portraitSprite);
            SetObject(serializedView, "hpFrameSprite", frameSprite);
            SetSpriteArray(serializedView, "hpFillSprites", hpSprites);
            serializedView.FindProperty("displayMode").enumValueIndex = (int)BossHealthBarDisplayMode.OnIntroStarted;
            serializedView.FindProperty("hideOnAwake").boolValue = true;
            serializedView.FindProperty("showWhenDamaged").boolValue = true;
            serializedView.FindProperty("hideOnDeath").boolValue = true;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
            Object.DestroyImmediate(canvasObject);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created boss health bar prefab at {PrefabPath}.");
        }

        public static void CreateBossHealthBarPrefabBatch()
        {
            CreateBossHealthBarPrefab();
            EditorApplication.Exit(0);
        }

        [MenuItem("LostMemory/UI/Configure Boss Health Bar Sprites")]
        public static void ConfigureBossHealthBarSpriteImports()
        {
            ConfigureSpriteImport(PortraitPath, 48f);
            ConfigureSpriteImport("Assets/_Project/Art/Enemies/Boss/1_Bertha/Portraits/Bertha.png", 48f);
            ConfigureSpriteImport("Assets/_Project/Art/Enemies/Boss/1_Bertha/Portraits/BerthaPortrait2.png", 48f);
            ConfigureSpriteImport($"{HpBarFolderPath}/Frame.png", 100f);

            for (int i = 0; i <= 100; i += 2)
            {
                ConfigureSpriteImport($"{HpBarFolderPath}/{i}.png", 100f);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite[] LoadHpSprites()
        {
            Sprite[] hpSprites = new Sprite[51];
            for (int i = 0; i < hpSprites.Length; i++)
            {
                int percent = i * 2;
                hpSprites[i] = LoadSprite($"{HpBarFolderPath}/{percent}.png");
            }

            return hpSprites;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"Sprite not found: {path}");
            }

            return sprite;
        }

        private static void ConfigureSpriteImport(string path, float pixelsPerUnit)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"Texture importer not found: {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetSpriteArray(
            SerializedObject serializedObject,
            string propertyName,
            Sprite[] sprites)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectPath = Directory.GetParent(Application.dataPath)?.FullName;
            return projectPath == null ? assetPath : Path.Combine(projectPath, assetPath);
        }
    }
}

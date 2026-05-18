using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LostMemory.Intro.Phase0;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostMemoryEditor.Intro
{
    /// <summary>
    /// 메뉴: Lost Memory > Intro > Setup Phase0 Intro Scene
    /// </summary>
    public static class Phase0IntroSetupMenu
    {
        private const string DataAssetPath = "Assets/_Project/Data/Intro/Phase0IntroSequenceData_SCN01.asset";
        private const string DataFolderPath = "Assets/_Project/Data/Intro";
        private const string FontAssetPath = "Assets/_Project/Art/Fonts/malgun SDF.asset";
        private const string IntroSpritesFolder = "Assets/_Project/Art/intro";
        private const string IntroSpritePrefix = "intro1_";
        private const int IntroSpriteCount = 8;
        private const string TargetSceneName = "Phase0_Intro";

        private const string RootGoName = "Phase0IntroRoot";
        private const string CanvasGoName = "Phase0_Canvas";
        private const string BackgroundGoName = "BlackBackground";
        private const string BackdropGoName = "IntroBackdrop";
        private const string NarrationGoName = "NarrationText";
        private const string SfxSourceGoName = "SfxSource";

        [MenuItem("Lost Memory/Intro/Setup Phase0 Intro Scene")]
        public static void RunSetup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != TargetSceneName)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Phase0 Intro Setup",
                    $"현재 씬이 '{TargetSceneName}'이 아닙니다 (현재: '{scene.name}').\n그래도 진행할까요?",
                    "진행",
                    "취소");
                if (!proceed) return;
            }

            Sprite[] introSprites = PrepareIntroSprites();
            Phase0IntroSequenceData data = LoadOrCreateData(introSprites);

            if (data != null && (data.BackdropFrames == null || data.BackdropFrames.Length == 0) && introSprites.Length > 0)
            {
                SetPrivateField(data, "backdropFrames", introSprites);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Phase0Setup] 기존 SO에 backdropFrames 비어있어 자동 채움 ({introSprites.Length}장).");
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                Debug.LogWarning($"[Phase0Setup] 폰트 로드 실패: {FontAssetPath}. 기본 폰트 사용.");
            }

            RemoveIfExists(CanvasGoName);
            RemoveIfExists(RootGoName);

            EnsureBlackMainCamera();

            GameObject canvasGo = CreateCanvas();
            CreateBlackBackground(canvasGo);
            Phase0NarrationBackdrop backdrop = CreateBackdrop(canvasGo);
            TMP_Text label = CreateNarrationText(canvasGo, font);

            GameObject rootGo = new GameObject(RootGoName);
            AudioSource bgmSource = rootGo.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f;
            bgmSource.volume = 0f;

            GameObject sfxGo = new GameObject(SfxSourceGoName);
            sfxGo.transform.SetParent(rootGo.transform, false);
            AudioSource sfxSource = sfxGo.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            Phase0NarrationTypewriter typewriter = rootGo.AddComponent<Phase0NarrationTypewriter>();
            SetPrivateField(typewriter, "label", label);
            SetPrivateField(typewriter, "sfxSource", sfxSource);
            EditorUtility.SetDirty(typewriter);

            Phase0IntroSequenceController controller = rootGo.AddComponent<Phase0IntroSequenceController>();
            SetPrivateField(controller, "sequenceData", data);
            SetPrivateField(controller, "typewriter", typewriter);
            SetPrivateField(controller, "backdrop", backdrop);
            SetPrivateField(controller, "bgmSource", bgmSource);
            SetPrivateField(controller, "autoStartOnEnable", true);
            SetPrivateField(controller, "debugLogging", true);
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string spriteSummary = introSprites.Length > 0
                ? $"\n✓ 인트로 이미지 {introSprites.Length}장 자동 import + SO 연결 완료."
                : "\n⚠ 인트로 이미지 0장 (Assets/_Project/Art/intro/intro1_1~8.png 확인 필요).";

            string summary = (saved ? "씬 저장 완료." : "셋업은 됐는데 씬 저장 실패 — Ctrl+S 해주세요.")
                + spriteSummary
                + "\n\n다음에 할 일:\n1) SO Inspector에서 BGM Clip / Type Clip 드래그\n2) Play로 검증";
            EditorUtility.DisplayDialog("Phase0 Intro Setup", summary, "OK");

            Debug.Log("[Phase0Setup] 셋업 완료.");
            Selection.activeGameObject = rootGo;
        }

        [MenuItem("Lost Memory/Intro/Refresh Backdrop Sprites")]
        public static void RefreshBackdrop()
        {
            Sprite[] sprites = PrepareIntroSprites();
            Phase0IntroSequenceData data = AssetDatabase.LoadAssetAtPath<Phase0IntroSequenceData>(DataAssetPath);
            if (data == null)
            {
                EditorUtility.DisplayDialog("Refresh Backdrop", $"SO를 먼저 생성해주세요: {DataAssetPath}", "OK");
                return;
            }
            SetPrivateField(data, "backdropFrames", sprites);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase0Setup] backdropFrames 강제 갱신: {sprites.Length}장.");
            EditorUtility.DisplayDialog("Refresh Backdrop", $"backdropFrames {sprites.Length}장으로 갱신 완료.", "OK");
        }

        private static Sprite[] PrepareIntroSprites()
        {
            List<Sprite> sprites = new List<Sprite>(IntroSpriteCount);
            for (int i = 1; i <= IntroSpriteCount; i++)
            {
                string path = $"{IntroSpritesFolder}/{IntroSpritePrefix}{i}.png";
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[Phase0Setup] 인트로 이미지 없음: {path}");
                    continue;
                }

                ConvertToSprite(path);
                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp == null)
                {
                    Debug.LogWarning($"[Phase0Setup] Sprite 로드 실패: {path}");
                    continue;
                }
                sprites.Add(sp);
            }
            return sprites.ToArray();
        }

        private static void ConvertToSprite(string assetPath)
        {
            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) return;

            bool dirty = false;
            if (ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                dirty = true;
            }
            if (ti.spriteImportMode != SpriteImportMode.Single)
            {
                ti.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }
            if (!ti.alphaIsTransparency)
            {
                ti.alphaIsTransparency = true;
                dirty = true;
            }
            if (dirty)
            {
                ti.SaveAndReimport();
            }
        }

        private static Phase0IntroSequenceData LoadOrCreateData(Sprite[] introSprites)
        {
            if (!Directory.Exists(DataFolderPath))
            {
                Directory.CreateDirectory(DataFolderPath);
                AssetDatabase.Refresh();
            }

            Phase0IntroSequenceData existing = AssetDatabase.LoadAssetAtPath<Phase0IntroSequenceData>(DataAssetPath);
            if (existing != null)
            {
                Debug.Log($"[Phase0Setup] 기존 SO 재사용: {DataAssetPath}");
                return existing;
            }

            Phase0IntroSequenceData data = ScriptableObject.CreateInstance<Phase0IntroSequenceData>();
            SetPrivateField(data, "initialBlackHold", 2f);
            SetPrivateField(data, "defaultCharsPerSecond", 18f);
            SetPrivateField(data, "lines", new[]
            {
                new Phase0IntroLine { text = "어느 날 마물이 마을을 침략해왔다.", delayAfter = 0.4f },
                new Phase0IntroLine { text = "나는 마을을 지키기 위해 싸웠다. 싸우고, 또 싸웠다.", delayAfter = 1.0f },
                new Phase0IntroLine { text = "죽고, 다시 살아났다.", delayAfter = 0f },
                new Phase0IntroLine { text = "죽고, 다시 살아났다.", instantReveal = true, delayAfter = 1.5f },
            });
            SetPrivateField(data, "bgmFadeInDuration", 2.5f);
            SetPrivateField(data, "bgmTargetVolume", 0.15f);
            SetPrivateField(data, "bgmStartDelay", 0f);
            SetPrivateField(data, "typeVolume", 0.35f);
            SetPrivateField(data, "typePitchJitter", 0.05f);
            SetPrivateField(data, "playEveryNCharacters", 2);
            SetPrivateField(data, "backdropFrames", introSprites);
            SetPrivateField(data, "backdropFps", 6f);
            SetPrivateField(data, "backdropFadeInDuration", 1.0f);
            SetPrivateField(data, "backdropFadeInDelay", 0f);
            SetPrivateField(data, "sceneVisualId", string.Empty);

            AssetDatabase.CreateAsset(data, DataAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase0Setup] 신규 SO 생성: {DataAssetPath} (backdrop {introSprites.Length}장 포함)");
            return data;
        }

        private static GameObject CreateCanvas()
        {
            GameObject canvasGo = new GameObject(CanvasGoName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvasGo;
        }

        private static void CreateBlackBackground(GameObject canvasGo)
        {
            GameObject bgGo = new GameObject(BackgroundGoName, typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rt = bgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = bgGo.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
        }

        private static Phase0NarrationBackdrop CreateBackdrop(GameObject canvasGo)
        {
            GameObject backdropGo = new GameObject(BackdropGoName,
                typeof(Image), typeof(CanvasGroup));
            backdropGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rt = backdropGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = backdropGo.GetComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
            img.preserveAspect = true;

            CanvasGroup cg = backdropGo.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            Phase0NarrationBackdrop backdrop = backdropGo.AddComponent<Phase0NarrationBackdrop>();
            SetPrivateField(backdrop, "image", img);
            SetPrivateField(backdrop, "canvasGroup", cg);
            EditorUtility.SetDirty(backdrop);
            return backdrop;
        }

        private static TMP_Text CreateNarrationText(GameObject canvasGo, TMP_FontAsset font)
        {
            GameObject txtGo = new GameObject(NarrationGoName, typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rt = txtGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1400, 600);
            rt.anchoredPosition = Vector2.zero;

            TextMeshProUGUI text = txtGo.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = 36;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.text = string.Empty;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            return text;
        }

        private static void EnsureBlackMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            EditorUtility.SetDirty(cam);
        }

        private static void RemoveIfExists(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"[Phase0Setup] private field '{fieldName}' 미발견 — {target.GetType().Name}");
                return;
            }
            field.SetValue(target, value);
        }
    }
}

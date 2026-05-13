using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// Normalizes screen-space canvases in builds without touching scene or prefab YAML.
    /// </summary>
    public sealed class CanvasScalerRuntimeNormalizer : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float MatchWidthOrHeight = 0.5f;
        private const float ScanInterval = 0.25f;

        private static readonly Vector2 ReferenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);

        private static CanvasScalerRuntimeNormalizer instance;
        private float nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            EnsureInstance();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            NormalizeLoadedCanvases();
        }

        private static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            GameObject normalizerObject = new GameObject("[Canvas Scaler Runtime Normalizer]");
            normalizerObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(normalizerObject);
            instance = normalizerObject.AddComponent<CanvasScalerRuntimeNormalizer>();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            NormalizeLoadedCanvases();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanTime)
            {
                return;
            }

            nextScanTime = Time.unscaledTime + ScanInterval;
            NormalizeLoadedCanvases();
        }

        private static void NormalizeLoadedCanvases()
        {
            CanvasScaler[] scalers = Object.FindObjectsByType<CanvasScaler>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < scalers.Length; i++)
            {
                NormalizeCanvasScaler(scalers[i]);
            }
        }

        private static void NormalizeCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null || IsWorldSpaceCanvas(scaler))
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;
        }

        private static bool IsWorldSpaceCanvas(CanvasScaler scaler)
        {
            Canvas canvas = scaler.GetComponent<Canvas>();
            return canvas != null && canvas.renderMode == RenderMode.WorldSpace;
        }
    }
}

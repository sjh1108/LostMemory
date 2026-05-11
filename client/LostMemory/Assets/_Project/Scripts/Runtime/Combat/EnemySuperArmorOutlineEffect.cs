using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Enemy Super Armor Outline Effect")]
    public sealed class EnemySuperArmorOutlineEffect : MonoBehaviour
    {
        private const string RuntimeRootName = "SuperArmorOutline_Runtime";

        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(4)] private int outlineRendererCount = 14;
        [SerializeField, Min(0f)] private float outlineOffset = 0.04f;
        [SerializeField, Min(0f)] private float flowSpeed = 6f;
        [SerializeField, Range(0f, 1f)] private float shimmerStrength = 0.35f;
        [SerializeField] private int sortingOrderOffset = -1;
        [SerializeField] private Color redOutlineColor = new Color(1f, 0.05f, 0.02f, 0.72f);
        [SerializeField] private Color yellowOutlineColor = new Color(1f, 0.82f, 0.05f, 0.92f);
        [SerializeField] private bool activeOnEnable;
        [SerializeField] private bool hideSourceOutlineWhileVisible = true;

        private readonly List<SpriteRenderer> outlineRenderers = new List<SpriteRenderer>();
        private GameObject runtimeRoot;
        private Material spriteDefaultMaterial;
        private Material originalSourceMaterial;
        private bool visible;
        private bool sourceMaterialOverridden;

        private void Reset()
        {
            ResolveSourceRenderer();
        }

        private void Awake()
        {
            ResolveSourceRenderer();
            SetVisible(activeOnEnable);
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void OnDestroy()
        {
            DestroyRuntimeRoot();
        }

        private void LateUpdate()
        {
            if (!visible)
            {
                return;
            }

            if (!EnsureRenderers())
            {
                return;
            }

            SyncRenderers();
        }

        public void SetVisible(bool isVisible)
        {
            visible = isVisible;

            if (!visible)
            {
                RestoreSourceMaterial();
                SetRuntimeRootActive(false);
                return;
            }

            if (!EnsureRenderers())
            {
                return;
            }

            SetRuntimeRootActive(true);
            HideSourceOutline();
            SyncRenderers();
        }

        private bool EnsureRenderers()
        {
            if (!ResolveSourceRenderer() || sourceRenderer.sprite == null)
            {
                SetRuntimeRootActive(false);
                return false;
            }

            if (runtimeRoot == null)
            {
                runtimeRoot = new GameObject(RuntimeRootName)
                {
                    hideFlags = HideFlags.DontSave,
                };
            }

            if (runtimeRoot.transform.parent != sourceRenderer.transform)
            {
                runtimeRoot.transform.SetParent(sourceRenderer.transform, false);
            }

            runtimeRoot.transform.localPosition = Vector3.zero;
            runtimeRoot.transform.localRotation = Quaternion.identity;
            runtimeRoot.transform.localScale = Vector3.one;

            int requiredRendererCount = Mathf.Max(4, outlineRendererCount);

            while (outlineRenderers.Count < requiredRendererCount)
            {
                outlineRenderers.Add(CreateOutlineRenderer(outlineRenderers.Count));
            }

            while (outlineRenderers.Count > requiredRendererCount)
            {
                int lastIndex = outlineRenderers.Count - 1;
                SpriteRenderer rendererToRemove = outlineRenderers[lastIndex];
                outlineRenderers.RemoveAt(lastIndex);
                DestroyRuntimeObject(rendererToRemove != null ? rendererToRemove.gameObject : null);
            }

            return outlineRenderers.Count > 0;
        }

        private SpriteRenderer CreateOutlineRenderer(int index)
        {
            GameObject rendererObject = new GameObject($"SuperArmorOutline_{index:00}")
            {
                hideFlags = HideFlags.DontSave,
            };
            rendererObject.transform.SetParent(runtimeRoot.transform, false);

            SpriteRenderer renderer = rendererObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = ResolveSpriteDefaultMaterial();
            return renderer;
        }

        private void SyncRenderers()
        {
            int rendererCount = outlineRenderers.Count;
            if (sourceRenderer == null || rendererCount == 0)
            {
                return;
            }

            float elapsed = Time.time * flowSpeed;

            for (int i = 0; i < rendererCount; i++)
            {
                SpriteRenderer outlineRenderer = outlineRenderers[i];
                if (outlineRenderer == null)
                {
                    continue;
                }

                float normalizedIndex = i / (float)rendererCount;
                float angle = normalizedIndex * Mathf.PI * 2f + elapsed * 0.18f;
                float wave = Mathf.Sin(elapsed + i * 0.73f);
                float sparkle = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(elapsed * 1.7f + i * 2.11f)), 4f);
                float offset = outlineOffset * (1f + wave * 0.18f + sparkle * shimmerStrength);
                float colorMix = Mathf.PingPong(elapsed * 0.22f + normalizedIndex * 1.35f, 1f);
                Color outlineColor = Color.Lerp(redOutlineColor, yellowOutlineColor, colorMix);
                outlineColor.a *= 0.7f + Mathf.Clamp01(wave * 0.5f + 0.5f) * 0.3f + sparkle * 0.25f;

                outlineRenderer.sprite = sourceRenderer.sprite;
                outlineRenderer.flipX = sourceRenderer.flipX;
                outlineRenderer.flipY = sourceRenderer.flipY;
                outlineRenderer.sharedMaterial = ResolveSpriteDefaultMaterial();
                outlineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                outlineRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
                outlineRenderer.color = outlineColor;
                outlineRenderer.enabled = sourceRenderer.enabled && sourceRenderer.sprite != null;
                outlineRenderer.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * offset;
                outlineRenderer.transform.localRotation = Quaternion.identity;
                outlineRenderer.transform.localScale = Vector3.one;
            }
        }

        private bool ResolveSourceRenderer()
        {
            if (IsValidSourceRenderer(sourceRenderer))
            {
                return true;
            }

            sourceRenderer = visualRoot != null
                ? FindSourceRenderer(visualRoot)
                : FindSourceRenderer(transform);

            return sourceRenderer != null;
        }

        private void HideSourceOutline()
        {
            if (!hideSourceOutlineWhileVisible || sourceRenderer == null || sourceMaterialOverridden)
            {
                return;
            }

            Material noOutlineMaterial = ResolveSpriteDefaultMaterial();
            if (noOutlineMaterial == null)
            {
                return;
            }

            originalSourceMaterial = sourceRenderer.sharedMaterial;
            sourceRenderer.sharedMaterial = noOutlineMaterial;
            sourceMaterialOverridden = true;
        }

        private void RestoreSourceMaterial()
        {
            if (!sourceMaterialOverridden)
            {
                return;
            }

            if (sourceRenderer != null && sourceRenderer.sharedMaterial == spriteDefaultMaterial)
            {
                sourceRenderer.sharedMaterial = originalSourceMaterial;
            }

            originalSourceMaterial = null;
            sourceMaterialOverridden = false;
        }

        private Material ResolveSpriteDefaultMaterial()
        {
            if (spriteDefaultMaterial != null)
            {
                return spriteDefaultMaterial;
            }

            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader == null)
            {
                return sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
            }

            spriteDefaultMaterial = new Material(spriteShader)
            {
                hideFlags = HideFlags.DontSave,
            };
            return spriteDefaultMaterial;
        }

        private SpriteRenderer FindSourceRenderer(Transform searchRoot)
        {
            if (searchRoot == null)
            {
                return null;
            }

            SpriteRenderer fallbackRenderer = null;
            SpriteRenderer[] renderers = searchRoot.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (!IsValidSourceRenderer(renderer))
                {
                    continue;
                }

                if (renderer.isVisible || renderer.gameObject.activeInHierarchy)
                {
                    return renderer;
                }

                fallbackRenderer ??= renderer;
            }

            return fallbackRenderer;
        }

        private bool IsValidSourceRenderer(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            return runtimeRoot == null || !renderer.transform.IsChildOf(runtimeRoot.transform);
        }

        private void SetRuntimeRootActive(bool active)
        {
            if (runtimeRoot != null && runtimeRoot.activeSelf != active)
            {
                runtimeRoot.SetActive(active);
            }
        }

        private void DestroyRuntimeRoot()
        {
            RestoreSourceMaterial();
            outlineRenderers.Clear();
            DestroyRuntimeObject(runtimeRoot);
            DestroyRuntimeObject(spriteDefaultMaterial);
            runtimeRoot = null;
            spriteDefaultMaterial = null;
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

            DestroyImmediate(target);
        }
    }
}

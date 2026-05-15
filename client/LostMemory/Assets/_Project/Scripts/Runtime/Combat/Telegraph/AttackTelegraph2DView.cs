using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace LostMemory.Combat.Telegraph
{
    public enum AttackTelegraphShape2D
    {
        Box,
        Circle
    }

    public struct AttackTelegraphRequest2D
    {
        public AttackTelegraphShape2D Shape;
        public Vector2 Center;
        public Vector2 Direction;
        public Vector2 Size;
        public Color Color;
        public float Duration;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Telegraph/Attack Telegraph 2D View")]
    public class AttackTelegraph2DView : MonoBehaviour
    {
        [SerializeField] private string previewObjectName = "AttackTelegraphPreview";
        [SerializeField] private Transform previewParent;
        [SerializeField] private SpriteRenderer sortingReference;
        [FormerlySerializedAs("sortingOrder")]
        [SerializeField] private int sortingOrderOffset = -5;
        [SerializeField] private bool renderOutsideSortingGroup = true;
        [SerializeField] private bool useFixedTelegraphSorting = true;
        [SerializeField] private string telegraphSortingLayerName = "Foreground";
        [SerializeField] private int telegraphSortingOrder = 50;
        [SerializeField] private SortingGroup sortingGroupReference;
        [SerializeField] private Color defaultColor = new Color(1f, 0.33f, 0.08f, 0.32f);
        [SerializeField] private float pulseSpeed = 8f;
        [SerializeField] [Range(0f, 1f)] private float pulseAlphaStrength = 0.18f;
        [SerializeField] private Vector3 positionOffset = new Vector3(0f, 0.02f, 0f);

        private const int TelegraphTextureSize = 64;

        private static Sprite _boxTelegraphSprite;
        private static Sprite _circleTelegraphSprite;

        private GameObject _previewObject;
        private Transform _previewTransform;
        private SpriteRenderer _previewRenderer;
        private AttackTelegraphRequest2D _activeRequest;
        private bool _visible;
        private float _remainingDuration = -1f;

        public bool IsVisible => _visible;

        private void Reset()
        {
            previewParent = transform;
            sortingReference = FindSortingReference();
        }

        private void Awake()
        {
            EnsurePreviewRenderer();
            HideImmediate();
        }

        private void OnDisable()
        {
            HideImmediate();
        }

        private void OnDestroy()
        {
            if (_previewObject == null)
            {
                return;
            }

            Destroy(_previewObject);
            _previewObject = null;
        }

        public void Show(AttackTelegraphRequest2D request)
        {
            EnsurePreviewRenderer();
            _activeRequest = request;
            _remainingDuration = request.Duration;
            _visible = true;
            ApplyRequest();
            _previewObject.SetActive(true);
        }

        public void Refresh(AttackTelegraphRequest2D request)
        {
            if (!_visible)
            {
                Show(request);
                return;
            }

            _activeRequest = request;
            _remainingDuration = request.Duration;
            ApplyRequest();
        }

        public void Hide()
        {
            HideImmediate();
        }

        private void Update()
        {
            if (!_visible || _previewRenderer == null)
            {
                return;
            }

            if (_remainingDuration > 0f)
            {
                _remainingDuration -= Time.deltaTime;
                if (_remainingDuration <= 0f)
                {
                    HideImmediate();
                    return;
                }
            }

            ApplyPulse();
        }

        private void EnsurePreviewRenderer()
        {
            if (_previewRenderer != null)
            {
                return;
            }

            previewParent ??= transform;
            sortingReference ??= FindSortingReference();
            sortingGroupReference ??= FindSortingGroupReference();

            _previewObject = new GameObject(previewObjectName);
            _previewObject.layer = gameObject.layer;

            _previewTransform = _previewObject.transform;
            _previewTransform.SetParent(ResolvePreviewRenderParent(), false);

            _previewRenderer = _previewObject.AddComponent<SpriteRenderer>();
            _previewRenderer.sprite = GetOrCreateTelegraphSprite(AttackTelegraphShape2D.Box);
            _previewRenderer.sortingOrder = sortingOrderOffset;
            _previewRenderer.drawMode = SpriteDrawMode.Simple;

            ApplySorting();
        }

        private void ApplyRequest()
        {
            Vector2 direction = _activeRequest.Direction.sqrMagnitude > 0.0001f
                ? _activeRequest.Direction.normalized
                : Vector2.right;
            float angle = _activeRequest.Shape == AttackTelegraphShape2D.Circle
                ? 0f
                : Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Color color = _activeRequest.Color.a > 0f ? _activeRequest.Color : defaultColor;

            _previewRenderer.sprite = GetOrCreateTelegraphSprite(_activeRequest.Shape);
            _previewTransform.position = (Vector3)_activeRequest.Center + positionOffset;
            _previewTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            _previewTransform.localScale = ResolveLocalScale(ResolveShapeSize(_activeRequest.Size, _activeRequest.Shape));
            _previewRenderer.color = color;
            _previewRenderer.enabled = true;

            ApplySorting();
        }

        private void ApplyPulse()
        {
            Color color = _activeRequest.Color.a > 0f ? _activeRequest.Color : defaultColor;
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float alphaMultiplier = Mathf.Lerp(1f - pulseAlphaStrength, 1f, pulse);
            _previewRenderer.color = new Color(color.r, color.g, color.b, color.a * alphaMultiplier);
        }

        private Vector3 ResolveLocalScale(Vector2 worldSize)
        {
            Transform renderParent = _previewTransform != null ? _previewTransform.parent : previewParent;
            Vector3 scaleBasis = renderParent != null ? renderParent.lossyScale : Vector3.one;
            float safeScaleX = Mathf.Abs(scaleBasis.x) > 0.0001f ? Mathf.Abs(scaleBasis.x) : 1f;
            float safeScaleY = Mathf.Abs(scaleBasis.y) > 0.0001f ? Mathf.Abs(scaleBasis.y) : 1f;
            return new Vector3(worldSize.x / safeScaleX, worldSize.y / safeScaleY, 1f);
        }

        private static Vector2 ResolveShapeSize(Vector2 requestedSize, AttackTelegraphShape2D shape)
        {
            if (shape != AttackTelegraphShape2D.Circle)
            {
                return requestedSize;
            }

            float diameter = Mathf.Max(Mathf.Abs(requestedSize.x), Mathf.Abs(requestedSize.y));
            return new Vector2(diameter, diameter);
        }

        private Transform ResolvePreviewRenderParent()
        {
            if (!renderOutsideSortingGroup)
            {
                return previewParent;
            }

            sortingGroupReference ??= FindSortingGroupReference();
            return sortingGroupReference != null ? sortingGroupReference.transform.parent : previewParent;
        }

        private void ApplySorting()
        {
            if (_previewRenderer == null)
            {
                return;
            }

            if (useFixedTelegraphSorting)
            {
                ApplyFixedTelegraphSorting();
                return;
            }

            sortingGroupReference ??= FindSortingGroupReference();
            if (renderOutsideSortingGroup && sortingGroupReference != null)
            {
                _previewRenderer.sortingLayerID = sortingGroupReference.sortingLayerID;
                _previewRenderer.sortingOrder = sortingGroupReference.sortingOrder + sortingOrderOffset;
                return;
            }

            sortingReference ??= FindSortingReference();
            if (sortingReference == null)
            {
                _previewRenderer.sortingOrder = sortingOrderOffset;
                return;
            }

            _previewRenderer.sortingLayerID = sortingReference.sortingLayerID;
            _previewRenderer.sortingOrder = sortingReference.sortingOrder + sortingOrderOffset;
        }

        private void ApplyFixedTelegraphSorting()
        {
            bool layerApplied = false;
            if (!string.IsNullOrWhiteSpace(telegraphSortingLayerName))
            {
                if (TryGetSortingLayerId(telegraphSortingLayerName, out int telegraphLayerId))
                {
                    _previewRenderer.sortingLayerID = telegraphLayerId;
                    layerApplied = true;
                }
            }

            if (!layerApplied)
            {
                sortingReference ??= FindSortingReference();
                if (sortingReference != null)
                {
                    _previewRenderer.sortingLayerID = sortingReference.sortingLayerID;
                }
            }

            _previewRenderer.sortingOrder = telegraphSortingOrder;
        }

        private static bool TryGetSortingLayerId(string layerName, out int layerId)
        {
            foreach (SortingLayer sortingLayer in SortingLayer.layers)
            {
                if (sortingLayer.name == layerName)
                {
                    layerId = sortingLayer.id;
                    return true;
                }
            }

            layerId = 0;
            return false;
        }

        private SortingGroup FindSortingGroupReference()
        {
            if (sortingReference != null)
            {
                SortingGroup group = sortingReference.GetComponentInParent<SortingGroup>();
                if (group != null)
                {
                    return group;
                }
            }

            if (previewParent != null)
            {
                SortingGroup group = previewParent.GetComponentInParent<SortingGroup>();
                if (group != null)
                {
                    return group;
                }
            }

            return GetComponentInParent<SortingGroup>();
        }

        private SpriteRenderer FindSortingReference()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i] == _previewRenderer)
                {
                    continue;
                }

                return renderers[i];
            }

            return null;
        }

        private void HideImmediate()
        {
            _visible = false;
            _remainingDuration = -1f;

            if (_previewObject != null)
            {
                _previewObject.SetActive(false);
            }
        }

        private static Sprite GetOrCreateTelegraphSprite(AttackTelegraphShape2D shape)
        {
            if (shape == AttackTelegraphShape2D.Circle)
            {
                return GetOrCreateCircleTelegraphSprite();
            }

            return GetOrCreateBoxTelegraphSprite();
        }

        private static Sprite GetOrCreateBoxTelegraphSprite()
        {
            if (_boxTelegraphSprite != null)
            {
                return _boxTelegraphSprite;
            }

            Texture2D texture = new Texture2D(TelegraphTextureSize, TelegraphTextureSize, TextureFormat.RGBA32, false)
            {
                name = "AttackTelegraphBoxTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[TelegraphTextureSize * TelegraphTextureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            _boxTelegraphSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TelegraphTextureSize, TelegraphTextureSize),
                new Vector2(0.5f, 0.5f),
                TelegraphTextureSize);
            _boxTelegraphSprite.name = "AttackTelegraphBoxSprite";
            return _boxTelegraphSprite;
        }

        private static Sprite GetOrCreateCircleTelegraphSprite()
        {
            if (_circleTelegraphSprite != null)
            {
                return _circleTelegraphSprite;
            }

            Texture2D texture = new Texture2D(TelegraphTextureSize, TelegraphTextureSize, TextureFormat.RGBA32, false)
            {
                name = "AttackTelegraphCircleTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[TelegraphTextureSize * TelegraphTextureSize];
            float radius = TelegraphTextureSize * 0.5f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < TelegraphTextureSize; y++)
            {
                for (int x = 0; x < TelegraphTextureSize; x++)
                {
                    Vector2 pixelCenter = new Vector2(x + 0.5f, y + 0.5f);
                    float distance = Vector2.Distance(pixelCenter, center);
                    pixels[y * TelegraphTextureSize + x] = distance <= radius ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            _circleTelegraphSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TelegraphTextureSize, TelegraphTextureSize),
                new Vector2(0.5f, 0.5f),
                TelegraphTextureSize);
            _circleTelegraphSprite.name = "AttackTelegraphCircleSprite";
            return _circleTelegraphSprite;
        }
    }
}

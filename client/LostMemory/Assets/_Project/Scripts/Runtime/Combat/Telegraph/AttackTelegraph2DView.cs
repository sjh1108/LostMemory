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
        public bool ShowTimingMarker;
        public float TimingProgress;
        public Color TimingMarkerColor;
        public float TimingMarkerThickness;
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
        [FormerlySerializedAs("timingOutlineObjectName")]
        [SerializeField] private string timingMarkerObjectName = "AttackTelegraphTimingMarker";
        [FormerlySerializedAs("timingOutlineDefaultColor")]
        [SerializeField] private Color timingMarkerDefaultColor = new Color(1f, 0.92f, 0.55f, 0.75f);
        [FormerlySerializedAs("timingOutlineMinScale")]
        [SerializeField] [Range(0.01f, 0.25f)] private float timingMarkerThickness = 0.055f;

        private const int TelegraphTextureSize = 64;

        private static Sprite _boxTelegraphSprite;
        private static Sprite _circleTelegraphSprite;

        private GameObject _previewObject;
        private Transform _previewTransform;
        private SpriteRenderer _previewRenderer;
        private GameObject _timingMarkerObject;
        private Transform _timingMarkerTransform;
        private SpriteRenderer _timingMarkerRenderer;
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
            _timingMarkerObject = null;
            _timingMarkerTransform = null;
            _timingMarkerRenderer = null;
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

        public bool TryGetRenderSorting(out int sortingLayerId, out int sortingOrder)
        {
            EnsurePreviewRenderer();
            if (_previewRenderer == null)
            {
                sortingLayerId = 0;
                sortingOrder = 0;
                return false;
            }

            ApplySorting();
            sortingLayerId = _previewRenderer.sortingLayerID;
            sortingOrder = _previewRenderer.sortingOrder;
            return true;
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
            TelegraphSpriteRendererUtility.ApplyTelegraphMaterial(_previewRenderer);

            ApplySorting();
            ApplyTimingMarker();
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
            ApplyTimingMarker();
        }

        private void ApplyPulse()
        {
            Color color = _activeRequest.Color.a > 0f ? _activeRequest.Color : defaultColor;
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float alphaMultiplier = Mathf.Lerp(1f - pulseAlphaStrength, 1f, pulse);
            _previewRenderer.color = new Color(color.r, color.g, color.b, color.a * alphaMultiplier);
        }

        private void EnsureTimingMarkerRenderer()
        {
            if (_timingMarkerRenderer != null)
            {
                return;
            }

            if (_previewTransform == null)
            {
                return;
            }

            string objectName = string.IsNullOrWhiteSpace(timingMarkerObjectName)
                ? "AttackTelegraphTimingMarker"
                : timingMarkerObjectName;
            _timingMarkerObject = new GameObject(objectName);
            _timingMarkerObject.layer = gameObject.layer;

            _timingMarkerTransform = _timingMarkerObject.transform;
            _timingMarkerTransform.SetParent(_previewTransform, false);

            _timingMarkerRenderer = _timingMarkerObject.AddComponent<SpriteRenderer>();
            _timingMarkerRenderer.sprite = GetOrCreateBoxTelegraphSprite();
            _timingMarkerRenderer.drawMode = SpriteDrawMode.Simple;
            TelegraphSpriteRendererUtility.ApplyTelegraphMaterial(_timingMarkerRenderer);

            ApplyTimingMarkerSorting();
        }

        private void ApplyTimingMarker()
        {
            if (!_activeRequest.ShowTimingMarker)
            {
                HideTimingMarker();
                return;
            }

            EnsureTimingMarkerRenderer();
            if (_timingMarkerRenderer == null)
            {
                return;
            }

            Color color = _activeRequest.TimingMarkerColor.a > 0f
                ? _activeRequest.TimingMarkerColor
                : timingMarkerDefaultColor;
            float progress = Mathf.Clamp01(_activeRequest.TimingProgress);
            float thickness = _activeRequest.TimingMarkerThickness > 0f
                ? _activeRequest.TimingMarkerThickness
                : timingMarkerThickness;
            thickness = Mathf.Clamp(thickness, 0.01f, 0.25f);
            float localX = Mathf.Lerp(-0.5f + thickness * 0.5f, 0.5f - thickness * 0.5f, progress);

            _timingMarkerRenderer.sprite = GetOrCreateBoxTelegraphSprite();
            _timingMarkerRenderer.color = color;
            _timingMarkerRenderer.enabled = true;
            _timingMarkerTransform.localPosition = new Vector3(localX, 0f, 0f);
            _timingMarkerTransform.localRotation = Quaternion.identity;
            _timingMarkerTransform.localScale = new Vector3(thickness, 1f, 1f);
            _timingMarkerObject.SetActive(true);

            ApplyTimingMarkerSorting();
        }

        private void HideTimingMarker()
        {
            if (_timingMarkerRenderer != null)
            {
                _timingMarkerRenderer.enabled = false;
            }

            if (_timingMarkerObject != null)
            {
                _timingMarkerObject.SetActive(false);
            }
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
                ApplyTimingMarkerSorting();
                return;
            }

            sortingGroupReference ??= FindSortingGroupReference();
            if (renderOutsideSortingGroup && sortingGroupReference != null)
            {
                _previewRenderer.sortingLayerID = sortingGroupReference.sortingLayerID;
                _previewRenderer.sortingOrder = sortingGroupReference.sortingOrder + sortingOrderOffset;
                ApplyTimingMarkerSorting();
                return;
            }

            sortingReference ??= FindSortingReference();
            if (sortingReference == null)
            {
                _previewRenderer.sortingOrder = sortingOrderOffset;
                ApplyTimingMarkerSorting();
                return;
            }

            _previewRenderer.sortingLayerID = sortingReference.sortingLayerID;
            _previewRenderer.sortingOrder = sortingReference.sortingOrder + sortingOrderOffset;
            ApplyTimingMarkerSorting();
        }

        private void ApplyTimingMarkerSorting()
        {
            if (_previewRenderer == null || _timingMarkerRenderer == null)
            {
                return;
            }

            _timingMarkerRenderer.sortingLayerID = _previewRenderer.sortingLayerID;
            _timingMarkerRenderer.sortingOrder = _previewRenderer.sortingOrder + 1;
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
                if (renderers[i] == null || renderers[i] == _previewRenderer || renderers[i] == _timingMarkerRenderer)
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

            HideTimingMarker();
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

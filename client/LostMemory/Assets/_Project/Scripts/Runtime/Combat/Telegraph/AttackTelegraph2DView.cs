using UnityEngine;

namespace LostMemory.Combat.Telegraph
{
    public enum AttackTelegraphShape2D
    {
        Box
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
        [SerializeField] private int sortingOrder = -5;
        [SerializeField] private Color defaultColor = new Color(1f, 0.33f, 0.08f, 0.32f);
        [SerializeField] private float pulseSpeed = 8f;
        [SerializeField] [Range(0f, 1f)] private float pulseAlphaStrength = 0.18f;
        [SerializeField] private Vector3 positionOffset = new Vector3(0f, 0.02f, 0f);

        private static Sprite _telegraphSprite;

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

            _previewObject = new GameObject(previewObjectName);
            _previewObject.layer = gameObject.layer;

            _previewTransform = _previewObject.transform;
            _previewTransform.SetParent(previewParent, false);

            _previewRenderer = _previewObject.AddComponent<SpriteRenderer>();
            _previewRenderer.sprite = GetOrCreateTelegraphSprite();
            _previewRenderer.sortingOrder = sortingOrder;
            _previewRenderer.drawMode = SpriteDrawMode.Simple;

            CopySortingFromReference();
        }

        private void ApplyRequest()
        {
            if (_activeRequest.Shape != AttackTelegraphShape2D.Box)
            {
                return;
            }

            Vector2 direction = _activeRequest.Direction.sqrMagnitude > 0.0001f
                ? _activeRequest.Direction.normalized
                : Vector2.right;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Color color = _activeRequest.Color.a > 0f ? _activeRequest.Color : defaultColor;

            _previewTransform.position = (Vector3)_activeRequest.Center + positionOffset;
            _previewTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            _previewTransform.localScale = ResolveLocalScale(_activeRequest.Size);
            _previewRenderer.color = color;
            _previewRenderer.enabled = true;

            CopySortingFromReference();
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
            Vector3 scaleBasis = previewParent != null ? previewParent.lossyScale : Vector3.one;
            float safeScaleX = Mathf.Abs(scaleBasis.x) > 0.0001f ? Mathf.Abs(scaleBasis.x) : 1f;
            float safeScaleY = Mathf.Abs(scaleBasis.y) > 0.0001f ? Mathf.Abs(scaleBasis.y) : 1f;
            return new Vector3(worldSize.x / safeScaleX, worldSize.y / safeScaleY, 1f);
        }

        private void CopySortingFromReference()
        {
            if (_previewRenderer == null)
            {
                return;
            }

            sortingReference ??= FindSortingReference();
            if (sortingReference == null)
            {
                return;
            }

            _previewRenderer.sortingLayerID = sortingReference.sortingLayerID;
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

        private static Sprite GetOrCreateTelegraphSprite()
        {
            if (_telegraphSprite != null)
            {
                return _telegraphSprite;
            }

            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                name = "AttackTelegraphBoxTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            _telegraphSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
            _telegraphSprite.name = "AttackTelegraphBoxSprite";
            return _telegraphSprite;
        }
    }
}

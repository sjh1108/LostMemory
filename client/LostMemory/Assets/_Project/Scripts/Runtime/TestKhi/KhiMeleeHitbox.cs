using System.Collections.Generic;
using LostMemory.Data;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    public class KhiMeleeHitbox : MonoBehaviour
    {
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField] private int maximumHitsPerSample = 32;
        [SerializeField] private float targetInvincibilityDuration = 0f;
        [SerializeField] private float targetFlickerDuration = 0f;
        [SerializeField] private bool drawDebugGizmos = true;
        [SerializeField] private Color debugGizmoColor = new Color(1f, 0.2f, 0.1f, 0.25f);
        [SerializeField] private bool showRuntimePreview = true;
        [SerializeField] private Color runtimePreviewColor = new Color(1f, 0f, 0f, 0.28f);
        [SerializeField] private int runtimePreviewSortingOrder = 1000;

        private Collider2D[] _overlapResults;
        private bool _hasDebugHitbox;
        private Vector2 _debugCenter;
        private Vector2 _debugSize;
        private float _debugAngleDeg;
        private SpriteRenderer _runtimePreviewRenderer;
        private Sprite _runtimePreviewSprite;

        private void Awake()
        {
            EnsureOverlapBuffer();
            EnsureRuntimePreview();
        }

        public int Sample(KhiAttackRequest request, AttackStepData step, float damage, HashSet<Health> alreadyHit, List<Health> hitsThisSample)
        {
            EnsureOverlapBuffer();
            hitsThisSample?.Clear();

            float aimAngleDeg = request.AimAngleDegrees;
            // baseline offset(Right 기준)을 현재 aim 각도로 회전시켜 실제 center 계산.
            Vector2 rotatedOffset = (Vector2)(Quaternion.Euler(0f, 0f, aimAngleDeg) * (Vector3)step.hitboxOffset);
            Vector2 center = (Vector2)request.Origin + rotatedOffset;
            _debugCenter = center;
            _debugSize = step.hitboxSize;
            _debugAngleDeg = aimAngleDeg;
            _hasDebugHitbox = true;
            ShowRuntimePreview(center, step.hitboxSize, aimAngleDeg);

            int hitCount = Physics2D.OverlapBoxNonAlloc(center, step.hitboxSize, aimAngleDeg, _overlapResults, targetLayers);
            int appliedHits = 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = _overlapResults[i];
                if (hitCollider == null)
                {
                    continue;
                }

                Health health = hitCollider.GetComponentInParent<Health>();
                if (health == null || alreadyHit.Contains(health) || IsOwnedByAttacker(health, request.Attacker))
                {
                    continue;
                }

                if (!health.CanTakeDamageThisFrame())
                {
                    continue;
                }

                alreadyHit.Add(health);
                health.Damage(damage, request.Attacker, targetFlickerDuration, targetInvincibilityDuration, request.AimDirection);
                hitsThisSample?.Add(health);
                appliedHits++;
            }

            return appliedHits;
        }

        public void HideRuntimePreview()
        {
            if (_runtimePreviewRenderer != null)
            {
                _runtimePreviewRenderer.enabled = false;
            }
        }

        private void EnsureOverlapBuffer()
        {
            if (_overlapResults != null && _overlapResults.Length == maximumHitsPerSample)
            {
                return;
            }

            _overlapResults = new Collider2D[Mathf.Max(1, maximumHitsPerSample)];
        }

        private void EnsureRuntimePreview()
        {
            if (!showRuntimePreview || _runtimePreviewRenderer != null)
            {
                return;
            }

            GameObject previewObject = new GameObject("Khi_MeleeHitboxPreview");
            previewObject.transform.SetParent(transform, false);
            _runtimePreviewRenderer = previewObject.AddComponent<SpriteRenderer>();
            _runtimePreviewRenderer.sprite = GetRuntimePreviewSprite();
            _runtimePreviewRenderer.color = runtimePreviewColor;
            CopySortingLayerFromOwner(_runtimePreviewRenderer);
            _runtimePreviewRenderer.sortingOrder = runtimePreviewSortingOrder;
            _runtimePreviewRenderer.enabled = false;
        }

        private Sprite GetRuntimePreviewSprite()
        {
            if (_runtimePreviewSprite != null)
            {
                return _runtimePreviewSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Khi_MeleeHitboxPreviewTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            _runtimePreviewSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _runtimePreviewSprite.name = "Khi_MeleeHitboxPreviewSprite";
            return _runtimePreviewSprite;
        }

        private void ShowRuntimePreview(Vector2 center, Vector2 size, float angleDeg)
        {
            if (!showRuntimePreview)
            {
                return;
            }

            EnsureRuntimePreview();
            if (_runtimePreviewRenderer == null)
            {
                return;
            }

            _runtimePreviewRenderer.transform.SetPositionAndRotation(
                new Vector3(center.x, center.y, transform.position.z),
                Quaternion.Euler(0f, 0f, angleDeg));
            _runtimePreviewRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            _runtimePreviewRenderer.color = runtimePreviewColor;
            CopySortingLayerFromOwner(_runtimePreviewRenderer);
            _runtimePreviewRenderer.sortingOrder = runtimePreviewSortingOrder;
            _runtimePreviewRenderer.enabled = true;
        }

        private void CopySortingLayerFromOwner(SpriteRenderer targetRenderer)
        {
            SpriteRenderer ownerRenderer = GetComponentInChildren<SpriteRenderer>();
            if (ownerRenderer == null || ownerRenderer == targetRenderer)
            {
                return;
            }

            targetRenderer.sortingLayerID = ownerRenderer.sortingLayerID;
        }

        private static bool IsOwnedByAttacker(Health health, GameObject attacker)
        {
            if (attacker == null)
            {
                return false;
            }

            return health.gameObject == attacker || health.transform.IsChildOf(attacker.transform);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos || !_hasDebugHitbox)
            {
                return;
            }

            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(_debugCenter, Quaternion.Euler(0f, 0f, _debugAngleDeg), Vector3.one);
            Gizmos.color = debugGizmoColor;
            Gizmos.DrawCube(Vector3.zero, _debugSize);
            Gizmos.color = new Color(debugGizmoColor.r, debugGizmoColor.g, debugGizmoColor.b, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, _debugSize);
            Gizmos.matrix = prevMatrix;
        }
    }
}

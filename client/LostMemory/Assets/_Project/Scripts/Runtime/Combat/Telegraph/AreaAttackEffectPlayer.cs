using System.Collections;
using System.Collections.Generic;
using LostMemory.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostMemory.Combat.Telegraph
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Telegraph/Area Attack Effect Player")]
    public sealed class AreaAttackEffectPlayer : MonoBehaviour
    {
        [SerializeField] private AttackTelegraph2DView sortingReferenceTelegraph;
        [SerializeField] private SpriteRenderer sortingReferenceRenderer;
        [SerializeField] private SortingGroup sortingGroupReference;
        [SerializeField] private Transform effectParent;
        [SerializeField] private int sortingOrderOffset = 1;
        [SerializeField] private Sprite[] animationSprites = System.Array.Empty<Sprite>();
        [SerializeField, Min(0.01f)] private float frameRate = 16f;
        [SerializeField] private Vector2 sizeOverride = Vector2.zero;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private Vector3 positionOffset = Vector3.zero;

        private readonly List<GameObject> _activeEffects = new List<GameObject>();

        private void Reset()
        {
            RefreshReferences();
        }

        private void Awake()
        {
            RefreshReferences();
        }

        private void OnValidate()
        {
            frameRate = Mathf.Max(0.01f, frameRate);
            RefreshReferences();
        }

        private void OnDestroy()
        {
            ClearActiveEffects();
        }

        public void PlayOnce(Vector2 center, Vector2 areaSize)
        {
            Sprite firstSprite = ResolveFirstSprite();
            if (firstSprite == null)
            {
                return;
            }

            Vector2 targetSize = ResolveTargetSize(areaSize);
            GameObject effectObject = CreateEffectObject(firstSprite, center, targetSize);
            if (effectObject == null)
            {
                return;
            }

            _activeEffects.Add(effectObject);
            StartCoroutine(PlayOnceSequence(
                effectObject,
                effectObject.transform,
                effectObject.GetComponent<SpriteRenderer>(),
                targetSize));
        }

        public void RefreshReferences()
        {
            sortingReferenceTelegraph ??= GetComponent<AttackTelegraph2DView>();
            sortingGroupReference ??= GetComponentInParent<SortingGroup>();
            sortingReferenceRenderer ??= FindSortingReferenceRenderer();
        }

        private IEnumerator PlayOnceSequence(
            GameObject effectObject,
            Transform effectTransform,
            SpriteRenderer spriteRenderer,
            Vector2 targetSize)
        {
            float frameDuration = 1f / Mathf.Max(0.01f, frameRate);

            for (int i = 0; i < animationSprites.Length; i++)
            {
                Sprite frame = animationSprites[i];
                if (frame == null)
                {
                    continue;
                }

                if (effectObject == null || spriteRenderer == null)
                {
                    yield break;
                }

                spriteRenderer.sprite = frame;
                effectTransform.localScale = ResolveEffectScale(frame, effectTransform.parent, targetSize);
                yield return new WaitForSeconds(frameDuration);
            }

            if (effectObject != null)
            {
                _activeEffects.Remove(effectObject);
                Destroy(effectObject);
            }
        }

        private GameObject CreateEffectObject(Sprite firstSprite, Vector2 center, Vector2 targetSize)
        {
            if (firstSprite == null)
            {
                return null;
            }

            GameObject effectObject = new GameObject("AreaAttackEffect");
            effectObject.layer = gameObject.layer;

            Transform effectTransform = effectObject.transform;
            effectTransform.SetParent(ResolveEffectParent(), false);
            effectTransform.position = (Vector3)center + positionOffset;

            SpriteRenderer spriteRenderer = effectObject.AddComponent<SpriteRenderer>();
            RuntimeSpriteMaterialUtility.ApplySpriteMaterial(spriteRenderer);
            spriteRenderer.sprite = firstSprite;
            spriteRenderer.color = color;

            ApplySorting(spriteRenderer);
            effectTransform.localScale = ResolveEffectScale(firstSprite, effectTransform.parent, targetSize);
            return effectObject;
        }

        private Vector2 ResolveTargetSize(Vector2 areaSize)
        {
            return sizeOverride.sqrMagnitude > 0.0001f ? sizeOverride : areaSize;
        }

        private Vector3 ResolveEffectScale(Sprite sprite, Transform renderParent, Vector2 targetSize)
        {
            if (sprite == null)
            {
                return Vector3.one;
            }

            Vector2 spriteSize = sprite.bounds.size;
            float safeSpriteWidth = Mathf.Abs(spriteSize.x) > 0.0001f ? Mathf.Abs(spriteSize.x) : 1f;
            float safeSpriteHeight = Mathf.Abs(spriteSize.y) > 0.0001f ? Mathf.Abs(spriteSize.y) : 1f;

            Vector3 parentScale = renderParent != null ? renderParent.lossyScale : Vector3.one;
            float safeParentX = Mathf.Abs(parentScale.x) > 0.0001f ? Mathf.Abs(parentScale.x) : 1f;
            float safeParentY = Mathf.Abs(parentScale.y) > 0.0001f ? Mathf.Abs(parentScale.y) : 1f;

            return new Vector3(
                targetSize.x / safeSpriteWidth / safeParentX,
                targetSize.y / safeSpriteHeight / safeParentY,
                1f);
        }

        private void ApplySorting(SpriteRenderer spriteRenderer)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (sortingReferenceTelegraph != null
                && sortingReferenceTelegraph.TryGetRenderSorting(out int telegraphSortingLayerId, out int telegraphSortingOrder))
            {
                spriteRenderer.sortingLayerID = telegraphSortingLayerId;
                spriteRenderer.sortingOrder = telegraphSortingOrder + Mathf.Max(1, sortingOrderOffset);
                return;
            }

            if (sortingGroupReference != null)
            {
                spriteRenderer.sortingLayerID = sortingGroupReference.sortingLayerID;
                spriteRenderer.sortingOrder = sortingGroupReference.sortingOrder + sortingOrderOffset;
                return;
            }

            if (sortingReferenceRenderer != null)
            {
                spriteRenderer.sortingLayerID = sortingReferenceRenderer.sortingLayerID;
                spriteRenderer.sortingOrder = sortingReferenceRenderer.sortingOrder + sortingOrderOffset;
                return;
            }

            spriteRenderer.sortingOrder = sortingOrderOffset;
        }

        private Transform ResolveEffectParent()
        {
            if (effectParent != null)
            {
                return effectParent;
            }

            if (sortingGroupReference != null)
            {
                return sortingGroupReference.transform.parent;
            }

            return transform.parent;
        }

        private Sprite ResolveFirstSprite()
        {
            if (animationSprites == null)
            {
                return null;
            }

            for (int i = 0; i < animationSprites.Length; i++)
            {
                if (animationSprites[i] != null)
                {
                    return animationSprites[i];
                }
            }

            return null;
        }

        private SpriteRenderer FindSortingReferenceRenderer()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    return renderers[i];
                }
            }

            return null;
        }

        private void ClearActiveEffects()
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (_activeEffects[i] != null)
                {
                    Destroy(_activeEffects[i]);
                }
            }

            _activeEffects.Clear();
        }
    }
}

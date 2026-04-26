using System;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Boss Room Door View")]
    public sealed class BossRoomDoorView : MonoBehaviour
    {
        [SerializeField] private GameObject blockingRoot;
        [SerializeField] private SpriteRenderer doorRenderer;
        [SerializeField] private bool tintDoorRenderer = true;
        [SerializeField] private LineRenderer outlineRenderer;
        [SerializeField] private Collider2D outlineBoundsSource;
        [SerializeField] private float outlineWidth = 0.08f;
        [SerializeField] private float outlineLocalZOffset;
        [SerializeField, Min(0)] private int outlineCornerVertices = 6;
        [SerializeField, Min(0)] private int outlineCapVertices;
        [SerializeField] private Collider2D[] blockingColliders = Array.Empty<Collider2D>();
        [SerializeField] private Color lockedColor = new Color(0.55f, 0.58f, 0.62f, 1f);
        [SerializeField] private Color unlockedColor = new Color(0.20f, 0.90f, 0.55f, 0.50f);
        [SerializeField] private Color waitingColor = new Color(1f, 0.78f, 0.20f, 0.75f);
        [SerializeField] private Color confirmedColor = new Color(0.25f, 0.90f, 1f, 0.85f);
        [SerializeField] private Color transitioningColor = new Color(1f, 1f, 1f, 0.95f);

        public void ApplyState(BossRoomDoorState state)
        {
            RefreshBindings();
            ApplyBlocking(state == BossRoomDoorState.Locked);
            ApplyColor(GetStateColor(state));
        }

        private void Reset()
        {
            blockingRoot = gameObject;
            doorRenderer = GetComponent<SpriteRenderer>();
            outlineRenderer = GetComponentInChildren<LineRenderer>();
            outlineBoundsSource = FindOutlineBoundsSource();
            RefreshBindings();
        }

        private void OnValidate()
        {
            RefreshBindings();
        }

        private void ApplyBlocking(bool isBlocking)
        {
            if (blockingColliders == null)
            {
                return;
            }

            for (int i = 0; i < blockingColliders.Length; i++)
            {
                Collider2D blockingCollider = blockingColliders[i];
                if (blockingCollider != null)
                {
                    blockingCollider.enabled = isBlocking;
                }
            }
        }

        private void ApplyColor(Color color)
        {
            if (tintDoorRenderer && doorRenderer != null)
            {
                doorRenderer.color = color;
            }

            if (outlineRenderer == null)
            {
                return;
            }

            ConfigureOutlineRenderer();
            ApplyOutlineGeometry();
            outlineRenderer.startColor = color;
            outlineRenderer.endColor = color;
        }

        private void RefreshBindings()
        {
            if (blockingRoot == null)
            {
                blockingRoot = gameObject;
            }

            if (doorRenderer == null)
            {
                doorRenderer = GetComponent<SpriteRenderer>();
            }

            if (outlineRenderer == null)
            {
                outlineRenderer = GetComponentInChildren<LineRenderer>();
            }

            if (outlineBoundsSource == null)
            {
                outlineBoundsSource = FindOutlineBoundsSource();
            }

            if (blockingColliders == null || blockingColliders.Length == 0)
            {
                Collider2D[] colliders = blockingRoot.GetComponentsInChildren<Collider2D>(true);
                int blockingColliderCount = 0;

                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null && !colliders[i].isTrigger)
                    {
                        blockingColliderCount++;
                    }
                }

                blockingColliders = new Collider2D[blockingColliderCount];

                int targetIndex = 0;
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider2D collider = colliders[i];
                    if (collider == null || collider.isTrigger)
                    {
                        continue;
                    }

                    blockingColliders[targetIndex] = collider;
                    targetIndex++;
                }
            }

            ConfigureOutlineRenderer();
            ApplyOutlineGeometry();
        }

        private void ConfigureOutlineRenderer()
        {
            if (outlineRenderer == null)
            {
                return;
            }

            outlineRenderer.enabled = true;
            outlineRenderer.useWorldSpace = false;
            outlineRenderer.loop = true;
            outlineRenderer.widthMultiplier = outlineWidth;
            outlineRenderer.numCapVertices = Mathf.Max(0, outlineCapVertices);
            outlineRenderer.numCornerVertices = Mathf.Max(0, outlineCornerVertices);
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.alignment = LineAlignment.TransformZ;

            if (outlineRenderer.sharedMaterial == null)
            {
                Shader spriteShader = Shader.Find("Sprites/Default");
                if (spriteShader != null)
                {
                    outlineRenderer.sharedMaterial = new Material(spriteShader);
                }
            }
        }

        private void ApplyOutlineGeometry()
        {
            if (outlineRenderer == null || outlineBoundsSource == null)
            {
                return;
            }

            Bounds bounds = outlineBoundsSource.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            Vector3[] worldCorners =
            {
                new Vector3(min.x, min.y, outlineRenderer.transform.position.z),
                new Vector3(min.x, max.y, outlineRenderer.transform.position.z),
                new Vector3(max.x, max.y, outlineRenderer.transform.position.z),
                new Vector3(max.x, min.y, outlineRenderer.transform.position.z)
            };

            if (outlineRenderer.positionCount != worldCorners.Length)
            {
                outlineRenderer.positionCount = worldCorners.Length;
            }

            for (int i = 0; i < worldCorners.Length; i++)
            {
                Vector3 localCorner = outlineRenderer.transform.InverseTransformPoint(worldCorners[i]);
                localCorner.z = outlineLocalZOffset;
                outlineRenderer.SetPosition(i, localCorner);
            }
        }

        private Collider2D FindOutlineBoundsSource()
        {
            if (outlineRenderer == null)
            {
                return null;
            }

            Collider2D ownCollider = outlineRenderer.GetComponent<Collider2D>();
            if (ownCollider != null)
            {
                return ownCollider;
            }

            Collider2D[] parentColliders = outlineRenderer.GetComponentsInParent<Collider2D>(true);
            for (int i = 0; i < parentColliders.Length; i++)
            {
                Collider2D parentCollider = parentColliders[i];
                if (parentCollider != null && parentCollider.isTrigger)
                {
                    return parentCollider;
                }
            }

            return parentColliders.Length > 0 ? parentColliders[0] : null;
        }

        private Color GetStateColor(BossRoomDoorState state)
        {
            switch (state)
            {
                case BossRoomDoorState.Locked:
                    return lockedColor;
                case BossRoomDoorState.UnlockedIdle:
                    return unlockedColor;
                case BossRoomDoorState.WaitingForParty:
                    return waitingColor;
                case BossRoomDoorState.EntryConfirmed:
                    return confirmedColor;
                case BossRoomDoorState.Transitioning:
                    return transitioningColor;
                default:
                    return lockedColor;
            }
        }
    }
}

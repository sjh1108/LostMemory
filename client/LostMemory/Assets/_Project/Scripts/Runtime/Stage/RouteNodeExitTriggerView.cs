using UnityEngine;

namespace LostMemory.Stage
{
    public enum RouteNodeExitTriggerViewState
    {
        Locked = 0,
        UnlockedIdle = 1,
        CandidateInside = 2,
        Transitioning = 3
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Route Node Exit Trigger View")]
    public sealed class RouteNodeExitTriggerView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer portalRenderer;
        [SerializeField] private bool tintPortalRenderer = true;
        [SerializeField] private LineRenderer outlineRenderer;
        [SerializeField] private Collider2D outlineBoundsSource;
        [SerializeField] private float outlineWidth = 0.08f;
        [SerializeField] private float outlineLocalZOffset;
        [SerializeField, Min(0)] private int outlineCornerVertices = 6;
        [SerializeField, Min(0)] private int outlineCapVertices;
        [SerializeField] private Color lockedColor = new Color(0.55f, 0.58f, 0.62f, 0.85f);
        [SerializeField] private Color unlockedColor = new Color(0.20f, 0.90f, 0.55f, 0.65f);
        [SerializeField] private Color candidateInsideColor = new Color(1f, 0.78f, 0.20f, 0.80f);
        [SerializeField] private Color transitioningColor = new Color(0.25f, 0.90f, 1f, 0.90f);

        private RouteNodeExitTriggerViewState currentState = RouteNodeExitTriggerViewState.Locked;

        public void ApplyState(RouteNodeExitTriggerViewState state)
        {
            currentState = state;
            RefreshBindings();
            ApplyColor(GetStateColor(state));
        }

        private void Reset()
        {
            portalRenderer = GetComponent<SpriteRenderer>();
            if (portalRenderer == null)
            {
                portalRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);
            }

            outlineRenderer = GetComponentInChildren<LineRenderer>(includeInactive: true);
            outlineBoundsSource = FindOutlineBoundsSource();
            RefreshBindings();
        }

        private void OnValidate()
        {
            RefreshBindings();
            ApplyColor(GetStateColor(currentState));
        }

        private void RefreshBindings()
        {
            if (portalRenderer == null)
            {
                portalRenderer = GetComponent<SpriteRenderer>();
                if (portalRenderer == null)
                {
                    portalRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);
                }
            }

            if (outlineRenderer == null)
            {
                outlineRenderer = GetComponentInChildren<LineRenderer>(includeInactive: true);
            }

            if (outlineBoundsSource == null)
            {
                outlineBoundsSource = FindOutlineBoundsSource();
            }

            ConfigureOutlineRenderer();
            ApplyOutlineGeometry();
        }

        private void ApplyColor(Color color)
        {
            if (tintPortalRenderer && portalRenderer != null)
            {
                portalRenderer.color = color;
            }

            if (outlineRenderer == null)
            {
                return;
            }

            outlineRenderer.startColor = color;
            outlineRenderer.endColor = color;
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
            Collider2D ownCollider = GetComponent<Collider2D>();
            if (ownCollider != null)
            {
                return ownCollider;
            }

            if (outlineRenderer != null)
            {
                Collider2D lineCollider = outlineRenderer.GetComponent<Collider2D>();
                if (lineCollider != null)
                {
                    return lineCollider;
                }

                Collider2D[] parentColliders = outlineRenderer.GetComponentsInParent<Collider2D>(includeInactive: true);
                for (int i = 0; i < parentColliders.Length; i++)
                {
                    Collider2D parentCollider = parentColliders[i];
                    if (parentCollider != null && parentCollider.isTrigger)
                    {
                        return parentCollider;
                    }
                }

                if (parentColliders.Length > 0)
                {
                    return parentColliders[0];
                }
            }

            return GetComponentInParent<Collider2D>();
        }

        private Color GetStateColor(RouteNodeExitTriggerViewState state)
        {
            switch (state)
            {
                case RouteNodeExitTriggerViewState.Locked:
                    return lockedColor;
                case RouteNodeExitTriggerViewState.UnlockedIdle:
                    return unlockedColor;
                case RouteNodeExitTriggerViewState.CandidateInside:
                    return candidateInsideColor;
                case RouteNodeExitTriggerViewState.Transitioning:
                    return transitioningColor;
                default:
                    return lockedColor;
            }
        }
    }
}

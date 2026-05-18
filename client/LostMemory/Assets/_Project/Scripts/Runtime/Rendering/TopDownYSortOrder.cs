using UnityEngine;
using UnityEngine.Rendering;

namespace LostMemory.Rendering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Rendering/Top Down Y Sort Order")]
    [DefaultExecutionOrder(450)]
    public sealed class TopDownYSortOrder : MonoBehaviour
    {
        private const float DefaultUnitsPerOrder = 100f;
        private const int MinSortingOrder = -30000;
        private const int MaxSortingOrder = 30000;

        [SerializeField] private SortingGroup sortingGroup;
        [SerializeField] private Transform sortPoint;
        [SerializeField] private SpriteRenderer sortingReference;
        [SerializeField] private float unitsPerOrder = DefaultUnitsPerOrder;
        [SerializeField] private int orderOffset;

        private int _lastAppliedOrder = int.MinValue;

        public static TopDownYSortOrder EnsureOn(
            GameObject host,
            Transform configuredSortPoint = null,
            SpriteRenderer configuredSortingReference = null,
            int configuredOrderOffset = 0)
        {
            if (host == null)
            {
                return null;
            }

            SortingGroup resolvedGroup = host.GetComponentInParent<SortingGroup>();
            GameObject owner = resolvedGroup != null ? resolvedGroup.gameObject : host;

            resolvedGroup ??= owner.GetComponent<SortingGroup>();
            if (resolvedGroup == null)
            {
                resolvedGroup = owner.AddComponent<SortingGroup>();
            }

            TopDownYSortOrder sorter = owner.GetComponent<TopDownYSortOrder>();
            if (sorter == null)
            {
                sorter = owner.AddComponent<TopDownYSortOrder>();
            }

            SpriteRenderer resolvedReference = configuredSortingReference != null
                ? configuredSortingReference
                : owner.GetComponentInChildren<SpriteRenderer>(includeInactive: true);

            sorter.Configure(
                resolvedGroup,
                configuredSortPoint != null ? configuredSortPoint : owner.transform,
                resolvedReference,
                configuredOrderOffset);

            return sorter;
        }

        public void Configure(
            SortingGroup configuredSortingGroup,
            Transform configuredSortPoint,
            SpriteRenderer configuredSortingReference,
            int configuredOrderOffset)
        {
            sortingGroup = configuredSortingGroup;
            sortPoint = configuredSortPoint;
            sortingReference = configuredSortingReference;
            orderOffset = configuredOrderOffset;

            ResolveReferences();
            ApplySortingOrder(force: true);
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplySortingOrder(force: true);
        }

        private void LateUpdate()
        {
            ApplySortingOrder(force: false);
        }

        private void ResolveReferences()
        {
            if (sortingGroup == null)
            {
                sortingGroup = GetComponent<SortingGroup>();
            }

            if (sortingGroup == null)
            {
                sortingGroup = gameObject.AddComponent<SortingGroup>();
            }

            sortPoint ??= transform;
            sortingReference ??= GetComponentInChildren<SpriteRenderer>(includeInactive: true);

            if (sortingReference != null)
            {
                sortingGroup.sortingLayerID = sortingReference.sortingLayerID;
            }
        }

        private void ApplySortingOrder(bool force)
        {
            if (sortingGroup == null || sortPoint == null)
            {
                return;
            }

            float safeUnitsPerOrder = Mathf.Max(1f, unitsPerOrder);
            int nextOrder = Mathf.Clamp(
                Mathf.RoundToInt(-sortPoint.position.y * safeUnitsPerOrder) + orderOffset,
                MinSortingOrder,
                MaxSortingOrder);

            if (!force && nextOrder == _lastAppliedOrder)
            {
                return;
            }

            sortingGroup.sortingOrder = nextOrder;
            _lastAppliedOrder = nextOrder;
        }
    }
}

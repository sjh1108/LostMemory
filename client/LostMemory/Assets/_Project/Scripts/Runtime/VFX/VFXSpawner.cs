using UnityEngine;

namespace LostMemory.VFX
{
    /// <summary>
    /// CL-200: 모든 VFX 의 단일 스폰/정리 진입점.
    /// 현재는 Instantiate + Destroy 단순 래핑. 추후 풀링 도입 시 본 클래스의
    /// 시그니처(Spawn/SpawnAttached/Despawn) 그대로 유지하고 내부만 교체 → 호출 측 변경 X.
    /// </summary>
    public static class VFXSpawner
    {
        private const string GameplayEffectSortingLayerName = "Foreground";
        private const int GameplayEffectSortingOrder = 60;

        public static GameObject Spawn(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation = default,
            float autoDestroySeconds = 0f,
            Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[VFXSpawner] Spawn 호출에 prefab=null. 무시.");
                return null;
            }

            // Quaternion default = (0,0,0,0) 이므로 identity 로 치환
            if (rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f)
                rotation = Quaternion.identity;

            GameObject instance = parent != null
                ? Object.Instantiate(prefab, position, rotation, parent)
                : Object.Instantiate(prefab, position, rotation);

            if (autoDestroySeconds > 0f)
                Object.Destroy(instance, autoDestroySeconds);

            return instance;
        }

        public static void ApplyGameplayEffectSorting(GameObject vfxInstance)
        {
            ApplyFixedSorting(vfxInstance, GameplayEffectSortingLayerName, GameplayEffectSortingOrder);
        }

        public static void ApplyFixedSorting(
            GameObject vfxInstance,
            string sortingLayerName,
            int baseSortingOrder)
        {
            if (vfxInstance == null)
            {
                return;
            }

            Renderer[] renderers = vfxInstance.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0)
            {
                return;
            }

            int layerId = 0;
            bool hasSortingLayer = TryGetSortingLayerId(sortingLayerName, out layerId);
            int minSortingOrder = int.MaxValue;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                minSortingOrder = Mathf.Min(minSortingOrder, renderers[i].sortingOrder);
            }

            if (minSortingOrder == int.MaxValue)
            {
                minSortingOrder = 0;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (hasSortingLayer)
                {
                    renderer.sortingLayerID = layerId;
                }

                renderer.sortingOrder = baseSortingOrder + (renderer.sortingOrder - minSortingOrder);
            }
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

        public static GameObject SpawnAttached(
            GameObject prefab,
            Transform target,
            Vector3 localOffset = default,
            float autoDestroySeconds = 0f)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[VFXSpawner] SpawnAttached 호출에 prefab=null. 무시.");
                return null;
            }
            if (target == null)
            {
                Debug.LogWarning("[VFXSpawner] SpawnAttached 호출에 target=null. 무시.");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, target);
            instance.transform.localPosition = localOffset;
            instance.transform.localRotation = Quaternion.identity;

            if (autoDestroySeconds > 0f)
                Object.Destroy(instance, autoDestroySeconds);

            return instance;
        }

        public static void Despawn(GameObject vfxInstance)
        {
            if (vfxInstance == null) return;

            ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

            Object.Destroy(vfxInstance);
        }
    }
}

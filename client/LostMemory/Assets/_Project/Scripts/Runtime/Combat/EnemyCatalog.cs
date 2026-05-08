using System;
using LostMemory.Enemies;
using UnityEngine;

namespace LostMemory.Combat
{
    [Serializable]
    public sealed class EnemyCatalogEntry
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private GameObject prefab;
        [SerializeField] private EnemyData data;

        public string Id => id;
        public GameObject Prefab => prefab;
        public EnemyData Data => data;
    }

    /// <summary>
    /// enemyId (string) -> 적 prefab / optional EnemyData 매핑 한 곳.
    /// CL-033 의 enemyId 컨벤션 (`enemy_melee_basic`, `enemy_ranged_basic`, `enemy_charger_basic`, ...) 을
    /// 실제 prefab 과 밸런스 데이터에 묶는다.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyCatalog", menuName = "LostMemory/Combat/EnemyCatalog")]
    public sealed class EnemyCatalog : ScriptableObject
    {
        [SerializeField] private EnemyCatalogEntry[] entries = Array.Empty<EnemyCatalogEntry>();

        public bool TryGetEntry(string id, out EnemyCatalogEntry resolvedEntry)
        {
            resolvedEntry = null;
            if (string.IsNullOrEmpty(id) || entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                EnemyCatalogEntry entry = entries[i];
                if (entry == null || entry.Prefab == null)
                {
                    continue;
                }

                if (entry.Id == id)
                {
                    resolvedEntry = entry;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetPrefab(string id, out GameObject prefab)
        {
            prefab = null;
            if (!TryGetEntry(id, out EnemyCatalogEntry entry))
            {
                return false;
            }

            prefab = entry.Prefab;
            return true;
        }
    }
}

using System;
using UnityEngine;

namespace LostMemory.Combat
{
    [Serializable]
    public sealed class EnemyCatalogEntry
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private GameObject prefab;

        public string Id => id;
        public GameObject Prefab => prefab;
    }

    /// <summary>
    /// enemyId (string) → 적 prefab 매핑 한 곳.
    /// CL-033 의 enemyId 컨벤션 (`enemy_melee_basic`, `enemy_ranged_basic`, `enemy_charger_basic`, ...) 을
    /// 실제 prefab 에 묶는다.
    ///
    /// 적 관리자(클라2)가 후속에 EnemyData SO 를 도입하면 본 SO 의 prefab 자리를
    /// EnemyData 로 교체하거나, 본 SO 자체를 EnemyDataRegistry 로 흡수한다.
    /// 본 작업(CL-034) 에서는 임시 lookup 한 곳만 보장한다.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyCatalog", menuName = "LostMemory/Combat/EnemyCatalog")]
    public sealed class EnemyCatalog : ScriptableObject
    {
        [SerializeField] private EnemyCatalogEntry[] entries = Array.Empty<EnemyCatalogEntry>();

        public bool TryGetPrefab(string id, out GameObject prefab)
        {
            prefab = null;
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
                    prefab = entry.Prefab;
                    return true;
                }
            }

            return false;
        }
    }
}

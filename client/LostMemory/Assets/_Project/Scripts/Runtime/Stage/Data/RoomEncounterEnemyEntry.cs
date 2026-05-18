using System;
using UnityEngine;

namespace LostMemory.Stage.Data
{
    [Serializable]
    public sealed class RoomEncounterEnemyEntry
    {
        [SerializeField] private string enemyId = string.Empty;
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField] private string spawnGroupFilter = string.Empty;

        public string EnemyId => enemyId;
        public int Count => count;
        public string SpawnGroupFilter => spawnGroupFilter;
    }
}

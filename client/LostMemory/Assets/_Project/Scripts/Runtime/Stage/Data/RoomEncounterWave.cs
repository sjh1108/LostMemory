using System;
using UnityEngine;

namespace LostMemory.Stage.Data
{
    [Serializable]
    public sealed class RoomEncounterWave
    {
        [SerializeField] private string label = string.Empty;
        [SerializeField, Min(0f)] private float startDelay;
        [SerializeField] private RoomEncounterEnemyEntry[] enemies = Array.Empty<RoomEncounterEnemyEntry>();

        public string Label => label;
        public float StartDelay => startDelay;
        public RoomEncounterEnemyEntry[] Enemies => enemies ?? Array.Empty<RoomEncounterEnemyEntry>();
    }
}

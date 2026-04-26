using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Stage.Data
{
    [Serializable]
    public sealed class RoomEncounterSpec
    {
        [SerializeField] private RoomEncounterWave[] waves = Array.Empty<RoomEncounterWave>();

        public IReadOnlyList<RoomEncounterWave> Waves =>
            waves ?? Array.Empty<RoomEncounterWave>();

        // CL-035 가 클리어 카운팅 기준으로 사용한다.
        public int TotalEnemyCount
        {
            get
            {
                if (waves == null)
                {
                    return 0;
                }

                int total = 0;
                for (int i = 0; i < waves.Length; i++)
                {
                    RoomEncounterWave wave = waves[i];
                    if (wave == null)
                    {
                        continue;
                    }

                    RoomEncounterEnemyEntry[] entries = wave.Enemies;
                    for (int j = 0; j < entries.Length; j++)
                    {
                        RoomEncounterEnemyEntry entry = entries[j];
                        if (entry != null)
                        {
                            total += entry.Count;
                        }
                    }
                }

                return total;
            }
        }
    }
}

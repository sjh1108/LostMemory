using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Stage.Data
{
    // Layout prefab 루트에 붙어 자식 RoomEncounterSpawnPoint 들을 일괄 노출.
    // CL-034 가 GetSpawnPoints(groupFilter) 로 후보를 받아간다.
    public sealed class RoomEncounterAnchor : MonoBehaviour
    {
        [SerializeField] private RoomEncounterSpawnPoint[] cachedSpawnPoints = Array.Empty<RoomEncounterSpawnPoint>();

        private static readonly RoomEncounterSpawnPoint[] EmptyArray = Array.Empty<RoomEncounterSpawnPoint>();

        public IReadOnlyList<RoomEncounterSpawnPoint> AllSpawnPoints =>
            cachedSpawnPoints ?? EmptyArray;

        private void Awake()
        {
            // prefab 직렬화된 cachedSpawnPoints 는 동일 layout prefab 을 여러 방으로 spawn 시 stale fileID 가
            // 다른 방의 SpawnPoint 를 가리키는 문제 발생 (1F-3R 부터 잘못된 위치 소환 버그).
            // 런타임엔 항상 자기 자식 chain 에서 fresh 재계산.
            RefreshSpawnPoints();
        }

        // 빈 문자열 filter 면 전체 반환, 아니면 groupTag 정확 일치만 반환.
        public IReadOnlyList<RoomEncounterSpawnPoint> GetSpawnPoints(string groupFilter)
        {
            if (cachedSpawnPoints == null || cachedSpawnPoints.Length == 0)
            {
                return EmptyArray;
            }

            if (string.IsNullOrEmpty(groupFilter))
            {
                return cachedSpawnPoints;
            }

            List<RoomEncounterSpawnPoint> matched = new List<RoomEncounterSpawnPoint>(cachedSpawnPoints.Length);
            for (int i = 0; i < cachedSpawnPoints.Length; i++)
            {
                RoomEncounterSpawnPoint point = cachedSpawnPoints[i];
                if (point != null && point.GroupTag == groupFilter)
                {
                    matched.Add(point);
                }
            }

            return matched;
        }

        [ContextMenu("Refresh Spawn Points")]
        public void RefreshSpawnPoints()
        {
            cachedSpawnPoints = GetComponentsInChildren<RoomEncounterSpawnPoint>(includeInactive: true);
        }
    }
}

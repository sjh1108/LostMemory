using System;
using System.Collections;
using System.Collections.Generic;
using LostMemory.Stage;
using LostMemory.Stage.Data;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    // RoomData.Encounter 를 읽어 wave / entry / spawn point 매칭 후 적 prefab 을 Instantiate.
    // 결정적 순차 선택 (modulo). 시드 무작위는 후속 CL.
    // RoomEntryRuntimeController 가 owner — 같은 GameObject 에 붙여 사용한다.
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Enemy Encounter Spawner")]
    public sealed class EnemyEncounterSpawner : MonoBehaviour
    {
        public event Action<EnemySpawnedPayload> Spawned;
        public event Action<WaveSpawnedPayload> WaveCompleted;

        private readonly List<GameObject> spawnedEnemies = new List<GameObject>();

        public IReadOnlyList<GameObject> SpawnedEnemies => spawnedEnemies;

        public void Begin(string roomId, RoomEncounterSpec spec, RoomEncounterAnchor anchor, EnemyCatalog catalog)
        {
            if (spec == null || anchor == null || catalog == null)
            {
                return;
            }

            IReadOnlyList<RoomEncounterWave> waves = spec.Waves;
            for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                RoomEncounterWave wave = waves[waveIndex];
                if (wave == null)
                {
                    continue;
                }

                StartCoroutine(RunWave(roomId, waveIndex, wave, anchor, catalog));
            }
        }

        private IEnumerator RunWave(
            string roomId,
            int waveIndex,
            RoomEncounterWave wave,
            RoomEncounterAnchor anchor,
            EnemyCatalog catalog)
        {
            if (wave.StartDelay > 0f)
            {
                yield return new WaitForSeconds(wave.StartDelay);
            }

            int spawnedCount = 0;
            RoomEncounterEnemyEntry[] entries = wave.Enemies;
            for (int i = 0; i < entries.Length; i++)
            {
                RoomEncounterEnemyEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (!catalog.TryGetPrefab(entry.EnemyId, out GameObject prefab))
                {
                    Debug.LogWarning(
                        $"[EnemyEncounterSpawner] enemyId '{entry.EnemyId}' not found in catalog. Skip {entry.Count} spawn(s) for room '{roomId}' wave {waveIndex}.",
                        this);
                    continue;
                }

                IReadOnlyList<RoomEncounterSpawnPoint> candidates = anchor.GetSpawnPoints(entry.SpawnGroupFilter);
                if (candidates.Count == 0)
                {
                    Debug.LogWarning(
                        $"[EnemyEncounterSpawner] no spawn points match filter '{entry.SpawnGroupFilter}' in room '{roomId}'. Skip {entry.Count} spawn(s).",
                        this);
                    continue;
                }

                for (int j = 0; j < entry.Count; j++)
                {
                    int candidateIndex = PickSpawnIndex(candidates.Count, j);
                    RoomEncounterSpawnPoint point = candidates[candidateIndex];
                    if (point == null)
                    {
                        continue;
                    }

                    GameObject instance = Instantiate(prefab, point.transform.position, Quaternion.identity);
                    HardenSpawnedInstance(instance);
                    spawnedEnemies.Add(instance);
                    spawnedCount++;
                    Spawned?.Invoke(new EnemySpawnedPayload(roomId, waveIndex, instance));

                    // 같은 프레임에 여러 적이 동시에 init 되면 일부 적의 init (특히 ranged 무기 attach) 이 깨지는 현상 관찰.
                    // entry 별 1프레임 양보로 회피. spawn 합 N마리면 약 N프레임 (~0.02s × N) 지연.
                    yield return null;
                }
            }

            WaveCompleted?.Invoke(new WaveSpawnedPayload(roomId, waveIndex, spawnedCount));
        }

        // 적 prefab 의 Health 가 DestroyOnDeath = false 로 셋업된 경우, 죽어도 GameObject 가 남아
        // sprite 가 서 있는 채로 보이거나 풀 사이클로 재활용되며 좀비 부활하는 현상이 관찰됨.
        // CL-034 spawn 경로에 한해 죽으면 GameObject 통째로 destroy 되도록 강제.
        // 적 prefab 의 원래 lifecycle 의도(풀 재활용 등)와 충돌할 수 있으므로 적 담당자(클라2)와 협의하여
        // 적 prefab 셋업이 정리되면 본 helper 는 제거 가능.
        private static void HardenSpawnedInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Health health = instance.GetComponent<Health>();
            if (health != null)
            {
                health.DestroyOnDeath = true;
            }
        }

        // 후보가 entry.Count 보다 적을 때 modulo 로 순환. EditMode 테스트 가능하도록 static.
        public static int PickSpawnIndex(int candidateCount, int sequence)
        {
            if (candidateCount <= 0)
            {
                return 0;
            }

            int wrapped = sequence % candidateCount;
            return wrapped < 0 ? wrapped + candidateCount : wrapped;
        }
    }
}

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
    //
    // CL-035 변경: Begin 시 모든 wave 의 startDelay 타이머 큐 + wave 별 idempotent 보장.
    //   외부에서 SpawnNextWave() 호출 시 가장 작은 미시작 wave 를 *조기 spawn*.
    //   둘 중 먼저 도착하는 쪽이 spawn — 사망률 트리거(tracker)와 시간 트리거(자체)의 OR 동작.
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Enemy Encounter Spawner")]
    public sealed class EnemyEncounterSpawner : MonoBehaviour
    {
        public event Action<EnemySpawnedPayload> Spawned;
        public event Action<WaveSpawnedPayload> WaveCompleted;

        private readonly List<GameObject> spawnedEnemies = new List<GameObject>();

        private string activeRoomId;
        private RoomEncounterSpec activeSpec;
        private RoomEncounterAnchor activeAnchor;
        private EnemyCatalog activeCatalog;
        private bool[] waveStarted = Array.Empty<bool>();

        public IReadOnlyList<GameObject> SpawnedEnemies => spawnedEnemies;
        public int WaveCount => activeSpec != null ? activeSpec.Waves.Count : 0;

        public void Begin(string roomId, RoomEncounterSpec spec, RoomEncounterAnchor anchor, EnemyCatalog catalog)
        {
            if (spec == null || anchor == null || catalog == null)
            {
                return;
            }

            activeRoomId = roomId;
            activeSpec = spec;
            activeAnchor = anchor;
            activeCatalog = catalog;

            IReadOnlyList<RoomEncounterWave> waves = spec.Waves;
            waveStarted = new bool[waves.Count];

            for (int i = 0; i < waves.Count; i++)
            {
                RoomEncounterWave wave = waves[i];
                if (wave == null)
                {
                    waveStarted[i] = true;
                    continue;
                }

                StartCoroutine(ScheduleWave(i, wave));
            }
        }

        // 외부 강제 트리거. 가장 작은 미시작 wave 1개 즉시 spawn (startDelay 무시).
        // tracker 가 사망률 임계 도달 시 호출.
        public bool SpawnNextWave()
        {
            if (activeSpec == null)
            {
                return false;
            }

            IReadOnlyList<RoomEncounterWave> waves = activeSpec.Waves;
            for (int i = 0; i < waves.Count; i++)
            {
                if (waveStarted[i])
                {
                    continue;
                }

                StartWaveOnce(i, waves[i]);
                return true;
            }

            return false;
        }

        private IEnumerator ScheduleWave(int waveIndex, RoomEncounterWave wave)
        {
            if (wave.StartDelay > 0f)
            {
                yield return new WaitForSeconds(wave.StartDelay);
            }
            StartWaveOnce(waveIndex, wave);
        }

        private void StartWaveOnce(int waveIndex, RoomEncounterWave wave)
        {
            if (waveStarted[waveIndex])
            {
                return;
            }
            waveStarted[waveIndex] = true;
            StartCoroutine(RunWave(activeRoomId, waveIndex, wave, activeAnchor, activeCatalog));
        }

        private IEnumerator RunWave(
            string roomId,
            int waveIndex,
            RoomEncounterWave wave,
            RoomEncounterAnchor anchor,
            EnemyCatalog catalog)
        {
            // startDelay 는 ScheduleWave 가 이미 처리함 (CL-035 변경).
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

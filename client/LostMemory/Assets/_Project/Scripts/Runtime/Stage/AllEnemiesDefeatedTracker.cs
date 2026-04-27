using System;
using System.Collections.Generic;
using LostMemory.Stage.Data;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    /// <summary>
    /// RoomClearConditionType.AllEnemiesDefeated 분기 본체.
    /// 각 spawn 된 적의 Health.OnDeath 를 구독해 wave 별 사망 카운트를 누적.
    ///
    /// 트리거 동작 (모델 B — wave 가 자기 종료 조건 보유):
    /// - 현재 wave 의 TriggerNextWaveDeathRatio 임계 도달 시 OnNextWaveReady(currentIndex+1) 1회 발행.
    ///   (다음 wave 의 시간 기반 startDelay 와 OR — 둘 중 먼저 만족하는 쪽이 spawn. spawner 측이 idempotent.)
    /// - 모든 wave 가 spawn 완료 + 모든 등록 적이 사망하면 OnRoomCleared 1회 발행.
    ///
    /// 호스트 권위: tracker 는 단일 인스턴스 — 멀티플레이 도입 시 호스트만 보유.
    /// Player 사망 처리는 본 CL 비범위 (CL-014 / CL-048 영역).
    /// </summary>
    internal sealed class AllEnemiesDefeatedTracker : IRoomClearConditionTracker
    {
        public event Action<RoomClearedPayload> OnRoomCleared;
        public event Action<int> OnNextWaveReady;

        private RoomData data;
        private readonly Dictionary<int, int> spawnedPerWave = new Dictionary<int, int>();
        private readonly Dictionary<int, int> deathPerWave = new Dictionary<int, int>();
        private readonly Dictionary<GameObject, int> enemyToWave = new Dictionary<GameObject, int>();
        private readonly HashSet<int> nextWaveTriggered = new HashSet<int>();
        private bool allWavesSpawned;
        private bool clearedFired;

        public void Begin(RoomData data)
        {
            this.data = data;

            if (data == null || data.Encounter == null || data.Encounter.Waves.Count == 0)
            {
                // 빈 wave = 즉시 클리어. (위험·결정 보류 § wave count = 0 edge.)
                allWavesSpawned = true;
                FireRoomClearedIfReady();
            }
        }

        public void RegisterEnemy(EnemySpawnedPayload payload)
        {
            GameObject enemy = payload.Enemy;
            if (enemy == null)
            {
                return;
            }

            enemyToWave[enemy] = payload.WaveIndex;

            Health health = enemy.GetComponent<Health>();
            if (health == null)
            {
                Debug.LogWarning(
                    $"[AllEnemiesDefeatedTracker] enemy '{enemy.name}' has no Health component. Death will not be tracked.");
                return;
            }

            GameObject capturedEnemy = enemy;
            health.OnDeath += () => HandleEnemyDeath(capturedEnemy);
        }

        public void NotifyWaveSpawned(WaveSpawnedPayload payload)
        {
            spawnedPerWave[payload.WaveIndex] = payload.SpawnedCount;

            if (data != null && data.Encounter != null && payload.WaveIndex == data.Encounter.Waves.Count - 1)
            {
                allWavesSpawned = true;
                FireRoomClearedIfReady();
            }
        }

        private void HandleEnemyDeath(GameObject enemy)
        {
            if (enemy == null || !enemyToWave.TryGetValue(enemy, out int waveIndex))
            {
                return;
            }

            if (!deathPerWave.ContainsKey(waveIndex))
            {
                deathPerWave[waveIndex] = 0;
            }
            deathPerWave[waveIndex]++;

            CheckNextWaveTrigger(waveIndex);
            FireRoomClearedIfReady();
        }

        // 모델 B: 현재 wave (sourceWaveIndex) 의 TriggerNextWaveDeathRatio 가 도달하면 다음 wave spawn 신호.
        // sourceWaveIndex 의 적이 죽을 때마다 호출.
        private void CheckNextWaveTrigger(int sourceWaveIndex)
        {
            if (data == null || data.Encounter == null)
            {
                return;
            }
            if (nextWaveTriggered.Contains(sourceWaveIndex))
            {
                return;
            }

            IReadOnlyList<RoomEncounterWave> waves = data.Encounter.Waves;
            int nextWaveIndex = sourceWaveIndex + 1;
            if (nextWaveIndex >= waves.Count)
            {
                return;
            }

            RoomEncounterWave currentWave = waves[sourceWaveIndex];
            if (currentWave == null)
            {
                return;
            }

            float ratio = currentWave.TriggerNextWaveDeathRatio;
            if (ratio <= 0f)
            {
                return;
            }

            if (!spawnedPerWave.TryGetValue(sourceWaveIndex, out int spawned) || spawned == 0)
            {
                return;
            }

            int deaths = deathPerWave.TryGetValue(sourceWaveIndex, out int d) ? d : 0;
            float current = deaths / (float)spawned;
            if (current + Mathf.Epsilon >= ratio)
            {
                nextWaveTriggered.Add(sourceWaveIndex);
                OnNextWaveReady?.Invoke(nextWaveIndex);
            }
        }

        private void FireRoomClearedIfReady()
        {
            if (clearedFired)
            {
                return;
            }
            if (!allWavesSpawned)
            {
                return;
            }

            int totalSpawned = 0;
            foreach (KeyValuePair<int, int> kv in spawnedPerWave)
            {
                totalSpawned += kv.Value;
            }

            int totalDeaths = 0;
            foreach (KeyValuePair<int, int> kv in deathPerWave)
            {
                totalDeaths += kv.Value;
            }

            if (totalSpawned == 0 || totalSpawned == totalDeaths)
            {
                clearedFired = true;
                OnRoomCleared?.Invoke(new RoomClearedPayload(data?.RoomId ?? string.Empty, data));
            }
        }
    }
}

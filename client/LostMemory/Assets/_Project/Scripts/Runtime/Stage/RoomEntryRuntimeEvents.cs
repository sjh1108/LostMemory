using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    // RoomEntryRuntimeController 가 발행하는 시그널 payload 정의.
    // CL-035 (적 전멸 클리어 판정) 와 CL-036 (문 열림·다음 방 전환) 가 이 시그널을 구독한다.
    // 신규 시그널을 추가할 때는 본 파일에 모은다.

    public readonly struct RoomEnteredPayload
    {
        public RoomEnteredPayload(string roomId, Character initiator)
        {
            RoomId = roomId;
            Initiator = initiator;
        }

        public string RoomId { get; }
        public Character Initiator { get; }
    }

    public readonly struct RoomCombatStartedPayload
    {
        public RoomCombatStartedPayload(string roomId, int waveCount)
        {
            RoomId = roomId;
            WaveCount = waveCount;
        }

        public string RoomId { get; }
        public int WaveCount { get; }
    }

    public readonly struct EnemySpawnedPayload
    {
        public EnemySpawnedPayload(string roomId, int waveIndex, GameObject enemy)
        {
            RoomId = roomId;
            WaveIndex = waveIndex;
            Enemy = enemy;
        }

        public string RoomId { get; }
        public int WaveIndex { get; }
        public GameObject Enemy { get; }
    }

    public readonly struct WaveSpawnedPayload
    {
        public WaveSpawnedPayload(string roomId, int waveIndex, int spawnedCount)
        {
            RoomId = roomId;
            WaveIndex = waveIndex;
            SpawnedCount = spawnedCount;
        }

        public string RoomId { get; }
        public int WaveIndex { get; }
        public int SpawnedCount { get; }
    }

    public readonly struct ExitDoorsLockRequestPayload
    {
        public ExitDoorsLockRequestPayload(string roomId)
        {
            RoomId = roomId;
        }

        public string RoomId { get; }
    }
}

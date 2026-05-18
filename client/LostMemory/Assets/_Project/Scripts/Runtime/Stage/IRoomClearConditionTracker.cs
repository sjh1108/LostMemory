using System;
using LostMemory.Stage.Data;

namespace LostMemory.Stage
{
    // 방 클리어 판정 추적 인터페이스. CL-035 가 본체를 채우고, RoomClearConditionType 분기별로 구현체가 선택된다.
    // RoomEntryRuntimeController 가 이벤트로만 통신 — tracker 는 spawner / controller 직접 참조하지 않는다.
    public interface IRoomClearConditionTracker
    {
        // 모든 wave 가 spawn 완료 + 모든 등록 적이 사망했을 때 1회 발행.
        event Action<RoomClearedPayload> OnRoomCleared;

        // 다음 wave 의 사망률 임계가 도달했을 때 발행. controller 가 받아 spawner.SpawnNextWave() 호출.
        // payload = 다음에 spawn 할 wave index.
        event Action<int> OnNextWaveReady;

        void Begin(RoomData data);

        // spawner 의 Spawned 이벤트마다 controller 가 forward.
        void RegisterEnemy(EnemySpawnedPayload payload);

        // spawner 의 WaveCompleted 이벤트마다 controller 가 forward.
        void NotifyWaveSpawned(WaveSpawnedPayload payload);
    }
}

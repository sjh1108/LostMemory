using System;
using LostMemory.Stage.Data;

namespace LostMemory.Stage
{
    // RoomClearConditionType.InteractionComplete / Custom 분기 또는 검증 fallback 으로 사용.
    // 이벤트 발행 없음 — *적 사망과 무관한 클리어 조건* 구현체가 도착하기 전 자리만 잡는다.
    internal sealed class StubRoomClearConditionTracker : IRoomClearConditionTracker
    {
        public event Action<RoomClearedPayload> OnRoomCleared
        {
            add { }
            remove { }
        }

        public event Action<int> OnNextWaveReady
        {
            add { }
            remove { }
        }

        public void Begin(RoomData data)
        {
        }

        public void RegisterEnemy(EnemySpawnedPayload payload)
        {
        }

        public void NotifyWaveSpawned(WaveSpawnedPayload payload)
        {
        }
    }
}

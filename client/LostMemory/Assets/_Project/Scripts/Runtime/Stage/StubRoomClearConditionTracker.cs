using System;
using LostMemory.Stage.Data;
using UnityEngine;

namespace LostMemory.Stage
{
    // CL-034 검증용 더미. 클리어 이벤트를 절대 발행하지 않는다.
    // CL-035 가 RoomClearConditionType 별 실제 구현체로 교체.
    internal sealed class StubRoomClearConditionTracker : IRoomClearConditionTracker
    {
        public event Action OnRoomCleared
        {
            add { }
            remove { }
        }

        public void Begin(RoomData data)
        {
        }

        public void RegisterEnemy(GameObject enemy)
        {
        }
    }
}

using System;
using LostMemory.Stage.Data;
using UnityEngine;

namespace LostMemory.Stage
{
    // 방 클리어 판정 추적 인터페이스. CL-034 가 시그니처만 노출하고, CL-035 가 본체를 채운다.
    // RoomEntryRuntimeController 가 RoomData.ClearCondition 에 따라 적절한 구현체를 선택한다.
    public interface IRoomClearConditionTracker
    {
        event Action OnRoomCleared;

        void Begin(RoomData data);

        void RegisterEnemy(GameObject enemy);
    }
}

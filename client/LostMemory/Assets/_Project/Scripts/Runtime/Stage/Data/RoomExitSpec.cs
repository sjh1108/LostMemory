using System;
using UnityEngine;

namespace LostMemory.Stage.Data
{
    // CL-036 에서 문 위치/전환 조건을 채운다.
    [Serializable]
    public sealed class RoomExitSpec
    {
        [SerializeField] private string nextRoomId = string.Empty;

        public string NextRoomId => nextRoomId;
    }
}

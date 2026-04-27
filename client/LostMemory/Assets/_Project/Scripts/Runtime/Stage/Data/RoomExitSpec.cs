using System;
using UnityEngine;

namespace LostMemory.Stage.Data
{
    // CL-036 에서 출구 위치/전환 조건을 채운다.
    [Serializable]
    public sealed class RoomExitSpec
    {
        [SerializeField, Tooltip("layout prefab 의 출구 GameObject (RoomExitWall) 를 식별하는 태그. RoomEntryAnchor 의 anchorTag 와 같은 결.")]
        private string exitAnchorTag = string.Empty;

        [SerializeField, Tooltip("이 출구를 통해 이동할 다음 방의 RoomData id. 본 CL 에서는 읽지 않음 — 후속 procedural / 미니맵 CL 에서 사용.")]
        private string nextRoomId = string.Empty;

        public string ExitAnchorTag => exitAnchorTag;
        public string NextRoomId => nextRoomId;
    }
}

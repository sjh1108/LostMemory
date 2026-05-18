using System;
using UnityEngine;

namespace LostMemory.Stage.Data
{
    // 방 진입 시 적용할 정적 컨텍스트.
    // 모든 필드는 옵션 — 빈 문자열 / Vector2.zero / false 면 해당 단계는 건너뛴다.
    // CL-034 의 RoomEntryRuntimeController 가 본 데이터를 읽어 적용한다.
    [Serializable]
    public sealed class RoomInitContextSpec
    {
        [SerializeField] private string playerSpawnAnchorTag = string.Empty;
        [SerializeField] private Vector2 facing;
        [SerializeField] private string bgmCueId = string.Empty;
        [SerializeField] private bool lockExitDoors = true;

        public string PlayerSpawnAnchorTag => playerSpawnAnchorTag;
        public Vector2 Facing => facing;
        public string BgmCueId => bgmCueId;
        public bool LockExitDoors => lockExitDoors;
    }
}

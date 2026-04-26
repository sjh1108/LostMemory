using System;
using UnityEngine;

namespace LostMemory.Stage
{
    [Serializable]
    public sealed class StageRoomProgress
    {
        [SerializeField] private string roomId = string.Empty;
        [SerializeField] private StageRoomType roomType = StageRoomType.Combat;
        [SerializeField] private BossEntryRequirementMode bossEntryRequirementMode = BossEntryRequirementMode.Auto;
        [SerializeField] private bool isVisited;
        [SerializeField] private bool isCompleted;

        public string RoomId => roomId;
        public StageRoomType RoomType => roomType;
        public BossEntryRequirementMode BossEntryRequirementMode => bossEntryRequirementMode;
        public bool IsVisited => isVisited;
        public bool IsCompleted => isCompleted;

        public bool CountsAsBossRequirement
        {
            get
            {
                switch (bossEntryRequirementMode)
                {
                    case BossEntryRequirementMode.Required:
                        return true;
                    case BossEntryRequirementMode.Ignored:
                        return false;
                    default:
                        return roomType == StageRoomType.Combat;
                }
            }
        }

        public void Configure(
            string id,
            StageRoomType type,
            BossEntryRequirementMode requirementMode = BossEntryRequirementMode.Auto)
        {
            roomId = id ?? string.Empty;
            roomType = type;
            bossEntryRequirementMode = requirementMode;
        }

        public bool MarkVisited()
        {
            if (isVisited)
            {
                return false;
            }

            isVisited = true;
            return true;
        }

        public bool MarkCompleted()
        {
            bool changed = !isVisited || !isCompleted;
            isVisited = true;
            isCompleted = true;
            return changed;
        }

        public bool ResetProgress()
        {
            if (!isVisited && !isCompleted)
            {
                return false;
            }

            isVisited = false;
            isCompleted = false;
            return true;
        }
    }
}

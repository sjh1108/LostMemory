using System.Collections.Generic;

namespace LostMemory.Stage
{
    public static class BossRoomEntryConditionCalculator
    {
        public static BossRoomEntryConditionResult Evaluate(IReadOnlyList<StageRoomProgress> roomSequence)
        {
            bool hasBossRoom = false;
            int requiredRoomCount = 0;
            int completedRequiredRoomCount = 0;
            int firstIncompleteRoomSequenceIndex = -1;
            string firstIncompleteRoomId = string.Empty;

            if (roomSequence != null)
            {
                for (int i = 0; i < roomSequence.Count; i++)
                {
                    StageRoomProgress room = roomSequence[i];
                    if (room == null)
                    {
                        continue;
                    }

                    if (room.RoomType == StageRoomType.Boss)
                    {
                        hasBossRoom = true;
                        break;
                    }

                    if (!room.CountsAsBossRequirement)
                    {
                        continue;
                    }

                    requiredRoomCount++;

                    if (room.IsCompleted)
                    {
                        completedRequiredRoomCount++;
                        continue;
                    }

                    if (firstIncompleteRoomSequenceIndex < 0)
                    {
                        firstIncompleteRoomSequenceIndex = i;
                        firstIncompleteRoomId = room.RoomId;
                    }
                }
            }

            int remainingRequiredRoomCount = requiredRoomCount - completedRequiredRoomCount;
            bool hasConfiguredRequirements = requiredRoomCount > 0;

            return new BossRoomEntryConditionResult(
                hasBossRoom,
                hasConfiguredRequirements,
                requiredRoomCount,
                completedRequiredRoomCount,
                remainingRequiredRoomCount,
                firstIncompleteRoomSequenceIndex,
                firstIncompleteRoomId);
        }
    }
}

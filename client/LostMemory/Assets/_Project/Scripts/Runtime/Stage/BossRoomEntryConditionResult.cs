namespace LostMemory.Stage
{
    public readonly struct BossRoomEntryConditionResult
    {
        public BossRoomEntryConditionResult(
            bool hasBossRoom,
            bool hasConfiguredRequirements,
            int requiredRoomCount,
            int completedRequiredRoomCount,
            int remainingRequiredRoomCount,
            int firstIncompleteRoomSequenceIndex,
            string firstIncompleteRoomId)
        {
            HasBossRoom = hasBossRoom;
            HasConfiguredRequirements = hasConfiguredRequirements;
            RequiredRoomCount = requiredRoomCount;
            CompletedRequiredRoomCount = completedRequiredRoomCount;
            RemainingRequiredRoomCount = remainingRequiredRoomCount;
            FirstIncompleteRoomSequenceIndex = firstIncompleteRoomSequenceIndex;
            FirstIncompleteRoomId = firstIncompleteRoomId ?? string.Empty;
        }

        public bool HasBossRoom { get; }
        public bool HasConfiguredRequirements { get; }
        public int RequiredRoomCount { get; }
        public int CompletedRequiredRoomCount { get; }
        public int RemainingRequiredRoomCount { get; }
        public int FirstIncompleteRoomSequenceIndex { get; }
        public string FirstIncompleteRoomId { get; }

        public bool CanEnterBossRoom => HasBossRoom && HasConfiguredRequirements && RemainingRequiredRoomCount == 0;
    }
}

using MoreMountains.TopDownEngine;

namespace LostMemory.Stage
{
    public readonly struct BossRoomEntryTransitionRequest
    {
        public BossRoomEntryTransitionRequest(
            BossRoomDoorController controller,
            Character initiator,
            string bossRoomId,
            string bossEntryPointId,
            string bossSceneName,
            int readyPlayerCount,
            int eligiblePlayerCount)
        {
            Controller = controller;
            Initiator = initiator;
            BossRoomId = bossRoomId ?? string.Empty;
            BossEntryPointId = bossEntryPointId ?? string.Empty;
            BossSceneName = bossSceneName ?? string.Empty;
            ReadyPlayerCount = readyPlayerCount;
            EligiblePlayerCount = eligiblePlayerCount;
        }

        public BossRoomDoorController Controller { get; }
        public Character Initiator { get; }
        public string BossRoomId { get; }
        public string BossEntryPointId { get; }
        public string BossSceneName { get; }
        public int ReadyPlayerCount { get; }
        public int EligiblePlayerCount { get; }
    }
}

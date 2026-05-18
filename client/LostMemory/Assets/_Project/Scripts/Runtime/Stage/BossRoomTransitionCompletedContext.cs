using MoreMountains.TopDownEngine;

namespace LostMemory.Stage
{
    public readonly struct BossRoomTransitionCompletedContext
    {
        public BossRoomTransitionCompletedContext(
            BossRoomEntryTransitionRequest request,
            BossRoomEntryPoint entryPoint,
            Character[] players)
        {
            Request = request;
            EntryPoint = entryPoint;
            Players = players ?? System.Array.Empty<Character>();
        }

        public BossRoomEntryTransitionRequest Request { get; }
        public BossRoomEntryPoint EntryPoint { get; }
        public Character[] Players { get; }
    }
}

using MoreMountains.TopDownEngine;

namespace LostMemory.Stage
{
    public sealed class BossRoomEntryParticipantState
    {
        public Character Character { get; private set; }
        public string PlayerId { get; private set; } = string.Empty;
        public bool IsEligible { get; internal set; }
        public bool IsInsideInteractionZone { get; internal set; }
        public bool IsReady { get; internal set; }
        public bool IsUnavailable { get; internal set; }

        public BossRoomEntryParticipantState(Character character)
        {
            BindCharacter(character);
        }

        public void BindCharacter(Character character)
        {
            Character = character;
            PlayerId = character != null ? character.PlayerID ?? string.Empty : string.Empty;
        }
    }
}

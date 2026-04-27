using System;

namespace LostMemory.Stage
{
    public interface IBossIntroDialoguePlayer
    {
        void Play(
            BossIntroSequenceData sequenceData,
            BossRoomTransitionCompletedContext context,
            Action onCompleted);
    }
}

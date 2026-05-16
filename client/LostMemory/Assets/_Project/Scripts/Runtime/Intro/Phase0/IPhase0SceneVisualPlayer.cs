using System;

namespace LostMemory.Intro.Phase0
{
    public interface IPhase0SceneVisualPlayer
    {
        void Play(string sceneVisualId, Action onCompleted);
    }
}

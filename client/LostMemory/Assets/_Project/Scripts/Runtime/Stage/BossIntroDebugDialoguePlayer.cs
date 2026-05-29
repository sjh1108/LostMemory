using System;
using System.Collections;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Boss Intro Debug Dialogue Player")]
    public sealed class BossIntroDebugDialoguePlayer : MonoBehaviour, IBossIntroDialoguePlayer
    {
        [SerializeField, Min(0f)] private float simulatedDialogueDuration = 1.5f;
        [SerializeField] private bool debugLogging = false;

        private Coroutine _playRoutine;

        public void Play(
            BossIntroSequenceData sequenceData,
            BossRoomTransitionCompletedContext context,
            Action onCompleted)
        {
            if (_playRoutine != null)
            {
                StopCoroutine(_playRoutine);
            }

            _playRoutine = StartCoroutine(PlayRoutine(sequenceData, context, onCompleted));
        }

        private IEnumerator PlayRoutine(
            BossIntroSequenceData sequenceData,
            BossRoomTransitionCompletedContext context,
            Action onCompleted)
        {
            string[] cueIds = sequenceData != null ? sequenceData.DialogueCueIds : System.Array.Empty<string>();
            if (debugLogging)
            {
                string cueSummary = cueIds.Length > 0 ? string.Join(", ", cueIds) : "<none>";
                Debug.Log(
                    "[BossIntroDebugDialogue] room=" + context.Request.BossRoomId +
                    ", cues=" + cueSummary,
                    this);
            }

            if (simulatedDialogueDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(simulatedDialogueDuration);
            }

            _playRoutine = null;
            onCompleted?.Invoke();
        }

        private void OnDisable()
        {
            if (_playRoutine != null)
            {
                StopCoroutine(_playRoutine);
                _playRoutine = null;
            }
        }
    }
}

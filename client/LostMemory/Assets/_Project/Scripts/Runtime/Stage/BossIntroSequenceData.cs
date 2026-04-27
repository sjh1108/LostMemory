using UnityEngine;

namespace LostMemory.Stage
{
    /// <summary>
    /// 보스방 인트로의 고정 길이와 대사 키를 외부 데이터로 분리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "BossIntroSequenceData_New", menuName = "LostMemory/Stage/BossIntroSequenceData")]
    public sealed class BossIntroSequenceData : ScriptableObject
    {
        [Header("Flow")]
        [SerializeField] private bool lockPlayersDuringIntro = true;
        [SerializeField] private bool unlockPlayersOnComplete = true;
        [SerializeField] private bool hideBossBeforeEntry;
        [SerializeField, Min(0f)] private float delayBeforeEntry;
        [SerializeField] private bool playEntryAnimation = true;
        [SerializeField, Min(0f)] private float entryDuration = 0.9f;
        [SerializeField, Min(0f)] private float delayBeforeDialogue = 0.2f;
        [SerializeField, Min(0f)] private float delayAfterDialogue = 0.2f;

        [Header("Dialogue")]
        [SerializeField] private bool playDialogue;
        [SerializeField] private string[] dialogueCueIds = System.Array.Empty<string>();

        public bool LockPlayersDuringIntro => lockPlayersDuringIntro;
        public bool UnlockPlayersOnComplete => unlockPlayersOnComplete;
        public bool HideBossBeforeEntry => hideBossBeforeEntry;
        public float DelayBeforeEntry => delayBeforeEntry;
        public bool PlayEntryAnimation => playEntryAnimation;
        public float EntryDuration => entryDuration;
        public float DelayBeforeDialogue => delayBeforeDialogue;
        public float DelayAfterDialogue => delayAfterDialogue;
        public bool PlayDialogue => playDialogue;
        public string[] DialogueCueIds => dialogueCueIds ?? System.Array.Empty<string>();
        public bool HasDialogueCueIds => dialogueCueIds != null && dialogueCueIds.Length > 0;
        public bool ShouldPlayDialogue => playDialogue && HasDialogueCueIds;
    }
}

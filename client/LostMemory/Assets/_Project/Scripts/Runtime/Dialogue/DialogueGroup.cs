using System.Collections.Generic;

namespace LostMemory.Dialogue
{
    /// <summary>
    /// 같은 prefix 를 공유하는 DialogueLine 들의 순서 있는 묶음.
    /// 예: groupId = "boss_intro" → Lines = [boss_intro_1, boss_intro_2, boss_intro_3]
    /// Order 오름차순으로 정렬된 상태로 만들어진다 (DialogueDatabase 책임).
    /// </summary>
    public sealed class DialogueGroup
    {
        public string GroupId { get; }
        public IReadOnlyList<DialogueLine> Lines { get; }
        public int Count => Lines.Count;

        public DialogueGroup(string groupId, IReadOnlyList<DialogueLine> lines)
        {
            GroupId = groupId;
            Lines = lines;
        }

        public DialogueLine this[int index] => Lines[index];
    }
}

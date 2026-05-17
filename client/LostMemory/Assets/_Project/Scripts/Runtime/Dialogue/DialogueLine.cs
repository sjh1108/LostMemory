namespace LostMemory.Dialogue
{
    /// <summary>
    /// CSV 한 행에서 파싱된 단일 대사 라인.
    ///
    /// CSV 컬럼: id, speaker, text, portraitId, isPlayer
    /// - id: 그룹 prefix + "_" + 순서번호. 예: "boss_intro_1", "boss_intro_2"
    /// - speaker: NameBox 에 표시되는 화자명
    /// - text: 대사 본문. 따옴표로 감싸 콤마/줄바꿈 허용 (RFC 4180)
    /// - portraitId: PortraitCatalog 조회 키
    /// - isPlayer: true → 좌측 portrait 활성/강조, false → 우측 portrait 활성/강조
    /// </summary>
    public sealed class DialogueLine
    {
        public string Id { get; }
        public string GroupId { get; }
        public int Order { get; }
        public string Speaker { get; }
        public string Text { get; }
        public string PortraitId { get; }
        public bool IsPlayer { get; }

        public DialogueLine(
            string id,
            string groupId,
            int order,
            string speaker,
            string text,
            string portraitId,
            bool isPlayer)
        {
            Id = id;
            GroupId = groupId;
            Order = order;
            Speaker = speaker;
            Text = text;
            PortraitId = portraitId;
            IsPlayer = isPlayer;
        }

        public override string ToString()
            => $"[{Id}] {Speaker}({(IsPlayer ? "L" : "R")}): {Text}";
    }
}

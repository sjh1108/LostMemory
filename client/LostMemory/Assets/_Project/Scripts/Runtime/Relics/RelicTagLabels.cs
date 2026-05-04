namespace LostMemory.Relics
{
    /// <summary>
    /// CL-138: RelicTag → 한국어 표시명 단일 매퍼.
    /// Shop / Reward / Tooltip 뷰가 동일 매핑을 공유하도록 묶음.
    /// items_draft.md §2 카테고리 분류 표 기준.
    /// </summary>
    public static class RelicTagLabels
    {
        public static string ToKorean(RelicTag tag) => tag switch
        {
            RelicTag.None        => "",
            RelicTag.AttackSpeed => "공속",
            RelicTag.Critical    => "치명타",
            RelicTag.AttackPower => "일반뎀",
            RelicTag.Ice         => "얼음",
            RelicTag.Lightning   => "전기",
            RelicTag.Cooldown    => "쿨감",
            RelicTag.Fire        => "불",
            RelicTag.Wind        => "바람",
            RelicTag.MagicalGirl => "미소녀",
            RelicTag.Health      => "체력",
            RelicTag.Defense     => "방어력",
            RelicTag.Dodge       => "회피",
            RelicTag.Range       => "범위",
            RelicTag.Luck        => "행운",
            RelicTag.Greed       => "탐욕",
            RelicTag.Tarot       => "타로",
            _                    => tag.ToString(),
        };
    }
}

namespace LostMemory.Memory
{
    /// <summary>
    /// 기억 조각(Piece) 해금 시 지급되는 보상의 종류.
    /// MemoryFragmentData.RewardType 에서 참조.
    ///
    /// 설계 원칙: 재능(Talent) 시스템과 역할 분리.
    ///   재능 = 수치 강화 (치명타/공속/방어/마나/체력)
    ///   기억 = 구조 확장 (슬롯·선택지·특수 기능 해금)
    ///
    /// RewardMagnitude 필드 용도 (MemoryFragmentData 참조):
    ///   StatBoost        — % 수치 (0.05 = 5%)
    ///   RelicSlotExpand  — 추가 슬롯 수
    ///   StartingGold     — 런 시작 시 지급 골드량
    ///   ShardDropBonus   — 런당 추가 파편 획득량
    ///   RewardSlotExpand — 보상 선택지 추가 수 (보통 1)
    ///   ShopSlotExpand   — 상점 진열 슬롯 추가 수 (보통 1)
    ///   RunStartRelic    — 미사용 (0)
    ///   ReviveOnce       — 미사용 (0)
    ///   RoomSkip         — 미사용 (0)
    /// </summary>
    public enum MemoryPieceRewardType
    {
        None = 0,

        // ── 수치 보상 ──────────────────────────────────────────

        /// <summary>
        /// 플레이어 스탯을 영구 증가. RewardStat / RewardMagnitude(%) 참조.
        /// 재능과 겹치지 않는 스탯(MoveSpeed 등)에만 사용 권장.
        /// </summary>
        StatBoost,

        // ── 인벤토리·슬롯 확장 ────────────────────────────────

        /// <summary>유물 인벤토리 슬롯 영구 추가. PlayerRelicInventory.AddSlots() 호출.</summary>
        RelicSlotExpand,

        // ── 런 시작 혜택 ──────────────────────────────────────

        /// <summary>런 시작 시 골드 추가 지급. RewardMagnitude = 골드량.</summary>
        StartingGold,

        /// <summary>런 시작 시 랜덤 유물 1개 보유 상태로 시작.</summary>
        RunStartRelic,

        // ── 메타 성장 가속 ────────────────────────────────────

        /// <summary>
        /// 런 종료마다 파편 추가 획득. RewardMagnitude = 런당 추가 파편 수.
        /// MemorySaveData.BonusShardPerRun 에 누적.
        /// </summary>
        ShardDropBonus,

        // ── 런 구조 확장 ──────────────────────────────────────

        /// <summary>
        /// 룸 클리어 보상 선택지 +1. 기본 3택 → 4택 등.
        /// MemorySaveData.BonusRewardSlots 에 누적.
        /// </summary>
        RewardSlotExpand,

        /// <summary>
        /// 상점 진열 슬롯 +1. 더 많은 아이템을 상점에서 선택 가능.
        /// MemorySaveData.BonusShopSlots 에 누적.
        /// </summary>
        ShopSlotExpand,

        // ── 특수 기능 해금 ────────────────────────────────────

        /// <summary>런 중 부활 1회 해금. MemorySaveData.HasRevive = true.</summary>
        ReviveOnce,

        /// <summary>런 중 방 1개 건너뛰기 가능. MemorySaveData.HasRoomSkip = true.</summary>
        RoomSkip,

        /// <summary>보상 등급 상향 확률 추가. RewardMagnitude = 확률 증가량(%).</summary>
        RewardRarityBoost,
    }
}

using System;
using System.Collections.Generic;
using LostMemory.Combat;

namespace LostMemory.Memory
{
    /// <summary>
    /// 기억 시스템의 영구 저장 데이터. JsonUtility 로 직렬화하여 PlayerPrefs 에 보관.
    /// MemoryMetaService 가 단독으로 읽고 쓴다.
    ///
    /// 조각 해금 시 MemoryPieceRewardType 에 따라 아래 필드들이 갱신된다.
    /// 런 시작 시 TalentStartupApplier (또는 MemoryStartupApplier) 가 읽어서 적용.
    /// </summary>
    [Serializable]
    public class MemorySaveData
    {
        // ── 수집 현황 ────────────────────────────────────────────

        /// <summary>현재 보유한 파편(Shard) 수. 런 종료 시 증가, 조각 해금 시 차감.</summary>
        public int AccumulatedShards;

        /// <summary>해금된 조각의 FragmentId 목록. 중복 없음.</summary>
        public List<string> UnlockedPieceIds = new();

        // ── 누적 메타 보너스 (조각 해금 보상 합산) ────────────────

        /// <summary>런당 파편 추가 획득량. ShardDropBonus 보상 합산.</summary>
        public int BonusShardPerRun;

        /// <summary>런 시작 시 추가 지급 골드. StartingGold 보상 합산.</summary>
        public int BonusStartingGold;

        /// <summary>
        /// 유물 인벤토리 영구 추가 슬롯 수. RelicSlotExpand 보상 합산.
        /// 런 시작 시 PlayerRelicInventory.AddSlots() 로 적용.
        /// _bonusSlots 는 게임 재실행 시 초기화되므로 SaveData 에 별도 보관.
        /// </summary>
        public int PermanentBonusRelicSlots;

        /// <summary>룸 보상 선택지 추가 수. RewardSlotExpand 보상 합산. 기본 3택 + 이 값.</summary>
        public int BonusRewardSlots;

        /// <summary>상점 진열 슬롯 추가 수. ShopSlotExpand 보상 합산.</summary>
        public int BonusShopSlots;

        /// <summary>보상 등급 상향 확률 추가(%). RewardRarityBoost 보상 합산.</summary>
        public float BonusRewardRarityPercent;

        /// <summary>런 시작 시 추가 사용 가능한 재능 포인트. TalentPointsBonus 보상 합산.</summary>
        public int BonusTalentPoints;

        /// <summary>런 시작 시 추가 지급할 랜덤 유물 개수. StartingRelicCount 보상 합산.</summary>
        public int BonusStartingRelicCount;

        // ── 특수 기능 해금 플래그 ─────────────────────────────────

        /// <summary>부활 1회 기능 해금 여부. ReviveOnce 보상으로 true 가 된다.</summary>
        public bool HasRevive;

        /// <summary>런 중 방 건너뛰기 기능 해금 여부. RoomSkip 보상으로 true 가 된다.</summary>
        public bool HasRoomSkip;

        /// <summary>런 시작 시 랜덤 유물 1개 지급 기능 해금 여부. RunStartRelic 보상으로 true 가 된다.</summary>
        public bool HasRunStartRelic;

        // ── 영구 스탯 부스트 (StatBoost 보상 기록) ───────────────

        /// <summary>
        /// 조각 해금 StatBoost 보상으로 획득한 영구 스탯 목록.
        /// 런 시작 시 PlayerStatModifierContainer 에 재등록.
        /// </summary>
        public List<MetaStatBoostEntry> PermanentBoosts = new();
    }

    /// <summary>조각 해금 StatBoost 보상 1건의 영구 기록.</summary>
    [Serializable]
    public class MetaStatBoostEntry
    {
        public StatId Stat;
        public float Magnitude;

        /// <summary>어느 조각의 해금 보상인지 추적. 중복 지급 방지.</summary>
        public string SourcePieceId;
    }
}

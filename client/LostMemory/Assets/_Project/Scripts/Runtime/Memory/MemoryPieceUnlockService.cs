using System;
using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.Memory
{
    /// <summary>
    /// 기억 조각(Piece) 해금 트랜잭션을 담당하는 서비스.
    ///
    /// 해금 흐름:
    ///   1. CanUnlock() — 파편 충분 + 미해금 + 이전 조각 해금 완료 검증
    ///   2. TryUnlock() — 파편 차감 → 해금 기록 → ApplyReward() → 저장
    ///   3. ApplyReward() — MemoryPieceRewardType 별 보상 집행
    ///
    /// 보상 집행 정책:
    ///   즉시 적용 : RelicSlotExpand (PlayerRelicInventory.AddSlots)
    ///   저장 후 적용: StatBoost, ShardDropBonus, StartingGold 등
    ///              → MemorySaveData 에 기록 → 런 시작 시 TalentStartupApplier 가 재등록
    ///   플래그 저장: ReviveOnce, RoomSkip, RunStartRelic
    ///              → MemorySaveData 플래그 true → 각 시스템이 런 시작 시 읽어서 활성
    ///
    /// UI(조각 해금 패널) 에서 TryUnlock() 을 호출한다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Memory/Memory Piece Unlock Service")]
    public sealed class MemoryPieceUnlockService : MonoBehaviour
    {
        [SerializeField] private MemoryProgressTracker _tracker;

        [Tooltip("RelicSlotExpand 보상 즉시 적용용. null 이면 저장만 하고 런 시작 시 적용.")]
        [SerializeField] private PlayerRelicInventory _relicInventory;

        [Header("Debug")]
        [SerializeField] private bool _logUnlocks = true;

        /// <summary>조각 해금 성공 시 발화. UI 갱신·연출 트리거용.</summary>
        public event Action<MemoryFragmentData> OnPieceUnlocked;

        // ── 공개 API ─────────────────────────────────────────

        /// <summary>
        /// 해금 가능 여부 검증.
        ///   - 이미 해금된 조각이 아닐 것
        ///   - 이전 순서(Order - 1) 조각이 해금되어 있을 것 (순서 강제)
        ///   - AccumulatedShards >= ShardCost
        /// </summary>
        public bool CanUnlock(MemoryFragmentData piece)
        {
            if (piece == null || _tracker == null) return false;

            MemorySaveData save = _tracker.SaveData;

            // 이미 해금됨
            if (save.UnlockedPieceIds.Contains(piece.FragmentId)) return false;

            // 이전 조각이 해금되어 있는지 확인 (Order > 0 인 경우)
            if (piece.Order > 0 && piece.ParentCanvas != null)
            {
                bool prevUnlocked = false;
                foreach (var frag in piece.ParentCanvas.Fragments)
                {
                    if (frag.Order == piece.Order - 1 &&
                        save.UnlockedPieceIds.Contains(frag.FragmentId))
                    {
                        prevUnlocked = true;
                        break;
                    }
                }
                if (!prevUnlocked) return false;
            }

            // 파편 충분 여부
            return save.AccumulatedShards >= piece.ShardCost;
        }

        /// <summary>
        /// 조각 해금 실행. 성공 시 true, 실패(파편 부족·순서 오류 등) 시 false.
        /// </summary>
        public bool TryUnlock(MemoryFragmentData piece)
        {
            if (!CanUnlock(piece)) return false;

            MemorySaveData save = _tracker.SaveData;

            // 파편 차감
            save.AccumulatedShards -= piece.ShardCost;

            // 해금 기록
            save.UnlockedPieceIds.Add(piece.FragmentId);

            // 보상 적용
            ApplyReward(piece, save);

            // 저장
            MemoryMetaService.Save(save);

            if (_logUnlocks)
            {
                Debug.Log($"[MemoryPieceUnlockService] Unlocked '{piece.DisplayName}'. " +
                          $"Reward={piece.RewardType} Shards={save.AccumulatedShards}", this);
            }

            OnPieceUnlocked?.Invoke(piece);
            return true;
        }

        // ── 보상 집행 ─────────────────────────────────────────

        private void ApplyReward(MemoryFragmentData piece, MemorySaveData save)
        {
            switch (piece.RewardType)
            {
                case MemoryPieceRewardType.None:
                    break;

                // ── 즉시 적용 ──────────────────────────────

                case MemoryPieceRewardType.RelicSlotExpand:
                    int slots = Mathf.Max(1, Mathf.RoundToInt(piece.RewardMagnitude));
                    if (_relicInventory != null)
                        _relicInventory.AddSlots(slots);
                    else
                        Debug.LogWarning("[MemoryPieceUnlockService] RelicInventory 미연결 — 슬롯 즉시 적용 불가.", this);
                    break;

                // ── 저장 후 런 시작 시 적용 ────────────────

                case MemoryPieceRewardType.StatBoost:
                    // PermanentBoosts 에 기록 → TalentStartupApplier 가 런 시작 시 container 에 등록
                    bool alreadyRecorded = false;
                    foreach (var entry in save.PermanentBoosts)
                    {
                        if (entry.SourcePieceId == piece.FragmentId) { alreadyRecorded = true; break; }
                    }
                    if (!alreadyRecorded)
                    {
                        save.PermanentBoosts.Add(new MetaStatBoostEntry
                        {
                            Stat = piece.RewardStat,
                            Magnitude = piece.RewardMagnitude,
                            SourcePieceId = piece.FragmentId,
                        });
                    }
                    break;

                case MemoryPieceRewardType.StartingGold:
                    save.BonusStartingGold += Mathf.RoundToInt(piece.RewardMagnitude);
                    break;

                case MemoryPieceRewardType.ShardDropBonus:
                    save.BonusShardPerRun += Mathf.RoundToInt(piece.RewardMagnitude);
                    break;

                case MemoryPieceRewardType.RewardSlotExpand:
                    save.BonusRewardSlots += Mathf.RoundToInt(piece.RewardMagnitude);
                    break;

                case MemoryPieceRewardType.ShopSlotExpand:
                    save.BonusShopSlots += Mathf.RoundToInt(piece.RewardMagnitude);
                    break;

                case MemoryPieceRewardType.RewardRarityBoost:
                    save.BonusRewardRarityPercent += piece.RewardMagnitude;
                    break;

                // ── 특수 기능 해금 플래그 ──────────────────
                // 실제 효과는 각 시스템(RunManager 등)이 런 시작 시 SaveData 를 읽어서 활성화.

                case MemoryPieceRewardType.ReviveOnce:
                    save.HasRevive = true;
                    break;

                case MemoryPieceRewardType.RoomSkip:
                    save.HasRoomSkip = true;
                    break;

                case MemoryPieceRewardType.RunStartRelic:
                    save.HasRunStartRelic = true;
                    break;

                default:
                    Debug.LogWarning($"[MemoryPieceUnlockService] 처리되지 않은 RewardType: {piece.RewardType}", this);
                    break;
            }
        }

        // ── 디버그 ContextMenu ────────────────────────────────

        [ContextMenu("Debug — Log accumulated shards")]
        private void DebugLogShards()
        {
            if (_tracker == null) { Debug.LogWarning("Tracker 미연결."); return; }
            Debug.Log($"[MemoryPieceUnlockService] AccumulatedShards={_tracker.AccumulatedShards}");
        }
    }
}

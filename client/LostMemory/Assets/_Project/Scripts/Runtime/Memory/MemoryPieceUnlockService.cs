using System;
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
        public static MemoryPieceUnlockService Instance { get; private set; }

        [SerializeField] private MemoryShardWallet _wallet;

        [Header("캔버스 순서 (index 0 = 첫 번째 기억)")]
        [SerializeField] private MemoryData[] _allCanvases;

        // RelicSlotExpand 는 게임 재실행 시 _bonusSlots 가 초기화되므로
        // 즉시 AddSlots() 대신 SaveData.PermanentBonusRelicSlots 에 저장하고
        // 런 시작 시 startup 시스템이 재적용한다.

        [Header("Debug")]
        [SerializeField] private bool _logUnlocks = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

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
            if (piece == null) return false;
            return CanUnlock(piece, ResolveWallet().SaveData);
        }

        private bool CanUnlock(MemoryFragmentData piece, MemorySaveData save)
        {
            if (piece == null || save == null) return false;
            // 이미 해금됨
            if (save.UnlockedPieceIds.Contains(piece.FragmentId)) return false;

            // 이전 캔버스의 조각이 모두 해금되어 있는지 확인
            // (같은 캔버스 내에서는 순서 제한 없이 자유롭게 해금 가능)
            if (_allCanvases != null && piece.ParentCanvas != null)
            {
                int canvasIndex = System.Array.IndexOf(_allCanvases, piece.ParentCanvas);
                if (canvasIndex > 0)
                {
                    MemoryData prevCanvas = _allCanvases[canvasIndex - 1];
                    if (prevCanvas?.Fragments != null)
                    {
                        foreach (var frag in prevCanvas.Fragments)
                        {
                            if (!save.UnlockedPieceIds.Contains(frag.FragmentId))
                                return false;
                        }
                    }
                }
            }

            // 파편 충분 여부
            return save.AccumulatedShards >= piece.ShardCost;
        }

        /// <summary>
        /// 조각 해금 실행. 성공 시 true, 실패(파편 부족·순서 오류 등) 시 false.
        /// </summary>
        public bool TryUnlock(MemoryFragmentData piece)
        {
            MemoryShardWallet wallet = ResolveWallet();
            MemorySaveData save = wallet.SaveData;
            if (!CanUnlock(piece, save)) return false;

            // 파편 차감
            save.AccumulatedShards -= piece.ShardCost;

            // 해금 기록
            save.UnlockedPieceIds.Add(piece.FragmentId);

            // 보상 적용
            ApplyReward(piece, save);

            // 저장
            wallet.SaveAndNotify();

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
                    // SaveData 에 누적 저장. 런 시작 시 startup 시스템이 AddSlots() 로 재적용.
                    // (게임 재실행 시 _bonusSlots 가 초기화되므로 즉시 호출 방식 사용 불가)
                    save.PermanentBonusRelicSlots += Mathf.Max(1, Mathf.RoundToInt(piece.RewardMagnitude));
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
            Debug.Log($"[MemoryPieceUnlockService] AccumulatedShards={ResolveWallet().CurrentShards}");
        }

        private MemoryShardWallet ResolveWallet()
        {
            if (_wallet == null)
            {
                _wallet = MemoryShardWallet.EnsureInstance();
            }

            return _wallet;
        }
    }
}

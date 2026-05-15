using System;
using LostMemory.Stage;
using LostMemory.Stage.Data;
using UnityEngine;

namespace LostMemory.Memory
{
    /// <summary>
    /// 런 중 방 클리어 기록을 추적하고, 런 종료 시 정산 파편을 MemoryShardWallet 에 영구 저장.
    ///
    /// 파편 지급 규칙 (Inspector 에서 수치 조정):
    ///   - 작은 전투방 클리어 → _shardPerSmallRoom
    ///   - 큰 전투방 클리어   → _shardPerLargeRoom
    ///   - 보스방 클리어      → _shardPerBossRoom
    ///   - 런 종료 시 누적 보너스     → MemorySaveData.BonusShardPerRun (조각 해금 보상 합산)
    ///
    /// RunManager 에서 호출하는 진입점:
    ///   - RecordRoomClear()    : 룸 클리어 시 카운트 기록
    ///   - SaveRunShards()      : CleanupRunResultingState() 끝에서 호출 → 영구 저장
    ///   - DiscardRunShards()   : 런 포기 시 이번 런 카운터만 폐기
    ///   - ResetRunShards()     : StartRun() 시 이번 런 카운터 초기화
    ///
    /// GoldWallet 과 동일한 패턴. RunManager 와 같은 GameObject 에 부착 권장.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Memory/Memory Progress Tracker")]
    public sealed class MemoryProgressTracker : MonoBehaviour
    {
        [Header("정산 파편 지급 규칙")]
        [SerializeField, Min(0), Tooltip("작은 전투방 클리어 시 지급 파편 수.")]
        private int _shardPerSmallRoom = 1;

        [SerializeField, Min(0), Tooltip("큰 전투방 클리어 시 지급 파편 수.")]
        private int _shardPerLargeRoom = 1;

        [SerializeField, Min(0), Tooltip("보스방 클리어 시 지급 파편 수.")]
        private int _shardPerBossRoom = 3;

        [Header("Debug")]
        [SerializeField] private bool _logChanges = true;

        private int _smallRoomClearCount;
        private int _largeRoomClearCount;
        private int _bossRoomClearCount;
        private int _manualRunShards;

        /// <summary>
        /// 정산 예정 파편 수 변경 시 발화. 인자 = 이번 런 결과 정산 시 받을 예정 파편.
        /// </summary>
        public event Action<int> ShardsChanged;

        /// <summary>영구 저장 파편 + 이번 런 정산 예정 파편. 기존 참조 호환용.</summary>
        public int TotalShards => AccumulatedShards + ThisRunShards;

        /// <summary>이번 런 결과 정산 시 받을 예정 파편 수. RunResultData 표시용.</summary>
        public int ThisRunShards => CalculatePendingRunShards();

        /// <summary>영구 저장된 파편 수. 조각 해금 UI 에서 참조.</summary>
        public int AccumulatedShards => MemoryShardWallet.EnsureInstance().CurrentShards;

        /// <summary>읽기 전용 저장 데이터 참조. 로비 UI / 해금 UI 에서 참조.</summary>
        public MemorySaveData SaveData => MemoryShardWallet.EnsureInstance().SaveData;

        public int ShardPerSmallRoom => _shardPerSmallRoom;
        public int ShardPerLargeRoom => _shardPerLargeRoom;
        public int ShardPerBossRoom => _shardPerBossRoom;
        public int SmallRoomClearCount => _smallRoomClearCount;
        public int LargeRoomClearCount => _largeRoomClearCount;
        public int BossRoomClearCount => _bossRoomClearCount;

        private void Awake()
        {
            MemoryShardWallet wallet = MemoryShardWallet.EnsureInstance();
            if (_logChanges)
            {
                Debug.Log($"[MemoryProgressTracker] Ready. AccumulatedShards={wallet.CurrentShards}", this);
            }
        }

        // ── 런 중 클리어 기록 ─────────────────────────────────

        /// <summary>
        /// 디버그/이벤트용 수동 정산 파편을 더한다. 즉시 영구 지급하지 않는다.
        /// </summary>
        public void AddShards(int amount)
        {
            if (amount <= 0) return;
            _manualRunShards += amount;
            if (_logChanges)
            {
                Debug.Log($"[MemoryProgressTracker] Add manual pending shards(+{amount}) pending={ThisRunShards}", this);
            }
            ShardsChanged?.Invoke(ThisRunShards);
        }

        /// <summary>
        /// 룸 데이터에 따라 클리어 카운트를 기록한다. RunManager.HandleRoomCleared 에서 호출.
        /// 파편은 즉시 지급하지 않고 런 결과 정산 시 영구 지갑에 합산한다.
        /// </summary>
        public void RecordRoomClear(RoomData roomData)
        {
            if (roomData == null)
            {
                return;
            }

            if (roomData.RoomType == StageRoomType.Boss)
            {
                _bossRoomClearCount++;
            }
            else if (roomData.RoomType == StageRoomType.Combat)
            {
                if (roomData.Category == RoomCategory.LargeRoom)
                {
                    _largeRoomClearCount++;
                }
                else
                {
                    _smallRoomClearCount++;
                }
            }

            if (_logChanges)
            {
                Debug.Log(
                    $"[MemoryProgressTracker] RecordRoomClear type={roomData.RoomType} category={roomData.Category} " +
                    $"small={_smallRoomClearCount} large={_largeRoomClearCount} boss={_bossRoomClearCount} pending={ThisRunShards}",
                    this);
            }

            ShardsChanged?.Invoke(ThisRunShards);
        }

        /// <summary>기존 호출부 호환용. RoomData 를 받을 수 없으면 Combat 은 작은방으로 계산한다.</summary>
        public void AddShardsForRoom(StageRoomType roomType)
        {
            switch (roomType)
            {
                case StageRoomType.Combat:
                    _smallRoomClearCount++;
                    break;
                case StageRoomType.Boss:
                    _bossRoomClearCount++;
                    break;
            }

            ShardsChanged?.Invoke(ThisRunShards);
        }

        // ── 런 경계 처리 ──────────────────────────────────────

        /// <summary>
        /// 런 시작 시 이번 런 카운터 초기화. RunManager.ResetRunResultTracking() 에서 호출.
        /// 영구 저장 데이터는 건드리지 않는다.
        /// </summary>
        public void ResetRunShards()
        {
            _smallRoomClearCount = 0;
            _largeRoomClearCount = 0;
            _bossRoomClearCount = 0;
            _manualRunShards = 0;
            if (_logChanges)
            {
                Debug.Log("[MemoryProgressTracker] ResetRunShards. pending=0", this);
            }
            ShardsChanged?.Invoke(ThisRunShards);
        }

        /// <summary>
        /// 런 종료 시 클리어 카운트 기반 정산 파편 + BonusShardPerRun 을 영구 파편 지갑에 합산하고 저장.
        /// RunManager.CleanupRunResultingState() 끝에서 호출.
        /// </summary>
        public void SaveRunShards()
        {
            MemoryShardWallet wallet = MemoryShardWallet.EnsureInstance();
            int earned = ThisRunShards;
            int bonus = wallet.SaveData.BonusShardPerRun;
            int total = earned + bonus;
            wallet.AddShards(total);

            if (_logChanges)
            {
                Debug.Log($"[MemoryProgressTracker] SaveRunShards. earned={earned} bonus={bonus} -> AccumulatedShards={wallet.CurrentShards}", this);
            }
            ResetRunShards();
        }

        public void DiscardRunShards()
        {
            int discarded = ThisRunShards;
            if (_logChanges)
            {
                Debug.Log($"[MemoryProgressTracker] DiscardRunShards. discarded={discarded}", this);
            }
            ResetRunShards();
        }

        public int CalculatePendingRunShards()
        {
            return _manualRunShards
                   + _smallRoomClearCount * _shardPerSmallRoom
                   + _largeRoomClearCount * _shardPerLargeRoom
                   + _bossRoomClearCount * _shardPerBossRoom;
        }

        // ── 디버그 ContextMenu ────────────────────────────────

        [ContextMenu("Debug — Add 10 shards")]
        private void DebugAdd10()
        {
            AddShards(10);
        }

        [ContextMenu("Debug — Save run shards")]
        private void DebugSave()
        {
            SaveRunShards();
        }

        [ContextMenu("Debug — Delete save data")]
        private void DebugDelete()
        {
            MemoryMetaService.DeleteAll();
            MemoryShardWallet.EnsureInstance().ReloadFromDisk();
            _smallRoomClearCount = 0;
            _largeRoomClearCount = 0;
            _bossRoomClearCount = 0;
            _manualRunShards = 0;
            Debug.Log("[MemoryProgressTracker] Save data deleted.", this);
            ShardsChanged?.Invoke(0);
        }
    }
}

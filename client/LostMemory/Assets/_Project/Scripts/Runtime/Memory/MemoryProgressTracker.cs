using System;
using LostMemory.Stage;
using UnityEngine;

namespace LostMemory.Memory
{
    /// <summary>
    /// 런 중 파편(Shard) 획득을 추적하고, 런 종료 시 MemoryMetaService 로 영구 저장.
    ///
    /// 파편 지급 규칙 (Inspector 에서 수치 조정):
    ///   - 일반 전투방(Combat) 클리어 → _shardPerCombatRoom
    ///   - 보스방(Boss) 클리어        → _shardPerBossRoom
    ///   - 런 종료 시 누적 보너스     → MemorySaveData.BonusShardPerRun (조각 해금 보상 합산)
    ///
    /// RunManager 에서 호출하는 진입점:
    ///   - AddShards(amount)    : 룸 클리어 시 파편 지급
    ///   - SaveRunShards()      : CleanupRunResultingState() 끝에서 호출 → 영구 저장
    ///   - ResetRunShards()     : StartRun() 시 이번 런 카운터 초기화
    ///
    /// GoldWallet 과 동일한 패턴. RunManager 와 같은 GameObject 에 부착 권장.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Memory/Memory Progress Tracker")]
    public sealed class MemoryProgressTracker : MonoBehaviour
    {
        [Header("파편 지급 규칙")]
        [SerializeField, Min(0), Tooltip("일반 전투방 클리어 시 지급 파편 수.")]
        private int _shardPerCombatRoom = 1;

        [SerializeField, Min(0), Tooltip("보스방 클리어 시 지급 파편 수.")]
        private int _shardPerBossRoom = 3;

        [Header("Debug")]
        [SerializeField] private bool _logChanges = true;

        // 영구 저장 데이터. Awake 에서 로드.
        private MemorySaveData _saveData;

        // 이번 런에서 새로 획득한 파편 (런 종료 전까지 임시).
        private int _thisRunShards;

        /// <summary>
        /// 파편 수 변경 시 발화. 인자 = 총 보유 파편(영구 저장 + 이번 런 획득 합산).
        /// UI 에서 구독해 실시간 표시에 사용.
        /// </summary>
        public event Action<int> ShardsChanged;

        /// <summary>총 보유 파편 수 (영구 저장 + 이번 런 획득 합산).</summary>
        public int TotalShards => _saveData.AccumulatedShards + _thisRunShards;

        /// <summary>이번 런에서 획득한 파편 수. RunResultData 표시용.</summary>
        public int ThisRunShards => _thisRunShards;

        /// <summary>영구 저장된 파편 수. 조각 해금 UI 에서 참조.</summary>
        public int AccumulatedShards => _saveData.AccumulatedShards;

        /// <summary>읽기 전용 저장 데이터 참조. 로비 UI / 해금 UI 에서 참조.</summary>
        public MemorySaveData SaveData => _saveData;

        public int ShardPerCombatRoom => _shardPerCombatRoom;
        public int ShardPerBossRoom => _shardPerBossRoom;

        private void Awake()
        {
            _saveData = MemoryMetaService.Load();
            if (_logChanges)
            {
                Debug.Log($"[MemoryProgressTracker] Loaded. AccumulatedShards={_saveData.AccumulatedShards}", this);
            }
        }

        // ── 런 중 파편 지급 ───────────────────────────────────

        /// <summary>
        /// 파편을 지급한다. RunManager 가 룸 클리어 이벤트 수신 시 호출.
        /// amount ≤ 0 이면 무시.
        /// </summary>
        public void AddShards(int amount)
        {
            if (amount <= 0) return;
            _thisRunShards += amount;
            if (_logChanges)
            {
                Debug.Log($"[MemoryProgressTracker] AddShards(+{amount}) thisRun={_thisRunShards} total={TotalShards}", this);
            }
            ShardsChanged?.Invoke(TotalShards);
        }

        /// <summary>
        /// 룸 타입에 따라 규칙 파편을 자동 지급. RunManager.HandleRoomCleared 에서 호출.
        /// </summary>
        public void AddShardsForRoom(StageRoomType roomType)
        {
            switch (roomType)
            {
                case StageRoomType.Combat:
                    AddShards(_shardPerCombatRoom);
                    break;
                case StageRoomType.Boss:
                    AddShards(_shardPerBossRoom);
                    break;
            }
        }

        // ── 런 경계 처리 ──────────────────────────────────────

        /// <summary>
        /// 런 시작 시 이번 런 카운터 초기화. RunManager.ResetRunResultTracking() 에서 호출.
        /// 영구 저장 데이터는 건드리지 않는다.
        /// </summary>
        public void ResetRunShards()
        {
            _thisRunShards = 0;
            if (_logChanges)
            {
                Debug.Log("[MemoryProgressTracker] ResetRunShards. thisRun=0", this);
            }
        }

        /// <summary>
        /// 런 종료 시 이번 런 파편 + BonusShardPerRun 을 AccumulatedShards 에 합산하고 저장.
        /// RunManager.CleanupRunResultingState() 끝에서 호출.
        /// </summary>
        public void SaveRunShards()
        {
            int bonus = _saveData.BonusShardPerRun;
            int total = _thisRunShards + bonus;

            _saveData.AccumulatedShards += total;
            _thisRunShards = 0;

            MemoryMetaService.Save(_saveData);

            if (_logChanges)
            {
                Debug.Log($"[MemoryProgressTracker] SaveRunShards. earned={total - bonus} bonus={bonus} → AccumulatedShards={_saveData.AccumulatedShards}", this);
            }
            ShardsChanged?.Invoke(_saveData.AccumulatedShards);
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
            _saveData = new MemorySaveData();
            _thisRunShards = 0;
            Debug.Log("[MemoryProgressTracker] Save data deleted.", this);
            ShardsChanged?.Invoke(0);
        }
    }
}

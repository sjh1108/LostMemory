using LostMemory.Combat;
using UnityEngine;

namespace LostMemory.Memory
{
    /// <summary>
    /// 기억 조각(Piece) 1개의 설정 데이터. ScriptableObject 로 생성.
    ///
    /// 용어 정리:
    ///   파편(Shard) — 런 중 획득하는 재화(숫자). MemorySaveData.AccumulatedShards 에 누적.
    ///   조각(Piece) — 파편 N개를 소모해서 해금하는 단위. 해금 시 보상 즉시 지급.
    ///   캔버스(Canvas) — 조각들이 모이는 큰 그림. 4개 존재. MemoryData 참조.
    ///
    /// 해금 흐름:
    ///   런 중 파편 획득 → AccumulatedShards 누적
    ///   → 파편 수 >= ShardCost 충족 시 해금 가능
    ///   → 해금 확정 → AccumulatedShards 차감 + RewardType 보상 지급
    /// </summary>
    [CreateAssetMenu(fileName = "MemoryPieceData_New", menuName = "LostMemory/Memory/Piece Data")]
    public class MemoryFragmentData : ScriptableObject
    {
        [Tooltip("저장 키. 전체에서 고유해야 하며 asset 파일명과 일치를 권장.")]
        [SerializeField] private string _fragmentId;

        [SerializeField] private string _displayName;

        [SerializeField] private Sprite _icon;

        [SerializeField, TextArea(2, 4)] private string _description;

        [Header("캔버스 내 위치")]
        [Tooltip("캔버스 내 순서. 0부터 시작. UI 에서 다음 해금 조각 표시에 사용.")]
        [SerializeField] private int _order;

        [Header("해금 비용")]
        [Tooltip("이 조각 해금에 필요한 파편(Shard) 수.")]
        [SerializeField] private int _shardCost;

        [Header("소속 캔버스 (역참조)")]
        [Tooltip("이 조각이 속한 MemoryData(캔버스). 완성 현황 표시에 사용.")]
        [SerializeField] private MemoryData _parentCanvas;

        [Header("해금 보상 (영구 메타)")]
        [SerializeField] private MemoryPieceRewardType _rewardType;

        [Tooltip("StatBoost 타입일 때 증가할 스탯.")]
        [SerializeField] private StatId _rewardStat;

        [Tooltip("보상 수치. StatBoost: % 단위 (0.05 = 5%), RelicSlotExpand: 추가 슬롯 수.")]
        [SerializeField] private float _rewardMagnitude;

        public string FragmentId => _fragmentId;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public string Description => _description;
        public int Order => _order;
        public int ShardCost => _shardCost;
        public MemoryData ParentCanvas => _parentCanvas;
        public MemoryPieceRewardType RewardType => _rewardType;
        public StatId RewardStat => _rewardStat;
        public float RewardMagnitude => _rewardMagnitude;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_fragmentId))
                _fragmentId = name;
        }
#endif
    }
}

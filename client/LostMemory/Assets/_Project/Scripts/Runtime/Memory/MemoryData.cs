using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Memory
{
    /// <summary>
    /// 기억 캔버스(Canvas) 1개의 설정 데이터. ScriptableObject 로 생성. 총 4개 존재.
    ///
    /// 캔버스는 여러 조각(MemoryFragmentData)으로 나뉘어진 큰 그림이다.
    /// 보상은 캔버스 완성이 아니라 조각 1개씩 해금할 때마다 지급된다.
    /// 단계가 높은 캔버스일수록 조각 수가 많고 해금 비용(ShardCost)도 높다.
    /// </summary>
    [CreateAssetMenu(fileName = "MemoryData_New", menuName = "LostMemory/Memory/Canvas Data")]
    public class MemoryData : ScriptableObject
    {
        [Tooltip("저장 키. 전체에서 고유해야 하며 asset 파일명과 일치를 권장.")]
        [SerializeField] private string _memoryId;

        [SerializeField] private string _displayName;

        [SerializeField] private Sprite _artwork;

        [SerializeField, TextArea(3, 6)] private string _loreText;

        [Header("구성 조각 목록 (Order 순으로 배치 권장)")]
        [SerializeField] private MemoryFragmentData[] _fragments;

        public string MemoryId => _memoryId;
        public string DisplayName => _displayName;
        public Sprite Artwork => _artwork;
        public string LoreText => _loreText;
        public IReadOnlyList<MemoryFragmentData> Fragments => _fragments;
        public int TotalPieceCount => _fragments?.Length ?? 0;

        /// <summary>
        /// 해금되지 않은 조각 중 Order 가 가장 낮은 것(다음 해금 대상)을 반환.
        /// unlockedIds 에 없는 것 중 최솟값.
        /// </summary>
        public MemoryFragmentData GetNextLockedPiece(ICollection<string> unlockedIds)
        {
            if (_fragments == null) return null;
            MemoryFragmentData next = null;
            foreach (MemoryFragmentData piece in _fragments)
            {
                if (unlockedIds.Contains(piece.FragmentId)) continue;
                if (next == null || piece.Order < next.Order)
                    next = piece;
            }
            return next;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_memoryId))
                _memoryId = name;
        }
#endif
    }
}

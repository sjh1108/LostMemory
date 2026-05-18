using System;
using TMPro;
using UnityEngine;

namespace LostMemory.Memory.UI
{
    /// <summary>
    /// 한 캔버스(MemoryData_khi)를 나타내는 카드 뷰. (테스트용)
    /// 직소 퍼즐 그리드 + 제목 + 진행 텍스트.
    /// MemoryCollectionPanelView_khi 가 4개를 배열로 관리한다.
    ///
    /// 원본 MemoryCardView 는 손대지 않고 별도 컴포넌트로 격리.
    ///
    /// Inspector 연결 항목:
    ///   _titleText      — 캔버스 이름 표시
    ///   _puzzleView     — 그리드 셀들을 동적으로 생성하는 컴포넌트
    ///   _progressText   — "3 / 4" 형태의 해금 진행 텍스트 (옵션)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Memory/UI/Memory Card View (khi)")]
    public sealed class MemoryCardView_khi : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private MemoryPuzzleView_khi _puzzleView;
        [SerializeField] private TextMeshProUGUI _progressText;

        private MemoryData_khi _builtCanvas;

        /// <summary>셀이 클릭됐을 때 발화. 부모 패널이 확인 모달을 띄운다.</summary>
        public event Action<MemoryFragmentData> OnPieceClicked;

        private void Awake()
        {
            if (_puzzleView != null)
                _puzzleView.OnPieceClicked += HandlePieceClicked;
        }

        private void OnDestroy()
        {
            if (_puzzleView != null)
                _puzzleView.OnPieceClicked -= HandlePieceClicked;
        }

        /// <summary>
        /// 카드를 초기화/갱신한다. MemoryCollectionPanelView_khi.Refresh() 에서 호출.
        /// 같은 canvas 면 그리드는 재사용하고 상태만 Refresh.
        /// </summary>
        public void Init(MemoryData_khi canvas, MemorySaveData save, string animatedPieceId = null)
        {
            if (canvas == null) return;

            if (_titleText != null)
                _titleText.text = canvas.DisplayName;

            if (_puzzleView != null)
            {
                if (_builtCanvas != canvas)
                {
                    _puzzleView.BuildGrid(canvas);
                    _builtCanvas = canvas;
                }
                _puzzleView.Refresh(save, animatedPieceId);
            }

            if (_progressText != null)
            {
                int unlocked = 0;
                int total = canvas.TotalPieceCount;
                foreach (var piece in canvas.Fragments)
                {
                    if (piece != null && save.UnlockedPieceIds.Contains(piece.FragmentId))
                        unlocked++;
                }
                _progressText.text = $"{unlocked} / {total}";
            }
        }

        private void HandlePieceClicked(MemoryFragmentData piece)
        {
            OnPieceClicked?.Invoke(piece);
        }
    }
}

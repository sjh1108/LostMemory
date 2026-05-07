using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Memory.UI
{
    /// <summary>
    /// 기억 캔버스(Canvas) 1개를 나타내는 카드 뷰.
    /// MemoryCollectionPanelView 가 4개를 배열로 관리한다.
    ///
    /// Inspector 연결 항목:
    ///   _titleText      — 캔버스 이름 표시
    ///   _artwork        — 캔버스 대표 이미지
    ///   _pieceIcons[]   — 조각 수만큼 배치된 아이콘 Image 배열 (Order 순)
    ///   _lockedOverlays[] — 각 아이콘 위에 얹는 잠금 오버레이 Image 배열 (해금 시 비활성)
    ///   _progressText   — "3 / 5" 형태의 해금 진행 텍스트
    /// </summary>
    public sealed class MemoryCardView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Image _artwork;
        [SerializeField] private Image[] _pieceIcons;
        [SerializeField] private GameObject[] _lockedOverlays;
        [SerializeField] private TextMeshProUGUI _progressText;

        /// <summary>
        /// 카드를 초기화한다. MemoryCollectionPanelView.Open() 에서 호출.
        /// </summary>
        /// <param name="canvas">표시할 캔버스 데이터</param>
        /// <param name="save">현재 저장 데이터 (해금 여부 판별용)</param>
        public void Init(MemoryData canvas, MemorySaveData save)
        {
            if (_titleText != null)
                _titleText.text = canvas.DisplayName;

            if (_artwork != null)
            {
                _artwork.sprite = canvas.Artwork;
                _artwork.enabled = canvas.Artwork != null;
            }

            int unlockedCount = 0;
            int total = canvas.TotalPieceCount;

            for (int i = 0; i < canvas.Fragments.Count; i++)
            {
                var piece = canvas.Fragments[i];
                bool unlocked = save.UnlockedPieceIds.Contains(piece.FragmentId);
                if (unlocked) unlockedCount++;

                if (i < _pieceIcons.Length && _pieceIcons[i] != null)
                {
                    _pieceIcons[i].sprite = piece.Icon;
                    _pieceIcons[i].enabled = piece.Icon != null;
                }

                if (i < _lockedOverlays.Length && _lockedOverlays[i] != null)
                    _lockedOverlays[i].SetActive(!unlocked);
            }

            if (_progressText != null)
                _progressText.text = $"{unlockedCount} / {total}";
        }
    }
}

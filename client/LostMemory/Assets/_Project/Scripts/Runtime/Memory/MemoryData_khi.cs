using UnityEngine;

namespace LostMemory.Memory
{
    /// <summary>
    /// MemoryData 의 테스트용 확장. 퍼즐 그리드(직소 분할) 시각화 검증용.
    /// 원본 MemoryData 클래스는 손대지 않기 위해 상속으로 추가 필드만 얹는다.
    ///
    /// 추가 필드:
    ///   GridWidth, GridHeight — Artwork 를 등분할 그리드 차원
    ///
    /// 사용처:
    ///   - MemoryPuzzleView_khi : Artwork 를 GridWidth × GridHeight 셀로 슬라이싱
    ///   - MemoryCardView_khi   : 카드 한 장이 하나의 MemoryData_khi 를 표시
    ///   - MemoryCanvasAuthoringWindow_khi : 누락 조각 SO 일괄 생성
    ///
    /// 본체 머지 시 이 클래스의 필드를 원본 MemoryData 로 옮기고 _khi 파일들을 제거.
    /// </summary>
    [CreateAssetMenu(fileName = "MemoryData_khi_New", menuName = "LostMemory/Memory/Canvas Data (khi)")]
    public class MemoryData_khi : MemoryData
    {
        [Header("퍼즐 그리드 (Artwork 를 width × height 셀로 등분)")]
        [Tooltip("가로 셀 수. 1 / 2 / 4 / 8 등.")]
        [SerializeField, Min(1)] private int _gridWidth = 1;

        [Tooltip("세로 셀 수. 1 / 2 / 4 / 8 등.")]
        [SerializeField, Min(1)] private int _gridHeight = 1;

        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_gridWidth < 1) _gridWidth = 1;
            if (_gridHeight < 1) _gridHeight = 1;

            int expected = _gridWidth * _gridHeight;
            int actual = Fragments?.Count ?? 0;
            if (actual != 0 && actual != expected)
            {
                Debug.LogError(
                    $"[MemoryData_khi] '{name}' 그리드 불일치: {_gridWidth}×{_gridHeight}={expected} 인데 Fragments={actual}.",
                    this);
            }

            if (Artwork != null && Artwork.texture != null)
            {
                int tw = Artwork.texture.width;
                int th = Artwork.texture.height;
                if (tw % _gridWidth != 0 || th % _gridHeight != 0)
                {
                    Debug.LogWarning(
                        $"[MemoryData_khi] '{name}' Artwork({tw}×{th}) 가 그리드({_gridWidth}×{_gridHeight})로 정확히 나뉘지 않습니다. 픽셀 정렬 깨질 수 있음.",
                        this);
                }
            }
        }
#endif
    }
}

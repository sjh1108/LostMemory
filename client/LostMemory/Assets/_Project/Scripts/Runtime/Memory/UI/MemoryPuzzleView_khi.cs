using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Memory.UI
{
    /// <summary>
    /// 한 캔버스(MemoryData_khi) 의 N×M 퍼즐 그리드. (테스트용)
    ///
    /// 책임:
    ///   - MemoryData_khi.Artwork 를 GridWidth×GridHeight 셀로 등분해 각 MemoryPuzzleCellView 에 슬라이스 sprite 할당
    ///   - GridLayoutGroup 의 cellSize 자동 계산
    ///   - 셀별 잠금/해금 상태 적용, 다음 해금 순서 셀만 클릭 허용
    ///   - 셀 클릭 → OnPieceClicked 이벤트 발화
    ///
    /// 좌표 규칙:
    ///   piece.Order = y * width + x (행 우선)
    ///   x = order % width, y = order / width
    ///   텍스처 V 좌표는 좌하단 원점이므로 슬라이싱 시 (height-1-y) 사용.
    ///
    /// Inspector 연결 항목:
    ///   _gridParent          — GridLayoutGroup 이 붙은 RectTransform. 부모 크기 기준 cellSize 자동.
    ///   _gridLayout          — GridLayoutGroup 컴포넌트.
    ///   _cellPrefab          — MemoryPuzzleCellView 컴포넌트가 붙은 prefab.
    ///   _desaturateMaterial  — UIDesaturate 머티리얼(_Saturation 프로퍼티 보유).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Memory/UI/Memory Puzzle View (khi)")]
    public sealed class MemoryPuzzleView_khi : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RectTransform _gridParent;
        [SerializeField] private GridLayoutGroup _gridLayout;
        [SerializeField] private MemoryPuzzleCellView _cellPrefab;
        [SerializeField] private Material _desaturateMaterial;

        private readonly List<MemoryPuzzleCellView> _cells = new();
        private MemoryData_khi _canvas;

        /// <summary>셀 클릭 시 발화. 부모 패널이 구독해 확인 모달을 띄운다.</summary>
        public event Action<MemoryFragmentData> OnPieceClicked;

        // ── 외부 공개 API ────────────────────────────────────

        /// <summary>
        /// 그리드 셀을 (재)생성한다. 캔버스가 바뀔 때만 호출.
        /// </summary>
        public void BuildGrid(MemoryData_khi canvas)
        {
            _canvas = canvas;
            ClearCells();

            if (canvas == null || canvas.Artwork == null || canvas.Artwork.texture == null)
            {
                Debug.LogWarning("[MemoryPuzzleView_khi] Canvas 또는 Artwork 누락.", this);
                return;
            }

            int width = canvas.GridWidth;
            int height = canvas.GridHeight;
            int expected = width * height;

            if (canvas.Fragments == null || canvas.Fragments.Count != expected)
            {
                Debug.LogError(
                    $"[MemoryPuzzleView_khi] '{canvas.MemoryId}' 그리드 불일치: " +
                    $"{width}×{height}={expected}, Fragments={canvas.Fragments?.Count ?? 0}.",
                    this);
                return;
            }

            ConfigureLayout(width, height);

            Texture2D tex = canvas.Artwork.texture;
            float cellW = (float)tex.width / width;
            float cellH = (float)tex.height / height;
            float ppu = canvas.Artwork.pixelsPerUnit;

            foreach (var piece in canvas.Fragments)
            {
                if (piece == null) continue;

                int x = piece.Order % width;
                int y = piece.Order / width;

                Rect rect = new Rect(
                    x * cellW,
                    (height - 1 - y) * cellH,
                    cellW,
                    cellH);

                Sprite sliced = Sprite.Create(
                    tex,
                    rect,
                    new Vector2(0.5f, 0.5f),
                    ppu);

                var cell = Instantiate(_cellPrefab, _gridParent);
                cell.name = $"Cell_{piece.Order:D2}_{piece.FragmentId}";
                cell.Init(piece, sliced, _desaturateMaterial);
                cell.OnClicked += HandleCellClicked;
                _cells.Add(cell);
            }
        }

        /// <summary>
        /// 셀들의 잠금/해금 상태를 갱신한다.
        /// animatedPieceId 가 일치하는 셀만 페이드 애니메이션, 나머지는 스냅.
        /// </summary>
        public void Refresh(MemorySaveData save, string animatedPieceId = null)
        {
            if (_canvas == null || save == null) return;

            var unlockedIds = save.UnlockedPieceIds;
            MemoryFragmentData next = _canvas.GetNextLockedPiece(unlockedIds);
            string nextId = next != null ? next.FragmentId : null;

            foreach (var cell in _cells)
            {
                if (cell == null || cell.Piece == null) continue;

                bool unlocked = unlockedIds.Contains(cell.Piece.FragmentId);
                bool canInteract = !unlocked && cell.Piece.FragmentId == nextId;
                bool animate = animatedPieceId != null && cell.Piece.FragmentId == animatedPieceId;

                cell.SetState(unlocked, canInteract, animate);
            }
        }

        // ── 내부 ─────────────────────────────────────────────

        private void ConfigureLayout(int width, int height)
        {
            if (_gridLayout == null || _gridParent == null) return;

            Vector2 size = _gridParent.rect.size;
            float cellW = size.x / width;
            float cellH = size.y / height;

            _gridLayout.cellSize = new Vector2(cellW, cellH);
            _gridLayout.spacing = Vector2.zero;
            _gridLayout.padding = new RectOffset(0, 0, 0, 0);
            _gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            _gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayout.constraintCount = width;
        }

        private void ClearCells()
        {
            foreach (var cell in _cells)
            {
                if (cell == null) continue;
                cell.OnClicked -= HandleCellClicked;
                Destroy(cell.gameObject);
            }
            _cells.Clear();
        }

        private void HandleCellClicked(MemoryPuzzleCellView cell)
        {
            if (cell == null || cell.Piece == null) return;
            OnPieceClicked?.Invoke(cell.Piece);
        }

        private void OnDestroy()
        {
            ClearCells();
        }
    }
}

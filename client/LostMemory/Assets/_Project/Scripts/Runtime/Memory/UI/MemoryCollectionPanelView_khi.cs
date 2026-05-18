using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Memory.UI
{
    /// <summary>
    /// 로비 기억 메타 화면 (테스트용). 캔버스 4개의 해금 현황을 직소 퍼즐 그리드로 표시한다.
    /// 원본 MemoryCollectionPanelView 는 손대지 않고 별도 컴포넌트로 격리.
    ///
    /// 흐름:
    ///   NPC 상호작용 → Open()
    ///   → MemoryMetaService 에서 최신 저장 데이터 로드 → 각 MemoryCardView_khi 갱신
    ///   → 셀 클릭 → MemoryPieceConfirmDialog 표시
    ///   → 확인 → MemoryPieceUnlockService.TryUnlock()
    ///   → OnPieceUnlocked 이벤트 → 해당 셀 페이드인 + 4장 카드 전체 Refresh
    ///
    /// Inspector 연결 항목:
    ///   _allCanvases[]   — MemoryData_khi ScriptableObject 4개
    ///   _cards[]         — MemoryCardView_khi 4개 (캔버스 순서와 일치)
    ///   _closeButton     — 닫기 버튼
    ///   _unlockService   — 조각 해금 트랜잭션 서비스
    ///   _tracker         — 보유 파편 조회용
    ///   _confirmDialog   — 셀 클릭 시 띄울 확인 모달
    ///   _autoOpenOnStart — 디버그용. true 면 Start 시 패널 열림.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Memory/UI/Memory Collection Panel View (khi)")]
    public sealed class MemoryCollectionPanelView_khi : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MemoryData_khi[] _allCanvases;

        [Header("Refs")]
        [SerializeField] private MemoryCardView_khi[] _cards;
        [SerializeField] private Button _closeButton;
        [SerializeField] private MemoryPieceUnlockService _unlockService;
        [SerializeField] private MemoryProgressTracker _tracker;
        [SerializeField] private MemoryPieceConfirmDialog _confirmDialog;

        [Header("Debug")]
        [SerializeField] private bool _autoOpenOnStart = false;

        private void Start()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);

            if (_unlockService != null)
                _unlockService.OnPieceUnlocked += HandlePieceUnlocked;

            foreach (var card in _cards)
            {
                if (card != null)
                    card.OnPieceClicked += HandlePieceClicked;
            }

            if (_autoOpenOnStart)
                Open();
            else
                gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_unlockService != null)
                _unlockService.OnPieceUnlocked -= HandlePieceUnlocked;

            foreach (var card in _cards)
            {
                if (card != null)
                    card.OnPieceClicked -= HandlePieceClicked;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        // ── 외부 공개 API ────────────────────────────────────

        /// <summary>NPC 상호작용 시 패널을 연다. 최신 저장 데이터로 카드 갱신.</summary>
        public void Open()
        {
            gameObject.SetActive(true);
            Refresh(null);
        }

        /// <summary>패널을 닫는다.</summary>
        public void Close()
        {
            if (_confirmDialog != null)
                _confirmDialog.Hide();

            gameObject.SetActive(false);
        }

        // ── 내부 ─────────────────────────────────────────────

        private void Refresh(string animatedPieceId)
        {
            MemorySaveData save = MemoryMetaService.Load();

            int count = Mathf.Min(_allCanvases.Length, _cards.Length);
            for (int i = 0; i < count; i++)
            {
                if (_allCanvases[i] != null && _cards[i] != null)
                    _cards[i].Init(_allCanvases[i], save, animatedPieceId);
            }
        }

        private void HandlePieceClicked(MemoryFragmentData piece)
        {
            if (piece == null || _confirmDialog == null || _unlockService == null)
                return;

            int shards = _tracker != null ? _tracker.AccumulatedShards : 0;
            bool canUnlock = _unlockService.CanUnlock(piece);

            _confirmDialog.Show(piece, shards, canUnlock, () => _unlockService.TryUnlock(piece));
        }

        private void HandlePieceUnlocked(MemoryFragmentData piece)
        {
            Refresh(piece != null ? piece.FragmentId : null);
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Memory.UI
{
    /// <summary>
    /// 기억 조각 해금 패널. 로비에서 NPC 상호작용 시 Open() 호출.
    ///
    /// 캔버스 4개를 Prev/Next 버튼으로 전환하며,
    /// 선택된 캔버스의 조각 6개를 모두 슬롯에 표시한다.
    /// 해금된 조각은 밝게, 해금 가능한 조각은 노란빛, 잠긴 조각은 회색으로 표시.
    ///
    /// Inspector 연결 항목:
    ///   _allCanvases[]     — MemoryData ScriptableObject 4개
    ///   _slots[]           — MemoryPieceSlotView 6개
    ///   _shardsText        — 현재 보유 파편 수 표시
    ///   _canvasNameText    — 현재 선택된 캔버스 이름 표시
    ///   _unlockService     — MemoryPieceUnlockService
    ///   _closeButton       — 닫기 버튼
    ///   _prevButton        — 이전 캔버스 버튼
    ///   _nextButton        — 다음 캔버스 버튼
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Memory/UI/Memory Unlock Panel View")]
    public sealed class MemoryUnlockPanelView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MemoryData[] _allCanvases;   // 캔버스 4개

        [Header("Refs")]
        [SerializeField] private MemoryPieceSlotView[] _slots; // 슬롯 6개
        [SerializeField] private TextMeshProUGUI _shardsText;
        [SerializeField] private TextMeshProUGUI _canvasNameText;
        // 프리팹에서는 Inspector 연결 불필요 — 런타임에 Instance로 자동 탐색
        [SerializeField] private MemoryPieceUnlockService _unlockService;
        [SerializeField] private MemoryUnlockConfirmPopup _confirmPopup;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;

        [Header("Debug")]
        // TODO: 서비스 배포 전 반드시 false 로 변경할 것.
        //       NPC 상호작용 시 Open() 을 직접 호출하는 방식으로 대체 예정.
        [SerializeField] private bool _autoOpenOnStart = false;

        private int _currentCanvasIndex = 0;

        // ── 라이프사이클 ──────────────────────────────────────

        private void Start()
        {
            // Inspector에 연결되지 않은 경우(프리팹 등) 씬에서 자동 탐색
            if (_unlockService == null)
                _unlockService = MemoryPieceUnlockService.Instance;

            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_prevButton  != null) _prevButton.onClick.AddListener(PrevCanvas);
            if (_nextButton  != null) _nextButton.onClick.AddListener(NextCanvas);

            if (_unlockService != null)
                _unlockService.OnPieceUnlocked += OnPieceUnlocked;

            if (_autoOpenOnStart)
                Open();
            else
                gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_unlockService != null)
                _unlockService.OnPieceUnlocked -= OnPieceUnlocked;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        // ── 외부 공개 API ────────────────────────────────────

        /// <summary>NPC 상호작용 시 패널을 연다.</summary>
        public void Open()
        {
            _currentCanvasIndex = 0;
            gameObject.SetActive(true);
            Refresh();
        }

        /// <summary>패널을 닫는다.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        // ── 캔버스 전환 ───────────────────────────────────────

        private void PrevCanvas()
        {
            if (_currentCanvasIndex <= 0) return;
            _currentCanvasIndex--;
            Refresh();
        }

        private void NextCanvas()
        {
            if (_allCanvases == null || _currentCanvasIndex >= _allCanvases.Length - 1) return;
            _currentCanvasIndex++;
            Refresh();
        }

        // ── 내부 갱신 ─────────────────────────────────────────

        private void Refresh()
        {
            MemorySaveData save = MemoryMetaService.Load();

            // 파편 보유량 표시
            if (_shardsText != null)
                _shardsText.text = $"보유 파편: {save.AccumulatedShards}";

            if (_allCanvases == null || _allCanvases.Length == 0) return;

            MemoryData canvas = _allCanvases[_currentCanvasIndex];
            if (canvas == null) return;

            // 캔버스 이름 표시
            if (_canvasNameText != null)
                _canvasNameText.text = canvas.DisplayName;

            // Prev: 첫 번째 캔버스면 비활성
            if (_prevButton != null)
                _prevButton.interactable = _currentCanvasIndex > 0;

            // Next: 마지막 캔버스이거나 현재 캔버스 조각이 모두 해금되지 않으면 비활성
            if (_nextButton != null)
                _nextButton.interactable = _currentCanvasIndex < _allCanvases.Length - 1
                                           && IsCurrentCanvasFullyUnlocked(canvas, save);

            // 슬롯 6개 갱신
            var fragments = canvas.Fragments;
            for (int i = 0; i < _slots.Length; i++)
            {
                MemoryPieceSlotView slot = _slots[i];
                if (slot == null) continue;

                // 이 인덱스에 조각 데이터가 없으면 빈 슬롯
                if (fragments == null || i >= fragments.Count)
                {
                    slot.SetEmpty();
                    continue;
                }

                MemoryFragmentData piece = fragments[i];
                bool isUnlocked = save.UnlockedPieceIds.Contains(piece.FragmentId);

                if (isUnlocked)
                {
                    slot.InitUnlocked(piece);
                }
                else
                {
                    bool canUnlock = _unlockService != null && _unlockService.CanUnlock(piece);
                    slot.InitLocked(piece, canUnlock, OnUnlockRequested);
                }
            }
        }

        /// <summary>현재 캔버스의 모든 조각이 해금되었는지 확인.</summary>
        private bool IsCurrentCanvasFullyUnlocked(MemoryData canvas, MemorySaveData save)
        {
            if (canvas?.Fragments == null || save == null) return false;
            foreach (var frag in canvas.Fragments)
            {
                if (!save.UnlockedPieceIds.Contains(frag.FragmentId))
                    return false;
            }
            return true;
        }

        private void OnUnlockRequested(MemoryFragmentData piece)
        {
            if (_unlockService == null) return;

            // 확인 팝업이 연결되어 있으면 팝업을 먼저 띄운다
            if (_confirmPopup != null)
            {
                _confirmPopup.Show(piece, () => _unlockService.TryUnlock(piece));
            }
            else
            {
                // 팝업 없으면 즉시 해금 (하위 호환)
                _unlockService.TryUnlock(piece);
            }
        }

        private void OnPieceUnlocked(MemoryFragmentData piece)
        {
            Refresh();
        }

        // ── 디버그 ContextMenu ────────────────────────────────

        [UnityEngine.ContextMenu("Debug — Add 30 shards")]
        private void DebugAdd30Shards()
        {
            MemoryShardWallet.EnsureInstance().AddShards(30);
            UnityEngine.Debug.Log("[MemoryUnlockPanelView] +30 shards added.");
            Refresh();
        }

        [UnityEngine.ContextMenu("Debug — Delete save data")]
        private void DebugDeleteSaveData()
        {
            MemoryMetaService.DeleteAll();
            MemoryShardWallet.EnsureInstance().ReloadFromDisk();
            UnityEngine.Debug.Log("[MemoryUnlockPanelView] Save data deleted.");
            Refresh();
        }
    }
}

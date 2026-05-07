using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Memory.UI
{
    /// <summary>
    /// 기억 조각 해금 패널. 로비에서 NPC 상호작용 시 Open() 호출.
    ///
    /// 각 캔버스의 다음 해금 대상 조각을 슬롯으로 표시하고,
    /// 플레이어가 해금 버튼을 누르면 MemoryPieceUnlockService.TryUnlock() 을 호출한다.
    ///
    /// Inspector 연결 항목:
    ///   _allCanvases[]     — MemoryData ScriptableObject 4개
    ///   _slots[]           — MemoryPieceSlotView 4개 (캔버스 순서와 일치)
    ///   _shardsText        — 현재 보유 파편 수 표시
    ///   _unlockService     — MemoryPieceUnlockService
    ///   _tracker           — MemoryProgressTracker (파편 수 조회용)
    ///   _closeButton       — 닫기 버튼
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Memory/UI/Memory Unlock Panel View")]
    public sealed class MemoryUnlockPanelView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MemoryData[] _allCanvases;

        [Header("Refs")]
        [SerializeField] private MemoryPieceSlotView[] _slots;
        [SerializeField] private TextMeshProUGUI _shardsText;
        [SerializeField] private MemoryPieceUnlockService _unlockService;
        [SerializeField] private MemoryProgressTracker _tracker;
        [SerializeField] private Button _closeButton;

        [Header("Debug")]
        [SerializeField] private bool _autoOpenOnStart = false;

        private void Start()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);

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
            gameObject.SetActive(true);
            Refresh();
        }

        /// <summary>패널을 닫는다.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        // ── 내부 ─────────────────────────────────────────────

        private void Refresh()
        {
            MemorySaveData save = MemoryMetaService.Load();

            if (_shardsText != null)
                _shardsText.text = $"보유 파편: {save.AccumulatedShards}";

            int count = Mathf.Min(_allCanvases.Length, _slots.Length);
            for (int i = 0; i < count; i++)
            {
                MemoryData canvas = _allCanvases[i];
                MemoryPieceSlotView slot = _slots[i];
                if (canvas == null || slot == null) continue;

                MemoryFragmentData next = canvas.GetNextLockedPiece(save.UnlockedPieceIds);
                if (next == null)
                {
                    slot.SetEmpty(canvas.DisplayName);
                }
                else
                {
                    bool canUnlock = _unlockService != null && _unlockService.CanUnlock(next);
                    slot.Init(next, canUnlock, OnUnlockRequested);
                }
            }
        }

        private void OnUnlockRequested(MemoryFragmentData piece)
        {
            if (_unlockService == null) return;
            _unlockService.TryUnlock(piece);
        }

        private void OnPieceUnlocked(MemoryFragmentData piece)
        {
            Refresh();
        }
    }
}

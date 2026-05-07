using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Memory.UI
{
    /// <summary>
    /// 로비 기억 메타 화면. 캔버스 4개의 해금 현황을 표시한다.
    ///
    /// NPC 상호작용 시 Open() 호출 → MemoryMetaService 에서 최신 저장 데이터 로드 →
    /// 각 MemoryCardView 를 갱신.
    ///
    /// Inspector 연결 항목:
    ///   _allCanvases[]  — MemoryData ScriptableObject 4개
    ///   _cards[]        — MemoryCardView 4개 (캔버스 순서와 일치)
    ///   _closeButton    — 닫기 버튼
    ///   _autoOpenOnStart — 디버그용. true 면 Start 시 패널 열림.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Memory/UI/Memory Collection Panel View")]
    public sealed class MemoryCollectionPanelView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MemoryData[] _allCanvases;

        [Header("Refs")]
        [SerializeField] private MemoryCardView[] _cards;
        [SerializeField] private Button _closeButton;

        [Header("Debug")]
        [SerializeField] private bool _autoOpenOnStart = false;

        private void Start()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);

            if (_autoOpenOnStart)
                Open();
            else
                gameObject.SetActive(false);
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

            int count = Mathf.Min(_allCanvases.Length, _cards.Length);
            for (int i = 0; i < count; i++)
            {
                if (_allCanvases[i] != null && _cards[i] != null)
                    _cards[i].Init(_allCanvases[i], save);
            }
        }
    }
}

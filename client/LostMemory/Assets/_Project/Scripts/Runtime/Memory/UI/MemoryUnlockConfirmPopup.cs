using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Memory.UI
{
    /// <summary>
    /// 기억 조각 해금 전 확인 팝업.
    ///
    /// 사용법:
    ///   Show(piece, () => { /* 해금 실행 */ });
    ///
    /// Inspector 연결 항목:
    ///   _titleText    — "해금 확인" 등 고정 제목
    ///   _bodyText     — 조각 이름 + 비용 안내 문구
    ///   _confirmButton — "예" 버튼
    ///   _cancelButton  — "아니오" 버튼
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Memory/UI/Memory Unlock Confirm Popup")]
    public sealed class MemoryUnlockConfirmPopup : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _bodyText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        private Action _onConfirm;

        private void Awake()
        {
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_cancelButton  != null) _cancelButton.onClick.AddListener(OnCancelClicked);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (_cancelButton  != null) _cancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        // ── 공개 API ──────────────────────────────────────────

        /// <summary>팝업을 열고 확인 시 실행할 콜백을 등록한다.</summary>
        public void Show(MemoryFragmentData piece, Action onConfirm)
        {
            _onConfirm = onConfirm;

            if (_titleText != null)
                _titleText.text = "해금 확인";

            if (_bodyText != null)
                _bodyText.text = MemoryPieceSlotView.BuildRewardText(piece);

            gameObject.SetActive(true);
        }

        // ── 내부 ──────────────────────────────────────────────

        private void OnConfirmClicked()
        {
            Hide();
            _onConfirm?.Invoke();
            _onConfirm = null;
        }

        private void OnCancelClicked()
        {
            Hide();
            _onConfirm = null;
        }

        private void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}

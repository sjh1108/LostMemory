using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Memory.UI
{
    /// <summary>
    /// 조각 해금 확인 모달.
    /// MemoryCollectionPanelView 가 셀 클릭 시 Show() 로 호출.
    ///
    /// Inspector 연결 항목:
    ///   _root          — 모달 루트 GameObject (Show/Hide 대상)
    ///   _iconImage     — piece.Icon 표시
    ///   _titleText     — piece.DisplayName
    ///   _descriptionText — piece.Description
    ///   _costText      — "{shardCost} 파편 (보유 {accumulated})" 표시
    ///   _insufficientHint — 파편 부족 시 활성화할 표시(옵션)
    ///   _confirmButton — 확인 버튼. canUnlock=false 면 interactable=false
    ///   _cancelButton  — 취소 버튼
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Memory/UI/Memory Piece Confirm Dialog")]
    public sealed class MemoryPieceConfirmDialog : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameObject _root;
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _costText;
        [SerializeField] private GameObject _insufficientHint;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        private Action _onConfirm;

        private void Awake()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(HandleConfirm);

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Hide);

            Hide();
        }

        private void OnDestroy()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(HandleConfirm);

            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(Hide);
        }

        private void Update()
        {
            if (_root != null && _root.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                Hide();
        }

        /// <summary>
        /// 모달 표시. canUnlock=false 면 확인 버튼 비활성 (파편 부족 또는 순서 미충족).
        /// 확인 시 onConfirm() 호출.
        /// </summary>
        public void Show(MemoryFragmentData piece, int accumulatedShards, bool canUnlock, Action onConfirm)
        {
            if (piece == null) return;

            _onConfirm = onConfirm;

            if (_iconImage != null)
            {
                _iconImage.sprite = piece.Icon;
                _iconImage.enabled = piece.Icon != null;
            }

            if (_titleText != null)
                _titleText.text = piece.DisplayName;

            if (_descriptionText != null)
                _descriptionText.text = piece.Description;

            if (_costText != null)
                _costText.text = $"{piece.ShardCost} 파편 (보유 {accumulatedShards})";

            bool insufficient = accumulatedShards < piece.ShardCost;
            if (_insufficientHint != null)
                _insufficientHint.SetActive(insufficient);

            if (_confirmButton != null)
                _confirmButton.interactable = canUnlock;

            if (_root != null)
                _root.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void Hide()
        {
            _onConfirm = null;

            if (_root != null)
                _root.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void HandleConfirm()
        {
            var cb = _onConfirm;
            Hide();
            cb?.Invoke();
        }
    }
}

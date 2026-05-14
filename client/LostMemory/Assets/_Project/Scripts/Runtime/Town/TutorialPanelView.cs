using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostMemory.Town
{
    /// <summary>
    /// 마을 첫 진입 안내 튜토리얼 패널의 뷰.
    /// 컨트롤러(TownTutorialController)가 Show/BindPage 로 페이지 데이터를 주입.
    /// 입력 이벤트는 OnNextClicked / OnPrevClicked / OnCloseClicked 로 노출.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Town/Tutorial Panel View")]
    public class TutorialPanelView : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _bodyText;
        [SerializeField] private TextMeshProUGUI _pageIndicatorText;

        [Header("Buttons")]
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TextMeshProUGUI _nextButtonLabel;

        [Header("Toggle")]
        [SerializeField] private Toggle _dontShowAgainToggle;

        [Header("Labels (i18n 자유 변경용)")]
        [SerializeField] private string _nextLabel = "다음";
        [SerializeField] private string _completeLabel = "완료";

        public event Action OnNextClicked;
        public event Action OnPrevClicked;
        public event Action OnCloseClicked;

        public bool DontShowAgain => _dontShowAgainToggle != null && _dontShowAgainToggle.isOn;

        private void Awake()
        {
            if (_prevButton != null)  _prevButton.onClick.AddListener(() => OnPrevClicked?.Invoke());
            if (_nextButton != null)  _nextButton.onClick.AddListener(() => OnNextClicked?.Invoke());
            if (_closeButton != null) _closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());
        }

        public void Show()
        {
            gameObject.SetActive(true);
            // 키보드 Submit(Space/Enter) 이 마지막 selected 버튼을 트리거하지 않도록 selection 해제.
            // Button 의 Navigation 이 None 이어도, 이전에 다른 UI 가 selected 인 상태로 들어오면
            // Submit 이 그 버튼을 누를 수 있어 추가 안전장치.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 한 페이지의 데이터를 뷰에 반영.
        /// 마지막 페이지면 Next 라벨을 "완료" 로, 첫 페이지면 Prev 비활성.
        /// </summary>
        public void BindPage(int index, int total, string title, string body)
        {
            if (_titleText != null)         _titleText.text = title;
            if (_bodyText != null)          _bodyText.text = body;
            if (_pageIndicatorText != null) _pageIndicatorText.text = $"{index + 1} / {total}";

            if (_prevButton != null) _prevButton.interactable = index > 0;

            bool isLast = index >= total - 1;
            if (_nextButtonLabel != null)
                _nextButtonLabel.text = isLast ? _completeLabel : _nextLabel;
        }
    }
}

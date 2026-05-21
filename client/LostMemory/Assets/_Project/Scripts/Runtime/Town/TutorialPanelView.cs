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
            if (_dontShowAgainToggle == null)
            {
                _dontShowAgainToggle = GetComponentInChildren<Toggle>(true);
                if (_dontShowAgainToggle == null)
                    _dontShowAgainToggle = CreateDontShowAgainToggle();
            }
            if (_prevButton != null)  _prevButton.onClick.AddListener(() => OnPrevClicked?.Invoke());
            if (_nextButton != null)  _nextButton.onClick.AddListener(() => OnNextClicked?.Invoke());
            if (_closeButton != null) _closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());
        }

        /// <summary>
        /// prefab 에 Toggle 이 wired 되어 있지 않으면 코드로 패널 좌하단에 자동 생성.
        /// 체크박스 16×16 + 라벨 "다시 보지 않기".
        /// </summary>
        private Toggle CreateDontShowAgainToggle()
        {
            // 패널 좌하단에 부착할 컨테이너
            GameObject toggleGO = new GameObject("DontShowAgainToggle_Auto",
                typeof(RectTransform), typeof(Toggle));
            toggleGO.transform.SetParent(transform, false);
            var toggleRT = (RectTransform)toggleGO.transform;
            toggleRT.anchorMin = new Vector2(0f, 0f);
            toggleRT.anchorMax = new Vector2(0f, 0f);
            toggleRT.pivot     = new Vector2(0f, 0f);
            toggleRT.anchoredPosition = new Vector2(16f, 16f);
            toggleRT.sizeDelta = new Vector2(160f, 24f);

            // 체크박스 배경 (Image)
            GameObject bgGO = new GameObject("Background",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGO.transform.SetParent(toggleGO.transform, false);
            var bgRT = (RectTransform)bgGO.transform;
            bgRT.anchorMin = new Vector2(0f, 0.5f);
            bgRT.anchorMax = new Vector2(0f, 0.5f);
            bgRT.pivot     = new Vector2(0f, 0.5f);
            bgRT.anchoredPosition = new Vector2(0f, 0f);
            bgRT.sizeDelta = new Vector2(20f, 20f);
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.85f);

            // 체크 표시 (Image — Toggle.graphic)
            GameObject checkGO = new GameObject("Checkmark",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            checkGO.transform.SetParent(bgGO.transform, false);
            var checkRT = (RectTransform)checkGO.transform;
            checkRT.anchorMin = new Vector2(0.15f, 0.15f);
            checkRT.anchorMax = new Vector2(0.85f, 0.85f);
            checkRT.offsetMin = Vector2.zero;
            checkRT.offsetMax = Vector2.zero;
            var checkImg = checkGO.GetComponent<Image>();
            checkImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // 라벨
            GameObject labelGO = new GameObject("Label",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(toggleGO.transform, false);
            var labelRT = (RectTransform)labelGO.transform;
            labelRT.anchorMin = new Vector2(0f, 0f);
            labelRT.anchorMax = new Vector2(1f, 1f);
            labelRT.pivot     = new Vector2(0f, 0.5f);
            labelRT.offsetMin = new Vector2(26f, 0f);
            labelRT.offsetMax = Vector2.zero;
            var labelTxt = labelGO.GetComponent<TextMeshProUGUI>();
            labelTxt.text = "다시 보지 않기";
            labelTxt.fontSize = 14;
            labelTxt.color = Color.white;
            labelTxt.alignment = TextAlignmentOptions.Left;
            labelTxt.raycastTarget = false;
            // 한글 폰트: 패널 내 다른 TMP_Text 의 font 빌림
            if (_titleText != null && _titleText.font != null) labelTxt.font = _titleText.font;
            else if (_bodyText != null && _bodyText.font != null) labelTxt.font = _bodyText.font;

            // Toggle wiring
            var toggle = toggleGO.GetComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = false;

            Debug.Log("[TutorialPanelView] '다시 보지 않기' Toggle 자동 생성 (prefab 미할당 대체).", this);
            return toggle;
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

        /// <summary>
        /// 이벤트 기반 튜토리얼 (던전) 에서 사용. 다음/이전 버튼을 통째로 숨기고 이벤트로만 페이지를 넘긴다.
        /// 마을 튜토리얼은 호출 안 함 → 기본 visible 상태 유지.
        /// </summary>
        public void SetNextButtonVisible(bool visible)
        {
            if (_nextButton != null) _nextButton.gameObject.SetActive(visible);
        }

        public void SetPrevButtonVisible(bool visible)
        {
            if (_prevButton != null) _prevButton.gameObject.SetActive(visible);
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// 퍼즈 패널 UI를 담당하는 뷰 컴포넌트.
    ///
    /// 버튼 3개 구성:
    ///   - 재개   → OnResumeClicked 이벤트 발행 (게임 로직은 TopRightHUDView가 처리)
    ///   - 환경설정 → SettingsPanel 토글 (자체 처리, 별도 이벤트 없음)
    ///   - 로비   → OnLobbyClicked 이벤트 발행 (씬 전환은 TopRightHUDView가 처리)
    /// </summary>
    [AddComponentMenu("Lost Memory/UI/Pause Panel View")]
    public class PausePanelView : MonoBehaviour
    {
        [Header("버튼")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _lobbyButton;

        [Header("환경설정 패널 (미구현이면 비워 두세요)")]
        [SerializeField] private GameObject _settingsPanel;

        /// <summary>재개 버튼을 눌렀을 때 발생</summary>
        public event Action OnResumeClicked;

        /// <summary>로비로 이동 버튼을 눌렀을 때 발생</summary>
        public event Action OnLobbyClicked;

        private void Awake()
        {
            if (_resumeButton != null)
                _resumeButton.onClick.AddListener(() => OnResumeClicked?.Invoke());

            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(ToggleSettings);

            if (_lobbyButton != null)
                _lobbyButton.onClick.AddListener(() => OnLobbyClicked?.Invoke());
        }

        /// <summary>퍼즈 패널을 연다.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
            // 패널 열릴 때 환경설정은 항상 닫힌 상태로 시작
            if (_settingsPanel != null)
                _settingsPanel.SetActive(false);
        }

        /// <summary>퍼즈 패널을 닫는다.</summary>
        public void Hide() => gameObject.SetActive(false);

        // ── 내부 처리 ─────────────────────────────────────────────────

        private void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            _settingsPanel.SetActive(!_settingsPanel.activeSelf);
        }
    }
}

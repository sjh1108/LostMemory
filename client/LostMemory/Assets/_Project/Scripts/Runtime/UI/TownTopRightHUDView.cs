using LostMemory.Talents;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LostMemory.UI
{
    /// <summary>
    /// 마을 화면 우상단 HUD를 담당하는 뷰 컴포넌트.
    ///
    /// T 키 또는 TalentButton 클릭 → TalentPanel 토글
    /// SettingButton 클릭 → SettingsPanel 토글
    /// ESC 키 → ESCPanel 토글 (열린 패널이 있으면 우선 닫음)
    /// </summary>
    [AddComponentMenu("Lost Memory/UI/Town Top Right HUD View")]
    public class TownTopRightHUDView : MonoBehaviour
    {
        [Header("버튼")]
        [SerializeField] private Button _talentButton;
        [SerializeField] private Button _settingsButton;

        [Header("패널")]
        [SerializeField] private TalentPanelView _talentPanel;
        [SerializeField] private SettingsPanelView _settingsPanel;
        [SerializeField] private GameObject _escPanel;

        [Header("ESC 패널 버튼")]
        [SerializeField] private Button _escSettingsButton;  // 환경설정
        [SerializeField] private Button _escResumeButton;    // 계속하기
        [SerializeField] private Button _escQuitButton;      // 게임종료

        // ── 라이프사이클 ──────────────────────────────────────────────

        private void Start()
        {
            if (_talentButton != null)
                _talentButton.onClick.AddListener(ToggleTalentPanel);
            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(HandleEscape);

            // ESC 패널 버튼 연결
            if (_escSettingsButton != null)
                _escSettingsButton.onClick.AddListener(OnEscSettingsClicked);
            if (_escResumeButton != null)
                _escResumeButton.onClick.AddListener(CloseEscPanel);
            if (_escQuitButton != null)
                _escQuitButton.onClick.AddListener(QuitGame);

            // 시작 시 모든 패널 닫힘 상태로
            _talentPanel?.Close();
            if (_settingsPanel != null)
                _settingsPanel.gameObject.SetActive(false);
            if (_escPanel != null)
                _escPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_talentButton != null)
                _talentButton.onClick.RemoveListener(ToggleTalentPanel);
            if (_settingsButton != null)
                _settingsButton.onClick.RemoveListener(HandleEscape);
            if (_escSettingsButton != null)
                _escSettingsButton.onClick.RemoveListener(OnEscSettingsClicked);
            if (_escResumeButton != null)
                _escResumeButton.onClick.RemoveListener(CloseEscPanel);
            if (_escQuitButton != null)
                _escQuitButton.onClick.RemoveListener(QuitGame);
        }

        // ── 키 입력 ───────────────────────────────────────────────────

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
                ToggleTalentPanel();

            if (Input.GetKeyDown(KeyCode.Escape))
                HandleEscape();
        }

        // ── 내부 처리 ─────────────────────────────────────────────────

        private void ToggleTalentPanel()
        {
            if (_talentPanel == null) return;
            if (_talentPanel.gameObject.activeSelf)
            {
                _talentPanel.Close();
            }
            else
            {
                _talentPanel.Open();
                _talentPanel.transform.SetAsLastSibling();
            }
        }

        private void ToggleSettingsPanel()
        {
            if (_settingsPanel == null) return;
            bool willOpen = !_settingsPanel.gameObject.activeSelf;
            _settingsPanel.gameObject.SetActive(willOpen);
            if (willOpen)
            {
                transform.SetAsLastSibling();
                _settingsPanel.transform.SetAsLastSibling();
            }
        }

        private void OnEscSettingsClicked()
        {
            if (_settingsPanel == null) return;
            _settingsPanel.gameObject.SetActive(true);
            _settingsPanel.transform.SetAsLastSibling();
        }

        private void CloseEscPanel()
        {
            _escPanel?.SetActive(false);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// ESC 처리 — 열린 패널을 Z축 높은 것부터 차례로 닫고,
        /// 아무것도 열려있지 않으면 ESCPanel을 연다.
        /// </summary>
        private void HandleEscape()
        {
            // 1순위: SettingsPanel 열려있으면 전부 닫기
            if (_settingsPanel != null && _settingsPanel.gameObject.activeSelf)
            {
                _settingsPanel.gameObject.SetActive(false);
                _escPanel?.SetActive(false);
                return;
            }
            // 2순위: TalentPanel 열려있으면 닫기
            if (_talentPanel != null && _talentPanel.gameObject.activeSelf)
            {
                _talentPanel.Close();
                return;
            }
            // 3순위: ESCPanel 열려있으면 닫기
            if (_escPanel != null && _escPanel.activeSelf)
            {
                _escPanel.SetActive(false);
                return;
            }
            // 아무것도 열려있지 않으면 ESCPanel 열기
            if (_escPanel != null)
            {
                _escPanel.SetActive(true);
                _escPanel.transform.SetAsLastSibling();
            }
        }
    }
}

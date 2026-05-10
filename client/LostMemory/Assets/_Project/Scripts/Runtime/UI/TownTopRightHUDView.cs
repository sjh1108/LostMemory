using LostMemory.Talents;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// 마을 화면 우상단 HUD를 담당하는 뷰 컴포넌트.
    ///
    /// T 키 또는 TalentButton 클릭 → TalentPanel 토글
    /// SettingButton 클릭 → SettingsPanel 토글
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

        // ── 라이프사이클 ──────────────────────────────────────────────

        private void Start()
        {
            if (_talentButton != null)
                _talentButton.onClick.AddListener(ToggleTalentPanel);

            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(ToggleSettingsPanel);

            if (_settingsPanel != null)
                _settingsPanel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_talentButton != null)
                _talentButton.onClick.RemoveListener(ToggleTalentPanel);

            if (_settingsButton != null)
                _settingsButton.onClick.RemoveListener(ToggleSettingsPanel);
        }

        // ── 키 입력 ───────────────────────────────────────────────────

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
                ToggleTalentPanel();

            if (Input.GetKeyDown(KeyCode.Escape))
                CloseTopPanel();
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

            // 열릴 때 항상 최상단(Z축 최우선)으로
            if (willOpen)
            {
                transform.SetAsLastSibling();
                _settingsPanel.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// ESC 처리:
        ///   열린 패널이 있으면 Z축 높은 것부터 차례로 닫는다.
        ///   아무것도 열려있지 않으면 SettingsPanel을 연다.
        /// </summary>
        private void CloseTopPanel()
        {
            // 1순위: SettingsPanel 열려있으면 닫기
            if (_settingsPanel != null && _settingsPanel.gameObject.activeSelf)
            {
                _settingsPanel.gameObject.SetActive(false);
                return;
            }
            // 2순위: TalentPanel 열려있으면 닫기
            if (_talentPanel != null && _talentPanel.gameObject.activeSelf)
            {
                _talentPanel.Close();
                return;
            }
            // 아무것도 열려있지 않으면 SettingsPanel 열기
            if (_settingsPanel != null)
            {
                transform.SetAsLastSibling();
                _settingsPanel.gameObject.SetActive(true);
                _settingsPanel.transform.SetAsLastSibling();
            }
        }
    }
}

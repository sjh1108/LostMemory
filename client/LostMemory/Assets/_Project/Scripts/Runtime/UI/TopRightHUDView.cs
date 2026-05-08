using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// 우상단 HUD를 담당하는 뷰 컴포넌트.
    ///
    /// Pause 버튼 1개만 노출하고, 누르면:
    ///   1. Time.timeScale = 0 (게임 일시정지)
    ///   2. PausePanel 표시 (재개 / 환경설정 / 로비 버튼 포함)
    /// </summary>
    [AddComponentMenu("Lost Memory/UI/Top Right HUD View")]
    public class TopRightHUDView : MonoBehaviour
    {
        [Header("버튼")]
        [SerializeField] private Button _pauseButton;

        [Header("패널")]
        [SerializeField] private PausePanelView _pausePanel;

        [Header("씬 이동")]
        [Tooltip("로비 씬 이름. Build Settings에 등록된 이름과 정확히 일치해야 합니다.")]
        [SerializeField] private string _lobbySceneName = "Lobby";

        // ── 라이프사이클 ──────────────────────────────────────────────

        private void Start()
        {
            if (_pauseButton != null)
                _pauseButton.onClick.AddListener(OnPauseClicked);

            if (_pausePanel != null)
            {
                _pausePanel.OnResumeClicked += ResumeGame;
                _pausePanel.OnLobbyClicked  += GoToLobby;
                _pausePanel.Hide();   // 시작 시 숨김
            }
        }

        private void OnDestroy()
        {
            if (_pausePanel != null)
            {
                _pausePanel.OnResumeClicked -= ResumeGame;
                _pausePanel.OnLobbyClicked  -= GoToLobby;
            }
        }

        // ── 키 입력 ───────────────────────────────────────────────────

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // PausePanel 열려있으면 닫기(재개), 닫혀있으면 열기(정지)
                if (_pausePanel != null && _pausePanel.gameObject.activeSelf)
                    ResumeGame();
                else
                    OnPauseClicked();
            }
        }

        // ── 버튼 처리 ─────────────────────────────────────────────────

        /// <summary>Pause 버튼 클릭 — 열려있으면 재개, 닫혀있으면 정지</summary>
        private void OnPauseClicked()
        {
            if (_pausePanel != null && _pausePanel.gameObject.activeSelf)
                ResumeGame();
            else
            {
                Time.timeScale = 0f;
                _pausePanel?.Show();
            }
        }

        /// <summary>재개 — 게임 재개 + 패널 숨김</summary>
        private void ResumeGame()
        {
            Time.timeScale = 1f;
            _pausePanel?.Hide();
        }

        /// <summary>로비 이동 — timeScale 복구 후 씬 전환</summary>
        private void GoToLobby()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_lobbySceneName);
        }
    }
}

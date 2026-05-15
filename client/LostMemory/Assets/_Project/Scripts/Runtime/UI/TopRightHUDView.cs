using System;
using LostMemory.Stage;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// 우상단 HUD를 담당하는 뷰 컴포넌트.
    ///
    /// Pause 버튼 1개만 노출하고, 누르면:
    ///   1. 싱글플레이에서는 Time.timeScale = 0, 멀티플레이에서는 로컬 메뉴만 표시
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

        private bool _isConfigured;

        // ── 라이프사이클 ──────────────────────────────────────────────

        private void Awake()
        {
            if (!enabled)
            {
                return;
            }

            _isConfigured = _pausePanel != null;

            if (_pausePanel == null)
            {
                Debug.LogWarning("[TopRightHUDView] PausePanel reference is missing. TopRightHUDView will stay inactive.", this);
                enabled = false;
                return;
            }

            if (_pauseButton == null)
            {
                Debug.LogWarning("[TopRightHUDView] PauseButton reference is missing. ESC pause still works.", this);
            }
        }

        private void Start()
        {
            if (!_isConfigured)
            {
                return;
            }

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
            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(OnPauseClicked);
            }

            if (_pausePanel != null)
            {
                _pausePanel.OnResumeClicked -= ResumeGame;
                _pausePanel.OnLobbyClicked  -= GoToLobby;
            }
        }

        private void OnDisable()
        {
            if (_isConfigured && _pausePanel != null && _pausePanel.gameObject.activeSelf)
            {
                ResumeGame();
            }
        }

        // ── 키 입력 ───────────────────────────────────────────────────

        private void Update()
        {
            if (!_isConfigured)
            {
                return;
            }

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
            if (!_isConfigured)
            {
                return;
            }

            if (_pausePanel != null && _pausePanel.gameObject.activeSelf)
                ResumeGame();
            else
            {
                PauseGame();
            }
        }

        /// <summary>재개 — 게임 재개 + 패널 숨김</summary>
        private void ResumeGame()
        {
            if (!IsMultiplayerSessionActive())
            {
                Time.timeScale = 1f;
            }

            _pausePanel?.Hide();
        }

        /// <summary>로비 이동 — timeScale 복구 후 씬 전환</summary>
        private void GoToLobby()
        {
            if (!IsMultiplayerSessionActive())
            {
                Time.timeScale = 1f;
            }

            _pausePanel?.Hide();

            if (IsMultiplayerSessionActive())
            {
                TryRequestMultiplayerTownReturn();
                return;
            }

            if (TryReturnToTownThroughRunManager())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_lobbySceneName))
            {
                Debug.LogWarning("[TopRightHUDView] Lobby scene name is empty.", this);
                return;
            }

            SceneManager.LoadScene(_lobbySceneName);
        }

        private void PauseGame()
        {
            if (!IsMultiplayerSessionActive())
            {
                Time.timeScale = 0f;
            }

            _pausePanel?.Show();
        }

        private bool IsMultiplayerSessionActive()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening;
        }

        private void TryRequestMultiplayerTownReturn()
        {
            if (!string.Equals(_lobbySceneName, "Town", StringComparison.Ordinal))
            {
                Debug.LogWarning("[TopRightHUDView] Multiplayer lobby return only supports Town for now.", this);
                return;
            }

            RunManager runManager = RunManager.Instance;
            if (runManager == null)
            {
                Debug.LogWarning("[TopRightHUDView] RunManager.Instance is missing. Cannot request multiplayer town return.", this);
                return;
            }

            if (!runManager.IsAuthority)
            {
                Debug.LogWarning("[TopRightHUDView] Client town return request is not wired yet. Host must return to Town.", this);
                return;
            }

            runManager.AbandonRunAndReturnToTown();
        }

        private bool TryReturnToTownThroughRunManager()
        {
            if (!string.Equals(_lobbySceneName, "Town", StringComparison.Ordinal))
            {
                return false;
            }

            RunManager runManager = RunManager.Instance;
            if (runManager == null)
            {
                return false;
            }

            runManager.AbandonRunAndReturnToTown();
            return true;
        }
    }
}

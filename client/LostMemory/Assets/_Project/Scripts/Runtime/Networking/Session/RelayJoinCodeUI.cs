using LostMemory.Networking.Common;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
// using Unity.Multiplayer.PlayMode;   // ← 추가
#endif

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// Test_Network 씬용 임시 UI 핸들러. 호스트 생성/코드 입력 참가/나가기 버튼을 묶고
    /// RelaySession 이벤트로 상태 텍스트를 갱신한다.
    ///
    /// 실제 게임 UI 가 도입되기 전 검증용. 본 컴포넌트는 _Project/Scenes/Test/Test_Network_AD.unity
    /// 의 Canvas 에 부착하고, Inspector 에서 각 필드를 연결한다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Relay Join Code UI")]
    public sealed class RelayJoinCodeUI : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text joinCodeDisplay;

        [Header("Input")]
        [SerializeField] private TMP_InputField joinCodeInput;

        [Header("Buttons")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button leaveButton;

        [Header("Config")]
        [SerializeField, Min(2)] private int maxPlayers = 4;

        [Header("Multi-Instance Override")]
        [Tooltip("MPPM 가상 인스턴스에서 다른 계정으로 로그인하려면 채울 것. 메인은 비워둠.")]
        [SerializeField] private string overrideLoginId;
        [SerializeField] private string overrideNickname;

        private void Awake()
        {
            // 임시 — Multiplayer Play Mode 인스턴스별 다른 자격증명.
            // MPPM 윈도우에서 각 가상 Player 에 태그("Guest1"/"Guest2"/"Guest3") 부여 시 자동 적용.
            // 메인 Editor (Player 1) 는 기본값(testuser) 그대로.
            ApplyMppmCredentialOverride();
        }

        private void ApplyMppmCredentialOverride()
        {
            string dataPath = Application.dataPath.Replace('\\', '/');
            NetLog.Info("UI", $"[MPPM] dataPath = {dataPath}");

            // MPPM 가상 플레이어는 임시 클론 폴더에서 실행됨.
            // Library/VP/{guid}/... 또는 .mppm/ 등 경로에 마커가 들어감.
            bool isVirtualPlayer = dataPath.Contains("/mppm/")
                                 || dataPath.Contains("/VP/")
                                 || dataPath.Contains("/VirtualProjects/")
                                 || dataPath.Contains("Library/VP");

            if (!isVirtualPlayer)
            {
                NetLog.Info("UI", "[MPPM] Detected MAIN editor. Login as testuser.");
                return;
            }

            // 1차: MPPM Tag 기반 분기 (collision-free)
            // MPPM 패널에서 각 Virtual Player 에 "guest1" / "guest2" / "guest3" 태그 부여 시 자동 매핑.
#if UNITY_EDITOR
            try
            {
                var tags = Unity.Multiplayer.PlayMode.CurrentPlayer.ReadOnlyTags();
                foreach (var tag in tags)
                {
                    if (tag != null && tag.StartsWith("guest"))
                    {
                        string idxStr = tag.Substring("guest".Length);
                        if (int.TryParse(idxStr, out int parsedIdx) && parsedIdx >= 1)
                        {
                            RelaySession.AutoLoginId = $"testuser0{parsedIdx}";
                            RelaySession.AutoLoginNickname = $"테스터0{parsedIdx}";
                            NetLog.Info("UI", $"[MPPM] Detected VIRTUAL player tag={tag}. Login as testuser0{parsedIdx}");
                            return;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                NetLog.Warn("UI", $"[MPPM] Tag 조회 실패, hash fallback 으로: {e.Message}");
            }
#endif

            // 2차 fallback: 폴더 경로 해시 — Tag 미부여 시. collision 가능
            int hash = System.Math.Abs(dataPath.GetHashCode());
            int idx = (hash % 3) + 1;  // 1, 2, 3
            RelaySession.AutoLoginId = $"testuser0{idx}";
            RelaySession.AutoLoginNickname = $"테스터0{idx}";
            NetLog.Info("UI", $"[MPPM] Detected VIRTUAL player (no tag). Fallback hash idx={idx}. Login as testuser0{idx}");
        }
        private void Update()
        {
            // 임시 — UI 정리 전까지 키 입력으로 우회. UI 정상화되면 제거 권장.
            if (Input.GetKeyDown(KeyCode.H) && !RelaySession.IsInSession) OnHostClicked();
            if (Input.GetKeyDown(KeyCode.J) && !RelaySession.IsInSession) OnJoinClicked();
            if (Input.GetKeyDown(KeyCode.L) && RelaySession.IsInSession) OnLeaveClicked();
        }

        private void OnEnable()
        {
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);

            RelaySession.Joined += HandleJoined;
            RelaySession.Left += HandleLeft;
            RelaySession.Failed += HandleFailed;

            SetStatus("대기 중. 호스트 생성 또는 코드 입력 참가.");
            RefreshButtonState();
        }

        private void OnDisable()
        {
            if (hostButton != null) hostButton.onClick.RemoveListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.RemoveListener(OnJoinClicked);
            if (leaveButton != null) leaveButton.onClick.RemoveListener(OnLeaveClicked);

            RelaySession.Joined -= HandleJoined;
            RelaySession.Left -= HandleLeft;
            RelaySession.Failed -= HandleFailed;
        }

        private async void OnHostClicked()
        {
            SetStatus("호스트 생성 중...");
            SetAllButtonsInteractable(false);

            RelaySessionHost.CreateResult result = await RelaySessionHost.CreateAsync(maxPlayers);
            if (result.Success)
            {
                if (joinCodeDisplay != null) joinCodeDisplay.text = result.JoinCode;
                SetStatus($"호스트 활성. 코드: {result.JoinCode}");
            }
            else
            {
                SetStatus(SessionErrorPolicy.ToUserMessage(result.ErrorKind, result.ErrorDetail));
            }
            RefreshButtonState();
        }

        private async void OnJoinClicked()
        {
            string code = joinCodeInput != null ? joinCodeInput.text : string.Empty;
            SetStatus("참가 중...");
            SetAllButtonsInteractable(false);

            RelaySessionClient.JoinResult result = await RelaySessionClient.JoinByCodeAsync(code);
            if (result.Success)
            {
                SetStatus("참가 성공.");
            }
            else
            {
                SetStatus(SessionErrorPolicy.ToUserMessage(result.ErrorKind, result.ErrorDetail));
            }
            RefreshButtonState();
        }

        private async void OnLeaveClicked()
        {
            SetStatus("나가는 중...");
            SetAllButtonsInteractable(false);

            await RelaySession.LeaveAsync();
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                nm.Shutdown(discardMessageQueue: true);   // ← discardMessageQueue 인자 추가
            }
            SetStatus("세션 종료.");
            if (joinCodeDisplay != null) joinCodeDisplay.text = string.Empty;
            RefreshButtonState();

            // (선택) NGO 의 NetworkManager.Singleton 이 재진입 spawn 을 깨끗하게 못 하면 scene reload 로 fallback
            // 사용 시 상단에 using UnityEngine.SceneManagement; 추가
            // var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            // UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
        }

        private void HandleJoined(bool asHost)
        {
            RefreshButtonState();
        }

        private void HandleLeft()
        {
            if (joinCodeDisplay != null) joinCodeDisplay.text = string.Empty;
            SetStatus("세션 이탈.");
            RefreshButtonState();
        }

        private void HandleFailed(SessionErrorKind kind, string detail)
        {
            SetStatus(SessionErrorPolicy.ToUserMessage(kind, detail));
            RefreshButtonState();
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
            NetLog.Info("UI", text);
        }

        private void RefreshButtonState()
        {
            bool inSession = RelaySession.IsInSession;
            if (hostButton != null) hostButton.interactable = !inSession;
            if (joinButton != null) joinButton.interactable = !inSession;
            if (leaveButton != null) leaveButton.interactable = inSession;
        }

        private void SetAllButtonsInteractable(bool value)
        {
            if (hostButton != null) hostButton.interactable = value;
            if (joinButton != null) joinButton.interactable = value;
            if (leaveButton != null) leaveButton.interactable = value;
        }
    }
}

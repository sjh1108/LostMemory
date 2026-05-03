using LostMemory.Networking.Common;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField, Min(2)] private int maxPlayers = 2;

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
                nm.Shutdown();
            }
            SetStatus("세션 종료.");
            if (joinCodeDisplay != null) joinCodeDisplay.text = string.Empty;
            RefreshButtonState();
        }

        private void HandleJoined(bool asHost)
        {
            RefreshButtonState();
        }

        private void HandleLeft()
        {
            if (joinCodeDisplay != null) joinCodeDisplay.text = string.Empty;
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

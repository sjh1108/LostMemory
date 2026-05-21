using System;
using LostMemory.Networking.Common;
using LostMemory.Networking.Session;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// 타이틀 씬 컨트롤러 — 로그인 / 회원가입 UI + 백엔드 호출.
    ///
    /// 흐름:
    ///   1. ID / Password / Nickname 입력
    ///   2. [로그인] 또는 [회원가입] 버튼
    ///   3. 백엔드 호출 (POST /api/auth/login 또는 signup → login)
    ///   4. 성공 시 RelaySession.AutoLogin* 정적 필드 sync + Town 씬 로드
    ///
    /// 부착 위치: Title.unity 씬의 Canvas 산하 GameObject. Inspector 에 UI 필드 연결.
    /// 후속 — 발표 후 정식 타이틀 화면으로 디자인 보강 가능.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Title Scene Controller")]
    public sealed class TitleSceneController : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] private TMP_InputField loginIdInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField, Tooltip("회원가입 시에만 사용. 로그인 시 비어있어도 OK.")]
        private TMP_InputField nicknameInput;

        [Header("Buttons")]
        [SerializeField] private Button loginButton;
        [SerializeField] private Button signupButton;

        [Header("Status")]
        [SerializeField] private TMP_Text statusText;

        [Header("Scene Transition")]
        [SerializeField, Tooltip("로그인 성공 시 로드할 마을 씬 이름.")]
        private string townSceneName = "Town";
        [SerializeField, Tooltip("인트로 미시청 유저에게 보여줄 인트로 씬 이름.")]
        private string introSceneName = "Phase0_Intro";
        [SerializeField, Tooltip("개발용: 켜면 PlayerPrefs 무시하고 항상 인트로부터 재생.")]
        private bool forcePlayIntro = false;

        private const string IntroSeenPrefKey = "LostMemory.IntroSeen";

        private void Awake()
        {
            if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
            if (signupButton != null) signupButton.onClick.AddListener(OnSignupClicked);
            SetStatus("로그인 또는 회원가입을 진행하세요.");
        }

        private void Start()
        {
            // L-1: Local Fallback 모드면 로그인 UI 자동 우회 → townSceneName 으로 직접 이동.
            // forcePlayIntro / introSeen PlayerPrefs 무관. 빠른 테스트 진입.
            if (RelaySession.UseLocalFallback)
            {
                SetStatus($"[LOCAL FALLBACK] 로그인 우회 → {townSceneName}");
                SceneManager.LoadScene(townSceneName);
            }
        }

        private async void OnLoginClicked()
        {
            // L-1: fallback 모드 — 입력 없이 버튼 눌러도 통과.
            if (RelaySession.UseLocalFallback)
            {
                SetStatus("[LOCAL FALLBACK] 로그인 우회.");
                LoadNextSceneAfterAuth("[LOCAL FALLBACK]");
                return;
            }

            string id = loginIdInput != null ? loginIdInput.text?.Trim() : null;
            string pw = passwordInput != null ? passwordInput.text : null;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
            {
                SetStatus("ID 와 비밀번호를 입력해주세요.");
                return;
            }

            SetButtonsInteractable(false);
            SetStatus("로그인 중...");

            try
            {
                bool loginOk = await SessionApiClient.LoginAsync(id, pw);
                if (!loginOk)
                {
                    SetStatus("로그인 실패. ID 또는 비밀번호를 확인해주세요.");
                    SetButtonsInteractable(true);
                    return;
                }

                bool meOk = await SessionApiClient.FetchMyUserIdAsync();
                if (!meOk)
                {
                    SetStatus("내 정보 조회 실패.");
                    SetButtonsInteractable(true);
                    return;
                }

                // RelaySession.EnsureInitializedAsync 가 다음에 호출되어도 IsLoggedIn=true 라 skip.
                // AutoLogin* 도 sync — 게임 도중 재로그인 흐름 (있다면) 대비.
                RelaySession.AutoLoginId = id;
                RelaySession.AutoLoginPassword = pw;

                LoadNextSceneAfterAuth("로그인 성공");
            }
            catch (Exception ex)
            {
                NetLog.Error("Title", $"Login threw: {ex.Message}");
                SetStatus("로그인 중 오류: " + ex.Message);
                SetButtonsInteractable(true);
            }
        }

        private async void OnSignupClicked()
        {
            // L-1: fallback 모드 — 입력 없이 버튼 눌러도 통과.
            if (RelaySession.UseLocalFallback)
            {
                SetStatus("[LOCAL FALLBACK] 회원가입 우회.");
                LoadNextSceneAfterAuth("[LOCAL FALLBACK]");
                return;
            }

            string id = loginIdInput != null ? loginIdInput.text?.Trim() : null;
            string pw = passwordInput != null ? passwordInput.text : null;
            string nick = nicknameInput != null ? nicknameInput.text?.Trim() : null;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw) || string.IsNullOrEmpty(nick))
            {
                SetStatus("ID / 비밀번호 / 닉네임 모두 입력해주세요.");
                return;
            }

            SetButtonsInteractable(false);
            SetStatus("회원가입 중...");

            try
            {
                // TrySignupAsync 는 silent — 이미 있으면 무시. 성공 여부는 다음 LoginAsync 로 판정.
                await SessionApiClient.TrySignupAsync(id, pw, nick);

                bool loginOk = await SessionApiClient.LoginAsync(id, pw);
                if (!loginOk)
                {
                    SetStatus("회원가입 실패 또는 ID 중복. 다른 ID 로 시도해주세요.");
                    SetButtonsInteractable(true);
                    return;
                }

                bool meOk = await SessionApiClient.FetchMyUserIdAsync();
                if (!meOk)
                {
                    SetStatus("회원가입 OK. 단 내 정보 조회 실패.");
                    SetButtonsInteractable(true);
                    return;
                }

                RelaySession.AutoLoginId = id;
                RelaySession.AutoLoginPassword = pw;
                RelaySession.AutoLoginNickname = nick;

                LoadNextSceneAfterAuth("회원가입·로그인 성공");
            }
            catch (Exception ex)
            {
                NetLog.Error("Title", $"Signup threw: {ex.Message}");
                SetStatus("회원가입 중 오류: " + ex.Message);
                SetButtonsInteractable(true);
            }
        }

        private void LoadNextSceneAfterAuth(string authLabel)
        {
            bool introSeen = !forcePlayIntro && PlayerPrefs.GetInt(IntroSeenPrefKey, 0) == 1;
            string target;
            string statusSuffix;
            if (introSeen)
            {
                target = townSceneName;
                statusSuffix = "마을로 이동합니다...";
            }
            else
            {
                target = !string.IsNullOrEmpty(introSceneName) ? introSceneName : townSceneName;
                statusSuffix = target == introSceneName ? "인트로를 재생합니다..." : "마을로 이동합니다...";
            }

            SetStatus($"{authLabel}. {statusSuffix}");
            SceneManager.LoadScene(target);
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            NetLog.Info("Title", msg);
        }

        private void SetButtonsInteractable(bool value)
        {
            if (loginButton != null) loginButton.interactable = value;
            if (signupButton != null) signupButton.interactable = value;
        }
    }
}

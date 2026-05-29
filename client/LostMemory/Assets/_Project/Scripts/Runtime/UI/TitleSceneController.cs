using System;
using System.Threading.Tasks;
using LostMemory.Networking.Analytics;
using LostMemory.Networking.Common;
using LostMemory.Networking.Session;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// 타이틀 씬 컨트롤러 — 로그인 / 회원가입 / 이메일 인증 통합 UI.
    ///
    /// 3 state machine 으로 같은 영역(InputArea + ButtonArea)을 공유하며 입력 칸·버튼 라벨을 갈아끼운다.
    ///
    /// 상태:
    ///   LOGIN  : loginId · password 입력 + [로그인][회원가입]
    ///   SIGNUP : loginId · password · email · nickname 입력 + [가입][돌아가기]
    ///   VERIFY : 6자리 코드 입력 + [인증][돌아가기][코드 재발송 (60s 쿨다운)]
    ///
    /// 인증 성공 시 즉시 토큰을 받아 게임 씬으로 진입 — 별도 로그인 단계 없음.
    /// 코드 만료/시도 초과 시 SIGNUP 으로 복귀. 잘못된 코드는 VERIFY 유지.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Title Scene Controller")]
    public sealed class TitleSceneController : MonoBehaviour
    {
        private enum TitleState { LOGIN, SIGNUP, VERIFY }

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField loginIdInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField, Tooltip("SIGNUP 에서만 활성. LOGIN/VERIFY 에선 자동 비활성.")]
        private TMP_InputField emailInput;
        [SerializeField, Tooltip("SIGNUP 에서만 활성.")]
        private TMP_InputField nicknameInput;
        [SerializeField, Tooltip("VERIFY 에서만 활성. characterLimit=6 권장.")]
        private TMP_InputField codeInput;

        [Header("Buttons")]
        [SerializeField, Tooltip("상태별 라벨: 로그인 / 가입 / 인증")]
        private Button primaryButton;
        [SerializeField, Tooltip("상태별 라벨: 회원가입 / 돌아가기 / 돌아가기")]
        private Button secondaryButton;
        [SerializeField, Tooltip("VERIFY 에서만 활성. 60초 쿨다운 후 재발송 가능.")]
        private Button resendButton;

        [Header("Button Labels (TMP)")]
        [SerializeField] private TMP_Text primaryButtonLabel;
        [SerializeField] private TMP_Text secondaryButtonLabel;
        [SerializeField] private TMP_Text resendButtonLabel;

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
        private const float  ResendCooldownSeconds = 60f;

        private TitleState _state;
        // SIGNUP → VERIFY 캐리값 (인증 성공 후 RelaySession 정적 필드 sync 에도 사용)
        private string _pendingLoginId;
        private string _pendingPassword;
        private string _pendingEmail;
        private string _pendingNickname;
        // 재발송 쿨다운
        private float _resendCooldownRemaining;

        private void Awake()
        {
            if (primaryButton != null)   primaryButton.onClick.AddListener(OnPrimaryClicked);
            if (secondaryButton != null) secondaryButton.onClick.AddListener(OnSecondaryClicked);
            if (resendButton != null)    resendButton.onClick.AddListener(OnResendClicked);

            SetState(TitleState.LOGIN);
        }

        private void Start()
        {
            // L-1: Local Fallback 모드면 로그인 UI 자동 우회 → townSceneName 으로 직접 이동
            if (RelaySession.UseLocalFallback)
            {
                SetStatus($"[LOCAL FALLBACK] 로그인 우회 → {townSceneName}");
                SceneManager.LoadScene(townSceneName);
            }
        }

        private void Update()
        {
            if (_resendCooldownRemaining > 0f)
            {
                _resendCooldownRemaining -= Time.deltaTime;
                if (_resendCooldownRemaining <= 0f)
                {
                    _resendCooldownRemaining = 0f;
                    SetResendLabel("코드 재발송");
                    if (resendButton != null) resendButton.interactable = true;
                }
                else
                {
                    SetResendLabel($"재발송 ({Mathf.CeilToInt(_resendCooldownRemaining)}s)");
                }
            }
        }

        // ================================================================
        // State machine
        // ================================================================

        private void SetState(TitleState next)
        {
            _state = next;

            bool isLogin  = next == TitleState.LOGIN;
            bool isSignup = next == TitleState.SIGNUP;
            bool isVerify = next == TitleState.VERIFY;

            SetActive(loginIdInput,  isLogin || isSignup);
            SetActive(passwordInput, isLogin || isSignup);
            SetActive(emailInput,    isSignup);
            SetActive(nicknameInput, isSignup);
            SetActive(codeInput,     isVerify);
            SetActive(resendButton,  isVerify);

            switch (next)
            {
                case TitleState.LOGIN:
                    SetPrimaryLabel("로그인");
                    SetSecondaryLabel("회원가입");
                    SetStatus("로그인 또는 회원가입을 진행하세요.");
                    break;
                case TitleState.SIGNUP:
                    SetPrimaryLabel("가입");
                    SetSecondaryLabel("돌아가기");
                    SetStatus("회원가입 정보를 입력해주세요.");
                    break;
                case TitleState.VERIFY:
                    SetPrimaryLabel("인증");
                    SetSecondaryLabel("돌아가기");
                    SetResendLabel(_resendCooldownRemaining > 0f
                        ? $"재발송 ({Mathf.CeilToInt(_resendCooldownRemaining)}s)"
                        : "코드 재발송");
                    if (resendButton != null) resendButton.interactable = _resendCooldownRemaining <= 0f;
                    SetStatus("이메일로 받은 6자리 코드를 입력해주세요.");
                    break;
            }

            SetButtonsInteractable(true);
        }

        // ================================================================
        // 버튼 핸들러
        // ================================================================

        private async void OnPrimaryClicked()
        {
            if (RelaySession.UseLocalFallback)
            {
                SetStatus("[LOCAL FALLBACK] 인증 우회.");
                LoadNextSceneAfterAuth("[LOCAL FALLBACK]");
                return;
            }

            switch (_state)
            {
                case TitleState.LOGIN:  await HandleLogin();  break;
                case TitleState.SIGNUP: await HandleSignup(); break;
                case TitleState.VERIFY: await HandleVerify(); break;
            }
        }

        private void OnSecondaryClicked()
        {
            switch (_state)
            {
                case TitleState.LOGIN:  SetState(TitleState.SIGNUP); break;
                case TitleState.SIGNUP: SetState(TitleState.LOGIN);  break;
                case TitleState.VERIFY: SetState(TitleState.SIGNUP); break;
            }
        }

        private async void OnResendClicked()
        {
            if (_state != TitleState.VERIFY) return;
            if (string.IsNullOrEmpty(_pendingEmail)) return;

            SetButtonsInteractable(false);
            SetStatus("코드 재발송 중...");
            try
            {
                var resp = await SessionApiClient.ResendVerificationCodeAsync(_pendingEmail);
                if (resp != null && resp.success)
                {
                    SetStatus("인증 코드를 다시 보냈습니다. 메일함을 확인해주세요.");
                    StartResendCooldown();
                }
                else
                {
                    string code = resp?.error?.code;
                    SetStatus(code switch
                    {
                        "AUTH_VERIFICATION_SEND_TOO_FREQUENT" => "잠시 후 다시 시도해주세요.",
                        "AUTH_VERIFICATION_SEND_DAILY_LIMIT"  => "오늘 발송 횟수를 모두 사용했습니다.",
                        "AUTH_EMAIL_ALREADY_VERIFIED"         => "이미 인증된 계정입니다. 로그인 화면으로 이동합니다.",
                        _ => "재발송에 실패했습니다."
                    });
                    if (code == "AUTH_EMAIL_ALREADY_VERIFIED") SetState(TitleState.LOGIN);
                    else if (code == "AUTH_VERIFICATION_SEND_DAILY_LIMIT")
                    {
                        if (resendButton != null) resendButton.interactable = false;
                    }
                }
            }
            catch (Exception ex)
            {
                NetLog.Error("Title", $"Resend threw: {ex.Message}");
                SetStatus("재발송 중 오류: " + ex.Message);
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        // ================================================================
        // 상태별 흐름
        // ================================================================

        private async Task HandleLogin()
        {
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
                var resp = await SessionApiClient.LoginAsync(id, pw);
                if (resp != null && resp.success)
                {
                    if (!await FetchMyUserIdOrFail()) return;
                    RelaySession.AutoLoginId = id;
                    RelaySession.AutoLoginPassword = pw;
                    LoadNextSceneAfterAuth("로그인 성공");
                    return;
                }

                string code = resp?.error?.code;
                SetStatus(code switch
                {
                    "AUTH_EMAIL_NOT_VERIFIED" => "이메일 인증을 완료해주세요.",
                    "AUTH_INVALID_CREDENTIALS" => "아이디 또는 비밀번호가 올바르지 않습니다.",
                    _ => "로그인에 실패했습니다."
                });
            }
            catch (Exception ex)
            {
                NetLog.Error("Title", $"Login threw: {ex.Message}");
                SetStatus("로그인 중 오류: " + ex.Message);
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private async Task HandleSignup()
        {
            string id    = loginIdInput != null ? loginIdInput.text?.Trim() : null;
            string pw    = passwordInput != null ? passwordInput.text : null;
            string email = emailInput != null ? emailInput.text?.Trim() : null;
            string nick  = nicknameInput != null ? nicknameInput.text?.Trim() : null;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw)
                || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(nick))
            {
                SetStatus("모든 항목을 입력해주세요.");
                return;
            }

            // 같은 이메일 = 직전 가입의 staging 이 백엔드에 살아있음 (TTL 30분) → API 호출 없이 VERIFY 로 직행
            // 재발송이 필요하면 VERIFY 화면의 재발송 버튼 사용.
            if (!string.IsNullOrEmpty(_pendingEmail) && email == _pendingEmail)
            {
                SetState(TitleState.VERIFY);
                SetStatus("인증 코드가 이미 발송되었습니다. 메일함을 확인하거나 재발송을 눌러주세요.");
                return;
            }

            SetButtonsInteractable(false);
            SetStatus("회원가입 중...");
            try
            {
                var resp = await SessionApiClient.SignupAsync(id, pw, email, nick);
                if (resp != null && resp.success)
                {
                    _pendingLoginId  = id;
                    _pendingPassword = pw;
                    _pendingEmail    = email;
                    _pendingNickname = nick;
                    StartResendCooldown();   // 가입 직후 발송된 코드 — 쿨다운 시작
                    SetState(TitleState.VERIFY);
                    SetStatus("인증 코드가 이메일로 발송되었습니다.");
                    return;
                }

                string code = resp?.error?.code;
                SetStatus(code switch
                {
                    "USER_LOGIN_ID_DUPLICATED" => "이미 사용 중인 아이디입니다.",
                    "USER_EMAIL_DUPLICATED"    => "이미 사용 중인 이메일입니다.",
                    "USER_NICKNAME_DUPLICATED" => "이미 사용 중인 닉네임입니다.",
                    "AUTH_PASSWORD_POLICY_VIOLATION" => "비밀번호 정책을 확인해주세요 (영문·숫자·특수문자, 8자 이상).",
                    "AUTH_MAIL_DELIVERY_FAILED" => "인증 메일 발송에 실패했습니다. 잠시 후 다시 시도해주세요.",
                    "AUTH_VERIFICATION_SEND_TOO_FREQUENT" => "잠시 후 다시 시도해주세요.",
                    "AUTH_VERIFICATION_SEND_DAILY_LIMIT"  => "오늘 발송 가능한 횟수를 모두 사용했습니다.",
                    _ => "입력값을 다시 확인해주세요."
                });
            }
            catch (Exception ex)
            {
                NetLog.Error("Title", $"Signup threw: {ex.Message}");
                SetStatus("회원가입 중 오류: " + ex.Message);
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private async Task HandleVerify()
        {
            string code = codeInput != null ? codeInput.text?.Trim() : null;
            if (string.IsNullOrEmpty(code))
            {
                SetStatus("인증 코드를 입력해주세요.");
                return;
            }
            if (string.IsNullOrEmpty(_pendingEmail))
            {
                SetStatus("가입 정보가 만료되었습니다. 다시 진행해주세요.");
                SetState(TitleState.SIGNUP);
                return;
            }

            SetButtonsInteractable(false);
            SetStatus("인증 중...");
            try
            {
                var resp = await SessionApiClient.VerifyEmailAsync(_pendingEmail, code);
                if (resp != null && resp.success)
                {
                    if (!await FetchMyUserIdOrFail()) return;
                    RelaySession.AutoLoginId       = _pendingLoginId;
                    RelaySession.AutoLoginPassword = _pendingPassword;
                    RelaySession.AutoLoginEmail    = _pendingEmail;
                    RelaySession.AutoLoginNickname = _pendingNickname;
                    LoadNextSceneAfterAuth("인증 완료");
                    return;
                }

                string err = resp?.error?.code;
                switch (err)
                {
                    case "AUTH_VERIFICATION_CODE_INVALID":
                        SetStatus("코드가 일치하지 않습니다.");
                        break;
                    case "AUTH_VERIFICATION_CODE_EXPIRED":
                        SetStatus("코드가 만료되었습니다. 회원가입을 다시 진행해주세요.");
                        ClearPendingSignup();   // 백엔드 staging 도 만료됐으니 클라 캐리값도 비움
                        SetState(TitleState.SIGNUP);
                        break;
                    case "AUTH_VERIFICATION_ATTEMPTS_EXCEEDED":
                        SetStatus("시도 횟수를 초과했습니다. 회원가입을 다시 진행해주세요.");
                        ClearPendingSignup();   // 백엔드 staging 도 함께 무효화된 상태
                        SetState(TitleState.SIGNUP);
                        break;
                    default:
                        SetStatus("인증에 실패했습니다.");
                        break;
                }
            }
            catch (Exception ex)
            {
                NetLog.Error("Title", $"Verify threw: {ex.Message}");
                SetStatus("인증 중 오류: " + ex.Message);
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        /** SIGNUP → VERIFY 캐리값 초기화 — 백엔드 staging 이 사라진 시점에 호출해 다음 가입 시도가 새 흐름으로 진행되도록. */
        private void ClearPendingSignup()
        {
            _pendingLoginId = null;
            _pendingPassword = null;
            _pendingEmail = null;
            _pendingNickname = null;
        }

        private async Task<bool> FetchMyUserIdOrFail()
        {
            bool meOk = await SessionApiClient.FetchMyUserIdAsync();
            if (!meOk)
            {
                SetStatus("내 정보 조회 실패. 잠시 후 다시 시도해주세요.");
                SetButtonsInteractable(true);
                return false;
            }
            return true;
        }

        private void LoadNextSceneAfterAuth(string authLabel)
        {
            var analytics = AnalyticsClient.EnsureExists();
            analytics.Track("session_start", stageId: null, payload: new
            {
                party_size = 1,
                device = Application.platform.ToString(),
            });

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

        private void StartResendCooldown()
        {
            _resendCooldownRemaining = ResendCooldownSeconds;
            if (resendButton != null) resendButton.interactable = false;
            SetResendLabel($"재발송 ({Mathf.CeilToInt(_resendCooldownRemaining)}s)");
        }

        private static void SetActive(Component c, bool active)
        {
            if (c != null && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
        }

        private void SetPrimaryLabel(string text)
        {
            if (primaryButtonLabel != null) primaryButtonLabel.text = text;
        }

        private void SetSecondaryLabel(string text)
        {
            if (secondaryButtonLabel != null) secondaryButtonLabel.text = text;
        }

        private void SetResendLabel(string text)
        {
            if (resendButtonLabel != null) resendButtonLabel.text = text;
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            NetLog.Info("Title", msg);
        }

        private void SetButtonsInteractable(bool value)
        {
            if (primaryButton != null)   primaryButton.interactable = value;
            if (secondaryButton != null) secondaryButton.interactable = value;
            // resendButton 은 쿨다운 흐름에서 별도 관리
        }
    }
}

using System;
using System.Threading.Tasks;
using LostMemory.Multiplayer;
using LostMemory.Networking.Common;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 세션 공통 상태 + 초기화 진입점. Host/Client 양쪽이 공유.
    ///
    /// (자체 Relay 마이그레이션 후) 책임:
    ///   - 백엔드 로그인 1회 보장 (테스트 씬용 testuser 자동 로그인)
    ///   - 활성 세션 메타 보유 (sessionId, isHost)
    ///   - 이벤트 게시 (Joined / Left / Failed)
    ///
    /// 다중 인스턴스 테스트 (Multiplayer Play Mode) 시:
    ///   인스턴스마다 다른 자격증명 필요하면 AutoLoginId/Password/Nickname 을
    ///   인스턴스 시작 시 (Awake 등) 다르게 설정. 예) "testuser" / "testuser01" / ...
    /// </summary>
    public static class RelaySession
    {
        // 테스트 씬용 자동 로그인 자격증명. 본 게임에선 별도 로그인 화면으로 교체 예정.
        // 가입 시점에 본 정적 필드들이 백엔드로 전송되며, 가입 완료(이메일 인증) 이후의 자동 로그인 시도에도 사용된다.
        public static string AutoLoginId = "testuser";
        public static string AutoLoginPassword = "Pass123!";
        public static string AutoLoginEmail = "testuser@local.test";
        public static string AutoLoginNickname = "테스터";

        public static long? ActiveSessionId { get; internal set; }
        public static bool IsHost { get; internal set; }
        public static bool IsInSession => ActiveSessionId.HasValue;

        /// <summary>현재 활성 세션의 입장 코드. 호스트는 생성 시·게스트는 join 시 set. Leave 시 null.</summary>
        public static string ActiveJoinCode { get; internal set; }

        // ============================================================
        // L-1: 백엔드 우회 Local Fallback (Editor 전용)
        // ============================================================

        /// <summary>
        /// EDITOR-ONLY: true 면 backend API / Relay 우회 + UTP localhost 자동 swap.
        /// 빌드에서는 항상 false (compile-out). 메뉴: Lost Memory > Toggle Local Fallback.
        /// </summary>
        public static bool UseLocalFallback
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool("LostMemory.RelaySession.UseLocalFallback", false);
#else
                return false;
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetBool("LostMemory.RelaySession.UseLocalFallback", value);
#endif
            }
        }

        /// <summary>fallback 가짜 userId (PID 기반 → 인스턴스마다 다름).</summary>
        public static ulong LocalFallbackUserId
            => (ulong)System.Diagnostics.Process.GetCurrentProcess().Id;

        public static string LocalFallbackNickname
        {
            get
            {
                int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                return $"테스터{pid % 100:00}";
            }
        }

        public static string LocalFallbackAddress = "127.0.0.1";
        public static ushort LocalFallbackPort = 7777;

        /// <summary>NetworkTransport 슬롯을 UnityTransport 로 동적 교체. prefab 영구 변경 없음.</summary>
        internal static bool SwapToUnityTransport(out string error)
        {
            error = null;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) { error = "NetworkManager.Singleton == null"; return false; }
            if (nm.IsListening) { error = "NetworkManager 이미 활성 — swap 불가"; return false; }

            UnityTransport utp = nm.GetComponent<UnityTransport>();
            if (utp == null) utp = nm.gameObject.AddComponent<UnityTransport>();
            utp.SetConnectionData(LocalFallbackAddress, LocalFallbackPort);
            nm.NetworkConfig.NetworkTransport = utp;
            NetLog.Info("Transport", $"[LOCAL FALLBACK] UnityTransport swapped to {LocalFallbackAddress}:{LocalFallbackPort}");
            return true;
        }

        /// <summary>NetworkTransport 를 LostMemoryRelayTransport 로 복원 (정상 모드 진입 시 idempotent).</summary>
        internal static bool RestoreRelayTransport(out string error)
        {
            error = null;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) { error = "NetworkManager.Singleton == null"; return false; }
            if (nm.IsListening) { error = "NetworkManager 이미 활성"; return false; }

            LostMemoryRelayTransport lrt = nm.GetComponent<LostMemoryRelayTransport>();
            if (lrt == null) { error = "LostMemoryRelayTransport 미부착"; return false; }
            nm.NetworkConfig.NetworkTransport = lrt;
            return true;
        }

        /// <summary>호스트/클라 진입 성공 시 발화. 인자: 본인이 호스트인지.</summary>
        public static event Action<bool> Joined;

        /// <summary>세션을 떠난 후 발화. UI 정리·씬 복귀 등에 사용.</summary>
        public static event Action Left;

        /// <summary>실패 시 발화. SessionErrorPolicy.ToUserMessage 로 사용자 메시지 변환 가능.</summary>
        public static event Action<SessionErrorKind, string> Failed;

        /// <summary>
        /// 백엔드 로그인 + myUserId 확보. 여러 번 호출돼도 안전 (캐시).
        /// 실패 시 Failed 이벤트 발화 + 예외 throw.
        /// </summary>
        internal static async Task EnsureInitializedAsync()
        {
            // L-1: Local Fallback — 백엔드 skip. AutoLoginNickname 만 fallback 닉네임으로.
            if (UseLocalFallback)
            {
                AutoLoginNickname = LocalFallbackNickname;
                NetLog.Info("Session", $"[LOCAL FALLBACK] init skipped. nick='{AutoLoginNickname}' userId={LocalFallbackUserId}");
                await Task.CompletedTask;
                return;
            }

            if (SessionApiClient.IsLoggedIn) return;

            // signup (이미 가입된 계정이면 409 — 결과 무시하고 다음 login 으로 판정)
            // 단, 신규 가입의 경우 status=pending 으로 저장되며 이메일 인증 전까지 login 이 실패한다.
            // 자동 로그인을 쓰려면 사전에 Title 흐름을 통해 한 번 이상 ACTIVE 로 전환해둬야 한다.
            try
            {
                await SessionApiClient.SignupAsync(
                    AutoLoginId, AutoLoginPassword, AutoLoginEmail, AutoLoginNickname);
            }
            catch (Exception ex)
            {
                NetLog.Warn("Session", $"signup threw (무시): {ex.Message}");
            }

            // login
            var loginResp = await SessionApiClient.LoginAsync(AutoLoginId, AutoLoginPassword);
            if (loginResp == null || !loginResp.success)
            {
                string code = loginResp?.error?.code;
                string detail = code == "AUTH_EMAIL_NOT_VERIFIED"
                    ? "이메일 인증 미완료 — Title 화면에서 인증 후 재시도"
                    : "백엔드 로그인 실패";
                RaiseFailed(SessionErrorKind.SignInFailed, detail);
                throw new Exception($"EnsureInitialized: backend login failed ({code})");
            }

            // myUserId
            bool meOk = await SessionApiClient.FetchMyUserIdAsync();
            if (!meOk)
            {
                RaiseFailed(SessionErrorKind.SignInFailed, "users/me 실패");
                throw new Exception("EnsureInitialized: fetch /users/me failed");
            }

            NetLog.Info("Session", $"EnsureLoggedIn: backend login OK ({AutoLoginId}, userId={SessionApiClient.MyUserId})");
        }

        /// <summary>
        /// 활성 세션을 떠난다. 호스트면 백엔드에 DELETE 까지 (CASCADE 로 session_joins 등 정리됨).
        /// 게스트면 메타만 클리어 (백엔드 측 session_joins 정리는 호스트 DELETE 또는 timeout 에 위임).
        /// </summary>
        public static async Task LeaveAsync()
        {
            long? sessionId = ActiveSessionId;
            if (!sessionId.HasValue) return;

            // L-1: Local Fallback — backend DELETE/leave 호출 skip.
            if (UseLocalFallback)
            {
                NetLog.Info("Session", $"[LOCAL FALLBACK] Leave skipped backend call.");
                ActiveSessionId = null;
                IsHost = false;
                ActiveJoinCode = null;
                RaiseLeft();
                await Task.CompletedTask;
                return;
            }

            try
            {
                if (IsHost)
                {
                    await SessionApiClient.DeleteSessionAsync(sessionId.Value);
                    NetLog.Info("Session", $"Host deleted session id={sessionId.Value}");
                }
                else
                {
                    await SessionApiClient.LeaveSessionAsync(sessionId.Value);
                    NetLog.Info("Session", $"Guest left session id={sessionId.Value}");
                }
            }
            catch (Exception ex)
            {
                NetLog.Warn("Session", $"Leave/DeleteSession threw: {ex.Message}");
            }
            finally
            {
                ActiveSessionId = null;
                IsHost = false;
                ActiveJoinCode = null;
                RaiseLeft();
            }
        }

        internal static void RaiseJoined(bool asHost)
        {
            try { Joined?.Invoke(asHost); }
            catch (Exception ex) { NetLog.Error("Session", $"Joined handler threw: {ex.Message}"); }
        }

        internal static void RaiseLeft()
        {
            try { Left?.Invoke(); }
            catch (Exception ex) { NetLog.Error("Session", $"Left handler threw: {ex.Message}"); }
        }

        internal static void RaiseFailed(SessionErrorKind kind, string detail = null)
        {
            try { Failed?.Invoke(kind, detail); }
            catch (Exception ex) { NetLog.Error("Session", $"Failed handler threw: {ex.Message}"); }
        }
    }
}

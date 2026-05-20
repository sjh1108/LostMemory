using System;
using System.Threading.Tasks;
using LostMemory.Networking.Common;

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
        public static string AutoLoginId = "testuser";
        public static string AutoLoginPassword = "password123";
        public static string AutoLoginNickname = "테스터";

        public static long? ActiveSessionId { get; internal set; }
        public static bool IsHost { get; internal set; }
        public static bool IsInSession => ActiveSessionId.HasValue;

        /// <summary>현재 활성 세션의 입장 코드. 호스트는 생성 시·게스트는 join 시 set. Leave 시 null.</summary>
        public static string ActiveJoinCode { get; internal set; }

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
            if (SessionApiClient.IsLoggedIn) return;

            // signup (이미 있으면 silent)
            try
            {
                await SessionApiClient.TrySignupAsync(AutoLoginId, AutoLoginPassword, AutoLoginNickname);
            }
            catch (Exception ex)
            {
                NetLog.Warn("Session", $"signup threw (무시): {ex.Message}");
            }

            // login
            bool loginOk = await SessionApiClient.LoginAsync(AutoLoginId, AutoLoginPassword);
            if (!loginOk)
            {
                RaiseFailed(SessionErrorKind.SignInFailed, "백엔드 로그인 실패");
                throw new Exception("EnsureInitialized: backend login failed");
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

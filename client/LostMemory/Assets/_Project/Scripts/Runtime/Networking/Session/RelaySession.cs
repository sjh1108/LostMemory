using System;
using System.Threading.Tasks;
using LostMemory.Networking.Common;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 세션 공통 상태 + 초기화 진입점. Host/Client 양쪽이 공유.
    ///
    /// 책임:
    ///   - Unity Services 1회 초기화 보장
    ///   - 익명 로그인 보장 (MVP — 로그인 흐름 본격 도입 전)
    ///   - 활성 ISession 보유 (Host 또는 Client 진입 후 set)
    ///   - 이벤트 게시 (Joined / Left / Failed)
    /// </summary>
    public static class RelaySession
    {
        public static ISession Active { get; internal set; }

        public static bool IsInSession => Active != null;

        /// <summary>호스트/클라 진입 성공 시 발화. 인자: 본인이 호스트인지.</summary>
        public static event Action<bool> Joined;

        /// <summary>세션을 떠난 후 발화 (자발/타발 모두). UI 정리·씬 복귀 등에 사용.</summary>
        public static event Action Left;

        /// <summary>실패 시 발화. SessionErrorPolicy.ToUserMessage 로 사용자 메시지 변환 가능.</summary>
        public static event Action<SessionErrorKind, string> Failed;

        /// <summary>
        /// Unity Services 초기화 + 익명 로그인 보장. 여러 번 호출되어도 안전.
        /// 실패 시 Failed 이벤트 발화 + 예외 throw.
        /// </summary>
        internal static async Task EnsureInitializedAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    NetLog.Info("Session", "Initializing UnityServices...");
                    await UnityServices.InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                NetLog.Error("Session", $"UnityServices init failed: {ex.Message}");
                RaiseFailed(SessionErrorKind.ServicesInitFailed, ex.Message);
                throw;
            }

            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    NetLog.Info("Session", "Signing in anonymously...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    NetLog.Info("Session", $"Signed in. PlayerId={AuthenticationService.Instance.PlayerId}");
                }
            }
            catch (Exception ex)
            {
                NetLog.Error("Session", $"SignIn failed: {ex.Message}");
                RaiseFailed(SessionErrorKind.SignInFailed, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 활성 세션을 떠난다. 자발적 종료 경로. 결과적으로 NGO 도 정리됨.
        /// </summary>
        public static async Task LeaveAsync()
        {
            ISession session = Active;
            if (session == null) return;

            try
            {
                await session.LeaveAsync();
                NetLog.Info("Session", "Left session.");
            }
            catch (Exception ex)
            {
                NetLog.Warn("Session", $"LeaveAsync threw: {ex.Message}");
            }
            finally
            {
                Active = null;
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

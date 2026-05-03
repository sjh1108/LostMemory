using System;
using System.Threading.Tasks;
using LostMemory.Networking.Common;
using Unity.Services.Multiplayer;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 코드 입력 기반 세션 참가 진입점. Relay + NGO StartClient 까지 묶어서 처리한다.
    ///
    /// 사용 예: <c>var result = await RelaySessionClient.JoinByCodeAsync(code);</c>
    /// </summary>
    public static class RelaySessionClient
    {
        public readonly struct JoinResult
        {
            public readonly bool Success;
            public readonly SessionErrorKind ErrorKind;
            public readonly string ErrorDetail;

            public JoinResult(bool success, SessionErrorKind kind, string detail)
            {
                Success = success;
                ErrorKind = kind;
                ErrorDetail = detail;
            }

            public static JoinResult Ok() => new JoinResult(true, SessionErrorKind.None, null);
            public static JoinResult Fail(SessionErrorKind kind, string detail) => new JoinResult(false, kind, detail);
        }

        public static async Task<JoinResult> JoinByCodeAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                NetLog.Warn("Client", "JoinByCodeAsync called with empty code.");
                return JoinResult.Fail(SessionErrorKind.InvalidJoinCode, null);
            }

            if (RelaySession.Active != null)
            {
                NetLog.Warn("Client", "Already in a session. Ignoring JoinByCodeAsync.");
                return JoinResult.Fail(SessionErrorKind.Unknown, "Already in a session.");
            }

            try
            {
                await RelaySession.EnsureInitializedAsync();
            }
            catch (Exception ex)
            {
                return JoinResult.Fail(SessionErrorPolicy.Classify(ex), ex.Message);
            }

            string normalized = joinCode.Trim().ToUpperInvariant();

            try
            {
                NetLog.Info("Client", $"Joining session by code={normalized}...");
                ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(normalized);
                RelaySession.Active = session;

                NetLog.Info("Client", "Joined session.");
                RelaySession.RaiseJoined(asHost: false);
                return JoinResult.Ok();
            }
            catch (Exception ex)
            {
                NetLog.Error("Client", $"JoinSessionByCodeAsync failed: {ex.Message}");
                SessionErrorKind kind = SessionErrorPolicy.Classify(ex);
                if (kind == SessionErrorKind.Unknown)
                {
                    kind = SessionErrorKind.JoinCodeNotFound;
                }
                RelaySession.RaiseFailed(kind, ex.Message);
                return JoinResult.Fail(kind, ex.Message);
            }
        }
    }
}

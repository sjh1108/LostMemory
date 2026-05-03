using System;
using System.Threading.Tasks;
using LostMemory.Networking.Common;
using Unity.Services.Multiplayer;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 호스트 세션 생성 진입점. Relay + NGO StartHost 까지 묶어서 처리하고 참여 코드를 반환한다.
    ///
    /// 사용 예: <c>var result = await RelaySessionHost.CreateAsync(2);</c>
    /// </summary>
    public static class RelaySessionHost
    {
        public readonly struct CreateResult
        {
            public readonly bool Success;
            public readonly string JoinCode;
            public readonly SessionErrorKind ErrorKind;
            public readonly string ErrorDetail;

            public CreateResult(bool success, string joinCode, SessionErrorKind kind, string detail)
            {
                Success = success;
                JoinCode = joinCode;
                ErrorKind = kind;
                ErrorDetail = detail;
            }

            public static CreateResult Ok(string code) => new CreateResult(true, code, SessionErrorKind.None, null);
            public static CreateResult Fail(SessionErrorKind kind, string detail) => new CreateResult(false, null, kind, detail);
        }

        /// <summary>
        /// 호스트 생성 + Relay 할당 + 참여 코드 발급. 성공 시 NGO 가 호스트 모드로 활성화된다.
        /// </summary>
        public static async Task<CreateResult> CreateAsync(int maxPlayers, string sessionName = "LostMemorySession")
        {
            if (RelaySession.Active != null)
            {
                NetLog.Warn("Host", "Already in a session. Ignoring CreateAsync.");
                return CreateResult.Fail(SessionErrorKind.Unknown, "Already in a session.");
            }

            try
            {
                await RelaySession.EnsureInitializedAsync();
            }
            catch (Exception ex)
            {
                return CreateResult.Fail(SessionErrorPolicy.Classify(ex), ex.Message);
            }

            try
            {
                var options = new SessionOptions
                {
                    Name = sessionName,
                    MaxPlayers = maxPlayers
                }.WithRelayNetwork();

                NetLog.Info("Host", $"Creating session (max={maxPlayers}, name={sessionName})...");
                ISession session = await MultiplayerService.Instance.CreateSessionAsync(options);
                RelaySession.Active = session;

                string joinCode = session.Code;
                NetLog.Info("Host", $"Session created. JoinCode={joinCode}");
                RelaySession.RaiseJoined(asHost: true);
                return CreateResult.Ok(joinCode);
            }
            catch (Exception ex)
            {
                NetLog.Error("Host", $"CreateSessionAsync failed: {ex.Message}");
                SessionErrorKind kind = SessionErrorPolicy.Classify(ex);
                if (kind == SessionErrorKind.Unknown)
                {
                    kind = SessionErrorKind.RelayAllocateFailed;
                }
                RelaySession.RaiseFailed(kind, ex.Message);
                return CreateResult.Fail(kind, ex.Message);
            }
        }
    }
}

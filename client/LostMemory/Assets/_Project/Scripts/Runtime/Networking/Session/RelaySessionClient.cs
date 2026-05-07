using System;
using System.Threading.Tasks;
using LostMemory.Multiplayer;
using LostMemory.Networking.Common;
using Unity.Netcode;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 게스트 입장 진입점. 백엔드로 코드 → sessionId 조회 → join → Transport 설정 → NGO StartClient.
    ///
    /// 흐름:
    ///   1. 백엔드 로그인 보장
    ///   2. GET /api/sessions/find?code= → sessionId 획득
    ///   3. POST /api/sessions/{id}/join → sessionToken 획득 + members 받음
    ///   4. members 에서 호스트 userId 추출
    ///   5. LostMemoryRelayTransport.SetSession + SetHostUserId
    ///   6. NetworkManager.StartClient() → Relay 핸드셰이크 + NGO 클라이언트 활성화
    /// </summary>
    public static class RelaySessionClient
    {
        public readonly struct JoinResult
        {
            public readonly bool Success;
            public readonly long SessionId;
            public readonly SessionErrorKind ErrorKind;
            public readonly string ErrorDetail;

            public JoinResult(bool success, long sessionId, SessionErrorKind kind, string detail)
            {
                Success = success;
                SessionId = sessionId;
                ErrorKind = kind;
                ErrorDetail = detail;
            }

            public static JoinResult Ok(long sessionId)
                => new JoinResult(true, sessionId, SessionErrorKind.None, null);

            public static JoinResult Fail(SessionErrorKind kind, string detail)
                => new JoinResult(false, 0, kind, detail);
        }

        public static async Task<JoinResult> JoinByCodeAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                NetLog.Warn("Client", "JoinByCodeAsync called with empty code.");
                return JoinResult.Fail(SessionErrorKind.InvalidJoinCode, null);
            }

            if (RelaySession.IsInSession)
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
                // 1. 코드로 sessionId 조회
                NetLog.Info("Client", $"Finding session by code={normalized}...");
                var find = await SessionApiClient.FindByCodeAsync(normalized);
                if (find == null)
                {
                    return JoinResult.Fail(SessionErrorKind.JoinCodeNotFound, "코드에 해당하는 세션 없음");
                }
                if (find.isFull)
                {
                    return JoinResult.Fail(SessionErrorKind.SessionFull, "정원 가득");
                }

                // 2. join 호출
                NetLog.Info("Client", $"Joining session id={find.sessionId}...");
                var data = await SessionApiClient.JoinSessionAsync(find.sessionId, normalized);
                if (data == null)
                {
                    return JoinResult.Fail(SessionErrorKind.JoinCodeNotFound, "백엔드 join 실패");
                }

                // 3. 호스트 userId 추출
                ulong hostUserId = 0;
                if (data.members != null)
                {
                    foreach (var m in data.members)
                    {
                        if (m.role == "HOST")
                        {
                            hostUserId = (ulong)m.userId;
                            break;
                        }
                    }
                }

                // 4. Transport 셋업
                var transport = NetworkManager.Singleton.GetComponent<LostMemoryRelayTransport>();
                if (transport == null)
                {
                    return JoinResult.Fail(SessionErrorKind.TransportStartFailed,
                        "LostMemoryRelayTransport 컴포넌트가 NetworkManager 에 부착돼있지 않음");
                }
                transport.SetSession(data.sessionToken, (ulong)SessionApiClient.MyUserId);
                if (hostUserId != 0)
                {
                    transport.SetHostUserId(hostUserId);
                }

                // 5. NGO 클라이언트 시작
                bool started = NetworkManager.Singleton.StartClient();
                if (!started)
                {
                    return JoinResult.Fail(SessionErrorKind.TransportStartFailed, "NetworkManager.StartClient() 실패");
                }

                RelaySession.ActiveSessionId = data.sessionId;
                RelaySession.IsHost = false;
                RelaySession.RaiseJoined(asHost: false);

                NetLog.Info("Client", $"Joined session id={data.sessionId}.");
                return JoinResult.Ok(data.sessionId);
            }
            catch (Exception ex)
            {
                NetLog.Error("Client", $"JoinByCodeAsync 실패: {ex.Message}");
                SessionErrorKind kind = SessionErrorPolicy.Classify(ex);
                if (kind == SessionErrorKind.Unknown) kind = SessionErrorKind.JoinCodeNotFound;
                RelaySession.RaiseFailed(kind, ex.Message);
                return JoinResult.Fail(kind, ex.Message);
            }
        }
    }
}

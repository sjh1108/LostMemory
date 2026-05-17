using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using LostMemory.Networking.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// LostMemory 백엔드 (싸피 EC2) 의 Sessions/Auth API HTTP 클라이언트.
    ///
    /// 정적 클래스 — 한 번 LoginAsync 후 accessToken 을 내부 캐시. 후속 호출은 자동으로 Bearer 헤더 부착.
    /// 다중 인스턴스 테스트 (Multiplayer Play Mode) 시 인스턴스마다 별도 자격증명 필요하면
    /// RelaySession.AutoLoginId 등 정적 필드를 인스턴스별로 다르게 설정할 것.
    ///
    /// 의존: Newtonsoft.Json (com.unity.nuget.newtonsoft-json)
    /// </summary>
    public static class SessionApiClient
    {
        /// <summary>
        /// 백엔드 베이스 URL.
        /// Editor (Play 모드 포함) → dev (localhost:8080), Build → prod (k14c201.p.ssafy.io).
        /// 런타임에 강제로 다른 URL 쓰려면 외부에서 직접 대입 가능 (정적 필드).
        /// </summary>
#if UNITY_EDITOR
        public static string BaseUrl = "http://localhost:8080/api";
#else
        public static string BaseUrl = "https://k14c201.p.ssafy.io/api";
#endif

        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string AccessToken { get; private set; }
        public static long MyUserId { get; private set; }
        public static string MyNickname { get; private set; }

        public static bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken) && MyUserId != 0;

        // ============================================================
        // 인증
        // ============================================================

        /// <summary>signup 시도. 이미 가입된 계정이면 silent 실패 (login 으로 진행 가능).</summary>
        public static async Task TrySignupAsync(string loginId, string password, string nickname)
        {
            var body = new { loginId, password, nickname };
            await PostAsync<object>("/auth/signup", body, requireAuth: false);
            // 결과 무시 — 409 (LOGIN_ID_DUPLICATED / NICKNAME_DUPLICATED) 면 login 으로 진행
        }

        public static async Task<bool> LoginAsync(string loginId, string password)
        {
            var body = new { loginId, password };
            var resp = await PostAsync<TokenData>("/auth/login", body, requireAuth: false);
            if (resp == null || !resp.success || resp.data == null)
            {
                NetLog.Error("API", $"login 실패: code={resp?.error?.code}");
                return false;
            }
            AccessToken = resp.data.accessToken;
            return true;
        }

        public static async Task<bool> FetchMyUserIdAsync()
        {
            var resp = await GetAsync<UserMeData>("/users/me");
            if (resp == null || !resp.success || resp.data == null)
            {
                NetLog.Error("API", $"users/me 실패: code={resp?.error?.code}");
                return false;
            }
            MyUserId = resp.data.userId;
            MyNickname = resp.data.nickname;
            return true;
        }

        // ============================================================
        // Sessions
        // ============================================================

        public static async Task<SessionResponseData> CreateSessionAsync(int maxPlayers, string privateCode)
        {
            var body = new { maxPlayers, privateCode };
            var resp = await PostAsync<SessionResponseData>("/sessions", body);
            if (resp == null || !resp.success)
            {
                NetLog.Error("API", $"create session 실패: code={resp?.error?.code}");
                return null;
            }
            return resp.data;
        }

        public static async Task<SessionFindData> FindByCodeAsync(string privateCode)
        {
            var url = $"/sessions/find?code={Uri.EscapeDataString(privateCode)}";
            var resp = await GetAsync<SessionFindData>(url);
            if (resp == null || !resp.success)
            {
                NetLog.Warn("API", $"find session 실패: code={resp?.error?.code}");
                return null;
            }
            return resp.data;
        }

        public static async Task<SessionResponseData> JoinSessionAsync(long sessionId, string privateCode)
        {
            var body = new { privateCode };
            var resp = await PostAsync<SessionResponseData>($"/sessions/{sessionId}/join", body);
            if (resp == null || !resp.success)
            {
                NetLog.Error("API", $"join session 실패: code={resp?.error?.code}");
                return null;
            }
            return resp.data;
        }

        public static async Task DeleteSessionAsync(long sessionId)
        {
            await DeleteAsync($"/sessions/{sessionId}");
        }

        /// <summary>게스트 자발 이탈. 백엔드가 본인 SessionJoin 만 삭제 → 정원 카운트 회복.</summary>
        public static async Task LeaveSessionAsync(long sessionId)
        {
            await PostAsync<object>($"/sessions/{sessionId}/leave", new { });
        }

        // ============================================================
        // HTTP helpers
        // ============================================================

        private static async Task<ApiEnvelope<T>> PostAsync<T>(string path, object body, bool requireAuth = true)
        {
            var json = JsonConvert.SerializeObject(body, jsonSettings);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path) { Content = content };
            if (requireAuth) AddAuth(req);
            return await SendAndParseAsync<T>(req);
        }

        private static async Task<ApiEnvelope<T>> GetAsync<T>(string path)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
            AddAuth(req);
            return await SendAndParseAsync<T>(req);
        }

        private static async Task DeleteAsync(string path)
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, BaseUrl + path);
            AddAuth(req);
            await SendAndParseAsync<object>(req);
        }

        private static void AddAuth(HttpRequestMessage req)
        {
            if (!string.IsNullOrEmpty(AccessToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            }
        }

        private static async Task<ApiEnvelope<T>> SendAndParseAsync<T>(HttpRequestMessage req)
        {
            try
            {
                using var resp = await http.SendAsync(req);
                var text = await resp.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(text))
                {
                    return new ApiEnvelope<T> { success = resp.IsSuccessStatusCode };
                }
                return JsonConvert.DeserializeObject<ApiEnvelope<T>>(text, jsonSettings);
            }
            catch (Exception ex)
            {
                NetLog.Error("API", $"{req.Method} {req.RequestUri.AbsolutePath} 실패: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // 응답 DTO — 백엔드 ApiResponse / 도메인 응답과 1:1 매핑
        // ============================================================

        [Serializable]
        public class ApiEnvelope<T>
        {
            public bool success;
            public T data;
            public ErrorDetail error;
        }

        [Serializable]
        public class ErrorDetail
        {
            public string code;
            public string message;
        }

        [Serializable]
        public class TokenData
        {
            public string accessToken;
            public string refreshToken;
            public long accessTokenExpiresIn;
        }

        [Serializable]
        public class UserMeData
        {
            public long userId;
            public string loginId;
            public string nickname;
            public string status;
        }

        [Serializable]
        public class SessionResponseData
        {
            public long sessionId;
            public long hostId;
            public int maxPlayers;
            public string privateCode;
            public string sessionToken;
            public long sessionTokenExpiresIn;
            public SessionMember[] members;
        }

        [Serializable]
        public class SessionMember
        {
            public long userId;
            public string nickname;
            public string role; // "HOST" or "GUEST"
            public string joinedAt;
        }

        [Serializable]
        public class SessionFindData
        {
            public long sessionId;
            public long hostId;
            public int maxPlayers;
            public long currentMembers;
            public bool isFull;
        }
    }
}

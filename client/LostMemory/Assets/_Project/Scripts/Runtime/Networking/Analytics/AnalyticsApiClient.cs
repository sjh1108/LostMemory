using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using LostMemory.Networking.Common;
using LostMemory.Networking.Session;
using Newtonsoft.Json;

namespace LostMemory.Networking.Analytics
{
    /// <summary>
    /// Player Analytics 전용 HTTP 클라이언트. POST /api/analytics/events 한 종류만.
    ///
    /// 인증 토큰은 SessionApiClient.AccessToken 재사용 — 로그인은 타이틀에서 한 번만 함.
    /// BaseUrl 도 SessionApiClient 의 것을 그대로 따라가, 환경 분기를 한 곳에서만 관리.
    /// </summary>
    public static class AnalyticsApiClient
    {
        private const string EndpointPath = "/analytics/events";

        // 짧게 — POST 가 느리면 게임 hitch 유발. 5초로 컷.
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public enum PostResult
        {
            Success,        // 2xx
            ClientError,    // 4xx — batch 폐기
            ServerError,    // 5xx 또는 네트워크 실패 — 재시도 대상
        }

        /// <summary>
        /// 이벤트 batch POST. 성공/4xx/5xx-or-network 3단계 결과 반환.
        /// 호출자는 ServerError 만 재시도하면 됨.
        /// </summary>
        public static async Task<PostResult> PostEventsAsync(AnalyticsBatchBody body)
        {
            string url = SessionApiClient.BaseUrl + EndpointPath;
            string json = JsonConvert.SerializeObject(body, jsonSettings);

            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                AddAuth(req);

                using var resp = await http.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                {
                    return PostResult.Success;
                }
                int status = (int)resp.StatusCode;
                if (status >= 400 && status < 500)
                {
                    NetLog.Warn("Analytics", $"POST {EndpointPath} {status} — batch 폐기 ({body.events?.Count ?? 0}개)");
                    return PostResult.ClientError;
                }
                NetLog.Warn("Analytics", $"POST {EndpointPath} {status} — 재시도 대상");
                return PostResult.ServerError;
            }
            catch (Exception ex)
            {
                NetLog.Warn("Analytics", $"POST {EndpointPath} 실패: {ex.Message} — 재시도 대상");
                return PostResult.ServerError;
            }
        }

        private static void AddAuth(HttpRequestMessage req)
        {
            string token = SessionApiClient.AccessToken;
            if (!string.IsNullOrEmpty(token))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
    }
}

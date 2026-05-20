using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LostMemory.Networking.Session;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace LostMemory.Networking.Llm
{
    /// <summary>
    /// OpenAI 호환 Chat Completions API 클라이언트.
    /// Ollama(로컬)와 vLLM(GPU 클라우드) 모두 동일 코드로 사용 — BaseUrl 과 ApiKey 만 교체하면 됨.
    ///
    /// [초심자 설명]
    ///   이 클래스는 LLM 서버에 "대화 내용"을 보내고 "NPC 응답"을 받아오는 창구입니다.
    ///   마치 카카오톡 서버에 메시지를 보내고 답장을 받는 것과 같습니다.
    ///
    ///   두 가지 방식을 제공합니다:
    ///   - ChatAsync()          : 응답이 완성된 후 한 번에 반환 (테스트/빠른 구현용)
    ///   - ChatStreamCoroutine(): 응답 글자가 도착할 때마다 실시간 전달 (타이핑 효과용)
    ///
    /// 의존: Newtonsoft.Json (com.unity.nuget.newtonsoft-json)
    /// </summary>
    public static class LlmApiClient
    {
        // ── 엔드포인트 설정 ────────────────────────────────────────────────────
        // 모든 요청은 백엔드 EC2 의 LLM 프록시(/api/llm/chat) 를 거쳐 RunPod vLLM 으로 도달.
        // 백엔드가 SSE 스트리밍 pass-through + stream=true 강제. 인증만 Editor/Build 분기.
        public static string BaseUrl   = "https://k14c201.p.ssafy.io/api/llm";
        public static string ModelName = "bllossom-8b"; // ⚠️ vLLM 등록 이름. HF id 아님

#if UNITY_EDITOR
        // Editor Play 모드 임시 인증 — 로그인 기능 완성되면 제거 예정.
        // ⚠️ 이 문자열은 절대 깃허브에 커밋 금지. dev 환경에서만 작동하지만 노출 시 우회 가능.
        public static string DevBypassToken = "REPLACE-WITH-DEV-BYPASS-TOKEN";
#endif

        // LLM 응답은 느릴 수 있어 60초 타임아웃 (SessionApiClient 는 10초)
        private static readonly HttpClient http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        // ── 비스트리밍: 응답 완성 후 한 번에 반환 ─────────────────────────────

        /// <summary>
        /// LLM 에 메시지 목록을 보내고 완성된 응답 문자열을 반환한다.
        /// 실패 시 null 반환. 코루틴에서 쓰려면 while (!task.IsCompleted) yield return null; 패턴 사용.
        /// </summary>
        public static async Task<string> ChatAsync(
            List<ChatMessage> messages,
            float temperature = 0.75f,
            int maxTokens = 250,
            CancellationToken cancellationToken = default)
        {
            var requestBody = new ChatRequest
            {
                model       = ModelName,
                messages    = messages,
                temperature = temperature,
                max_tokens  = maxTokens,
                stream      = false
            };

            var json    = JsonConvert.SerializeObject(requestBody, jsonSettings);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/chat")
            {
                Content = content
            };
#if UNITY_EDITOR
            request.Headers.Add("X-Dev-Bypass", DevBypassToken);
#else
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SessionApiClient.AccessToken);
#endif

            try
            {
                using var response = await http.SendAsync(request, cancellationToken);
                var body   = await response.Content.ReadAsStringAsync();
                var parsed = JsonConvert.DeserializeObject<ChatResponse>(body, jsonSettings);

                var msg = parsed?.choices?[0]?.message;
                if (msg == null) return null;

                // Qwen3 계열: content 가 비고 reasoning_content 만 있는 경우 경고.
                // /no_think 토큰을 시스템 프롬프트 끝에 넣어 thinking 모드를 끄세요.
                if (string.IsNullOrEmpty(msg.content) && !string.IsNullOrEmpty(msg.reasoning_content))
                {
                    Debug.LogWarning(
                        "[LlmApiClient] content 가 비어있고 reasoning_content 만 반환됨. " +
                        "모델이 thinking 모드로 동작 중. 시스템 프롬프트 끝에 '/no_think' 추가 권장.");
                }

                return msg.content;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LlmApiClient] ChatAsync 실패: {ex.Message}");
                return null;
            }
        }

        // ── 스트리밍: 글자가 도착할 때마다 실시간 전달 (타이핑 효과) ──────────

        /// <summary>
        /// LLM 스트리밍 응답 코루틴. MonoBehaviour.StartCoroutine() 에서 사용.
        ///
        /// onChunk   : 새 텍스트 조각이 도착할 때마다 호출 → UI 텍스트에 추가
        /// onComplete: 전체 응답이 완성됐을 때 최종 전체 텍스트와 함께 호출
        /// onError   : 연결 실패 등 오류 발생 시 호출
        /// </summary>
        public static IEnumerator ChatStreamCoroutine(
            List<ChatMessage> messages,
            Action<string>    onChunk,
            Action<string>    onComplete,
            Action<string>    onError    = null,
            float             temperature = 0.75f,
            int               maxTokens  = 250)
        {
            var requestBody = new ChatRequest
            {
                model       = ModelName,
                messages    = messages,
                temperature = temperature,
                max_tokens  = maxTokens,
                stream      = true
            };

            var json    = JsonConvert.SerializeObject(requestBody, jsonSettings);
            var handler = new LlmStreamingDownloadHandler(onChunk, onComplete, onError);

            // 디버그 로그: 실제 송신 body 와 URL 확인
            Debug.Log($"[LlmApiClient] REQUEST → {BaseUrl}/chat\n  body({Encoding.UTF8.GetByteCount(json)} bytes)={json}");

            using var req = new UnityWebRequest(BaseUrl + "/chat", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = handler
            };
            req.SetRequestHeader("Content-Type", "application/json");
#if UNITY_EDITOR
            req.SetRequestHeader("X-Dev-Bypass", DevBypassToken);
#else
            req.SetRequestHeader("Authorization", "Bearer " + SessionApiClient.AccessToken);
#endif
            req.SetRequestHeader("Accept", "text/event-stream");

            // nginx 호환성 옵션 (SSL renegotiation / 100-continue)
            req.useHttpContinue = false;
            req.timeout = 120;
            req.redirectLimit = 0;

            yield return req.SendWebRequest();

            // HTTP/2 의 unclean stream close 같은 종료 단계 ConnectionError 는
            // handler 가 이미 [DONE] 받고 완료된 상태면 무시 — 응답은 정상 수신됨.
            if (handler.IsCompleted)
            {
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                var error = req.error ?? "알 수 없는 오류";
                long code = req.responseCode;
                string body = req.downloadHandler != null ? req.downloadHandler.text : "(no body)";
                int bodyBytes = req.downloadHandler?.data?.Length ?? 0;
                var headers = req.GetResponseHeaders();
                string headerStr = headers != null
                    ? string.Join(", ", System.Linq.Enumerable.Select(headers, kv => $"{kv.Key}={kv.Value}"))
                    : "(none)";
                Debug.LogError(
                    $"[LlmApiClient] 스트리밍 실패\n" +
                    $"  result={req.result}\n" +
                    $"  responseCode={code}\n" +
                    $"  error={error}\n" +
                    $"  url={req.url}\n" +
                    $"  bodyBytes={bodyBytes}\n" +
                    $"  body={body}\n" +
                    $"  headers={headerStr}");
                onError?.Invoke(error);
            }
        }

        // ── DTOs ──────────────────────────────────────────────────────────────

        [Serializable]
        public class ChatMessage
        {
            public string role;    // "system" / "user" / "assistant"
            public string content;

            public ChatMessage(string role, string content)
            {
                this.role    = role;
                this.content = content;
            }
        }

        [Serializable]
        private class ChatRequest
        {
            public string           model;
            public List<ChatMessage> messages;
            public float            temperature;
            public int              max_tokens;
            public bool             stream;
        }

        [Serializable]
        private class ChatResponse
        {
            public Choice[] choices;

            [Serializable]
            public class Choice
            {
                public ChatMessageWithReasoning message;
                public string finish_reason;
            }
        }

        /// <summary>
        /// Qwen3 같이 reasoning_content 를 별도 필드로 반환하는 모델 대응.
        /// content 가 비어있으면 reasoning_content 를 fallback 으로 사용 (개발 진단용).
        /// </summary>
        [Serializable]
        private class ChatMessageWithReasoning
        {
            public string role;
            public string content;
            public string reasoning_content;
        }
    }
}

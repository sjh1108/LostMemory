using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace LostMemory.Networking.Llm
{
    /// <summary>
    /// OpenAI 스트리밍 응답(SSE: Server-Sent Events) 을 실시간으로 파싱하는 DownloadHandler.
    /// LlmApiClient.ChatStreamCoroutine() 내부에서만 사용.
    ///
    /// [초심자 설명]
    ///   일반 HTTP 응답은 "응답이 완전히 도착한 뒤" 한 번에 읽습니다.
    ///   스트리밍은 응답이 조금씩 도착할 때마다 ReceiveData() 가 호출됩니다.
    ///   이 클래스는 도착하는 조각(chunk)에서 실제 텍스트를 꺼내어
    ///   onChunk 콜백으로 UI에 전달합니다.
    ///
    ///   LLM 스트리밍 응답 포맷 (SSE):
    ///     data: {"choices":[{"delta":{"content":"안"}}]}
    ///     data: {"choices":[{"delta":{"content":"녕"}}]}
    ///     data: [DONE]
    /// </summary>
    internal sealed class LlmStreamingDownloadHandler : DownloadHandlerScript
    {
        private readonly Action<string> _onChunk;
        private readonly Action<string> _onComplete;
        private readonly Action<string> _onError;
        private readonly StringBuilder  _fullText = new StringBuilder();
        private readonly StringBuilder  _buffer   = new StringBuilder();
        private bool _completed;

        /// <summary>
        /// [DONE] 수신 또는 응답 본문 끝까지 받아서 onComplete 가 이미 호출됐는지.
        /// ChatStreamCoroutine 이 SendWebRequest 종료 후 ConnectionError 와 정상 완료를 구분할 때 사용.
        /// </summary>
        public bool IsCompleted => _completed;

        public LlmStreamingDownloadHandler(
            Action<string> onChunk,
            Action<string> onComplete,
            Action<string> onError = null)
            : base(new byte[8192]) // 8KB preallocated buffer — SSE chunked transfer 안정성 위해
        {
            _onChunk    = onChunk;
            _onComplete = onComplete;
            _onError    = onError;
        }

        // 데이터 조각이 도착할 때마다 Unity 내부에서 호출됨
        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0)
            {
                Debug.LogWarning($"[LlmStreaming] ReceiveData: data null or length=0");
                return true;
            }

            string incoming = Encoding.UTF8.GetString(data, 0, dataLength);
            Debug.Log($"[LlmStreaming] RAW chunk ({dataLength} bytes): {incoming}");
            _buffer.Append(incoming);

            // SSE 는 줄 단위로 파싱 (각 이벤트는 빈 줄로 구분)
            string buf = _buffer.ToString();
            int newlineIdx;
            while ((newlineIdx = buf.IndexOf('\n')) >= 0)
            {
                string line = buf.Substring(0, newlineIdx).TrimEnd('\r');
                _buffer.Remove(0, newlineIdx + 1);
                buf = _buffer.ToString();

                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("data: "))
                {
                    Debug.Log($"[LlmStreaming] non-data line: {line}");
                    continue;
                }

                string payload = line.Substring(6); // "data: " 제거

                if (payload == "[DONE]")
                {
                    Debug.Log($"[LlmStreaming] DONE received. fullText length={_fullText.Length}");
                    _completed = true;
                    _onComplete?.Invoke(_fullText.ToString());
                    return true;
                }

                try
                {
                    var chunk = JsonConvert.DeserializeObject<StreamChunk>(payload);
                    var delta = chunk?.choices?[0]?.delta;
                    if (delta == null) continue;

                    // 일반 응답 토큰
                    if (!string.IsNullOrEmpty(delta.content))
                    {
                        _fullText.Append(delta.content);
                        _onChunk?.Invoke(delta.content);
                    }

                    // Qwen3 thinking 모드 진단용: reasoning_content 가 오면 Console 에 표시
                    if (!string.IsNullOrEmpty(delta.reasoning_content))
                    {
                        Debug.Log($"[LlmStreaming] reasoning_content delta: {delta.reasoning_content}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LlmStreaming] JSON parse failed: {ex.Message}\n payload={payload}");
                }
            }

            return true;
        }

        // 연결이 끊어졌을 때 Unity 내부에서 호출됨
        protected override void CompleteContent()
        {
            Debug.Log($"[LlmStreaming] CompleteContent. completed={_completed}, fullText.Length={_fullText.Length}, buffer.Length={_buffer.Length}");

            // [DONE] 없이 연결이 끊어진 경우 처리 (일부 서버 동작)
            if (!_completed && _fullText.Length > 0)
            {
                _completed = true;
                _onComplete?.Invoke(_fullText.ToString());
            }
        }

        // ── SSE 청크 파싱용 내부 DTO ──────────────────────────────────────────

        [Serializable]
        private class StreamChunk
        {
            public ChoiceItem[] choices;

            [Serializable]
            public class ChoiceItem
            {
                public DeltaItem delta;
            }

            [Serializable]
            public class DeltaItem
            {
                public string content;
                public string reasoning_content; // Qwen3 thinking 모드 진단용
            }
        }
    }
}

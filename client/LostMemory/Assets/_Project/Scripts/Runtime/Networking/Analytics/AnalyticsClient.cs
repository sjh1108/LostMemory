using System;
using System.Collections;
using System.Collections.Generic;
using LostMemory.Networking.Common;
using UnityEngine;

namespace LostMemory.Networking.Analytics
{
    /// <summary>
    /// 인게임 이벤트 batch 전송기. TitleSceneController 로그인 성공 직후 Bootstrap 으로 생성.
    ///
    /// 동작:
    ///   - Awake 에서 session_id (UUID v4) 발급. 한 게임 실행 = 한 session_id
    ///   - Track(...) 호출은 in-memory queue 에 적재 — 응답을 기다리지 않음
    ///   - 5초마다 또는 큐가 50개 차면 즉시 flush
    ///   - 4xx 응답: batch 폐기. 5xx/네트워크 실패: queue 앞쪽에 재 enqueue, 최대 3회 재시도
    ///   - OnApplicationQuit: 동기 flush 1회 시도 (best-effort)
    ///
    /// 단일 인스턴스 — Bootstrap.EnsureExists() 로만 생성. DontDestroyOnLoad.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnalyticsClient : MonoBehaviour
    {
        private const int FlushBatchSize = 50;
        private const float FlushIntervalSeconds = 5f;
        private const int MaxRetries = 3;
        private const string ClientVersion = "0.1.4";  // TODO: Application.version 으로 빌드 단계 동기화

        public static AnalyticsClient Instance { get; private set; }

        /// <summary>한 게임 실행 동안 고정. UUID v4.</summary>
        public string SessionId { get; private set; }

        /// <summary>최근 진입한 stage_id — session_end payload 의 last_stage_id 채우기용. stage_entered hook 에서 갱신.</summary>
        public string LastStageId { get; set; }

        private readonly Queue<AnalyticsEvent> _pending = new Queue<AnalyticsEvent>();
        private bool _isFlushing;
        private float _sessionStartRealtime;
        private float _lastStageEnteredRealtime;

        /// <summary>없으면 생성. 멱등. AnalyticsDamageTracker 도 함께 부착.</summary>
        public static AnalyticsClient EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AnalyticsClient");
            DontDestroyOnLoad(go);
            var client = go.AddComponent<AnalyticsClient>();
            go.AddComponent<AnalyticsDamageTracker>();
            return client;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SessionId = Guid.NewGuid().ToString();
            _sessionStartRealtime = Time.realtimeSinceStartup;
            NetLog.Info("Analytics", $"session_id={SessionId}");
        }

        private void Start()
        {
            StartCoroutine(FlushTimerLoop());
        }

        /// <summary>이벤트 적재. payload 는 익명/Dictionary OK — Newtonsoft 가 JSONB 로 직렬화.</summary>
        public void Track(string eventType, string stageId = null, object payload = null)
        {
            if (string.IsNullOrEmpty(eventType)) return;
            var evt = new AnalyticsEvent
            {
                event_type = eventType,
                event_time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                stage_id = stageId,
                payload = ToDict(payload),
            };
            _pending.Enqueue(evt);
            if (_pending.Count >= FlushBatchSize && !_isFlushing)
            {
                StartCoroutine(FlushOnce());
            }
        }

        private IEnumerator FlushTimerLoop()
        {
            var wait = new WaitForSecondsRealtime(FlushIntervalSeconds);
            while (true)
            {
                yield return wait;
                if (_pending.Count > 0 && !_isFlushing)
                {
                    yield return FlushOnce();
                }
            }
        }

        private IEnumerator FlushOnce()
        {
            if (_isFlushing) yield break;
            _isFlushing = true;

            var batch = DrainUpTo(FlushBatchSize);
            var body = new AnalyticsBatchBody
            {
                client_version = ClientVersion,
                session_id = SessionId,
                events = batch,
            };

            int attempt = 0;
            while (attempt < MaxRetries)
            {
                attempt++;
                var task = AnalyticsApiClient.PostEventsAsync(body);
                while (!task.IsCompleted) yield return null;

                var result = task.Result;
                if (result == AnalyticsApiClient.PostResult.Success)
                {
                    NetLog.Info("Analytics", $"flushed {batch.Count} events");
                    break;
                }
                if (result == AnalyticsApiClient.PostResult.ClientError)
                {
                    // batch 폐기 — 다음 라운드부터 새로 쌓임
                    break;
                }
                // ServerError — exponential backoff 후 재시도
                if (attempt < MaxRetries)
                {
                    yield return new WaitForSecondsRealtime(Mathf.Pow(2, attempt));
                }
                else
                {
                    NetLog.Warn("Analytics", $"flush {MaxRetries} 회 실패 — {batch.Count} events 폐기");
                }
            }

            _isFlushing = false;
        }

        private List<AnalyticsEvent> DrainUpTo(int max)
        {
            int n = Math.Min(_pending.Count, max);
            var list = new List<AnalyticsEvent>(n);
            for (int i = 0; i < n; i++) list.Add(_pending.Dequeue());
            return list;
        }

        private static Dictionary<string, object> ToDict(object payload)
        {
            if (payload == null) return null;
            if (payload is Dictionary<string, object> d) return d;
            // 익명 객체 → reflection 으로 평탄화
            var result = new Dictionary<string, object>();
            foreach (var prop in payload.GetType().GetProperties())
            {
                result[prop.Name] = prop.GetValue(payload);
            }
            foreach (var field in payload.GetType().GetFields())
            {
                result[field.Name] = field.GetValue(payload);
            }
            return result;
        }

        /// <summary>stage 진입 발화. RunManager 가 RoomEntered 구독해서 호출.</summary>
        public void FireStageEntered(string stageId, int partySize, string prevStageId)
        {
            _lastStageEnteredRealtime = Time.realtimeSinceStartup;
            LastStageId = stageId;
            Track("stage_entered", stageId: stageId, payload: new
            {
                party_size = partySize,
                prev_stage_id = prevStageId,
            });
        }

        /// <summary>stage 클리어 발화. RunManager 가 RoomCleared 구독해서 호출.</summary>
        public void FireStageCleared(string stageId)
        {
            float duration = _lastStageEnteredRealtime > 0f
                ? Time.realtimeSinceStartup - _lastStageEnteredRealtime
                : 0f;
            Track("stage_cleared", stageId: stageId, payload: new
            {
                duration_sec = duration,
            });
        }

        /// <summary>player_died 발화. RunManager 가 KhiDownController.DefeatedBy* 구독해서 호출.</summary>
        public void FirePlayerDied(string stageId, string runId, float runElapsedSec)
        {
            Track("player_died", stageId: stageId, payload: new
            {
                cause_enemy_id = AnalyticsDamageTracker.LastEnemyId,
                cause_pattern_id = AnalyticsDamageTracker.LastPatternId,
                run_id = runId,
                run_elapsed_sec = runElapsedSec,
            });
        }

        /// <summary>외부에서 명시적 종료 사유로 호출 가능 (예: 네트워크 디스커넥트). OnApplicationQuit 도 내부적으로 호출.</summary>
        public void FireSessionEnd(string reason)
        {
            Track("session_end", stageId: LastStageId, payload: new
            {
                last_stage_id = LastStageId,
                reason = reason,
                duration_sec = Time.realtimeSinceStartup - _sessionStartRealtime,
            });
        }

        private void OnApplicationQuit()
        {
            FireSessionEnd("quit");
            // 동기 flush 시도 — 게임 종료 직전이라 best-effort. 실패해도 무시.
            if (_pending.Count == 0) return;
            var batch = DrainUpTo(FlushBatchSize);
            var body = new AnalyticsBatchBody
            {
                client_version = ClientVersion,
                session_id = SessionId,
                events = batch,
            };
            try
            {
                var task = AnalyticsApiClient.PostEventsAsync(body);
                task.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                NetLog.Warn("Analytics", $"OnApplicationQuit flush 실패: {ex.Message}");
            }
        }
    }
}

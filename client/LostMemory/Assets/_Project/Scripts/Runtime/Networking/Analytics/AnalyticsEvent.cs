using System;
using System.Collections.Generic;

namespace LostMemory.Networking.Analytics
{
    /// <summary>
    /// 자체 백엔드(POST /api/analytics/events) 로 보낼 단일 인게임 이벤트.
    ///
    /// 필드명은 백엔드 PostgreSQL 컬럼/JSONB 키와 1:1 매칭 — snake_case 그대로 유지.
    /// 핸드오프 문서: docs/khi/2026-05-24-player-analytics-backend-handoff.md §2.2
    /// </summary>
    [Serializable]
    public class AnalyticsEvent
    {
        public string event_type;
        public string event_time;          // ISO-8601 UTC, 클라 시계
        public string stage_id;            // nullable
        public Dictionary<string, object> payload;
    }

    /// <summary>POST body wrapper. 한 번에 여러 이벤트 batch 전송.</summary>
    [Serializable]
    public class AnalyticsBatchBody
    {
        public string client_version;
        public string session_id;          // UUID v4
        public List<AnalyticsEvent> events;
    }
}

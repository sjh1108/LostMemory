using System;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 세션 흐름에서 발생 가능한 실패 분류. UI 표시 메시지를 한 곳에서 결정한다.
    /// </summary>
    public enum SessionErrorKind
    {
        None = 0,
        ServicesInitFailed,         // Unity Services Initialize 실패 (Cloud 프로젝트 미연결 등)
        SignInFailed,               // 익명 로그인 실패
        InvalidJoinCode,            // 코드 형식 자체가 비정상 (빈 문자열/길이 등)
        JoinCodeNotFound,           // Relay 가 코드 자체를 인식 못 함
        SessionFull,                // 정원 초과
        SessionExpired,             // 만료된 세션
        RelayAllocateFailed,        // Allocation/할당 실패
        TransportStartFailed,       // UnityTransport 또는 NetworkManager StartHost/StartClient 실패
        HostDisconnected,           // 클라이언트 입장에서 호스트 이탈
        UserCanceled,               // 사용자 취소
        Unknown
    }

    public static class SessionErrorPolicy
    {
        public static string ToUserMessage(SessionErrorKind kind, string detail = null)
        {
            string baseMessage = kind switch
            {
                SessionErrorKind.None => string.Empty,
                SessionErrorKind.ServicesInitFailed => "Unity 서비스 초기화에 실패했습니다. Project Settings 의 Cloud 연결을 확인해 주세요.",
                SessionErrorKind.SignInFailed => "로그인에 실패했습니다. 잠시 후 다시 시도해 주세요.",
                SessionErrorKind.InvalidJoinCode => "참여 코드 형식이 올바르지 않습니다.",
                SessionErrorKind.JoinCodeNotFound => "참여 코드를 찾을 수 없습니다. 코드를 다시 확인해 주세요.",
                SessionErrorKind.SessionFull => "방이 이미 가득 찼습니다.",
                SessionErrorKind.SessionExpired => "세션이 만료되었습니다.",
                SessionErrorKind.RelayAllocateFailed => "방 생성에 실패했습니다. 네트워크 상태를 확인하고 다시 시도해 주세요.",
                SessionErrorKind.TransportStartFailed => "네트워크 시작에 실패했습니다. 잠시 후 다시 시도해 주세요.",
                SessionErrorKind.HostDisconnected => "방장이 나가서 세션이 종료되었습니다.",
                SessionErrorKind.UserCanceled => "취소되었습니다.",
                SessionErrorKind.Unknown => "알 수 없는 오류가 발생했습니다.",
                _ => "알 수 없는 오류가 발생했습니다."
            };

            return string.IsNullOrEmpty(detail) ? baseMessage : $"{baseMessage} ({detail})";
        }

        /// <summary>
        /// 예외 → 분류 매핑. 메시지 키워드 기반의 휴리스틱 — Relay/Multiplayer SDK 가 던지는 예외 타입이
        /// 패치별로 달라지는 점을 고려해 보수적으로 처리.
        /// </summary>
        public static SessionErrorKind Classify(Exception exception)
        {
            if (exception == null) return SessionErrorKind.None;

            string message = exception.Message ?? string.Empty;
            string lower = message.ToLowerInvariant();

            if (lower.Contains("not found") || lower.Contains("invalid join code") || lower.Contains("invalid code"))
            {
                return SessionErrorKind.JoinCodeNotFound;
            }
            if (lower.Contains("full") || lower.Contains("max players"))
            {
                return SessionErrorKind.SessionFull;
            }
            if (lower.Contains("expired"))
            {
                return SessionErrorKind.SessionExpired;
            }
            if (lower.Contains("allocate") || lower.Contains("allocation"))
            {
                return SessionErrorKind.RelayAllocateFailed;
            }
            if (lower.Contains("sign in") || lower.Contains("authentication"))
            {
                return SessionErrorKind.SignInFailed;
            }
            if (lower.Contains("services") && lower.Contains("init"))
            {
                return SessionErrorKind.ServicesInitFailed;
            }

            return SessionErrorKind.Unknown;
        }
    }
}

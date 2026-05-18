using UnityEngine;

namespace LostMemory.Networking.Common
{
    /// <summary>
    /// 네트워크 핵심 로그 진입점. 연결/참가/이탈/동기화 실패 추적용 단일 게이트.
    /// EnableInfo / EnableWarning 토글로 빌드별 차단 가능. Error 는 항상 출력.
    /// </summary>
    public static class NetLog
    {
        public static bool EnableInfo = true;
        public static bool EnableWarning = true;

        private const string Prefix = "[Net]";

        public static void Info(string message, Object context = null)
        {
            if (!EnableInfo) return;
            Debug.Log($"{Prefix} {message}", context);
        }

        public static void Info(string category, string message, Object context = null)
        {
            if (!EnableInfo) return;
            Debug.Log($"{Prefix}[{category}] {message}", context);
        }

        public static void Warn(string message, Object context = null)
        {
            if (!EnableWarning) return;
            Debug.LogWarning($"{Prefix} {message}", context);
        }

        public static void Warn(string category, string message, Object context = null)
        {
            if (!EnableWarning) return;
            Debug.LogWarning($"{Prefix}[{category}] {message}", context);
        }

        public static void Error(string message, Object context = null)
        {
            Debug.LogError($"{Prefix} {message}", context);
        }

        public static void Error(string category, string message, Object context = null)
        {
            Debug.LogError($"{Prefix}[{category}] {message}", context);
        }
    }
}

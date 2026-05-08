using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// CL-152: 토스트 알림 placeholder. 정식 UI (Canvas Text + fade animation) 는 별도 ticket 후속.
    ///
    /// 현재는 <see cref="Debug.LogWarning"/> 으로 콘솔 출력만. 검증 단계에서 충분.
    /// 사용처: ToastNotifierBridge 가 PlayerRelicInventory.OnTryAddRejected 받아 호출.
    /// </summary>
    public static class ToastNotifier
    {
        /// <summary>토스트 메시지 표시. duration 은 정식 UI 시 fade 시간.</summary>
        public static void Show(string msg, float duration = 2.5f)
        {
            Debug.LogWarning($"[TOAST] {msg}");
            // TODO: 정식 토스트 UI ticket 후 — Canvas 의 임시 TMP_Text 표시 + fade animation.
            // Singleton 패턴으로 Bootstrap 시 Canvas 자동 생성 또는 인스펙터 wiring.
        }
    }
}

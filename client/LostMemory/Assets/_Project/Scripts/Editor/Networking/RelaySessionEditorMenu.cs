using LostMemory.Networking.Session;
using UnityEditor;
using UnityEngine;

namespace LostMemory.EditorTools.Networking
{
    /// <summary>
    /// L-1: RelaySession.UseLocalFallback 토글 메뉴.
    /// 백엔드 다운 시 backend API 우회 + UTP localhost 자동 swap 으로 멀티 sync 테스트.
    ///
    /// 위치: Lost Memory > Toggle Local Fallback (Backend Bypass)
    ///
    /// 자동화 범위:
    ///   ✓ Backend API 우회
    ///   ✓ 가짜 userId/nickname (PID 기반)
    ///   ✓ NetworkTransport 동적 swap
    /// 빌드 영향: 없음 (EditorPrefs + #if UNITY_EDITOR).
    /// </summary>
    public static class RelaySessionEditorMenu
    {
        private const string MenuPath = "Lost Memory/Toggle Local Fallback (Backend Bypass)";

        [MenuItem(MenuPath, priority = 1000)]
        private static void Toggle()
        {
            RelaySession.UseLocalFallback = !RelaySession.UseLocalFallback;
            string state = RelaySession.UseLocalFallback ? "ON" : "OFF";
            Debug.Log($"[RelaySession] Local Fallback {state}.");
            EditorUtility.DisplayDialog(
                "Local Fallback",
                $"백엔드 우회 Local Fallback: {state}\n\n" +
                (RelaySession.UseLocalFallback
                    ? "✓ Backend 호출 우회\n✓ 가짜 userId/nickname 자동 (PID 기반)\n✓ UnityTransport 자동 swap (127.0.0.1:7777)"
                    : "정상 backend 흐름 복귀\n✓ LostMemoryRelay 자동 복원"),
                "OK");
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, RelaySession.UseLocalFallback);
            return true;
        }
    }
}

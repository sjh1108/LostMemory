using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 특정 scene 진입 시 마우스 커서 텍스처 교체. 이 컴포넌트가 부착된 GameObject 가
    /// active 인 동안만 커서 적용. scene 빠질 때 (OnDisable) default 커서로 자동 복귀.
    ///
    /// 사용법:
    ///   1. 적용할 scene (예: Town_Preview.unity) 열기
    ///   2. Hierarchy 우클릭 → Create Empty (이름: [TownPreviewCursorSetter])
    ///   3. Add Component → Town Preview Cursor Setter
    ///   4. cursorTexture 슬롯에 원하는 Texture2D drag (예: tool_sword_b.png)
    ///   5. hotspot 조정 (검 끝 클릭 포인트면 검 끝 픽셀 좌표)
    ///   6. scene 저장
    ///
    /// 텍스처 import 권장 설정:
    ///   - Texture Type: Cursor (또는 Default. Sprite 도 작동하지만 sub-optimal)
    ///   - Read/Write Enabled: ON (CursorMode.ForceSoftware 사용 시 필수)
    ///   - Compression: None (선명도)
    ///   - 크기: 32x32 또는 64x64 권장 (OS 한계 — 너무 크면 잘림)
    ///
    /// CursorToggleDebug (F5) 와 호환:
    ///   F5 로 Cursor.visible 토글해도 SetCursor 한 텍스처는 유지됨. visible=true 복귀 시 텍스처 그대로.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Test Khi/Town Preview Cursor Setter")]
    public class TownPreviewCursorSetter : MonoBehaviour
    {
        [SerializeField, Tooltip("씬 진입 시 마우스 커서로 사용할 텍스처. " +
            "tool_sword_b.png 등. Texture Type=Cursor + Read/Write Enabled 권장.")]
        private Texture2D cursorTexture;

        [SerializeField, Tooltip("커서 hotspot (실제 클릭 위치) — texture 픽셀 좌표. " +
            "(0,0)=좌상단. 검 끝이 클릭 포인트면 검 끝의 픽셀 좌표 입력.")]
        private Vector2 hotspot = Vector2.zero;

        [SerializeField, Tooltip("Auto = OS hardware cursor (빠름, 일부 효과 제한). " +
            "ForceSoftware = Unity 렌더링 (Read/Write Enabled 필수, 더 유연).")]
        private CursorMode cursorMode = CursorMode.Auto;

        [SerializeField, Tooltip("OnEnable 시 로그 출력 — 적용 확인용.")]
        private bool logOnApply = true;

        private void OnEnable()
        {
            ApplyCursor();
        }

        private void OnDisable()
        {
            // scene 빠질 때 default 커서로 복귀
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (logOnApply)
            {
                Debug.Log("[TownPreviewCursorSetter] 커서 default 복귀.", this);
            }
        }

        private void ApplyCursor()
        {
            if (cursorTexture == null)
            {
                Debug.LogWarning("[TownPreviewCursorSetter] cursorTexture 가 null — 커서 교체 skip. Inspector 에서 텍스처 할당하세요.", this);
                return;
            }

            Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
            if (logOnApply)
            {
                Debug.Log($"[TownPreviewCursorSetter] 커서 적용 — texture='{cursorTexture.name}' hotspot={hotspot} mode={cursorMode}", this);
            }
        }

#if UNITY_EDITOR
        // Inspector 에서 hotspot/texture 바꿔보면서 즉시 확인 (Play 중일 때만 의미 있음)
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            if (isActiveAndEnabled) ApplyCursor();
        }
#endif
    }
}

using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.Networking
{
    /// <summary>
    /// LLM dev bypass 토큰 입력/저장 EditorWindow.
    /// 클라 로그인 안정화 전까지 Editor Play 모드에서 백엔드 /api/llm/chat 호출 시
    /// X-Dev-Bypass 헤더에 박을 토큰을 EditorPrefs 에 저장한다.
    ///
    /// 메뉴: LostMemory/LLM/Dev Bypass Token
    /// 저장 위치: EditorPrefs key = "LostMemory.LlmDevBypass.Token" (이 PC 의 Unity 사용자 단위)
    /// 깃 추적 X — 코드/프로젝트에 평문 토큰 박지 말 것.
    /// </summary>
    public class LlmDevBypassTokenWindow : EditorWindow
    {
        // 주의: LlmApiClient.DevBypassPrefsKey 와 반드시 동일해야 함.
        private const string PrefsKey = "LostMemory.LlmDevBypass.Token";

        private string _tokenInput = "";
        private bool   _showRaw;

        [MenuItem("LostMemory/LLM/Dev Bypass Token")]
        public static void Open()
        {
            var w = GetWindow<LlmDevBypassTokenWindow>("LLM Dev Bypass Token");
            w.minSize = new Vector2(440, 200);
        }

        private void OnEnable()
        {
            _tokenInput = EditorPrefs.GetString(PrefsKey, "");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("LLM Dev Bypass Token", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Editor Play 모드에서 /api/llm/chat 호출 시 X-Dev-Bypass 헤더에 박힘.\n" +
                "이 PC 의 EditorPrefs 에 사용자 단위로 저장됨 — 깃에 커밋되지 않음.\n" +
                "운영자에게서 받은 DEV_BYPASS_SECRET 값을 입력하세요.",
                MessageType.Info);

            EditorGUILayout.Space();

            _showRaw = EditorGUILayout.ToggleLeft("값 보이기 (디버그용)", _showRaw);
            _tokenInput = _showRaw
                ? EditorGUILayout.TextField("Token", _tokenInput)
                : EditorGUILayout.PasswordField("Token", _tokenInput);

            var saved = EditorPrefs.GetString(PrefsKey, "");
            EditorGUILayout.LabelField(
                "저장된 토큰 길이",
                string.IsNullOrEmpty(saved) ? "(없음)" : $"{saved.Length} 자");

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("저장", GUILayout.Height(28)))
                {
                    EditorPrefs.SetString(PrefsKey, _tokenInput ?? "");
                    Debug.Log($"[LlmDevBypassToken] 저장됨 (length={(_tokenInput ?? "").Length}).");
                }

                if (GUILayout.Button("삭제", GUILayout.Height(28)))
                {
                    EditorPrefs.DeleteKey(PrefsKey);
                    _tokenInput = "";
                    Debug.Log("[LlmDevBypassToken] 삭제됨.");
                }
            }
        }
    }
}

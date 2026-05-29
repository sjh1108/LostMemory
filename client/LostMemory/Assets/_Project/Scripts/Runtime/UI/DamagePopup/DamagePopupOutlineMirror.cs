using TMPro;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 픽셀 폰트 outline 트릭 — 메인 TMP 의 text 를 4방향 그림자 TMP 에 복사 + 매 LateUpdate 마다
    /// DamagePopupSpawner.Style 의 outlineColor/outlineOffset/outlineEnabled 를 적용.
    /// SDF 변환 없이 Bitmap shader 그대로 outline 효과 — 픽셀 모양 유지.
    /// Play 중 SO 인스펙터 값 변경 시 살아있는 popup 도 즉시 반영 (실시간 튜닝).
    ///
    /// Outline 배열 순서 규약: [0]=Up, [1]=Down, [2]=Left, [3]=Right.
    /// Prefab 인스펙터에서 wiring 시 이 순서 지킬 것.
    /// </summary>
    [AddComponentMenu("Lost Memory/UI/Damage Popup Outline Mirror")]
    [DefaultExecutionOrder(100)]
    public class DamagePopupOutlineMirror : MonoBehaviour
    {
        [Tooltip("메인(흰색) TMP. MMFloatingText 가 set 한다.")]
        [SerializeField] private TMP_Text mainText;

        [Tooltip("4방향 outline TMP. 순서: [0]=Up, [1]=Down, [2]=Left, [3]=Right. 정확히 4개 권장.")]
        [SerializeField] private TMP_Text[] outlineTexts;

        private string _lastText;

        private void OnEnable()
        {
            _lastText = null;
        }

        private void LateUpdate()
        {
            if (mainText == null || outlineTexts == null) return;

            SyncText();
            ApplyStyle();
        }

        private void SyncText()
        {
            string current = mainText.text;
            if (current == _lastText) return;
            _lastText = current;

            for (int i = 0; i < outlineTexts.Length; i++)
            {
                if (outlineTexts[i] != null) outlineTexts[i].text = current;
            }
        }

        private void ApplyStyle()
        {
            DamagePopupSpawner spawner = DamagePopupSpawner.Instance;
            DamagePopupStyle style = spawner != null ? spawner.Style : null;
            if (style == null) return;

            // Layout 은 메인 + 4 outline 모두에 동일 적용.
            ApplyLayoutTo(mainText, style.fontSize, style.characterSpacing, style.lineSpacing);

            bool active = style.outlineEnabled;
            Color color = style.outlineColor;
            float d = style.outlineOffset;

            for (int i = 0; i < outlineTexts.Length; i++)
            {
                TMP_Text outline = outlineTexts[i];
                if (outline == null) continue;

                if (outline.gameObject.activeSelf != active)
                    outline.gameObject.SetActive(active);
                if (!active) continue;

                outline.color = color;
                ApplyLayoutTo(outline, style.fontSize, style.characterSpacing, style.lineSpacing);

                Vector3 pos = i switch
                {
                    0 => new Vector3(0f, d, 0f),    // Up
                    1 => new Vector3(0f, -d, 0f),   // Down
                    2 => new Vector3(-d, 0f, 0f),   // Left
                    3 => new Vector3(d, 0f, 0f),    // Right
                    _ => Vector3.zero,
                };
                Transform t = outline.transform;
                if (t.localPosition != pos) t.localPosition = pos;
            }
        }

        private static void ApplyLayoutTo(TMP_Text tmp, float fontSize, float spacing, float lineSpacing)
        {
            if (tmp == null) return;
            // TMP 는 값 변경 시 자동 SetVerticesDirty. 같은 값이면 no-op (가벼움).
            if (!Mathf.Approximately(tmp.fontSize, fontSize)) tmp.fontSize = fontSize;
            if (!Mathf.Approximately(tmp.characterSpacing, spacing)) tmp.characterSpacing = spacing;
            if (!Mathf.Approximately(tmp.lineSpacing, lineSpacing)) tmp.lineSpacing = lineSpacing;
        }
    }
}

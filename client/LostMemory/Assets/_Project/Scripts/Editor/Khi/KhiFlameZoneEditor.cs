using LostMemory.TestKhi;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor
{
    /// <summary>
    /// KhiFlameZone 의 Scene 뷰 핸들 에디터. Primary(좌클릭) + Secondary(우클릭) 두 콘 동시 표시.
    /// - Primary 콘 (오렌지): range / coneHalfAngleDeg
    /// - Secondary 콘 (빨강·자주): secondaryRange / secondaryConeHalfAngleDeg
    /// 각 콘마다 끝점(range) + 가장자리(half-angle) 핸들 제공 → 총 6개 핸들.
    ///
    /// Edit 모드: aim = transform.right (캐릭터 정면 +X).
    /// Play 모드: KhiFlameZone.OnDrawGizmosSelected 가 마우스 추적 콘 별도 표시.
    /// </summary>
    [CustomEditor(typeof(KhiFlameZone))]
    public sealed class KhiFlameZoneEditor : UnityEditor.Editor
    {
        private const float RangeHandleSize = 0.18f;
        private const float AngleHandleSize = 0.12f;
        private const float MinRange = 0.5f;
        private const float MinHalfAngle = 5f;
        private const float MaxHalfAngle = 80f;

        // Primary (오렌지)
        private static readonly Color PrimaryOutlineColor = new Color(1f, 0.5f, 0f, 0.95f);
        private static readonly Color PrimaryFillColor = new Color(1f, 0.5f, 0f, 0.12f);
        private static readonly Color PrimaryRangeHandleColor = new Color(1f, 0.4f, 0.1f, 1f);
        private static readonly Color PrimaryAngleHandleColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color PrimaryCenterLineColor = new Color(1f, 0.6f, 0.1f, 0.5f);

        // Secondary (빨강·자주)
        private static readonly Color SecondaryOutlineColor = new Color(1f, 0.2f, 0.4f, 0.95f);
        private static readonly Color SecondaryFillColor = new Color(1f, 0.2f, 0.4f, 0.12f);
        private static readonly Color SecondaryRangeHandleColor = new Color(1f, 0.15f, 0.35f, 1f);
        private static readonly Color SecondaryAngleHandleColor = new Color(1f, 0.5f, 0.7f, 1f);
        private static readonly Color SecondaryCenterLineColor = new Color(1f, 0.3f, 0.5f, 0.5f);

        private SerializedProperty _primaryRangeProp;
        private SerializedProperty _primaryAngleProp;
        private SerializedProperty _secondaryRangeProp;
        private SerializedProperty _secondaryAngleProp;

        private void OnEnable()
        {
            _primaryRangeProp = serializedObject.FindProperty("range");
            _primaryAngleProp = serializedObject.FindProperty("coneHalfAngleDeg");
            _secondaryRangeProp = serializedObject.FindProperty("secondaryRange");
            _secondaryAngleProp = serializedObject.FindProperty("secondaryConeHalfAngleDeg");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Scene 뷰 핸들로 시각 조절:\n" +
                "• 오렌지 큰 구체 → Primary Range (좌클릭)\n" +
                "• 노랑 작은 구체 → Primary Half Angle\n" +
                "• 빨강 큰 구체 → Secondary Range (우클릭)\n" +
                "• 분홍 작은 구체 → Secondary Half Angle\n" +
                "Edit 모드: aim = transform.right. Play 모드는 Gizmo 별도 표시.",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            KhiFlameZone zone = (KhiFlameZone)target;
            if (zone == null) return;

            serializedObject.Update();

            Vector3 origin = zone.transform.position;
            Vector3 aim = zone.transform.right;
            aim.z = 0f;
            if (aim.sqrMagnitude < 1e-6f) aim = Vector3.right;
            aim.Normalize();
            float aimAngleDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            float primaryRange = _primaryRangeProp.floatValue;
            float primaryAngle = _primaryAngleProp.floatValue;
            float secondaryRange = _secondaryRangeProp.floatValue;
            float secondaryAngle = _secondaryAngleProp.floatValue;

            // 콘 외곽선 (Secondary 먼저 그려서 Primary 가 위에 오도록 — 시각적으로 둘 다 잘 보임)
            DrawConeShape(origin, aimAngleDeg, secondaryRange, secondaryAngle,
                SecondaryOutlineColor, SecondaryFillColor, SecondaryCenterLineColor);
            DrawConeShape(origin, aimAngleDeg, primaryRange, primaryAngle,
                PrimaryOutlineColor, PrimaryFillColor, PrimaryCenterLineColor);

            // 핸들 — Primary
            DrawRangeHandle(zone, _primaryRangeProp, origin, aim, primaryRange,
                PrimaryRangeHandleColor, "Adjust Primary Range");
            ProcessAngleHandle(zone, _primaryAngleProp, origin, aimAngleDeg, primaryRange, primaryAngle, +1,
                PrimaryAngleHandleColor, "Adjust Primary Angle");
            ProcessAngleHandle(zone, _primaryAngleProp, origin, aimAngleDeg, primaryRange, primaryAngle, -1,
                PrimaryAngleHandleColor, "Adjust Primary Angle");

            // 핸들 — Secondary
            DrawRangeHandle(zone, _secondaryRangeProp, origin, aim, secondaryRange,
                SecondaryRangeHandleColor, "Adjust Secondary Range");
            ProcessAngleHandle(zone, _secondaryAngleProp, origin, aimAngleDeg, secondaryRange, secondaryAngle, +1,
                SecondaryAngleHandleColor, "Adjust Secondary Angle");
            ProcessAngleHandle(zone, _secondaryAngleProp, origin, aimAngleDeg, secondaryRange, secondaryAngle, -1,
                SecondaryAngleHandleColor, "Adjust Secondary Angle");

            // Scene 뷰 좌상단 라벨
            Handles.BeginGUI();
            var rect = new Rect(10f, 10f, 280f, 80f);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(rect);
            GUILayout.Space(4f);
            GUILayout.Label($"  Primary:   Range {primaryRange:F2}m, Half {primaryAngle:F1}° (Total {primaryAngle * 2f:F0}°)");
            GUILayout.Label($"  Secondary: Range {secondaryRange:F2}m, Half {secondaryAngle:F1}° (Total {secondaryAngle * 2f:F0}°)");
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void DrawRangeHandle(
            KhiFlameZone zone, SerializedProperty rangeProp,
            Vector3 origin, Vector3 aim, float currentRange,
            Color color, string undoLabel)
        {
            Vector3 tipPos = origin + aim * currentRange;
            EditorGUI.BeginChangeCheck();
            Handles.color = color;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            Vector3 newTip = Handles.Slider(id, tipPos, aim, RangeHandleSize, Handles.SphereHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck()) return;

            float projected = Vector3.Dot(newTip - origin, aim);
            float newRange = Mathf.Max(MinRange, projected);
            Undo.RecordObject(zone, undoLabel);
            rangeProp.floatValue = newRange;
            serializedObject.ApplyModifiedProperties();
        }

        private void ProcessAngleHandle(
            KhiFlameZone zone, SerializedProperty angleProp,
            Vector3 origin, float aimAngleDeg, float currentRange, float currentHalfAngle,
            int sign, Color color, string undoLabel)
        {
            float edgeAngleDeg = aimAngleDeg + currentHalfAngle * sign;
            float edgeAngleRad = edgeAngleDeg * Mathf.Deg2Rad;
            Vector3 edgeDir = new Vector3(Mathf.Cos(edgeAngleRad), Mathf.Sin(edgeAngleRad), 0f);
            Vector3 edgePos = origin + edgeDir * currentRange;

            EditorGUI.BeginChangeCheck();
            Handles.color = color;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            Vector3 newEdgePos = Handles.FreeMoveHandle(
                id, edgePos, AngleHandleSize, Vector3.zero, Handles.SphereHandleCap);
            if (!EditorGUI.EndChangeCheck()) return;

            Vector3 fromOrigin = newEdgePos - origin;
            if (fromOrigin.sqrMagnitude < 1e-6f) return;

            float currentAngle = Mathf.Atan2(fromOrigin.y, fromOrigin.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(aimAngleDeg, currentAngle);

            // 사용자가 aim 라인 너머로 핸들을 끌면 부호 반전 → 콘 뒤집힘 방지.
            float signedHalfAngle = delta * sign;
            float newHalfAngle = Mathf.Clamp(signedHalfAngle, MinHalfAngle, MaxHalfAngle);

            Undo.RecordObject(zone, undoLabel);
            angleProp.floatValue = newHalfAngle;
            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawConeShape(
            Vector3 origin, float aimAngleDeg, float useRange, float useHalfAngle,
            Color outlineColor, Color fillColor, Color centerLineColor)
        {
            if (useRange < 0.001f || useHalfAngle < 0.1f) return;

            int segments = Mathf.Clamp(Mathf.CeilToInt(useHalfAngle / 4f), 8, 40);
            Vector3[] fillPoints = new Vector3[segments + 2];
            fillPoints[0] = origin;
            float step = (useHalfAngle * 2f) / segments;
            for (int i = 0; i <= segments; i++)
            {
                float a = (aimAngleDeg - useHalfAngle + step * i) * Mathf.Deg2Rad;
                fillPoints[i + 1] = origin + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * useRange;
            }

            // 반투명 채움
            Handles.color = fillColor;
            Handles.DrawAAConvexPolygon(fillPoints);

            // 가장자리 라인 2개 + 끝부분 호
            float leftRad = (aimAngleDeg - useHalfAngle) * Mathf.Deg2Rad;
            float rightRad = (aimAngleDeg + useHalfAngle) * Mathf.Deg2Rad;
            Vector3 leftEdge = origin + new Vector3(Mathf.Cos(leftRad), Mathf.Sin(leftRad), 0f) * useRange;
            Vector3 rightEdge = origin + new Vector3(Mathf.Cos(rightRad), Mathf.Sin(rightRad), 0f) * useRange;

            Handles.color = outlineColor;
            Handles.DrawLine(origin, leftEdge);
            Handles.DrawLine(origin, rightEdge);

            Vector3 leftEdgeDir = new Vector3(Mathf.Cos(leftRad), Mathf.Sin(leftRad), 0f);
            Handles.DrawWireArc(origin, Vector3.forward, leftEdgeDir, useHalfAngle * 2f, useRange);

            // 중심선 (점선)
            float aimRad = aimAngleDeg * Mathf.Deg2Rad;
            Vector3 aimDir = new Vector3(Mathf.Cos(aimRad), Mathf.Sin(aimRad), 0f);
            Handles.color = centerLineColor;
            Handles.DrawDottedLine(origin, origin + aimDir * useRange, 3f);
        }
    }
}

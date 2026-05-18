using LostMemory.Intro.Phase0;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Intro.Phase0.Editor
{
    public static class Phase0IntroLinesAutoFill
    {
        private struct LineData
        {
            public string text;
            public bool clearBefore;
            public float delayBefore;
            public float delayAfter;
            public float cps;
            public bool instant;

            public LineData(string t, bool clear, float before, float after, float c = 0f, bool inst = false)
            {
                text = t; clearBefore = clear; delayBefore = before; delayAfter = after; cps = c; instant = inst;
            }
        }

        [MenuItem("Lost Memory/Intro/Fill Phase0 Script Lines (v3)")]
        private static void FillLines()
        {
            string[] guids = AssetDatabase.FindAssets("t:Phase0IntroSequenceData", new[] { "Assets/_Project/Data" });
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("오류", "Phase0IntroSequenceData 에셋을 찾을 수 없습니다.\nAssets/_Project/Data 폴더를 확인해주세요.", "확인");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            Phase0IntroSequenceData so = AssetDatabase.LoadAssetAtPath<Phase0IntroSequenceData>(path);
            SerializedObject serialized = new SerializedObject(so);

            // 기본 설정
            serialized.FindProperty("initialBlackHold").floatValue = 2f;
            serialized.FindProperty("defaultCharsPerSecond").floatValue = 16.7f;
            serialized.FindProperty("bgmFadeInDuration").floatValue = 2.5f;
            serialized.FindProperty("bgmTargetVolume").floatValue = 0.15f;
            serialized.FindProperty("bgmStartDelay").floatValue = 0f;
            serialized.FindProperty("typeVolume").floatValue = 0.35f;
            serialized.FindProperty("typePitchJitter").floatValue = 0.05f;
            serialized.FindProperty("playEveryNCharacters").intValue = 2;
            serialized.FindProperty("backdropFps").floatValue = 1f;
            serialized.FindProperty("backdropFadeInDuration").floatValue = 1f;
            serialized.FindProperty("backdropFadeInDelay").floatValue = 25f;
            serialized.FindProperty("sceneVisualId").stringValue = "SCN-05-SHAKE";

            // v3 스크립트 전체 Lines 데이터
            LineData[] script = new[]
            {
                // SCN-01 | 암전 오프닝
                new LineData("??? 찾았다.",                                                             true,  0f,   1.5f),
                new LineData("B ...",                                                                    true,  0f,   1.0f),
                new LineData("??? 오래 걸려서 미안.",                                                   true,  0f,   2.0f),

                // SCN-02 | 세계 소개
                new LineData("내가 사는 이 마을은 작은 하늘 섬이라,",                                  true,  0f,   0.3f),
                new LineData("비교적 살기 좋은 곳이라고 한다.",                                         false, 0f,   0.8f),
                new LineData("나는 이 밖을 나가본 적 없지만,",                                          true,  0f,   0f),
                new LineData("나간 사람은 아무도 돌아오지 않았기 때문에",                               false, 0f,   0f),
                new LineData("이 말을 검증할 순 없다.",                                                  false, 0f,   1.5f),

                // SCN-03 | 싸움
                new LineData("얼마 후 마물이 마을을 공격했다.",                                         true,  0f,   0.8f),
                new LineData("나는 관성적으로 마을을 위해 싸웠다.",                                      true,  0f,   0f,   14f),
                new LineData("싸우고, 또 싸웠다.",                                                       false, 0f,   0.5f),
                new LineData("빠르게 검을 휘두르는 내가 어색하면서도 익숙했다.",                        true,  0f,   0.8f),
                new LineData("'내가 이걸 누구에게 배웠지?'",                                            true,  0f,   1.5f, 12f),
                new LineData("죽고, 다시 살아났다.",                                                     true,  0f,   0f),
                new LineData("죽고, 다시 살아났다.",                                                     true,  0f,   1.2f, 22f),
                new LineData("마을 사람들을 지켜야 하니까.",                                             true,  0f,   1.0f),

                // SCN-04 | 이름들
                new LineData("소꿉친구 이름은 유나였다가, 에이버리였다가, 어떨 땐 하퍼였다.",           true,  0f,   0f,   14f),
                new LineData("이상한 건 나인 줄 알았다.",                                                true,  0f,   1.0f),
                new LineData("이름만 다른 건 아니었다.",                                                  true,  0f,   0.8f),
                new LineData("유나는 꽃을 싫어했었다.",                                                  true,  0f,   3.0f),

                // SCN-05 | 던전의 끝
                new LineData("셀 수 없이 쓰러지다 보면, 어떠한 역경의 마지막에 이르기도 한다.",        true,  0f,   0f),
                new LineData("이 던전의 끝에서 그 원인을 제거하는 데에 성공했다.",                      true,  0f,   0.5f),
                new LineData("무언가를 없앴다는 감각이 검을 통해 손으로 이어졌을 때,",                  true,  0f,   0f),
                new LineData("세상이 흔들렸다.",                                                          true,  0f,   0.5f),
                new LineData("그리고 멈췄다.",                                                            true,  0f,   2.5f),
                new LineData("그 순간은 찰나인 듯 영겁같아서 기억이 온전치 않다.",                      true,  0f,   0f),
                new LineData("유일한 기억의 조각은, 처음 듣는 목소리가 들렸다는 것 정도였다.",          true,  0f,   2.0f, 12f),

                // SCN-06 | 소꿉친구의 첫 목소리 (instantReveal)
                new LineData("??? 계속 마을을 지켜주세요.",                                              true,  0f,   4.0f, 0f, true),

                // SCN-07 | 마지막 장면
                new LineData("나는 또다시 마을에서 눈을 떴다.",                                          true,  0f,   0.8f),
                new LineData("익숙한 듯 옆에 있는 사람을 바라봤다.",                                    true,  0f,   1.0f),
                new LineData("넌 누구지?",                                                               true,  0f,   1.0f),
            };

            SerializedProperty linesProp = serialized.FindProperty("lines");
            linesProp.arraySize = script.Length;

            for (int i = 0; i < script.Length; i++)
            {
                SerializedProperty elem = linesProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("text").stringValue          = script[i].text;
                elem.FindPropertyRelative("clearBeforeLine").boolValue = script[i].clearBefore;
                elem.FindPropertyRelative("delayBefore").floatValue    = script[i].delayBefore;
                elem.FindPropertyRelative("delayAfter").floatValue     = script[i].delayAfter;
                elem.FindPropertyRelative("charsPerSecond").floatValue = script[i].cps;
                elem.FindPropertyRelative("instantReveal").boolValue   = script[i].instant;
            }

            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "완료",
                $"Lines {script.Length}개 자동 입력 완료!\n\n남은 작업:\n1) Inspector에서 BGM Clip / Type Clip 드래그 연결\n2) Play로 검증",
                "확인"
            );

            EditorGUIUtility.PingObject(so);
            Selection.activeObject = so;
        }
    }
}

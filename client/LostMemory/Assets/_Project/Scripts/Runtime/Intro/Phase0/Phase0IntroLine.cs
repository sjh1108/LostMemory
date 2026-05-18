using System;
using UnityEngine;

namespace LostMemory.Intro.Phase0
{
    [Serializable]
    public struct Phase0IntroLine
    {
        [TextArea(1, 4)]
        public string text;

        [Min(0f)] public float delayBefore;

        [Tooltip("0이면 SO 기본값(defaultCharsPerSecond) 사용")]
        [Min(0f)] public float charsPerSecond;

        [Tooltip("타이핑 없이 즉시 출력")]
        public bool instantReveal;

        [Tooltip("이 줄 시작 전 누적 텍스트 초기화")]
        public bool clearBeforeLine;

        [Min(0f)] public float delayAfter;
    }
}

using UnityEngine;

namespace LostMemory.Dialogue
{
    /// <summary>
    /// 대화창 시각 디자인 묶음 (배경/프레임/색/속도).
    /// 같은 prefab + 서로 다른 Skin SO 교체로 보스용/상점용/스토리용 디자인 분기.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueSkin", menuName = "LostMemory/Dialogue/Dialogue Skin")]
    public sealed class DialogueSkin : ScriptableObject
    {
        [Header("Sprites")]
        [Tooltip("패널 전체 배경 (9-slice 권장)")]
        public Sprite backgroundSprite;
        [Tooltip("이름 박스 배경 (9-slice 권장)")]
        public Sprite namePlateSprite;
        [Tooltip("대사 박스 배경 (9-slice 권장). null 이면 배경 sprite 만 사용")]
        public Sprite textBoxSprite;
        [Tooltip("'다음 ▼' 표시 sprite")]
        public Sprite continueIndicatorSprite;

        [Header("Colors")]
        public Color backgroundColor = new Color(0f, 0f, 0f, 0.85f);
        public Color speakerNameColor = Color.white;
        public Color dialogueTextColor = Color.white;

        [Header("Portrait Alpha")]
        [Tooltip("화자 측 portrait 알파 (강조). 기본 1.0")]
        [Range(0f, 1f)] public float speakerPortraitAlpha = 1.0f;
        [Tooltip("비화자 측 portrait 알파 (어둡게). 기본 0.4 — 사용자 결정사항")]
        [Range(0f, 1f)] public float idlePortraitAlpha = 0.4f;

        [Header("Typewriter")]
        [Tooltip("초당 글자 수. 0 또는 음수면 즉시 표시.")]
        [Min(0f)] public float charactersPerSecond = 30f;
        [Tooltip("타이프라이터 종료 후 ContinueIndicator 깜빡임 주기 (초). 0 이면 깜빡임 X")]
        [Min(0f)] public float continueIndicatorBlinkInterval = 0.5f;
    }
}

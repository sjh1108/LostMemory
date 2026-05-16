using UnityEngine;

namespace LostMemory.Intro.Phase0
{
    [CreateAssetMenu(fileName = "Phase0IntroSequenceData_New", menuName = "LostMemory/Intro/Phase0 Intro Sequence Data")]
    public sealed class Phase0IntroSequenceData : ScriptableObject
    {
        [Header("Flow")]
        [Min(0f)] [SerializeField] private float initialBlackHold = 2f;
        [Min(1f)] [SerializeField] private float defaultCharsPerSecond = 18f;
        [SerializeField] private Phase0IntroLine[] lines = System.Array.Empty<Phase0IntroLine>();

        [Header("BGM")]
        [SerializeField] private AudioClip bgmClip;
        [Min(0f)] [SerializeField] private float bgmFadeInDuration = 2.5f;
        [Range(0f, 1f)] [SerializeField] private float bgmTargetVolume = 0.15f;
        [Min(0f)] [SerializeField] private float bgmStartDelay = 0f;

        [Header("Typing SFX")]
        [SerializeField] private AudioClip typeClip;
        [Range(0f, 1f)] [SerializeField] private float typeVolume = 0.35f;
        [Range(0f, 0.5f)] [SerializeField] private float typePitchJitter = 0.05f;
        [Min(1)] [SerializeField] private int playEveryNCharacters = 2;

        [Header("Backdrop (배경 일러스트 루프)")]
        [Tooltip("순서대로 깜빡일 스프라이트. 비어있으면 검은 배경 유지.")]
        [SerializeField] private Sprite[] backdropFrames = System.Array.Empty<Sprite>();
        [Min(0.1f)] [SerializeField] private float backdropFps = 6f;
        [Min(0f)] [SerializeField] private float backdropFadeInDuration = 1.0f;
        [Tooltip("암전 끝난 시점(=initialBlackHold) 기준으로 N초 후에 페이드인 시작.")]
        [Min(0f)] [SerializeField] private float backdropFadeInDelay = 0f;

        [Header("Visuals (확장 포인트)")]
        [Tooltip("빈 문자열이면 시각 처리 없음. SCN-05 같은 컷씬용.")]
        [SerializeField] private string sceneVisualId = string.Empty;

        public float InitialBlackHold => initialBlackHold;
        public float DefaultCharsPerSecond => defaultCharsPerSecond;
        public Phase0IntroLine[] Lines => lines ?? System.Array.Empty<Phase0IntroLine>();

        public AudioClip BgmClip => bgmClip;
        public float BgmFadeInDuration => bgmFadeInDuration;
        public float BgmTargetVolume => bgmTargetVolume;
        public float BgmStartDelay => bgmStartDelay;

        public AudioClip TypeClip => typeClip;
        public float TypeVolume => typeVolume;
        public float TypePitchJitter => typePitchJitter;
        public int PlayEveryNCharacters => playEveryNCharacters;

        public Sprite[] BackdropFrames => backdropFrames ?? System.Array.Empty<Sprite>();
        public float BackdropFps => backdropFps;
        public float BackdropFadeInDuration => backdropFadeInDuration;
        public float BackdropFadeInDelay => backdropFadeInDelay;
        public bool HasBackdrop => backdropFrames != null && backdropFrames.Length > 0;

        public string SceneVisualId => sceneVisualId ?? string.Empty;
        public bool HasSceneVisual => !string.IsNullOrEmpty(sceneVisualId);
    }
}

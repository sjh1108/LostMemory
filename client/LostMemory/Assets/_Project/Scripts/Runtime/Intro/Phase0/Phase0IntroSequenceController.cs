using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Intro.Phase0
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Intro/Phase0 Intro Sequence Controller")]
    public sealed class Phase0IntroSequenceController : MonoBehaviour
    {
        public const string IntroSeenPrefKey = "LostMemory.IntroSeen";

        [SerializeField] private Phase0IntroSequenceData sequenceData;
        [SerializeField] private Phase0NarrationTypewriter typewriter;
        [SerializeField] private Phase0NarrationBackdrop backdrop;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private GameObject visualPlayerOwner;
        [SerializeField] private bool autoStartOnEnable = true;
        [SerializeField] private bool debugLogging;

        [Header("Scene Flow")]
        [SerializeField, Tooltip("인트로 완료/스킵 후 로드할 다음 씬 이름.")]
        private string nextSceneName = "Town";
        [SerializeField, Tooltip("완료/스킵 시 PlayerPrefs 에 시청 기록(IntroSeen=1) 저장.")]
        private bool markSeenOnComplete = true;
        [SerializeField, Tooltip("완료/스킵 후 자동으로 다음 씬 로드.")]
        private bool loadNextSceneOnComplete = true;

        [Header("Skip Input")]
        [SerializeField, Tooltip("스킵 키. 이 키를 holdDuration 동안 누르면 인트로 스킵.")]
        private KeyCode skipKey = KeyCode.P;
        [SerializeField, Tooltip("스킵 키를 누르고 있어야 하는 시간(초).")]
        private float skipHoldDuration = 1.0f;

        private Coroutine _introRoutine;
        private Coroutine _bgmFadeRoutine;
        private Coroutine _backdropRoutine;
        private bool _isRunning;
        private float _skipHoldTime;

        public event Action IntroStarted;
        public event Action IntroCompleted;

        public bool IsRunning => _isRunning;

        public KeyCode SkipKey => skipKey;

        public float SkipProgress => _isRunning
            ? Mathf.Clamp01(_skipHoldTime / Mathf.Max(skipHoldDuration, 0.0001f))
            : 0f;

        private void Reset()
        {
            typewriter = GetComponentInChildren<Phase0NarrationTypewriter>();
            bgmSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (typewriter != null)
            {
                typewriter.Clear();
            }

            // BGM AudioSource 를 Music mixer group 으로 라우팅 → 옵션 메뉴 Music 슬라이더 영향.
            if (bgmSource != null
                && LostMemory.Audio.GameAudioSettings.Instance != null
                && LostMemory.Audio.GameAudioSettings.Instance.MusicGroup != null)
            {
                bgmSource.outputAudioMixerGroup =
                    LostMemory.Audio.GameAudioSettings.Instance.MusicGroup;
            }

            if (autoStartOnEnable && sequenceData != null)
            {
                BeginIntro();
            }
        }

        private void OnDisable()
        {
            StopActive();
        }

        private void Update()
        {
            if (!_isRunning) return;

            if (Input.GetKey(skipKey))
            {
                _skipHoldTime += Time.unscaledDeltaTime;
                if (_skipHoldTime >= skipHoldDuration)
                {
                    Skip();
                }
            }
            else if (_skipHoldTime > 0f)
            {
                _skipHoldTime = 0f;
            }
        }

        public void Skip()
        {
            if (!_isRunning) return;
            Log($"인트로 스킵 ({skipKey} {skipHoldDuration}s hold).");
            StopActive();
            CompleteIntro();
        }

        public void BeginIntro()
        {
            if (_isRunning)
            {
                Log("중복 인트로 시작 요청 무시.");
                return;
            }

            if (sequenceData == null)
            {
                Log("sequenceData 미할당. 인트로 시작 불가.");
                return;
            }

            _introRoutine = StartCoroutine(RunIntroSequence());
        }

        private IEnumerator RunIntroSequence()
        {
            _isRunning = true;
            IntroStarted?.Invoke();
            Log("Phase0 인트로 시작.");

            Phase0IntroSequenceData data = sequenceData;

            if (typewriter != null)
            {
                typewriter.Clear();
            }

            if (data.BgmClip != null && bgmSource != null)
            {
                if (_bgmFadeRoutine != null)
                {
                    StopCoroutine(_bgmFadeRoutine);
                }
                _bgmFadeRoutine = StartCoroutine(PlayBgmWithFadeIn(data));
            }

            if (data.InitialBlackHold > 0f)
            {
                yield return new WaitForSecondsRealtime(data.InitialBlackHold);
            }

            if (backdrop != null && data.HasBackdrop)
            {
                if (_backdropRoutine != null)
                {
                    StopCoroutine(_backdropRoutine);
                }
                _backdropRoutine = StartCoroutine(StartBackdropParallel(data));
            }

            Phase0IntroLine[] lines = data.Lines;
            for (int i = 0; i < lines.Length; i++)
            {
                Phase0IntroLine line = lines[i];

                if (line.delayBefore > 0f)
                {
                    yield return new WaitForSecondsRealtime(line.delayBefore);
                }

                if (line.clearBeforeLine && typewriter != null)
                {
                    typewriter.Clear();
                }

                if (typewriter != null && !string.IsNullOrEmpty(line.text))
                {
                    if (line.instantReveal)
                    {
                        typewriter.AppendInstant(line.text);
                    }
                    else
                    {
                        float cps = line.charsPerSecond > 0f ? line.charsPerSecond : data.DefaultCharsPerSecond;
                        yield return typewriter.AppendTyped(line.text, cps, data);
                    }
                }

                if (line.delayAfter > 0f)
                {
                    yield return new WaitForSecondsRealtime(line.delayAfter);
                }
            }

            if (data.HasSceneVisual && visualPlayerOwner != null)
            {
                IPhase0SceneVisualPlayer visualPlayer = visualPlayerOwner.GetComponent<IPhase0SceneVisualPlayer>();
                if (visualPlayer != null)
                {
                    bool visualCompleted = false;
                    visualPlayer.Play(data.SceneVisualId, () => visualCompleted = true);
                    while (!visualCompleted)
                    {
                        yield return null;
                    }
                }
                else
                {
                    Log($"sceneVisualId='{data.SceneVisualId}' 지정됐지만 IPhase0SceneVisualPlayer 미발견. 스킵.");
                }
            }

            CompleteIntro();
        }

        private IEnumerator StartBackdropParallel(Phase0IntroSequenceData data)
        {
            if (data.BackdropFadeInDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(data.BackdropFadeInDelay);
            }

            backdrop.StartLoop(data.BackdropFrames, data.BackdropFps);
            yield return backdrop.FadeIn(data.BackdropFadeInDuration);
            _backdropRoutine = null;
        }

        private IEnumerator PlayBgmWithFadeIn(Phase0IntroSequenceData data)
        {
            if (data.BgmStartDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(data.BgmStartDelay);
            }

            bgmSource.clip = data.BgmClip;
            bgmSource.loop = true;
            bgmSource.volume = 0f;
            bgmSource.Play();

            float duration = data.BgmFadeInDuration;
            float target = data.BgmTargetVolume;

            if (duration <= 0f)
            {
                bgmSource.volume = target;
                _bgmFadeRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0f, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            bgmSource.volume = target;
            _bgmFadeRoutine = null;
        }

        private void CompleteIntro()
        {
            _introRoutine = null;
            _isRunning = false;
            _skipHoldTime = 0f;
            IntroCompleted?.Invoke();
            Log("Phase0 인트로 완료.");

            if (markSeenOnComplete)
            {
                PlayerPrefs.SetInt(IntroSeenPrefKey, 1);
                PlayerPrefs.Save();
                Log($"PlayerPrefs '{IntroSeenPrefKey}' = 1 저장.");
            }

            if (loadNextSceneOnComplete && !string.IsNullOrEmpty(nextSceneName))
            {
                Log($"다음 씬 로드: {nextSceneName}");
                SceneManager.LoadScene(nextSceneName);
            }
        }

        private void StopActive()
        {
            if (_introRoutine != null)
            {
                StopCoroutine(_introRoutine);
                _introRoutine = null;
            }

            if (_bgmFadeRoutine != null)
            {
                StopCoroutine(_bgmFadeRoutine);
                _bgmFadeRoutine = null;
            }

            if (_backdropRoutine != null)
            {
                StopCoroutine(_backdropRoutine);
                _backdropRoutine = null;
            }

            if (backdrop != null)
            {
                backdrop.StopLoop();
            }

            _isRunning = false;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[Phase0Intro] " + message, this);
            }
        }
    }
}

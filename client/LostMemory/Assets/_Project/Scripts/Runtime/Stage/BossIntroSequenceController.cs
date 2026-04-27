using System;
using System.Collections;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Boss Intro Sequence Controller")]
    public sealed class BossIntroSequenceController : MonoBehaviour
    {
        [SerializeField] private BossIntroSequenceData sequenceData;
        [SerializeField] private Animator bossAnimator;
        [SerializeField] private string entryTriggerName = string.Empty;
        [SerializeField] private string entryStateName = "Entry";
        [SerializeField, Min(0)] private int entryStateLayer;
        [SerializeField] private GameObject[] introVisibilityTargets = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject dialoguePlayerOwner;
        [SerializeField] private bool debugLogging;

        private Coroutine _introRoutine;
        private Character[] _cachedPlayers = System.Array.Empty<Character>();
        private BossRoomTransitionCompletedContext _currentContext;
        private bool _isIntroRunning;

        public event Action<BossRoomTransitionCompletedContext> IntroStarted;
        public event Action<BossRoomTransitionCompletedContext> IntroCompleted;

        public bool IsIntroRunning => _isIntroRunning;
        public BossRoomTransitionCompletedContext CurrentContext => _currentContext;
        public Animator BossAnimator => bossAnimator;

        private void Reset()
        {
            bossAnimator = ResolveBossAnimator();
            AutoAssignVisibilityTargets();
            dialoguePlayerOwner = gameObject;
        }

        private void OnEnable()
        {
            bossAnimator ??= ResolveBossAnimator();
            AutoAssignVisibilityTargets();

            if (!_isIntroRunning && sequenceData != null && sequenceData.HideBossBeforeEntry)
            {
                SetIntroVisibility(isVisible: false);
            }
        }

        private void OnDisable()
        {
            StopActiveIntro(unfreezePlayers: true);
            SetIntroVisibility(isVisible: true);
        }

        public void BeginIntro(BossRoomTransitionCompletedContext context)
        {
            if (_isIntroRunning)
            {
                Log("Ignored duplicate intro start request.");
                return;
            }

            AutoAssignVisibilityTargets();
            _currentContext = context;
            _cachedPlayers = CopyPlayers(context.Players);
            _introRoutine = StartCoroutine(RunIntroSequence(context));
        }

        private IEnumerator RunIntroSequence(BossRoomTransitionCompletedContext context)
        {
            _isIntroRunning = true;
            IntroStarted?.Invoke(context);
            Log("Boss intro sequence started.");

            BossIntroSequenceData data = sequenceData;
            bool shouldLockPlayers = data == null || data.LockPlayersDuringIntro;
            bool shouldUnlockPlayers = data == null || data.UnlockPlayersOnComplete;

            if (data == null)
            {
                SetIntroVisibility(isVisible: true);
                Log("BossIntroSequenceData is not assigned. Running fallback intro flow.");
            }

            if (shouldLockPlayers)
            {
                FreezeCachedPlayers();
            }
            else
            {
                UnfreezeCachedPlayers();
            }

            if (data != null)
            {
                if (data.HideBossBeforeEntry)
                {
                    SetIntroVisibility(isVisible: false);
                }
                else
                {
                    SetIntroVisibility(isVisible: true);
                }

                if (data.DelayBeforeEntry > 0f)
                {
                    yield return new WaitForSecondsRealtime(data.DelayBeforeEntry);
                }

                if (data.HideBossBeforeEntry)
                {
                    SetIntroVisibility(isVisible: true);
                }

                if (data.PlayEntryAnimation)
                {
                    PlayEntryAnimation();
                    if (data.EntryDuration > 0f)
                    {
                        yield return new WaitForSecondsRealtime(data.EntryDuration);
                    }
                }

                if (data.DelayBeforeDialogue > 0f)
                {
                    yield return new WaitForSecondsRealtime(data.DelayBeforeDialogue);
                }

                if (data.ShouldPlayDialogue)
                {
                    bool dialogueCompleted = false;
                    PlayDialogue(data, context, () => dialogueCompleted = true);
                    while (!dialogueCompleted)
                    {
                        yield return null;
                    }
                }

                if (data.DelayAfterDialogue > 0f)
                {
                    yield return new WaitForSecondsRealtime(data.DelayAfterDialogue);
                }
            }

            CompleteIntro(context, shouldUnlockPlayers);
        }

        private void PlayEntryAnimation()
        {
            if (bossAnimator == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(entryTriggerName))
            {
                bossAnimator.ResetTrigger(entryTriggerName);
                bossAnimator.SetTrigger(entryTriggerName);
                return;
            }

            if (!string.IsNullOrWhiteSpace(entryStateName))
            {
                bossAnimator.Play(entryStateName, entryStateLayer, 0f);
            }
        }

        private void PlayDialogue(
            BossIntroSequenceData data,
            BossRoomTransitionCompletedContext context,
            Action onCompleted)
        {
            IBossIntroDialoguePlayer dialoguePlayer = null;
            if (dialoguePlayerOwner != null)
            {
                dialoguePlayer = dialoguePlayerOwner.GetComponent<IBossIntroDialoguePlayer>();
            }

            if (dialoguePlayer == null)
            {
                Log("Dialogue player missing. Continuing intro without dialogue playback.");
                onCompleted?.Invoke();
                return;
            }

            dialoguePlayer.Play(data, context, onCompleted);
        }

        private void CompleteIntro(BossRoomTransitionCompletedContext context, bool shouldUnlockPlayers)
        {
            if (shouldUnlockPlayers)
            {
                UnfreezeCachedPlayers();
            }

            _introRoutine = null;
            _isIntroRunning = false;
            IntroCompleted?.Invoke(context);
            Log("Boss intro sequence completed.");
        }

        private void StopActiveIntro(bool unfreezePlayers)
        {
            if (_introRoutine != null)
            {
                StopCoroutine(_introRoutine);
                _introRoutine = null;
            }

            if (!_isIntroRunning)
            {
                return;
            }

            if (unfreezePlayers)
            {
                UnfreezeCachedPlayers();
            }

            SetIntroVisibility(isVisible: true);
            _isIntroRunning = false;
            Log("Boss intro sequence stopped.");
        }

        private void AutoAssignVisibilityTargets()
        {
            if (introVisibilityTargets != null && introVisibilityTargets.Length > 0)
            {
                return;
            }

            Transform visualChild = transform.Find("Visual");
            if (visualChild != null)
            {
                introVisibilityTargets = new[] { visualChild.gameObject };
                return;
            }

            if (bossAnimator != null)
            {
                introVisibilityTargets = new[] { bossAnimator.gameObject };
            }
        }

        private Animator ResolveBossAnimator()
        {
            Transform visualChild = transform.Find("Visual");
            if (visualChild != null)
            {
                Transform spriteChild = visualChild.Find("BerthaSprite");
                if (spriteChild != null)
                {
                    Animator spriteAnimator = spriteChild.GetComponent<Animator>();
                    if (spriteAnimator != null)
                    {
                        return spriteAnimator;
                    }
                }

                Animator visualAnimator = visualChild.GetComponentInChildren<Animator>(includeInactive: true);
                if (visualAnimator != null)
                {
                    return visualAnimator;
                }
            }

            return GetComponentInChildren<Animator>(includeInactive: true);
        }

        private void SetIntroVisibility(bool isVisible)
        {
            if (introVisibilityTargets == null || introVisibilityTargets.Length == 0)
            {
                return;
            }

            for (int i = 0; i < introVisibilityTargets.Length; i++)
            {
                GameObject target = introVisibilityTargets[i];
                if (target != null && target.activeSelf != isVisible)
                {
                    target.SetActive(isVisible);
                }
            }
        }

        private void FreezeCachedPlayers()
        {
            for (int i = 0; i < _cachedPlayers.Length; i++)
            {
                Character player = _cachedPlayers[i];
                if (player != null)
                {
                    player.Freeze();
                }
            }
        }

        private void UnfreezeCachedPlayers()
        {
            for (int i = 0; i < _cachedPlayers.Length; i++)
            {
                Character player = _cachedPlayers[i];
                if (player != null)
                {
                    player.UnFreeze();
                }
            }
        }

        private static Character[] CopyPlayers(Character[] players)
        {
            if (players == null || players.Length == 0)
            {
                return System.Array.Empty<Character>();
            }

            Character[] copiedPlayers = new Character[players.Length];
            System.Array.Copy(players, copiedPlayers, players.Length);
            return copiedPlayers;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BossIntroSequence] " + message, this);
            }
        }
    }
}

using System.Collections.Generic;
using LostMemory.Stage;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Brain Animation Controller")]
    public sealed class BerthaBrainAnimationController : MonoBehaviour, MMEventListener<AIStateEvent>
    {
        private static readonly string[] RequiredIdleStateNames =
        {
            "Detecting",
            "LightTelegraph",
            "LightRecover",
            "Light2Telegraph",
            "Light2Recover",
            "HeavyTelegraph",
            "HeavyRecover",
            "NormalDashTelegraph",
            "NormalDashRecover",
            "DashTelegraph",
            "DashRecover",
            "FullTelegraph",
            "FullRecover",
            "ProjectileBarrageTelegraph",
            "ProjectileBarrageRecover",
            "ProjectileStormTelegraph",
            "ProjectileStormRecover",
            "Recover"
        };

        [SerializeField] private AIBrain brain;
        [SerializeField] private Animator animator;
        [SerializeField] private BossIntroSequenceController introSequenceController;
        [SerializeField] private string movingStateName = "Moving";
        [SerializeField] private string[] idleStateNames = { "Detecting", "LightTelegraph", "LightRecover", "Light2Telegraph", "Light2Recover", "HeavyTelegraph", "HeavyRecover", "NormalDashTelegraph", "NormalDashRecover", "DashTelegraph", "DashRecover", "FullTelegraph", "FullRecover", "ProjectileBarrageTelegraph", "ProjectileBarrageRecover", "ProjectileStormTelegraph", "ProjectileStormRecover", "Recover" };
        [SerializeField] private string idleAnimationStateName = "Idle";
        [SerializeField] private string walkAnimationStateName = "Walk";
        [SerializeField, Min(0)] private int animationLayer;
        [SerializeField] private bool debugLogging;

        private bool _introPlaying;
        private string _currentAnimationStateName;
        private BossIntroSequenceController _boundIntroSequenceController;

        private void Reset()
        {
            RefreshReferences();
            EnsureIdleStatesConfigured();
        }

        private void Awake()
        {
            RefreshReferences();
            EnsureIdleStatesConfigured();
        }

        private void OnEnable()
        {
            RefreshReferences();
            EnsureIdleStatesConfigured();
            RebindIntroSequenceEvents();
            this.MMEventStartListening<AIStateEvent>();
            SyncAnimationToCurrentState();
        }

        private void OnDisable()
        {
            this.MMEventStopListening<AIStateEvent>();
            RebindIntroSequenceEvents(null);
        }

        public void OnMMEvent(AIStateEvent stateEvent)
        {
            if (stateEvent.Brain != brain || _introPlaying)
            {
                return;
            }

            string enteringState = stateEvent.EnterState != null ? stateEvent.EnterState.StateName : string.Empty;
            if (string.IsNullOrWhiteSpace(enteringState))
            {
                return;
            }

            if (enteringState == movingStateName)
            {
                PlayAnimation(walkAnimationStateName);
                return;
            }

            if (ContainsState(idleStateNames, enteringState))
            {
                PlayAnimation(idleAnimationStateName);
            }
        }

        public void RefreshReferences()
        {
            brain ??= GetComponent<AIBrain>();
            introSequenceController ??= GetComponent<BossIntroSequenceController>();
            if (animator == null)
            {
                animator = introSequenceController != null
                    ? introSequenceController.BossAnimator
                    : ResolveAnimator();
            }
        }

        public void Configure(
            AIBrain configuredBrain,
            Animator configuredAnimator,
            BossIntroSequenceController configuredIntroSequenceController)
        {
            brain = configuredBrain;
            animator = configuredAnimator;
            introSequenceController = configuredIntroSequenceController;
            RefreshReferences();
            EnsureIdleStatesConfigured();
            RebindIntroSequenceEvents();
            SyncAnimationToCurrentState();
        }

        private void OnIntroStarted(BossRoomTransitionCompletedContext _)
        {
            _introPlaying = true;
        }

        private void OnIntroCompleted(BossRoomTransitionCompletedContext _)
        {
            _introPlaying = false;
            SyncAnimationToCurrentState();
        }

        private void PlayAnimation(string stateName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            if (_currentAnimationStateName == stateName && IsAnimationStatePlaying(stateName))
            {
                return;
            }

            animator.Play(stateName, animationLayer, 0f);
            _currentAnimationStateName = stateName;
            Log("Play animation: " + stateName);
        }

        private bool IsAnimationStatePlaying(string stateName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            return animator.GetCurrentAnimatorStateInfo(animationLayer).IsName(stateName);
        }

        private static bool ContainsState(string[] stateNames, string targetStateName)
        {
            if (stateNames == null || string.IsNullOrWhiteSpace(targetStateName))
            {
                return false;
            }

            for (int i = 0; i < stateNames.Length; i++)
            {
                if (stateNames[i] == targetStateName)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureIdleStatesConfigured()
        {
            List<string> mergedStates = new List<string>();

            if (idleStateNames != null)
            {
                for (int i = 0; i < idleStateNames.Length; i++)
                {
                    string stateName = idleStateNames[i];
                    if (!string.IsNullOrWhiteSpace(stateName) && !mergedStates.Contains(stateName))
                    {
                        mergedStates.Add(stateName);
                    }
                }
            }

            for (int i = 0; i < RequiredIdleStateNames.Length; i++)
            {
                string requiredStateName = RequiredIdleStateNames[i];
                if (!mergedStates.Contains(requiredStateName))
                {
                    mergedStates.Add(requiredStateName);
                }
            }

            idleStateNames = mergedStates.ToArray();
        }

        private void SyncAnimationToCurrentState()
        {
            if (_introPlaying || brain == null || brain.CurrentState == null)
            {
                return;
            }

            string currentStateName = brain.CurrentState.StateName;
            if (currentStateName == movingStateName)
            {
                PlayAnimation(walkAnimationStateName);
                return;
            }

            if (ContainsState(idleStateNames, currentStateName))
            {
                PlayAnimation(idleAnimationStateName);
            }
        }

        private void RebindIntroSequenceEvents()
        {
            RebindIntroSequenceEvents(introSequenceController);
        }

        private void RebindIntroSequenceEvents(BossIntroSequenceController nextController)
        {
            if (_boundIntroSequenceController != null)
            {
                _boundIntroSequenceController.IntroStarted -= OnIntroStarted;
                _boundIntroSequenceController.IntroCompleted -= OnIntroCompleted;
            }

            _boundIntroSequenceController = nextController;

            if (_boundIntroSequenceController != null && isActiveAndEnabled)
            {
                _boundIntroSequenceController.IntroStarted += OnIntroStarted;
                _boundIntroSequenceController.IntroCompleted += OnIntroCompleted;
            }
        }

        private Animator ResolveAnimator()
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

                Animator visualAnimator = visualChild.GetComponentInChildren<Animator>(true);
                if (visualAnimator != null)
                {
                    return visualAnimator;
                }
            }

            return GetComponentInChildren<Animator>(true);
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BerthaBrainAnimation] " + message, this);
            }
        }
    }
}

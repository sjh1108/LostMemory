using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Boss Room Encounter Starter")]
    public sealed class BossRoomEncounterStarter : MonoBehaviour
    {
        [SerializeField] private BossRoomLocalTransitionDriver transitionDriver;
        [SerializeField] private BossIntroSequenceController introSequenceController;
        [SerializeField] private GameObject encounterControllerOwner;
        [SerializeField] private bool disableTransitionDriverAutoUnfreeze = true;
        [SerializeField] private bool debugLogging;

        private bool _autoUnfreezeOverrideApplied;
        private bool _previousAutoUnfreezeValue;

        private void Reset()
        {
            transitionDriver = GetComponent<BossRoomLocalTransitionDriver>();
            introSequenceController = GetComponentInChildren<BossIntroSequenceController>(includeInactive: true);
        }

        private void OnEnable()
        {
            transitionDriver ??= GetComponent<BossRoomLocalTransitionDriver>();
            introSequenceController ??= GetComponentInChildren<BossIntroSequenceController>(includeInactive: true);

            if (transitionDriver != null)
            {
                transitionDriver.TransitionCompleted += HandleTransitionCompleted;
                if (disableTransitionDriverAutoUnfreeze)
                {
                    _previousAutoUnfreezeValue = transitionDriver.AutoUnfreezePlayers;
                    transitionDriver.AutoUnfreezePlayers = false;
                    _autoUnfreezeOverrideApplied = true;
                }
            }

            if (introSequenceController != null)
            {
                introSequenceController.IntroCompleted += HandleIntroCompleted;
            }
        }

        private void OnDisable()
        {
            if (transitionDriver != null)
            {
                transitionDriver.TransitionCompleted -= HandleTransitionCompleted;
                if (_autoUnfreezeOverrideApplied)
                {
                    transitionDriver.AutoUnfreezePlayers = _previousAutoUnfreezeValue;
                    _autoUnfreezeOverrideApplied = false;
                }
            }

            if (introSequenceController != null)
            {
                introSequenceController.IntroCompleted -= HandleIntroCompleted;
            }
        }

        private void HandleTransitionCompleted(BossRoomTransitionCompletedContext context)
        {
            if (introSequenceController != null)
            {
                introSequenceController.BeginIntro(context);
                return;
            }

            ReleasePlayers(context.Players);
            StartEncounter();
        }

        private void HandleIntroCompleted(BossRoomTransitionCompletedContext context)
        {
            StartEncounter();
        }

        private void StartEncounter()
        {
            IBossEncounterController encounterController = null;
            if (encounterControllerOwner != null)
            {
                encounterController = encounterControllerOwner.GetComponent<IBossEncounterController>();
            }

            if (encounterController == null)
            {
                Log("Encounter controller missing. Intro finished without combat activation.");
                return;
            }

            encounterController.BeginEncounter();
            Log("Encounter started.");
        }

        private static void ReleasePlayers(Character[] players)
        {
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Length; i++)
            {
                Character player = players[i];
                if (player != null)
                {
                    player.UnFreeze();
                }
            }
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BossRoomEncounterStarter] " + message, this);
            }
        }
    }
}

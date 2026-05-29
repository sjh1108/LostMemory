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

        // Player Analytics — 보스 진입 시 stage_entered 발화에 사용. 보스 prefab 의 RoomEntryRuntimeController 참조.
        private RoomEntryRuntimeController _roomController;

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
            _roomController ??= GetComponentInParent<RoomEntryRuntimeController>(includeInactive: true);

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
            // Player Analytics — 보스 진입 시 stage_entered 명시 발화.
            // 일반 RoomEntryRuntimeController.BeginRoomEntry() 흐름을 거치지 않고 직접 텔레포트 + 인트로로 이어지므로
            // RoomEntered 이벤트가 자동 발화되지 않음 → RunManager 의 RoomEntered 구독 hook 도 안 걸림. 여기서 보강.
            //
            // GetComponentInParent 가 못 잡는 케이스 (BossRoomEncounterStarter 가 RoomEntryRuntimeController 와 다른 hierarchy) 대비 FindObjectOfType fallback.
            if (_roomController == null)
            {
                _roomController = FindObjectOfType<RoomEntryRuntimeController>();
            }
            var analytics = LostMemory.Networking.Analytics.AnalyticsClient.Instance;
            if (analytics != null && _roomController != null && _roomController.RoomData != null)
            {
                analytics.FireStageEntered(
                    stageId: _roomController.RoomData.RoomId,
                    partySize: ResolvePartySize(),
                    prevStageId: analytics.LastStageId);
            }

            if (introSequenceController != null)
            {
                introSequenceController.BeginIntro(context);
                return;
            }

            ReleasePlayers(context.Players);
            StartEncounter();
        }

        private static int ResolvePartySize()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsListening && nm.ConnectedClientsIds != null)
            {
                int n = nm.ConnectedClientsIds.Count;
                if (n > 0) return n;
            }
            return 1;
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

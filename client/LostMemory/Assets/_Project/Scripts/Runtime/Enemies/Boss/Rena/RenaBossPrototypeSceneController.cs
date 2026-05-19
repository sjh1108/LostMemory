using System.Collections;
using LostMemory.Stage;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Rena
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Rena/Rena Boss Prototype Scene Controller")]
    public sealed class RenaBossPrototypeSceneController : MonoBehaviour
    {
        [SerializeField] private BossIntroSequenceController introController;
        [SerializeField] private RenaBossEncounterController encounterController;
        [SerializeField] private Character[] players = System.Array.Empty<Character>();
        [SerializeField, Min(0f)] private float introDelaySeconds = 0.5f;
        [SerializeField] private string bossRoomId = "2F_Boss";
        [SerializeField] private string bossEntryPointId = "Rena";
        [SerializeField] private string bossSceneName = "Dungeon_2F_Boss";
        [SerializeField] private bool autoStartIntro = true;
        [SerializeField] private bool debugLogging;

        private Coroutine _introRoutine;
        private bool _started;

        private void Reset()
        {
            RefreshReferences();
        }

        private void Awake()
        {
            RefreshReferences();
        }

        private void OnEnable()
        {
            RefreshReferences();
            if (introController != null)
            {
                introController.IntroCompleted += HandleIntroCompleted;
            }

            if (autoStartIntro)
            {
                StartIntro();
            }
        }

        private void OnDisable()
        {
            if (introController != null)
            {
                introController.IntroCompleted -= HandleIntroCompleted;
            }

            if (_introRoutine != null)
            {
                StopCoroutine(_introRoutine);
                _introRoutine = null;
            }
        }

        public void StartIntro()
        {
            if (_started || _introRoutine != null)
            {
                return;
            }

            _introRoutine = StartCoroutine(StartIntroRoutine());
        }

        private IEnumerator StartIntroRoutine()
        {
            if (introDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(introDelaySeconds);
            }
            else
            {
                yield return null;
            }

            RefreshReferences();
            if (introController == null)
            {
                Debug.LogWarning("[RenaBossPrototypeScene] BossIntroSequenceController is missing.", this);
                _introRoutine = null;
                yield break;
            }

            Character[] resolvedPlayers = ResolvePlayers();
            BossRoomEntryTransitionRequest request = new BossRoomEntryTransitionRequest(
                null,
                resolvedPlayers.Length > 0 ? resolvedPlayers[0] : null,
                bossRoomId,
                bossEntryPointId,
                bossSceneName,
                resolvedPlayers.Length,
                resolvedPlayers.Length);
            BossRoomTransitionCompletedContext context = new BossRoomTransitionCompletedContext(
                request,
                null,
                resolvedPlayers);

            _started = true;
            _introRoutine = null;
            Log("Starting Rena intro.");
            introController.BeginIntro(context);
        }

        private void HandleIntroCompleted(BossRoomTransitionCompletedContext _)
        {
            RefreshReferences();
            encounterController?.BeginEncounter();
        }

        private void RefreshReferences()
        {
            if (introController == null)
            {
                introController = FindFirstObjectByType<BossIntroSequenceController>();
            }

            if (encounterController == null)
            {
                encounterController = FindFirstObjectByType<RenaBossEncounterController>();
            }

            if (players == null || players.Length == 0)
            {
                players = FindObjectsByType<Character>(FindObjectsSortMode.None);
            }
        }

        private Character[] ResolvePlayers()
        {
            RefreshReferences();
            if (players == null || players.Length == 0)
            {
                return System.Array.Empty<Character>();
            }

            System.Collections.Generic.List<Character> validPlayers = new System.Collections.Generic.List<Character>();
            for (int i = 0; i < players.Length; i++)
            {
                Character player = players[i];
                if (player != null &&
                    player.gameObject.activeInHierarchy &&
                    player.CharacterType == Character.CharacterTypes.Player)
                {
                    validPlayers.Add(player);
                }
            }

            return validPlayers.ToArray();
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[RenaBossPrototypeScene] " + message, this);
            }
        }
    }
}

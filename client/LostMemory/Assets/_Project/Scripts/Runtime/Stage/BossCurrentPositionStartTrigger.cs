using System.Collections.Generic;
using LostMemory.Audio;
using LostMemory.Networking.Player;
using LostMemory.Rewards;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Lost Memory/Stage/Boss Current Position Start Trigger")]
    public sealed class BossCurrentPositionStartTrigger : MonoBehaviour
    {
        [SerializeField] private BossIntroSequenceController introSequenceController;
        [SerializeField] private GameObject encounterControllerOwner;
        [SerializeField] private bool includeAllScenePlayers = true;
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private string acceptedPlayerId = string.Empty;
        [SerializeField] private bool gatherPlayersBeforeIntro = true;
        [SerializeField] private bool freezePlayersAfterGather = true;
        [SerializeField] private BossRoomEntryPoint bossEntryPoint;
        [SerializeField] private bool autoResolveBossEntryPoint = true;
        [SerializeField] private Transform gatherPoint;
        [SerializeField] private Vector2 gatheredPlayerSpacing = new Vector2(0.75f, 0f);
        [SerializeField] private bool alignFacingDirection;
        [SerializeField] private Character.FacingDirections gatheredFacingDirection = Character.FacingDirections.North;
        [SerializeField] private string bossRoomId = "Boss";
        [SerializeField] private string bossEntryPointId = string.Empty;
        [SerializeField] private string bossSceneName = string.Empty;

        [Header("Boss Start BGM (optional)")]
        [SerializeField] private AudioClip bossStartBgmClip;
        [SerializeField] private int bossStartBgmId = StageBgmPlayer.Stage2BossBgmId;
        [SerializeField, Range(0f, 2f)] private float bossStartBgmVolume = 1f;

        [SerializeField] private bool debugLogging;

        private Collider2D _trigger;
        private bool _started;
        private bool _waitingForIntroComplete;
        private BossIntroSequenceController _subscribedIntroController;

        private void Reset()
        {
            RefreshReferences();
            ConfigureTrigger();
        }

        private void Awake()
        {
            RefreshReferences();
            ConfigureTrigger();
        }

        private void OnEnable()
        {
            RefreshReferences();
            SubscribeIntroCompleted();
        }

        private void OnDisable()
        {
            UnsubscribeIntroCompleted();
            _waitingForIntroComplete = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            bool isPlayer = ResolvePlayerCharacter(other) != null;
            Debug.Log($"[BossEntry] OnTriggerEnter2D by '{other.name}' isPlayer={isPlayer} started={_started}", this);

            if (triggerOnce && _started)
            {
                return;
            }

            Character initiator = ResolvePlayerCharacter(other);
            if (initiator == null)
            {
                return;
            }

            // 자기 측 reward panel 떠있으면 trigger 발화 차단 — 보상 선택 후 다시 영역 들어와야 함.
            // ClientRpc 텔레포트 수신 측 wait 가 1차 안전망, 본 trigger 측 차단은 2차 — 자기가 발화 자체 안 함.
            RewardPanelView rewardPanel = FindFirstObjectByType<RewardPanelView>(FindObjectsInactive.Exclude);
            if (rewardPanel != null && rewardPanel.gameObject.activeInHierarchy)
            {
                Log("Reward panel 활성 상태 — boss trigger 발화 보류.");
                return;
            }

            // 멀티 환경 그룹 진입 — owner-auth NetworkTransform 특성상 host 가 게스트 transform 못 옮김.
            // 게스트가 먼저 진입해도 모두 같이 이동하도록:
            //   1. 게스트 측 OnTriggerEnter2D 발화 시 → owner player 의 ServerRpc 로 host 위임
            //   2. host 측에서 BeginEncounter + 모든 player ClientRpc 텔레포트
            // 우리 P0 fix 의 IsServer 가드는 ServerRpc 위임으로 대체 — 게스트가 영영 trigger 못 밟는 dead-lock 제거.
            NetworkManager nm = NetworkManager.Singleton;
            bool networkActive = nm != null && nm.IsListening;

            if (networkActive && !nm.IsServer)
            {
                // 게스트: 자기 owner player 의 PlayerMovementSync 통해 ServerRpc 발송.
                // host 가 자기 측에서 StartBossAtCurrentPositions 호출 → 모든 player 그룹 텔레포트.
                PlayerMovementSync sync = other.GetComponentInParent<PlayerMovementSync>();
                if (sync == null || !sync.IsOwner)
                {
                    // 다른 player 의 collider 또는 spawn 직후 NetworkObject 미완성 — 무시.
                    return;
                }
                sync.RequestBossStartServerRpc();
                return;
            }

            // host 또는 솔로 환경 — 기존 흐름.
            StartBossAtCurrentPositions(initiator);
        }

        public void StartBossAtCurrentPositions(Character initiator)
        {
            if (triggerOnce && _started)
            {
                return;
            }

            RefreshReferences();

            Character[] players = ResolvePlayers(initiator);
            if (players.Length == 0)
            {
                Debug.LogWarning("[BossCurrentPositionStartTrigger] No valid player was found.", this);
                return;
            }

            if (gatherPlayersBeforeIntro)
            {
                GatherPlayers(players, freezePlayersAfterGather && introSequenceController != null);
            }

            BossRoomEntryTransitionRequest request = new BossRoomEntryTransitionRequest(
                null,
                initiator != null ? initiator : players[0],
                bossRoomId,
                bossEntryPointId,
                bossSceneName,
                players.Length,
                players.Length);

            BossRoomTransitionCompletedContext context = new BossRoomTransitionCompletedContext(
                request,
                bossEntryPoint,
                players);

            _started = true;
            if (triggerOnce && _trigger != null)
            {
                _trigger.enabled = false;
            }

            PlayBossStartBgm();

            if (introSequenceController != null)
            {
                _waitingForIntroComplete = true;
                SubscribeIntroCompleted();
                Log("Starting boss intro at current player positions.");
                introSequenceController.BeginIntro(context);
                return;
            }

            Log("Starting boss encounter without intro.");
            BeginEncounter();
        }

        private void HandleIntroCompleted(BossRoomTransitionCompletedContext context)
        {
            if (!_waitingForIntroComplete)
            {
                return;
            }

            _waitingForIntroComplete = false;
            BeginEncounter();
        }

        private void BeginEncounter()
        {
            IBossEncounterController encounterController = null;
            if (encounterControllerOwner != null)
            {
                encounterController = encounterControllerOwner.GetComponent<IBossEncounterController>();
            }

            if (encounterController == null)
            {
                Debug.LogWarning("[BossCurrentPositionStartTrigger] Encounter controller is missing.", this);
                return;
            }

            encounterController.BeginEncounter();
            Log("Boss encounter started.");
        }

        private void PlayBossStartBgm()
        {
            if (bossStartBgmClip == null)
            {
                return;
            }

            StageBgmPlayer.PlayLoop(
                bossStartBgmClip,
                bossStartBgmId,
                bossStartBgmVolume,
                this,
                bossRoomId);
        }

        private void GatherPlayers(IReadOnlyList<Character> players, bool freezeAfterGather)
        {
            if (players == null || players.Count == 0)
            {
                return;
            }

            NetworkManager nm = NetworkManager.Singleton;
            bool networkActive = nm != null && nm.IsListening;

            for (int i = 0; i < players.Count; i++)
            {
                Character player = players[i];
                if (player == null)
                {
                    continue;
                }

                Vector3 targetPosition = GetGatherPosition(i, players.Count);

                if (networkActive)
                {
                    // 멀티: owner-auth NetworkTransform 이므로 owner client 측에서 자기 player 위치 변경해야 sync 됨.
                    // host 가 각 player 의 PlayerMovementSync 인스턴스에 ClientRpc 발송 → 각 owner client 가 자기측 텔레포트.
                    PlayerMovementSync sync = player.GetComponentInParent<PlayerMovementSync>();
                    if (sync != null && sync.IsSpawned)
                    {
                        sync.TeleportPlayerClientRpc(targetPosition, freezeAfterGather);
                        continue;
                    }
                    // PlayerMovementSync 미부착 또는 미spawn — fallback 으로 직접 처리.
                }

                // 솔로 또는 fallback — 기존 직접 텔레포트.
                TeleportCharacter(player, targetPosition);
                if (freezeAfterGather)
                {
                    player.Freeze();
                }
            }

            Debug.Log($"[BossEntry] Gathered {players.Count} player(s), freezeAfter={freezeAfterGather}, networkActive={networkActive} — TeleportPlayerClientRpc broadcast 완료.", this);
            Log("Gathered " + players.Count + " player(s) before boss intro.");
        }

        private Vector3 GetGatherPosition(int participantIndex, int participantCount)
        {
            if (bossEntryPoint != null)
            {
                Transform arrivalPoint = bossEntryPoint.GetArrivalPoint(participantIndex);
                return arrivalPoint != null ? arrivalPoint.position : bossEntryPoint.transform.position;
            }

            Vector3 basePosition = gatherPoint != null ? gatherPoint.position : transform.position;
            if (participantCount <= 1 || gatheredPlayerSpacing == Vector2.zero)
            {
                return basePosition;
            }

            float centeredIndex = participantIndex - (participantCount - 1) * 0.5f;
            Vector3 offset = new Vector3(
                gatheredPlayerSpacing.x * centeredIndex,
                gatheredPlayerSpacing.y * centeredIndex,
                0f);
            return basePosition + offset;
        }

        private void TeleportCharacter(Character character, Vector3 targetPosition)
        {
            if (character == null)
            {
                return;
            }

            TopDownController controller = character.GetComponent<TopDownController>();
            if (controller != null)
            {
                controller.SetMovement(Vector3.zero);
                controller.MovePosition(targetPosition, true);
            }
            else
            {
                character.transform.position = targetPosition;
            }

            if (!alignFacingDirection)
            {
                return;
            }

            CharacterOrientation2D orientation2D = character.FindAbility<CharacterOrientation2D>();
            if (orientation2D != null)
            {
                Character.FacingDirections facingDirection = ResolveGatheredFacingDirection();
                orientation2D.InitialFacingDirection = facingDirection;
                orientation2D.Face(facingDirection);
                return;
            }

            CharacterOrientation3D orientation3D = character.FindAbility<CharacterOrientation3D>();
            if (orientation3D != null)
            {
                orientation3D.Face(ResolveGatheredFacingDirection());
            }
        }

        private Character.FacingDirections ResolveGatheredFacingDirection()
        {
            return bossEntryPoint != null
                ? bossEntryPoint.FacingDirection
                : gatheredFacingDirection;
        }

        private void RefreshReferences()
        {
            if (_trigger == null)
            {
                _trigger = GetComponent<Collider2D>();
            }

            if (introSequenceController == null)
            {
                introSequenceController = FindFirstObjectByType<BossIntroSequenceController>();
            }

            if (encounterControllerOwner == null && introSequenceController != null)
            {
                encounterControllerOwner = introSequenceController.gameObject;
            }

            if (bossEntryPoint == null && autoResolveBossEntryPoint)
            {
                bossEntryPoint = ResolveBossEntryPoint();
            }
        }

        private BossRoomEntryPoint ResolveBossEntryPoint()
        {
            BossRoomEntryPoint[] entryPoints = FindObjectsByType<BossRoomEntryPoint>(FindObjectsSortMode.None);
            if (entryPoints == null || entryPoints.Length == 0)
            {
                return null;
            }

            BossRoomEntryPoint fallback = null;
            for (int i = 0; i < entryPoints.Length; i++)
            {
                BossRoomEntryPoint entryPoint = entryPoints[i];
                if (entryPoint == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = entryPoint;
                }

                if (!string.IsNullOrWhiteSpace(bossEntryPointId) && entryPoint.Matches(bossEntryPointId))
                {
                    return entryPoint;
                }
            }

            return fallback;
        }

        private void ConfigureTrigger()
        {
            if (_trigger != null)
            {
                _trigger.isTrigger = true;
            }
        }

        private void SubscribeIntroCompleted()
        {
            if (_subscribedIntroController == introSequenceController)
            {
                return;
            }

            UnsubscribeIntroCompleted();

            if (introSequenceController == null)
            {
                return;
            }

            _subscribedIntroController = introSequenceController;
            _subscribedIntroController.IntroCompleted += HandleIntroCompleted;
        }

        private void UnsubscribeIntroCompleted()
        {
            if (_subscribedIntroController == null)
            {
                return;
            }

            _subscribedIntroController.IntroCompleted -= HandleIntroCompleted;
            _subscribedIntroController = null;
        }

        private Character ResolvePlayerCharacter(Collider2D other)
        {
            if (other == null)
            {
                return null;
            }

            Character character = other.GetComponentInParent<Character>();
            if (!IsValidPlayer(character))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(acceptedPlayerId) &&
                !string.Equals(character.PlayerID, acceptedPlayerId, System.StringComparison.Ordinal))
            {
                return null;
            }

            return character;
        }

        private Character[] ResolvePlayers(Character initiator)
        {
            List<Character> players = new List<Character>();

            if (includeAllScenePlayers)
            {
                Character[] sceneCharacters = FindObjectsByType<Character>(FindObjectsSortMode.None);
                for (int i = 0; i < sceneCharacters.Length; i++)
                {
                    Character character = sceneCharacters[i];
                    if (IsValidPlayer(character) && !players.Contains(character))
                    {
                        players.Add(character);
                    }
                }

                players.Sort(CompareCharacters);
            }

            if (initiator != null && IsValidPlayer(initiator) && !players.Contains(initiator))
            {
                players.Insert(0, initiator);
            }

            return players.ToArray();
        }

        private static bool IsValidPlayer(Character character)
        {
            if (character == null || !character.gameObject.activeInHierarchy)
            {
                return false;
            }

            bool isPlayerType = character.CharacterType == Character.CharacterTypes.Player;
            NetworkObject networkObject = character.GetComponentInParent<NetworkObject>();
            bool isNetworkPlayer = networkObject != null && networkObject.IsPlayerObject;
            if (!isPlayerType && !isNetworkPlayer)
            {
                return false;
            }

            if (character.ConditionState != null &&
                character.ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead)
            {
                return false;
            }

            Health health = character.CharacterHealth;
            return health == null || !health.Initialized || health.CurrentHealth > 0f;
        }

        private static int CompareCharacters(Character left, Character right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            string leftPlayerId = left.PlayerID ?? string.Empty;
            string rightPlayerId = right.PlayerID ?? string.Empty;
            int result = string.CompareOrdinal(leftPlayerId, rightPlayerId);
            if (result != 0)
            {
                return result;
            }

            return left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BossCurrentPositionStartTrigger] " + message, this);
            }
        }
    }
}

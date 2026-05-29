using System;
using System.Collections;
using LostMemory.Audio;
using LostMemory.Combat;
using LostMemory.Networking.Common;
using LostMemory.Networking.Player;
using LostMemory.Relics;
using LostMemory.Rewards;
using LostMemory.Stage.Data;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Stage
{
    /// <summary>
    /// 방 단위 진입 권위. layout prefab 루트에 붙는다.
    ///
    /// 진입 흐름:
    ///   BeginRoomEntry(initiator)
    ///     → IsAuthority 가드 → 1회 보장
    ///     → progress.MarkVisited()
    ///     → ApplyInitContext (player 정렬 / facing / BGM stub log / 출구 잠금 시그널)
    ///     → BeginEncounter (clear tracker 등록 + spawner 시작)
    ///     → 이벤트 발행 (RoomEntered / ExitDoorsLockRequested / RoomCombatStarted)
    ///
    /// CL-035 가 IRoomClearConditionTracker 본체를 채우고,
    /// CL-036 이 ExitDoorsLockRequested 와 RoomCombatStarted 를 받아 문 흐름을 잡는다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Room Entry Runtime Controller")]
    public sealed class RoomEntryRuntimeController : NetworkBehaviour
    {
        /// <summary>
        /// 출구 잠금 상태. server write / everyone read.
        /// false = 자유 통과 (진입 전 default), true = 막힘 (진입 후~클리어 전).
        /// 변화 시 ExitLockedStateChanged 이벤트 발행 → 자식 RoomExitWall 들이 자기 collider/renderer 토글.
        /// GameObject.SetActive 를 토글하지 않으므로 NetworkObject 부담 / 초기 race 없음.
        /// </summary>
        public readonly NetworkVariable<bool> IsLocked = new NetworkVariable<bool>(
            value: false,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        /// <summary>RoomExitWall 들이 구독. NetworkSpawn 시 현재 값으로 1회 발행 (late join 대응).</summary>
        public event Action<bool> ExitLockedStateChanged;


        [Header("Data")]
        [SerializeField] private RoomData roomData;
        [SerializeField] private EnemyCatalog enemyCatalog;

        [Header("Refs (자식 또는 같은 GameObject)")]
        [SerializeField] private RoomEncounterAnchor encounterAnchor;
        [SerializeField] private EnemyEncounterSpawner encounterSpawner;
        [SerializeField] private RoomEntryAnchor[] entryAnchors = Array.Empty<RoomEntryAnchor>();
        // CL-036: 진입 시 활성화 (player 못 나감), 클리어 시 비활성화 (다음 방 통과 가능).
        [SerializeField] private RoomExitWall[] exitWalls = Array.Empty<RoomExitWall>();

        [Header("Optional Refs")]
        // CL-035: tracker 가 OnRoomCleared 발행 시 자동으로 TryMarkRoomCompleted(roomId) 호출.
        // null 허용 — 보스 의존성 없는 단독 검증 씬에서는 비워둠.
        [SerializeField] private BossRoomEntryTracker bossTracker;

        [Header("Reward Integration (CL-110)")]
        [Tooltip("true 면 클리어 시 자동으로 출구 벽 비활성화 (기존 동작). RewardController 가 관리하는 Combat 방은 false 로 두고 RewardController 가 OpenExits() 호출.")]
        [SerializeField] private bool autoOpenExitsOnCleared = true;

        [Header("Room Entry BGM (optional)")]
        [Tooltip("방 진입 시 재생할 BGM. 같은 클립이 이미 같은 ID 로 재생 중이면 재시작 없이 유지 (StageBgmPlayer 가 처리).")]
        [SerializeField] private AudioClip roomEntryBgmClip;
        [SerializeField] private int roomEntryBgmId = StageBgmPlayer.Stage2DungeonBgmId;
        [SerializeField, Range(0f, 2f)] private float roomEntryBgmVolume = 1f;

        public event Action<RoomEnteredPayload> RoomEntered;
        public event Action<RoomCombatStartedPayload> RoomCombatStarted;
        public event Action<EnemySpawnedPayload> EnemySpawned;
        public event Action<WaveSpawnedPayload> WaveSpawned;
        public event Action<ExitDoorsLockRequestPayload> ExitDoorsLockRequested;
        // CL-035: tracker 가 모든 wave + 모든 적 사망을 판정하면 1회 발행. 호스트 측에서만 발화.
        // 호스트 단독 상태 변경 (보스 트래커 등) 구독자가 대상.
        public event Action<RoomClearedPayload> RoomCleared;

        /// <summary>
        /// A-3: 4인 멀티에서 모두에게 발화되는 클리어 브로드캐스트. 솔로/멀티 양쪽 안전.
        /// 보상 UI / 시각 변경 등 각 클라에서 독립 실행되어야 하는 구독자가 대상.
        /// </summary>
        public event Action<RoomClearedPayload> RoomClearedBroadcast;

        private readonly StageRoomProgress progress = new StageRoomProgress();
        private IRoomClearConditionTracker clearTracker;
        private bool entryConsumed;
        private bool roomCleared;

        // Phase B-1: HostAuthority.IsHost 로 통일. 싱글 실행 시 NetworkManager 비활성 → true 반환.
        // 멀티 실행 시 호스트만 BeginRoomEntry / NotifyCustomRoomCleared 권위 보유.
        public bool IsAuthority => HostAuthority.IsHost;

        public RoomData RoomData => roomData;
        public StageRoomProgress Progress => progress;

        /// <summary>CL-110: RewardController 가 RoomCleared payload 와 controller 매칭에 사용.</summary>
        public string RoomId => roomData != null ? roomData.RoomId : null;

        private void Awake()
        {
            if (roomData != null)
            {
                progress.Configure(
                    roomData.RoomId,
                    roomData.RoomType,
                    roomData.BossEntryRequirementMode,
                    roomData);
            }

            if (encounterAnchor == null)
            {
                encounterAnchor = GetComponentInChildren<RoomEncounterAnchor>(includeInactive: true);
            }
            if (encounterSpawner == null)
            {
                encounterSpawner = GetComponentInChildren<EnemyEncounterSpawner>(includeInactive: true);
            }
            if (entryAnchors == null || entryAnchors.Length == 0)
            {
                entryAnchors = GetComponentsInChildren<RoomEntryAnchor>(includeInactive: true);
            }
            if (exitWalls == null || exitWalls.Length == 0)
            {
                exitWalls = GetComponentsInChildren<RoomExitWall>(includeInactive: true);
            }

            // CL-323 → 변경: SetExitWallsActive(false) 호출 제거.
            // RoomExitWall 들이 자기 Awake 에서 default false 상태(collider/renderer 둘 다 disabled)로 시작함.
            // GameObject.SetActive 토글 안 함 → NetworkBehaviour 활성화 보장.
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            IsLocked.OnValueChanged += HandleLockedChanged;
            // late join 대응: 현재 값으로 즉시 1회 wall 동기화.
            ExitLockedStateChanged?.Invoke(IsLocked.Value);
        }

        public override void OnNetworkDespawn()
        {
            IsLocked.OnValueChanged -= HandleLockedChanged;
            base.OnNetworkDespawn();
        }

        private void HandleLockedChanged(bool previous, bool current)
        {
            ExitLockedStateChanged?.Invoke(current);
        }

        private void Reset()
        {
            encounterAnchor = GetComponentInChildren<RoomEncounterAnchor>(includeInactive: true);
            encounterSpawner = GetComponentInChildren<EnemyEncounterSpawner>(includeInactive: true);
            entryAnchors = GetComponentsInChildren<RoomEntryAnchor>(includeInactive: true);
            exitWalls = GetComponentsInChildren<RoomExitWall>(includeInactive: true);
        }

        public void BeginRoomEntry(Character initiator)
        {
            if (!IsAuthority)
            {
                return;
            }
            if (entryConsumed)
            {
                return;
            }
            if (roomData == null)
            {
                Debug.LogWarning($"[RoomEntryRuntimeController] roomData is null on '{name}'. Skip entry.", this);
                return;
            }

            entryConsumed = true;
            roomCleared = false;
            progress.MarkVisited();

            ApplyInitContext(initiator);
            BeginEncounter();

            RoomEntered?.Invoke(new RoomEnteredPayload(roomData.RoomId, initiator));
        }

        public void NotifyCustomRoomCleared()
        {
            if (!IsAuthority)
            {
                return;
            }
            if (roomData == null)
            {
                Debug.LogWarning($"[RoomEntryRuntimeController] roomData is null on '{name}'. Cannot clear room.", this);
                return;
            }

            FireRoomCleared(new RoomClearedPayload(roomData.RoomId, roomData));
        }

        // CL-112: DA spawn 후 DungeonRunBootstrap 이 시퀀스별 RoomData 를 주입.
        // Awake 의 progress.Configure 를 *덮어쓰기* 위해 같은 호출을 재실행.
        public void SetRoomData(RoomData data)
        {
            if (entryConsumed)
            {
                Debug.LogWarning($"[RoomEntryRuntimeController] SetRoomData called after entry on '{name}'. Ignored.", this);
                return;
            }
            roomData = data;
            if (data != null)
            {
                progress.Configure(
                    data.RoomId,
                    data.RoomType,
                    data.BossEntryRequirementMode,
                    data);
            }
        }

        private void ApplyInitContext(Character initiator)
        {
            RoomInitContextSpec init = roomData.InitContext;
            if (init == null)
            {
                // [DiagRoomSpawn] InitContext 가 asset 에 미설정 → 텔레포트 자체 발동 안 됨.
                Debug.LogWarning($"[DiagRoomSpawn] Room '{roomData.RoomId}' InitContext is null → ApplyInitContext SKIP (텔레포트 불가). RoomData asset 의 InitContext 필드 확인 필요.", this);
                return;
            }

            // 모든 방에서 anchor 적용 + 전 플레이어 텔레포트.
            // 한 명이 진입 트리거 밟으면 호스트가 ClientRpc 로 모든 클라이언트의 로컬 player 를 anchor 로 이동.
            // → 코옵에서 팀원 분리 / 벽 잠금 후 입장 불가 문제 원천 차단.
            if (initiator != null && !string.IsNullOrEmpty(init.PlayerSpawnAnchorTag))
            {
                RoomEntryAnchor anchor = ResolveEntryAnchor(init.PlayerSpawnAnchorTag);
                if (anchor == null)
                {
                    // [DiagRoomSpawn] tag 미스매치 — 플레이어가 이전 위치 (RouteNodeSpawnPoint) 에 그대로 남음.
                    // available anchors 와 requested tag 비교해 사용자가 asset / scene 어느 쪽 수정할지 결정 가능.
                    string availableTags = "[]";
                    if (entryAnchors != null && entryAnchors.Length > 0)
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.Append('[');
                        for (int i = 0; i < entryAnchors.Length; i++)
                        {
                            if (entryAnchors[i] == null) continue;
                            if (sb.Length > 1) sb.Append(", ");
                            sb.Append("'").Append(entryAnchors[i].AnchorTag).Append("'");
                        }
                        sb.Append(']');
                        availableTags = sb.ToString();
                    }
                    Debug.LogWarning(
                        $"[DiagRoomSpawn] Room '{roomData.RoomId}' anchor NOT FOUND. requestedTag='{init.PlayerSpawnAnchorTag}' availableAnchors={availableTags} entryAnchorsCount={(entryAnchors != null ? entryAnchors.Length : 0)} " +
                        $"→ 텔레포트 SKIP, 플레이어가 이전 위치 (RouteNodeSpawnPoint) 에 그대로 남음. " +
                        $"픽스: RoomData asset 의 PlayerSpawnAnchorTag 를 availableAnchors 중 하나로 수정하거나, layout prefab 의 RoomEntryAnchor.anchorTag 를 RoomData 값으로 수정.", this);
                }
                else
                {
                    Debug.Log($"[DiagRoomSpawn] Room '{roomData.RoomId}' anchor='{init.PlayerSpawnAnchorTag}' resolved at {anchor.transform.position} → teleport.", this);
                    TeleportAllToAnchor(initiator, anchor.transform.position, init.Facing);
                }
            }
            else if (initiator == null)
            {
                Debug.LogWarning($"[DiagRoomSpawn] Room '{roomData.RoomId}' initiator is null → 텔레포트 SKIP.", this);
            }
            else
            {
                // [DiagRoomSpawn] tag 가 빈 문자열 → asset 측 픽스 필요.
                Debug.LogWarning(
                    $"[DiagRoomSpawn] Room '{roomData.RoomId}' init.PlayerSpawnAnchorTag is empty → 텔레포트 SKIP, 플레이어 위치 유지. " +
                    $"픽스: RoomData asset 의 InitContext.PlayerSpawnAnchorTag 에 값 입력 (1R/2R/3R 의 asset 값 복사 추천).", this);
            }

            if (init.LockExitDoors)
            {
                SetExitWallsActive(true);
                ExitDoorsLockRequested?.Invoke(new ExitDoorsLockRequestPayload(roomData.RoomId));
            }

            if (!string.IsNullOrEmpty(init.BgmCueId))
            {
                Debug.Log($"[RoomEntryRuntimeController] BGM stub: roomId='{roomData.RoomId}' cue='{init.BgmCueId}'", this);
            }

            PlayRoomEntryBgm();
        }

        private void PlayRoomEntryBgm()
        {
            if (roomEntryBgmClip == null)
            {
                return;
            }

            StageBgmPlayer.PlayLoop(
                roomEntryBgmClip,
                roomEntryBgmId,
                roomEntryBgmVolume,
                this,
                roomData != null ? roomData.RoomId : "RoomEntry");
        }

        private RoomEntryAnchor ResolveEntryAnchor(string tag)
        {
            for (int i = 0; i < entryAnchors.Length; i++)
            {
                RoomEntryAnchor anchor = entryAnchors[i];
                if (anchor != null && anchor.Matches(tag))
                {
                    return anchor;
                }
            }
            return null;
        }

        // 코옵: 호스트가 진입 트리거 감지 → 모든 클라이언트 (호스트 포함) 의 로컬 player 를 anchor 로 이동.
        // 싱글플레이: NetworkManager 미동작 → initiator 직접 이동.
        // 다중 플레이어 겹침 방지: ClientRpc 받는 각 클라이언트가 자기 random offset 적용 (sync 불필요).
        private void TeleportAllToAnchor(Character initiator, Vector3 position, Vector2 facing)
        {
            if (IsSpawned)
            {
                // 멀티: ClientRpc 브로드캐스트. 호스트도 받아서 자기 캐릭터 이동.
                TeleportLocalPlayerClientRpc(position, facing);
            }
            else
            {
                // 싱글: initiator 가 곧 로컬 player.
                if (initiator != null)
                {
                    AlignCharacterTo(initiator, position, facing);
                }
            }
        }

        [ClientRpc]
        private void TeleportLocalPlayerClientRpc(Vector3 position, Vector2 facing)
        {
            // 내 화면에 RewardPanel 열려있으면 카드 선택 끝날 때까지 텔레포트 대기.
            // 호스트는 자기 보상 보는 중엔 timeScale=0 이라 진입 트리거 못 밟으므로 자기 화면엔 거의 항상 닫혀있음.
            // 게스트가 늦게 고르는 중일 때를 위한 안전장치.
            RewardPanelView rewardPanel = UnityEngine.Object.FindFirstObjectByType<RewardPanelView>();
            if (rewardPanel != null && rewardPanel.gameObject.activeSelf)
            {
                StartCoroutine(WaitForRewardThenTeleport(rewardPanel, position, facing));
                return;
            }

            DoLocalPlayerTeleport(position, facing);
        }

        private IEnumerator WaitForRewardThenTeleport(RewardPanelView panel, Vector3 position, Vector2 facing)
        {
            bool selected = false;
            Action<RelicData> handler = _ => selected = true;
            panel.RewardSelected += handler;

            try
            {
                // timeScale=0 에서도 frame 은 진행되므로 yield return null 안전.
                while (!selected && panel != null && panel.gameObject.activeSelf)
                {
                    yield return null;
                }
            }
            finally
            {
                if (panel != null) panel.RewardSelected -= handler;
            }

            DoLocalPlayerTeleport(position, facing);
        }

        private static void DoLocalPlayerTeleport(Vector3 position, Vector2 facing)
        {
            // 프로젝트 표준: LocalPlayerResolver.LocalCharacter 사용.
            Character localPlayer = LocalPlayerResolver.LocalCharacter;
            if (localPlayer == null)
            {
                return;
            }

            // 다중 플레이어 anchor 겹침 방지: 작은 random offset (각 클라 독립).
            Vector2 offset = UnityEngine.Random.insideUnitCircle * 0.4f;
            Vector3 spawnPos = position + new Vector3(offset.x, offset.y, 0f);
            AlignCharacterTo(localPlayer, spawnPos, facing);
        }

        // BossRoomLocalTransitionDriver.TeleportCharacter / AlignFacingDirection 의 알고리즘을 본 컴포넌트 결로 옮겼다.
        // (보스 driver 직접 호출 X — 컴포넌트는 보스 전용)
        private static void AlignCharacterTo(Character character, Vector3 position, Vector2 facing)
        {
            TopDownController controller = character.GetComponent<TopDownController>();
            if (controller != null)
            {
                controller.SetMovement(Vector3.zero);
                controller.MovePosition(position, true);
            }
            else
            {
                character.transform.position = position;
            }

            if (facing.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Character.FacingDirections direction = ResolveFacing(facing);
            CharacterOrientation2D orientation2D = character.FindAbility<CharacterOrientation2D>();
            if (orientation2D != null)
            {
                orientation2D.InitialFacingDirection = direction;
                orientation2D.Face(direction);
                return;
            }

            CharacterOrientation3D orientation3D = character.FindAbility<CharacterOrientation3D>();
            if (orientation3D != null)
            {
                orientation3D.Face(direction);
            }
        }

        private static Character.FacingDirections ResolveFacing(Vector2 facing)
        {
            if (Mathf.Abs(facing.x) >= Mathf.Abs(facing.y))
            {
                return facing.x >= 0f ? Character.FacingDirections.East : Character.FacingDirections.West;
            }
            return facing.y >= 0f ? Character.FacingDirections.North : Character.FacingDirections.South;
        }

        private void BeginEncounter()
        {
            // [Fix Dungeon_1F_2R auto-advance]
            // AllEnemiesDefeatedTracker.Begin 은 RoomData.Encounter == null || Waves.Count == 0 이면
            // 즉시 OnRoomCleared 를 fire → 입장 직후 다음 방 진행으로 보임. 1F_2R 의 RoomData wiring 실수
            // (Encounter 미할당) 또는 인코딩 의도와 무관한 빈 wave 가 원인. 본 가드가 안전망 —
            // 데이터 측 픽스 (씬 Inspector 또는 mvpRoomDataSequence 에 valid Encounter 할당) 가 정공.
            if (roomData == null || roomData.Encounter == null || roomData.Encounter.Waves == null || roomData.Encounter.Waves.Count == 0)
            {
                Debug.LogWarning(
                    $"[RoomEntryRuntimeController] Room '{(roomData != null ? roomData.RoomId : "NULL")}' has no encounter waves — tracker skip (자동 클리어 차단). " +
                    $"Encounter null={(roomData == null || roomData.Encounter == null)} waveCount={(roomData != null && roomData.Encounter != null && roomData.Encounter.Waves != null ? roomData.Encounter.Waves.Count : 0)}",
                    this);
                return;
            }

            clearTracker = CreateTrackerFor(roomData.ClearCondition);
            clearTracker.OnNextWaveReady += HandleNextWaveReady;
            clearTracker.OnRoomCleared += HandleRoomCleared;
            clearTracker.Begin(roomData);

            if (encounterSpawner != null && encounterAnchor != null && enemyCatalog != null && roomData.Encounter != null)
            {
                encounterSpawner.Spawned += HandleSpawned;
                encounterSpawner.WaveCompleted += HandleWaveCompleted;
                encounterSpawner.Begin(roomData.RoomId, roomData.Encounter, encounterAnchor, enemyCatalog);
            }
            else
            {
                Debug.LogWarning(
                    $"[RoomEntryRuntimeController] encounter dependency missing. spawner={encounterSpawner!=null} anchor={encounterAnchor!=null} catalog={enemyCatalog!=null} encounter={roomData.Encounter!=null}",
                    this);
            }

            int waveCount = roomData.Encounter != null ? roomData.Encounter.Waves.Count : 0;
            RoomCombatStarted?.Invoke(new RoomCombatStartedPayload(roomData.RoomId, waveCount));
        }

        private static IRoomClearConditionTracker CreateTrackerFor(RoomClearConditionType clearCondition)
        {
            switch (clearCondition)
            {
                case RoomClearConditionType.AllEnemiesDefeated:
                    return new AllEnemiesDefeatedTracker();
                default:
                    // InteractionComplete / Custom 분기는 후속 CL 가 채울 때까지 stub.
                    return new StubRoomClearConditionTracker();
            }
        }

        private void HandleSpawned(EnemySpawnedPayload payload)
        {
            clearTracker?.RegisterEnemy(payload);
            EnemySpawned?.Invoke(payload);
        }

        private void HandleWaveCompleted(WaveSpawnedPayload payload)
        {
            clearTracker?.NotifyWaveSpawned(payload);
            WaveSpawned?.Invoke(payload);
        }

        private void HandleNextWaveReady(int nextWaveIndex)
        {
            if (encounterSpawner == null)
            {
                return;
            }
            encounterSpawner.SpawnNextWave();
        }

        private void HandleRoomCleared(RoomClearedPayload payload)
        {
            // [DiagPhaseF] reward panel 4인 sync 진단 — Log 1: host clearTracker 발화 path 추적.
            var nmLog1 = NetworkManager.Singleton;
            Debug.Log($"[RoomCtrl] HandleRoomCleared on '{name}' roomId={payload.RoomId} " +
                      $"spawnedEnemies={payload.SpawnedEnemyCount} alreadyCleared={roomCleared} " +
                      $"NM(host={nmLog1?.IsHost},srv={nmLog1?.IsServer},client={nmLog1?.IsClient},listening={nmLog1?.IsListening}," +
                      $"localId={nmLog1?.LocalClientId}) IsSpawned={IsSpawned} IsAuthority={IsAuthority}", this);

            // CL-110: 옵트인. RewardController 가 관리하는 방은 false 로 두고 카드 선택 후 OpenExits() 호출.
            if (autoOpenExitsOnCleared)
            {
                SetExitWallsActive(false);
            }
            FireRoomCleared(payload);
        }

        private void FireRoomCleared(RoomClearedPayload payload)
        {
            if (roomCleared)
            {
                // [DiagPhaseF] Log 2a — 중복 호출 가시화.
                Debug.LogWarning($"[RoomCtrl] FireRoomCleared SKIP (already cleared) roomId={payload.RoomId} on '{name}'", this);
                return;
            }

            roomCleared = true;
            Debug.Log($"[Controller] RoomCleared: roomId='{payload.RoomId}' on '{name}'.");

            RoomCleared?.Invoke(payload);

            if (bossTracker != null && !string.IsNullOrEmpty(payload.RoomId))
            {
                bossTracker.TryMarkRoomCompleted(payload.RoomId);
            }

            // A-3: 4인 멀티 — 보상 UI 등 클라 로컬 구독자들에게 브로드캐스트.
            NetworkManager nm = NetworkManager.Singleton;
            bool willBroadcast = IsSpawned && nm != null && nm.IsListening;
            // [DiagPhaseF] Log 2b — ClientRpc 분기 진단. willBroadcast=false 면 guest 가 못 받는다는 의미.
            Debug.Log($"[RoomCtrl] FireRoomCleared roomId={payload.RoomId} willBroadcastClientRpc={willBroadcast} " +
                      $"IsSpawned={IsSpawned} IsListening={nm?.IsListening} localId={nm?.LocalClientId} on '{name}'", this);
            if (willBroadcast)
            {
                RoomClearedBroadcastClientRpc(payload.SpawnedEnemyCount);
            }
            else
            {
                Debug.LogWarning($"[RoomCtrl] FireRoomCleared LOCAL-ONLY invoke (guest will NOT receive) roomId={payload.RoomId}", this);
                RoomClearedBroadcast?.Invoke(payload);
            }
        }

        [ClientRpc]
        private void RoomClearedBroadcastClientRpc(int spawnedEnemyCount)
        {
            // 각 클라가 자기 인스턴스의 roomData 로 payload 재구성 — ScriptableObject 가 NGO 직렬화 불가.
            string id = roomData != null ? roomData.RoomId : null;
            // [DiagPhaseF] Log 3 — ClientRpc 가 양 클라에 도달했고 guest 측 roomData wiring 상태 확인. NULL 이면 reward STOP 의 1차 원인.
            var nmLog3 = NetworkManager.Singleton;
            Debug.Log($"[RoomCtrl] RoomClearedBroadcastClientRpc RECEIVED on '{name}' " +
                      $"roomData={(roomData != null ? roomData.name : "NULL")} resolvedRoomId='{id}' " +
                      $"spawnedEnemies={spawnedEnemyCount} localId={nmLog3?.LocalClientId} isHost={nmLog3?.IsHost}", this);
            RoomClearedBroadcast?.Invoke(new RoomClearedPayload(id, roomData, spawnedEnemyCount));
        }

        /// <summary>CL-110: RewardController 가 보상 선택 후 호출. autoOpenExitsOnCleared=false 일 때 외부에서 문 열기 트리거.</summary>
        public void OpenExits()
        {
            SetExitWallsActive(false);
        }

        // CL-036 → 멀티 안전화: NetworkVariable<bool> IsLocked 토글로 변경.
        // 호스트만 write → 게스트는 OnValueChanged 로 자동 sync.
        // RoomExitWall 측이 ExitLockedStateChanged 구독해서 자기 collider/renderer 토글.
        private void SetExitWallsActive(bool active)
        {
            if (!IsAuthority)
            {
                return;
            }

            if (IsSpawned)
            {
                // 멀티: NetworkVariable 통해 sync. OnValueChanged → ExitLockedStateChanged.
                if (IsLocked.Value != active)
                {
                    IsLocked.Value = active;
                }
            }
            else
            {
                // 싱글 / NetworkManager 미동작: NetworkVariable 작동 안 함 → 직접 이벤트 발행.
                // RoomExitWall 들이 ExitLockedStateChanged 구독하므로 같은 결과.
                ExitLockedStateChanged?.Invoke(active);
            }
        }

        public override void OnDestroy()
        {
            if (encounterSpawner != null)
            {
                encounterSpawner.Spawned -= HandleSpawned;
                encounterSpawner.WaveCompleted -= HandleWaveCompleted;
            }
            if (clearTracker != null)
            {
                clearTracker.OnNextWaveReady -= HandleNextWaveReady;
                clearTracker.OnRoomCleared -= HandleRoomCleared;
            }
            base.OnDestroy();
        }
    }
}

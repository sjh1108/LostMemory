using System;
using LostMemory.Combat;
using LostMemory.Stage.Data;
using MoreMountains.TopDownEngine;
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
    public sealed class RoomEntryRuntimeController : MonoBehaviour
    {
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

        public event Action<RoomEnteredPayload> RoomEntered;
        public event Action<RoomCombatStartedPayload> RoomCombatStarted;
        public event Action<EnemySpawnedPayload> EnemySpawned;
        public event Action<WaveSpawnedPayload> WaveSpawned;
        public event Action<ExitDoorsLockRequestPayload> ExitDoorsLockRequested;
        // CL-035: tracker 가 모든 wave + 모든 적 사망을 판정하면 1회 발행.
        public event Action<RoomClearedPayload> RoomCleared;

        private readonly StageRoomProgress progress = new StageRoomProgress();
        private IRoomClearConditionTracker clearTracker;
        private bool entryConsumed;

        // 후속 네트워크 CL 이 한 줄만 바꾸면 호스트 권위 분기로 전환된다.
        public bool IsAuthority => true;

        public RoomData RoomData => roomData;
        public StageRoomProgress Progress => progress;

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

            // CL-323: 시작 시 *모든 ExitWall 비활성화* — 진입 전이라 막을 필요 X.
            // BeginRoomEntry 의 ApplyInitContext 가 lockExitDoors=true 일 때 활성화.
            // 디자이너 셋업 (prefab 의 active 상태) 무관하게 안전한 기본 상태 보장.
            SetExitWallsActive(false);
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
            progress.MarkVisited();

            ApplyInitContext(initiator);
            BeginEncounter();

            RoomEntered?.Invoke(new RoomEnteredPayload(roomData.RoomId, initiator));
        }

        private void ApplyInitContext(Character initiator)
        {
            RoomInitContextSpec init = roomData.InitContext;
            if (init == null)
            {
                return;
            }

            if (initiator != null && !string.IsNullOrEmpty(init.PlayerSpawnAnchorTag))
            {
                RoomEntryAnchor anchor = ResolveEntryAnchor(init.PlayerSpawnAnchorTag);
                if (anchor != null)
                {
                    AlignCharacterTo(initiator, anchor.transform.position, init.Facing);
                }
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
            Debug.Log($"[Controller] HandleRoomCleared: roomId='{payload.RoomId}' on '{name}'. Disabling exit walls.");
            SetExitWallsActive(false);
            RoomCleared?.Invoke(payload);

            if (bossTracker != null && !string.IsNullOrEmpty(payload.RoomId))
            {
                bossTracker.TryMarkRoomCompleted(payload.RoomId);
            }
        }

        // CL-036: 모든 출구 벽 일괄 토글. 출구별 다른 정책 (LockPolicy 등) 은 후속 CL.
        private void SetExitWallsActive(bool active)
        {
            if (exitWalls == null)
            {
                return;
            }
            for (int i = 0; i < exitWalls.Length; i++)
            {
                RoomExitWall wall = exitWalls[i];
                if (wall != null)
                {
                    wall.gameObject.SetActive(active);
                }
            }
        }

        private void OnDestroy()
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
        }
    }
}

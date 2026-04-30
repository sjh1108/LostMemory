using DungeonArchitect;
using LostMemory.Stage.Data;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    /// <summary>
    /// CL-323: DA Dungeon 의 build 흐름과 우리 RoomEntryRuntimeController 시스템을 연결.
    ///
    /// 책임:
    ///   1. 런 시작 시 *시드 결정* (무작위 또는 고정) 후 Dungeon.Build() 호출
    ///   2. DA 가 module 들을 spawn 한 직후 (OnSpawnedManagedObjects) 콜백 받음
    ///   3. (옵션) 시작 module 식별 + player 위치 정렬
    ///
    /// 본 컴포넌트는 *DA Dungeon 과 같은 GameObject* 또는 *그 자식 / 부모* 에 배치.
    /// `DungeonEventListener` 상속이라 DA 가 이벤트 자동 호출.
    ///
    /// 이후 흐름:
    ///   - 각 module 의 RoomEntryRuntimeController 는 *방 단위 자율*
    ///   - player 가 module 의 RoomEntryZone 에 닿으면 자동 BeginRoomEntry 발동
    ///   - 따라서 본 bootstrap 은 시드 + Build 만 책임지고, 첫 방 트리거는 RoomEntryZone 의 자동 진입에 위임 (1차)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Dungeon Run Bootstrap")]
    public sealed class DungeonRunBootstrap : DungeonEventListener
    {
        [Header("Refs")]
        [SerializeField] private Dungeon dungeon;
        [SerializeField] private Character player;

        [Header("Seed")]
        [SerializeField, Tooltip("true 면 런 시작 시 *무작위 시드*. false 면 fixedSeed 사용.")]
        private bool randomizeSeedOnStart = true;

        [SerializeField, Tooltip("randomizeSeedOnStart=false 일 때 사용할 고정 시드. 디자이너 검증 / 튜토리얼용.")]
        private int fixedSeed;

        [Header("Behavior")]
        [SerializeField, Tooltip("Awake 후 자동으로 Build 실행. false 면 외부에서 BuildRun() 명시 호출.")]
        private bool autoBuildOnStart = true;

        [SerializeField, Tooltip("Build 후 player 를 첫 module 의 EntryAnchor 위치로 정렬. 없으면 player 위치 변동 없음.")]
        private bool warpPlayerToFirstRoom = true;

        [Header("MVP Sequence (CL-112)")]
        [SerializeField, Tooltip("DA spawn 순서대로 매핑할 RoomData. [0]=첫 방 ~ [N-1]=마지막. 빈 배열이면 주입 skip (legacy 동작).")]
        private RoomData[] mvpRoomDataSequence = new RoomData[6];

        [Header("Pre-built Layout")]
        [SerializeField, Tooltip("true: 방이 씬에 직접 배치된 MVP 씬 (DA Build 사용 안 함). DungeonBuilt 즉시 발화 → RunManager / RewardController 가 FindObjectsOfType 으로 씬의 방 모두 구독.")]
        private bool usePrebuiltLayout = false;

        // 후속 네트워크 CL 이 한 줄만 바꾸면 호스트 권위 분기로 전환된다.
        public bool IsAuthority => true;

        /// <summary>
        /// DA Build 완료 + module spawn 끝난 직후 발행. CL-048 RunManager 가
        /// Initializing → InRun 전이 트리거로 구독. 본 bootstrap 의 자체 wiring 은 변경 없음.
        /// </summary>
        public event System.Action DungeonBuilt;

        private bool buildRequested;

        public Dungeon Dungeon => dungeon;
        public Character Player => player;

        private void Reset()
        {
            dungeon = GetComponent<Dungeon>();
        }

        private void Awake()
        {
            if (dungeon == null)
            {
                dungeon = GetComponent<Dungeon>();
            }
        }

        private void Start()
        {
            if (autoBuildOnStart)
            {
                BuildRun();
            }
        }

        /// <summary>
        /// 런 시작 — 시드 결정 + Dungeon.Build 호출. 외부에서도 호출 가능.
        /// 호스트 권위 가드 적용.
        /// </summary>
        public void BuildRun()
        {
            if (!IsAuthority)
            {
                return;
            }

            // 방이 씬에 직접 배치된 모드 — DA Build skip + DungeonBuilt 즉시 발화.
            // RunManager.HandleDungeonBuilt → RewardController.SubscribeAllRoomControllers 가
            // FindObjectsOfType<RoomEntryRuntimeController> 로 씬의 방 모두 구독.
            if (usePrebuiltLayout)
            {
                Debug.Log("[DungeonRunBootstrap] usePrebuiltLayout=true. Skipping DA Build; firing DungeonBuilt directly.");
                buildRequested = true;
                DungeonBuilt?.Invoke();
                return;
            }

            if (dungeon == null)
            {
                Debug.LogWarning("[DungeonRunBootstrap] Dungeon reference is null. Skip build.", this);
                return;
            }

            if (randomizeSeedOnStart)
            {
                dungeon.RandomizeSeed();
            }
            else
            {
                dungeon.SetSeed(fixedSeed);
            }

            buildRequested = true;
            dungeon.Build();
        }

        public override void OnSpawnedManagedObjects(Dungeon dungeon, GameObject[] spawnedManagedObjects, DungeonModel activeModel)
        {
            Debug.Log($"[DungeonRunBootstrap] DA spawned {spawnedManagedObjects.Length} managed objects (seed={dungeon.Config?.Seed})");

            // CL-112 spike: spawn 배열의 순서/위치를 5회 반복 비교하기 위한 임시 디버그.
            // 합격 후 (spawn array index 0..N-1 = sequence 순서가 deterministic 임을 확인) 본 블록 제거.
            for (int i = 0; i < spawnedManagedObjects.Length; i++)
            {
                GameObject m = spawnedManagedObjects[i];
                Debug.Log($"[Spike] [{i}] {(m != null ? m.name : "<null>")} pos={(m != null ? m.transform.position.ToString("F2") : "<null>")}");
            }

            // CL-112: spawn 순서대로 mvpRoomDataSequence 주입. RewardController.Subscribe 가
            // controller.RoomData 를 의존하지 않더라도 *주입 후 DungeonBuilt 발행* 으로 일관성 보장.
            InjectMvpRoomDataSequence(spawnedManagedObjects);

            // CL-048: RunManager 등 외부 구독자에게 DA build 완료 신호. RoomData 주입 *이후* 발행 (CL-112).
            DungeonBuilt?.Invoke();

            if (!warpPlayerToFirstRoom || player == null)
            {
                return;
            }

            // 1차 — *spawn 된 첫 module 중 RoomEntryRuntimeController 가 있는 것* 의 EntryAnchor 위치로 player 정렬.
            // 추후 RoomData.RoomType (Start/Combat/Boss) 또는 그래프 메타데이터 기반 정확한 식별로 보강 가능.
            for (int i = 0; i < spawnedManagedObjects.Length; i++)
            {
                GameObject moduleInstance = spawnedManagedObjects[i];
                if (moduleInstance == null)
                {
                    continue;
                }

                RoomEntryRuntimeController controller = moduleInstance.GetComponentInChildren<RoomEntryRuntimeController>(includeInactive: true);
                if (controller == null)
                {
                    continue;
                }

                RoomEntryAnchor anchor = moduleInstance.GetComponentInChildren<RoomEntryAnchor>(includeInactive: true);
                Vector3 targetPosition = anchor != null ? anchor.transform.position : moduleInstance.transform.position;

                WarpPlayer(targetPosition);
                Debug.Log($"[DungeonRunBootstrap] Warped player to first room '{controller.name}' at {targetPosition}.");

                // OnTriggerEnter2D 가 *이미 trigger 안에 있는 상태* 에서는 발동하지 않으므로,
                // 본 bootstrap 이 controller 의 BeginRoomEntry 를 *직접* 호출해 첫 방 시작을 보장.
                controller.BeginRoomEntry(player);
                Debug.Log($"[DungeonRunBootstrap] Called BeginRoomEntry on first room controller.");
                return;
            }

            Debug.LogWarning("[DungeonRunBootstrap] No module with RoomEntryRuntimeController found among spawned objects. Player not warped.", this);
        }

        // CL-112: DA spawn 결과의 controller-가진 module 들을 *순서대로* mvpRoomDataSequence 의 RoomData 와 매핑.
        // BossArea_Test 처럼 controller 미부착 prefab 은 skip (CL-112 결정 #4 의 Option F1 에서 Boss 에도 controller 부착 권장).
        private void InjectMvpRoomDataSequence(GameObject[] spawnedManagedObjects)
        {
            if (mvpRoomDataSequence == null || mvpRoomDataSequence.Length == 0)
            {
                return;
            }

            int sequenceIndex = 0;
            for (int i = 0; i < spawnedManagedObjects.Length; i++)
            {
                GameObject moduleInstance = spawnedManagedObjects[i];
                if (moduleInstance == null)
                {
                    continue;
                }

                RoomEntryRuntimeController controller = moduleInstance.GetComponentInChildren<RoomEntryRuntimeController>(includeInactive: true);
                if (controller == null)
                {
                    continue;
                }

                if (sequenceIndex >= mvpRoomDataSequence.Length)
                {
                    Debug.LogWarning($"[DungeonRunBootstrap] More controllers than mvpRoomDataSequence entries (idx {sequenceIndex}). Extra modules use prefab default RoomData.", this);
                    return;
                }

                RoomData injected = mvpRoomDataSequence[sequenceIndex];
                if (injected != null)
                {
                    controller.SetRoomData(injected);
                    Debug.Log($"[DungeonRunBootstrap] Injected '{injected.RoomId}' into '{controller.name}' (idx {sequenceIndex}).");
                }
                else
                {
                    Debug.LogWarning($"[DungeonRunBootstrap] mvpRoomDataSequence[{sequenceIndex}] is null. Skipping injection for '{controller.name}'.", this);
                }
                sequenceIndex++;
            }

            if (sequenceIndex != mvpRoomDataSequence.Length)
            {
                Debug.LogWarning($"[DungeonRunBootstrap] Injected {sequenceIndex}/{mvpRoomDataSequence.Length} RoomData. Remainder unused.", this);
            }
        }

        private void WarpPlayer(Vector3 position)
        {
            if (player == null)
            {
                return;
            }

            TopDownController controller = player.GetComponent<TopDownController>();
            if (controller != null)
            {
                controller.SetMovement(Vector3.zero);
                controller.MovePosition(position, true);
            }
            else
            {
                player.transform.position = position;
            }
        }
    }
}

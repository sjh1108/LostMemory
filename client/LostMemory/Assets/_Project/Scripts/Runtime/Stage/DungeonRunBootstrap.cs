using DungeonArchitect;
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

            // CL-048: RunManager 등 외부 구독자에게 DA build 완료 신호. warp / BeginRoomEntry 보다 *먼저* 발행하여
            // 외부 시스템이 InRun 전이 후 첫 방 진입 흐름을 관찰할 수 있게 함.
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

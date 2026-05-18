using UnityEngine;

namespace LostMemory.Stage.Data
{
    /// <summary>
    /// 한 방의 정적 템플릿 데이터를 담는 ScriptableObject.
    /// Assets/_Project/ScriptableObjects/Rooms/ 아래에 .asset 으로 저장한다.
    ///
    /// 책임 분리:
    /// - RoomData (본 SO): 정적 템플릿 — 디자이너가 만들고 모든 클라이언트가 동일하게 본다.
    /// - StageRoomProgress: 런타임 상태(visited/completed) — per-run mutable, 호스트 권위.
    ///
    /// 본 SO 는 CL-032 단계에서는 *스켈레톤 + 슬롯* 까지만 정의한다.
    /// 슬롯 내부 데이터는 후속 CL 이 채운다 — 슬롯 매핑은 docs/khi/cl032_room_data_structure_plan.md 참고.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomData_New", menuName = "LostMemory/Stage/RoomData")]
    public class RoomData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string roomId = string.Empty;
        [SerializeField] private string displayName = string.Empty;

        [Header("Type / Category")]
        [SerializeField] private StageRoomType roomType = StageRoomType.Combat;
        [SerializeField] private RoomCategory category = RoomCategory.SmallRoom;
        [SerializeField] private BossEntryRequirementMode bossEntryRequirementMode = BossEntryRequirementMode.Auto;
        [SerializeField, Min(0)] private int sequenceIndex;

        [Header("Layout")]
        [SerializeField] private GameObject layoutPrefab;

        [Header("Slots (filled by CL-033~036 / reward CL)")]
        [SerializeField] private RoomInitContextSpec initContext;
        [SerializeField] private RoomEncounterSpec encounter;
        [SerializeField] private RoomClearConditionType clearCondition = RoomClearConditionType.AllEnemiesDefeated;
        [SerializeField] private RoomExitSpec[] exits;
        [SerializeField] private RoomRewardPoolSpec rewardPool;

        public string RoomId => roomId;
        public string DisplayName => displayName;
        public StageRoomType RoomType => roomType;
        public RoomCategory Category => category;
        public BossEntryRequirementMode BossEntryRequirementMode => bossEntryRequirementMode;
        public int SequenceIndex => sequenceIndex;
        public GameObject LayoutPrefab => layoutPrefab;
        public RoomInitContextSpec InitContext => initContext;
        public RoomEncounterSpec Encounter => encounter;
        public RoomClearConditionType ClearCondition => clearCondition;
        public RoomExitSpec[] Exits => exits;
        public RoomRewardPoolSpec RewardPool => rewardPool;
    }
}

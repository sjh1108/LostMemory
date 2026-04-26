using UnityEngine;

namespace LostMemory.Stage.Data
{
    // Layout prefab 안 빈 GameObject 에 붙어 한 스폰 위치를 표현한다.
    // 위치는 Transform, 그룹 태그는 본 컴포넌트가 보유.
    public sealed class RoomEncounterSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string groupTag = string.Empty;

        public string GroupTag => groupTag;
    }
}

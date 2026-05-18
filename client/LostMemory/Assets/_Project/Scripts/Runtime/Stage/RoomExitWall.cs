using UnityEngine;

namespace LostMemory.Stage
{
    // 방의 출구를 막는 벽 GameObject 에 붙는 식별 컴포넌트.
    // 본 컴포넌트 자체는 enable/disable 로직을 갖지 않는다 — RoomEntryRuntimeController 가
    // 진입 / 클리어 시점에 GameObject.SetActive(true/false) 로 토글한다.
    //
    // GameObject 자체에 Sprite / BoxCollider2D (non-trigger) 등의 시각·물리 컴포넌트가 있으면
    // SetActive 토글로 함께 켜지고 꺼진다. 디자이너가 prefab 안에서 자유롭게 셋업.
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Room Exit Wall")]
    public sealed class RoomExitWall : MonoBehaviour
    {
        [SerializeField] private string exitTag = string.Empty;

        public string ExitTag => exitTag;

        public bool Matches(string candidateTag)
        {
            return !string.IsNullOrEmpty(exitTag) && exitTag == candidateTag;
        }
    }
}

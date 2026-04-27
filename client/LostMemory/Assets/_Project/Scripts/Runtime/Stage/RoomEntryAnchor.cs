using UnityEngine;

namespace LostMemory.Stage
{
    // 방 진입 시 player 가 정렬될 위치.
    // RoomInitContextSpec.PlayerSpawnAnchorTag 와 anchorTag 가 정확히 일치하는 첫 anchor 를 사용한다.
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Room Entry Anchor")]
    public sealed class RoomEntryAnchor : MonoBehaviour
    {
        [SerializeField] private string anchorTag = "default";

        public string AnchorTag => anchorTag;

        public bool Matches(string candidateTag)
        {
            return !string.IsNullOrEmpty(anchorTag) && anchorTag == candidateTag;
        }
    }
}

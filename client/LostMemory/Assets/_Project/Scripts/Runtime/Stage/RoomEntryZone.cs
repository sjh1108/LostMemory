using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    // 플레이어가 BoxCollider2D trigger 안에 들어오면 controller.BeginRoomEntry 를 호출.
    // 1회 보장은 controller 가 책임 — zone 은 단순 wrapper.
    // CL-036 이 zone 을 우회하고 controller 를 직접 호출해도 동일 흐름.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Lost Memory/Stage/Room Entry Zone")]
    public sealed class RoomEntryZone : MonoBehaviour
    {
        [SerializeField] private RoomEntryRuntimeController controller;

        private void Reset()
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            controller = GetComponentInParent<RoomEntryRuntimeController>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (controller == null || other == null)
            {
                return;
            }

            Character character = other.GetComponentInParent<Character>();
            if (character == null || character.CharacterType != Character.CharacterTypes.Player)
            {
                return;
            }

            controller.BeginRoomEntry(character);
        }
    }
}

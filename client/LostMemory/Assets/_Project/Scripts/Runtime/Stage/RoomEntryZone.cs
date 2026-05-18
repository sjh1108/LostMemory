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
            Debug.Log($"[Zone] OnTriggerEnter2D on '{name}' by '{(other != null ? other.name : "null")}'");
            if (controller == null || other == null)
            {
                Debug.Log($"[Zone] skip: controller={controller != null} other={other != null}");
                return;
            }

            Character character = other.GetComponentInParent<Character>();
            if (character == null)
            {
                Debug.Log($"[Zone] skip: no Character on '{other.name}'");
                return;
            }
            if (character.CharacterType != Character.CharacterTypes.Player)
            {
                Debug.Log($"[Zone] skip: '{character.name}' type={character.CharacterType} (not Player)");
                return;
            }

            Debug.Log($"[Zone] forward to controller.BeginRoomEntry");
            controller.BeginRoomEntry(character);
        }
    }
}

using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Stage
{
    // 플레이어가 BoxCollider2D trigger 안에 들어오면 controller.BeginRoomEntry 를 호출.
    // 1회 보장은 controller 가 책임 — zone 은 단순 wrapper.
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

            // 멀티: PlayerMovementSync.convertNonOwnerToAi=true 면 게스트 player 가 호스트 측에선 AI 로 변환됨.
            // CharacterType 만 보면 게스트 player skip → BeginRoomEntry 미발동.
            // NetworkObject.IsPlayerObject 로 보강 — NGO PlayerObject 도 player 로 인식.
            bool isPlayerType = character.CharacterType == Character.CharacterTypes.Player;
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            bool isNetworkedPlayer = netObj != null && netObj.IsPlayerObject;

            if (!isPlayerType && !isNetworkedPlayer)
            {
                Debug.Log($"[Zone] skip: '{character.name}' type={character.CharacterType} netPlayer={isNetworkedPlayer}");
                return;
            }

            Debug.Log($"[Zone] forward to controller.BeginRoomEntry (player={isPlayerType} netPlayer={isNetworkedPlayer})");
            controller.BeginRoomEntry(character);
        }
    }
}

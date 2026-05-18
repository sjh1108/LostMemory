using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Route Node Spawn Point")]
    public sealed class RouteNodeSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string spawnId = "default";
        [SerializeField] private Vector2 facing = Vector2.down;

        public string SpawnId => spawnId;
        public Vector2 Facing => facing;

        public bool Matches(string candidateSpawnId)
        {
            if (string.IsNullOrEmpty(spawnId))
            {
                return string.IsNullOrEmpty(candidateSpawnId);
            }

            return spawnId == candidateSpawnId;
        }

        public void Place(Character character, Vector3 offset)
        {
            if (character == null)
            {
                return;
            }

            Vector3 target = transform.position + offset;
            TopDownController controller = character.GetComponent<TopDownController>();
            if (controller != null)
            {
                controller.SetMovement(Vector3.zero);
                controller.MovePosition(target, true);
            }
            else
            {
                character.transform.position = target;
            }

            ApplyFacing(character);
        }

        private void ApplyFacing(Character character)
        {
            if (character == null || facing.sqrMagnitude <= Mathf.Epsilon)
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

        private static Character.FacingDirections ResolveFacing(Vector2 value)
        {
            if (Mathf.Abs(value.x) >= Mathf.Abs(value.y))
            {
                return value.x >= 0f
                    ? Character.FacingDirections.East
                    : Character.FacingDirections.West;
            }

            return value.y >= 0f
                ? Character.FacingDirections.North
                : Character.FacingDirections.South;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
            Gizmos.DrawSphere(transform.position, 0.25f);

            if (facing.sqrMagnitude > Mathf.Epsilon)
            {
                Vector3 dir = new Vector3(facing.normalized.x, facing.normalized.y, 0f);
                Gizmos.DrawLine(transform.position, transform.position + dir);
            }
        }
    }
}

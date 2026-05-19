using System;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Boss Room Entry Point")]
    public sealed class BossRoomEntryPoint : MonoBehaviour
    {
        [SerializeField] private string entryPointId = "Boss";
        [SerializeField] private Character.FacingDirections facingDirection = Character.FacingDirections.North;
        [SerializeField] private Transform[] arrivalPoints = Array.Empty<Transform>();

        public string EntryPointId => entryPointId;
        public Character.FacingDirections FacingDirection => facingDirection;

        public void Configure(
            string configuredEntryPointId,
            Character.FacingDirections configuredFacingDirection,
            Transform[] configuredArrivalPoints = null)
        {
            entryPointId = string.IsNullOrWhiteSpace(configuredEntryPointId)
                ? "Boss"
                : configuredEntryPointId;
            facingDirection = configuredFacingDirection;
            arrivalPoints = configuredArrivalPoints ?? Array.Empty<Transform>();
        }

        public bool Matches(string candidateId)
        {
            return !string.IsNullOrWhiteSpace(entryPointId) &&
                   string.Equals(entryPointId, candidateId, StringComparison.Ordinal);
        }

        public Transform GetArrivalPoint(int participantIndex)
        {
            if (arrivalPoints == null || arrivalPoints.Length == 0)
            {
                return transform;
            }

            int resolvedIndex = Mathf.Max(0, participantIndex);
            int validIndex = 0;
            Transform lastValidPoint = null;

            for (int i = 0; i < arrivalPoints.Length; i++)
            {
                Transform arrivalPoint = arrivalPoints[i];
                if (arrivalPoint == null)
                {
                    continue;
                }

                if (validIndex == resolvedIndex)
                {
                    return arrivalPoint;
                }

                lastValidPoint = arrivalPoint;
                validIndex++;
            }

            return lastValidPoint != null ? lastValidPoint : transform;
        }

        private void Reset()
        {
            int childCount = transform.childCount;
            if (childCount <= 0)
            {
                arrivalPoints = Array.Empty<Transform>();
                return;
            }

            arrivalPoints = new Transform[childCount];
            for (int i = 0; i < childCount; i++)
            {
                arrivalPoints[i] = transform.GetChild(i);
            }
        }
    }
}

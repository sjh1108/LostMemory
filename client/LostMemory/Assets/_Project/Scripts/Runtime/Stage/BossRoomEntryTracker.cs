using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Boss Room Entry Tracker")]
    public sealed class BossRoomEntryTracker : MonoBehaviour
    {
        [SerializeField] private List<StageRoomProgress> roomSequence = new List<StageRoomProgress>();

        private BossRoomEntryConditionResult _currentCondition;

        public event Action<BossRoomEntryConditionResult> BossEntryConditionChanged;

        public IReadOnlyList<StageRoomProgress> RoomSequence => roomSequence;
        public BossRoomEntryConditionResult CurrentCondition => _currentCondition;
        public bool CanEnterBossRoom => _currentCondition.CanEnterBossRoom;

        private void Awake()
        {
            RefreshCondition(false);
        }

        private void OnValidate()
        {
            RefreshCondition(false);
        }

        public BossRoomEntryConditionResult RefreshCondition(bool notifyListeners = true)
        {
            _currentCondition = BossRoomEntryConditionCalculator.Evaluate(roomSequence);

            if (notifyListeners)
            {
                BossEntryConditionChanged?.Invoke(_currentCondition);
            }

            return _currentCondition;
        }

        public void SetRoomSequence(IEnumerable<StageRoomProgress> rooms, bool resetProgress = true)
        {
            roomSequence.Clear();

            if (rooms != null)
            {
                foreach (StageRoomProgress room in rooms)
                {
                    if (room == null)
                    {
                        continue;
                    }

                    if (resetProgress)
                    {
                        room.ResetProgress();
                    }

                    roomSequence.Add(room);
                }
            }

            RefreshCondition();
        }

        public bool TryMarkRoomVisited(string roomId)
        {
            int roomIndex = FindRoomIndex(roomId);
            return roomIndex >= 0 && TryMarkRoomVisitedAt(roomIndex);
        }

        public bool TryMarkRoomCompleted(string roomId)
        {
            int roomIndex = FindRoomIndex(roomId);
            return roomIndex >= 0 && TryMarkRoomCompletedAt(roomIndex);
        }

        public bool TryMarkRoomVisitedAt(int roomIndex)
        {
            StageRoomProgress room = GetRoomAt(roomIndex);
            if (room == null || !room.MarkVisited())
            {
                return false;
            }

            RefreshCondition();
            return true;
        }

        public bool TryMarkRoomCompletedAt(int roomIndex)
        {
            StageRoomProgress room = GetRoomAt(roomIndex);
            if (room == null || !room.MarkCompleted())
            {
                return false;
            }

            RefreshCondition();
            return true;
        }

        public void ResetProgress()
        {
            bool changed = false;

            for (int i = 0; i < roomSequence.Count; i++)
            {
                StageRoomProgress room = roomSequence[i];
                if (room != null)
                {
                    changed |= room.ResetProgress();
                }
            }

            if (changed)
            {
                RefreshCondition();
            }
            else
            {
                RefreshCondition(false);
            }
        }

        private int FindRoomIndex(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return -1;
            }

            for (int i = 0; i < roomSequence.Count; i++)
            {
                StageRoomProgress room = roomSequence[i];
                if (room != null && string.Equals(room.RoomId, roomId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private StageRoomProgress GetRoomAt(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= roomSequence.Count)
            {
                return null;
            }

            return roomSequence[roomIndex];
        }
    }
}

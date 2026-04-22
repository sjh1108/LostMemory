using System;
using UnityEngine;

namespace LostMemory.TestKhi
{
    public enum KhiAttackDirection
    {
        Right = 0,
        Up = 1,
        Left = 2,
        Down = 3
    }

    [Serializable]
    public struct KhiAttackRequest
    {
        public int SequenceId;
        public int ComboStep;
        public KhiAttackDirection Direction;
        public Vector2 DirectionVector;
        public Vector3 Origin;
        public float StartedAt;
        public GameObject Attacker;
    }

    [Serializable]
    public struct KhiDirectionalHitbox
    {
        public Vector2 Offset;
        public Vector2 Size;

        public KhiDirectionalHitbox(Vector2 offset, Vector2 size)
        {
            Offset = offset;
            Size = size;
        }
    }

    [Serializable]
    public class KhiMeleeAttackStep
    {
        public int ComboStep = 1;
        public float DamageMultiplier = 1f;
        public float StartupDuration = 0.05f;
        public float ActiveDuration = 0.07f;
        public float RecoveryDuration = 0.08f;
        public string AnimatorTrigger = "Attack_1";

        public KhiDirectionalHitbox Right;
        public KhiDirectionalHitbox Up;
        public KhiDirectionalHitbox Left;
        public KhiDirectionalHitbox Down;

        public KhiDirectionalHitbox GetHitbox(KhiAttackDirection direction)
        {
            return direction switch
            {
                KhiAttackDirection.Up => Up,
                KhiAttackDirection.Left => Left,
                KhiAttackDirection.Down => Down,
                _ => Right
            };
        }
    }
}

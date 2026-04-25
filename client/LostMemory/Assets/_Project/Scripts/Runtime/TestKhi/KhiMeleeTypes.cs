using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace LostMemory.TestKhi
{
    [Serializable]
    public struct KhiAttackRequest
    {
        public int SequenceId;
        public int ComboStep;
        public Vector2 AimDirection;      // 정규화된 aim 벡터 (360° 연속)
        public float AimAngleDegrees;     // atan2(AimDirection.y, AimDirection.x) * Rad2Deg
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

        // Right 기준 baseline hitbox. 런타임에서 aim 각도로 회전시켜 사용.
        // 기존 Right 필드에서 자동 마이그레이션 (FormerlySerializedAs).
        [FormerlySerializedAs("Right")]
        public KhiDirectionalHitbox Baseline;
    }
}

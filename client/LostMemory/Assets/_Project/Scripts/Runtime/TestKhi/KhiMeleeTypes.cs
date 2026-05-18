using System;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 한 attack 코루틴 동안 유지되는 런타임 공격 정보.
    /// SO 데이터 (`AttackStepData`) 와 분리된 일시 상태 — 매 공격마다 새로 생성.
    /// </summary>
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
}

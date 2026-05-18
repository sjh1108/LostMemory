using System;
using UnityEngine;

namespace LostMemory.Stage.Data
{
    [Serializable]
    public sealed class RoomEncounterWave
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Inspector 가독용 라벨. 게임 로직과 무관. 예: \"Wave 1: 근접 2 → 50% 사망 후 Wave 2\".")]
        private string label = string.Empty;

        [Header("Trigger (when this wave's NEXT wave will spawn)")]
        [SerializeField, Min(0f), Tooltip("방 진입 후 이 wave 가 spawn 될 때까지의 절대 시간(초). 0 = 즉시.")]
        private float startDelay;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("이 wave 의 적이 본 비율(0~1) 사망하면 다음 wave 가 spawn.\n" +
                 "0 = 비활성 (다음 wave 의 startDelay 만 트리거).\n" +
                 "0.5 = 50% 사망 시 다음 wave.\n" +
                 "마지막 wave 는 다음이 없으므로 무시됨.\n" +
                 "다음 wave 의 startDelay 와 OR — 둘 중 먼저 만족되는 쪽으로 spawn.")]
        private float triggerNextWaveDeathRatio;

        [Header("Enemies")]
        [SerializeField, Tooltip("이 wave 에서 spawn 할 적 종류·수량 묶음.")]
        private RoomEncounterEnemyEntry[] enemies = Array.Empty<RoomEncounterEnemyEntry>();

        public string Label => label;
        public float StartDelay => startDelay;
        public float TriggerNextWaveDeathRatio => triggerNextWaveDeathRatio;
        public RoomEncounterEnemyEntry[] Enemies => enemies ?? Array.Empty<RoomEncounterEnemyEntry>();
    }
}

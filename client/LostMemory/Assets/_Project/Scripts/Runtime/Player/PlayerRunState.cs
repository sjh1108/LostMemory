using System.Collections.Generic;
using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.Player
{
    /// <summary>
    /// 런 중 Player GameObject 가 씬 전환 시 새로 스폰되어 인벤토리/HP 가 초기화되는 문제를 보완.
    /// 씬 전환 직전 (StageRouteManager) 가 Player 의 현재 상태를 Capture 하고, 새 Player 의
    /// Inventory/Health 컴포넌트가 Awake/Start 에서 본 스냅샷을 읽어 자체 복구한다.
    ///
    /// PlayerWallet / MemoryShardWallet 과 동일 패턴 (싱글톤 + DontDestroyOnLoad).
    /// Run 종료 시 RunManager.CleanupRunResultingState 에서 Clear() 호출 → 다음 런 빈 상태 시작.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Player/Player Run State")]
    public sealed class PlayerRunState : MonoBehaviour
    {
        public static PlayerRunState Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static PlayerRunState EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("PlayerRunState");
            return go.AddComponent<PlayerRunState>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool HasSnapshot { get; private set; }
        public PlayerSnapshot Snapshot { get; private set; }

        public void Capture(PlayerSnapshot snapshot)
        {
            Snapshot = snapshot;
            HasSnapshot = true;
        }

        public void Clear()
        {
            Snapshot = default;
            HasSnapshot = false;
        }
    }

    public struct PlayerSnapshot
    {
        public List<RelicPlacementSnapshot> Placements;
        public RelicData[] ConsumableSlots;
        public int BonusSlots;
        public float CurrentHealth;
        public float MaximumHealth;
        public bool HasHealth;
    }

    public struct RelicPlacementSnapshot
    {
        public RelicData Data;
        public int X;
        public int Y;
    }
}

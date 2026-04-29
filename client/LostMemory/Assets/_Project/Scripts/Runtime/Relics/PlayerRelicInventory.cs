using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// 런(Run) 중 플레이어가 보유한 유물 목록을 관리한다.
    /// RunManager 또는 Player GameObject에 부착한다.
    /// </summary>
    public class PlayerRelicInventory : MonoBehaviour
    {
        private readonly List<RelicData> _ownedRelics = new();

        /// <summary>현재 보유한 유물 목록 (읽기 전용)</summary>
        public IReadOnlyList<RelicData> OwnedRelics => _ownedRelics;

        /// <summary>유물이 새로 획득되었을 때 발화. RelicEffectApplier 등이 구독.</summary>
        public event Action<RelicData> OnRelicAcquired;

        /// <summary>
        /// 유물을 인벤토리에 추가한다.
        /// 이미 보유 중이거나 소모품이면 추가하지 않는다.
        /// </summary>
        /// <returns>실제로 추가됐으면 true</returns>
        public bool TryAdd(RelicData relic)
        {
            if (relic == null)
            {
                Debug.LogWarning("[PlayerRelicInventory] relic is null");
                return false;
            }

            if (relic.IsConsumable)
            {
                // 즉시 사용 소모품(랜덤박스 등)은 인벤토리에 등록하지 않음
                if (relic.IsInstantUse)
                {
                    Debug.Log($"[PlayerRelicInventory] 즉시 사용 소모품 — 인벤토리 미등록: {relic.DisplayName}");
                    return false;
                }

                // 보관 소모품(물약 등)은 중복 허용하여 인벤토리에 추가
                _ownedRelics.Add(relic);
                Debug.Log($"[PlayerRelicInventory] 소모품 획득: {relic.DisplayName}");
                return true;
            }

            // 일반 유물: 중복 체크
            if (Has(relic))
            {
                Debug.LogWarning($"[PlayerRelicInventory] 이미 보유 중: {relic.DisplayName}");
                return false;
            }

            _ownedRelics.Add(relic);
            OnRelicAcquired?.Invoke(relic);
            Debug.Log($"[PlayerRelicInventory] 유물 획득: {relic.DisplayName}");
            return true;
        }

        /// <summary>해당 유물을 보유 중인지 확인한다.</summary>
        public bool Has(RelicData relic) =>
            _ownedRelics.Any(r => r.name == relic.name);

        /// <summary>
        /// RewardPool.DrawThree()에 넘길 보유 유물 이름 목록을 반환한다.
        /// </summary>
        public IEnumerable<string> GetOwnedNames() =>
            _ownedRelics.Select(r => r.name);

        /// <summary>런 종료 시 인벤토리를 초기화한다.</summary>
        public void Clear() => _ownedRelics.Clear();

        // ── CL-107 검증용 디버그 진입점 ─────────────────────
        // CL-110 의 RewardPanel 자동 흐름이 완성되기 전까지 Inspector 에서 수동 TryAdd 로 효과 검증.
        [Header("Debug (CL-107 검증용)")]
        [Tooltip("Inspector 우상단 ︙ → 'Debug — Add all assigned relics' 클릭 시 모두 TryAdd. 비워두면 무동작.")]
        [SerializeField] private RelicData[] _debugRelicsToAdd;

        [ContextMenu("Debug — Add all assigned relics")]
        private void DebugAddAllAssignedRelics()
        {
            if (_debugRelicsToAdd == null) return;
            foreach (RelicData r in _debugRelicsToAdd)
            {
                if (r != null) TryAdd(r);
            }
        }
    }
}

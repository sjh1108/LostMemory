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
                // 소모품은 단축키바(PlayerConsumableInventory)로 라우팅 — 유물 인벤토리 미등록
                Debug.Log($"[PlayerRelicInventory] 소모품은 단축키바로 라우팅: {relic.DisplayName}");
                return false;
            }

            // 일반 유물: 중복 체크
            if (Has(relic))
            {
                Debug.LogWarning($"[PlayerRelicInventory] 이미 보유 중: {relic.DisplayName}");
                return false;
            }

            int emptyIdx = _ownedRelics.IndexOf(null);
            if (emptyIdx >= 0) _ownedRelics[emptyIdx] = relic;
            else               _ownedRelics.Add(relic);
            Debug.Log($"[PlayerRelicInventory] 유물 획득: {relic.DisplayName}");
            return true;
        }

        /// <summary>해당 유물을 보유 중인지 확인한다.</summary>
        public bool Has(RelicData relic) =>
            _ownedRelics.Any(r => r != null && r.name == relic.name);

        /// <summary>
        /// RewardPool.DrawThree()에 넘길 보유 유물 이름 목록을 반환한다.
        /// </summary>
        public IEnumerable<string> GetOwnedNames() =>
            _ownedRelics.Where(r => r != null).Select(r => r.name);

        /// <summary>유물을 인벤토리에서 1개 제거한다.</summary>
        /// <returns>실제로 제거됐으면 true</returns>
        public bool Remove(RelicData relic)
        {
            if (relic == null) return false;
            int idx = _ownedRelics.FindIndex(r => r != null && r.name == relic.name);
            if (idx < 0) return false;
            _ownedRelics[idx] = null;   // 슬롯 위치 유지 — 제거 대신 null로 교체
            Debug.Log($"[PlayerRelicInventory] 유물 버림: {relic.DisplayName}");
            return true;
        }

        /// <summary>두 인덱스의 유물 위치를 교환한다. 리스트가 짧으면 null로 채워 확장한다.</summary>
        public void Swap(int indexA, int indexB)
        {
            if (indexA < 0 || indexB < 0) return;
            if (indexA == indexB) return;

            // 목적지 인덱스까지 null로 채워 리스트를 확장
            int needed = Mathf.Max(indexA, indexB) + 1;
            while (_ownedRelics.Count < needed)
                _ownedRelics.Add(null);

            (_ownedRelics[indexA], _ownedRelics[indexB]) = (_ownedRelics[indexB], _ownedRelics[indexA]);
        }

        /// <summary>런 종료 시 인벤토리를 초기화한다.</summary>
        public void Clear() => _ownedRelics.Clear();
    }
}

using System.Collections.Generic;
using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// 단축키바에 들어가는 소모품 슬롯 4칸을 관리한다.
    /// PlayerRelicInventory와 별도로 존재하며, 소모품(IsConsumable=true)만 보관한다.
    /// </summary>
    public class PlayerConsumableInventory : MonoBehaviour
    {
        public const int SlotCount = 4;

        private readonly RelicData[] _slots = new RelicData[SlotCount];

        /// <summary>슬롯 전체 목록 (null = 빈 칸)</summary>
        public IReadOnlyList<RelicData> Slots => _slots;

        /// <summary>
        /// 소모품을 첫 번째 빈 칸에 추가한다.
        /// </summary>
        /// <returns>추가 성공이면 true, 단축키바가 꽉 찼으면 false</returns>
        public bool TryAdd(RelicData consumable)
        {
            if (consumable == null || !consumable.IsConsumable)
            {
                Debug.LogWarning("[PlayerConsumableInventory] 소모품이 아닌 아이템은 추가할 수 없습니다.");
                return false;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == null)
                {
                    _slots[i] = consumable;
                    Debug.Log($"[PlayerConsumableInventory] 소모품 획득 (슬롯 {i + 1}): {consumable.DisplayName}");
                    return true;
                }
            }

            Debug.LogWarning("[PlayerConsumableInventory] 단축키바가 꽉 찼습니다.");
            return false;
        }

        /// <summary>지정한 슬롯의 소모품을 제거한다.</summary>
        public bool Remove(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return false;
            if (_slots[slotIndex] == null) return false;

            Debug.Log($"[PlayerConsumableInventory] 소모품 제거 (슬롯 {slotIndex + 1}): {_slots[slotIndex].DisplayName}");
            _slots[slotIndex] = null;
            return true;
        }

        /// <summary>지정한 슬롯의 소모품을 반환한다. 없으면 null.</summary>
        public RelicData Get(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return null;
            return _slots[slotIndex];
        }

        /// <summary>두 슬롯의 소모품을 교환한다.</summary>
        public void Swap(int indexA, int indexB)
        {
            if (indexA < 0 || indexA >= SlotCount) return;
            if (indexB < 0 || indexB >= SlotCount) return;
            if (indexA == indexB) return;
            (_slots[indexA], _slots[indexB]) = (_slots[indexB], _slots[indexA]);
        }

        /// <summary>런 종료 시 슬롯을 초기화한다.</summary>
        public void Clear()
        {
            for (int i = 0; i < SlotCount; i++)
                _slots[i] = null;
        }
    }
}

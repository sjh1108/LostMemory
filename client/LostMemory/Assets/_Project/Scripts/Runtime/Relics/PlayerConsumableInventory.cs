using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// 단축키바에 들어가는 소모품 슬롯 4칸을 관리한다.
    /// PlayerRelicInventory와 별도로 존재하며, 소모품(IsConsumable=true)만 보관한다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Relics/Player Consumable Inventory")]
    public class PlayerConsumableInventory : MonoBehaviour
    {
        public const int SlotCount = 4;

        private readonly RelicData[] _slots = new RelicData[SlotCount];

        /// <summary>슬롯 전체 목록 (null = 빈 칸)</summary>
        public IReadOnlyList<RelicData> Slots => _slots;

        /// <summary>슬롯 내용이 바뀔 때 발생. ShortcutBarView 갱신용.</summary>
        public event Action Changed;

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
                    Changed?.Invoke();
                    return true;
                }
            }

            Debug.LogWarning("[PlayerConsumableInventory] 단축키바가 꽉 찼습니다.");
            return false;
        }

        /// <summary>
        /// CL-177: 지정 슬롯에 소모품 배치. 슬롯이 차 있으면 false (덮어쓰기 X).
        /// InventoryTestWindow 의 슬롯 지정 UI 가 사용. 게임 보상 흐름은 기존 TryAdd (첫 빈 슬롯 자동) 사용.
        /// </summary>
        /// <returns>배치 성공이면 true, 실패 (차 있음 / 범위 밖 / 소모품 아님) 이면 false</returns>
        public bool TryAddAt(int slot, RelicData consumable)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                Debug.LogWarning($"[PlayerConsumableInventory] 슬롯 인덱스 범위 오류: {slot}");
                return false;
            }
            if (consumable == null || !consumable.IsConsumable)
            {
                Debug.LogWarning("[PlayerConsumableInventory] 소모품이 아닌 아이템은 추가할 수 없습니다.");
                return false;
            }
            if (_slots[slot] != null)
            {
                Debug.LogWarning($"[PlayerConsumableInventory] 슬롯 {slot + 1} 이미 사용 중: {_slots[slot].DisplayName}");
                return false;
            }

            _slots[slot] = consumable;
            Debug.Log($"[PlayerConsumableInventory] 소모품 지정 배치 (슬롯 {slot + 1}): {consumable.DisplayName}");
            Changed?.Invoke();
            return true;
        }

        /// <summary>지정한 슬롯의 소모품을 제거한다.</summary>
        public bool Remove(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return false;
            if (_slots[slotIndex] == null) return false;

            Debug.Log($"[PlayerConsumableInventory] 소모품 제거 (슬롯 {slotIndex + 1}): {_slots[slotIndex].DisplayName}");
            _slots[slotIndex] = null;
            Changed?.Invoke();
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
            Changed?.Invoke();
        }

        /// <summary>런 종료 시 슬롯을 초기화한다.</summary>
        public void Clear()
        {
            bool changed = false;
            for (int i = 0; i < SlotCount; i++)
            {
                changed |= _slots[i] != null;
                _slots[i] = null;
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }
    }
}

using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-228 (A 옵션) — 1 아이템 단순 vending machine 설정.
    /// MysteryShop 의 카드 시스템 (3 슬롯 + 정체 가림 + reveal) 과 분리된 단순 버전.
    /// 사용처: 한 방 안의 NPC 하나가 정해진 1 아이템을 정해진 가격에 판매.
    /// </summary>
    [CreateAssetMenu(fileName = "VendingMachineConfig", menuName = "LostMemory/Events/Vending Machine Config")]
    public sealed class VendingMachineConfig : ScriptableObject
    {
        [Header("판매 아이템")]
        [Tooltip("판매할 RelicData. 보통 소모품 (랜덤박스 / 회복약 등). 유물도 가능.")]
        [SerializeField] private RelicData item;

        [Header("가격")]
        [SerializeField, Min(0)] private int price = 200;

        [Header("표시 라벨 (옵션)")]
        [Tooltip("item 이 null 일 때 표시할 라벨. item 이 있으면 item.DisplayName 우선.")]
        [SerializeField] private string fallbackLabel = "랜덤 박스";

        public RelicData Item => item;
        public int Price => price;
        public string FallbackLabel => fallbackLabel;
    }
}

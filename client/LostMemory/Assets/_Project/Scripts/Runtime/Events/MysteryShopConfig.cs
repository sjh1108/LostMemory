using System.Collections.Generic;
using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-228 — 미스터리 구매 이벤트 방의 설정 SO.
    /// 슬롯 가격 → 가격대별 등급 가중치 → 미보유 유물 추첨 흐름의 입력값.
    ///
    /// 기존 ShopConfig 와의 차이:
    /// - 슬롯 수와 가격을 *디자이너가 명시* (랜덤 X)
    /// - 가격대별 등급 가중치 테이블 — 가격 ↑ 일수록 상위 등급 가중치 ↑
    /// - 후보 유물 없으면 fallbackConsumable 로 대체 (포션 등)
    /// </summary>
    [CreateAssetMenu(fileName = "MysteryShopConfig", menuName = "LostMemory/Events/Mystery Shop Config")]
    public sealed class MysteryShopConfig : ScriptableObject
    {
        [Header("슬롯 가격 (배열 길이 = 슬롯 수)")]
        [Tooltip("각 슬롯의 고정 가격. 가격대별 등급 가중치 테이블 lookup 의 키.")]
        [SerializeField] private int[] slotPrices = { 80, 200, 400 };

        [Header("가격대별 등급 가중치")]
        [Tooltip("가격이 80 이하인 슬롯의 등급별 가중치. Common 위주.")]
        [SerializeField] private RarityWeights tier1Weights = new RarityWeights { common = 80, rare = 20, unique = 0, legendary = 0 };
        [Tooltip("가격이 81~250 사이인 슬롯의 등급별 가중치. Rare 위주.")]
        [SerializeField] private RarityWeights tier2Weights = new RarityWeights { common = 30, rare = 50, unique = 20, legendary = 0 };
        [Tooltip("가격이 251 이상인 슬롯의 등급별 가중치. Unique~Legendary 위주.")]
        [SerializeField] private RarityWeights tier3Weights = new RarityWeights { common = 0,  rare = 30, unique = 50, legendary = 20 };

        [Header("티어 경계 (가격 기준)")]
        [SerializeField] private int tier1MaxPrice = 80;
        [SerializeField] private int tier2MaxPrice = 250;

        [Header("폴백 — 후보 유물 없을 때 대체 지급")]
        [Tooltip("RewardPool 에 미보유 유물이 동난 경우 슬롯에 대신 들어갈 소모품 (보통 큰 회복약).")]
        [SerializeField] private RelicData fallbackConsumable;

        public int[] SlotPrices => slotPrices;
        public RelicData FallbackConsumable => fallbackConsumable;

        /// <summary>가격을 받아 가격대별 RelicRarity → weight 테이블 반환. RewardPool.DrawOneRelicForShop 입력용.</summary>
        public Dictionary<RelicRarity, int> GetRarityWeightsForPrice(int price)
        {
            RarityWeights w;
            if (price <= tier1MaxPrice)      w = tier1Weights;
            else if (price <= tier2MaxPrice) w = tier2Weights;
            else                             w = tier3Weights;

            return new Dictionary<RelicRarity, int>
            {
                { RelicRarity.Common,    w.common    },
                { RelicRarity.Rare,      w.rare      },
                { RelicRarity.Unique,    w.unique    },
                { RelicRarity.Legendary, w.legendary },
            };
        }

        [System.Serializable]
        public struct RarityWeights
        {
            public int common;
            public int rare;
            public int unique;
            public int legendary;
        }
    }
}

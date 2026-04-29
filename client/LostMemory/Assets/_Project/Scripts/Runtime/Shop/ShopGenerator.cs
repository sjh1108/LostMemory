using System.Collections.Generic;
using LostMemory.Relics;
using LostMemory.Rewards;
using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// 상점 방문 시 동적으로 ShopData를 생성하는 유틸리티.
    /// 유물 3칸 + 비유물 1칸(포션 or 랜덤박스)으로 구성한다.
    /// </summary>
    public static class ShopGenerator
    {
        private const int RelicSlotCount = 3;

        /// <summary>
        /// 플레이어 보유 목록·보상 풀·상점 설정을 받아 ShopData를 동적으로 생성한다.
        /// </summary>
        /// <param name="inventory">보유 유물 인벤토리 (이미 보유한 유물 제외용)</param>
        /// <param name="rewardPool">유물 추첨 풀</param>
        /// <param name="config">상점 설정 (가중치·가격·소모품 참조)</param>
        public static ShopData Generate(
            PlayerRelicInventory inventory,
            RewardPool           rewardPool,
            ShopConfig           config)
        {
            var shopData = ScriptableObject.CreateInstance<ShopData>();
            var items    = new List<ShopItemData>();

            // 이번 상점에서 이미 뽑힌 유물도 중복 제외 목록에 추가
            var excludedNames = new HashSet<string>(inventory.GetOwnedNames());

            var rarityWeights = new Dictionary<RelicRarity, int>
            {
                { RelicRarity.Common,    config.RandomBoxCommonWeight    },
                { RelicRarity.Rare,      config.RandomBoxRareWeight      },
                { RelicRarity.Unique,    config.RandomBoxUniqueWeight    },
                { RelicRarity.Legendary, config.RandomBoxLegendaryWeight },
            };

            // ── 유물 슬롯 3개 ───────────────────────────────────────
            for (int i = 0; i < RelicSlotCount; i++)
            {
                var relic = rewardPool.DrawOneRelicForShop(excludedNames, rarityWeights);
                if (relic == null) break;   // 더 이상 후보 없으면 슬롯 줄임

                items.Add(new ShopItemData
                {
                    Relic = relic,
                    Price = GetRelicPrice(relic, config)
                });
                excludedNames.Add(relic.name);   // 이번 상점 내 중복 방지
            }

            // ── 비유물 슬롯 1개 (포션 or 랜덤박스) ──────────────────
            var nonRelic = DrawNonRelicItem(config);
            if (nonRelic != null)
                items.Add(new ShopItemData
                {
                    Relic = nonRelic,
                    Price = GetConsumablePrice(nonRelic, config)
                });

            shopData.Items = items.ToArray();
            return shopData;
        }

        // ── 비유물 추첨 ──────────────────────────────────────────────

        private static RelicData DrawNonRelicItem(ShopConfig config)
        {
            int total = config.SmallPotionWeight + config.LargePotionWeight + config.RandomBoxWeight;
            if (total <= 0) return config.SmallHealPotion;

            int roll = Random.Range(0, total);

            if (roll < config.SmallPotionWeight)
                return config.SmallHealPotion;
            roll -= config.SmallPotionWeight;

            if (roll < config.LargePotionWeight)
                return config.LargeHealPotion;

            return config.RandomBox;
        }

        // ── 가격 계산 ────────────────────────────────────────────────

        private static int GetRelicPrice(RelicData relic, ShopConfig config)
        {
            return relic.Rarity switch
            {
                RelicRarity.Common    => config.CommonPrice,
                RelicRarity.Rare      => config.RarePrice,
                RelicRarity.Unique    => config.UniquePrice,
                RelicRarity.Legendary => config.LegendaryPrice,
                _                     => config.CommonPrice
            };
        }

        private static int GetConsumablePrice(RelicData consumable, ShopConfig config)
        {
            if (consumable == null)                    return 0;
            if (consumable == config.SmallHealPotion)  return config.SmallPotionPrice;
            if (consumable == config.LargeHealPotion)  return config.LargePotionPrice;
            if (consumable == config.RandomBox)        return config.RandomBoxPrice;
            return config.SmallPotionPrice;
        }
    }
}

using System.Collections.Generic;
using LostMemory.Relics;
using LostMemory.Rewards;
using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// 상점 방문 시 동적으로 ShopData를 생성하는 유틸리티.
    ///
    /// Phase A 슬롯 구성:
    ///   1. 유물 3칸 — RewardPool 에서 보유 필터 + 등급 가중 추첨
    ///   2. 포션 1칸 (확정) — SmallPotion 또는 LargePotion 중 가중 추첨 (RandomBox 제외)
    ///   3. 랜덤박스 1칸 (확률 등장) — config.RandomBoxAppearChance 확률로 슬롯 추가
    ///
    /// → 최소 4 슬롯 (RandomBox 미등장 시), 최대 5 슬롯 (등장 시).
    /// → ShopPanel.prefab 의 _itemViews 배열 크기 ≥ 5 여야 5번째 슬롯이 표시됨.
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

            // ── 포션 슬롯 1개 (확정) — Small or Large 가중 추첨 ─────
            var potion = DrawPotionOnly(config);
            if (potion != null)
                items.Add(new ShopItemData
                {
                    Relic = potion,
                    Price = GetConsumablePrice(potion, config)
                });

            // ── 랜덤박스 슬롯 (확률 등장) ──────────────────────────
            // config.RandomBoxAppearChance 로 한 번 굴려서 hit 시에만 5번째 슬롯 추가.
            // miss 시 4 슬롯으로 종료. ShopPanelView 는 shopData.Items.Length 만큼만 표시.
            if (config.RandomBox != null && Random.value < config.RandomBoxAppearChance)
            {
                items.Add(new ShopItemData
                {
                    Relic = config.RandomBox,
                    Price = GetConsumablePrice(config.RandomBox, config)
                });
            }

            shopData.Items = items.ToArray();
            return shopData;
        }

        // ── 포션 추첨 (Small vs Large, RandomBox 제외) ──────────────

        private static RelicData DrawPotionOnly(ShopConfig config)
        {
            int total = config.SmallPotionWeight + config.LargePotionWeight;
            if (total <= 0) return config.SmallHealPotion;

            int roll = Random.Range(0, total);
            if (roll < config.SmallPotionWeight)
                return config.SmallHealPotion;

            return config.LargeHealPotion;
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

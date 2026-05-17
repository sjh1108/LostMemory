using System.Collections.Generic;
using LostMemory.Relics;
using LostMemory.Rewards;
using LostMemory.Shop;
using UnityEngine;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-228 — 미스터리 구매 ShopData 동적 생성.
    /// 기존 ShopGenerator 와 정반대 흐름:
    ///   ShopGenerator: 등급 추첨 → 등급별 가격
    ///   본 generator:  슬롯별 가격 고정 → 가격대별 등급 가중치 → 미보유 유물 추첨
    ///
    /// 결과는 LostMemory.Shop.ShopData 인스턴스로 반환 — ShopItemData[] 그대로 재사용.
    /// 패널 뷰 측에서 *카드 뒷면 + 가격만 노출* 하는 정도가 시각적 차별점.
    /// </summary>
    public static class MysteryShopGenerator
    {
        /// <summary>
        /// 슬롯별 가격이 정해진 미스터리 ShopData 생성.
        /// 가격대별 가중치로 미보유 유물 추첨, 후보 없으면 fallbackConsumable 로 대체.
        /// </summary>
        public static ShopData Generate(
            MysteryShopConfig    config,
            RewardPool           rewardPool,
            PlayerRelicInventory inventory)
        {
            var shopData = ScriptableObject.CreateInstance<ShopData>();
            var items    = new List<ShopItemData>();
            var excluded = new HashSet<string>(inventory.GetOwnedNames());

            int[] prices = config.SlotPrices;
            for (int i = 0; i < prices.Length; i++)
            {
                int price = prices[i];
                Dictionary<RelicRarity, int> rarityWeights = config.GetRarityWeightsForPrice(price);

                RelicData relic = rewardPool.DrawOneRelicForShop(excluded, rarityWeights);
                if (relic == null)
                {
                    // 후보 동남 — 폴백 소모품.
                    relic = config.FallbackConsumable;
                    if (relic == null)
                    {
                        Debug.LogWarning($"[MysteryShopGenerator] Slot {i} (price={price}) 후보 0 + fallback 미설정. 슬롯 skip.");
                        continue;
                    }
                }

                items.Add(new ShopItemData
                {
                    Relic = relic,
                    Price = price,
                });
                excluded.Add(relic.name);
            }

            shopData.Items = items.ToArray();
            return shopData;
        }
    }
}

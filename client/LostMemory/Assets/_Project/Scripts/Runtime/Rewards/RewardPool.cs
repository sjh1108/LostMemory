using System.Collections.Generic;
using System.Linq;
using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.Rewards
{
    /// <summary>
    /// 보상 후보 풀을 보관하고 3개를 추첨하는 ScriptableObject.
    /// 등급별 가중치로 확률을 조절하며, 이미 보유한 유물은 제외한다.
    /// (소모품은 중복 보유 가능하므로 항상 후보에 포함)
    ///
    /// 
    /// </summary>
    [CreateAssetMenu(fileName = "RewardPool",
                     menuName = "LostMemory/Reward Pool")]
    public class RewardPool : ScriptableObject
    {
        [SerializeField] private RelicData[] _allRewards;

        // 등급별 추첨 가중치
        private static readonly Dictionary<RelicRarity, int> RarityWeights = new()
        {
            { RelicRarity.Common,    60 },
            { RelicRarity.Rare,      25 },
            { RelicRarity.Unique,    10 },
            { RelicRarity.Legendary,  5 },
        };

        // 소모품 고정 가중치 (등급 없음)
        // - 현재 3택에서 소무품이 1개 이상 나올 확률 = 8%
        // - 15로 바꾸면 22%로 오름
        private const int ConsumableWeight = 5; 

        /// <summary>
        /// 보유 유물 이름 목록을 받아 1개를 추첨해 반환한다.
        /// 후보가 없으면 null 반환.
        /// </summary>
        public RelicData DrawOne(IEnumerable<string> ownedRelicNames)
        {
            var results = DrawThree(ownedRelicNames);
            return results.Count > 0 ? results[0] : null;
        }

        /// <summary>
        /// 보유 유물 이름 목록을 받아 3개를 추첨해 반환한다.
        /// </summary>
        /// <param name="ownedRelicNames">이미 보유한 유물의 name(asset 파일명) 집합</param>
        public List<RelicData> DrawThree(IEnumerable<string> ownedRelicNames)
        {
            var owned = new HashSet<string>(ownedRelicNames);

            // 소모품은 항상 포함, 유물은 미보유만 포함
            var available = _allRewards
                .Where(r => r.IsConsumable || !owned.Contains(r.name))
                .ToList();

            return PickWeighted(available, 3);
        }

        /// <summary>
        /// 상점 유물 슬롯용 추첨.
        /// 소모품을 제외하고, 미보유 유물만 대상으로 커스텀 가중치를 적용해 1개를 반환한다.
        /// 후보가 없으면 null 반환.
        /// </summary>
        /// <param name="excludedNames">제외할 유물 이름 목록 (보유 유물 + 이번 상점 중복 방지)</param>
        /// <param name="rarityWeights">등급별 가중치 테이블</param>
        public RelicData DrawOneRelicForShop(
            IEnumerable<string>              excludedNames,
            Dictionary<RelicRarity, int>     rarityWeights)
        {
            var excluded   = new HashSet<string>(excludedNames);
            var candidates = _allRewards
                .Where(r => !r.IsConsumable && !excluded.Contains(r.name))
                .ToList();

            if (candidates.Count == 0) return null;

            return PickOneWeightedByRarity(candidates, rarityWeights);
        }

        // ── private ──────────────────────────────────────────────

        private List<RelicData> PickWeighted(List<RelicData> pool, int count)
        {
            var result    = new List<RelicData>();
            var remaining = new List<RelicData>(pool);

            for (int i = 0; i < count && remaining.Count > 0; i++)
            {
                int totalWeight = remaining.Sum(GetWeight);
                int roll        = Random.Range(0, totalWeight);
                int cumulative  = 0;

                foreach (var item in remaining)
                {
                    cumulative += GetWeight(item);
                    if (roll < cumulative)
                    {
                        result.Add(item);
                        remaining.Remove(item);
                        break;
                    }
                }
            }

            return result;
        }

        private static int GetWeight(RelicData data)
            => data.IsConsumable ? ConsumableWeight : RarityWeights[data.Rarity];

        private static RelicData PickOneWeightedByRarity(
            List<RelicData>              pool,
            Dictionary<RelicRarity, int> weights)
        {
            int totalWeight = pool.Sum(r => weights.TryGetValue(r.Rarity, out int w) ? w : 0);
            if (totalWeight <= 0)
                return pool[Random.Range(0, pool.Count)];   // 가중치 없으면 균등 추첨

            int roll       = Random.Range(0, totalWeight);
            int cumulative = 0;

            foreach (var item in pool)
            {
                if (!weights.TryGetValue(item.Rarity, out int w)) continue;
                cumulative += w;
                if (roll < cumulative) return item;
            }

            return pool[pool.Count - 1];   // 안전망
        }
    }
}

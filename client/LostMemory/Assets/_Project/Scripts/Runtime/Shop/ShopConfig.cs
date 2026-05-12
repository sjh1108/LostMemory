using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// 상점 구성에 필요한 설정을 보관하는 ScriptableObject.
    /// 가중치·가격·소모품 참조를 Inspector에서 조정할 수 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "LostMemory/Shop Config")]
    public class ShopConfig : ScriptableObject
    {
        // ── 비유물 슬롯 구성 ─────────────────────────────────────────
        [Header("비유물 슬롯 후보 아이템")]
        public RelicData SmallHealPotion;
        public RelicData LargeHealPotion;
        public RelicData RandomBox;

        [Header("포션 슬롯 가중치 — Small vs Large (RandomBox 제외)")]
        [Tooltip("매 상점 4번째 슬롯에 확정 등장하는 포션의 종류 가중치.")]
        public int SmallPotionWeight = 50;   // 67% (50 / 75)
        public int LargePotionWeight = 25;   // 33% (25 / 75)
        [System.Obsolete("Phase A 이후 비사용. 랜덤박스는 RandomBoxAppearChance 로 독립 슬롯 등장.", false)]
        public int RandomBoxWeight   = 25;   // deprecated

        [Header("랜덤박스 슬롯 등장 (Phase A)")]
        [Tooltip("매 상점에서 랜덤박스 슬롯이 등장할 확률. 0=절대 미등장, 1=항상 등장.")]
        [Range(0f, 1f)]
        public float RandomBoxAppearChance = 0.3f;

        // ── 랜덤박스 설정 ────────────────────────────────────────────
        [Header("랜덤박스 유물 등급 가중치")]
        public int RandomBoxCommonWeight    = 55;
        public int RandomBoxRareWeight      = 30;
        public int RandomBoxUniqueWeight    = 12;
        public int RandomBoxLegendaryWeight =  3;

        [Header("랜덤박스 폴백 (후보 유물 없을 때 지급)")]
        [Tooltip("후보 유물이 없을 때 대신 지급할 아이템. 보통 큰 회복약.")]
        public RelicData RandomBoxFallback;

        // ── 유물 가격 ────────────────────────────────────────────────
        [Header("유물 슬롯 가격 (등급별)")]
        public int CommonPrice    = 100;
        public int RarePrice      = 200;
        public int UniquePrice    = 300;
        public int LegendaryPrice = 500;

        // ── 소모품 가격 ──────────────────────────────────────────────
        [Header("소모품 가격")]
        public int SmallPotionPrice = 50;
        public int LargePotionPrice = 80;
        public int RandomBoxPrice   = 150;
    }
}

using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// 유물 등급별 색상 — 슬롯 배경, 툴팁 텍스트 등에서 공유.
    /// </summary>
    public static class RelicRarityColors
    {
        public static readonly Color Empty      = new(0.18f, 0.18f, 0.22f);
        public static readonly Color Consumable = new(0.55f, 0.55f, 0.55f);

        /// <summary>등급별 원색 (텍스트/포커스 표시용).</summary>
        public static Color Bright(RelicRarity rarity) => rarity switch
        {
            RelicRarity.Common    => Color.white,
            RelicRarity.Rare      => new Color(0.31f, 0.59f, 0.96f),
            RelicRarity.Unique    => new Color(1f,    0.30f, 0.99f),
            RelicRarity.Legendary => new Color(0.96f, 0.77f, 0.26f),
            _                     => Color.white
        };

        /// <summary>슬롯 배경용 톤다운 색.</summary>
        public static Color Slot(RelicData relic)
        {
            if (relic == null) return Empty;
            Color baseColor = relic.IsConsumable
                ? Consumable
                : Bright(relic.Rarity);
            Color c = baseColor * 0.45f;
            c.a = 1f;
            return c;
        }

        /// <summary>호버 시 밝게 한 슬롯 색.</summary>
        public static Color SlotHover(RelicData relic)
        {
            Color c = Slot(relic) * 1.6f;
            c.a = 1f;
            return c;
        }

        /// <summary>툴팁 이름 등 텍스트 색.</summary>
        public static Color Text(RelicData relic)
        {
            if (relic == null) return Color.white;
            if (relic.IsConsumable) return new Color(0.7f, 0.7f, 0.7f);
            return Bright(relic.Rarity);
        }
    }
}

using System;
using LostMemory.Relics;

namespace LostMemory.Shop
{
    /// <summary>상점에서 판매하는 아이템 1개 (유물 + 가격)</summary>
    [Serializable]
    public class ShopItemData
    {
        public RelicData Relic;
        public int Price;
    }
}

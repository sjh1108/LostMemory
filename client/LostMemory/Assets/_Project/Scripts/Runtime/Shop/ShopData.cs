using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// 상점에서 판매할 아이템 목록을 보관하는 ScriptableObject.
    /// Assets/_Project/ScriptableObjects/Shop/ 아래에 저장한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopData", menuName = "LostMemory/Shop Data")]
    public class ShopData : ScriptableObject
    {
        public ShopItemData[] Items;
    }
}

using LostMemory.Relics;
using LostMemory.Rewards;
using LostMemory.Shop;
using UnityEngine;

/// <summary>상점 UI 테스트용 스크립트</summary>
public class TestShop : MonoBehaviour
{
    [SerializeField] private ShopPanelView        _shopPanel;
    [SerializeField] private InventoryPanelView   _inventoryPanel;
    [SerializeField] private PlayerRelicInventory _inventory;
    [SerializeField] private ShopData             _shopData;

    [Tooltip("랜덤박스 구매 시 사용할 보상 풀. 없으면 랜덤박스 효과 미발동")]
    [SerializeField] private RewardPool           _rewardPool;

    [SerializeField] private int _startGold = 300;

    private int _gold;

    private void Start()
    {
        if (_shopPanel == null)
        {
            Debug.LogError("[TestShop] _shopPanel이 null입니다. 메뉴 LostMemory > Shop > Build Test Scene을 실행하세요.");
            return;
        }
        if (_inventoryPanel == null)
        {
            Debug.LogError("[TestShop] _inventoryPanel이 null입니다. 메뉴 LostMemory > Shop > Build Test Scene을 실행하세요.");
            return;
        }
        if (_shopData == null)
        {
            Debug.LogError("[TestShop] _shopData가 null입니다. 메뉴 LostMemory > Shop > Build Test Scene을 실행하세요.");
            return;
        }
        if (_inventory == null)
        {
            Debug.LogError("[TestShop] _inventory가 null입니다. 메뉴 LostMemory > Shop > Build Test Scene을 실행하세요.");
            return;
        }

        _gold = _startGold;

        _shopPanel.Init(_shopData, _inventory, _gold);
        _shopPanel.OnItemPurchased += OnItemPurchased;

        _inventoryPanel.Refresh(_inventory, _gold);
    }

    private void OnItemPurchased(ShopItemData item)
    {
        _gold -= item.Price;

        // 즉시 사용 소모품(랜덤박스 등): RewardPool로 아이템 1개 즉시 지급
        if (item.Relic.IsConsumable && item.Relic.IsInstantUse)
            TriggerInstantConsumable(item.Relic);

        _inventoryPanel.Refresh(_inventory, _gold);
        _shopPanel.UpdateGold(_gold);
        Debug.Log($"[TestShop] {item.Relic.DisplayName} 구매 완료 / 잔여 골드: {_gold}");
    }

    /// <summary>
    /// 즉시 사용 소모품 효과를 발동한다.
    /// 현재는 랜덤박스 전용 — RewardPool에서 1개를 추첨해 인벤토리에 추가.
    /// </summary>
    private void TriggerInstantConsumable(RelicData consumable)
    {
        if (_rewardPool == null)
        {
            Debug.LogWarning($"[TestShop] RewardPool이 없어 '{consumable.DisplayName}' 효과 미발동." +
                             " TestShop의 _rewardPool 필드에 RewardPool 에셋을 연결하세요.");
            return;
        }

        var rewarded = _rewardPool.DrawOne(_inventory.GetOwnedNames());
        if (rewarded == null)
        {
            Debug.Log("[TestShop] 랜덤박스: 추첨 가능한 아이템 없음");
            return;
        }

        _inventory.TryAdd(rewarded);
        Debug.Log($"[TestShop] 랜덤박스 결과 → {rewarded.DisplayName}");
    }
}

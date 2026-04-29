using System.Collections.Generic;
using LostMemory.Relics;
using LostMemory.Rewards;
using LostMemory.Shop;
using UnityEngine;

/// <summary>상점 UI 테스트용 스크립트</summary>
public class TestShop : MonoBehaviour
{
    [SerializeField] private ShopPanelView             _shopPanel;
    [SerializeField] private InventoryPanelView        _inventoryPanel;
    [SerializeField] private ShortcutBarView           _shortcutBar;
    [SerializeField] private PlayerRelicInventory      _inventory;
    [SerializeField] private PlayerConsumableInventory _consumableInventory;

    [Header("상점 구성")]
    [Tooltip("ShopConfig가 있으면 동적 생성, 없으면 아래 ShopData를 고정 사용")]
    [SerializeField] private ShopConfig _shopConfig;
    [SerializeField] private RewardPool _rewardPool;

    [Tooltip("ShopConfig가 없을 때 사용할 고정 상점 데이터 (레거시)")]
    [SerializeField] private ShopData   _shopData;

    [SerializeField] private int _startGold = 300;

    private int      _gold;
    private ShopData _activeShopData;   // 현재 상점에서 사용 중인 ShopData

    private void Start()
    {
        if (_shopPanel == null)
        {
            Debug.LogError("[TestShop] _shopPanel이 null입니다.");
            return;
        }
        if (_inventoryPanel == null)
        {
            Debug.LogError("[TestShop] _inventoryPanel이 null입니다.");
            return;
        }
        if (_inventory == null)
        {
            Debug.LogError("[TestShop] _inventory가 null입니다.");
            return;
        }
        if (_consumableInventory == null)
            Debug.LogWarning("[TestShop] _consumableInventory가 null입니다. 소모품이 단축키바에 추가되지 않습니다.");
        if (_shortcutBar == null)
            Debug.LogWarning("[TestShop] _shortcutBar가 null입니다. LostMemory > Shop > Sync Shortcut Bar를 실행하세요.");

        _gold = _startGold;

        // ── 상점 데이터 준비 ────────────────────────────────────────
        _activeShopData = GenerateOrFallbackShopData();
        if (_activeShopData == null)
        {
            Debug.LogError("[TestShop] ShopData를 준비할 수 없습니다. ShopConfig 또는 ShopData를 연결하세요.");
            return;
        }

        _shopPanel.Init(_activeShopData, _inventory, _gold);
        _shopPanel.OnItemPurchased += OnItemPurchased;

        _inventoryPanel.Init(_inventory, _gold);

        if (_shortcutBar != null && _consumableInventory != null)
            _shortcutBar.Init(_consumableInventory);
    }

    // ── 상점 데이터 생성 ────────────────────────────────────────────

    /// <summary>ShopConfig + RewardPool이 있으면 동적 생성, 없으면 고정 ShopData 반환</summary>
    private ShopData GenerateOrFallbackShopData()
    {
        if (_shopConfig != null && _rewardPool != null)
        {
            Debug.Log("[TestShop] 상점 데이터 동적 생성");
            return ShopGenerator.Generate(_inventory, _rewardPool, _shopConfig);
        }

        if (_shopData != null)
        {
            Debug.Log("[TestShop] 고정 ShopData 사용 (레거시)");
            return _shopData;
        }

        return null;
    }

    // ── 구매 처리 ───────────────────────────────────────────────────

    private void OnItemPurchased(ShopItemData item)
    {
        _gold -= item.Price;

        if (item.Relic.IsConsumable && item.Relic.IsInstantUse)
        {
            // 즉시 사용 소모품(랜덤박스): 결과물을 라우팅
            TriggerInstantConsumable(item.Relic);
        }
        else if (item.Relic.IsConsumable)
        {
            // 일반 소모품(포션) → 단축키바
            if (_consumableInventory != null)
            {
                _consumableInventory.TryAdd(item.Relic);
                _shortcutBar?.Refresh();
            }
        }
        // 일반 유물은 ShopPanelView.TryBuy()에서 이미 _inventory.TryAdd() 완료

        _inventoryPanel.Refresh(_inventory, _gold);
        _shopPanel.UpdateGold(_gold);
        Debug.Log($"[TestShop] {item.Relic.DisplayName} 구매 완료 / 잔여 골드: {_gold}");
    }

    // ── 랜덤박스 처리 ───────────────────────────────────────────────

    /// <summary>
    /// 랜덤박스 효과 발동.
    /// ShopConfig의 등급 가중치(55/30/12/3)로 미보유 유물 1개를 추첨한다.
    /// 후보가 없으면 ShopConfig.RandomBoxFallback(큰 회복약)을 지급한다.
    /// </summary>
    private void TriggerInstantConsumable(RelicData consumable)
    {
        if (_rewardPool == null)
        {
            Debug.LogWarning($"[TestShop] RewardPool이 없어 '{consumable.DisplayName}' 효과 미발동.");
            return;
        }

        RelicData rewarded = DrawRandomBoxResult();

        if (rewarded == null)
        {
            Debug.Log("[TestShop] 랜덤박스: 추첨 가능한 아이템 없음");
            return;
        }

        // 결과물 라우팅: 소모품 → 단축키바, 유물 → 인벤토리
        if (rewarded.IsConsumable && !rewarded.IsInstantUse && _consumableInventory != null)
        {
            _consumableInventory.TryAdd(rewarded);
            _shortcutBar?.Refresh();
            Debug.Log($"[TestShop] 랜덤박스 결과 → 단축키바: {rewarded.DisplayName}");
        }
        else
        {
            _inventory.TryAdd(rewarded);
            Debug.Log($"[TestShop] 랜덤박스 결과 → 인벤토리: {rewarded.DisplayName}");
        }
    }

    private RelicData DrawRandomBoxResult()
    {
        if (_shopConfig != null)
        {
            // 기획의 등급 확률 테이블 사용 (55/30/12/3)
            var rarityWeights = new Dictionary<RelicRarity, int>
            {
                { RelicRarity.Common,    _shopConfig.RandomBoxCommonWeight    },
                { RelicRarity.Rare,      _shopConfig.RandomBoxRareWeight      },
                { RelicRarity.Unique,    _shopConfig.RandomBoxUniqueWeight    },
                { RelicRarity.Legendary, _shopConfig.RandomBoxLegendaryWeight },
            };

            var rewarded = _rewardPool.DrawOneRelicForShop(_inventory.GetOwnedNames(), rarityWeights);

            // 후보 없으면 폴백 (큰 회복약)
            if (rewarded == null && _shopConfig.RandomBoxFallback != null)
            {
                Debug.Log("[TestShop] 랜덤박스: 후보 유물 없음 → 폴백 지급");
                return _shopConfig.RandomBoxFallback;
            }

            return rewarded;
        }

        // ShopConfig 없으면 기존 RewardPool.DrawOne() 사용
        return _rewardPool.DrawOne(_inventory.GetOwnedNames());
    }
}

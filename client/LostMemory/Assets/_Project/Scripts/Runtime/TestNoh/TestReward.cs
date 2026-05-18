using LostMemory.Relics;
using LostMemory.Rewards;
using UnityEngine;

/// <summary>보상 3택 UI 테스트용 스크립트</summary>
public class TestReward : MonoBehaviour
{
    [SerializeField] private RewardPanelView _panel;

    private void Start()
    {
        // 인벤토리 없이 빈 목록으로 테스트
        var tempInventory = gameObject.AddComponent<PlayerRelicInventory>();
        _panel.Show(tempInventory);
    }
}

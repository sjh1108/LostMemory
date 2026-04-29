using LostMemory.UI;
using UnityEngine;

/// <summary>런 결산 창 테스트용 스크립트</summary>
public class TestRunResult : MonoBehaviour
{
    [SerializeField] private RunResultPanelView _panel;

    private void Start()
    {
        _panel.Show(new RunResultData
        {
            KillCount       = 63,
            BossKillCount   = 2,
            TotalDamage     = 4138401,
            PlayTime        = 754f, // 12:34
            MemoryFragments = 24
        });
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// 런 결산 창 UI를 담당하는 뷰 컴포넌트.
    /// Show()를 호출하면 RunResultData를 받아 각 텍스트를 채운다.
    /// </summary>
    public class RunResultPanelView : MonoBehaviour
    {
        [Header("스탯")]
        [SerializeField] private TextMeshProUGUI _killCountText;
        [SerializeField] private TextMeshProUGUI _bossKillCountText;
        [SerializeField] private TextMeshProUGUI _totalDamageText;
        [SerializeField] private TextMeshProUGUI _playTimeText;
        [SerializeField] private TextMeshProUGUI _memoryFragmentsText;

        [Header("버튼")]
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _lobbyButton;

        public event Action OnRestart;
        public event Action OnLobby;

        private void Awake()
        {
            _restartButton.onClick.AddListener(() => OnRestart?.Invoke());
            _lobbyButton.onClick.AddListener(() => OnLobby?.Invoke());
        }

        /// <summary>결산 창을 열고 데이터를 표시한다.</summary>
        public void Show(RunResultData data)
        {
            _killCountText.text       = data.KillCount.ToString();
            _bossKillCountText.text   = data.BossKillCount.ToString();
            _totalDamageText.text     = data.TotalDamage.ToString("N0");
            _playTimeText.text        = FormatTime(data.PlayTime);
            _memoryFragmentsText.text = data.MemoryFragments.ToString();

            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private static string FormatTime(float seconds)
        {
            int m = (int)seconds / 60;
            int s = (int)seconds % 60;
            return $"{m:00}:{s:00}";
        }
    }
}

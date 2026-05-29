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

        [Header("Auto Return")]
        [Tooltip("4명 사망 자동 마을 복귀 모드 — Restart/Lobby 버튼 자동 hide. " +
                 "RunManager.AutoReturnAfterResult 가 timeout 후 자동 ReturnToTown 호출. " +
                 "false 로 두면 기존 버튼 클릭 흐름 복구.")]
        [SerializeField] private bool hideButtonsForAutoReturn = true;

        public event Action OnRestart;
        public event Action OnLobby;

        private void Awake()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(() => OnRestart?.Invoke());
                // [Auto Return] 자동 마을 복귀 모드 — 버튼 hide.
                // listener 는 등록 그대로 (토글 OFF 시 즉시 부활 가능).
                if (hideButtonsForAutoReturn) _restartButton.gameObject.SetActive(false);
            }
            if (_lobbyButton != null)
            {
                _lobbyButton.onClick.AddListener(() => OnLobby?.Invoke());
                if (hideButtonsForAutoReturn) _lobbyButton.gameObject.SetActive(false);
            }
        }

        /// <summary>결산 창을 열고 데이터를 표시한다.</summary>
        public void Show(RunResultData data)
        {
            // 다른 UI 패널에 가려지지 않도록 sibling 순서를 맨 뒤로 (= 최상위 렌더링).
            transform.SetAsLastSibling();

            // 어떤 해상도에서도 화면 전체를 덮도록 RectTransform 을 강제 stretch.
            // (prefab 인스턴스 wiring 사고나 부모 leftover offset 으로 사이즈가 어긋나는 경우 방어.)
            if (transform is RectTransform rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.localScale = Vector3.one;
            }

            SetText(_killCountText, data.KillCount.ToString());
            SetText(_bossKillCountText, data.BossKillCount.ToString());
            SetText(_totalDamageText, data.TotalDamage.ToString("N0"));
            SetText(_playTimeText, FormatTime(data.PlayTime));
            SetText(_memoryFragmentsText, data.MemoryFragments.ToString());

            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private static string FormatTime(float seconds)
        {
            int m = (int)seconds / 60;
            int s = (int)seconds % 60;
            return $"{m:00}:{s:00}";
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}

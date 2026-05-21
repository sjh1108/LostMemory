using LostMemory.Stage;
using TMPro;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// CL-234 (A-12): 던전 진행 중 플레이 시간을 HUD 에 실시간 표시.
    ///
    /// 동작:
    ///   - <see cref="RunManager.Instance"/> 의 <see cref="RunManager.ElapsedRunTime"/> 을 매 프레임 폴링.
    ///   - InRun 상태가 아니면 텍스트 숨김 (gameObject 비활성 또는 빈 문자열).
    ///
    /// 권장 부착 위치: 미니맵 패널 prefab 안의 자식 TMP_Text (예: <c>MinimapPanel/BottomInfo/Timer</c>).
    /// B-15 (스테이지/라운드 표기) 와 형제 노드로 공존.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Run Timer Hud Presenter")]
    public sealed class RunTimerHudPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _timerText;

        [Tooltip("InRun 이 아닐 때 표시 동작. true 이면 빈 문자열, false 이면 GameObject 비활성.")]
        [SerializeField] private bool _keepActiveWhenIdle = true;

        [Tooltip("MM:SS 외에 100분의 1초(.ff) 까지 표시. 스피드런용.")]
        [SerializeField] private bool _showCentiseconds = false;

        private void Reset()
        {
            _timerText = GetComponent<TextMeshProUGUI>();
        }

        private void Awake()
        {
            if (_timerText == null) _timerText = GetComponent<TextMeshProUGUI>();
        }

        private void Update()
        {
            RunManager rm = RunManager.Instance;
            if (rm == null)
            {
                ApplyIdle();
                return;
            }

            float elapsed = rm.ElapsedRunTime;
            if (elapsed <= 0f)
            {
                ApplyIdle();
                return;
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (_timerText != null) _timerText.text = FormatTime(elapsed);
        }

        private void ApplyIdle()
        {
            if (_timerText != null)
            {
                _timerText.text = string.Empty;
            }
            if (!_keepActiveWhenIdle && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private string FormatTime(float seconds)
        {
            int m = (int)seconds / 60;
            int s = (int)seconds % 60;
            if (_showCentiseconds)
            {
                int cs = (int)((seconds - (int)seconds) * 100f);
                return $"{m:00}:{s:00}.{cs:00}";
            }
            return $"{m:00}:{s:00}";
        }
    }
}

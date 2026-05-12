using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// 상호작용 안내(F 키) prompt 의 두 비주얼 상태를 일정 간격으로 번갈아 표시.
    ///
    /// 사용 패턴:
    ///   - 부모 GameObject 에 본 컴포넌트 부착.
    ///   - 자식으로 stateA / stateB GameObject 두 개 두고 각각 SpriteRenderer 배치
    ///     (예: stateA = "F 키 떠있는 그래픽", stateB = "F 키 눌린 그래픽").
    ///   - 부모는 ShopNpcInteractable.promptObject 슬롯에 연결 — 트리거 진입/이탈 시
    ///     부모가 SetActive 토글되므로, OnEnable 에서 자동으로 초기 상태로 리셋된다.
    ///
    /// 책임:
    ///   1. OnEnable 시 startWithA 에 따라 초기 상태로 리셋 (재진입마다 같은 시작점 보장)
    ///   2. Update 에서 interval 마다 stateA ↔ stateB SetActive 토글
    ///
    /// Time.unscaledDeltaTime 사용 — 향후 timeScale 변경 시에도 깜빡임 유지.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Shop/Prompt Alternator")]
    public sealed class PromptAlternator : MonoBehaviour
    {
        [Header("States")]
        [Tooltip("초기 상태(startWithA=true) 시 활성화될 GameObject. 예: F 키 안 눌린 그래픽.")]
        [SerializeField] private GameObject stateA;
        [Tooltip("교대 시 활성화될 GameObject. 예: F 키 눌린 그래픽.")]
        [SerializeField] private GameObject stateB;

        [Header("Config")]
        [Tooltip("상태 전환 간격(초). 0.6~1.2 권장. 너무 짧으면 산만함.")]
        [SerializeField, Min(0.05f)] private float interval = 0.8f;

        [Tooltip("OnEnable 시 stateA 로 시작. false 면 stateB 로 시작.")]
        [SerializeField] private bool startWithA = true;

        private float _timer;
        private bool _showingA;

        private void OnEnable()
        {
            // 재진입마다 동일한 시작점 보장 — 코루틴/타이머 잔여 상태 리셋.
            _showingA = startWithA;
            _timer = 0f;
            Apply();
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer >= interval)
            {
                _timer -= interval;
                _showingA = !_showingA;
                Apply();
            }
        }

        private void Apply()
        {
            if (stateA != null) stateA.SetActive(_showingA);
            if (stateB != null) stateB.SetActive(!_showingA);
        }
    }
}

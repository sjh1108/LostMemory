using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// CL-234 (A-1/A-2): 인게임 입력(공격·대시·패리·텔레포트·스킬 등)이 차단되어야 하는
    /// UI 패널(인벤토리·상점·보상창·일시정지·재능 등) 의 열림 상태를 정적 카운터로 추적.
    ///
    /// EventSystem.IsPointerOverGameObject() 는 마우스 포인터 위치 기반이라
    /// Space 대시·Q 스킬 같은 키보드 입력은 차단 못 함. 본 블로커가 보완.
    ///
    /// 사용 방법:
    ///   - 패널 GameObject 에 <see cref="UIInputBlockerSource"/> 부착 → OnEnable/Disable
    ///     훅으로 자동 Acquire/Release.
    ///   - 또는 코드에서 직접 Acquire/Release 짝맞춰 호출.
    ///
    /// 입력 핸들러는 진입부에서 <see cref="IsBlocked"/> 체크.
    /// </summary>
    public static class UIInputBlocker
    {
        private static int _blockerCount;

        /// <summary>현재 차단 카운트(0 = 차단 없음).</summary>
        public static int BlockerCount => _blockerCount;

        /// <summary>활성 차단 source 가 하나라도 있으면 true.</summary>
        public static bool IsBlocked => _blockerCount > 0;

        /// <summary>차단 source 등록. UI 패널 OnEnable 에서 호출.</summary>
        public static void Acquire()
        {
            _blockerCount++;
        }

        /// <summary>차단 source 해제. UI 패널 OnDisable 에서 호출.</summary>
        public static void Release()
        {
            _blockerCount = Mathf.Max(0, _blockerCount - 1);
        }

        /// <summary>씬 전환·런 재시작 시 강제 초기화. 카운터 불일치 안전망.</summary>
        public static void Reset()
        {
            _blockerCount = 0;
        }
    }
}

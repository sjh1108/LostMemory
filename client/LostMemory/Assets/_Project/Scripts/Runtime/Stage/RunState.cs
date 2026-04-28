namespace LostMemory.Stage
{
    /// <summary>
    /// CL-047 표 기반 런 진행 상태.
    /// 본 enum 은 *메인 상태* 만 표현. 현재 방 종류 (Combat/Shop/Event/Boss) 는
    /// <see cref="StageRoomType"/> 으로 분리되어 있으며, RunManager 가 별도 노출한다.
    ///
    /// CL-047 v1 (분리안: InRun_Combat / InRun_Bridge / InRun_Boss) 대비 v1.1 단일화.
    /// 단일화 근거:
    ///   - StageRoomType 에 Bridge 가 존재하지 않음 (Unknown/Combat/Shop/Event/Boss)
    ///   - 방 종류는 sub-info 로 충분, 메인 상태 분리 시 곱집합 폭발 우려 (멀티 시 InRun_Combat_MemberDown 등)
    ///   - 외부 트리거 기반 storage 패턴에 단일화가 정합
    /// </summary>
    public enum RunState
    {
        /// <summary>런 시작 전 / 메인 메뉴.</summary>
        None,

        /// <summary>시드 결정 + DA Build 진행 중.</summary>
        Initializing,

        /// <summary>런 진행 중. 현재 방 종류는 RunManager.CurrentRoomType 으로 조회.</summary>
        InRun,

        /// <summary>보스 처치 → 런 성공.</summary>
        RunCleared,

        /// <summary>모든 멤버 사망 → 런 실패. (솔로 = Player Defeated 즉시)</summary>
        RunFailed,

        /// <summary>결과 화면 표시 중.</summary>
        Resulting,
    }
}

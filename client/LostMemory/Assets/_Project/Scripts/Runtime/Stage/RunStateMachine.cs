using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Stage
{
    /// <summary>
    /// CL-048 런 상태 머신 — Storage 패턴 (BossRoomDoorController 스타일).
    ///
    /// 책임:
    ///   1. 현재 RunState 보유 (storage)
    ///   2. <see cref="TryTransition"/> 호출 시 가드 매트릭스 검사 → 통과 시 storage 변경 + StateChanged 발행
    ///   3. 잘못된 전이는 reject + 경고 로그 (transition lockout invariant 강제)
    ///
    /// 멀티 도입 시 *RPC 진입점 = TryTransition()* 한 곳. RunManager.IsAuthority 게이트와 결합.
    ///
    /// 가드 매트릭스 출처: <c>docs/khi/cl047_run_state_transition_table_plan.md</c> 섹션 2.
    /// </summary>
    public sealed class RunStateMachine
    {
        public RunState Current { get; private set; } = RunState.None;

        /// <summary>
        /// 상태 전이 발생 시 발행. 인자: (prev, current).
        /// 가드 매트릭스 통과 후, storage 변경 *직후* 발행되므로 핸들러는 최신 상태 참조 가능.
        /// </summary>
        public event Action<RunState, RunState> StateChanged;

        /// <summary>현재 상태가 *런 진행 중* (Initializing 또는 InRun) 인지.</summary>
        public bool IsInProgress => Current == RunState.Initializing || Current == RunState.InRun;

        /// <summary>현재 상태가 *종료 단계* (RunCleared / RunFailed / Resulting) 인지.</summary>
        public bool IsRunOver => Current == RunState.RunCleared
                              || Current == RunState.RunFailed
                              || Current == RunState.Resulting;

        // CL-047 표 기반 가드 매트릭스. (from, to) 쌍이 여기 있어야 전이 허용.
        private static readonly HashSet<(RunState, RunState)> ValidTransitions = new()
        {
            // None → Initializing (런 시작 버튼)
            (RunState.None, RunState.Initializing),

            // Initializing → InRun (DungeonBuilt 콜백)
            (RunState.Initializing, RunState.InRun),
            // Initializing → RunFailed (DA Build 실패 시 — 예외 처리, 후속 CL 에서 트리거 정의)
            (RunState.Initializing, RunState.RunFailed),

            // InRun -> Initializing (boss clear portal -> next stage build)
            (RunState.InRun, RunState.Initializing),

            // InRun → RunCleared (last stage boss clear portal)
            (RunState.InRun, RunState.RunCleared),
            // InRun → RunFailed (Player Defeated)
            (RunState.InRun, RunState.RunFailed),

            // RunCleared/RunFailed → Resulting (delay 후)
            (RunState.RunCleared, RunState.Resulting),
            (RunState.RunFailed, RunState.Resulting),

            // Resulting → None (메인 메뉴 복귀 / 다시하기)
            (RunState.Resulting, RunState.None),

            // ESC 메뉴/씬 이탈처럼 결과 정산 없이 런을 포기하는 귀환.
            (RunState.Initializing, RunState.None),
            (RunState.InRun, RunState.None),
            (RunState.RunCleared, RunState.None),
            (RunState.RunFailed, RunState.None),
        };

        /// <summary>
        /// 전이 시도. 가드 매트릭스 통과 시 storage 변경 + StateChanged 발행 후 true.
        /// 무효 전이는 false 반환 + 경고 로그 (transition lockout 등 invariant 강제).
        /// </summary>
        public bool TryTransition(RunState to)
        {
            if (Current == to)
            {
                // 자기 자신으로의 전이는 무시 (no-op). 경고 로그도 안 남김.
                return false;
            }

            if (!ValidTransitions.Contains((Current, to)))
            {
                Debug.LogWarning($"[RunStateMachine] Invalid transition: {Current} -> {to}");
                return false;
            }

            RunState prev = Current;
            Current = to;
            StateChanged?.Invoke(prev, Current);
            return true;
        }

        /// <summary>특정 전이가 유효한지 *조회만*. 실제 전이는 발생하지 않음.</summary>
        public bool CanTransition(RunState from, RunState to)
        {
            return ValidTransitions.Contains((from, to));
        }

        /// <summary>현재 상태에서 *유효한 다음 상태들* 반환. 디버그 / UI 용.</summary>
        public IEnumerable<RunState> GetValidNextStates()
        {
            foreach (var (from, to) in ValidTransitions)
            {
                if (from == Current)
                {
                    yield return to;
                }
            }
        }
    }
}

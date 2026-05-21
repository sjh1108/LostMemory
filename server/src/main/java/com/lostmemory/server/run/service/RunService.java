package com.lostmemory.server.run.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.run.dto.EndRunRequest;
import com.lostmemory.server.run.dto.RunDetailResponse;
import com.lostmemory.server.run.dto.RunResponse;
import com.lostmemory.server.run.dto.RunResultResponse;
import com.lostmemory.server.run.entity.Run;
import com.lostmemory.server.run.entity.RunMember;
import com.lostmemory.server.run.entity.RunResult;
import com.lostmemory.server.run.entity.RunResultStatus;
import com.lostmemory.server.run.entity.RunStatus;
import com.lostmemory.server.run.repository.RunMemberRepository;
import com.lostmemory.server.run.repository.RunRepository;
import com.lostmemory.server.run.repository.RunResultRepository;
import com.lostmemory.server.session.entity.Session;
import com.lostmemory.server.session.entity.SessionJoin;
import com.lostmemory.server.session.repository.SessionJoinRepository;
import com.lostmemory.server.session.repository.SessionRepository;
import com.lostmemory.server.user.entity.User;
import com.lostmemory.server.user.repository.UserCurrencyRepository;
import com.lostmemory.server.user.repository.UserRecordRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class RunService {

    private final RunRepository runRepository;
    private final RunMemberRepository runMemberRepository;
    private final RunResultRepository runResultRepository;
    private final SessionRepository sessionRepository;
    private final SessionJoinRepository sessionJoinRepository;
    private final UserCurrencyRepository userCurrencyRepository;
    private final UserRecordRepository userRecordRepository;

    /**
     * 런 시작 — 호스트만 호출.
     *  - session 호스트 검증
     *  - 같은 session 의 진행 중 런 중복 방지
     *  - session_joins 의 모든 멤버를 RunMember 로 등록
     */
    @Transactional
    public RunResponse startRun(Long userId, Long sessionId) {
        Session session = sessionRepository.findById(sessionId)
                .orElseThrow(() -> new BusinessException(ErrorCode.SESSION_NOT_FOUND));

        if (!session.getHost().getId().equals(userId)) {
            throw new BusinessException(ErrorCode.RUN_NOT_HOST);
        }

        if (runRepository.existsBySessionIdAndStatus(sessionId, RunStatus.PROGRESS)) {
            throw new BusinessException(ErrorCode.RUN_ALREADY_IN_PROGRESS);
        }

        Run run = runRepository.save(Run.start(session));

        List<SessionJoin> joins = sessionJoinRepository.findAllBySessionIdWithUser(sessionId);
        for (SessionJoin join : joins) {
            runMemberRepository.save(RunMember.of(run, join.getUser()));
        }

        return RunResponse.from(run);
    }

    /**
     * 런 종료 — 호스트만 호출. 트랜잭션 안에서 4 가지 변경:
     *  1. runs.status = END + ended_at = now
     *  2. run_results insert (3 필드 + result 추론값)
     *  3. 모든 멤버의 user_currencies.memory_shards += earned
     *  4. 모든 멤버의 user_record.cleared_chapter = max(기존, chapter_reached)
     *
     * result 는 클라가 안 보내고 chapter_reached 로 추론 — 1챕터=1보스 정책 (클라 합의).
     *   chapterReached > 0 → CLEAR, 0 → DEATH. surrender 는 추후 신호 들어오면 분기 추가.
     *
     * idempotency: 이미 종료된 run 에 대한 재호출은 기존 result 반환 (클라 네트워크 retry 안전).
     */
    @Transactional
    public RunResultResponse endRun(Long userId, Long runId, EndRunRequest request) {
        Run run = runRepository.findById(runId)
                .orElseThrow(() -> new BusinessException(ErrorCode.RUN_NOT_FOUND));

        if (!run.getSession().getHost().getId().equals(userId)) {
            throw new BusinessException(ErrorCode.RUN_NOT_HOST);
        }

        if (run.isEnded()) {
            // idempotent — 기존 result 그대로 반환. 보상 재적립 안 함 (이미 처리됨).
            return runResultRepository.findById(runId)
                    .map(RunResultResponse::from)
                    .orElseThrow(() -> new BusinessException(ErrorCode.RUN_NOT_FOUND));
        }

        run.markEnded();

        RunResultStatus inferredResult = request.chapterReached() > 0
                ? RunResultStatus.CLEAR
                : RunResultStatus.DEATH;

        RunResult result = runResultRepository.save(RunResult.of(
                run,
                inferredResult,
                request.durationSeconds(),
                request.chapterReached(),
                request.memoryShardsEarned()
        ));

        // 모든 멤버에게 동일 양 파편 적립 + 최고 전적 갱신
        List<RunMember> members = runMemberRepository.findAllByRunIdWithUser(runId);
        for (RunMember member : members) {
            applyRewards(member.getUser(), request.memoryShardsEarned(), request.chapterReached());
        }

        return RunResultResponse.from(result);
    }

    /**
     * 런 단건 조회 — RunMember (본인이 참여한 런) 만 허용.
     * 응답 (RunDetailResponse) 은 런 공통 결과 (run + run_results 4 필드) 만 노출하고
     * run_member 의 멤버별 private 데이터 (final_hp / final_gold / JSONB 컬럼) 는 미포함이라
     * 게스트가 호스트 결과 조회 = 공유된 4 필드만 보는 케이스. MVP 정책으로 RunMember 면 OK.
     * 결과 row 가 없으면 (진행 중) null.
     */
    public RunDetailResponse getRun(Long userId, Long runId) {
        Run run = runRepository.findById(runId)
                .orElseThrow(() -> new BusinessException(ErrorCode.RUN_NOT_FOUND));

        boolean isMember = runMemberRepository.findAllByRunIdWithUser(runId).stream()
                .anyMatch(m -> m.getUser().getId().equals(userId));
        if (!isMember) {
            throw new BusinessException(ErrorCode.RUN_NOT_MEMBER);
        }

        RunResultResponse resultResponse = runResultRepository.findById(runId)
                .map(RunResultResponse::from)
                .orElse(null);

        return RunDetailResponse.of(RunResponse.from(run), resultResponse);
    }

    /**
     * 멤버 한 명에게 파편 적립 + 전적 갱신.
     * PostgreSQL UPSERT (`INSERT ... ON CONFLICT DO UPDATE`) 로 원자 처리 — lost-update / PK 충돌 race 방지.
     * SELECT-modify-WRITE 패턴 제거로 멤버 간 동시 endRun 도 안전.
     */
    private void applyRewards(User user, int earnedShards, int chapterReached) {
        userCurrencyRepository.upsertShards(user.getId(), earnedShards);
        userRecordRepository.upsertRecord(user.getId(), chapterReached, 0);
    }
}

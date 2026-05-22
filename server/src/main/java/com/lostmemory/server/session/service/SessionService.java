package com.lostmemory.server.session.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.global.security.JwtProperties;
import com.lostmemory.server.global.security.JwtProvider;
import com.lostmemory.server.session.dto.CreateSessionRequest;
import com.lostmemory.server.session.dto.JoinSessionRequest;
import com.lostmemory.server.session.dto.SessionFindResponse;
import com.lostmemory.server.session.dto.SessionMemberResponse;
import com.lostmemory.server.session.dto.SessionResponse;
import com.lostmemory.server.session.entity.Session;
import com.lostmemory.server.session.entity.SessionJoin;
import com.lostmemory.server.session.entity.SessionRole;
import com.lostmemory.server.session.repository.SessionJoinRepository;
import com.lostmemory.server.session.repository.SessionRepository;
import com.lostmemory.server.user.entity.User;
import com.lostmemory.server.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class SessionService {

    private final SessionRepository sessionRepository;
    private final SessionJoinRepository sessionJoinRepository;
    private final UserRepository userRepository;
    private final JwtProvider jwtProvider;
    private final JwtProperties jwtProperties;

    /**
     * 세션 생성: 호스트가 새 매칭룸을 연다.
     * - private_code 중복 검증 (열린 세션끼리만, MVP 는 단순화해 전체 검색)
     * - sessions row + session_joins(HOST) row 동시 생성
     * - 호스트 sessionToken 발급
     */
    @Transactional
    public SessionResponse createSession(Long userId, CreateSessionRequest request) {
        User host = userRepository.findById(userId)
                .orElseThrow(() -> new BusinessException(ErrorCode.USER_NOT_FOUND));

        // 이전 세션 멤버십이 잔존(좀비)하면 정리 후 진행 — 비정상 종료(크래시·네트워크 단절·
        // refresh 만료)로 DELETE/leave 호출이 누락된 케이스 복구. 게임 흐름상 한 유저는 동시에
        // 한 세션에만 속하므로, 충돌 = 잔존 좀비로 간주하고 정리한다 (2차 방어).
        if (sessionJoinRepository.existsByUserId(userId)) {
            cleanupStaleMembership(userId);
        }

        if (sessionRepository.existsByPrivateCode(request.privateCode())) {
            throw new BusinessException(ErrorCode.SESSION_PRIVATE_CODE_DUPLICATED);
        }

        Session session = sessionRepository.save(
                Session.create(host, request.maxPlayers(), request.privateCode())
        );
        sessionJoinRepository.save(SessionJoin.of(session, host, SessionRole.HOST));

        return buildResponse(session, host.getId(), SessionRole.HOST);
    }

    /**
     * 세션 참가: 게스트가 입장 코드로 join.
     * - 비관적 락으로 정원·중복 race 차단
     * - private_code 일치 검증
     * - 정원 초과 / 중복 참가 방어
     * - session_joins(GUEST) insert + sessionToken 발급
     */
    @Transactional
    public SessionResponse joinSession(Long userId, Long sessionId, JoinSessionRequest request) {
        User user = userRepository.findById(userId)
                .orElseThrow(() -> new BusinessException(ErrorCode.USER_NOT_FOUND));

        // 이전 세션 멤버십이 잔존(좀비)하면 정리 후 진행 — 비정상 종료로 DELETE/leave 가 누락된
        // 케이스 복구. 한 유저 = 한 세션 원칙이므로 충돌 = 잔존 좀비로 간주하고 정리한다 (2차 방어).
        if (sessionJoinRepository.existsByUserId(userId)) {
            cleanupStaleMembership(userId);
        }

        Session session = sessionRepository.findByIdForUpdate(sessionId)
                .orElseThrow(() -> new BusinessException(ErrorCode.SESSION_NOT_FOUND));

        if (session.getPrivateCode() == null
                || !session.getPrivateCode().equals(request.privateCode())) {
            throw new BusinessException(ErrorCode.SESSION_INVALID_PRIVATE_CODE);
        }

        if (sessionJoinRepository.existsBySessionIdAndUserId(sessionId, userId)) {
            throw new BusinessException(ErrorCode.SESSION_ALREADY_JOINED);
        }

        long currentMembers = sessionJoinRepository.countBySessionId(sessionId);
        if (currentMembers >= session.getMaxPlayers()) {
            throw new BusinessException(ErrorCode.SESSION_FULL);
        }

        sessionJoinRepository.save(SessionJoin.of(session, user, SessionRole.GUEST));

        return buildResponse(session, user.getId(), SessionRole.GUEST);
    }

    /**
     * 코드로 세션 조회. 입장 전 단계 — privateCode 만 알고 sessionId 모를 때 클라가 호출.
     * 입장 안 했으니 sessionToken 발급 X. 정원·호스트 정보만 노출해 클라 UI 용.
     */
    public SessionFindResponse findByPrivateCode(String privateCode) {
        Session session = sessionRepository.findByPrivateCode(privateCode)
                .orElseThrow(() -> new BusinessException(ErrorCode.SESSION_NOT_FOUND));

        long currentMembers = sessionJoinRepository.countBySessionId(session.getId());
        return new SessionFindResponse(
                session.getId(),
                session.getHost().getId(),
                session.getMaxPlayers(),
                currentMembers,
                currentMembers >= session.getMaxPlayers()
        );
    }

    /**
     * 세션 종료: 호스트만 가능. CASCADE 로 session_joins / runs / run_member 같이 삭제됨.
     */
    @Transactional
    public void deleteSession(Long userId, Long sessionId) {
        Session session = sessionRepository.findById(sessionId)
                .orElseThrow(() -> new BusinessException(ErrorCode.SESSION_NOT_FOUND));

        if (!session.getHost().getId().equals(userId)) {
            throw new BusinessException(ErrorCode.SESSION_NOT_HOST);
        }

        sessionRepository.delete(session);
    }

    /**
     * 게스트 자발 이탈: 본인 SessionJoin row 만 삭제 (정원 카운트 회복).
     * - 호스트가 호출하면 거부 (호스트는 deleteSession 사용)
     * - 이미 떠난 상태면 idempotent 하게 no-op (재시도 안전)
     */
    @Transactional
    public void leaveSession(Long userId, Long sessionId) {
        Session session = sessionRepository.findById(sessionId)
                .orElseThrow(() -> new BusinessException(ErrorCode.SESSION_NOT_FOUND));

        if (session.getHost().getId().equals(userId)) {
            throw new BusinessException(ErrorCode.SESSION_HOST_CANNOT_LEAVE);
        }

        sessionJoinRepository
                .findBySessionIdAndUserId(sessionId, userId)
                .ifPresent(sessionJoinRepository::delete);
    }

    /**
     * lazy cleanup (좀비 세션 정리 — 2차 방어).
     * 비정상 종료로 DELETE/leave 가 누락돼 잔존한 이전 세션 멤버십을 제거한다.
     *  - 호스트였으면 세션 자체 삭제 (FK ON DELETE CASCADE 로 session_joins/runs 동반 정리)
     *  - 게스트였으면 본인 session_join 만 삭제 (정원 카운트 회복)
     * 정리 결과를 새 create/join 쿼리가 일관되게 보도록 즉시 flush 한다.
     */
    private void cleanupStaleMembership(Long userId) {
        for (SessionJoin join : sessionJoinRepository.findAllByUserId(userId)) {
            Session session = join.getSession();
            if (session.getHost().getId().equals(userId)) {
                sessionRepository.delete(session);
            } else {
                sessionJoinRepository.delete(join);
            }
        }
        sessionJoinRepository.flush();
    }

    /** 응답 조립 — 호스트/게스트 둘 다 멤버 목록 + 본인 sessionToken 을 받는다. */
    private SessionResponse buildResponse(Session session, Long callerUserId, SessionRole callerRole) {
        List<SessionMemberResponse> members = sessionJoinRepository
                .findAllBySessionIdWithUser(session.getId())
                .stream()
                .map(SessionMemberResponse::from)
                .toList();

        String sessionToken = jwtProvider.createSessionToken(
                callerUserId, session.getId(), callerRole.name()
        );

        return SessionResponse.of(
                session,
                sessionToken,
                jwtProperties.sessionExpiration(),
                members
        );
    }
}

package com.lostmemory.server.session.service;

import com.lostmemory.server.global.security.JwtProperties;
import com.lostmemory.server.global.security.JwtProvider;
import com.lostmemory.server.session.dto.CreateSessionRequest;
import com.lostmemory.server.session.dto.JoinSessionRequest;
import com.lostmemory.server.session.dto.SessionResponse;
import com.lostmemory.server.session.entity.Session;
import com.lostmemory.server.session.entity.SessionJoin;
import com.lostmemory.server.session.repository.SessionJoinRepository;
import com.lostmemory.server.session.repository.SessionRepository;
import com.lostmemory.server.user.entity.User;
import com.lostmemory.server.user.repository.UserRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.List;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

/**
 * SessionService 의 lazy cleanup (좀비 세션 정리 — 2차 방어) 분기 검증.
 *
 * 핵심: create/join 시 USER_ALREADY_IN_SESSION 충돌이 나면 throw 가 아니라
 *   잔존 멤버십을 정리(호스트→세션 삭제 / 게스트→본인 join 삭제)한 뒤 그대로 진행한다.
 */
@ExtendWith(MockitoExtension.class)
class SessionServiceTest {

    @Mock private SessionRepository sessionRepository;
    @Mock private SessionJoinRepository sessionJoinRepository;
    @Mock private UserRepository userRepository;
    @Mock private JwtProvider jwtProvider;
    @Mock private JwtProperties jwtProperties;

    @InjectMocks private SessionService sessionService;

    private static final Long USER_ID = 1L;
    private static final Long OTHER_HOST_ID = 999L;
    private static final Long NEW_SESSION_ID = 100L;
    private static final Long TARGET_SESSION_ID = 200L;
    private static final String CODE = "CODE1";

    @Test
    @DisplayName("createSession — 잔존 HOST 멤버십(좀비) 있으면 세션 자체 삭제 후 신규 생성")
    void createSession_staleHostMembership_deletesSessionThenCreates() {
        User caller = userWithId(USER_ID);
        when(userRepository.findById(USER_ID)).thenReturn(Optional.of(caller));
        when(sessionJoinRepository.existsByUserId(USER_ID)).thenReturn(true);

        // 좀비: 본인이 HOST 인 잔존 세션
        User staleHost = userWithId(USER_ID);
        Session staleSession = mock(Session.class);
        when(staleSession.getHost()).thenReturn(staleHost);
        SessionJoin staleJoin = mock(SessionJoin.class);
        when(staleJoin.getSession()).thenReturn(staleSession);
        when(sessionJoinRepository.findAllByUserId(USER_ID)).thenReturn(List.of(staleJoin));

        stubSuccessfulCreate();

        SessionResponse resp = sessionService.createSession(USER_ID, new CreateSessionRequest(2, CODE));

        // 호스트 좀비 → 세션 삭제(CASCADE), join 단독 삭제는 호출되지 않음
        verify(sessionRepository).delete(staleSession);
        verify(sessionJoinRepository, never()).delete(any(SessionJoin.class));
        verify(sessionJoinRepository).flush();
        assertThat(resp.sessionId()).isEqualTo(NEW_SESSION_ID);
    }

    @Test
    @DisplayName("createSession — 잔존 GUEST 멤버십(좀비) 있으면 본인 join 만 삭제 후 신규 생성")
    void createSession_staleGuestMembership_deletesJoinOnlyThenCreates() {
        User caller = userWithId(USER_ID);
        when(userRepository.findById(USER_ID)).thenReturn(Optional.of(caller));
        when(sessionJoinRepository.existsByUserId(USER_ID)).thenReturn(true);

        // 좀비: 남이 HOST 인 세션의 GUEST 잔존
        User staleHost = userWithId(OTHER_HOST_ID);
        Session staleSession = mock(Session.class);
        when(staleSession.getHost()).thenReturn(staleHost);
        SessionJoin staleJoin = mock(SessionJoin.class);
        when(staleJoin.getSession()).thenReturn(staleSession);
        when(sessionJoinRepository.findAllByUserId(USER_ID)).thenReturn(List.of(staleJoin));

        stubSuccessfulCreate();

        sessionService.createSession(USER_ID, new CreateSessionRequest(2, CODE));

        // 게스트 좀비 → 본인 join 만 삭제, 세션은 보존
        verify(sessionJoinRepository).delete(staleJoin);
        verify(sessionRepository, never()).delete(any(Session.class));
        verify(sessionJoinRepository).flush();
    }

    @Test
    @DisplayName("joinSession — 잔존 멤버십(좀비) 정리 후 대상 세션에 정상 참가")
    void joinSession_staleMembership_cleansUpThenJoins() {
        User caller = userWithId(USER_ID);
        when(userRepository.findById(USER_ID)).thenReturn(Optional.of(caller));
        when(sessionJoinRepository.existsByUserId(USER_ID)).thenReturn(true);

        // 좀비: 게스트 잔존
        User staleHost = userWithId(OTHER_HOST_ID);
        Session staleSession = mock(Session.class);
        when(staleSession.getHost()).thenReturn(staleHost);
        SessionJoin staleJoin = mock(SessionJoin.class);
        when(staleJoin.getSession()).thenReturn(staleSession);
        when(sessionJoinRepository.findAllByUserId(USER_ID)).thenReturn(List.of(staleJoin));

        // 대상 세션 정상 참가 흐름
        Session target = sessionWith(TARGET_SESSION_ID);
        when(sessionRepository.findByIdForUpdate(TARGET_SESSION_ID)).thenReturn(Optional.of(target));
        when(sessionJoinRepository.existsBySessionIdAndUserId(TARGET_SESSION_ID, USER_ID)).thenReturn(false);
        when(sessionJoinRepository.countBySessionId(TARGET_SESSION_ID)).thenReturn(1L);
        when(sessionJoinRepository.findAllBySessionIdWithUser(anyLong())).thenReturn(List.of());
        when(jwtProvider.createSessionToken(anyLong(), anyLong(), anyString())).thenReturn("tk");
        when(jwtProperties.sessionExpiration()).thenReturn(3600L);

        SessionResponse resp = sessionService.joinSession(
                USER_ID, TARGET_SESSION_ID, new JoinSessionRequest(CODE));

        verify(sessionJoinRepository).delete(staleJoin);
        verify(sessionJoinRepository).flush();
        verify(sessionJoinRepository).save(any(SessionJoin.class));
        assertThat(resp.sessionId()).isEqualTo(TARGET_SESSION_ID);
    }

    // ── helpers ────────────────────────────────────────────────

    /** create 충돌 정리 이후의 정상 신규 생성 흐름 stub. */
    private void stubSuccessfulCreate() {
        Session newSession = sessionWith(NEW_SESSION_ID);
        when(sessionRepository.existsByPrivateCode(CODE)).thenReturn(false);
        when(sessionRepository.save(any(Session.class))).thenReturn(newSession);
        when(sessionJoinRepository.findAllBySessionIdWithUser(anyLong())).thenReturn(List.of());
        when(jwtProvider.createSessionToken(anyLong(), anyLong(), anyString())).thenReturn("tk");
        when(jwtProperties.sessionExpiration()).thenReturn(3600L);
    }

    private User userWithId(Long id) {
        User u = mock(User.class);
        when(u.getId()).thenReturn(id);
        return u;
    }

    private Session sessionWith(Long id) {
        User host = userWithId(USER_ID);
        Session s = mock(Session.class);
        when(s.getId()).thenReturn(id);
        when(s.getHost()).thenReturn(host);
        when(s.getMaxPlayers()).thenReturn(2);
        when(s.getPrivateCode()).thenReturn(CODE);
        return s;
    }
}

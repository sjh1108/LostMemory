package com.lostmemory.server.session.repository;

import com.lostmemory.server.session.entity.SessionJoin;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;

@Repository
public interface SessionJoinRepository extends JpaRepository<SessionJoin, Long> {

    long countBySessionId(Long sessionId);

    boolean existsBySessionIdAndUserId(Long sessionId, Long userId);

    /** 유저가 (다른) active 세션에 이미 참여 중인지 — 같은 유저 동시 다중 세션 진입 차단용 */
    boolean existsByUserId(Long userId);

    /**
     * 유저의 현재 세션 멤버십(정상 흐름상 0~1개) — lazy cleanup 시 host/guest 분기에 사용.
     * 세션 행을 함께 잡아 호스트 판별 시 추가 쿼리를 피한다.
     */
    @Query("SELECT sj FROM SessionJoin sj JOIN FETCH sj.session WHERE sj.user.id = :userId")
    List<SessionJoin> findAllByUserId(@Param("userId") Long userId);

    Optional<SessionJoin> findBySessionIdAndUserId(Long sessionId, Long userId);

    /** 세션의 모든 멤버를 join_at 오름차순으로 (host 가 먼저, guest 입장 순) 반환 */
    @Query("SELECT sj FROM SessionJoin sj " +
            "JOIN FETCH sj.user " +
            "WHERE sj.session.id = :sessionId " +
            "ORDER BY sj.joinedAt ASC")
    List<SessionJoin> findAllBySessionIdWithUser(@Param("sessionId") Long sessionId);
}

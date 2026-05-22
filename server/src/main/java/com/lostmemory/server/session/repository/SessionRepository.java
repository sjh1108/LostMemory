package com.lostmemory.server.session.repository;

import com.lostmemory.server.session.entity.Session;
import jakarta.persistence.LockModeType;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.time.Instant;
import java.util.Optional;

@Repository
public interface SessionRepository extends JpaRepository<Session, Long> {

    boolean existsByPrivateCode(String privateCode);

    Optional<Session> findByPrivateCode(String privateCode);

    /**
     * join 처리 직전에 사용. 같은 세션에 여러 사람이 동시 join 할 때
     * 정원 초과 race 를 차단하기 위해 비관적 락으로 행을 잡는다.
     */
    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("SELECT s FROM Session s WHERE s.id = :id")
    Optional<Session> findByIdForUpdate(@Param("id") Long id);

    /**
     * 좀비 세션 정리 (안전망) — 클라가 비정상 종료(크래시·네트워크 단절·refresh 만료)해서
     * DELETE 가 안 나간 세션을 주기적으로 일괄 삭제.
     *
     * 대상: created_at 이 threshold 이전 AND 진행 중(progress) run 이 없는 세션.
     *   - 진행 중 run 이 있으면 = 실제 플레이 중이므로 보존 (장시간 플레이 유저 오판 방지).
     * DB FK ON DELETE CASCADE 로 session_joins / runs / run_results 도 함께 정리됨.
     */
    @Modifying
    @Query(value = """
            DELETE FROM sessions s
            WHERE s.created_at < :threshold
              AND NOT EXISTS (
                  SELECT 1 FROM runs r
                  WHERE r.session_id = s.session_id AND r.status = 'progress'
              )
            """, nativeQuery = true)
    int deleteStaleSessions(@Param("threshold") Instant threshold);
}

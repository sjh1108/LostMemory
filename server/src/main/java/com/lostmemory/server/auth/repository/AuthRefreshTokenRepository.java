package com.lostmemory.server.auth.repository;

import com.lostmemory.server.auth.entity.AuthRefreshToken;
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
public interface AuthRefreshTokenRepository extends JpaRepository<AuthRefreshToken, Long> {

    Optional<AuthRefreshToken> findByTokenHash(String tokenHash);

    /**
     * refresh rotation 진입 직전에 사용. 같은 토큰으로 동시 요청이 들어와도
     * 한 트랜잭션이 SELECT FOR UPDATE 로 행을 잡고 있는 동안 나머지는 대기 →
     * 두 쌍의 새 토큰이 동시 발급되는 race 를 막는다.
     */
    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("SELECT a FROM AuthRefreshToken a WHERE a.tokenHash = :tokenHash")
    Optional<AuthRefreshToken> findByTokenHashForUpdate(@Param("tokenHash") String tokenHash);

    /**
     * reuse 감지 시 해당 user 의 모든 active(미revoke) refresh 토큰을 한 번에 무효화.
     * 탈취된 토큰 흔적을 family 단위로 차단하기 위함(OAuth refresh rotation 권고).
     */
    @Modifying(clearAutomatically = true, flushAutomatically = true)
    @Query("UPDATE AuthRefreshToken a SET a.revokedAt = :now " +
            "WHERE a.user.id = :userId AND a.revokedAt IS NULL")
    int revokeAllActiveByUserId(@Param("userId") Long userId, @Param("now") Instant now);
}

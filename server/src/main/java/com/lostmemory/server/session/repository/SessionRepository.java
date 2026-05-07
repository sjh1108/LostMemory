package com.lostmemory.server.session.repository;

import com.lostmemory.server.session.entity.Session;
import jakarta.persistence.LockModeType;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

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
}

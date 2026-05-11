package com.lostmemory.server.user.repository;

import com.lostmemory.server.user.entity.UserCurrency;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

@Repository
public interface UserCurrencyRepository extends JpaRepository<UserCurrency, Long> {
    // PK = user_id 이므로 findById(userId) 가 곧 findByUserId

    /**
     * memory_shards 원자적 적립.
     * row 없으면 INSERT (memory_shards=delta), 있으면 UPDATE (memory_shards += delta).
     * PostgreSQL UPSERT 로 lost-update / PK 충돌 race 방지.
     */
    @Modifying
    @Query(value = "INSERT INTO user_currencies (user_id, memory_shards) " +
            "VALUES (:userId, :delta) " +
            "ON CONFLICT (user_id) DO UPDATE " +
            "SET memory_shards = user_currencies.memory_shards + EXCLUDED.memory_shards",
            nativeQuery = true)
    void upsertShards(@Param("userId") Long userId, @Param("delta") int delta);
}

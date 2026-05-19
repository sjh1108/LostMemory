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

    /**
     * memory_shards 원자적 차감 (무기/프레임 칸 해금 시 사용).
     * WHERE 절로 보유량 >= cost 일 때만 차감 — 음수 보유량 방지.
     *
     * @param cost 차감할 양 (양수)
     * @return 갱신된 row 수. 0 이면 row 없거나 보유량 부족 — 서비스에서 검증 후 BusinessException 던지기.
     */
    @Modifying
    @Query(value = "UPDATE user_currencies " +
            "SET memory_shards = memory_shards - :cost " +
            "WHERE user_id = :userId AND memory_shards >= :cost",
            nativeQuery = true)
    int subtractShards(@Param("userId") Long userId, @Param("cost") int cost);
}

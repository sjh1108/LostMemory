package com.lostmemory.server.user.repository;

import com.lostmemory.server.user.entity.UserRecord;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.Optional;

@Repository
public interface UserRecordRepository extends JpaRepository<UserRecord, Long> {

    Optional<UserRecord> findByUserId(Long userId);

    /**
     * 최고 전적 원자적 max 갱신.
     * row 없으면 INSERT (chapter/stage = 새 값), 있으면 UPDATE (GREATEST 로 max 갱신).
     * PostgreSQL UPSERT 로 lost-update / PK 충돌 race 방지.
     */
    @Modifying
    @Query(value = "INSERT INTO user_record (user_id, cleared_chapter, cleared_stage) " +
            "VALUES (:userId, :chapter, :stage) " +
            "ON CONFLICT (user_id) DO UPDATE " +
            "SET cleared_chapter = GREATEST(user_record.cleared_chapter, EXCLUDED.cleared_chapter), " +
            "    cleared_stage   = GREATEST(user_record.cleared_stage,   EXCLUDED.cleared_stage)",
            nativeQuery = true)
    void upsertRecord(@Param("userId") Long userId, @Param("chapter") int chapter, @Param("stage") int stage);
}

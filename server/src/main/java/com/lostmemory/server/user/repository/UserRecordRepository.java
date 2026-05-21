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
     * 최고 전적 원자적 갱신. chapter 우선 + 동일 chapter 면 stage 비교 (lexicographic).
     * row 없으면 INSERT (새 값), 있으면 새 값이 더 깊을 때만 UPDATE.
     * (2,3) 상태에서 (1,5) 들어오면 chapter 1 < 2 라 갱신 X — 기존 record 보존.
     * PostgreSQL UPSERT 의 WHERE 절로 lost-update / PK 충돌 race 방지.
     */
    @Modifying
    @Query(value = "INSERT INTO user_record (user_id, cleared_chapter, cleared_stage) " +
            "VALUES (:userId, :chapter, :stage) " +
            "ON CONFLICT (user_id) DO UPDATE " +
            "SET cleared_chapter = EXCLUDED.cleared_chapter, " +
            "    cleared_stage   = EXCLUDED.cleared_stage " +
            "WHERE EXCLUDED.cleared_chapter > user_record.cleared_chapter " +
            "   OR (EXCLUDED.cleared_chapter = user_record.cleared_chapter " +
            "       AND EXCLUDED.cleared_stage > user_record.cleared_stage)",
            nativeQuery = true)
    void upsertRecord(@Param("userId") Long userId, @Param("chapter") int chapter, @Param("stage") int stage);
}

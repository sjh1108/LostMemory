package com.lostmemory.server.user.repository;

import com.lostmemory.server.user.entity.UserRecord;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.Optional;

@Repository
public interface UserRecordRepository extends JpaRepository<UserRecord, Long> {

    Optional<UserRecord> findByUserId(Long userId);
}

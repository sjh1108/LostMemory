package com.lostmemory.server.run.repository;

import com.lostmemory.server.run.entity.RunMember;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface RunMemberRepository extends JpaRepository<RunMember, Long> {

    /** 런에 참여한 멤버 — User fetch join 으로 N+1 방지 */
    @Query("SELECT rm FROM RunMember rm " +
            "JOIN FETCH rm.user " +
            "WHERE rm.run.id = :runId")
    List<RunMember> findAllByRunIdWithUser(@Param("runId") Long runId);
}

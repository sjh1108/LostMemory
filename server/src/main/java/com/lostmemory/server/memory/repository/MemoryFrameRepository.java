package com.lostmemory.server.memory.repository;

import com.lostmemory.server.memory.entity.MemoryFrame;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface MemoryFrameRepository extends JpaRepository<MemoryFrame, Long> {

    /** display_order 오름차순 — 클라는 이 순서대로 UI 배치. */
    List<MemoryFrame> findAllByOrderByDisplayOrderAsc();
}

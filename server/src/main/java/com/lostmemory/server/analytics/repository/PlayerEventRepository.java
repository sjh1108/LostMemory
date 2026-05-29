package com.lostmemory.server.analytics.repository;

import com.lostmemory.server.analytics.entity.PlayerEvent;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

/**
 * player_events 영속화. saveAll 로 batch 적재.
 * 조회는 모니터링 측이 PG 에 직접 붙어 수행하므로 별도 finder 메서드 없음.
 */
@Repository
public interface PlayerEventRepository extends JpaRepository<PlayerEvent, Long> {
}

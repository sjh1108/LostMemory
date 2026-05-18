package com.lostmemory.server.run.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EntityListeners;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.MapsId;
import jakarta.persistence.OneToOne;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.OnDelete;
import org.hibernate.annotations.OnDeleteAction;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.jpa.domain.support.AuditingEntityListener;

import java.time.Instant;

/**
 * 런 종료 결과 (1:1 with Run). MVP 단계엔 4 필드만 클라가 보냄:
 *   result, duration_seconds, chapter_reached, memory_shards_earned
 * 나머지 stage_reached / bosses_defeated / rooms_cleared / enemies_killed 는 0 으로 초기화.
 */
@Entity
@Table(name = "run_results")
@EntityListeners(AuditingEntityListener.class)
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class RunResult {

    @Id
    @Column(name = "run_id")
    private Long runId;

    @OneToOne(fetch = FetchType.LAZY, optional = false)
    @MapsId
    @JoinColumn(name = "run_id")
    @OnDelete(action = OnDeleteAction.CASCADE)
    private Run run;

    @Column(nullable = false, length = 20)
    private RunResultStatus result;

    @Column(name = "duration_seconds", nullable = false)
    private Integer durationSeconds;

    @Column(name = "chapter_reached", nullable = false)
    private Integer chapterReached;

    @Column(name = "stage_reached", nullable = false)
    private Integer stageReached;

    @Column(name = "memory_shards_earned", nullable = false)
    private Integer memoryShardsEarned;

    @Column(name = "bosses_defeated", nullable = false)
    private Integer bossesDefeated;

    @Column(name = "rooms_cleared", nullable = false)
    private Integer roomsCleared;

    @Column(name = "enemies_killed", nullable = false)
    private Integer enemiesKilled;

    @CreatedDate
    @Column(name = "saved_at", nullable = false, updatable = false)
    private Instant savedAt;

    private RunResult(Run run, RunResultStatus result, Integer durationSeconds,
                      Integer chapterReached, Integer memoryShardsEarned) {
        this.run = run;
        this.result = result;
        this.durationSeconds = durationSeconds;
        this.chapterReached = chapterReached;
        this.memoryShardsEarned = memoryShardsEarned;
        // MVP 4 필드 외 나머지는 0 — schema default 와 일치
        this.stageReached = 0;
        this.bossesDefeated = 0;
        this.roomsCleared = 0;
        this.enemiesKilled = 0;
    }

    /** 런 종료 시 결과 row 생성 — RunService 가 트랜잭션 내에서 호출. */
    public static RunResult of(Run run, RunResultStatus result, Integer durationSeconds,
                                Integer chapterReached, Integer memoryShardsEarned) {
        return new RunResult(run, result, durationSeconds, chapterReached, memoryShardsEarned);
    }
}

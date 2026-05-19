package com.lostmemory.server.user.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.OneToOne;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.OnDelete;
import org.hibernate.annotations.OnDeleteAction;

import java.time.Instant;

/**
 * 유저 최고 전적. user_id 는 UNIQUE FK (1유저당 1 row). PK 는 별도 record_id.
 * updated_at 은 PostgreSQL 트리거가 자동 갱신.
 */
@Entity
@Table(name = "user_record")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserRecord {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "record_id")
    private Long id;

    @OneToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "user_id", nullable = false, unique = true)
    @OnDelete(action = OnDeleteAction.CASCADE)
    private User user;

    @Column(name = "cleared_chapter", nullable = false)
    private Integer clearedChapter;

    @Column(name = "cleared_stage", nullable = false)
    private Integer clearedStage;

    @Column(name = "updated_at", insertable = false, updatable = false)
    private Instant updatedAt;

    private UserRecord(User user, Integer clearedChapter, Integer clearedStage) {
        this.user = user;
        this.clearedChapter = clearedChapter;
        this.clearedStage = clearedStage;
    }

    /** 신규 row — 첫 런 종료 시점에 호출. */
    public static UserRecord create(User user, Integer clearedChapter, Integer clearedStage) {
        return new UserRecord(user, clearedChapter, clearedStage);
    }

    /**
     * 더 깊이 클리어한 경우만 갱신. chapter 우선, 동일 chapter 면 stage 비교.
     * (2,3) 상태에서 (1,5) 들어와도 chapter 1 < 2 라 갱신 X — 기존 record 보존.
     *
     * @return true 면 갱신됨, false 면 기존 record 가 더 깊거나 동일 — 변화 없음
     */
    public boolean upgradeIfDeeper(int chapter, int stage) {
        if (chapter > this.clearedChapter
                || (chapter == this.clearedChapter && stage > this.clearedStage)) {
            this.clearedChapter = chapter;
            this.clearedStage = stage;
            return true;
        }
        return false;
    }
}

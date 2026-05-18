package com.lostmemory.website.notice.entity;

import com.lostmemory.website.global.time.BaseTimeEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Entity
@Table(name = "notice")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Notice extends BaseTimeEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, length = 200)
    private String title;

    @Column(nullable = false, columnDefinition = "TEXT")
    private String body;

    @Column(name = "author_id")
    private Long authorId;

    @Column(nullable = false)
    private boolean published;

    public Notice(String title, String body, Long authorId, boolean published) {
        this.title = title;
        this.body = body;
        this.authorId = authorId;
        this.published = published;
    }

    public void update(String title, String body, boolean published) {
        this.title = title;
        this.body = body;
        this.published = published;
    }
}

package com.lostmemory.website.patchnote.entity;

import com.lostmemory.website.global.time.BaseTimeEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.LocalDate;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Entity
@Table(name = "patch_note")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class PatchNote extends BaseTimeEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, length = 50)
    private String version;

    @Column(name = "release_date", nullable = false)
    private LocalDate releaseDate;

    @Column(nullable = false, columnDefinition = "TEXT")
    private String body;

    @Column(name = "author_id")
    private Long authorId;

    @Column(nullable = false)
    private boolean published;

    public PatchNote(String version, LocalDate releaseDate, String body, Long authorId, boolean published) {
        this.version = version;
        this.releaseDate = releaseDate;
        this.body = body;
        this.authorId = authorId;
        this.published = published;
    }

    public void update(String version, LocalDate releaseDate, String body, boolean published) {
        this.version = version;
        this.releaseDate = releaseDate;
        this.body = body;
        this.published = published;
    }
}

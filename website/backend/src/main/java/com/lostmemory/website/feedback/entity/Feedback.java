package com.lostmemory.website.feedback.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.LocalDateTime;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.jpa.domain.support.AuditingEntityListener;

@Entity
@Table(name = "feedback")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
@jakarta.persistence.EntityListeners(AuditingEntityListener.class)
public class Feedback {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 32)
    private FeedbackCategory category;

    @Column(nullable = false, length = 200)
    private String title;

    @Column(nullable = false, columnDefinition = "TEXT")
    private String body;

    @Column(length = 255)
    private String contact;

    @Column(nullable = false)
    private boolean handled;

    @CreatedDate
    @Column(name = "created_at", updatable = false, nullable = false)
    private LocalDateTime createdAt;

    @Column(name = "handled_at")
    private LocalDateTime handledAt;

    public Feedback(FeedbackCategory category, String title, String body, String contact) {
        this.category = category;
        this.title = title;
        this.body = body;
        this.contact = contact;
        this.handled = false;
    }

    public void markHandled() {
        this.handled = true;
        this.handledAt = LocalDateTime.now();
    }

    public void unmarkHandled() {
        this.handled = false;
        this.handledAt = null;
    }
}

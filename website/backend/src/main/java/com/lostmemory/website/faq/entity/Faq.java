package com.lostmemory.website.faq.entity;

import com.lostmemory.website.faqcategory.entity.FaqCategory;
import com.lostmemory.website.global.time.BaseTimeEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Entity
@Table(name = "faq")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Faq extends BaseTimeEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, length = 500)
    private String question;

    @Column(nullable = false, columnDefinition = "TEXT")
    private String answer;

    @Column(name = "sort_order", nullable = false)
    private int sortOrder;

    @Column(nullable = false)
    private boolean published;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "category_id")
    private FaqCategory category;

    public Faq(String question, String answer, int sortOrder, boolean published, FaqCategory category) {
        this.question = question;
        this.answer = answer;
        this.sortOrder = sortOrder;
        this.published = published;
        this.category = category;
    }

    public void update(String question, String answer, int sortOrder, boolean published, FaqCategory category) {
        this.question = question;
        this.answer = answer;
        this.sortOrder = sortOrder;
        this.published = published;
        this.category = category;
    }
}

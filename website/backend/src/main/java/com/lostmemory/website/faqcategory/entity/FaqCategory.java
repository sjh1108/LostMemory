package com.lostmemory.website.faqcategory.entity;

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
@Table(name = "faq_category")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class FaqCategory extends BaseTimeEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, length = 64, unique = true)
    private String name;

    @Column(name = "sort_order", nullable = false)
    private int sortOrder;

    @Column(nullable = false)
    private boolean published;

    public FaqCategory(String name, int sortOrder, boolean published) {
        this.name = name;
        this.sortOrder = sortOrder;
        this.published = published;
    }

    public void update(String name, int sortOrder, boolean published) {
        this.name = name;
        this.sortOrder = sortOrder;
        this.published = published;
    }
}

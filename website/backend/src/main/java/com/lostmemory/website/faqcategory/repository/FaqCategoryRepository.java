package com.lostmemory.website.faqcategory.repository;

import com.lostmemory.website.faqcategory.entity.FaqCategory;
import java.util.List;
import org.springframework.data.jpa.repository.JpaRepository;

public interface FaqCategoryRepository extends JpaRepository<FaqCategory, Long> {

    List<FaqCategory> findAllByOrderBySortOrderAscIdAsc();

    List<FaqCategory> findByPublishedTrueOrderBySortOrderAscIdAsc();
}

package com.lostmemory.website.faq.repository;

import com.lostmemory.website.faq.entity.Faq;
import java.util.List;
import org.springframework.data.jpa.repository.JpaRepository;

public interface FaqRepository extends JpaRepository<Faq, Long> {

    List<Faq> findByPublishedTrueOrderBySortOrderAsc();

    List<Faq> findAllByOrderBySortOrderAsc();
}

package com.lostmemory.website.notice.repository;

import com.lostmemory.website.notice.entity.Notice;
import java.util.List;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

public interface NoticeRepository extends JpaRepository<Notice, Long> {

    List<Notice> findByPublishedTrueOrderByCreatedAtDesc();

    List<Notice> findByPublishedTrueOrderByCreatedAtDesc(Pageable pageable);

    List<Notice> findAllByOrderByCreatedAtDesc();
}

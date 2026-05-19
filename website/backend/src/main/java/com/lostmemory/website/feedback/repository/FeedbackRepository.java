package com.lostmemory.website.feedback.repository;

import com.lostmemory.website.feedback.entity.Feedback;
import java.util.List;
import org.springframework.data.jpa.repository.JpaRepository;

public interface FeedbackRepository extends JpaRepository<Feedback, Long> {

    List<Feedback> findAllByOrderByHandledAscCreatedAtDesc();

    long countByHandledFalse();
}

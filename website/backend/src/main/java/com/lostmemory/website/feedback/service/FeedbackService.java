package com.lostmemory.website.feedback.service;

import com.lostmemory.website.feedback.dto.FeedbackForm;
import com.lostmemory.website.feedback.entity.Feedback;
import com.lostmemory.website.feedback.repository.FeedbackRepository;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class FeedbackService {

    private final FeedbackRepository repository;

    public List<Feedback> findAll() {
        return repository.findAllByOrderByHandledAscCreatedAtDesc();
    }

    public Feedback findById(Long id) {
        return repository.findById(id)
            .orElseThrow(() -> new IllegalArgumentException("feedback not found: " + id));
    }

    public long countUnhandled() {
        return repository.countByHandledFalse();
    }

    @Transactional
    public Long submit(FeedbackForm form) {
        Feedback feedback = new Feedback(form.getCategory(), form.getTitle(), form.getBody(), form.getContact());
        return repository.save(feedback).getId();
    }

    @Transactional
    public void markHandled(Long id) {
        Feedback feedback = findById(id);
        feedback.markHandled();
    }

    @Transactional
    public void unmarkHandled(Long id) {
        Feedback feedback = findById(id);
        feedback.unmarkHandled();
    }
}

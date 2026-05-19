package com.lostmemory.website.notice.service;

import com.lostmemory.website.notice.dto.NoticeForm;
import com.lostmemory.website.notice.entity.Notice;
import com.lostmemory.website.notice.repository.NoticeRepository;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.PageRequest;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class NoticeService {

    private final NoticeRepository repository;

    public List<Notice> findRecentPublished(int limit) {
        return repository.findByPublishedTrueOrderByCreatedAtDesc(PageRequest.of(0, limit));
    }

    public List<Notice> findAllPublished() {
        return repository.findByPublishedTrueOrderByCreatedAtDesc();
    }

    public List<Notice> findAll() {
        return repository.findAllByOrderByCreatedAtDesc();
    }

    public Notice findById(Long id) {
        return repository.findById(id)
            .orElseThrow(() -> new IllegalArgumentException("notice not found: " + id));
    }

    public Notice findPublishedById(Long id) {
        Notice notice = findById(id);
        if (!notice.isPublished()) {
            throw new IllegalArgumentException("notice not published: " + id);
        }
        return notice;
    }

    @Transactional
    public Long create(NoticeForm form, Long authorId) {
        Notice notice = new Notice(form.getTitle(), form.getBody(), authorId, form.isPublished());
        return repository.save(notice).getId();
    }

    @Transactional
    public void update(Long id, NoticeForm form) {
        Notice notice = findById(id);
        notice.update(form.getTitle(), form.getBody(), form.isPublished());
    }

    @Transactional
    public void delete(Long id) {
        repository.deleteById(id);
    }

    public long countPublished() {
        return repository.findByPublishedTrueOrderByCreatedAtDesc().size();
    }

    public long countAll() {
        return repository.count();
    }
}

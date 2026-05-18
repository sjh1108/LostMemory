package com.lostmemory.website.faq.service;

import com.lostmemory.website.faq.dto.FaqForm;
import com.lostmemory.website.faq.entity.Faq;
import com.lostmemory.website.faq.repository.FaqRepository;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class FaqService {

    private final FaqRepository repository;

    public List<Faq> findAllPublished() {
        return repository.findByPublishedTrueOrderBySortOrderAsc();
    }

    public List<Faq> findAll() {
        return repository.findAllByOrderBySortOrderAsc();
    }

    public Faq findById(Long id) {
        return repository.findById(id)
            .orElseThrow(() -> new IllegalArgumentException("faq not found: " + id));
    }

    @Transactional
    public Long create(FaqForm form) {
        Faq faq = new Faq(form.getQuestion(), form.getAnswer(), form.getSortOrder(), form.isPublished());
        return repository.save(faq).getId();
    }

    @Transactional
    public void update(Long id, FaqForm form) {
        Faq faq = findById(id);
        faq.update(form.getQuestion(), form.getAnswer(), form.getSortOrder(), form.isPublished());
    }

    @Transactional
    public void delete(Long id) {
        repository.deleteById(id);
    }

    public long countAll() {
        return repository.count();
    }
}

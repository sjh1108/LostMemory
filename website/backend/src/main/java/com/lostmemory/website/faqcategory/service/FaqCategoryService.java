package com.lostmemory.website.faqcategory.service;

import com.lostmemory.website.faqcategory.dto.FaqCategoryForm;
import com.lostmemory.website.faqcategory.entity.FaqCategory;
import com.lostmemory.website.faqcategory.repository.FaqCategoryRepository;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class FaqCategoryService {

    private final FaqCategoryRepository repository;

    public List<FaqCategory> findAll() {
        return repository.findAllByOrderBySortOrderAscIdAsc();
    }

    public List<FaqCategory> findAllPublished() {
        return repository.findByPublishedTrueOrderBySortOrderAscIdAsc();
    }

    public FaqCategory findById(Long id) {
        return repository.findById(id)
            .orElseThrow(() -> new IllegalArgumentException("faq category not found: " + id));
    }

    @Transactional
    public Long create(FaqCategoryForm form) {
        FaqCategory entity = new FaqCategory(form.getName(), form.getSortOrder(), form.isPublished());
        return repository.save(entity).getId();
    }

    @Transactional
    public void update(Long id, FaqCategoryForm form) {
        FaqCategory entity = findById(id);
        entity.update(form.getName(), form.getSortOrder(), form.isPublished());
    }

    @Transactional
    public void delete(Long id) {
        repository.deleteById(id);
    }

    public long countAll() {
        return repository.count();
    }
}

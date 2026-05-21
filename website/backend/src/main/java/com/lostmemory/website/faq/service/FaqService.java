package com.lostmemory.website.faq.service;

import com.lostmemory.website.faq.dto.FaqForm;
import com.lostmemory.website.faq.entity.Faq;
import com.lostmemory.website.faq.repository.FaqRepository;
import com.lostmemory.website.faqcategory.entity.FaqCategory;
import com.lostmemory.website.faqcategory.service.FaqCategoryService;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class FaqService {

    private final FaqRepository repository;
    private final FaqCategoryService categoryService;

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

    /**
     * Published FAQ 를 카테고리별로 그룹화. 카테고리 sortOrder ASC 정렬, 미분류는 마지막 (null key).
     * 각 그룹 안 FAQ 는 sortOrder ASC + createdAt DESC (repository 의 OrderBy 유지).
     */
    public Map<FaqCategory, List<Faq>> findAllPublishedGroupedByCategory() {
        List<Faq> all = repository.findByPublishedTrueOrderBySortOrderAsc();
        Comparator<FaqCategory> categoryOrder = Comparator
            .comparing((FaqCategory c) -> c == null)            // null (미분류) 마지막
            .thenComparingInt(c -> c == null ? Integer.MAX_VALUE : c.getSortOrder())
            .thenComparing(c -> c == null ? Long.MAX_VALUE : c.getId());
        return all.stream().collect(Collectors.groupingBy(
            Faq::getCategory,
            () -> new LinkedHashMap<>(),
            Collectors.toList()
        )).entrySet().stream()
            .sorted(Map.Entry.comparingByKey(categoryOrder))
            .collect(Collectors.toMap(Map.Entry::getKey, Map.Entry::getValue,
                (a, b) -> a, LinkedHashMap::new));
    }

    @Transactional
    public Long create(FaqForm form) {
        FaqCategory category = resolveCategory(form.getCategoryId());
        Faq faq = new Faq(form.getQuestion(), form.getAnswer(), form.getSortOrder(), form.isPublished(), category);
        return repository.save(faq).getId();
    }

    @Transactional
    public void update(Long id, FaqForm form) {
        Faq faq = findById(id);
        FaqCategory category = resolveCategory(form.getCategoryId());
        faq.update(form.getQuestion(), form.getAnswer(), form.getSortOrder(), form.isPublished(), category);
    }

    @Transactional
    public void delete(Long id) {
        repository.deleteById(id);
    }

    public long countAll() {
        return repository.count();
    }

    private FaqCategory resolveCategory(Long categoryId) {
        return categoryId == null ? null : categoryService.findById(categoryId);
    }
}

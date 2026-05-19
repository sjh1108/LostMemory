package com.lostmemory.website.faqcategory.controller;

import com.lostmemory.website.faqcategory.dto.FaqCategoryForm;
import com.lostmemory.website.faqcategory.entity.FaqCategory;
import com.lostmemory.website.faqcategory.service.FaqCategoryService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.validation.BindingResult;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.ModelAttribute;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.servlet.mvc.support.RedirectAttributes;

@Controller
@RequestMapping("${app.admin.base-path}/faq-categories")
@RequiredArgsConstructor
public class FaqCategoryAdminController {

    private final FaqCategoryService service;

    @Value("${app.admin.base-path}")
    private String adminBase;

    @GetMapping
    public String list(Model model) {
        model.addAttribute("categories", service.findAll());
        return "admin/faq-category/list";
    }

    @GetMapping("/new")
    public String newForm(Model model) {
        model.addAttribute("form", new FaqCategoryForm());
        model.addAttribute("categoryId", null);
        return "admin/faq-category/edit";
    }

    @PostMapping
    public String create(@Valid @ModelAttribute("form") FaqCategoryForm form,
                         BindingResult result,
                         RedirectAttributes ra) {
        if (result.hasErrors()) {
            return "admin/faq-category/edit";
        }
        service.create(form);
        ra.addFlashAttribute("message", "FAQ 카테고리 저장 완료");
        return "redirect:" + adminBase + "/faq-categories";
    }

    @GetMapping("/{id}/edit")
    public String editForm(@PathVariable Long id, Model model) {
        FaqCategory category = service.findById(id);
        model.addAttribute("form", FaqCategoryForm.from(category));
        model.addAttribute("categoryId", category.getId());
        return "admin/faq-category/edit";
    }

    @PostMapping("/{id}")
    public String update(@PathVariable Long id,
                         @Valid @ModelAttribute("form") FaqCategoryForm form,
                         BindingResult result,
                         Model model,
                         RedirectAttributes ra) {
        if (result.hasErrors()) {
            model.addAttribute("categoryId", id);
            return "admin/faq-category/edit";
        }
        service.update(id, form);
        ra.addFlashAttribute("message", "FAQ 카테고리 갱신 완료");
        return "redirect:" + adminBase + "/faq-categories";
    }

    @PostMapping("/{id}/delete")
    public String delete(@PathVariable Long id, RedirectAttributes ra) {
        service.delete(id);
        ra.addFlashAttribute("message", "FAQ 카테고리 삭제 완료. 연결된 FAQ 는 미분류로 이동.");
        return "redirect:" + adminBase + "/faq-categories";
    }
}

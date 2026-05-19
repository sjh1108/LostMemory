package com.lostmemory.website.faq.controller;

import com.lostmemory.website.faq.dto.FaqForm;
import com.lostmemory.website.faq.entity.Faq;
import com.lostmemory.website.faq.service.FaqService;
import com.lostmemory.website.faqcategory.service.FaqCategoryService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
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
@RequestMapping("/admin/faqs")
@RequiredArgsConstructor
public class FaqAdminController {

    private final FaqService service;
    private final FaqCategoryService categoryService;

    @GetMapping
    public String list(Model model) {
        model.addAttribute("faqs", service.findAll());
        return "admin/faq/list";
    }

    @GetMapping("/new")
    public String newForm(Model model) {
        model.addAttribute("form", new FaqForm());
        model.addAttribute("faqId", null);
        model.addAttribute("categories", categoryService.findAll());
        return "admin/faq/edit";
    }

    @PostMapping
    public String create(@Valid @ModelAttribute("form") FaqForm form,
                         BindingResult result,
                         Model model,
                         RedirectAttributes ra) {
        if (result.hasErrors()) {
            model.addAttribute("categories", categoryService.findAll());
            return "admin/faq/edit";
        }
        service.create(form);
        ra.addFlashAttribute("message", "FAQ 저장 완료");
        return "redirect:/admin/faqs";
    }

    @GetMapping("/{id}/edit")
    public String editForm(@PathVariable Long id, Model model) {
        Faq faq = service.findById(id);
        model.addAttribute("form", FaqForm.from(faq));
        model.addAttribute("faqId", faq.getId());
        model.addAttribute("categories", categoryService.findAll());
        return "admin/faq/edit";
    }

    @PostMapping("/{id}")
    public String update(@PathVariable Long id,
                         @Valid @ModelAttribute("form") FaqForm form,
                         BindingResult result,
                         Model model,
                         RedirectAttributes ra) {
        if (result.hasErrors()) {
            model.addAttribute("faqId", id);
            model.addAttribute("categories", categoryService.findAll());
            return "admin/faq/edit";
        }
        service.update(id, form);
        ra.addFlashAttribute("message", "FAQ 갱신 완료");
        return "redirect:/admin/faqs";
    }

    @PostMapping("/{id}/delete")
    public String delete(@PathVariable Long id, RedirectAttributes ra) {
        service.delete(id);
        ra.addFlashAttribute("message", "FAQ 삭제 완료");
        return "redirect:/admin/faqs";
    }
}

package com.lostmemory.website.notice.controller;

import com.lostmemory.website.notice.dto.NoticeForm;
import com.lostmemory.website.notice.entity.Notice;
import com.lostmemory.website.notice.service.NoticeService;
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
@RequestMapping("${app.admin.base-path}/notices")
@RequiredArgsConstructor
public class NoticeAdminController {

    private final NoticeService service;

    @Value("${app.admin.base-path}")
    private String adminBase;

    @GetMapping
    public String list(Model model) {
        model.addAttribute("notices", service.findAll());
        return "admin/notice/list";
    }

    @GetMapping("/new")
    public String newForm(Model model) {
        model.addAttribute("form", new NoticeForm());
        model.addAttribute("noticeId", null);
        return "admin/notice/edit";
    }

    @PostMapping
    public String create(@Valid @ModelAttribute("form") NoticeForm form,
                         BindingResult result,
                         RedirectAttributes ra) {
        if (result.hasErrors()) {
            return "admin/notice/edit";
        }
        // MVP — authorId 단순화 (null). 추후 SecurityContext 의 username → admin_user.id lookup.
        service.create(form, null);
        ra.addFlashAttribute("message", "공지 저장 완료");
        return "redirect:" + adminBase + "/notices";
    }

    @GetMapping("/{id}/edit")
    public String editForm(@PathVariable Long id, Model model) {
        Notice notice = service.findById(id);
        model.addAttribute("form", NoticeForm.from(notice));
        model.addAttribute("noticeId", notice.getId());
        return "admin/notice/edit";
    }

    @PostMapping("/{id}")
    public String update(@PathVariable Long id,
                         @Valid @ModelAttribute("form") NoticeForm form,
                         BindingResult result,
                         Model model,
                         RedirectAttributes ra) {
        if (result.hasErrors()) {
            model.addAttribute("noticeId", id);
            return "admin/notice/edit";
        }
        service.update(id, form);
        ra.addFlashAttribute("message", "공지 갱신 완료");
        return "redirect:" + adminBase + "/notices";
    }

    @PostMapping("/{id}/delete")
    public String delete(@PathVariable Long id, RedirectAttributes ra) {
        service.delete(id);
        ra.addFlashAttribute("message", "공지 삭제 완료");
        return "redirect:" + adminBase + "/notices";
    }
}

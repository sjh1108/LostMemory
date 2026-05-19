package com.lostmemory.website.feedback.controller;

import com.lostmemory.website.feedback.entity.Feedback;
import com.lostmemory.website.feedback.service.FeedbackService;
import lombok.RequiredArgsConstructor;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.servlet.mvc.support.RedirectAttributes;

@Controller
@RequestMapping("${app.admin.base-path}/feedbacks")
@RequiredArgsConstructor
public class FeedbackAdminController {

    private final FeedbackService service;

    @Value("${app.admin.base-path}")
    private String adminBase;

    @GetMapping
    public String list(Model model) {
        model.addAttribute("feedbacks", service.findAll());
        return "admin/feedback/list";
    }

    @GetMapping("/{id}")
    public String detail(@PathVariable Long id, Model model) {
        Feedback feedback = service.findById(id);
        model.addAttribute("feedback", feedback);
        return "admin/feedback/detail";
    }

    @PostMapping("/{id}/handle")
    public String handle(@PathVariable Long id, RedirectAttributes ra) {
        service.markHandled(id);
        ra.addFlashAttribute("message", "처리 완료로 표시했습니다");
        return "redirect:" + adminBase + "/feedbacks/" + id;
    }

    @PostMapping("/{id}/unhandle")
    public String unhandle(@PathVariable Long id, RedirectAttributes ra) {
        service.unmarkHandled(id);
        ra.addFlashAttribute("message", "처리 완료 해제했습니다");
        return "redirect:" + adminBase + "/feedbacks/" + id;
    }
}

package com.lostmemory.website.admin.controller;

import com.lostmemory.website.faq.service.FaqService;
import com.lostmemory.website.faqcategory.service.FaqCategoryService;
import com.lostmemory.website.feedback.service.FeedbackService;
import com.lostmemory.website.notice.service.NoticeService;
import com.lostmemory.website.patchnote.service.PatchNoteService;
import java.security.Principal;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;

@Controller
@RequiredArgsConstructor
public class AdminDashboardController {

    private final NoticeService noticeService;
    private final PatchNoteService patchNoteService;
    private final FaqService faqService;
    private final FaqCategoryService faqCategoryService;
    private final FeedbackService feedbackService;

    @GetMapping("${app.admin.base-path}/dashboard")
    public String dashboard(Principal principal, Model model) {
        model.addAttribute("username", principal.getName());
        model.addAttribute("noticeCount", noticeService.countAll());
        model.addAttribute("patchNoteCount", patchNoteService.countAll());
        model.addAttribute("faqCount", faqService.countAll());
        model.addAttribute("faqCategoryCount", faqCategoryService.countAll());
        model.addAttribute("feedbackUnhandledCount", feedbackService.countUnhandled());
        return "admin/dashboard";
    }
}

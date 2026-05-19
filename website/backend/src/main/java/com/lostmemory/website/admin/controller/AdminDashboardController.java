package com.lostmemory.website.admin.controller;

import com.lostmemory.website.faq.service.FaqService;
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

    @GetMapping("/admin/dashboard")
    public String dashboard(Principal principal, Model model) {
        model.addAttribute("username", principal.getName());
        model.addAttribute("noticeCount", noticeService.countAll());
        model.addAttribute("patchNoteCount", patchNoteService.countAll());
        model.addAttribute("faqCount", faqService.countAll());
        return "admin/dashboard";
    }
}

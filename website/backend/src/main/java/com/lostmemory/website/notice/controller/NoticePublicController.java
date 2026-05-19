package com.lostmemory.website.notice.controller;

import com.lostmemory.website.global.markdown.MarkdownRenderer;
import com.lostmemory.website.notice.entity.Notice;
import com.lostmemory.website.notice.service.NoticeService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;

@Controller
@RequestMapping("/notices")
@RequiredArgsConstructor
public class NoticePublicController {

    private final NoticeService service;
    private final MarkdownRenderer markdown;

    @GetMapping
    public String list(Model model) {
        model.addAttribute("notices", service.findAllPublished());
        return "notice/list";
    }

    @GetMapping("/{id}")
    public String detail(@PathVariable Long id, Model model) {
        Notice notice = service.findPublishedById(id);
        model.addAttribute("notice", notice);
        model.addAttribute("bodyHtml", markdown.render(notice.getBody()));
        return "notice/detail";
    }
}

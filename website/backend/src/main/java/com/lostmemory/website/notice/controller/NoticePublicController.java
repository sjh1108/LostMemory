package com.lostmemory.website.notice.controller;

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

    @GetMapping
    public String list(Model model) {
        model.addAttribute("notices", service.findAllPublished());
        return "notice/list";
    }

    @GetMapping("/{id}")
    public String detail(@PathVariable Long id, Model model) {
        model.addAttribute("notice", service.findPublishedById(id));
        return "notice/detail";
    }
}

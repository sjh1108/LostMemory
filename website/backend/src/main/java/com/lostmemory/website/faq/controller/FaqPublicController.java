package com.lostmemory.website.faq.controller;

import com.lostmemory.website.faq.service.FaqService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;

@Controller
@RequestMapping("/faq")
@RequiredArgsConstructor
public class FaqPublicController {

    private final FaqService service;

    @GetMapping
    public String list(Model model) {
        model.addAttribute("faqs", service.findAllPublished());
        return "faq/list";
    }
}

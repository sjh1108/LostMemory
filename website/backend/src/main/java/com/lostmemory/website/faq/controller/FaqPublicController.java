package com.lostmemory.website.faq.controller;

import java.util.List;

import com.lostmemory.website.faq.dto.FaqView;
import com.lostmemory.website.faq.service.FaqService;
import com.lostmemory.website.global.markdown.MarkdownRenderer;
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
    private final MarkdownRenderer markdown;

    @GetMapping
    public String list(Model model) {
        List<FaqView> faqs = service.findAllPublished().stream()
                .map(f -> new FaqView(f.getQuestion(), markdown.render(f.getAnswer())))
                .toList();
        model.addAttribute("faqs", faqs);
        return "faq/list";
    }
}

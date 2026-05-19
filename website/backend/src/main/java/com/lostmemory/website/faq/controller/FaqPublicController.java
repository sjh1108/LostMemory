package com.lostmemory.website.faq.controller;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

import com.lostmemory.website.faq.dto.FaqView;
import com.lostmemory.website.faq.service.FaqService;
import com.lostmemory.website.faqcategory.entity.FaqCategory;
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
        Map<FaqCategory, List<FaqView>> groupedFaqs = service.findAllPublishedGroupedByCategory().entrySet().stream()
                .collect(Collectors.toMap(
                    Map.Entry::getKey,
                    e -> e.getValue().stream()
                        .map(f -> new FaqView(f.getQuestion(), markdown.render(f.getAnswer())))
                        .toList(),
                    (a, b) -> a,
                    LinkedHashMap::new
                ));
        model.addAttribute("groupedFaqs", groupedFaqs);
        return "faq/list";
    }
}

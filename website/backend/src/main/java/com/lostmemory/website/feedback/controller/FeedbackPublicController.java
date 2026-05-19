package com.lostmemory.website.feedback.controller;

import com.lostmemory.website.feedback.dto.FeedbackForm;
import com.lostmemory.website.feedback.entity.FeedbackCategory;
import com.lostmemory.website.feedback.service.FeedbackService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.validation.BindingResult;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.ModelAttribute;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;

@Controller
@RequestMapping("/feedback")
@RequiredArgsConstructor
public class FeedbackPublicController {

    private final FeedbackService service;

    @GetMapping
    public String form(Model model) {
        if (!model.containsAttribute("form")) {
            model.addAttribute("form", new FeedbackForm());
        }
        model.addAttribute("categories", FeedbackCategory.values());
        return "feedback/form";
    }

    @PostMapping
    public String submit(@Valid @ModelAttribute("form") FeedbackForm form,
                         BindingResult result,
                         Model model) {
        if (result.hasErrors()) {
            model.addAttribute("categories", FeedbackCategory.values());
            return "feedback/form";
        }
        service.submit(form);
        return "redirect:/feedback/submitted";
    }

    @GetMapping("/submitted")
    public String submitted() {
        return "feedback/submitted";
    }
}

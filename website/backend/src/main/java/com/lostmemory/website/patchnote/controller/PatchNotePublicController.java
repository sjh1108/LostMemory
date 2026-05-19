package com.lostmemory.website.patchnote.controller;

import com.lostmemory.website.patchnote.service.PatchNoteService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;

@Controller
@RequestMapping("/patch-notes")
@RequiredArgsConstructor
public class PatchNotePublicController {

    private final PatchNoteService service;

    @GetMapping
    public String list(Model model) {
        model.addAttribute("patchNotes", service.findAllPublished());
        return "patchnote/list";
    }

    @GetMapping("/{id}")
    public String detail(@PathVariable Long id, Model model) {
        model.addAttribute("patchNote", service.findPublishedById(id));
        return "patchnote/detail";
    }
}

package com.lostmemory.website.patchnote.controller;

import com.lostmemory.website.global.markdown.MarkdownRenderer;
import com.lostmemory.website.patchnote.entity.PatchNote;
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
    private final MarkdownRenderer markdown;

    @GetMapping
    public String list(Model model) {
        model.addAttribute("patchNotes", service.findAllPublished());
        return "patchnote/list";
    }

    @GetMapping("/{id}")
    public String detail(@PathVariable Long id, Model model) {
        PatchNote patchNote = service.findPublishedById(id);
        model.addAttribute("patchNote", patchNote);
        model.addAttribute("bodyHtml", markdown.render(patchNote.getBody()));
        return "patchnote/detail";
    }
}

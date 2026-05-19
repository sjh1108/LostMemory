package com.lostmemory.website.patchnote.controller;

import com.lostmemory.website.patchnote.dto.PatchNoteForm;
import com.lostmemory.website.patchnote.entity.PatchNote;
import com.lostmemory.website.patchnote.service.PatchNoteService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.validation.BindingResult;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.ModelAttribute;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.servlet.mvc.support.RedirectAttributes;

@Controller
@RequestMapping("/admin/patch-notes")
@RequiredArgsConstructor
public class PatchNoteAdminController {

    private final PatchNoteService service;

    @GetMapping
    public String list(Model model) {
        model.addAttribute("patchNotes", service.findAll());
        return "admin/patchnote/list";
    }

    @GetMapping("/new")
    public String newForm(Model model) {
        model.addAttribute("form", new PatchNoteForm());
        model.addAttribute("patchNoteId", null);
        return "admin/patchnote/edit";
    }

    @PostMapping
    public String create(@Valid @ModelAttribute("form") PatchNoteForm form,
                         BindingResult result,
                         RedirectAttributes ra) {
        if (result.hasErrors()) {
            return "admin/patchnote/edit";
        }
        service.create(form, null);
        ra.addFlashAttribute("message", "패치노트 저장 완료");
        return "redirect:/admin/patch-notes";
    }

    @GetMapping("/{id}/edit")
    public String editForm(@PathVariable Long id, Model model) {
        PatchNote pn = service.findById(id);
        model.addAttribute("form", PatchNoteForm.from(pn));
        model.addAttribute("patchNoteId", pn.getId());
        return "admin/patchnote/edit";
    }

    @PostMapping("/{id}")
    public String update(@PathVariable Long id,
                         @Valid @ModelAttribute("form") PatchNoteForm form,
                         BindingResult result,
                         Model model,
                         RedirectAttributes ra) {
        if (result.hasErrors()) {
            model.addAttribute("patchNoteId", id);
            return "admin/patchnote/edit";
        }
        service.update(id, form);
        ra.addFlashAttribute("message", "패치노트 갱신 완료");
        return "redirect:/admin/patch-notes";
    }

    @PostMapping("/{id}/delete")
    public String delete(@PathVariable Long id, RedirectAttributes ra) {
        service.delete(id);
        ra.addFlashAttribute("message", "패치노트 삭제 완료");
        return "redirect:/admin/patch-notes";
    }
}

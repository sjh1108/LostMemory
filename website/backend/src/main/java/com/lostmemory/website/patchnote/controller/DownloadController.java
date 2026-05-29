package com.lostmemory.website.patchnote.controller;

import com.lostmemory.website.patchnote.service.PatchNoteService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.servlet.view.RedirectView;

@Controller
@RequestMapping("/downloads")
@RequiredArgsConstructor
public class DownloadController {

    private final PatchNoteService service;

    @GetMapping("/latest")
    public RedirectView latest() {
        return redirect(service.recordLatestDownload());
    }

    @GetMapping("/{id}")
    public RedirectView byPatchNote(@PathVariable Long id) {
        return redirect(service.recordDownload(id));
    }

    private RedirectView redirect(String url) {
        RedirectView view = new RedirectView(url);
        view.setStatusCode(org.springframework.http.HttpStatus.FOUND);
        view.setExposeModelAttributes(false);
        return view;
    }
}

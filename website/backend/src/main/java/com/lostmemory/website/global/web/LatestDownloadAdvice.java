package com.lostmemory.website.global.web;

import com.lostmemory.website.patchnote.service.PatchNoteService;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.ControllerAdvice;
import org.springframework.web.bind.annotation.ModelAttribute;

@ControllerAdvice
@RequiredArgsConstructor
public class LatestDownloadAdvice {

    private final PatchNoteService patchNoteService;

    @ModelAttribute("latestDownloadUrl")
    public String latestDownloadUrl() {
        return patchNoteService.findLatestPublishedDownloadUrl().orElse(null);
    }
}

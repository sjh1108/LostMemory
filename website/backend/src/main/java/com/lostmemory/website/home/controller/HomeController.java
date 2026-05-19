package com.lostmemory.website.home.controller;

import com.lostmemory.website.global.markdown.MarkdownRenderer;
import com.lostmemory.website.notice.entity.Notice;
import com.lostmemory.website.notice.service.NoticeService;
import com.lostmemory.website.patchnote.entity.PatchNote;
import com.lostmemory.website.patchnote.service.PatchNoteService;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;

@Controller
@RequiredArgsConstructor
public class HomeController {

    private static final int LATEST_PATCH_EXCERPT_LENGTH = 200;

    private final NoticeService noticeService;
    private final PatchNoteService patchNoteService;
    private final MarkdownRenderer markdownRenderer;

    @GetMapping("/")
    public String index(Model model) {
        List<Notice> recent = noticeService.findRecentPublished(3);
        model.addAttribute("recentNotices", recent);

        PatchNote latest = patchNoteService.findLatestPublished().orElse(null);
        model.addAttribute("latestPatchNote", latest);
        if (latest != null) {
            String body = latest.getBody();
            String excerpt = body.length() > LATEST_PATCH_EXCERPT_LENGTH
                ? body.substring(0, LATEST_PATCH_EXCERPT_LENGTH) + "…"
                : body;
            model.addAttribute("latestPatchNoteExcerptHtml", markdownRenderer.render(excerpt));
        }
        return "home/index";
    }
}

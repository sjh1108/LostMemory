package com.lostmemory.website.home.controller;

import com.lostmemory.website.notice.entity.Notice;
import com.lostmemory.website.notice.service.NoticeService;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;

@Controller
@RequiredArgsConstructor
public class HomeController {

    private final NoticeService noticeService;

    @GetMapping("/")
    public String index(Model model) {
        List<Notice> recent = noticeService.findRecentPublished(3);
        model.addAttribute("recentNotices", recent);
        return "home/index";
    }
}

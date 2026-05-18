package com.lostmemory.website.notice.dto;

import com.lostmemory.website.notice.entity.Notice;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
public class NoticeForm {

    @NotBlank
    @Size(max = 200)
    private String title;

    @NotBlank
    private String body;

    private boolean published;

    public static NoticeForm from(Notice notice) {
        NoticeForm form = new NoticeForm();
        form.setTitle(notice.getTitle());
        form.setBody(notice.getBody());
        form.setPublished(notice.isPublished());
        return form;
    }
}

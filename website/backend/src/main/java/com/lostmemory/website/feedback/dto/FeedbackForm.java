package com.lostmemory.website.feedback.dto;

import com.lostmemory.website.feedback.entity.FeedbackCategory;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
public class FeedbackForm {

    @NotNull
    private FeedbackCategory category;

    @NotBlank
    @Size(max = 200)
    private String title;

    @NotBlank
    private String body;

    @Size(max = 255)
    private String contact;
}

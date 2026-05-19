package com.lostmemory.website.faq.dto;

import com.lostmemory.website.faq.entity.Faq;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.PositiveOrZero;
import jakarta.validation.constraints.Size;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
public class FaqForm {

    @NotBlank
    @Size(max = 500)
    private String question;

    @NotBlank
    private String answer;

    @PositiveOrZero
    private int sortOrder;

    private boolean published = true;

    public static FaqForm from(Faq faq) {
        FaqForm form = new FaqForm();
        form.setQuestion(faq.getQuestion());
        form.setAnswer(faq.getAnswer());
        form.setSortOrder(faq.getSortOrder());
        form.setPublished(faq.isPublished());
        return form;
    }
}

package com.lostmemory.website.faqcategory.dto;

import com.lostmemory.website.faqcategory.entity.FaqCategory;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.PositiveOrZero;
import jakarta.validation.constraints.Size;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
public class FaqCategoryForm {

    @NotBlank
    @Size(max = 64)
    private String name;

    @PositiveOrZero
    private int sortOrder;

    private boolean published = true;

    public static FaqCategoryForm from(FaqCategory category) {
        FaqCategoryForm form = new FaqCategoryForm();
        form.setName(category.getName());
        form.setSortOrder(category.getSortOrder());
        form.setPublished(category.isPublished());
        return form;
    }
}

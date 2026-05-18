package com.lostmemory.website.patchnote.dto;

import com.lostmemory.website.patchnote.entity.PatchNote;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import java.time.LocalDate;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;
import org.springframework.format.annotation.DateTimeFormat;

@Getter
@Setter
@NoArgsConstructor
public class PatchNoteForm {

    @NotBlank
    @Size(max = 50)
    private String version;

    @NotNull
    @DateTimeFormat(iso = DateTimeFormat.ISO.DATE)
    private LocalDate releaseDate;

    @NotBlank
    private String body;

    private boolean published;

    public static PatchNoteForm from(PatchNote pn) {
        PatchNoteForm form = new PatchNoteForm();
        form.setVersion(pn.getVersion());
        form.setReleaseDate(pn.getReleaseDate());
        form.setBody(pn.getBody());
        form.setPublished(pn.isPublished());
        return form;
    }
}

package com.lostmemory.website.patchnote.service;

import com.lostmemory.website.patchnote.dto.PatchNoteForm;
import com.lostmemory.website.patchnote.entity.PatchNote;
import com.lostmemory.website.patchnote.repository.PatchNoteRepository;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class PatchNoteService {

    private final PatchNoteRepository repository;

    public List<PatchNote> findAllPublished() {
        return repository.findByPublishedTrueOrderByReleaseDateDesc();
    }

    public List<PatchNote> findAll() {
        return repository.findAllByOrderByReleaseDateDesc();
    }

    public PatchNote findById(Long id) {
        return repository.findById(id)
            .orElseThrow(() -> new IllegalArgumentException("patch note not found: " + id));
    }

    public PatchNote findPublishedById(Long id) {
        PatchNote pn = findById(id);
        if (!pn.isPublished()) {
            throw new IllegalArgumentException("patch note not published: " + id);
        }
        return pn;
    }

    @Transactional
    public Long create(PatchNoteForm form, Long authorId) {
        PatchNote pn = new PatchNote(form.getVersion(), form.getReleaseDate(), form.getBody(), authorId, form.isPublished());
        return repository.save(pn).getId();
    }

    @Transactional
    public void update(Long id, PatchNoteForm form) {
        PatchNote pn = findById(id);
        pn.update(form.getVersion(), form.getReleaseDate(), form.getBody(), form.isPublished());
    }

    @Transactional
    public void delete(Long id) {
        repository.deleteById(id);
    }

    public long countAll() {
        return repository.count();
    }
}

package com.lostmemory.website.patchnote.service;

import com.lostmemory.website.patchnote.dto.PatchNoteForm;
import com.lostmemory.website.patchnote.entity.PatchNote;
import com.lostmemory.website.patchnote.repository.PatchNoteRepository;
import java.util.List;
import java.util.Optional;
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

    public Optional<PatchNote> findLatestPublished() {
        return repository.findFirstByPublishedTrueOrderByReleaseDateDesc();
    }

    public Optional<String> findLatestPublishedDownloadUrl() {
        return findLatestPublished()
            .map(PatchNote::getDownloadUrl)
            .filter(url -> url != null && !url.isBlank());
    }

    @Transactional
    public String recordDownload(Long id) {
        PatchNote pn = findPublishedById(id);
        String url = pn.getDownloadUrl();
        if (url == null || url.isBlank()) {
            throw new IllegalArgumentException("patch note has no download url: " + id);
        }
        repository.incrementDownloadCount(id);
        return url;
    }

    @Transactional
    public String recordLatestDownload() {
        PatchNote pn = findLatestPublished()
            .orElseThrow(() -> new IllegalArgumentException("no published patch note"));
        String url = pn.getDownloadUrl();
        if (url == null || url.isBlank()) {
            throw new IllegalArgumentException("latest patch note has no download url");
        }
        repository.incrementDownloadCount(pn.getId());
        return url;
    }

    @Transactional
    public Long create(PatchNoteForm form, Long authorId) {
        PatchNote pn = new PatchNote(form.getVersion(), form.getReleaseDate(), form.getBody(),
                                     authorId, form.isPublished(), form.getDownloadUrl());
        return repository.save(pn).getId();
    }

    @Transactional
    public void update(Long id, PatchNoteForm form) {
        PatchNote pn = findById(id);
        pn.update(form.getVersion(), form.getReleaseDate(), form.getBody(),
                  form.isPublished(), form.getDownloadUrl());
    }

    @Transactional
    public void delete(Long id) {
        repository.deleteById(id);
    }

    public long countAll() {
        return repository.count();
    }
}

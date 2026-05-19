package com.lostmemory.website.patchnote.repository;

import com.lostmemory.website.patchnote.entity.PatchNote;
import java.util.List;
import org.springframework.data.jpa.repository.JpaRepository;

public interface PatchNoteRepository extends JpaRepository<PatchNote, Long> {

    List<PatchNote> findByPublishedTrueOrderByReleaseDateDesc();

    List<PatchNote> findAllByOrderByReleaseDateDesc();
}

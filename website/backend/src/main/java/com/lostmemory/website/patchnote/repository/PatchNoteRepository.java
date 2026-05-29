package com.lostmemory.website.patchnote.repository;

import com.lostmemory.website.patchnote.entity.PatchNote;
import java.util.List;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface PatchNoteRepository extends JpaRepository<PatchNote, Long> {

    List<PatchNote> findByPublishedTrueOrderByReleaseDateDesc();

    List<PatchNote> findAllByOrderByReleaseDateDesc();

    Optional<PatchNote> findFirstByPublishedTrueOrderByReleaseDateDesc();

    @Modifying
    @Query("update PatchNote p set p.downloadCount = p.downloadCount + 1 where p.id = :id")
    int incrementDownloadCount(@Param("id") Long id);
}

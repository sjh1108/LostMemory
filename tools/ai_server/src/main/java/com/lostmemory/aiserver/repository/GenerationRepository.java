package com.lostmemory.aiserver.repository;

import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.lostmemory.aiserver.generation.GenerationEntity;

public interface GenerationRepository extends JpaRepository<GenerationEntity, Long> {

    Optional<GenerationEntity> findByPromptId(String promptId);
}

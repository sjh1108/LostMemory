package com.lostmemory.aiserver.generation;

import java.time.Instant;
import java.util.UUID;

import org.springframework.stereotype.Service;

@Service
public class GenerationService {

    public GenerationRequestAcceptedResponse accept(CreateGenerationRequest request) {
        return new GenerationRequestAcceptedResponse(
                UUID.randomUUID().toString(),
                GenerationRequestStatus.RECEIVED,
                request.workflowId(),
                normalizeUserId(request.userId()),
                request.prompt(),
                Instant.now());
    }

    private String normalizeUserId(String userId) {
        if (userId == null || userId.isBlank()) {
            return null;
        }

        return userId;
    }
}

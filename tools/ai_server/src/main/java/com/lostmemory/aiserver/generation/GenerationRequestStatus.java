package com.lostmemory.aiserver.generation;

import io.swagger.v3.oas.annotations.media.Schema;

@Schema(description = "Current handling status for a generation request draft")
public enum GenerationRequestStatus {
    RECEIVED
}

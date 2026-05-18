package com.lostmemory.aiserver.generation;

import java.util.List;

record GenerationHistorySnapshot(
        String promptId,
        boolean historyFound,
        boolean completed,
        String statusText,
        GenerationOutputImage outputImage,
        List<String> messageTypes
) {
}

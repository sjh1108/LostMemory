package com.lostmemory.aiserver.generation;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;
import java.util.Map;

import org.junit.jupiter.api.Test;

class GenerationHistoryParserTest {

    private final GenerationHistoryParser parser = new GenerationHistoryParser();

    @Test
    void parseReadsCompletedStatusAndFirstOutputImageWithoutNodeIdHardcoding() {
        GenerationHistorySnapshot snapshot = parser.parse(
                "prompt-001",
                Map.of(
                        "prompt-001", Map.of(
                                "outputs", Map.of(
                                        "5", Map.of("images", List.of()),
                                        "8", Map.of("images", List.of(
                                                Map.of(
                                                        "filename", "AI301_Test3_PromptSample_00001_.png",
                                                        "subfolder", "",
                                                        "type", "output")))),
                                "status", Map.of(
                                        "completed", true,
                                        "status_str", "success",
                                        "messages", List.of(
                                                List.of("execution_start", Map.of()),
                                                List.of("execution_success", Map.of()))))));

        assertThat(snapshot.historyFound()).isTrue();
        assertThat(snapshot.completed()).isTrue();
        assertThat(snapshot.statusText()).isEqualTo("success");
        assertThat(snapshot.outputImage()).isNotNull();
        assertThat(snapshot.outputImage().filename()).isEqualTo("AI301_Test3_PromptSample_00001_.png");
        assertThat(snapshot.outputImage().subfolder()).isEmpty();
        assertThat(snapshot.outputImage().type()).isEqualTo("output");
        assertThat(snapshot.messageTypes()).containsExactly("execution_start", "execution_success");
    }

    @Test
    void parseReturnsPendingSnapshotWhenPromptIdIsMissing() {
        GenerationHistorySnapshot snapshot = parser.parse("missing-prompt", Map.of());

        assertThat(snapshot.historyFound()).isFalse();
        assertThat(snapshot.completed()).isFalse();
        assertThat(snapshot.outputImage()).isNull();
        assertThat(snapshot.messageTypes()).isEmpty();
    }
}

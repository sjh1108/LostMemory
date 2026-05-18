package com.lostmemory.aiserver.generation;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

class PromptSummaryExtractorTest {

    private final PromptSummaryExtractor promptSummaryExtractor = new PromptSummaryExtractor();

    @Test
    void extractNormalizesWhitespace() {
        String summary = promptSummaryExtractor.extract("pixel   art mage \n blue robe\tidle pose");

        assertThat(summary).isEqualTo("pixel art mage blue robe idle pose");
    }

    @Test
    void extractTruncatesLongPromptToConfiguredLength() {
        String summary = promptSummaryExtractor.extract(
                "pixel art mage girl with blue robe and silver staff standing in front view with simple background and clean outline");

        assertThat(summary.length()).isLessThanOrEqualTo(PromptSummaryExtractor.MAX_SUMMARY_LENGTH);
    }

    @Test
    void extractReturnsFallbackWhenPromptIsBlank() {
        assertThat(promptSummaryExtractor.extract("   ")).isEqualTo(PromptSummaryExtractor.EMPTY_SUMMARY);
    }
}

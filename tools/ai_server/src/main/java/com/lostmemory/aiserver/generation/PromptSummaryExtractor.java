package com.lostmemory.aiserver.generation;

import org.springframework.stereotype.Component;

/**
 * prompt_summary는 workflow JSON 조각이 아니라 "사람이 목록에서 바로 읽는 한 줄 요약"이다.
 *
 * 따라서 여기서는:
 * - 공백 정리
 * - 길이 제한
 * - 빈 값 fallback
 * 정도만 담당하고, 별도 NLP 요약은 하지 않는다.
 */
@Component
public class PromptSummaryExtractor {

    static final int MAX_SUMMARY_LENGTH = 100;
    static final String EMPTY_SUMMARY = "prompt unavailable";

    public String extract(String fullPrompt) {
        if (fullPrompt == null) {
            return EMPTY_SUMMARY;
        }

        String normalized = fullPrompt.replaceAll("\\s+", " ").trim();
        if (normalized.isEmpty()) {
            return EMPTY_SUMMARY;
        }

        if (normalized.length() <= MAX_SUMMARY_LENGTH) {
            return normalized;
        }

        // 목록 가독성이 목적이라 잘리는 기준은 deterministic하면 충분하다.
        return normalized.substring(0, MAX_SUMMARY_LENGTH).trim();
    }
}

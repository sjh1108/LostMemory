package com.lostmemory.server.llm.dto;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.Valid;
import jakarta.validation.constraints.DecimalMax;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.Size;

import java.util.List;

/**
 * vLLM Chat Completions 호환 요청 DTO.
 *
 * 화이트리스트 8개 필드 (model, messages, temperature, max_tokens, stream,
 * frequency_penalty, presence_penalty, repetition_penalty) 만 수용 —
 * 그 외는 {@code @JsonIgnoreProperties(ignoreUnknown=false)} 로 UnrecognizedPropertyException
 * 던져 Spring 이 400 으로 매핑 (request smuggling 차단).
 *
 * {@code @JsonInclude(NON_NULL)} 는 LlmProxyService 의 {@code objectMapper.valueToTree(body)}
 * 직렬화 시 null 필드 제외용 — 클라가 안 보낸 sampling 파라미터가 vLLM 으로 {@code null} 로
 * forward 되지 않도록 차단 (vLLM 일부 버전이 null 거절).
 *
 * {@code stream} 은 백엔드가 항상 true 로 강제 forward 하므로 클라가 보내는 값과 무관 —
 * spec 정합 위해 필드 유지.
 *
 * sampling 파라미터 (frequency_penalty / presence_penalty / repetition_penalty):
 * - vLLM SamplingParams 가 네이티브 지원. 백엔드는 그대로 forward.
 * - Anthropic Claude swap 후엔 strip 필요 (LlmProxyService 의 anthropic 분기 별도 task).
 */
@JsonIgnoreProperties(ignoreUnknown = false)
@JsonInclude(JsonInclude.Include.NON_NULL)
public record ChatRequest(
        @Schema(description = "vLLM 등록 모델 ID", example = "bllossom-8b",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        String model,

        @Schema(description = "대화 messages 배열 (system 고정 + user/assistant 교대)",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotEmpty
        @Size(max = 100, message = "messages 는 최대 100 개까지 허용됩니다")
        @Valid
        List<ChatMessage> messages,

        @Schema(description = "샘플링 온도 (0.0~2.0). null 이면 vLLM default", example = "0.75")
        @DecimalMin(value = "0.0", message = "temperature 는 0 이상이어야 합니다")
        @DecimalMax(value = "2.0", message = "temperature 는 2 이하이어야 합니다")
        Double temperature,

        @Schema(description = "최대 출력 토큰 (1~2048). null 이면 vLLM default", example = "250")
        @JsonProperty("max_tokens")
        @Min(value = 1, message = "max_tokens 는 1 이상이어야 합니다")
        @Max(value = 2048, message = "max_tokens 는 2048 이하이어야 합니다")
        Integer maxTokens,

        @Schema(description = "스트리밍 여부 — 백엔드가 true 로 강제하므로 값 무시됨")
        Boolean stream,

        @Schema(description = "OpenAI 표준 frequency penalty (-2.0~2.0). " +
                "자주 등장한 토큰 억제. null 이면 vLLM default", example = "0.5")
        @JsonProperty("frequency_penalty")
        @DecimalMin(value = "-2.0", message = "frequency_penalty 는 -2.0 이상이어야 합니다")
        @DecimalMax(value = "2.0", message = "frequency_penalty 는 2.0 이하이어야 합니다")
        Double frequencyPenalty,

        @Schema(description = "OpenAI 표준 presence penalty (-2.0~2.0). " +
                "이미 등장한 주제 재등장 억제. null 이면 vLLM default", example = "0.4")
        @JsonProperty("presence_penalty")
        @DecimalMin(value = "-2.0", message = "presence_penalty 는 -2.0 이상이어야 합니다")
        @DecimalMax(value = "2.0", message = "presence_penalty 는 2.0 이하이어야 합니다")
        Double presencePenalty,

        @Schema(description = "vLLM 확장 repetition penalty (1.0~2.0). " +
                "누적 반복 강한 억제. null 이면 vLLM default", example = "1.15")
        @JsonProperty("repetition_penalty")
        @DecimalMin(value = "1.0", message = "repetition_penalty 는 1.0 이상이어야 합니다")
        @DecimalMax(value = "2.0", message = "repetition_penalty 는 2.0 이하이어야 합니다")
        Double repetitionPenalty
) {
}

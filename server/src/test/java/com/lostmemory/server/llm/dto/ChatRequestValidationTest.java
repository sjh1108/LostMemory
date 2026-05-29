package com.lostmemory.server.llm.dto;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.exc.UnrecognizedPropertyException;
import jakarta.validation.ConstraintViolation;
import jakarta.validation.Validation;
import jakarta.validation.Validator;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.Set;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

/**
 * ChatRequest/ChatMessage validation + Jackson 화이트리스트 검증.
 * Validator 단독 사용 — Spring context 띄울 필요 없음.
 */
class ChatRequestValidationTest {

    private static Validator validator;
    private static ObjectMapper objectMapper;

    @BeforeAll
    static void initValidator() {
        validator = Validation.buildDefaultValidatorFactory().getValidator();
        objectMapper = new ObjectMapper();
    }

    private ChatRequest validRequest() {
        return new ChatRequest(
                "bllossom-8b",
                List.of(new ChatMessage("user", "안녕")),
                0.75,
                250,
                true,
                null,
                null,
                null
        );
    }

    @Test
    @DisplayName("정상 body 는 violation 0")
    void validRequest_passes() {
        Set<ConstraintViolation<ChatRequest>> violations = validator.validate(validRequest());
        assertThat(violations).isEmpty();
    }

    @Test
    @DisplayName("sampling 3개 모두 정상 값이면 violation 0")
    void samplingValid_passes() {
        ChatRequest req = new ChatRequest(
                "bllossom-8b",
                List.of(new ChatMessage("user", "x")),
                0.75, 250, true,
                0.5, 0.4, 1.15
        );
        assertThat(validator.validate(req)).isEmpty();
    }

    @Test
    @DisplayName("sampling 3개 모두 null (생략) 이어도 violation 0 — 기존 호환성")
    void samplingNull_passes() {
        ChatRequest req = new ChatRequest(
                "bllossom-8b",
                List.of(new ChatMessage("user", "x")),
                0.75, 250, true,
                null, null, null
        );
        assertThat(validator.validate(req)).isEmpty();
    }

    @Test
    @DisplayName("model 이 빈 문자열이면 @NotBlank violation")
    void blankModel_fails() {
        ChatRequest req = new ChatRequest(
                "", List.of(new ChatMessage("user", "x")), 0.75, 250, true,
                null, null, null);
        assertThat(validator.validate(req))
                .anyMatch(v -> v.getPropertyPath().toString().equals("model"));
    }

    @Test
    @DisplayName("messages 가 빈 배열이면 @NotEmpty violation")
    void emptyMessages_fails() {
        ChatRequest req = new ChatRequest("bllossom-8b", List.of(), 0.75, 250, true,
                null, null, null);
        assertThat(validator.validate(req))
                .anyMatch(v -> v.getPropertyPath().toString().equals("messages"));
    }

    @Test
    @DisplayName("role 이 system/user/assistant 외이면 @Pattern violation")
    void invalidRole_fails() {
        ChatRequest req = new ChatRequest(
                "bllossom-8b",
                List.of(new ChatMessage("tool", "x")),
                0.75, 250, true,
                null, null, null);
        assertThat(validator.validate(req))
                .anyMatch(v -> v.getPropertyPath().toString().contains("role"));
    }

    @Test
    @DisplayName("max_tokens > 2048 이면 @Max violation")
    void maxTokensTooLarge_fails() {
        ChatRequest req = new ChatRequest(
                "bllossom-8b",
                List.of(new ChatMessage("user", "x")),
                0.75, 99_999, true,
                null, null, null);
        assertThat(validator.validate(req))
                .anyMatch(v -> v.getPropertyPath().toString().equals("maxTokens"));
    }

    @Test
    @DisplayName("temperature > 2.0 이면 @DecimalMax violation")
    void temperatureTooHigh_fails() {
        ChatRequest req = new ChatRequest(
                "bllossom-8b",
                List.of(new ChatMessage("user", "x")),
                3.0, 250, true,
                null, null, null);
        assertThat(validator.validate(req))
                .anyMatch(v -> v.getPropertyPath().toString().equals("temperature"));
    }

    @Test
    @DisplayName("frequency_penalty > 2.0 이면 @DecimalMax violation")
    void frequencyPenaltyTooHigh_fails() {
        ChatRequest req = new ChatRequest(
                "bllossom-8b",
                List.of(new ChatMessage("user", "x")),
                0.75, 250, true,
                3.0, null, null);
        assertThat(validator.validate(req))
                .anyMatch(v -> v.getPropertyPath().toString().equals("frequencyPenalty"));
    }

    @Test
    @DisplayName("frequency_penalty < -2.0 이면 @DecimalMin violation")
    void frequencyPenaltyTooLow_fails() {
        ChatRequest req = new ChatRequest(
                "bllossom-8b",
                List.of(new ChatMessage("user", "x")),
                0.75, 250, true,
                -3.0, null, null);
        assertThat(validator.validate(req))
                .anyMatch(v -> v.getPropertyPath().toString().equals("frequencyPenalty"));
    }

    @Test
    @DisplayName("repetition_penalty < 1.0 이면 @DecimalMin violation")
    void repetitionPenaltyTooLow_fails() {
        ChatRequest req = new ChatRequest(
                "bllossom-8b",
                List.of(new ChatMessage("user", "x")),
                0.75, 250, true,
                null, null, 0.5);
        assertThat(validator.validate(req))
                .anyMatch(v -> v.getPropertyPath().toString().equals("repetitionPenalty"));
    }

    @Test
    @DisplayName("repetition_penalty > 2.0 이면 @DecimalMax violation")
    void repetitionPenaltyTooHigh_fails() {
        ChatRequest req = new ChatRequest(
                "bllossom-8b",
                List.of(new ChatMessage("user", "x")),
                0.75, 250, true,
                null, null, 3.0);
        assertThat(validator.validate(req))
                .anyMatch(v -> v.getPropertyPath().toString().equals("repetitionPenalty"));
    }

    @Test
    @DisplayName("unknown 필드가 있으면 Jackson 이 UnrecognizedPropertyException 던진다 (화이트리스트 강제)")
    void unknownField_jacksonRejects() {
        String jsonWithUnknown = """
                {
                  "model": "bllossom-8b",
                  "messages": [{"role":"user","content":"x"}],
                  "evil_param": "smuggled"
                }
                """;
        assertThatThrownBy(() -> objectMapper.readValue(jsonWithUnknown, ChatRequest.class))
                .isInstanceOf(UnrecognizedPropertyException.class);
    }

    @Test
    @DisplayName("max_tokens 는 JSON snake_case 로 들어와도 maxTokens 필드로 매핑된다")
    void maxTokensSnakeCase_mapped() throws Exception {
        String json = """
                {
                  "model": "bllossom-8b",
                  "messages": [{"role":"user","content":"x"}],
                  "max_tokens": 500
                }
                """;
        ChatRequest parsed = objectMapper.readValue(json, ChatRequest.class);
        assertThat(parsed.maxTokens()).isEqualTo(500);
    }

    @Test
    @DisplayName("sampling 3개 snake_case JSON 이 record 필드로 매핑된다")
    void samplingSnakeCase_mapped() throws Exception {
        String json = """
                {
                  "model": "bllossom-8b",
                  "messages": [{"role":"user","content":"x"}],
                  "frequency_penalty": 0.5,
                  "presence_penalty": 0.4,
                  "repetition_penalty": 1.15
                }
                """;
        ChatRequest parsed = objectMapper.readValue(json, ChatRequest.class);
        assertThat(parsed.frequencyPenalty()).isEqualTo(0.5);
        assertThat(parsed.presencePenalty()).isEqualTo(0.4);
        assertThat(parsed.repetitionPenalty()).isEqualTo(1.15);
    }

    @Test
    @DisplayName("@JsonInclude(NON_NULL) — null sampling 필드는 직렬화에서 제외된다 (vLLM forward 시 null 안 감)")
    void samplingNullExcludedOnSerialize() throws Exception {
        ChatRequest req = new ChatRequest(
                "bllossom-8b",
                List.of(new ChatMessage("user", "x")),
                0.75, 250, true,
                null, null, null);
        String json = objectMapper.writeValueAsString(req);
        assertThat(json).doesNotContain("frequency_penalty");
        assertThat(json).doesNotContain("presence_penalty");
        assertThat(json).doesNotContain("repetition_penalty");
    }
}

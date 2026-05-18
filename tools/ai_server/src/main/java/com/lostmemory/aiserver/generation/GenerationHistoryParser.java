package com.lostmemory.aiserver.generation;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;

import org.springframework.stereotype.Component;

@Component
public class GenerationHistoryParser {

    public GenerationHistorySnapshot parse(String promptId, Map<String, Object> responseBody) {
        Object historyObject = responseBody.get(promptId);

        if (!(historyObject instanceof Map<?, ?> historyEntry)) {
            return new GenerationHistorySnapshot(promptId, false, false, null, null, List.of());
        }

        Map<String, Object> historyMap = castMap(historyEntry);
        Map<String, Object> statusMap = readMap(historyMap.get("status"));

        return new GenerationHistorySnapshot(
                promptId,
                true,
                Boolean.TRUE.equals(statusMap.get("completed")),
                asString(statusMap.get("status_str")),
                extractFirstOutput(readMap(historyMap.get("outputs"))),
                extractMessageTypes(statusMap.get("messages")));
    }

    private GenerationOutputImage extractFirstOutput(Map<String, Object> outputs) {
        for (Object nodeOutput : outputs.values()) {
            if (!(nodeOutput instanceof Map<?, ?> outputEntry)) {
                continue;
            }

            Object imagesObject = outputEntry.get("images");
            if (!(imagesObject instanceof List<?> images) || images.isEmpty()) {
                continue;
            }

            Object firstImage = images.get(0);
            if (!(firstImage instanceof Map<?, ?> imageEntry)) {
                continue;
            }

            return new GenerationOutputImage(
                    asString(imageEntry.get("filename")),
                    asString(imageEntry.get("subfolder")),
                    asString(imageEntry.get("type")));
        }

        return null;
    }

    private List<String> extractMessageTypes(Object messagesObject) {
        if (!(messagesObject instanceof List<?> messages)) {
            return List.of();
        }

        List<String> messageTypes = new ArrayList<>();
        for (Object message : messages) {
            if (!(message instanceof List<?> tuple) || tuple.isEmpty()) {
                continue;
            }

            Object type = tuple.get(0);
            if (type instanceof String value && !value.isBlank()) {
                messageTypes.add(value);
            }
        }

        return Collections.unmodifiableList(messageTypes);
    }

    @SuppressWarnings("unchecked")
    private Map<String, Object> castMap(Map<?, ?> map) {
        return (Map<String, Object>) map;
    }

    private Map<String, Object> readMap(Object value) {
        if (value instanceof Map<?, ?> map) {
            return castMap(map);
        }

        return Map.of();
    }

    private String asString(Object value) {
        if (value instanceof String text) {
            return text;
        }

        return null;
    }
}

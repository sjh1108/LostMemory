package com.lostmemory.aiserver.generation;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;

import org.springframework.stereotype.Component;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.NullNode;
import com.fasterxml.jackson.databind.node.ObjectNode;

/**
 * workflow snapshot upsert 기준이 되는 hash를 계산한다.
 *
 * 단순 toString()이나 파일 원본 문자열을 그대로 hash하면
 * - key 순서 차이
 * - formatter 차이
 * - mapper 구현 차이
 * 때문에 같은 workflow인데도 다른 hash가 나올 수 있다.
 *
 * 그래서 object key를 정렬한 canonical JSON을 먼저 만들고 SHA-256을 계산한다.
 */
@Component
public class WorkflowHashCalculator {

    private final ObjectMapper objectMapper;

    public WorkflowHashCalculator(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    public String calculate(JsonNode workflowJson) {
        try {
            JsonNode canonicalNode = canonicalize(workflowJson);
            byte[] digest = MessageDigest.getInstance("SHA-256")
                    .digest(objectMapper.writeValueAsString(canonicalNode).getBytes(StandardCharsets.UTF_8));

            return toHex(digest);
        } catch (JsonProcessingException exception) {
            throw new IllegalStateException("Failed to serialize workflow snapshot JSON for hashing.", exception);
        } catch (NoSuchAlgorithmException exception) {
            throw new IllegalStateException("SHA-256 is not available in the current JVM.", exception);
        }
    }

    private JsonNode canonicalize(JsonNode node) {
        if (node == null || node.isNull()) {
            return NullNode.getInstance();
        }

        if (node.isObject()) {
            ObjectNode sorted = objectMapper.createObjectNode();
            List<String> fieldNames = new ArrayList<>();
            node.fieldNames().forEachRemaining(fieldNames::add);
            fieldNames.sort(Comparator.naturalOrder());

            for (String fieldName : fieldNames) {
                sorted.set(fieldName, canonicalize(node.get(fieldName)));
            }

            return sorted;
        }

        if (node.isArray()) {
            ArrayNode array = objectMapper.createArrayNode();
            for (JsonNode element : node) {
                array.add(canonicalize(element));
            }
            return array;
        }

        return node.deepCopy();
    }

    private String toHex(byte[] bytes) {
        StringBuilder builder = new StringBuilder(bytes.length * 2);
        for (byte value : bytes) {
            builder.append(String.format("%02x", value));
        }
        return builder.toString();
    }
}

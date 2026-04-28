package com.lostmemory.aiserver.generation;

import java.util.Map;

import com.fasterxml.jackson.databind.JsonNode;

/**
 * ComfyUI submit payload와 DB 저장용 workflow snapshot 메타데이터를 같이 묶는다.
 *
 * 같은 template에서 두 결과를 동시에 만드는 이유:
 * - ComfyUI로 보내는 payload는 requestId, seed, filename_prefix 같은 실행값이 들어간다.
 * - DB snapshot은 hash/upsert를 위해 실행값이 섞이지 않은 "기준 template"를 써야 한다.
 */
public record PromptAssemblyResult(
        Map<String, Object> promptRequest,
        JsonNode workflowSnapshotJson,
        String workflowName,
        String workflowVersion,
        String sourceFilename,
        String primaryModelName,
        JsonNode modelMetadataJson
) {
}

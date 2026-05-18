package com.lostmemory.aiserver.generation;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

class WorkflowHashCalculatorTest {

    private final ObjectMapper objectMapper = new ObjectMapper();
    private final WorkflowHashCalculator workflowHashCalculator = new WorkflowHashCalculator(objectMapper);

    @Test
    void calculateReturnsSameHashWhenObjectFieldOrderDiffers() throws Exception {
        JsonNode left = objectMapper.readTree("""
                {
                  "prompt": {
                    "1": {
                      "inputs": {
                        "unet_name": "model-a",
                        "weight_dtype": "default"
                      },
                      "class_type": "UNETLoader"
                    }
                  },
                  "client_id": "sample-client"
                }
                """);

        JsonNode right = objectMapper.readTree("""
                {
                  "client_id": "sample-client",
                  "prompt": {
                    "1": {
                      "class_type": "UNETLoader",
                      "inputs": {
                        "weight_dtype": "default",
                        "unet_name": "model-a"
                      }
                    }
                  }
                }
                """);

        assertThat(workflowHashCalculator.calculate(left))
                .isEqualTo(workflowHashCalculator.calculate(right));
    }
}

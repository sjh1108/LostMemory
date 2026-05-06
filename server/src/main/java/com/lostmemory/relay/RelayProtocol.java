package com.lostmemory.relay;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.io.IOException;

/**
 * Relay 핸드셰이크 메시지 정의 + Jackson 직렬화 유틸.
 *
 * 핸드셰이크는 JSON (디버깅 쉬움). 데이터 패킷은 NGO 의 raw bytes 로,
 * 첫 바이트가 magic byte (RelayHandler.MAGIC_DATA) 면 데이터로 판정해 forward.
 *
 * 클라 → 서버 (HELLO): {"type":"HELLO","token":"<jwt>"}
 * 서버 → 클라 (ACK ok): {"type":"ACK","ok":true,"sessionId":42,"role":"HOST"}
 * 서버 → 클라 (ACK fail): {"type":"ACK","ok":false,"reason":"INVALID_TOKEN"}
 */
public final class RelayProtocol {

    private static final ObjectMapper MAPPER = new ObjectMapper()
            .setSerializationInclusion(JsonInclude.Include.NON_NULL);

    private RelayProtocol() {
    }

    public static HandshakeMessage parseHello(String json) throws IOException {
        return MAPPER.readValue(json, HandshakeMessage.class);
    }

    public static String toJson(Object message) {
        try {
            return MAPPER.writeValueAsString(message);
        } catch (IOException e) {
            throw new IllegalStateException("RelayProtocol JSON 직렬화 실패", e);
        }
    }

    /** 클라 → 서버 첫 패킷 */
    public record HandshakeMessage(String type, String token) {
    }

    /** 서버 → 클라 핸드셰이크 응답 */
    public record AckMessage(String type, boolean ok, Long sessionId, String role, String reason) {

        public static AckMessage ok(Long sessionId, String role) {
            return new AckMessage("ACK", true, sessionId, role, null);
        }

        public static AckMessage fail(String reason) {
            return new AckMessage("ACK", false, null, null, reason);
        }
    }
}

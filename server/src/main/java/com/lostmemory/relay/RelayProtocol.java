package com.lostmemory.relay;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.io.IOException;

/**
 * Relay 컨트롤 메시지 정의 + Jackson 직렬화 유틸.
 *
 * 컨트롤 메시지는 JSON (디버깅 쉬움). 데이터 패킷은 NGO 의 raw bytes 로,
 * 첫 바이트가 magic byte (RelayHandler.MAGIC_DATA) 면 데이터로 판정해 forward.
 *
 * 메시지 종류:
 *   클라 → 서버 (HELLO): {"type":"HELLO","token":"<jwt>"}
 *   클라 → 서버 (BYE):   {"type":"BYE"}                             — 명시적 이탈 신호
 *   서버 → 클라 (ACK ok): {"type":"ACK","ok":true,"sessionId":42,"role":"HOST"}
 *   서버 → 클라 (ACK fail): {"type":"ACK","ok":false,"reason":"INVALID_TOKEN"}
 *   서버 → 클라 (PEER_LEFT): {"type":"PEER_LEFT","userId":N}        — 다른 peer 가 떠났음을 알림
 */
public final class RelayProtocol {

    public static final String TYPE_HELLO = "HELLO";
    public static final String TYPE_BYE = "BYE";
    public static final String TYPE_PING = "PING";
    public static final String TYPE_ACK = "ACK";
    public static final String TYPE_PEER_LEFT = "PEER_LEFT";

    private static final ObjectMapper MAPPER = new ObjectMapper()
            .setSerializationInclusion(JsonInclude.Include.NON_NULL);

    private RelayProtocol() {
    }

    /** 컨트롤 메시지의 type 만 추출 — 디스패치용. JSON 파싱 실패 시 null */
    public static String peekType(String json) {
        try {
            return MAPPER.readTree(json).path("type").asText(null);
        } catch (IOException e) {
            return null;
        }
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

    /** 클라 → 서버 첫 패킷 (HELLO) */
    public record HandshakeMessage(String type, String token) {
    }

    /** 서버 → 클라 핸드셰이크 응답 */
    public record AckMessage(String type, boolean ok, Long sessionId, String role, String reason) {

        public static AckMessage ok(Long sessionId, String role) {
            return new AckMessage(TYPE_ACK, true, sessionId, role, null);
        }

        public static AckMessage fail(String reason) {
            return new AckMessage(TYPE_ACK, false, null, null, reason);
        }
    }

    /** 서버 → 클라: 같은 세션의 다른 peer 가 떠났음을 알림 */
    public record PeerLeftMessage(String type, Long userId) {

        public static PeerLeftMessage of(Long userId) {
            return new PeerLeftMessage(TYPE_PEER_LEFT, userId);
        }
    }
}

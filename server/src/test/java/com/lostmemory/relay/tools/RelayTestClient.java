package com.lostmemory.relay.tools;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.Map;

/**
 * 자체 Relay PoC 검증용 테스트 클라이언트.
 *
 * 실행 전제:
 *   - ServerApplication 이 localhost:18080 에서 실행 중
 *   - RelayApplication 이 localhost:7777 에서 실행 중
 *   - testuser / password123 계정 존재 (없으면 미리 signup)
 *   - testuser01 / password123 / 테스터01 계정 존재 (없으면 본 코드가 자동 signup)
 *
 * 검증 시나리오:
 *   1. 호스트 (testuser) 로그인 → POST /sessions → sessionToken 획득
 *   2. 게스트 (testuser01) 로그인 → POST /sessions/{id}/join → 게스트 sessionToken 획득
 *   3. UDP 소켓 2개 (호스트·게스트) 로 각각 핸드셰이크 → ACK 검증
 *   4. 호스트가 데이터 패킷 전송 → 게스트가 forward 받음 검증
 *   5. 게스트가 데이터 패킷 전송 → 호스트가 forward 받음 검증
 *   6. 세션 정리 (DELETE)
 *
 * 실행 방법:
 *   IntelliJ 에서 본 클래스 우클릭 → Run 'RelayTestClient.main()'
 *   또는 gradlew 에 task 추가해서 실행
 */
public class RelayTestClient {

    private static final String BASE_URL = "http://localhost:18080/api";
    private static final String RELAY_HOST = "localhost";
    private static final int RELAY_PORT = 7777;
    private static final byte MAGIC_DATA = 0x01;

    private static final HttpClient http = HttpClient.newBuilder()
            .connectTimeout(Duration.ofSeconds(5))
            .build();
    private static final ObjectMapper mapper = new ObjectMapper();

    public static void main(String[] args) throws Exception {
        System.out.println("==== Relay Test Client 시작 ====");

        // 1. 호스트 login
        String hostToken = login("testuser", "password123");
        System.out.println("[Host] login OK");

        // 2. 게스트 signup (이미 있으면 무시) + login
        trySignup("testuser01", "password123", "테스터01");
        String guestToken = login("testuser01", "password123");
        System.out.println("[Guest] login OK");

        // 3. 호스트가 세션 생성
        String code = "TEST" + (System.currentTimeMillis() % 100000);
        SessionInfo hostSess = createSession(hostToken, 4, code);
        System.out.println("[Host] session created. id=" + hostSess.sessionId + ", code=" + code);

        try {
            // 4. 게스트 join
            SessionInfo guestSess = joinSession(guestToken, hostSess.sessionId, code);
            System.out.println("[Guest] session joined");

            // 5. UDP 소켓 두 개 열고 양쪽 핸드셰이크
            try (DatagramSocket hostSock = new DatagramSocket();
                 DatagramSocket guestSock = new DatagramSocket()) {

                hostSock.setSoTimeout(3000);
                guestSock.setSoTimeout(3000);

                doHandshake(hostSock, hostSess.sessionToken, "Host");
                doHandshake(guestSock, guestSess.sessionToken, "Guest");

                // 6. Host → Guest forward 검증
                String hostMsg = "Hello from host";
                sendData(hostSock, hostMsg.getBytes(StandardCharsets.UTF_8));
                String received1 = receiveData(guestSock);
                check("[Guest] forward 수신",
                        hostMsg.equals(received1),
                        "expected '" + hostMsg + "' got '" + received1 + "'");

                // 7. Guest → Host forward 검증
                String guestMsg = "Hello from guest";
                sendData(guestSock, guestMsg.getBytes(StandardCharsets.UTF_8));
                String received2 = receiveData(hostSock);
                check("[Host] forward 수신",
                        guestMsg.equals(received2),
                        "expected '" + guestMsg + "' got '" + received2 + "'");

                // 8. 잘못된 토큰으로 핸드셰이크 거절 검증
                try (DatagramSocket badSock = new DatagramSocket()) {
                    badSock.setSoTimeout(3000);
                    boolean rejected = doHandshakeExpectFail(badSock, "this.is.not.a.valid.jwt", "Bad");
                    check("[Bad] 잘못된 토큰 거절",
                            rejected,
                            "잘못된 토큰인데 ACK ok=true 떨어짐 — Relay JWT 검증 미동작");
                }
            }
        } finally {
            // 9. cleanup
            try {
                deleteSession(hostToken, hostSess.sessionId);
                System.out.println("[Host] session deleted");
            } catch (Exception e) {
                System.err.println("[Host] cleanup 실패 (무시): " + e.getMessage());
            }
        }

        System.out.println("==== ✅ 모든 체크 통과 ====");
    }

    // ---------- HTTP helpers ----------

    private static String login(String loginId, String password) throws Exception {
        String body = mapper.writeValueAsString(Map.of("loginId", loginId, "password", password));
        HttpResponse<String> resp = postJson("/auth/login", body, null);
        if (resp.statusCode() != 200) {
            throw new RuntimeException("login 실패: " + resp.statusCode() + " " + resp.body());
        }
        return mapper.readTree(resp.body()).path("data").path("accessToken").asText();
    }

    private static void trySignup(String loginId, String password, String nickname) throws Exception {
        String body = mapper.writeValueAsString(Map.of(
                "loginId", loginId, "password", password, "nickname", nickname));
        postJson("/auth/signup", body, null);
        // 결과 무시 — 이미 존재하는 계정이면 409 떨어지고 login 으로 진행
    }

    private static SessionInfo createSession(String token, int maxPlayers, String privateCode) throws Exception {
        String body = mapper.writeValueAsString(Map.of(
                "maxPlayers", maxPlayers, "privateCode", privateCode));
        HttpResponse<String> resp = postJson("/sessions", body, token);
        if (resp.statusCode() != 200) {
            throw new RuntimeException("createSession 실패: " + resp.statusCode() + " " + resp.body());
        }
        return parseSessionInfo(resp.body());
    }

    private static SessionInfo joinSession(String token, long sessionId, String privateCode) throws Exception {
        String body = mapper.writeValueAsString(Map.of("privateCode", privateCode));
        HttpResponse<String> resp = postJson("/sessions/" + sessionId + "/join", body, token);
        if (resp.statusCode() != 200) {
            throw new RuntimeException("joinSession 실패: " + resp.statusCode() + " " + resp.body());
        }
        return parseSessionInfo(resp.body());
    }

    private static void deleteSession(String token, long sessionId) throws Exception {
        HttpRequest req = HttpRequest.newBuilder()
                .uri(URI.create(BASE_URL + "/sessions/" + sessionId))
                .header("Authorization", "Bearer " + token)
                .DELETE()
                .build();
        http.send(req, HttpResponse.BodyHandlers.ofString());
    }

    private static HttpResponse<String> postJson(String path, String body, String bearer) throws Exception {
        HttpRequest.Builder b = HttpRequest.newBuilder()
                .uri(URI.create(BASE_URL + path))
                .header("Content-Type", "application/json; charset=UTF-8")
                .POST(HttpRequest.BodyPublishers.ofString(body, StandardCharsets.UTF_8));
        if (bearer != null) {
            b.header("Authorization", "Bearer " + bearer);
        }
        return http.send(b.build(), HttpResponse.BodyHandlers.ofString());
    }

    private static SessionInfo parseSessionInfo(String json) throws Exception {
        JsonNode tree = mapper.readTree(json).path("data");
        return new SessionInfo(
                tree.path("sessionId").asLong(),
                tree.path("sessionToken").asText(),
                tree.path("privateCode").asText()
        );
    }

    // ---------- UDP helpers ----------

    private static void doHandshake(DatagramSocket sock, String sessionToken, String label) throws Exception {
        String hello = mapper.writeValueAsString(Map.of("type", "HELLO", "token", sessionToken));
        InetAddress addr = InetAddress.getByName(RELAY_HOST);
        byte[] payload = hello.getBytes(StandardCharsets.UTF_8);
        sock.send(new DatagramPacket(payload, payload.length, addr, RELAY_PORT));

        byte[] buf = new byte[4096];
        DatagramPacket pkt = new DatagramPacket(buf, buf.length);
        sock.receive(pkt);
        String json = new String(pkt.getData(), pkt.getOffset(), pkt.getLength(), StandardCharsets.UTF_8);
        JsonNode ack = mapper.readTree(json);
        boolean ok = ack.path("ok").asBoolean();
        if (!ok) {
            throw new RuntimeException("[" + label + "] handshake 거절: " + ack.path("reason").asText());
        }
        System.out.println("[" + label + "] handshake OK. sessionId=" + ack.path("sessionId").asLong()
                + ", role=" + ack.path("role").asText());
    }

    /** 잘못된 토큰으로 핸드셰이크 시도 — ok=false 가 떨어져야 정상 (true 반환) */
    private static boolean doHandshakeExpectFail(DatagramSocket sock, String badToken, String label) throws Exception {
        String hello = mapper.writeValueAsString(Map.of("type", "HELLO", "token", badToken));
        InetAddress addr = InetAddress.getByName(RELAY_HOST);
        byte[] payload = hello.getBytes(StandardCharsets.UTF_8);
        sock.send(new DatagramPacket(payload, payload.length, addr, RELAY_PORT));

        byte[] buf = new byte[4096];
        DatagramPacket pkt = new DatagramPacket(buf, buf.length);
        sock.receive(pkt);
        String json = new String(pkt.getData(), pkt.getOffset(), pkt.getLength(), StandardCharsets.UTF_8);
        JsonNode ack = mapper.readTree(json);
        boolean ok = ack.path("ok").asBoolean();
        System.out.println("[" + label + "] 잘못된 토큰 응답: ok=" + ok
                + ", reason=" + ack.path("reason").asText());
        return !ok;
    }

    private static void sendData(DatagramSocket sock, byte[] payload) throws Exception {
        byte[] withMagic = new byte[payload.length + 1];
        withMagic[0] = MAGIC_DATA;
        System.arraycopy(payload, 0, withMagic, 1, payload.length);
        InetAddress addr = InetAddress.getByName(RELAY_HOST);
        sock.send(new DatagramPacket(withMagic, withMagic.length, addr, RELAY_PORT));
    }

    private static String receiveData(DatagramSocket sock) throws Exception {
        byte[] buf = new byte[4096];
        DatagramPacket pkt = new DatagramPacket(buf, buf.length);
        sock.receive(pkt);
        if (pkt.getLength() < 1 || pkt.getData()[pkt.getOffset()] != MAGIC_DATA) {
            throw new RuntimeException("MAGIC_DATA prefix 누락");
        }
        return new String(pkt.getData(), pkt.getOffset() + 1, pkt.getLength() - 1, StandardCharsets.UTF_8);
    }

    private static void check(String label, boolean cond, String failMsg) {
        if (cond) {
            System.out.println("✅ " + label);
        } else {
            throw new RuntimeException("❌ " + label + " — " + failMsg);
        }
    }

    private record SessionInfo(long sessionId, String sessionToken, String privateCode) {
    }
}

package com.lostmemory.relay;

import org.springframework.stereotype.Component;

import java.net.InetSocketAddress;
import java.util.ArrayList;
import java.util.List;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

/**
 * sessionId 별 peer 집합 + 역방향 인덱스 (peer → sessionId/userId/lastSeenAt).
 *
 * Netty EventLoop 가 단일 스레드라도 향후 멀티 worker 확장 가능성을 위해 동시성 안전 자료구조 사용.
 * Relay 는 stateless 하지만 routing table 만 in-memory 로 보관 — 컨테이너 재기동 시 모든 룸 초기화됨.
 * 클라가 재핸드셰이크하면 자동 복구.
 *
 * lastSeenAt 은 timeout 기반 dead peer 감지에 사용 (cleanup 스케줄러).
 */
@Component
public class RoomRegistry {

    private final ConcurrentHashMap<Long, Set<InetSocketAddress>> sessions = new ConcurrentHashMap<>();
    private final ConcurrentHashMap<InetSocketAddress, PeerEntry> peerIndex = new ConcurrentHashMap<>();

    /** 핸드셰이크 성공 시 peer 등록. 같은 peer 가 다른 세션으로 재등록하면 이전 세션에서 자동 제거. */
    public void register(Long sessionId, Long userId, InetSocketAddress peer) {
        long now = System.currentTimeMillis();
        PeerEntry entry = new PeerEntry(sessionId, userId, now);
        PeerEntry previous = peerIndex.put(peer, entry);
        if (previous != null && !previous.sessionId.equals(sessionId)) {
            removeFromSession(previous.sessionId, peer);
        }
        sessions.computeIfAbsent(sessionId, k -> ConcurrentHashMap.newKeySet()).add(peer);
    }

    /** 데이터 패킷 forward 직전 — 발신자가 등록된 세션 ID 조회. 미등록이면 null */
    public Long findSessionId(InetSocketAddress peer) {
        PeerEntry entry = peerIndex.get(peer);
        return entry == null ? null : entry.sessionId;
    }

    /** peer 의 userId 조회 (PEER_LEFT 메시지 채우기용). 미등록이면 null */
    public Long findUserId(InetSocketAddress peer) {
        PeerEntry entry = peerIndex.get(peer);
        return entry == null ? null : entry.userId;
    }

    /** 같은 세션의 모든 peer (발신자 본인 포함). 호출자가 본인 제외 처리 책임 */
    public Set<InetSocketAddress> peersOf(Long sessionId) {
        Set<InetSocketAddress> peers = sessions.get(sessionId);
        return peers == null ? Set.of() : peers;
    }

    /** 데이터 또는 컨트롤 패킷 수신 시 호출 — lastSeenAt 갱신 */
    public void touch(InetSocketAddress peer) {
        PeerEntry entry = peerIndex.get(peer);
        if (entry != null) {
            entry.lastSeenAt = System.currentTimeMillis();
        }
    }

    /**
     * peer 가 떠날 때 호출 (BYE 수신 시 또는 timeout cleanup 시).
     * 반환값 — 떠난 peer 의 PeerEntry (sessionId/userId 가 PEER_LEFT broadcast 에 필요).
     * 미등록 peer 면 null.
     */
    public PeerEntry unregister(InetSocketAddress peer) {
        PeerEntry entry = peerIndex.remove(peer);
        if (entry != null) {
            removeFromSession(entry.sessionId, peer);
        }
        return entry;
    }

    /**
     * thresholdMillis 이상 송신 없는 peer 들을 찾아 unregister 후 (peer, PeerEntry) 쌍을 반환.
     * cleanup 스케줄러가 호출. 반환된 목록을 기반으로 PEER_LEFT broadcast.
     */
    public List<TimedOutPeer> sweepIdle(long thresholdMillis) {
        long deadline = System.currentTimeMillis() - thresholdMillis;
        List<TimedOutPeer> dead = new ArrayList<>();
        for (var e : peerIndex.entrySet()) {
            if (e.getValue().lastSeenAt < deadline) {
                dead.add(new TimedOutPeer(e.getKey(), e.getValue()));
            }
        }
        for (TimedOutPeer p : dead) {
            unregister(p.peer);
        }
        return dead;
    }

    private void removeFromSession(Long sessionId, InetSocketAddress peer) {
        Set<InetSocketAddress> peers = sessions.get(sessionId);
        if (peers != null) {
            peers.remove(peer);
            if (peers.isEmpty()) {
                sessions.remove(sessionId);
            }
        }
    }

    /** peer 메타. lastSeenAt 만 mutable (단일 EventLoop 갱신 가정 — 동시성 충돌 시 약간의 지연 허용) */
    public static final class PeerEntry {
        public final Long sessionId;
        public final Long userId;
        public volatile long lastSeenAt;

        PeerEntry(Long sessionId, Long userId, long lastSeenAt) {
            this.sessionId = sessionId;
            this.userId = userId;
            this.lastSeenAt = lastSeenAt;
        }
    }

    public record TimedOutPeer(InetSocketAddress peer, PeerEntry entry) {
    }
}

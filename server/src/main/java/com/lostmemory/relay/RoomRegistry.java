package com.lostmemory.relay;

import org.springframework.stereotype.Component;

import java.net.InetSocketAddress;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

/**
 * sessionId 별 peer 집합 + 역방향 인덱스 (peer → sessionId).
 *
 * Netty EventLoop 가 단일 스레드라도 향후 멀티 worker 확장 가능성을 위해 동시성 안전 자료구조 사용.
 * Relay 는 stateless 하지만 routing table 만 in-memory 로 보관 — 컨테이너 재기동 시 모든 룸 초기화됨.
 * 클라가 재핸드셰이크하면 자동 복구.
 */
@Component
public class RoomRegistry {

    private final ConcurrentHashMap<Long, Set<InetSocketAddress>> sessions = new ConcurrentHashMap<>();
    private final ConcurrentHashMap<InetSocketAddress, Long> peerIndex = new ConcurrentHashMap<>();

    /** 핸드셰이크 성공 시 peer 등록. 같은 peer 가 다른 세션으로 재등록하면 이전 세션에서 자동 제거. */
    public void register(Long sessionId, InetSocketAddress peer) {
        Long previous = peerIndex.put(peer, sessionId);
        if (previous != null && !previous.equals(sessionId)) {
            removeFromSession(previous, peer);
        }
        sessions.computeIfAbsent(sessionId, k -> ConcurrentHashMap.newKeySet()).add(peer);
    }

    /** 데이터 패킷 forward 직전 — 발신자가 등록된 세션 ID 조회. 미등록이면 null */
    public Long findSessionId(InetSocketAddress peer) {
        return peerIndex.get(peer);
    }

    /** 같은 세션의 모든 peer (발신자 본인 포함). 호출자가 본인 제외 처리 책임 */
    public Set<InetSocketAddress> peersOf(Long sessionId) {
        Set<InetSocketAddress> peers = sessions.get(sessionId);
        return peers == null ? Set.of() : peers;
    }

    /** peer 가 떠날 때 호출 (현재 미사용, 향후 timeout/disconnect 감지 시) */
    public void unregister(InetSocketAddress peer) {
        Long sessionId = peerIndex.remove(peer);
        if (sessionId != null) {
            removeFromSession(sessionId, peer);
        }
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
}

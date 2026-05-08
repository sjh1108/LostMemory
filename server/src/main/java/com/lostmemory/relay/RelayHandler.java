package com.lostmemory.relay;

import com.lostmemory.server.global.security.JwtProvider;
import com.lostmemory.server.global.security.SessionPrincipal;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;
import io.netty.channel.Channel;
import io.netty.channel.ChannelHandler;
import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.SimpleChannelInboundHandler;
import io.netty.channel.socket.DatagramPacket;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;

import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.util.Set;

/**
 * UDP 패킷 핸들러.
 * - 컨트롤 패킷 (JSON, 첫 byte 가 MAGIC_DATA 가 아닌 경우): HELLO 또는 BYE 로 디스패치
 * - 데이터 패킷 (앞 바이트가 MAGIC_DATA): 같은 sessionId 의 다른 peer 들에게 forward
 * - PEER_LEFT broadcast: BYE 수신 시 즉시, timeout cleanup 시 지연
 *
 * NioDatagramChannel 은 단일 채널이라 인스턴스를 공유해도 안전 — @Sharable 필요.
 */
@Slf4j
@Component
@ChannelHandler.Sharable
@RequiredArgsConstructor
public class RelayHandler extends SimpleChannelInboundHandler<DatagramPacket> {

    /** 데이터 패킷 식별 magic byte. JSON ('{', '[') 와 구분. NGO 측도 이 prefix 를 붙여서 보내야 함 */
    public static final byte MAGIC_DATA = 0x01;

    private final RoomRegistry rooms;
    private final JwtProvider jwtProvider;

    /** RelayServer 가 bind 후 주입. cleanup 스케줄러가 PEER_LEFT broadcast 시 사용. */
    private volatile Channel channel;

    public void setChannel(Channel channel) {
        this.channel = channel;
    }

    @Override
    protected void channelRead0(ChannelHandlerContext ctx, DatagramPacket packet) {
        ByteBuf content = packet.content();
        if (content.readableBytes() == 0) {
            return;
        }

        InetSocketAddress sender = packet.sender();
        byte first = content.getByte(content.readerIndex());

        if (first == MAGIC_DATA) {
            forwardData(ctx, sender, content);
        } else {
            handleControlMessage(ctx, sender, content);
        }
    }

    private void handleControlMessage(ChannelHandlerContext ctx, InetSocketAddress sender, ByteBuf content) {
        String json = content.toString(StandardCharsets.UTF_8);
        String type = RelayProtocol.peekType(json);

        if (RelayProtocol.TYPE_HELLO.equals(type)) {
            handleHandshake(ctx, sender, json);
        } else if (RelayProtocol.TYPE_BYE.equals(type)) {
            handleBye(ctx, sender);
        } else if (RelayProtocol.TYPE_PING.equals(type)) {
            rooms.touch(sender); // lastSeenAt 갱신만, 응답 없음
        } else {
            log.debug("[Relay] Unknown control message from {}: type={}", sender, type);
        }
    }

    private void handleHandshake(ChannelHandlerContext ctx, InetSocketAddress sender, String json) {
        try {
            RelayProtocol.HandshakeMessage hello = RelayProtocol.parseHello(json);
            if (hello.token() == null) {
                throw new IllegalArgumentException("token 누락");
            }

            SessionPrincipal principal = jwtProvider.parseSessionToken(hello.token());

            rooms.register(principal.sessionId(), principal.userId(), sender);
            log.info("[Relay] Peer joined: sessionId={}, userId={}, role={}, addr={}",
                    principal.sessionId(), principal.userId(), principal.role(), sender);

            String ackJson = RelayProtocol.toJson(
                    RelayProtocol.AckMessage.ok(principal.sessionId(), principal.role())
            );
            sendJson(ctx, sender, ackJson);
        } catch (Exception e) {
            log.warn("[Relay] Handshake failed from {}: {}", sender, e.getMessage());
            String ackJson = RelayProtocol.toJson(RelayProtocol.AckMessage.fail(e.getMessage()));
            sendJson(ctx, sender, ackJson);
        }
    }

    private void handleBye(ChannelHandlerContext ctx, InetSocketAddress sender) {
        RoomRegistry.PeerEntry entry = rooms.unregister(sender);
        if (entry == null) {
            log.debug("[Relay] BYE from unregistered peer {} — ignore", sender);
            return;
        }
        log.info("[Relay] Peer left (BYE): sessionId={}, userId={}, addr={}",
                entry.sessionId, entry.userId, sender);
        broadcastPeerLeft(entry.sessionId, entry.userId);
    }

    private void forwardData(ChannelHandlerContext ctx, InetSocketAddress sender, ByteBuf content) {
        Long sessionId = rooms.findSessionId(sender);
        if (sessionId == null) {
            log.debug("[Relay] Data from unregistered peer {} — drop", sender);
            return;
        }
        rooms.touch(sender);

        Set<InetSocketAddress> peers = rooms.peersOf(sessionId);
        if (peers.size() <= 1) {
            return; // 본인 외 peer 없음
        }

        for (InetSocketAddress peer : peers) {
            if (peer.equals(sender)) {
                continue;
            }
            ByteBuf copy = content.retainedDuplicate();
            ctx.writeAndFlush(new DatagramPacket(copy, peer));
        }
    }

    /**
     * 같은 세션의 모든 peer 에게 PEER_LEFT 메시지 broadcast.
     * BYE 즉시 처리 또는 timeout cleanup 에서 호출.
     * 떠난 peer 본인은 이미 unregister 된 상태라 자동 제외됨.
     */
    public void broadcastPeerLeft(Long sessionId, Long leftUserId) {
        if (channel == null) {
            return;
        }
        Set<InetSocketAddress> peers = rooms.peersOf(sessionId);
        if (peers.isEmpty()) {
            return;
        }
        String json = RelayProtocol.toJson(RelayProtocol.PeerLeftMessage.of(leftUserId));
        for (InetSocketAddress peer : peers) {
            ByteBuf buf = Unpooled.copiedBuffer(json, StandardCharsets.UTF_8);
            channel.writeAndFlush(new DatagramPacket(buf, peer));
        }
    }

    private void sendJson(ChannelHandlerContext ctx, InetSocketAddress recipient, String json) {
        ByteBuf buf = Unpooled.copiedBuffer(json, StandardCharsets.UTF_8);
        ctx.writeAndFlush(new DatagramPacket(buf, recipient));
    }

    @Override
    public void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) {
        log.error("[Relay] Channel exception", cause);
    }
}

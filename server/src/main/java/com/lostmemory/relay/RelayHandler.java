package com.lostmemory.relay;

import com.lostmemory.server.global.security.JwtProvider;
import com.lostmemory.server.global.security.SessionPrincipal;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;
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
 * - 첫 패킷이 JSON 핸드셰이크 ({"type":"HELLO","token":"..."}) → JWT 검증 + 룸 등록 + ACK
 * - 데이터 패킷 (앞 바이트가 MAGIC_DATA) → 같은 sessionId 의 다른 peer 들에게 forward
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
            handleHandshake(ctx, sender, content);
        }
    }

    private void handleHandshake(ChannelHandlerContext ctx, InetSocketAddress sender, ByteBuf content) {
        String json = content.toString(StandardCharsets.UTF_8);
        try {
            RelayProtocol.HandshakeMessage hello = RelayProtocol.parseHello(json);
            if (hello.token() == null) {
                throw new IllegalArgumentException("token 누락");
            }

            SessionPrincipal principal = jwtProvider.parseSessionToken(hello.token());

            rooms.register(principal.sessionId(), sender);
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

    private void forwardData(ChannelHandlerContext ctx, InetSocketAddress sender, ByteBuf content) {
        Long sessionId = rooms.findSessionId(sender);
        if (sessionId == null) {
            log.debug("[Relay] Data from unregistered peer {} — drop", sender);
            return;
        }

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

    private void sendJson(ChannelHandlerContext ctx, InetSocketAddress recipient, String json) {
        ByteBuf buf = Unpooled.copiedBuffer(json, StandardCharsets.UTF_8);
        ctx.writeAndFlush(new DatagramPacket(buf, recipient));
    }

    @Override
    public void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) {
        log.error("[Relay] Channel exception", cause);
    }
}

package com.lostmemory.relay;

import io.netty.bootstrap.Bootstrap;
import io.netty.channel.Channel;
import io.netty.channel.ChannelOption;
import io.netty.channel.EventLoopGroup;
import io.netty.channel.nio.NioEventLoopGroup;
import io.netty.channel.socket.nio.NioDatagramChannel;
import io.netty.util.concurrent.ScheduledFuture;
import jakarta.annotation.PreDestroy;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.stereotype.Component;

import java.util.List;
import java.util.concurrent.TimeUnit;

/**
 * Netty UDP listener.
 * Spring 컨텍스트가 완전 초기화된 뒤 ApplicationRunner.run() 으로 listen 시작.
 * shutdown 시 PreDestroy 로 graceful close.
 *
 * 추가로 idle peer cleanup 스케줄러를 EventLoop 에 등록 — BYE 누락 시
 * peerIdleTimeoutMs 초과 peer 를 자동 unregister 하고 PEER_LEFT broadcast.
 */
@Slf4j
@Component
@RequiredArgsConstructor
public class RelayServer implements ApplicationRunner {

    private final RelayProperties properties;
    private final RelayHandler handler;
    private final RoomRegistry rooms;

    private EventLoopGroup group;
    private Channel channel;
    private ScheduledFuture<?> cleanupTask;

    @Override
    public void run(ApplicationArguments args) throws Exception {
        group = new NioEventLoopGroup();
        Bootstrap bootstrap = new Bootstrap()
                .group(group)
                .channel(NioDatagramChannel.class)
                .option(ChannelOption.SO_BROADCAST, false)
                .handler(handler);

        channel = bootstrap.bind(properties.port()).sync().channel();
        handler.setChannel(channel);
        log.info("[Relay] UDP listener started on port {}", properties.port());

        cleanupTask = channel.eventLoop().scheduleAtFixedRate(
                this::cleanupIdlePeers,
                properties.cleanupIntervalMs(),
                properties.cleanupIntervalMs(),
                TimeUnit.MILLISECONDS
        );
        log.info("[Relay] Idle peer cleanup scheduled — interval={}ms, idleThreshold={}ms",
                properties.cleanupIntervalMs(), properties.peerIdleTimeoutMs());
    }

    private void cleanupIdlePeers() {
        try {
            List<RoomRegistry.TimedOutPeer> dead = rooms.sweepIdle(properties.peerIdleTimeoutMs());
            for (RoomRegistry.TimedOutPeer p : dead) {
                log.info("[Relay] Peer timed out: sessionId={}, userId={}, addr={}",
                        p.entry().sessionId, p.entry().userId, p.peer());
                handler.broadcastPeerLeft(p.entry().sessionId, p.entry().userId);
            }
        } catch (Exception e) {
            log.error("[Relay] Cleanup task threw", e);
        }
    }

    @PreDestroy
    public void shutdown() {
        if (cleanupTask != null) {
            cleanupTask.cancel(false);
        }
        if (channel != null) {
            channel.close();
        }
        if (group != null) {
            group.shutdownGracefully();
        }
        log.info("[Relay] Shutdown complete");
    }
}

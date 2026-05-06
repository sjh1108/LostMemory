package com.lostmemory.relay;

import io.netty.bootstrap.Bootstrap;
import io.netty.channel.Channel;
import io.netty.channel.ChannelOption;
import io.netty.channel.EventLoopGroup;
import io.netty.channel.nio.NioEventLoopGroup;
import io.netty.channel.socket.nio.NioDatagramChannel;
import jakarta.annotation.PreDestroy;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.stereotype.Component;

/**
 * Netty UDP listener.
 * Spring 컨텍스트가 완전 초기화된 뒤 ApplicationRunner.run() 으로 listen 시작.
 * shutdown 시 PreDestroy 로 graceful close.
 */
@Slf4j
@Component
@RequiredArgsConstructor
public class RelayServer implements ApplicationRunner {

    private final RelayProperties properties;
    private final RelayHandler handler;

    private EventLoopGroup group;
    private Channel channel;

    @Override
    public void run(ApplicationArguments args) throws Exception {
        group = new NioEventLoopGroup();
        Bootstrap bootstrap = new Bootstrap()
                .group(group)
                .channel(NioDatagramChannel.class)
                .option(ChannelOption.SO_BROADCAST, false)
                .handler(handler);

        channel = bootstrap.bind(properties.port()).sync().channel();
        log.info("[Relay] UDP listener started on port {}", properties.port());
    }

    @PreDestroy
    public void shutdown() {
        if (channel != null) {
            channel.close();
        }
        if (group != null) {
            group.shutdownGracefully();
        }
        log.info("[Relay] Shutdown complete");
    }
}

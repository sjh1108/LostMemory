package com.lostmemory.relay;

import com.fasterxml.jackson.databind.ObjectMapper;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;
import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.BenchmarkMode;
import org.openjdk.jmh.annotations.Level;
import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.annotations.OutputTimeUnit;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.Setup;
import org.openjdk.jmh.annotations.State;

import java.nio.charset.StandardCharsets;
import java.util.concurrent.TimeUnit;

@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.NANOSECONDS)
@State(Scope.Thread)
public class RelayDispatchBenchmark {

    private static final ObjectMapper MAPPER = new ObjectMapper();
    private static final byte MAGIC_DATA = 0x01;

    private ByteBuf jsonPacket;
    private ByteBuf binaryPacket;

    @Setup(Level.Trial)
    public void setup() {
        String json = "{\"type\":\"DATA\",\"senderUserId\":123456789,\"targetUserId\":987654321,\"payload\":\"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz\"}";
        jsonPacket = Unpooled.wrappedBuffer(json.getBytes(StandardCharsets.UTF_8));

        byte[] binary = new byte[128];
        binary[0] = MAGIC_DATA;
        for (int i = 1; i < binary.length; i++) {
            binary[i] = (byte) (i & 0x7F);
        }
        binaryPacket = Unpooled.wrappedBuffer(binary);
    }

    @Benchmark
    public boolean jsonTypeDispatch() throws Exception {
        String json = jsonPacket.toString(StandardCharsets.UTF_8);
        String type = MAPPER.readTree(json).path("type").asText(null);
        return "DATA".equals(type);
    }

    @Benchmark
    public boolean magicByteDispatch() {
        byte first = binaryPacket.getByte(binaryPacket.readerIndex());
        return first == MAGIC_DATA;
    }
}

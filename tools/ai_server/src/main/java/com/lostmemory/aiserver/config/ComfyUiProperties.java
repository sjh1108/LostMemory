package com.lostmemory.aiserver.config;

import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;
import java.net.URI;
import java.time.Duration;

import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.validation.annotation.Validated;

@ConfigurationProperties(prefix = "comfyui")
@Validated
public class ComfyUiProperties {

    @NotNull
    private URI baseUrl;
    private String outputDir = "";
    private String modelsDir = "";
    private String lorasDir = "";
    @Positive
    private int connectTimeoutMs = 5000;
    @Positive
    private int readTimeoutSeconds = 30;
    @Positive
    private int historyPollIntervalMs = 1000;
    @Positive
    private int historyPollTimeoutSeconds = 300;

    public URI getBaseUrl() {
        return baseUrl;
    }

    public void setBaseUrl(URI baseUrl) {
        this.baseUrl = baseUrl;
    }

    public String getOutputDir() {
        return outputDir;
    }

    public void setOutputDir(String outputDir) {
        this.outputDir = outputDir;
    }

    public String getModelsDir() {
        return modelsDir;
    }

    public void setModelsDir(String modelsDir) {
        this.modelsDir = modelsDir;
    }

    public String getLorasDir() {
        return lorasDir;
    }

    public void setLorasDir(String lorasDir) {
        this.lorasDir = lorasDir;
    }

    public int getConnectTimeoutMs() {
        return connectTimeoutMs;
    }

    public void setConnectTimeoutMs(int connectTimeoutMs) {
        this.connectTimeoutMs = connectTimeoutMs;
    }

    public int getReadTimeoutSeconds() {
        return readTimeoutSeconds;
    }

    public void setReadTimeoutSeconds(int readTimeoutSeconds) {
        this.readTimeoutSeconds = readTimeoutSeconds;
    }

    public int getHistoryPollIntervalMs() {
        return historyPollIntervalMs;
    }

    public void setHistoryPollIntervalMs(int historyPollIntervalMs) {
        this.historyPollIntervalMs = historyPollIntervalMs;
    }

    public int getHistoryPollTimeoutSeconds() {
        return historyPollTimeoutSeconds;
    }

    public void setHistoryPollTimeoutSeconds(int historyPollTimeoutSeconds) {
        this.historyPollTimeoutSeconds = historyPollTimeoutSeconds;
    }

    public Duration getConnectTimeout() {
        return Duration.ofMillis(connectTimeoutMs);
    }

    public Duration getReadTimeout() {
        return Duration.ofSeconds(readTimeoutSeconds);
    }

    public String getHost() {
        return baseUrl.getHost();
    }

    public int getPort() {
        if (baseUrl.getPort() != -1) {
            return baseUrl.getPort();
        }
        return "https".equalsIgnoreCase(baseUrl.getScheme()) ? 443 : 80;
    }
}

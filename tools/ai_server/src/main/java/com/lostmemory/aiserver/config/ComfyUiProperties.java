package com.lostmemory.aiserver.config;

import java.net.URI;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "comfyui")
public class ComfyUiProperties {

    private URI baseUrl = URI.create("http://host.docker.internal:8188");
    private String outputDir = "";
    private String modelsDir = "";
    private String lorasDir = "";
    private int historyPollIntervalMs = 1000;
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
}

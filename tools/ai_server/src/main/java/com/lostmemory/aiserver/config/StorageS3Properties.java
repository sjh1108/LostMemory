package com.lostmemory.aiserver.config;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Positive;
import java.time.Duration;

import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.validation.annotation.Validated;

@ConfigurationProperties(prefix = "storage.s3")
@Validated
public class StorageS3Properties {

    @NotBlank
    private String region;
    @NotBlank
    private String bucket;
    @NotBlank
    private String accessKeyId;
    @NotBlank
    private String secretAccessKey;
    @Positive
    private long signedUrlTtlSeconds = 86400;

    public String getRegion() {
        return region;
    }

    public void setRegion(String region) {
        this.region = region;
    }

    public String getBucket() {
        return bucket;
    }

    public void setBucket(String bucket) {
        this.bucket = bucket;
    }

    public String getAccessKeyId() {
        return accessKeyId;
    }

    public void setAccessKeyId(String accessKeyId) {
        this.accessKeyId = accessKeyId;
    }

    public String getSecretAccessKey() {
        return secretAccessKey;
    }

    public void setSecretAccessKey(String secretAccessKey) {
        this.secretAccessKey = secretAccessKey;
    }

    public long getSignedUrlTtlSeconds() {
        return signedUrlTtlSeconds;
    }

    public void setSignedUrlTtlSeconds(long signedUrlTtlSeconds) {
        this.signedUrlTtlSeconds = signedUrlTtlSeconds;
    }

    public Duration getSignedUrlTtl() {
        return Duration.ofSeconds(signedUrlTtlSeconds);
    }
}

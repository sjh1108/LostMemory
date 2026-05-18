package com.lostmemory.aiserver.generation;

import java.time.Instant;

import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.type.SqlTypes;

import com.fasterxml.jackson.databind.JsonNode;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.PrePersist;
import jakarta.persistence.Table;

@Entity
@Table(name = "workflow_snapshots")
public class WorkflowSnapshotEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(name = "workflow_name", nullable = false, length = 255)
    private String workflowName;

    @Column(name = "workflow_version", nullable = false, length = 50)
    private String workflowVersion;

    @Column(name = "workflow_hash", nullable = false, length = 64, unique = true)
    private String workflowHash;

    @Column(name = "source_filename", nullable = false, length = 255)
    private String sourceFilename;

    @Column(name = "primary_model_name", length = 255)
    private String primaryModelName;

    @JdbcTypeCode(SqlTypes.JSON)
    @Column(name = "model_metadata_json", nullable = false, columnDefinition = "jsonb")
    private JsonNode modelMetadataJson;

    @JdbcTypeCode(SqlTypes.JSON)
    @Column(name = "workflow_json", nullable = false, columnDefinition = "jsonb")
    private JsonNode workflowJson;

    // 인증 미구현 단계라 null 허용. userId 문자열을 억지로 숫자 FK에 맞추지 않는다.
    @Column(name = "created_by")
    private Long createdBy;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;

    @PrePersist
    void onCreate() {
        if (createdAt == null) {
            createdAt = Instant.now();
        }
    }

    public Long getId() {
        return id;
    }

    public String getWorkflowName() {
        return workflowName;
    }

    public void setWorkflowName(String workflowName) {
        this.workflowName = workflowName;
    }

    public String getWorkflowVersion() {
        return workflowVersion;
    }

    public void setWorkflowVersion(String workflowVersion) {
        this.workflowVersion = workflowVersion;
    }

    public String getWorkflowHash() {
        return workflowHash;
    }

    public void setWorkflowHash(String workflowHash) {
        this.workflowHash = workflowHash;
    }

    public String getSourceFilename() {
        return sourceFilename;
    }

    public void setSourceFilename(String sourceFilename) {
        this.sourceFilename = sourceFilename;
    }

    public String getPrimaryModelName() {
        return primaryModelName;
    }

    public void setPrimaryModelName(String primaryModelName) {
        this.primaryModelName = primaryModelName;
    }

    public JsonNode getModelMetadataJson() {
        return modelMetadataJson;
    }

    public void setModelMetadataJson(JsonNode modelMetadataJson) {
        this.modelMetadataJson = modelMetadataJson;
    }

    public JsonNode getWorkflowJson() {
        return workflowJson;
    }

    public void setWorkflowJson(JsonNode workflowJson) {
        this.workflowJson = workflowJson;
    }

    public Long getCreatedBy() {
        return createdBy;
    }

    public void setCreatedBy(Long createdBy) {
        this.createdBy = createdBy;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }
}

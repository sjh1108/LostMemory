package com.lostmemory.aiserver.generation;

import java.time.Instant;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.PrePersist;
import jakarta.persistence.PreUpdate;
import jakarta.persistence.Table;

@Entity
@Table(name = "generations")
public class GenerationEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(name = "prompt_id", nullable = false, unique = true, length = 100)
    private String promptId;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "workflow_snapshot_id", nullable = false)
    private WorkflowSnapshotEntity workflowSnapshot;

    // AI-701 이전에는 인증 주체가 항상 숫자 user PK로 확보되지 않는다.
    @Column(name = "created_by")
    private Long createdBy;

    @Enumerated(EnumType.STRING)
    @Column(name = "execution_status", nullable = false, length = 30)
    private GenerationExecutionStatus executionStatus;

    @Enumerated(EnumType.STRING)
    @Column(name = "failure_reason", length = 50)
    private GenerationFailureReason failureReason;

    @Column(name = "failed_stage", length = 30)
    private String failedStage;

    @Column(name = "prompt_summary", columnDefinition = "text")
    private String promptSummary;

    @Column(name = "full_prompt", columnDefinition = "text")
    private String fullPrompt;

    @Column(name = "user_message", columnDefinition = "text")
    private String userMessage;

    @Column(name = "internal_message", columnDefinition = "text")
    private String internalMessage;

    @Column(name = "completed_at")
    private Instant completedAt;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;

    @PrePersist
    void onCreate() {
        Instant now = Instant.now();
        if (createdAt == null) {
            createdAt = now;
        }
        updatedAt = now;
    }

    @PreUpdate
    void onUpdate() {
        updatedAt = Instant.now();
    }

    public Long getId() {
        return id;
    }

    public String getPromptId() {
        return promptId;
    }

    public void setPromptId(String promptId) {
        this.promptId = promptId;
    }

    public WorkflowSnapshotEntity getWorkflowSnapshot() {
        return workflowSnapshot;
    }

    public void setWorkflowSnapshot(WorkflowSnapshotEntity workflowSnapshot) {
        this.workflowSnapshot = workflowSnapshot;
    }

    public Long getCreatedBy() {
        return createdBy;
    }

    public void setCreatedBy(Long createdBy) {
        this.createdBy = createdBy;
    }

    public GenerationExecutionStatus getExecutionStatus() {
        return executionStatus;
    }

    public void setExecutionStatus(GenerationExecutionStatus executionStatus) {
        this.executionStatus = executionStatus;
    }

    public GenerationFailureReason getFailureReason() {
        return failureReason;
    }

    public void setFailureReason(GenerationFailureReason failureReason) {
        this.failureReason = failureReason;
    }

    public String getFailedStage() {
        return failedStage;
    }

    public void setFailedStage(String failedStage) {
        this.failedStage = failedStage;
    }

    public String getPromptSummary() {
        return promptSummary;
    }

    public void setPromptSummary(String promptSummary) {
        this.promptSummary = promptSummary;
    }

    public String getFullPrompt() {
        return fullPrompt;
    }

    public void setFullPrompt(String fullPrompt) {
        this.fullPrompt = fullPrompt;
    }

    public String getUserMessage() {
        return userMessage;
    }

    public void setUserMessage(String userMessage) {
        this.userMessage = userMessage;
    }

    public String getInternalMessage() {
        return internalMessage;
    }

    public void setInternalMessage(String internalMessage) {
        this.internalMessage = internalMessage;
    }

    public Instant getCompletedAt() {
        return completedAt;
    }

    public void setCompletedAt(Instant completedAt) {
        this.completedAt = completedAt;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }

    public Instant getUpdatedAt() {
        return updatedAt;
    }
}

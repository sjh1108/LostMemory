-- AI-501 최소 스키마
-- 초기 MVP에서 필요한 공통 테이블 뼈대만 먼저 생성한다.
-- 상세 컬럼 확장은 AI-502, AI-503, AI-504에서 이어간다.

CREATE TABLE IF NOT EXISTS users (
    id BIGSERIAL PRIMARY KEY,
    username VARCHAR(100) NOT NULL UNIQUE,
    display_name VARCHAR(100),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS workflow_snapshots (
    id BIGSERIAL PRIMARY KEY,
    workflow_name VARCHAR(255) NOT NULL,
    workflow_version VARCHAR(50),
    source_filename VARCHAR(255) NOT NULL,
    workflow_api_json JSONB NOT NULL,
    created_by BIGINT REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS generations (
    id BIGSERIAL PRIMARY KEY,
    prompt_id VARCHAR(100) NOT NULL UNIQUE,
    workflow_snapshot_id BIGINT REFERENCES workflow_snapshots(id),
    created_by BIGINT REFERENCES users(id),
    execution_status VARCHAR(30) NOT NULL,
    failure_reason VARCHAR(50),
    prompt_summary VARCHAR(255),
    user_message TEXT,
    internal_message TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS generation_outputs (
    id BIGSERIAL PRIMARY KEY,
    generation_id BIGINT NOT NULL REFERENCES generations(id) ON DELETE CASCADE,
    filename VARCHAR(255) NOT NULL,
    subfolder VARCHAR(255),
    output_type VARCHAR(50) NOT NULL,
    image_url TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS audit_logs (
    id BIGSERIAL PRIMARY KEY,
    action_type VARCHAR(50) NOT NULL,
    status VARCHAR(30) NOT NULL,
    prompt_id VARCHAR(100),
    actor_user_id BIGINT REFERENCES users(id),
    detail_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_workflow_snapshots_created_by
    ON workflow_snapshots (created_by);

CREATE INDEX IF NOT EXISTS idx_generations_workflow_snapshot_id
    ON generations (workflow_snapshot_id);

CREATE INDEX IF NOT EXISTS idx_generations_created_by
    ON generations (created_by);

CREATE INDEX IF NOT EXISTS idx_generations_execution_status
    ON generations (execution_status);

CREATE INDEX IF NOT EXISTS idx_generation_outputs_generation_id
    ON generation_outputs (generation_id);

CREATE INDEX IF NOT EXISTS idx_audit_logs_action_type
    ON audit_logs (action_type);

CREATE INDEX IF NOT EXISTS idx_audit_logs_status
    ON audit_logs (status);

CREATE INDEX IF NOT EXISTS idx_audit_logs_prompt_id
    ON audit_logs (prompt_id);

CREATE INDEX IF NOT EXISTS idx_audit_logs_actor_user_id
    ON audit_logs (actor_user_id);

-- AI-502 ~ AI-504 상세 스키마
-- AI 도구 1차 MVP에서 필요한 사용자, workflow snapshot, generation, output, audit log 컬럼을 확정한다.
-- 이 파일은 초기 스키마 source of truth이며, 빈 volume 첫 생성 시점에만 자동 실행된다.

CREATE TABLE IF NOT EXISTS users (
    id BIGSERIAL PRIMARY KEY,
    username VARCHAR(100) NOT NULL UNIQUE,
    display_name VARCHAR(100),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    last_login_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_users_username_not_blank CHECK (BTRIM(username) <> '')
);

-- workflow snapshot은 생성 시점 workflow의 immutable 사본으로 본다.
CREATE TABLE IF NOT EXISTS workflow_snapshots (
    id BIGSERIAL PRIMARY KEY,
    workflow_name VARCHAR(255) NOT NULL,
    workflow_version VARCHAR(50) NOT NULL DEFAULT 'v1',
    workflow_hash VARCHAR(64) NOT NULL UNIQUE,
    source_filename VARCHAR(255) NOT NULL,
    primary_model_name VARCHAR(255),
    model_metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    workflow_json JSONB NOT NULL,
    created_by BIGINT REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_workflow_name_not_blank CHECK (BTRIM(workflow_name) <> ''),
    CONSTRAINT chk_source_filename_not_blank CHECK (BTRIM(source_filename) <> ''),
    CONSTRAINT chk_workflow_hash_not_blank CHECK (BTRIM(workflow_hash) <> '')
);

-- generation 한 건은 prompt submit부터 완료/실패 판정까지의 실행 단위를 의미한다.
CREATE TABLE IF NOT EXISTS generations (
    id BIGSERIAL PRIMARY KEY,
    prompt_id VARCHAR(100) NOT NULL UNIQUE,
    workflow_snapshot_id BIGINT NOT NULL REFERENCES workflow_snapshots(id),
    created_by BIGINT REFERENCES users(id),
    execution_status VARCHAR(30) NOT NULL,
    failure_reason VARCHAR(50),
    failed_stage VARCHAR(30),
    prompt_summary TEXT,
    full_prompt TEXT,
    user_message TEXT,
    internal_message TEXT,
    completed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_generations_prompt_id_not_blank CHECK (BTRIM(prompt_id) <> ''),
    CONSTRAINT chk_generations_execution_status CHECK (
        execution_status IN ('SUBMITTED', 'RUNNING', 'SUCCEEDED', 'FAILED', 'TIMED_OUT')
    ),
    CONSTRAINT chk_generations_failure_reason CHECK (
        failure_reason IS NULL OR
        failure_reason IN ('HISTORY_FETCH_FAILED', 'COMFYUI_REPORTED_FAILURE', 'OUTPUT_MISSING', 'POLL_TIMEOUT', 'UNKNOWN_FAILURE')
    ),
    CONSTRAINT chk_generations_failed_stage CHECK (
        failed_stage IS NULL OR
        failed_stage IN ('PROMPT_SUBMIT', 'GENERATION', 'HISTORY_POLL', 'OUTPUT_DISCOVERY', 'S3_UPLOAD', 'METADATA_SAVE')
    )
);

-- generation output은 한 generation에서 생성된 결과 이미지들을 순서대로 보관한다.
CREATE TABLE IF NOT EXISTS generation_outputs (
    id BIGSERIAL PRIMARY KEY,
    generation_id BIGINT NOT NULL REFERENCES generations(id) ON DELETE CASCADE,
    output_index INTEGER NOT NULL DEFAULT 0,
    filename VARCHAR(255) NOT NULL,
    subfolder VARCHAR(255),
    output_type VARCHAR(50) NOT NULL,
    mime_type VARCHAR(100),
    image_url TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_generation_outputs_filename_not_blank CHECK (BTRIM(filename) <> ''),
    CONSTRAINT chk_generation_outputs_output_index_non_negative CHECK (output_index >= 0),
    CONSTRAINT uq_generation_outputs_generation_id_output_index UNIQUE (generation_id, output_index)
);

-- audit_logs는 login / generate / upload / download의 성공/실패를 남기는 최소 추적 테이블이다.
CREATE TABLE IF NOT EXISTS audit_logs (
    id BIGSERIAL PRIMARY KEY,
    action_type VARCHAR(50) NOT NULL,
    status VARCHAR(30) NOT NULL,
    prompt_id VARCHAR(100),
    actor_user_id BIGINT REFERENCES users(id),
    detail_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_audit_logs_action_type CHECK (
        action_type IN ('LOGIN', 'GENERATE', 'UPLOAD', 'DOWNLOAD')
    ),
    CONSTRAINT chk_audit_logs_status CHECK (
        status IN ('SUCCESS', 'FAILED')
    )
);

CREATE INDEX IF NOT EXISTS idx_workflow_snapshots_created_by
    ON workflow_snapshots (created_by);

CREATE INDEX IF NOT EXISTS idx_workflow_snapshots_workflow_name
    ON workflow_snapshots (workflow_name);

CREATE INDEX IF NOT EXISTS idx_generations_workflow_snapshot_id
    ON generations (workflow_snapshot_id);

CREATE INDEX IF NOT EXISTS idx_generations_created_by
    ON generations (created_by);

CREATE INDEX IF NOT EXISTS idx_generations_execution_status
    ON generations (execution_status);

CREATE INDEX IF NOT EXISTS idx_generations_created_at_desc
    ON generations (created_at DESC);

CREATE INDEX IF NOT EXISTS idx_generations_created_by_created_at_desc
    ON generations (created_by, created_at DESC);

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

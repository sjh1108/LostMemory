-- =====================================================================
-- PostgreSQL DDL  (ERD 기반)
-- =====================================================================
-- 주의사항
--  * 모든 시간 컬럼은 TIMESTAMPTZ (timestamp with time zone) 사용
--  * JSON 컬럼은 JSONB 사용 (인덱싱/쿼리 성능 우위)
--  * PK 는 BIGINT GENERATED ALWAYS AS IDENTITY 로 자동 증가 (PostgreSQL 10+ 표준)
--  * SESSION_JOINS 는 관계 정의상 session_id FK 가 필요하므로 컬럼 추가함
--  * 무기/프레임의 cost 정보는 백엔드 관리하지 않음 — 클라가 알아서 계산하고
--    파편 소비량(consumed_shards) 만 백엔드로 보내 user_currencies.memory_shards 차감
--  * 프레임 칸 해금은 6칸 비트마스크 (unlocked_mask, 0~63) 로 관리
--    state 는 mask 로부터 derive (0=Locked, 63=Done, 그 외=In Progress) — 컬럼 보관 X
-- =====================================================================


-- =====================================================================
-- 1. USERS : 계정 정보
-- =====================================================================
CREATE TABLE users (
    user_id         BIGINT       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    login_id        VARCHAR(50)  NOT NULL UNIQUE,
    password_hash   VARCHAR(255) NOT NULL,
    nickname        VARCHAR(50)  NOT NULL UNIQUE,
    status          VARCHAR(20)  NOT NULL DEFAULT 'active'
                                 CHECK (status IN ('active', 'suspended', 'deleted')),
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login_at   TIMESTAMPTZ
);

COMMENT ON TABLE  users                 IS '유저 계정';
COMMENT ON COLUMN users.user_id         IS '내부 유저 ID (PK)';
COMMENT ON COLUMN users.login_id        IS '로그인 ID (UNIQUE)';
COMMENT ON COLUMN users.nickname        IS '인게임 닉네임 (UNIQUE)';
COMMENT ON COLUMN users.status          IS '계정 상태';


-- =====================================================================
-- 2. AUTH_REFRESH_TOKENS : 리프레시 토큰
-- =====================================================================
CREATE TABLE auth_refresh_tokens (
    refresh_token_id BIGINT       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id          BIGINT       NOT NULL
                                  REFERENCES users(user_id) ON DELETE CASCADE,
    token_hash       VARCHAR(255) NOT NULL,
    expires_at       TIMESTAMPTZ  NOT NULL,
    last_used_at     TIMESTAMPTZ,
    created_at       TIMESTAMPTZ  NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_refresh_tokens_user_id    ON auth_refresh_tokens(user_id);
CREATE INDEX idx_refresh_tokens_token_hash ON auth_refresh_tokens(token_hash);
CREATE INDEX idx_refresh_tokens_expires_at ON auth_refresh_tokens(expires_at);


-- =====================================================================
-- 3. USER_CURRENCIES : 유저 재화 (파편 보유량)
--   * 백엔드는 보유량만 관리. 적립/소비량은 클라가 계산해서 delta 만 보냄.
-- =====================================================================
CREATE TABLE user_currencies (
    user_id        BIGINT      PRIMARY KEY
                               REFERENCES users(user_id) ON DELETE CASCADE,
    memory_shards  INTEGER     NOT NULL DEFAULT 0
                               CHECK (memory_shards >= 0),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

COMMENT ON COLUMN user_currencies.memory_shards IS '기억의 파편 보유량';


-- =====================================================================
-- 4. USER_TALENT_ALLOCATIONS : 특성 포인트 분배
--   * 백엔드는 4개 영역별 투자 포인트 값만 저장/조회. 총량 / 잔여 포인트는 관리 X.
--   * 4 영역: 치명타 확률 / 공격속도 / 방어력 / 최대체력 (마나재생 slot 은 기획 정리로 제거).
--   * total_point 컬럼은 의미 없는 0 으로 유지 (legacy — 추후 schema 정리 시 제거).
-- =====================================================================
CREATE TABLE user_talent_allocations (
    user_id              BIGINT      PRIMARY KEY
                                     REFERENCES users(user_id) ON DELETE CASCADE,
    total_point          INTEGER     NOT NULL DEFAULT 0 CHECK (total_point          >= 0),
    crit_rate_points     INTEGER     NOT NULL DEFAULT 0 CHECK (crit_rate_points     >= 0),
    attack_speed_points  INTEGER     NOT NULL DEFAULT 0 CHECK (attack_speed_points  >= 0),
    defense_points       INTEGER     NOT NULL DEFAULT 0 CHECK (defense_points       >= 0),
    max_hp_points        INTEGER     NOT NULL DEFAULT 0 CHECK (max_hp_points        >= 0),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- =====================================================================
-- 5. WEAPONS : 무기 마스터
--   * cost 정보는 클라가 관리. 백엔드는 트리 구조 + 식별자만.
-- =====================================================================
CREATE TABLE weapons (
    weapon_id           BIGINT       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    weapon_name         VARCHAR(100) NOT NULL,
    weapon_type         VARCHAR(50)  NOT NULL,
    parent_weapon_id    BIGINT       REFERENCES weapons(weapon_id) ON DELETE SET NULL,
    display_order       INTEGER      NOT NULL DEFAULT 0
);

CREATE INDEX idx_weapons_parent        ON weapons(parent_weapon_id);
CREATE INDEX idx_weapons_display_order ON weapons(display_order);

COMMENT ON COLUMN weapons.parent_weapon_id IS '상위(트리) 무기 ID. 루트면 NULL';


-- =====================================================================
-- 6. USER_WEAPON_UNLOCKS : 유저별 무기 해금 상태
-- =====================================================================
CREATE TABLE user_weapon_unlocks (
    user_weapon_unlocked_id BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id                 BIGINT      NOT NULL
                                        REFERENCES users(user_id) ON DELETE CASCADE,
    unlock_node_id          BIGINT      NOT NULL
                                        REFERENCES weapons(weapon_id) ON DELETE CASCADE,
    unlocked_at             TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    -- 같은 유저가 같은 무기를 중복 해금하지 못하도록
    CONSTRAINT uq_user_weapon UNIQUE (user_id, unlock_node_id)
);

CREATE INDEX idx_user_weapon_unlocks_user ON user_weapon_unlocks(user_id);


-- =====================================================================
-- 7. SESSIONS : 멀티플레이 세션(방)
-- =====================================================================
CREATE TABLE sessions (
    session_id    BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    host_id       BIGINT      NOT NULL
                              REFERENCES users(user_id) ON DELETE CASCADE,
    max_players   INTEGER     NOT NULL CHECK (max_players > 0),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    private_code  VARCHAR(20)  -- NULL 이면 공개 방
);

CREATE INDEX idx_sessions_host         ON sessions(host_id);
CREATE UNIQUE INDEX idx_sessions_private_code
    ON sessions(private_code) WHERE private_code IS NOT NULL;


-- =====================================================================
-- 8. SESSION_JOINS : 세션 참가자
-- =====================================================================
CREATE TABLE session_joins (
    session_join_id BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    session_id      BIGINT      NOT NULL
                                REFERENCES sessions(session_id) ON DELETE CASCADE,
    user_id         BIGINT      NOT NULL
                                REFERENCES users(user_id) ON DELETE CASCADE,
    role            VARCHAR(10) NOT NULL
                                CHECK (role IN ('host', 'guest')),
    joined_at       TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_session_user UNIQUE (session_id, user_id)
);

CREATE INDEX idx_session_joins_session ON session_joins(session_id);
CREATE INDEX idx_session_joins_user    ON session_joins(user_id);


-- =====================================================================
-- 9. RUNS : 한 판의 플레이 인스턴스
-- =====================================================================
CREATE TABLE runs (
    run_id      BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    session_id  BIGINT      NOT NULL
                            REFERENCES sessions(session_id) ON DELETE CASCADE,
    status      VARCHAR(20) NOT NULL
                            CHECK (status IN ('progress', 'end')),
    started_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ended_at    TIMESTAMPTZ,
    CONSTRAINT chk_run_end_after_start
        CHECK (ended_at IS NULL OR ended_at >= started_at)
);

CREATE INDEX idx_runs_session ON runs(session_id);
CREATE INDEX idx_runs_status  ON runs(status);


-- =====================================================================
-- 10. RUN_MEMBER : 런에 참여한 유저별 스냅샷
-- =====================================================================
CREATE TABLE run_member (
    run_member_id            BIGINT    GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    run_id                   BIGINT    NOT NULL
                                       REFERENCES runs(run_id) ON DELETE CASCADE,
    user_id                  BIGINT    NOT NULL
                                       REFERENCES users(user_id) ON DELETE CASCADE,
    selected_weapon_id       BIGINT    REFERENCES weapons(weapon_id) ON DELETE SET NULL,
    final_hp                 INTEGER,
    final_gold               INTEGER,
    selected_artifacts_json  JSONB,
    shop_purchases_json      JSONB,
    CONSTRAINT uq_run_user UNIQUE (run_id, user_id)
);

CREATE INDEX idx_run_member_run  ON run_member(run_id);
CREATE INDEX idx_run_member_user ON run_member(user_id);


-- =====================================================================
-- 11. RUN_RESULTS : 런 종료 결과 (1:1)
-- =====================================================================
CREATE TABLE run_results (
    run_id                BIGINT      PRIMARY KEY
                                      REFERENCES runs(run_id) ON DELETE CASCADE,
    result                VARCHAR(20) NOT NULL
                                      CHECK (result IN ('clear', 'death', 'surrender')),
    duration_seconds      INTEGER     NOT NULL CHECK (duration_seconds >= 0),
    chapter_reached       INTEGER     NOT NULL DEFAULT 0,
    stage_reached         INTEGER     NOT NULL DEFAULT 0,
    memory_shards_earned  INTEGER     NOT NULL DEFAULT 0 CHECK (memory_shards_earned >= 0),
    bosses_defeated       INTEGER     NOT NULL DEFAULT 0 CHECK (bosses_defeated      >= 0),
    rooms_cleared         INTEGER     NOT NULL DEFAULT 0 CHECK (rooms_cleared        >= 0),
    enemies_killed        INTEGER     NOT NULL DEFAULT 0 CHECK (enemies_killed       >= 0),
    saved_at              TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- =====================================================================
-- 12. MEMORY_FRAMES : 기억 액자 마스터
--   * 칸 정보 / 요구 cost 는 클라가 관리. 백엔드는 frame_id + 정렬만 보관.
-- =====================================================================
CREATE TABLE memory_frames (
    frame_id       BIGINT  GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    display_order  INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX idx_memory_frames_display_order ON memory_frames(display_order);


-- =====================================================================
-- 13. USER_MEMORY_PROGRESS : 유저별 액자 해금 상태
--   * unlocked_mask : 6칸 비트마스크 (0~63). 1 비트가 해금된 칸.
--     예) 0b101010 (=42) → slot 1, 3, 5 해금
--   * state 는 mask 로부터 derive (보관 X):
--       0  → Locked
--       63 → Done (6칸 다 해금)
--       그 외 → In Progress
-- =====================================================================
CREATE TABLE user_memory_progress (
    memory_progress_id BIGINT  GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id            BIGINT  NOT NULL
                               REFERENCES users(user_id) ON DELETE CASCADE,
    frame_id           BIGINT  NOT NULL
                               REFERENCES memory_frames(frame_id) ON DELETE CASCADE,
    unlocked_mask      INTEGER NOT NULL DEFAULT 0
                               CHECK (unlocked_mask BETWEEN 0 AND 63),
    CONSTRAINT uq_user_frame UNIQUE (user_id, frame_id)
);

CREATE INDEX idx_user_memory_progress_user ON user_memory_progress(user_id);

COMMENT ON COLUMN user_memory_progress.unlocked_mask
    IS '6칸 비트마스크 (0~63). bit n=1 이면 slot n 해금. 63 이면 Done.';


-- =====================================================================
-- 14. USER_RECORD : 유저 최고 전적
-- =====================================================================
CREATE TABLE user_record (
    record_id        BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id          BIGINT      NOT NULL UNIQUE
                                 REFERENCES users(user_id) ON DELETE CASCADE,
    cleared_chapter  INTEGER     NOT NULL DEFAULT 0 CHECK (cleared_chapter >= 0),
    cleared_stage    INTEGER     NOT NULL DEFAULT 0 CHECK (cleared_stage   >= 0),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- =====================================================================
-- 15. USER_WEAPON_SELECTION : 유저별 마지막 선택 무기
--   * 다음 런 시작 시 자동으로 쥐어줄 무기. 회원가입 시 weapon_id=1 (검) 으로 초기화.
--   * user_id PK = 1:1. weapons.weapon_id FK. 무기 마스터 삭제는 RESTRICT 로 차단 (마스터 무결성).
-- =====================================================================
CREATE TABLE user_weapon_selection (
    user_id              BIGINT      PRIMARY KEY
                                     REFERENCES users(user_id) ON DELETE CASCADE,
    selected_weapon_id   BIGINT      NOT NULL
                                     REFERENCES weapons(weapon_id) ON DELETE RESTRICT,
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

COMMENT ON COLUMN user_weapon_selection.selected_weapon_id
    IS '다음 런 시작 시 자동 선택될 무기 ID. 회원가입 시 1 (검).';


-- =====================================================================
-- updated_at 자동 갱신 트리거
-- =====================================================================
CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at := CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_user_currencies_updated_at
    BEFORE UPDATE ON user_currencies
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_user_talent_allocations_updated_at
    BEFORE UPDATE ON user_talent_allocations
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_user_record_updated_at
    BEFORE UPDATE ON user_record
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_user_weapon_selection_updated_at
    BEFORE UPDATE ON user_weapon_selection
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();


-- =====================================================================
-- 마스터 시드 데이터 — 무기 / 기억 액자
--   * 회원가입 보강 (AuthService) 이 weapons 마스터를 읽어 user_weapon_unlocks 를 만드므로,
--     이 시드가 없으면 신규 회원의 unlocks 가 빈 배열이 됨.
--   * 멱등 (ON CONFLICT DO NOTHING) — schema.sql 재실행해도 중복 INSERT 없음.
--   * IDENTITY 시퀀스 setval 로 수동 ID 삽입 후 다음 INSERT 충돌 방지.
-- =====================================================================


-- 무기 6종 (검·단검·활·화염방사기·스태프·강화 스태프)
-- parent_weapon_id 트리 구조: 검→단검, 활→화염방사기, 스태프→강화 스태프
INSERT INTO weapons (weapon_id, weapon_name, weapon_type, parent_weapon_id, display_order)
OVERRIDING SYSTEM VALUE
VALUES
    (1, '검',           'Sword',         NULL, 1),
    (2, '단검',         'Dagger',        1,    2),
    (3, '활',           'Bow',           NULL, 3),
    (4, '화염방사기',   'Flamethrower',  3,    4),
    (5, '스태프',       'Staff',         NULL, 5),
    (6, '강화 스태프',  'EnhancedStaff', 5,    6)
ON CONFLICT (weapon_id) DO NOTHING;

SELECT setval(pg_get_serial_sequence('weapons', 'weapon_id'),
              (SELECT COALESCE(MAX(weapon_id), 1) FROM weapons));


-- 기억 액자 4종 (display_order 1~4). 각 프레임의 칸 정보(rows/cols/cost) 는 클라가 관리.
INSERT INTO memory_frames (frame_id, display_order)
OVERRIDING SYSTEM VALUE
VALUES
    (1, 1),
    (2, 2),
    (3, 3),
    (4, 4)
ON CONFLICT (frame_id) DO NOTHING;

SELECT setval(pg_get_serial_sequence('memory_frames', 'frame_id'),
              (SELECT COALESCE(MAX(frame_id), 1) FROM memory_frames));

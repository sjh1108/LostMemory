-- S14P31C201-622: 비로그인 방문자 건의/버그 리포트 페이지.
-- handled BOOLEAN 으로 admin 처리 상태 토글. contact 는 선택 (이메일/디스코드).

CREATE TABLE feedback (
    id          BIGSERIAL PRIMARY KEY,
    category    VARCHAR(32) NOT NULL,        -- 'BUG' / 'SUGGESTION' / 'ETC'
    title       VARCHAR(200) NOT NULL,
    body        TEXT NOT NULL,
    contact     VARCHAR(255),
    handled     BOOLEAN NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    handled_at  TIMESTAMP
);

CREATE INDEX idx_feedback_handled_created ON feedback (handled, created_at DESC);

-- users.status CHECK 제약에 'pending' 추가.
-- 가입 직후 이메일 미인증 상태를 표현하기 위해 도입. 인증 완료 시 'active' 로 전환.
--
-- 데이터 검증:
--   SELECT status, COUNT(*) FROM users GROUP BY status;   -- 기존 'active'/'suspended'/'deleted' 유지

ALTER TABLE users DROP CONSTRAINT users_status_check;
ALTER TABLE users ADD CONSTRAINT users_status_check
    CHECK (status IN ('pending', 'active', 'suspended', 'deleted'));

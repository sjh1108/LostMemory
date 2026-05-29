-- users 테이블에 email 컬럼을 추가한다.
-- login_id 는 그대로 로그인 식별자로 유지된다 — email 은 인증·비밀번호 재설정용 보조 식별자.
--
-- 적용 절차:
--   1) 본 스크립트 적용 (email 추가/backfill/NOT NULL/UNIQUE)
--   2) 새 코드 배포 (email 컬럼을 활용한 인증 코드 발송·비번 재설정)
--
-- 데이터 검증 (적용 직후):
--   SELECT COUNT(*) FROM users WHERE email IS NULL OR email = '';  -- 0
--   SELECT email, COUNT(*) FROM users GROUP BY email HAVING COUNT(*) > 1;  -- 0 row

-- (A) email 컬럼 추가 (NULL 허용)
ALTER TABLE users ADD COLUMN email VARCHAR(255);

-- (B) 기존 row backfill — 닉네임 기반 더미 (테스트 데이터 가정).
--     운영 데이터가 있다면 본 라인을 운영용 backfill 로 교체할 것.
UPDATE users SET email = LOWER(nickname) || '@migrate.local' WHERE email IS NULL;

-- (C) NOT NULL + UNIQUE
ALTER TABLE users ALTER COLUMN email SET NOT NULL;
ALTER TABLE users ADD CONSTRAINT uk_users_email UNIQUE (email);

-- S14P31C201-620: FAQ 카테고리 admin CRUD (옵션 c).
-- Faq.sort_order DB 컬럼 + 정렬 로직 + admin UI 모두 유지 (사용자 정정).
-- 카테고리 삭제 시 ON DELETE SET NULL → FAQ 보존, category_id=NULL 로 미분류 그룹 노출.

CREATE TABLE faq_category (
    id          BIGSERIAL PRIMARY KEY,
    name        VARCHAR(64) NOT NULL UNIQUE,
    sort_order  INT NOT NULL DEFAULT 0,
    published   BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE faq ADD COLUMN category_id BIGINT REFERENCES faq_category(id) ON DELETE SET NULL;
CREATE INDEX idx_faq_category ON faq (category_id);

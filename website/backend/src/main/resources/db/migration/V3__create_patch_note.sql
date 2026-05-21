CREATE TABLE patch_note (
    id              BIGSERIAL PRIMARY KEY,
    version         VARCHAR(50) NOT NULL,
    release_date    DATE NOT NULL,
    body            TEXT NOT NULL,
    author_id       BIGINT REFERENCES admin_user(id) ON DELETE SET NULL,
    published       BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_patchnote_pub_release ON patch_note (published, release_date DESC);

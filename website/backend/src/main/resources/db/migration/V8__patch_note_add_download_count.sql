-- S14P31C201-641: 패치노트 다운로드 버튼 클릭 수 측정.
-- /downloads/latest, /downloads/{id} 가 Google Drive URL 로 302 redirect 하기 전에
-- 이 컬럼을 1 증가시킨다. 어드민 리스트에 다운로드 수 컬럼으로 노출.

ALTER TABLE patch_note ADD COLUMN download_count BIGINT NOT NULL DEFAULT 0;

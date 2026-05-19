-- S14P31C201-618: 패치노트에 다운로드 URL (Windows 한정) 필드 추가.
-- 메인 헤더의 "최신 버전 다운로드" link 및 patchnote/detail 의 다운로드 버튼 source of truth.
-- nullable — 다운로드 첨부 없는 패치노트 (예: 서버 핫픽스) 도 published 가능.

ALTER TABLE patch_note ADD COLUMN download_url VARCHAR(1024);

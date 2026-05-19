-- =====================================================================
-- 마스터 시드 데이터 — 무기 / 기억 액자
-- =====================================================================
-- 적용:
--   psql -d <db> -f schema.sql -f seed_masters.sql
--
-- 멱등 (ON CONFLICT DO NOTHING) — 반복 실행해도 중복 INSERT 없음.
-- 시퀀스 setval 은 빈 테이블 첫 INSERT 후엔 MAX(id) 가 0 이면 setval 1 로 안전.
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

-- IDENTITY 시퀀스 재정렬 (수동 ID 삽입 이후 다음 INSERT 충돌 방지)
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

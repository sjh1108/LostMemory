# 데이터 영속화 / 스키마 / 런 결과 데이터 서빙 전략 문서

- 문서 목적: 현재까지 확정된 인증/네트워크/용어 기준을 바탕으로, **데이터 영속화 원칙**, **DB 스키마 방향**, **런 결과 데이터 서빙 및 저장 전략**을 정리한다.
- 문서 상태: 초안
- 적용 범위: MVP 기준
- 최신 기준 요약:
  - 인증은 **`loginId + password + nickname` 기반 간단 계정 로그인**
  - **인런 실시간 상태는 Unity 런타임**
  - **정산 및 저장은 백엔드**
  - **gold는 인런 재화**
  - **memoryShards(기억의 파편)는 영구 재화**
  - **무기 해금/무기 트리 해금은 런 외부 시스템**
  - **무기 해금 재화는 기억의 파편으로 통일**

---

## 1. 문서 범위

이 문서는 아래 3가지를 다룬다.

1. **데이터 영속화 전략**
   - 어떤 데이터를 저장할지
   - 어떤 데이터는 저장하지 않을지
   - 저장 시점은 언제인지

2. **스키마 전략**
   - 어떤 테이블/엔티티가 필요한지
   - 서로 어떤 관계를 가지는지
   - MVP에서 반드시 필요한 수준은 어디까지인지

3. **런 결과 데이터 서빙 전략**
   - Unity가 백엔드에 어떤 형태로 결과를 넘길지
   - 백엔드는 어떤 데이터를 저장/조회/정산할지
   - 통계/전적/밸런스 분석 확장을 어떻게 고려할지

---

## 2. 핵심 원칙

### 2.1 책임 분리 원칙
- **Unity 런타임**은 인런 실시간 상태를 authoritative 하게 관리한다.
- **백엔드**는 계정, 영구 성장, 런 시작/종료 기록, 결과 정산, 메타 데이터 저장을 담당한다.

즉,
- 현재 체력
- 현재 gold
- 현재 보유 유물
- 현재 방 진행
- 보상 선택
- 상점 구매
- 실시간 전투 판정

은 Unity 책임이다.

반면,
- 회원가입/로그인
- 유저 영구 데이터
- 기억의 파편 누적
- 무기 해금 상태
- 재능 분배 저장
- 런 종료 결과 저장
- 전적/통계 조회

는 백엔드 책임이다.

---

### 2.2 영속화 최소화 원칙
MVP에서는 **게임 런타임 필수 의존성**을 최소화한다.

즉,
- 인런 중 매 순간 바뀌는 상태를 백엔드에 계속 밀어 넣지 않는다.
- 런 종료 시점에 **요약 결과**를 받아 저장한다.
- 중간 저장이 꼭 필요한 경우에만 별도 `progress` 성격을 둔다.

---

### 2.3 재화 분리 원칙
- `gold`는 **인런 재화**다.
- `memoryShards`는 **영구 재화**다.
- `gold`는 플레이어 영구 currency로 저장하지 않는다.
- 무기 해금/무기 트리 해금은 `memoryShards`를 사용한다.

---

### 2.4 용어 분리 원칙
- `세션` = 매칭룸 생성 ~ 제거/폭파
- `런` = 던전 입장 ~ 종료
- `로비 == 마을`
- `상점방`은 인런 내부 공간
- `방장 == 호스트`
- `스토리 진행도`와 `챕터 진행도`는 별도 개념으로 본다.

---

## 3. 데이터 분류

프로젝트 데이터는 크게 4종류로 나눈다.

### 3.1 계정/인증 데이터
예:
- loginId
- password hash
- nickname
- refresh token 세션
- 내부 user_id

특징:
- 영구 저장
- 백엔드 전담
- 보안 요구 높음

---

### 3.2 메타 성장 데이터
예:
- memoryShards
- 기억의 조각 해금 상태
- 메인 기억 완성 상태
- 스토리 해금/시청 여부
- 재능 분배 상태
- 무기 해금 상태
- 무기 트리 해금 상태

특징:
- 영구 저장
- 백엔드 전담
- 계정 귀속

---

### 3.3 마스터 데이터
예:
- 유물 정의
- 적 정의
- 보상 테이블
- 상점 테이블
- 무기 트리 정의
- 기억/조각 정의

특징:
- 주로 읽기 전용
- 서버가 JSON/테이블 기준으로 서빙
- 수정 시 배포 또는 데이터 갱신 필요
- DB에 넣기보다 파일/테이블 혼합 전략 가능

---

### 3.4 런 결과 데이터
예:
- 플레이 타임
- 결과(`clear`, `death`, `surrender`)
- 도달 챕터/스테이지
- 처치 수
- 획득한 memoryShards
- 선택한 무기
- 최종 빌드 요약
- 밸런스 분석용 선택 로그

특징:
- 런 종료 시 저장
- 전적/통계/랭킹/분석 용도
- 인런 실시간 상태 전체를 저장하는 것이 아니라, **종료 시점 요약본**을 저장

---

## 4. 영속화 전략

## 4.1 반드시 영구 저장할 데이터

### 계정
- user_id
- loginId
- password_hash
- nickname
- created_at / updated_at
- last_login_at
- status

### 인증 세션
- refresh token hash
- 만료 시각
- revoked 여부
- last_used_at

### 메타 성장
- memoryShards 총량
- 기억의 조각 해금 상태
- 메인 기억 완성 상태
- 스토리 해금/시청 여부
- 재능 분배 저장값
- 무기 해금 상태
- 무기 트리 해금 상태

### 런 결과
- run_id
- user_id
- started_at
- ended_at
- result
- duration_seconds
- chapter_or_stage_reached
- memory_shards_earned
- 전투/선택 로그 요약(선택적)
- 최종 빌드 요약(선택적)

---

## 4.2 저장하지 않거나, 원칙적으로 영속화하지 않을 데이터

### 인런 실시간 상태
- current_hp
- current_gold
- current_artifacts
- current_room
- boss_phase
- 현재 상점 진열
- 현재 보상 후보

이 값들은 Unity 런타임이 들고 가는 값이다.

즉,
백엔드는 이 값을 **실시간 authoritative 상태**로 저장하지 않는다.

---

### gold 영구 누적
- `gold`는 플레이어 영구 currency에 넣지 않는다.
- 런 종료 후 사라지는 인런 재화로 유지한다.

---

## 4.3 저장 시점

### 회원가입 시
- users 생성
- 인증 identity 생성
- 필요 시 기본 메타 데이터 row 초기화

### 로그인 시
- last_login_at 갱신
- refresh token row 생성/교체

### 로그아웃 시
- refresh token revoke

### 기억/재능/무기 해금 시
- 즉시 영구 저장

### 런 시작 시
- 필요 시 `runs` row를 `in_progress` 상태로 생성
- 최소 정보만 저장
  - user_id
  - weapon 선택
  - talent snapshot
  - started_at

### 런 종료 시
- `runs` 종료 처리
- `run_results` 저장
- `memoryShards` 정산
- 필요 시 통계성 로그 저장

---

## 5. 스키마 전략

아래는 **MVP 기준 권장 스키마 묶음**이다.

## 5.1 계정 / 인증

### users
목적:
- 내부 유저 기본 정보 저장

권장 필드:
- `user_id` PK
- `login_id` UNIQUE
- `password_hash`
- `nickname` UNIQUE
- `status`
- `created_at`
- `updated_at`
- `last_login_at`

비고:
- 현재 MVP는 이메일 없이 `login_id` 기반

---

### user_auth_identities
목적:
- 인증 수단 확장 대비
- 추후 Steam 연동 대비

권장 필드:
- `identity_id` PK
- `user_id` FK
- `provider_type`
- `provider_account_id`
- `created_at`
- `updated_at`

비고:
- 현재 1단계 provider 명칭은 추후 확정 필요
- 예: `LOCAL`, `BASIC`, `LOGIN_ID`
- 향후 `STEAM` 추가 가능

---

### auth_refresh_tokens
목적:
- refresh token 세션 관리

권장 필드:
- `refresh_token_id` PK
- `user_id` FK
- `token_hash`
- `expires_at`
- `revoked_at`
- `last_used_at`
- `created_at`

비고:
- refresh token rotation 고려

---

## 5.2 메타 성장

### user_currencies
목적:
- 영구 재화 저장

권장 필드:
- `user_id` PK/FK
- `memory_shards`
- `updated_at`

비고:
- `gold`는 넣지 않음

---

### memories
목적:
- 메인 기억 마스터 데이터

권장 필드:
- `memory_id` PK
- `name`
- `display_order`
- `puzzle_size`
- `reward_type`
- `reward_value`

비고:
- 마스터 데이터 성격
- JSON/DB 중 선택 가능

---

### memory_fragments
목적:
- 기억의 조각 마스터 데이터

권장 필드:
- `fragment_id` PK
- `memory_id` FK
- `position_x`
- `position_y`
- `cost_memory_shards`
- `effect_type`
- `effect_value`
- `display_order`

---

### user_memory_fragments
목적:
- 유저별 기억의 조각 해금 상태

권장 필드:
- `user_id` FK
- `fragment_id` FK
- `unlocked_at`

PK:
- `(user_id, fragment_id)`

---

### user_memories
목적:
- 유저별 메인 기억 완성 상태

권장 필드:
- `user_id` FK
- `memory_id` FK
- `completed`
- `completed_at`

PK:
- `(user_id, memory_id)`

---

### user_story_progress
목적:
- 유저별 스토리 해금/시청 상태

권장 필드:
- `user_id` FK
- `story_id`
- `unlocked`
- `viewed`
- `unlocked_at`
- `viewed_at`

PK:
- `(user_id, story_id)`

---

### user_talent_allocations
목적:
- 유저의 재능 분배 상태 저장

권장 필드:
- `user_id` PK/FK
- `crit_rate_points`
- `attack_speed_points`
- `defense_points`
- `mana_regen_points`
- `max_hp_points`
- `updated_at`

비고:
- 재분배 가능한 구조 전제

---

## 5.3 무기 해금 / 무기 트리 해금

### weapons
목적:
- 무기 마스터 데이터

권장 필드:
- `weapon_id` PK
- `name`
- `weapon_type`
- `base_weapon_id` nullable
- `tree_group`
- `display_order`

비고:
- 검 / 망치 / 활
- 이후 트리 분기 표현 가능

---

### weapon_unlock_nodes
목적:
- 무기 트리의 해금 노드 정의

권장 필드:
- `unlock_node_id` PK
- `weapon_id` FK
- `parent_node_id` nullable
- `cost_memory_shards`
- `node_type`
- `display_name`
- `display_order`

비고:
- 예: 일반 검 -> 불검 / 얼음검 / 장검
- 트리 구조 표현 핵심

---

### user_weapon_unlocks
목적:
- 유저별 무기/무기 트리 해금 상태

권장 필드:
- `user_id` FK
- `unlock_node_id` FK
- `unlocked_at`

PK:
- `(user_id, unlock_node_id)`

비고:
- 앞으로는 `무기 강화`보다 `무기 해금`, `무기 트리 해금` 표현 사용

---

## 5.4 런 결과

### runs
목적:
- 런 시작/종료의 상위 레코드

권장 필드:
- `run_id` PK
- `user_id` FK
- `status`
- `selected_weapon_id`
- `selected_unlock_node_id` nullable
- `started_at`
- `ended_at`

비고:
- MVP에서는 1인 기준
- 추후 멀티 구조 확장 시 `run_players` 분리 가능

---

### run_results
목적:
- 런 종료 결과 요약

권장 필드:
- `run_id` PK/FK
- `result` (`clear`, `death`, `surrender`)
- `duration_seconds`
- `chapter_reached`
- `stage_reached`
- `memory_shards_earned`
- `bosses_defeated`
- `rooms_cleared`
- `enemies_killed`
- `saved_at`

비고:
- 최소 필수값:
  - 플레이 타임
  - 런 결과
- 그 외는 단계적 확장 가능

---

### run_build_snapshots
목적:
- 종료 시점 최종 빌드 요약 저장

권장 필드:
- `run_id` PK/FK
- `final_hp`
- `final_gold`
- `selected_artifacts_json`
- `shop_purchases_json`
- `extra_summary_json`

비고:
- 밸런스 분석용
- MVP 필수는 아님
- JSON 컬럼 허용 가능

---

## 6. 런 결과 데이터 서빙 전략

## 6.1 기본 원칙
백엔드는 인런 중 상태를 계속 조회/서빙하는 서버가 아니다.  
백엔드는 **런 종료 시점에 Unity가 넘겨준 결과 요약을 저장하고, 이후 조회 API로 서빙**하는 쪽에 집중한다.

즉,
- 실시간 current_hp 조회 API
- 실시간 current_gold 조회 API
- 실시간 current_artifacts 조회 API

같은 건 백엔드 핵심 책임이 아니다.

---

## 6.2 최소 서빙 대상

### 플레이어 본인용 결과 조회
예:
- 최근 런 결과
- 마지막 클리어/사망/포기 기록
- 누적 플레이 시간
- 최근 획득한 memoryShards

### 후순위 전적/통계
예:
- 최근 20판 결과
- 평균 플레이 타임
- 무기별 클리어율
- 유물 선택 빈도
- 사망 지점 분포

---

## 6.3 MVP 최소 결과 저장 포맷
Unity → 백엔드 전달 예시:

```json
{
  "runId": "run_001",
  "result": "clear",
  "durationSeconds": 842,
  "chapterReached": 1,
  "stageReached": 1,
  "memoryShardsEarned": 10,
  "selectedWeaponId": "sword",
  "summary": {
    "roomsCleared": 20,
    "bossesDefeated": 1,
    "enemiesKilled": 87
  }
}
```

이 정도면 MVP 기준으로는 충분하다.

---

## 6.4 확장 결과 저장 포맷
밸런스 분석이나 전적 확장 시:

```json
{
  "runId": "run_001",
  "result": "death",
  "durationSeconds": 731,
  "chapterReached": 1,
  "stageReached": 1,
  "memoryShardsEarned": 4,
  "selectedWeaponId": "fire_sword_01",
  "finalBuild": {
    "artifacts": [
      "art_warrior_strap",
      "art_red_fang",
      "art_counter_mark"
    ],
    "finalHp": 0,
    "finalGold": 85
  },
  "choiceLog": {
    "rewardChoices": ["art_red_fang", "art_counter_mark"],
    "shopPurchases": ["small_potion"]
  }
}
```

비고:
- 이건 **후순위**
- 현재는 저장 가능성만 열어두면 됨

---

## 6.5 정산 원칙
런 종료 시 백엔드는 아래 순서로 처리한다.

1. `runs` 종료 처리
2. `run_results` 저장
3. 획득한 `memoryShards`를 `user_currencies`에 합산
4. 필요 시 `run_build_snapshots` 저장
5. 조회 API에서 서빙 가능 상태로 전환

---

## 7. API 전략 방향

## 7.1 권장 최소 API
- `POST /auth/signup`
- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/logout`

- `GET /player/me`
- `GET /player/me/currency`

- `GET /memory`
- `GET /memory/{memoryId}`
- `POST /memory/fragments/{fragmentId}/unlock`

- `GET /talent`
- `PUT /talent/allocation`

- `GET /weapons`
- `POST /weapons/unlock/{unlockNodeId}`

- `POST /runs`
- `POST /runs/{runId}/end`
- `GET /runs/history` (후순위)

---

## 7.2 API 설계 메모
- `POST /runs` 는 런 시작 기록
- `POST /runs/{runId}/end` 는 결과 요약 저장
- `GET /runs/history` 는 후순위 전적 기능
- 인런 실시간 상태 API는 MVP 범위에서 축소

---

## 8. 마스터 데이터 관리 전략

## 8.1 DB가 아니라 JSON/테이블 우선인 대상
- 유물 정의
- 적 정의
- 보상 테이블
- 상점 구성 테이블
- 무기 트리 정의
- 기억/조각 정의

## 8.2 이유
- 자주 튜닝될 가능성
- 클라이언트/기획/밸런스 협업 필요
- 런타임에서 읽기 중심
- 스키마 변경 부담을 줄일 수 있음

## 8.3 권장 방식
- 초기엔 `JSON + 서버 로드`
- 안정화 후 일부만 DB화 검토

즉,
- **유저 상태 = DB**
- **게임 정의 데이터 = JSON/테이블**
- **런 결과 = DB**
로 정리하는 게 좋다.

---

## 9. MVP 기준 비범위 / 후순위

### 비범위
- 인런 실시간 authoritative 상태 저장
- gold 영속화
- 실시간 보상/상점 상태 서버 동기화
- 관리자 페이지 고도화

### 후순위
- 상세 전적 분석
- 유저 아이템 선택 로그 전체 저장
- 랭킹 고도화
- 멀티 런 공용 결과 구조
- 재접속 복구용 런 스냅샷 저장

---

## 10. 남은 논의 항목

아직 후속 합의가 필요한 부분:
- 1단계 provider 명칭
- 스토리 진행도 vs 챕터 진행도 저장 구조
- 다운 부활 패널티가 결과 데이터에 포함될 필요가 있는지
- 무기 트리 노드 정의를 JSON으로 둘지 DB 마스터로 둘지
- 기억의 조각 생성 규칙과 중복 처리 방식

---

## 11. 한 줄 정리

현재 데이터 전략은 **인런 실시간 상태는 Unity가 관리하고, 백엔드는 계정/영구 성장/런 종료 결과만 영속화하는 구조**로 간다.  
스키마는 **계정/메타 성장/무기 해금/런 결과 중심**으로 최소화하고,  
유물·적·상점·보상·무기 트리 같은 **게임 정의 데이터는 JSON/테이블 중심 서빙 전략**을 우선 채택한다.

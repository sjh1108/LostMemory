# 스키마 필드 역할 설명 보강 문서

이 문서는 기존 스키마 전략 문서에서 정의한 주요 테이블의 **각 필드
역할을 한 줄 수준으로 빠르게 이해할 수 있도록** 보강한 문서다. 목표는
다음과 같다.

-   팀원이 빠르게 이해할 수 있도록 하기
-   API / 서비스 로직 작성 시 필드 의미 혼동 방지
-   DB 변경 시 영향 범위 판단 용이

------------------------------------------------------------------------

# 1. users

  ------------------------------------------------------------------------
  필드              타입 (예시)                          역할
  ----------------- ------------------------------------ -----------------
  user_id           bigint (PK)                          내부 시스템 기준
                                                         유저 식별자. 모든
                                                         데이터의 기준 키

  login_id          varchar                              로그인 시
                                                         사용하는 사용자
                                                         식별 문자열

  password_hash     varchar                              암호화된 비밀번호
                                                         저장 값

  nickname          varchar                              게임 내 표시 이름

  status            varchar                              계정 상태
                                                         (ACTIVE,
                                                         SUSPENDED 등)

  created_at        datetime                             계정 생성 시각

  updated_at        datetime                             계정 정보 마지막
                                                         수정 시각

  last_login_at     datetime                             마지막 로그인
                                                         시각
  ------------------------------------------------------------------------

------------------------------------------------------------------------

# 2. auth_refresh_tokens

  필드               타입          역할
  ------------------ ------------- -----------------------------
  refresh_token_id   bigint (PK)   refresh token 레코드 식별자
  
  user_id            bigint (FK)   해당 토큰이 속한 사용자
  
  token_hash         varchar       실제 토큰을 해시 처리한 값
  
  expires_at         datetime      토큰 만료 시각
  
  revoked_at         datetime      토큰 강제 무효화 시각
  
  last_used_at       datetime      마지막 사용 시각
  
  created_at         datetime      토큰 생성 시각

------------------------------------------------------------------------

# 3. user_currencies

  필드            타입             역할
  --------------- ---------------- ------------------------------
  user_id         bigint (PK/FK)   사용자 식별자
  
  memory_shards   int              현재 보유한 기억의 파편 총량
  
  updated_at      datetime         마지막 갱신 시각

------------------------------------------------------------------------

# 4. weapons

  필드             타입           역할
  ---------------- -------------- -------------------------------------
  weapon_id        varchar (PK)   무기 고유 식별자
  
  name             varchar        무기 이름
  
  weapon_type      varchar        무기 분류 (sword, hammer 등)
  
  base_weapon_id   varchar        상위 무기 식별자 (트리 구조 표현용)
  
  tree_group       varchar        같은 무기 트리 그룹 식별
  
  display_order    int            UI 표시 순서

------------------------------------------------------------------------

# 5. weapon_unlock_nodes

  필드                 타입           역할
  -------------------- -------------- --------------------------------
  unlock_node_id       varchar (PK)   해금 노드 식별자
  
  weapon_id            varchar (FK)   해당 노드가 속한 무기
  
  parent_node_id       varchar        이전 단계 노드
  
  cost_memory_shards   int            해금에 필요한 기억의 파편 비용
  
  node_type            varchar        노드 유형 (weapon, passive 등)
  
  display_name         varchar        UI 표시 이름
  
  display_order        int            UI 표시 순서

------------------------------------------------------------------------

# 6. runs

  필드                      타입           역할
  ------------------------- -------------- --------------------------------------
  run_id                    varchar (PK)   런 식별자
  
  user_id                   bigint (FK)    런을 시작한 사용자
  
  status                    varchar        현재 상태 (in_progress, finished 등)
  
  selected_weapon_id        varchar        시작 시 선택한 무기
  
  selected_unlock_node_id   varchar        선택한 무기 트리 노드
  
  started_at                datetime       런 시작 시각
  
  ended_at                  datetime       런 종료 시각

------------------------------------------------------------------------

# 7. run_results

  필드                   타입              역할
  ---------------------- ----------------- -------------------------------------
  run_id                 varchar (PK/FK)   런 식별자
  
  result                 varchar           결과 상태 (clear, death, surrender)
  
  duration_seconds       int               총 플레이 시간
  
  chapter_reached        int               도달한 챕터
  
  stage_reached          int               도달한 스테이지
  
  memory_shards_earned   int               해당 런에서 획득한 기억의 파편
  
  bosses_defeated        int               처치한 보스 수
  
  rooms_cleared          int               클리어한 방 수
  
  enemies_killed         int               처치한 몬스터 수
  
  saved_at               datetime          결과 저장 시각

------------------------------------------------------------------------

# 핵심 의도 요약

-   모든 필드는 **"이 값이 왜 존재하는가"** 를 기준으로 정의한다.
-   필드명은 최대한 **도메인 의미가 바로 드러나도록** 유지한다.
-   런타임 상태 필드는 DB에 넣지 않는다.
-   결과 데이터는 요약 형태로만 저장한다.

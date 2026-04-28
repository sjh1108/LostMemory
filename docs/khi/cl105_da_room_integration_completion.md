# CL-105 DA 와 기존 방 생성 연동 — 완료 보고

본 문서는 [cl105_da_room_integration_plan.md](cl105_da_room_integration_plan.md) 의 *실제 결과*. 계획과 결과가 다른 부분은 명시.

## 완료 시점

- 날짜: 2026-04-28
- 브랜치: `feat/S14P31C201-323/cl-da-와-기존-방-생성-연동`
- Jira 티켓: `S14P31C201-323`
- 내부 CL 번호: CL-105

## 실제 산출물

### 신규 코드 (1 파일)

```
Assets/_Project/Scripts/Runtime/Stage/
└── DungeonRunBootstrap.cs
```

- `DungeonEventListener` 상속 → DA 가 빌드 콜백 자동 호출
- 시드 결정(무작위 또는 고정) → `Dungeon.Build()` 호출
- `OnSpawnedManagedObjects` 콜백에서 *첫 module 식별* → player warp + `BeginRoomEntry` 직접 호출
- `IsAuthority` 게이트 (현재 항상 true, 후속 네트워크 CL 에서 호스트 권위 분기로 전환)

### 코드 수정 (작은 fix)

- `RoomEntryRuntimeController.Awake()` — `SetExitWallsActive(false)` 추가
  - 이유: prefab active 상태 의존 제거, 안전한 기본 상태 보장

### 디자이너 산출물 (수동 작업 완료)

- 모든 module prefab 에 CombatRoom 셋업 추가 (RoomEntryRuntimeController + 자식 EntryAnchor / EntryZone / ExitWall × N / SpawnPoints)
- module 별 1:1 RoomData asset 작성
- SnapConnection Category 분리 (Horizontal / Vertical)
- FlowGraph Start/End 카테고리 셋업 (`start_room` / `boss_room`)

### plan 과의 차이

- 거의 그대로. plan 의 *구현 순서* 1~7 모두 처리.
- 가장 큰 차이는 *시드 정책*. plan 은 "런 시작 시 무작위 + 호스트 권위" 로 가볍게 적었으나, 실제로는 *시드 의존성* 이 큰 이슈로 드러나 검증 단계에서는 **고정 시드**로 정책 변경 (아래 핵심 발견 참조).

## 검증 결과 — 시나리오별

| 시나리오 | 결과 | 비고 |
|---|---|---|
| **A** — 단일 module 던전 한 사이클 | ✅ 통과 | DA module spawn → bootstrap warp → BeginRoomEntry → wave / clear / exit |
| **B** — N 방 던전 전체 흐름 | ✅ 통과 | `Module_Bridge 1` 의 EntryZone GameObject 에 `Room Entry Zone` 컴포넌트 누락 발견 → Inspector 에서 컴포넌트 추가 + Controller 슬롯 연결 후 통과 |
| **C** — 첫 방 자동 트리거 | ✅ 통과 | `OnSpawnedManagedObjects` 콜백 → 첫 module 의 `BeginRoomEntry` 직접 호출 (OnTriggerEnter2D 의 *이미 trigger 안* 한계 회피) |
| **D** — 시드 고정 동일 던전 재현 | ✅ 통과 | `Randomize Seed On Start = false` + `Fixed Seed = <검증 시드>` 로 구성. 같은 시드로 두 번 빌드 시 동일 던전 모양 |

## 🔥 핵심 발견 (plan 에 없던 것)

### DA Snap2D 의 *시드 의존성*

검증 중반에 모듈 겹침 미스터리의 진짜 원인을 발견:

- 의심했던 후보들 (모두 부분 영향만): Tilemap bounds, Collision Test Contraction 값, SnapConnection forward 방향, FlowGraph edge, 모듈 풀 사이즈
- **진짜 원인**: 런 시작 시 random seed 가 매번 새로 결정 → 같은 module 셋업으로도 *어떤 시드는 정상 / 어떤 시드는 겹침*

즉 *셋업 결함*이 아니라 **DA Snap2D 가 시드별 배치 안정성에 한계가 있다** 는 본질.

### 영향 / 정책 변경

- **검증 단계** — 고정 시드 (검증된 좋은 값) 박아두기. `DungeonRunBootstrap.fixedSeed` 사용
- **프로덕션 런** — 무작위 시드는 *fallback / 재시도 로직과 함께* 가야 함 (현재 미구현, 후속 CL 영역)
- **시나리오 D 의 의미 재해석** — "시드 고정 = 동일 던전 재현" 이 단순 검증을 넘어 *건전성(sanity) 검증* 으로 더 중요해짐

### 사용자 워크플로우 (인스펙터)

`DungeonRunBootstrap` 컴포넌트:
- `Randomize Seed On Start = false` (체크 해제)
- `Fixed Seed` = 정상 빌드 확인된 시드 값
- `Snap Config.Seed` 필드는 *결과 표시용*. `BuildRun()` 이 매번 덮어씀

## 잔여 / 후속 처리 필요

### 임시 fix (정리 대상)

- `EnemyEncounterSpawner.HardenSpawnedInstance` — 적 prefab 의 ItemPicker / DeadlineCollectible NPE 회피용
- `EnemyEncounterSpawner.RunWave` 의 `yield return null` — race 회피용
- 둘 다 **적 prefab 정리 (CL-037~042 인계, [cl037_042_enemy_handoff.md](cl037_042_enemy_handoff.md)) 완료 시 제거** 권장

### 정책 / 시스템 보강 (후속 CL)

- DA Build 실패 / 모듈 겹침 *자동 감지 + 재시도* 로직 — 본 CL 비범위
- 검증된 시드 풀 보유 + 디자이너 가이드 — 디자이너 도구 (CL-103) 와 합쳐 검토
- `Snap2D` 의 `Max Processing Power` / `Collision Test Contraction` 튜닝 가이드

### 멀티플레이 영역

- 시드 RPC (호스트 권위 → 클라이언트 동기화) — 네트워크 CL 시점에 wiring
- `IsAuthority` 게이트 한 줄만 바꾸면 분기 가능하도록 코드는 준비 완료

## 후속 CL

| CL | 주제 | 본 CL 과의 관계 |
|---|---|---|
| CL-014 | Player 다운·부활 | 본 CL 비범위였던 *room failure* / 사망 처리 |
| CL-047 / CL-048 | 런 상태 전이 / 런 상태 머신 | 전역 *현재 방 추적* / *런 종료 판정* / 시드 멀티 동기화 |
| CL-049 | 보스방 진입 조건 | 현재는 FlowGraph End 노드가 자동 보스. *N 방 클리어 시* 등 조건 강화 |
| CL-103 | 디자이너 도구 | module 별 RoomData + CombatRoom 셋업 비용 절감 |
| CL-106 | 유물 효과 적용 | 별도 epic. 보상 트리거 위치는 본 CL 의 RoomCleared 이벤트 (의존성 검토 필요) |

## CL-105 1차 완료 기준 — 평가

plan 의 마지막 섹션 기준:

- [x] 단일 module 던전 (시나리오 A) 의 *한 사이클* 동작
- [x] N 방 던전 (시나리오 B) 의 *전체 흐름* 동작
- [x] 첫 방 자동 진입 (시나리오 C) 동작
- [x] 시드 고정 재현 (시나리오 D) 동작
- [x] 디자이너가 *새 module 추가* 시 *해당 RoomData 만 작성* 하면 자연 동작 — 코드 수정 X
- [x] 적 측 / 보상 UI / Player failure / 멀티플레이 RPC 영역 침범 없음
- [x] 후속 CL (047/048/049 등) 가 본 CL 산출물 위에 자연 통합 가능

→ **모든 기준 충족**. CL-105 1차 완료.

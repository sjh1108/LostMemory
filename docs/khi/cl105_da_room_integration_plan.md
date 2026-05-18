# CL-105 DA 와 기존 방 생성 연동 — 계획

## 목적

DungeonArchitect (DA) 의 *Snap2D 빌더* 가 절차적으로 생성한 module 인스턴스에, CL-034~036 에서 만든 우리 *방 단위 시스템* (RoomData + RoomEntryRuntimeController + Encounter / Wave / Clear / Exit) 을 연결한다. 결과: **DA 가 생성한 던전이 실제로 *플레이 가능한* 방 흐름을 가지는 상태**.

후속 CL-047 (런 상태 전이) / CL-048 (런 상태 머신) 이 *전역 던전 진행 / 멀티플레이 동기화* 를 흡수. 본 CL 은 *DA 와 우리 방 시스템의 한 사이클 동작* 까지 책임.

## 사용자 의도 (확정)

- (Q1) **1:1 module ↔ RoomData 매핑** — 각 module prefab 에 *고유* RoomData asset
- (Q2) **첫 방 / 보스방 = FlowGraph 의 Start/End 노드 전용 카테고리** (`start_room` / `boss_room`)
- (Q3) **모든 module 이 *방*** — Bridge 도 적 spawn 포함. 즉 RoomData / Controller 모두 부착
- (Q4) **시드 = 런 시작 시 무작위 + 호스트 권위** (의견 반영, 고정 시드는 후속 CL)

## 현 상태 인벤토리

| 영역 | 상태 |
|---|---|
| DA Snap2D 빌더 라이브러리 | ✅ 셋업 완료 (`Assets/CodeRespawn/DungeonArchitect/`) |
| Snap2D sample (`DemoBuilder_Snap2D`) | ✅ 동작 확인. Module_Horizontal / Vertical / Bridge 셋업 |
| URP Material 변환 | ✅ 200+ .mat 자동 변환 (이전 작업) |
| 우리 RoomData / RoomEntryRuntimeController 시스템 | ✅ CL-034~036 완료. *방 단위 자율* 모델 |
| `CombatRoom_Sample_Small.prefab` 검증 | ✅ Wave / Clear / Exit 한 사이클 통과 |
| 적 prefab (Orc / OrcRider / SkeletonArcher) | ⚠️ 임시 hardening fix 적용 중 (`HardenSpawnedInstance`). 적 담당자 협의 영역 |
| DA Snap2D 의 SnapConnection Category 셋업 | ⚠️ 이번 CL 에서 Horizontal / Vertical 분리 |
| Module 별 RoomData asset | ❌ 미작성. 본 CL 의 디자이너 수동 작업 |
| Module prefab 의 CombatRoom 셋업 (RoomEntryRuntimeController 등) | ❌ 미작성. 본 CL 의 디자이너 수동 작업 |
| FlowGraph 의 Start / End 카테고리 | ❌ 미설정. 본 CL 의 디자이너 수동 작업 |
| 런 시작 / DA build 완료 시 첫 방 트리거 | ❌ 미작성. 본 CL 의 코드 작업 |

## 결정 사항

1. **각 module prefab 에 *우리 CombatRoom 셋업* 추가**
   - 자식 구조: `RoomEntryRuntimeController` + `RoomEntryAnchor` + `RoomEntryZone` + `RoomEncounterAnchor` + `EnemyEncounterSpawner` + `RoomExitWall × N` + `SpawnPoints`
   - 즉 `CombatRoom_Sample_Small.prefab` 의 자식 트리를 *각 module prefab* 에 동일 적용
   - DA 가 module instantiate 시 *자식 GameObject 들도 같이 spawn* → controller Awake 자동 발동

2. **Module ↔ RoomData 1:1 매핑**
   - `Module_Horizontal_A.prefab` ↔ `RoomData_Module_Horizontal_A.asset`
   - module prefab 의 `RoomEntryRuntimeController.roomData` 슬롯에 대응 asset 연결
   - 디자이너가 module 디자인 시 RoomData (Encounter / ClearCondition) 도 같이 작성

3. **SnapConnection Category 분리**
   - 가로 module 의 동/서 출구 = Category `Horizontal`
   - 세로 module 의 남/북 출구 = Category `Vertical`
   - 가로↔세로 매칭 차단 → *방향 섞임* 방지

4. **FlowGraph 의 Start / End 노드 전용 카테고리**
   - Start 노드 → Category `start_room` — 첫 방 전용 module 만 매칭
   - End 노드 → Category `boss_room` — 보스방 전용 module 만 매칭
   - 일반 노드 → `combat_room` (또는 카테고리별 분리)

5. **시드 정책**
   - 런 시작 시 *무작위 시드 결정* (단일 플레이 또는 호스트)
   - DA 의 `DungeonConfig.Seed` 에 입력
   - 호스트 권위 가드 — 클라이언트는 호스트 시드 수신 (네트워크 CL 에서 RPC wiring)
   - 고정 시드 (튜토리얼 / 데일리) 는 후속 CL

6. **DA 던전 Build 완료 콜백 + 첫 방 자동 트리거**
   - DA 의 `Dungeon` 컴포넌트가 build 완료 시 이벤트 발행 (또는 우리가 wrapping)
   - 우리 측 `DungeonRunBootstrap` (가칭) 이 콜백 받아:
     - `start_room` 카테고리 module 인스턴스 식별
     - 그 안의 `RoomEntryRuntimeController` 호출 → `BeginRoomEntry(player)`
     - player 위치를 그 방의 `RoomEntryAnchor` 로 정렬

7. **Player 사망 / 다음 층 / UI 부수 시스템**
   - 본 CL 비범위
   - CL-014 (다운·부활) / CL-048 (런 상태 머신) 영역

## 코드 구조 (변경 범위)

추가 (1 파일):

```
Assets/_Project/Scripts/Runtime/Stage/
  DungeonRunBootstrap.cs          # DA 빌드 호출 + 시드 결정 + build 완료 콜백 + 첫 방 BeginRoomEntry
```

수정:

```
(우리 코드 측 큰 변경 없음 — 우리 시스템이 *방 단위 자율* 이라 DA 와 자연 정합)

가능한 작은 수정:
- RoomEntryRuntimeController — DA 회전·미러 시 EntryAnchor 좌표 검증 (필요 시)
```

자산 (디자이너 수동 작업):

```
Assets/_Project/Prefabs/Rooms/Modules/    (또는 비슷)
  Module_Horizontal_A.prefab               # 각 module 에 CombatRoom 셋업
  Module_Horizontal_B.prefab
  Module_Vertical_A.prefab
  ...

Assets/_Project/ScriptableObjects/Rooms/Modules/
  RoomData_Module_Horizontal_A.asset       # 각 module 에 1:1 RoomData
  RoomData_Module_Horizontal_B.asset
  ...

(FlowGraph 자산 수정 — Start/End 카테고리)
```

## 디자이너 수동 작업 — Unity 에디터

### 1. SnapConnection Category 분리

**모든 module prefab** 의 SnapConnection 컴포넌트 Category 필드:
- 가로 module 의 동/서 출구 → `Horizontal`
- 세로 module 의 남/북 출구 → `Vertical`

(이전 세션에서 이미 안내한 패턴)

### 2. SnapConnection rotation 정합

원본 sample (Module_Bridge) 의 rotation 값 그대로:
- 북 (위) `(-90, 0, 0)`, 남 (아래) `(90, 0, 180)`
- 서 (왼) `(0, -90, 0)`, 동 (오른) `(0, 90, 0)`

### 3. Module prefab 별 *CombatRoom 셋업* 추가

각 module prefab 에 `CombatRoom_Sample_Small.prefab` 의 자식 구조 복사:

```
Module_X
├── (기존 Tilemap + Grid)
├── (기존 SnapConnection 들)
│
├── RoomEntryRuntimeController     ← 루트에 컴포넌트로
│     RoomData → RoomData_Module_X (asset)
│     EnemyCatalog → EnemyCatalog_Default (asset)
├── RoomEncounterAnchor             ← 루트에 컴포넌트로
├── EnemyEncounterSpawner           ← 루트에 컴포넌트로
│
├── EntryAnchor_Default (자식)
│     Room Entry Anchor (Anchor Tag = "default")
├── EntryZone (자식)
│     Box Collider 2D (Is Trigger = true)
│     Room Entry Zone (Controller = 부모 RoomEntryRuntimeController)
├── ExitWall_East (자식, 출구 위치)
│     Box Collider 2D (Is Trigger = false)
│     Room Exit Wall (Exit Tag = "east")
├── ExitWall_West / North / South ... (출구 방향마다)
└── SpawnPoints (자식)
      ├── Spawn_NearDoor_0  Room Encounter Spawn Point (Group Tag = "near_door")
      ├── Spawn_Far_0       (Group Tag = "far")
      └── ...
```

### 4. Module 별 RoomData 작성

각 module 마다 1:1 RoomData asset:
- Room Id: 고유 (예: `room_module_horizontal_a`)
- Encounter: Wave 1 ~ N 셋업 (Orc / OrcRider / SkeletonArcher 등)
- Init Context: `playerSpawnAnchorTag = "default"` 등
- Clear Condition: `AllEnemiesDefeated`
- Exits: 각 ExitWall 의 anchorTag 와 매칭

### 5. FlowGraph 의 Start / End 카테고리 설정

`Assets/CodeRespawn/DungeonArchitect_Samples/DemoBuilder_Snap2D/FlowGraphs/` 의 그래프 자산 또는 우리 신규 FlowGraph:

- Start 노드 → Category `start_room`
- End 노드 → Category `boss_room`
- 그 외 → `combat_room`

각 카테고리에 대응하는 module 만 등록.

### 6. 검증 씬

기존 `test_khi.unity` 또는 새 검증 씬:
- DungeonSnapSideScroller (builder) GameObject 배치
- `DungeonRunBootstrap` 컴포넌트 추가
- ▶ Play → DA 던전 자동 생성 → 첫 방 자동 진입

## 구현 순서

1. **단일 module 검증** (1~2일)
   - Module_Horizontal 1 개에 CombatRoom 셋업 추가
   - 1:1 RoomData asset 작성
   - DA 가 module 1개 짜리 던전 생성 → 우리 controller 자동 동작 확인

2. **`DungeonRunBootstrap` 작성** (0.5일)
   - DA 의 `Dungeon` 컴포넌트 build 콜백 구독
   - 시드 결정 + Build 호출
   - build 완료 시 `start_room` 카테고리 module 식별
   - 그 module 의 controller 의 `BeginRoomEntry` 호출

3. **DA 회전 / 미러 호환성 검증** (0.5일)
   - DA 가 module 을 미러 spawn 했을 때 우리 자식 GameObject 들 (EntryAnchor / SpawnPoints / ExitWall) 정상 동작 확인
   - 미러 시 좌표가 *기대대로 반영* 되는지

4. **전체 module 셋업 확장** (디자이너 작업, 시간 변동)
   - 모든 module prefab 에 CombatRoom 셋업
   - 각 module 별 RoomData asset 작성
   - SnapConnection Category 분리 (Horizontal / Vertical 또는 더 세분)

5. **FlowGraph 의 Start / End 카테고리** (0.5일)
   - FlowGraph 노드 그래프 수정
   - 첫 방 / 보스방 전용 module 분리 등록

6. **시드 정책** (0.5일)
   - 런 시작 시 무작위 시드 결정
   - 호스트 권위 가드 (`IsAuthority`) — 네트워크 CL wiring 은 미루고 stub

7. **검증** + design doc 보강 (0.5일)
   - 시나리오 A~D 통과
   - design doc 의 *현재 검증 결과* 업데이트

총 코드 작업량: 1~3일 (DungeonRunBootstrap 작성 + 회전 검증).
디자이너 수동 작업량: module 풀 크기에 따라 1~5일.

## 검증

수동 (Unity 에디터):

**시나리오 A — 단일 module 던전**
- FlowGraph 를 *Module 1개짜리 던전* 으로 단순화
- 그 module 에 CombatRoom 셋업 + RoomData
- ▶ Play → DA 던전 생성 → 첫 방 자동 진입 → wave spawn → 적 처치 → RoomCleared → 출구 벽 해제
- 기대: CL-034~036 의 한 사이클이 *DA 가 생성한 module 안에서* 그대로 동작

**시나리오 B — N 방 던전**
- FlowGraph 를 *3~5 방 sequence* 로
- 모든 module 에 CombatRoom 셋업
- ▶ Play → 첫 방 자동 진입 → 클리어 → 출구 통해 다음 방 → ... → 마지막 방까지 한 사이클

**시나리오 C — 첫 방 자동 트리거**
- `DungeonRunBootstrap` 이 build 완료 시 *start_room* 카테고리 module 식별 + BeginRoomEntry 호출
- player 가 그 방의 EntryAnchor 위치로 자동 정렬

**시나리오 D — 시드 고정 시 동일 던전 재현**
- DungeonConfig.Seed 를 특정 값으로 고정 → 두 번 빌드 → 던전 모양 동일
- 시드 변경 → 다른 던전

자동 (선택, EditMode):
- `DungeonRunBootstrap` 의 *start_room module 식별 로직* 단위 테스트

## 후속 작업

- **CL-047 / CL-048** (런 상태 전이 / 런 상태 머신) — 전역 *현재 방 추적* / *런 종료 판정* / *시드 멀티플레이 동기화*
- **CL-049** (보스방 진입 조건) — *N 방 클리어 시* 보스 등장 등 조건 강화. 현재는 FlowGraph 의 End 노드가 자동 보스
- **CL-014** (다운·부활) — Player 사망 / room failure 처리
- **고정 시드 지원** — 튜토리얼 / 데일리 / 디자이너 검증용 시드 매핑 시스템
- **분기 던전** — FlowGraph 에 분기 / 합류 노드 도입 + Corner module 추가
- **Module weight / 분포 통제** — DA Snap 의 한계 우회. SnapGridFlow 빌더 전환 검토 또는 module pool 확장

## 위험 / 결정 보류

- **DA 회전 / 미러 시 자식 GameObject 호환성** — 미검증. 첫 검증 시 자식 좌표가 기대대로 변환되는지 확인 필요. 깨지면 *회전 / 미러 비활성화* 옵션 검토.
- **Awake 자동 발동** — DA 의 module Instantiate 가 Unity 의 표준 Instantiate 라면 Awake 자동. 별도 구현이면 검증 필요.
- **적 prefab 의 임시 fix (`HardenSpawnedInstance`) 의 영향** — DA 가 만든 module 안의 Spawn 도 같은 fix 가 적용되는지 확인. 적 담당자 협의 결과 (`cl037_042_enemy_handoff.md`) 가 정리되면 fix 제거.
- **디자이너 워크플로우 비용** — 매 module 별 RoomData + CombatRoom 셋업 = *module 작성 시간 2~3 배*. 디자이너 도구 (CL-103) 가 이 비용 줄여줄 수 있음.
- **Sample 의 Bridge module** — sample 디자인이 *통로* 전제. 우리는 *작은 방* 이므로 디자이너가 *전투 가능 작은 방* 으로 다시 디자인 필요.
- **멀티플레이 시드 RPC** — 본 CL 비범위. 네트워크 CL 시점에 wiring.
- **FlowGraph 의 Start/End 카테고리 셋업** — DA 의 grammar 노드 에디터 학습 곡선. 디자이너가 도구 익숙해지는 시간.
- **Module weight 통제 부재** — DA Snap 의 한계 (이전 세션 발견). 풀이 작으면 분포 치우침. *module 풀 크기* 가 디자이너의 자연 해결책.

## CL-105 1차 완료 기준

- 단일 module 던전 (시나리오 A) 의 *한 사이클* 동작
- N 방 던전 (시나리오 B) 의 *전체 흐름* 동작
- 첫 방 자동 진입 (시나리오 C) 동작
- 시드 고정 재현 (시나리오 D) 동작
- 디자이너가 *새 module 추가* 시 *해당 RoomData 만 작성* 하면 자연 동작 — 코드 수정 X
- 적 측 / 보상 UI / Player failure / 멀티플레이 RPC 영역 침범 없음
- 후속 CL (047/048/049 등) 가 본 CL 산출물 위에 자연 통합 가능

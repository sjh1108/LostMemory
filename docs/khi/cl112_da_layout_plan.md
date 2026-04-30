# CL-112 DA 던전 레이아웃 정의 (전투4 + 상점1 + 보스1, 6방 1런) — 계획

## 목적

MVP 데모 흐름 (`마을 → 던전 → 6 방 → 결과 → 마을`) 의 *던전 자체* 를 정의. CL-105 의 DA Snap2D 빌더 위에 **6 방 1 런 명시 sequence + 시드 고정** 적용 + RoomData asset 6 개 생성.

본 CL 은 *layout 의 형태 + 데이터* 만 책임. 각 방의 *내부 동작* (Shop UI / Boss 처치 → RoomCleared) 은 후속 CL 로 분리.

- **상위 계획**: MVP 데모 흐름 통합 — `마을 → 던전 → 전투2 → 상점 → 전투2 → 보스 → 결과 → 마을 복귀`
- **선행**: CL-105 (DA 와 기존 방 생성 연동), CL-110 (보상 UI ↔ 방 클리어 연동)
- **후속**: CL-113 (Shop module + RoomType 분기), CL-114 (Boss module + Bertha wiring), CL-117 (마을 ↔ 던전 씬 전환)

## 사용자 의도 (확정)

- (Q1) **scope = layout 정의만** — RoomData 6 개 + FlowGraph + 시드. Shop/Boss module 셋업은 CL-113/114 로 분리
- (Q2) **6 방 순서 강제 = FlowGraph 노드 명시 연결** — Snap2D 유지 (SnapGridFlow 전환 X)
- (Q3) **Shop module 외관 = Combat module 복제 + 적 제거 + 구매 trigger** (CL-113 산출물, 본 CL 은 *카테고리 placeholder* 만)
- (Q4) **DA 시드 = 사용자 제공 안전 시드값** (본 CL Step 3 placeholder — 즉시 갱신)

## 현 상태 인벤토리

| 영역 | 상태 |
|---|---|
| DA Snap2D 빌더 (`DemoBuilder_Snap2D`) | ✅ CL-105 완료. FlowGraph 절차적 build 동작 |
| `DungeonRunBootstrap.cs` | ✅ `randomizeSeedOnStart` / `fixedSeed` 옵션 존재 (l.32-103) |
| `RoomData.cs` + `StageRoomType` enum | ✅ Combat / Shop / Boss / Event 모두 정의 |
| 기존 RoomData asset | ⚠️ `RoomData_Sample_Combat_Small.asset` / `RoomData_Sample_Boss.asset` 만 존재. **MVP 6 방용 asset 미작성** |
| FlowGraph 노드 카테고리 | ✅ `start_room` / `combat_room` / `boss_room` 정의 (CL-105). **`shop_room` 카테고리 미추가** |
| FlowGraph 의 *명시 sequence 강제* | ❌ 절차적 sampling 만. 본 CL 의 검증 대상 (spike 필요) |
| Combat module prefab | ✅ `CombatRoom_Sample_Small.prefab` 동작 |
| Shop module prefab | ❌ 부재. 본 CL 은 *placeholder 로 Combat module 등록* (CL-113 에서 교체) |
| Boss module prefab | ⚠️ `BossArea_Test.prefab` 짐작 (검증 미실시). **CL-114 에서 정식화** |
| 안전 DA 시드 | ⚠️ 사용자 제공 예정 |

## 결정 사항

### 1. RoomData asset 6 개 신규 생성

위치: `client/LostMemory/Assets/_Project/ScriptableObjects/Stage/Rooms/`

| asset 이름 | RoomType | RoomCategory | sequenceIndex |
|---|---|---|---|
| `RoomData_MVP_Combat_01.asset` | Combat | SmallRoom | 1 |
| `RoomData_MVP_Combat_02.asset` | Combat | SmallRoom | 2 |
| `RoomData_MVP_Shop_01.asset` | Shop | SmallRoom | 3 |
| `RoomData_MVP_Combat_03.asset` | Combat | SmallRoom | 4 |
| `RoomData_MVP_Combat_04.asset` | Combat | LargeRoom | 5 |
| `RoomData_MVP_Boss_01.asset` | Boss | LargeRoom | 6 |

`RoomId` = asset 이름 또는 짧은 식별자 (`mvp_combat_01` 등). Encounter / ClearCondition 같은 *방 내부 데이터* 는 후속 CL 에서 채움 — 본 CL 은 *식별자 + 타입* 만.

### 2. FlowGraph 명시 sequence 그래프 신규

대상: `Assets/CodeRespawn/DungeonArchitect_Samples/DemoBuilder_Snap2D/FlowGraphs/SnapFlowGraph2D.asset`

**전략**: 원본은 *DA 샘플* 이라 보존. 복사본 생성 → `Assets/_Project/DA/SnapFlowGraph2D_MVP.asset`.

**그래프 구조** (DA Snap2D Editor):

```
[Start]──[Combat_1]──[Combat_2]──[Shop]──[Combat_3]──[Combat_4]──[Boss]
```

- 각 노드 = 한 방 모듈 인스턴스
- 분기 노드 0 개 — *sequence 강제 핵심*
- 노드 카테고리:
  - Start: 기존 `start_room` 활용
  - Combat_1~4: 기존 `combat_room` (4 회 등장)
  - Shop: `shop_room` 신설
  - Boss: 기존 `boss_room`

### 3. DA Snap Module Database 에 `shop_room` 카테고리 추가

대상: 기존 Module Database asset 의 *복사본* 생성 → `Assets/_Project/DA/SnapModuleDatabase_MVP.asset`. 패키지 원본 보존.

- 기존 `combat_room` / `boss_room` 옆에 `shop_room` 카테고리 등록
- 본 CL 의 `shop_room` 카테고리 = *Combat module prefab 등록* (placeholder)
- CL-113 에서 *진짜 Shop module prefab* 으로 교체

### 4. DungeonRunBootstrap 시드 고정 + asset slot 교체

대상: test_khi.unity 또는 신규 dungeon 씬의 `DungeonRunBootstrap` GameObject

Inspector 변경 (코드 변경 X):
- `randomizeSeedOnStart = false`
- `fixedSeed = <사용자 제공 안전 시드값>`
- DA `Dungeon` 컴포넌트의 FlowGraph slot → `SnapFlowGraph2D_MVP.asset`
- DA `Dungeon` 컴포넌트의 Module Database slot → `SnapModuleDatabase_MVP.asset`

### 5. Editor 검증 (5 항목)

1. RoomData asset 6 개 존재 + 필드 정합 (RoomType / Category / sequenceIndex)
2. FlowGraph spike 통과 — Build 5 회 반복 시 항상 같은 6 방 sequence
3. NRE 0 건 (안전 시드 효과)
4. 각 방 spawn 후 `RoomEntryRuntimeController.roomData` slot 에 올바른 RoomData 부착 (DA 자동 wiring 안 되면 module prefab 별 수동 부착)
5. 플레이어 첫 방 자동 진입 — `DungeonRunBootstrap.OnSpawnedManagedObjects` (l.109-152) 의 `BeginRoomEntry()` 호출 확인

## 본 CL 범위 외 (후속 ticket 으로 분리)

| 후속 CL | 내용 |
|---|---|
| **CL-113** | Shop module prefab 신규 (Combat 복제 + 적 제거 + 구매 trigger) + `ShopRoomClearController` 신설 + `RoomEntryRuntimeController` 의 RoomType=Shop 분기 + Module Database 의 `shop_room` 슬롯 교체 |
| **CL-114** | Boss module prefab 정식화 (BerthaRoot + `BossDefeatRoomClearController` wiring 검증) + RoomType=Boss 분기는 [`RunManager.cs:223`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs:223) 에 이미 존재 |
| **CL-117** | 마을 → 던전 씬 전환 — 본 CL 의 dungeon 씬을 마을 씬에서 LoadScene |

## 위험 / fallback

### 1. FlowGraph 명시 sequence 가능성 (가장 큰 위험)

DA Snap2D 가 *분기 없는 일렬 그래프* 를 *절차적 sampling 없이* 그대로 따를 수 있는지 사전 spike (30 분) 필수.

- **spike 절차**: FlowGraph 신규 → 7 노드 (Start + Combat×2 + Shop + Combat×2 + Boss) → 일렬 connection → Build 5 회 반복 → *항상 같은 sequence* 인지 확인
- **성공 시**: 본 CL 의 Step 2/3 그대로 진행
- **실패 시 fallback**: *수동 .unity 씬 1 개* 에 6 module 일렬 배치 (DA 사용 안 함). 본 CL scope 변경 + 별도 ticket spinoff 또는 본 CL 안에서 처리. 단 *DA 의 procedural 장점 잃음* 데모 후 CL-124 (보류) 로 추적

### 2. 시드 의존성 잔존

FlowGraph 가 sequence 강제해도 *각 노드의 module instance 선택* (combat_room 카테고리 안 어떤 prefab) 은 시드 의존. 안전 시드 1 개로 *항상 같은 던전* 보장. 시드값 사용자 제공 즉시 plan Step 4 갱신.

### 3. DA Build NRE (cl105 후속)

안전 시드로 우회. 정 안 되면 CL-124 (보류 ticket) 진단.

### 4. Snap Module Database 의 `shop_room` 카테고리 추가

DA 패키지 데이터 직접 수정 — 패키지 업데이트 시 충돌. **복사본 (`SnapModuleDatabase_MVP.asset`) 신규 생성** 으로 회피.

## 핵심 파일

### 신규
- `client/LostMemory/Assets/_Project/ScriptableObjects/Stage/Rooms/RoomData_MVP_Combat_01.asset` ~ `_04.asset`
- `client/LostMemory/Assets/_Project/ScriptableObjects/Stage/Rooms/RoomData_MVP_Shop_01.asset`
- `client/LostMemory/Assets/_Project/ScriptableObjects/Stage/Rooms/RoomData_MVP_Boss_01.asset`
- `client/LostMemory/Assets/_Project/DA/SnapFlowGraph2D_MVP.asset` (FlowGraph 복사본 + 명시 sequence)
- `client/LostMemory/Assets/_Project/DA/SnapModuleDatabase_MVP.asset` (Module Database 복사본 + `shop_room` 카테고리 추가)

### 수정 (Inspector 만)
- 던전 씬의 `DungeonRunBootstrap` GameObject — `randomizeSeedOnStart=false` + `fixedSeed=<사용자 제공>`
- 던전 씬의 `Dungeon` 컴포넌트 — FlowGraph / Module Database slot 을 MVP 사본으로 교체

### 참조 (읽기 전용)
- [DungeonRunBootstrap.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs)
- [RoomData.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/Data/RoomData.cs)
- [StageRoomType.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/Data/StageRoomType.cs)
- [cl105_da_room_integration_completion.md](cl105_da_room_integration_completion.md)

## 작업자 메모 (TODO)

- [ ] **사용자에게 안전 시드값 받기** — Step 4 의 `fixedSeed` placeholder 갱신
- [ ] **사전 30 분 spike** — FlowGraph 명시 sequence 가능성 검증. 실패 시 fallback (수동 씬) 으로 plan 갱신
- [ ] **본 CL 마무리 시점** — `cl112.md` 완료 보고서 작성 (cl108/cl109/cl110 표준 구조)

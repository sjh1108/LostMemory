# Epic D — β: 손으로 짠 Tilemap 방 2개로 fun + 워크플로우 검증 (상세판)

## Context

CL-032~036(방 데이터/스폰/진입/클리어/문 전환) 코드/와이어링은 끝났지만 실제 맵 위에서 굴린 적이 없다. 최종적으로는 **방 N개 손으로 짜기 + DA stitching = production 던전**이 목표 아키텍처. 그러나 그 전에 답이 필요한 질문이 3개 있고, 한 번에 답하면 변수 폭발 → 분리해서 푼다.

**검증해야 할 3가지 독립 질문:**
1. **플레이 재미** — 한 방에서 한 바퀴 돌면 재밌는가?
2. **제작 워크플로우** — 새 방 만드는 비용이 sustainable 한가?
3. **DA 조합** — 만든 방들 stitch했을 때 다양성/연결이 잘 되는가?

이번 β 슬라이스는 **질문 1 + 2** 만. 질문 3은 후속 γ.

**왜 DA 우회**: 단일 방 fun-check에 DA 기능적 기여 0. 검증되지 않은 디자인을 production 템플릿으로 박제하는 위험 회피.

**왜 방 1개가 아니라 2개**: 1번째는 학습. 2번째에서 진짜 워크플로우 cost 드러남.

---

## 사용자 결정 사항 (확정)

- 범위: Tilemap 방 2개 (DA 모듈 사용 X)
- 타일: `_Project/Art/Tiles/BasicPalette.prefab` + `BossATile/` (프로젝트 표준 PPU 일관성 유지)
- 적: `EnemyCatalog_Default.asset`의 CL-037~042 melee/charge/ranged
- Out of scope: DA 결합, 멀티룸 전환, 시드/네트워크

---

## Approach

기존 `CombatRoom_Sample_Small.prefab`의 와이어링 그대로 두고, 안에 **Tilemap 자식만 추가**하는 방식. 코드 0 수정.

---

## Step 0. 타일 import 사전 검증 (5~10분)

### 0-a. 타일 PPU/Filter 확인
1. Project view: `Assets/_Project/Art/Tiles/BossATile/TX Tileset Grass_0.asset` 클릭
2. Inspector 상단의 Sprite Reference 따라가서 원본 PNG 클릭 (`TX Tileset Grass.png` 같은 파일, 보통 `Pixel Art Top Down Basic/Texture/`에 있음)
3. Inspector에서 다음 값 확인:
   - `Sprite Mode`: Multiple
   - `Pixels Per Unit`: **프로젝트 표준값에 일관** (cl032 문서는 64 권장이나 절대값 아님 — 플레이어/적 sprite와 같은 PPU면 OK)
   - `Filter Mode`: **Point (no filter)**
   - `Compression`: None
4. 안 맞으면 수정 → 우하단 `Apply` 클릭
5. `Sprite Editor` 버튼 → 상단 `Slice` 드롭다운 → `Grid By Cell Size` → 원본 셀 크기 (예: 32x32 또는 64x64) → `Slice` → 우상단 `Apply`

**PPU 일관성 빠른 확인 (1분):**
- Hierarchy/Project에서 플레이어 prefab → Sprite Renderer → 참조 sprite → Inspector → Pixels Per Unit 값 확인
- 타일 PPU와 동일하면 → 그대로 진행
- 다르면 → 한쪽 통일 (작업량 적은 쪽으로)

### 0-b. BasicPalette 등록 확인
1. `Window > 2D > Tile Palette` 열기
2. 상단 `Active Tilemap` 옆의 팔레트 드롭다운에 **BasicPalette** 가 보이면 OK
3. 안 보이면: `Assets/_Project/Art/Tiles/BasicPalette.prefab` 을 Tile Palette 창 안으로 드래그
4. 팔레트 안에 grass 타일들이 보이는지 확인. 비어 있으면: BossATile/ 폴더의 타일들을 팔레트 창으로 다중 선택해서 드래그

### 0-c. Console clean 확인
- Console 창 열고 빨간 에러 0건 확인. 있으면 import 실패 → 위 단계 재확인

---

## Step 1. Room A — 첫 번째 방 (학습)

### 1-a. Layout prefab 복제
1. Project view: `Assets/_Project/Prefabs/Rooms/CombatRoom_Sample_Small.prefab` 클릭
2. `Ctrl+D` → 새 프리팹 생성됨
3. F2 (또는 우클릭 > Rename) → `CombatRoom_Custom_RoomA` 입력 → Enter

### 1-b. Prefab 편집 모드 진입
1. `CombatRoom_Custom_RoomA.prefab` 더블클릭
2. Hierarchy 배경이 회색으로 변하면 prefab 편집 모드 진입한 것
3. 루트 GameObject 선택 → Inspector에서 다음 컴포넌트 그대로인지 확인:
   - `RoomEntryRuntimeController`
   - `RoomEncounterAnchor`
   - `EnemyEncounterSpawner`
4. 자식 그대로인지: `SpawnPoints` (안에 4개), `EntryZone`, `ExitWall_East`, `EntryAnchor_Default`

### 1-c. Floor Tilemap 생성
1. Hierarchy: 루트 GameObject 우클릭 → `2D Object > Tilemap > Rectangular`
2. 자동으로 `Grid` + 그 자식 `Tilemap` 생성됨
3. `Tilemap` 자식 이름을 `FloorTilemap` 으로 변경
4. `FloorTilemap` 선택 → Inspector → `Tilemap Renderer` 컴포넌트:
   - `Sorting Layer`: Default
   - `Order in Layer`: **0**

### 1-d. Wall Tilemap 생성 (별도 레이어)
1. Hierarchy: 위에서 만든 `Grid` 우클릭 → `2D Object > Tilemap > Rectangular`
2. 새로 생긴 `Tilemap` 자식 이름을 `WallTilemap` 으로 변경
3. `WallTilemap` 선택 → Inspector → `Tilemap Renderer`:
   - `Order in Layer`: **1** (floor 위)
4. `Add Component` → `Tilemap Collider 2D`
5. `Add Component` → `Composite Collider 2D` (자동으로 `Rigidbody 2D` 추가됨)
6. `Rigidbody 2D` 컴포넌트:
   - `Body Type`: **Static**
7. `Tilemap Collider 2D` 컴포넌트:
   - `Used By Composite`: **체크**

### 1-e. Floor 그리기
1. Hierarchy에서 `FloorTilemap` 선택 (페인트 대상이 됨)
2. Tile Palette 창에서 grass 타일 (예: `TX Tileset Grass_0`) 선택
3. 브러시 도구 (B 키) 활성
4. Scene 뷰에서 (0,0) 중심으로 **10칸(가로) × 6칸(세로)** 사각형 페인트
   - 좌표 가이드: 좌하단 (-5, -3) → 우상단 (5, 3)
5. Scene 뷰에서 floor 가 깔린 거 확인

### 1-f. Walls 그리기
1. Hierarchy에서 `WallTilemap` 선택
2. Tile Palette에서 다른 색 grass 또는 stone 계열 타일 선택 (벽 구분용)
3. Floor 둘레로 벽 한 줄 그리기:
   - 좌측 벽: x=-6, y=-3~3 (7칸)
   - 우측 벽: x=5, y=-3~3 — **단 가운데 2칸 (y=-1, y=0)은 비워둠** (출구)
   - 위쪽 벽: y=3, x=-5~5 (10칸)
   - 아래쪽 벽: y=-3, x=-5~5 — **단 가운데 2칸 (x=-1, x=0) 비워둠** (입구; 또는 좌측 벽 가운데를 비우는 게 더 직관적)
4. 입구/출구 위치는 자유. 권장: 입구 = 좌측 벽 가운데, 출구 = 우측 벽 가운데

### 1-g. 기존 자식 위치 재배치
**Hierarchy에서 각 자식 클릭 → Inspector → Transform > Position 수정.**

| 자식 | Position (x, y, z) | 비고 |
|---|---|---|
| `EntryAnchor_Default` | (-5, 0, 0) | 입구 안쪽 1칸 |
| `EntryZone` | (-5.5, 0, 0) | 입구 트리거. BoxCollider2D Size (1, 2) |
| `Spawn_NearDoor_0` | (-3, 1.5, 0) | 입구 근처 위 |
| `Spawn_NearDoor_1` | (-3, -1.5, 0) | 입구 근처 아래 |
| `Spawn_Far_0` | (3, 1.5, 0) | 반대쪽 끝 위 |
| `Spawn_Far_1` | (3, -1.5, 0) | 반대쪽 끝 아래 |
| `ExitWall_East` | (5, 0, 0) | 출구 위치. BoxCollider2D Size (1, 2) |

**중요한 검증** (각 SpawnPoint 마다):
- Inspector → `Room Encounter Spawn Point` 컴포넌트 → `Group Tag` 필드:
  - `Spawn_NearDoor_0/1` → `near_door` 그대로
  - `Spawn_Far_0/1` → `far` 그대로
- 비어 있거나 다른 값이면 클리어 안 됨 → 다시 입력

**ExitWall_East 검증:**
- Inspector → `Room Exit Wall` 컴포넌트 → `Exit Tag` = `east` 그대로

### 1-h. Prefab 저장
1. `Ctrl+S` (또는 prefab 편집 모드 상단의 Save 버튼)
2. Hierarchy 좌상단의 `<` 화살표 클릭 → prefab 편집 모드 빠져나옴

### 1-i. RoomData 작성
1. Project view: `Assets/_Project/ScriptableObjects/Rooms/RoomData_Sample_Combat_Small.asset` 클릭
2. `Ctrl+D` → `RoomData_Custom_RoomA` 로 리네임
3. 새 asset 클릭 → Inspector:
   - `Room Id`: `room_a` 입력
   - `Display Name`: `Room A` (선택)
   - `Layout Prefab`: 비워둬도 됨 (현재 무참조 필드, 문서용)
   - `Encounter` / `Exits` 등은 그대로 유지

### 1-j. Prefab을 RoomData_Custom_RoomA 와 연결
1. `CombatRoom_Custom_RoomA.prefab` 더블클릭 (다시 prefab 편집 모드)
2. 루트 GameObject 클릭
3. Inspector → `Room Entry Runtime Controller` → `Room Data` 필드:
   - 현재 `RoomData_Sample_Combat_Small` 일 것
   - Project view 에서 `RoomData_Custom_RoomA.asset` 을 드래그해서 슬롯에 떨굼
4. `Ctrl+S` → 편집 모드 나옴

### 1-k. 테스트 씬 만들기
1. Project view: `Assets/Scenes/Test/test_map.unity` 클릭 → `Ctrl+D` → `test_room_a` 로 리네임
2. `test_room_a.unity` 더블클릭 (씬 열림)
3. Hierarchy: 기존 `CombatRoom_Sample_Small` 인스턴스가 있으면 우클릭 > Delete
4. Project view에서 `CombatRoom_Custom_RoomA.prefab` 을 Hierarchy 또는 Scene 뷰로 드래그 → Position (0, 0, 0)
5. 플레이어 인스턴스 위치 조정:
   - Hierarchy 에서 player 프리팹 인스턴스 찾음 (`TestKhi_MinimalCharacter2D` 또는 사용 중인 player)
   - Position을 EntryZone 밖으로 옮김 → 예: (-8, 0, 0)
6. Main Camera 조정:
   - Inspector → `Camera` → `Orthographic Size`: 5~7 (방 전체 + 약간 여유)
   - Position Z = -10 그대로
7. `Ctrl+S` → 씬 저장

### 1-l. Play & 평가 (질문 1 — 재미)
1. 상단 ▶ Play 버튼
2. 플레이어 이동 (W/A/S/D 또는 InputSystem 셋업) → EntryZone 안으로 들어감
3. **Console 검증** (Window > General > Console):
   - `BGM stub: ... cue='combat_default'` 또는 비슷한 로그
   - 에러 빨간색 0건
4. **Scene 시각 검증**:
   - `ExitWall_East` 활성화 (벽이 보이고 통과 못함)
   - Wave 1 적들 스폰 (`near_door` 와 `far` 위치에)
5. 적 처치 → 50% 사망 시 Wave 2 즉시 시작 OR 5초 경과 시 자동
6. 모든 적 사망 → ExitWall 사라짐 → 동쪽 통과 가능
7. ▶ 한 번 더 클릭 → Play 종료

**평가 메모 (이 자리에서 바로 적기)**:
- 방 크기 적당? (좁음 / 적당 / 넓음)
- 입구→첫 충돌 타이밍 적당?
- 스폰 위치가 압박감 적당? (너무 가까움 / 적당 / 너무 멈)
- 적 수 적당? (적음 / 적당 / 많음)
- 클리어 후 출구 통과까지 템포 ok?
- **다른 형태 시도해보고 싶은 거 1개 메모** → Room B 디자인에 반영

---

## Step 2. Room B — 두 번째 방 (워크플로우 측정)

**의도적으로 다른 형태로 만든다**. 예시:
- L자 모양
- 정사각 (8x8)
- 좁고 긴 복도 (16x4)
- 큰 정사각 + 가운데 벽 기둥 1개

### 2-a. 시간 재기 시작 ⏱️
지금 시각 적기. 핸드폰 타이머 또는 stopwatch.

### 2-b. Prefab 복제
1. Project view: `CombatRoom_Custom_RoomA.prefab` → `Ctrl+D` → `CombatRoom_Custom_RoomB`

### 2-c. Tilemap 다시 그리기
1. `CombatRoom_Custom_RoomB.prefab` 더블클릭
2. Hierarchy: `FloorTilemap` 선택 → Tile Palette → 지우개 도구 (E 키)
3. Scene 뷰에서 기존 floor 다 지움
4. 브러시로 다시 새 형태 그림 (선택한 형태)
5. `WallTilemap` 도 마찬가지로 지우고 다시

### 2-d. 자식 위치 재배치
새 Tilemap 형태에 맞춰 Step 1-g 표의 좌표를 본인 형태에 맞게 재계산. SpawnPoint groupTag 검증 잊지 말 것.

### 2-e. RoomData B
1. Project view: `RoomData_Custom_RoomA.asset` → `Ctrl+D` → `RoomData_Custom_RoomB`
2. `Room Id` = `room_b`
3. (옵션) Encounter spec 살짝 다르게:
   - Wave 1 의 melee 수 4 → 5 또는 3
   - 또는 ranged 0 → 1
4. `CombatRoom_Custom_RoomB.prefab` 의 `Room Entry Runtime Controller > Room Data` 를 `RoomData_Custom_RoomB.asset` 으로 교체

### 2-f. 테스트 씬 B
1. `test_room_a.unity` → `Ctrl+D` → `test_room_b`
2. 열어서 Hierarchy 의 CombatRoom 인스턴스 삭제 → `CombatRoom_Custom_RoomB.prefab` 드래그
3. 플레이어 위치, 카메라 조정
4. 저장

### 2-g. 시간 재기 끝 ⏱️
- 걸린 시간 적기
- **목표**: 15~20분 안에 완료 = sustainable
- **마찰 노트** (꼭 적기): 어디서 막혔는지 1줄씩
  - 예: "Tilemap 지우기 단축키 헷갈림 — E 인 거 까먹음"
  - 예: "ExitWall BoxCollider 크기 매번 손으로 맞추는 거 귀찮음"
  - 예: "RoomData layoutPrefab 슬롯 무참조라 헷갈림"

### 2-h. Play & 평가 (질문 1 다시)
1. ▶ Play
2. 한 바퀴 돌고 평가
3. **Room A vs Room B 비교 메모**: 어느 형태가 더 재밌었나? 왜?

---

## Step 3. 평가 + γ 진행 여부 결정 (10~20분)

### fun (질문 1) 결과 분기
- **둘 다 재미 별로** → 전투/유닛 디자인 자체 이슈 → CL-037~042 적 행동 / 플레이어 콤보(CL-009~018) 다시 보기. **γ 보류**.
- **한 쪽이 명확히 더 나음** → 방 사이즈/형태/스폰 거리 디자인 가이드 1줄로 정리. γ 모듈 템플릿 의 입력값.
- **둘 다 ok** → 다양한 형태가 굴러간다는 신호. **γ로 자신감 ↑**.

### workflow (질문 2) 결과 분기
- **2번째 방 ≤ 20분** → sustainable. **γ 진행**.
- **20~40분** → 마찰 지점 우선 개선 (예: 공통 자식 prefab 분리, RoomData 자동 생성 Editor 스크립트). 그 다음 γ.
- **40분+** → 워크플로우 자체가 재설계 필요. γ 보류, 마찰 해결 우선.

### 마찰 해결 후보 (메모만)
- `RoomFlowChildren` 같은 자식 prefab으로 SpawnPoints + EntryZone + ExitWall 묶음 → 새 방 만들 때 드래그 한 번으로 끝
- Editor 메뉴 `LostMemory > Stage > Create Room From Tilemap` 같은 자동화
- RoomData 자동 생성 (prefab → SO 한 번에)

---

## 검증 (End-to-end)

각 방마다:
- `Scenes/Test/test_room_a.unity` (또는 b) Play
- EntryZone 진입 → Console `BGM stub` 발화 → ExitWall 활성
- Wave 스폰 → 전멸 → `RoomCleared` → ExitWall 비활성 → 통과 가능
- 진입~클리어 한 사이클 시간 측정 (재미와 별개로 템포 지표)

---

## Out of scope (명시)

- DA `Module_Horizontal` 사용 / DA `Dungeon.Build()` / Snap stitching → γ 슬라이스
- 멀티룸 CL-036 다음 방 전환 / 시드 결정론 / NGO 동기화 → 후속
- DA 데모 머티리얼 URP 변환 → DA 슬라이스 갈 때
- 보스방 흐름, 신규 적 종류 → 별도 티켓
- 자기 도트 production 품질 보강 (Step 0에서 import 설정만, 미관 작업 X)
- 신규 코드 작성 (`Editor/` 자동화 스크립트 등) — Step 3 마찰 해결로 식별되면 그때 별도 티켓

---

## 위험 + 막힐 만한 포인트

1. **타일 import 설정 일관성** — BasicPalette/BossATile 의 PPU/Filter 가 안 맞으면 게임뷰에서 솔기/jitter. Step 0 에서 미리 확인.
2. **Wall Tilemap의 collider 3종 세트** — `TilemapCollider2D` + `CompositeCollider2D` + `Rigidbody2D(Static)` + `Used By Composite` 체크. 하나라도 빠지면 플레이어 통과 또는 콜라이더 깨짐.
3. **Floor Tilemap에 콜라이더 붙이지 말 것** — 플레이어가 floor 위에 못 서고 막힘.
4. **EntryZone BoxCollider2D `Is Trigger` 체크** — 안 켜져 있으면 진입 트리거 발화 안 함. Sample prefab은 이미 켜져 있을 거지만 위치 변경 후 재확인.
5. **SpawnPoint groupTag 보존** — 위치 옮기다가 컴포넌트 reset 하면 빈 문자열 됨 → 클리어 안 남.
6. **Player 위치 vs EntryZone** — 시작부터 EntryZone 안에 있으면 진입 트리거 발화 안 할 수 있음 (트리거는 OnTriggerEnter 로 동작). 반드시 밖에 시작.
7. **RoomData_Custom_RoomA 참조 미연결** — Step 1-j 까먹으면 Room A/B 둘 다 같은 SO 가리켜서 차이 안 남.
8. **Console URP 분홍색 머티리얼 경고** — DA 데모 자산 임포트되어 있으면 무관한 경고가 떠도 무시 가능 (이번 슬라이스는 DA 안 씀)

---

## Critical Files

**참조용 (수정 X)**
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs`
- `client/LostMemory/Assets/_Project/ScriptableObjects/Enemies/EnemyCatalog_Default.asset`

**복제 원본**
- `client/LostMemory/Assets/_Project/Prefabs/Rooms/CombatRoom_Sample_Small.prefab`
- `client/LostMemory/Assets/_Project/ScriptableObjects/Rooms/RoomData_Sample_Combat_Small.asset`
- `client/LostMemory/Assets/Scenes/Test/test_map.unity`

**신규 (사용자가 작성)**
- `client/LostMemory/Assets/_Project/Prefabs/Rooms/CombatRoom_Custom_RoomA.prefab`
- `client/LostMemory/Assets/_Project/Prefabs/Rooms/CombatRoom_Custom_RoomB.prefab`
- `client/LostMemory/Assets/_Project/ScriptableObjects/Rooms/RoomData_Custom_RoomA.asset`
- `client/LostMemory/Assets/_Project/ScriptableObjects/Rooms/RoomData_Custom_RoomB.asset`
- `client/LostMemory/Assets/Scenes/Test/test_room_a.unity`
- `client/LostMemory/Assets/Scenes/Test/test_room_b.unity`

**자산**
- `client/LostMemory/Assets/_Project/Art/Tiles/BasicPalette.prefab`
- `client/LostMemory/Assets/_Project/Art/Tiles/BossATile/` (64개 grass 타일)

---

## 후속 (γ 슬라이스 — 별도 티켓)

β가 fun + 워크플로우 양쪽 모두 OK 신호 받으면:
1. 검증된 방 디자인을 DA 모듈 형식으로 재작성 (SnapConnection 4방향 + PlaceableMarker로 스폰 위치 표시)
2. DA `Module Database SO` 등록
3. DA Snap_SideScroller 빌더로 모듈 N개 stitch → 멀티룸 던전 출력
4. CL-036 다음 방 전환과 통합 → run 한 번 풀 루프
5. cl032 문서의 "Runtime Build 검증" / "NGO 시드 동기화" 항목과 묶어서 한 번에 닫음

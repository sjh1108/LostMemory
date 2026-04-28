# CL-048 런 상태 머신 최소 버전 구현 — 계획

소속 Epic: **Epic D. 런 진행 / 방·적·보스** (1단계 MVP)
짝 티켓 spec: [cl047_run_state_transition_table_plan.md](cl047_run_state_transition_table_plan.md)

## Context

CL-047 spec 의 *코드 구현*. 솔로 시점에 *런 시작 → 결과 화면* 한 사이클을 실제 동작시키는 minimal viable.

- 다른 commonness docs 가 RunManager 존재를 *전제*. 본 CL 이 그 빈자리를 채움
- NGO 는 다음 epic. 솔로 first 확정
- CL-105 (DA · 방 연동) 위에 자연 통합

## 결정 사항

1. **범위**: enum + RunStateMachine + RunManager + DungeonRunBootstrap / RoomEntryRuntimeController 와 *솔로 한 사이클 wiring*
2. **머신 패턴**: Storage (BossRoomDoorController 스타일) — `_state` + `TryTransition(to)` + 가드 매트릭스 + StateChanged event
3. **상태 enum 단일화**: `InRun_Combat / Bridge / Boss` → `InRun` + sub-info 별도 (CL-047 *위험 1번* 결정)
   - Bridge 는 `StageRoomType` 자체에 없음 (Unknown / Combat / Shop / Event / Boss). 단일화가 데이터 모델과 정합
4. **검증 scene** = `test_khi.unity` (DA + bootstrap 셋업됨, RunManager 수동 배치)
5. **Bootstrap scene** = 본 CL 비범위. test scene 수동 배치로 우회
6. **CL-014 미구현 시**: `KhiPlayerStateAggregator.StateChanged` 구독 + `Defeated` 검출 시 즉시 RunFailed (TODO 명시)
7. **결과 UI**: `RunResultPanelView` 가 이미 존재 → *최소 활성화 토글*. `RunResultData` 집계 / 데이터 wiring 은 후속 CL

## 산출물

### 신규 코드 (3 파일)

```
client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/
├── RunState.cs           # enum 6개 (None / Initializing / InRun / RunCleared / RunFailed / Resulting)
├── RunStateMachine.cs    # Storage + 가드 매트릭스 + StateChanged event + 헬퍼
└── RunManager.cs         # Singleton + DontDestroyOnLoad + 외부 시스템 wiring
```

### 코드 수정 (1 파일)

- `DungeonRunBootstrap.cs` — `public event System.Action DungeonBuilt;` 추가 + `OnSpawnedManagedObjects` 진입부에서 발행 (10줄 변경)

### Unity 에셋 수정 (디자이너 / 사용자 수동)

- `test_khi.unity` — RunManager GameObject 추가, Inspector 슬롯 wiring (아래 *셋업 가이드* 참조)

## RunState 가드 매트릭스 (구현됨)

| From | To | 트리거 |
|---|---|---|
| `None` | `Initializing` | `RunManager.StartRun()` |
| `Initializing` | `InRun` | `DungeonRunBootstrap.DungeonBuilt` |
| `Initializing` | `RunFailed` | DA Build 실패 (트리거 미정 — 후속 CL) |
| `InRun` | `RunCleared` | 보스방 RoomCleared |
| `InRun` | `RunFailed` | `KhiPlayerState.Defeated` (CL-014 임시) |
| `RunCleared` | `Resulting` | 2초 delay 후 자동 |
| `RunFailed` | `Resulting` | 2초 delay 후 자동 |
| `Resulting` | `None` | `RunManager.CloseResulting()` |

> 자기 자신 전이 (`X → X`) 는 no-op. 그 외 무효 전이는 reject + 경고 로그.

## Wiring 흐름 (실제 코드 동작)

```
[None] ──── RunManager.StartRun()
   │           ├── TryTransition(Initializing) → StateChanged(None, Initializing) 발행
   │           └── DungeonRunBootstrap.BuildRun() 호출
   ↓
[Initializing] ── DA build → DungeonRunBootstrap.OnSpawnedManagedObjects()
   │                        └── DungeonBuilt event 발행 (warp / BeginRoomEntry 보다 *먼저*)
   │           RunManager.HandleDungeonBuilt()
   │           ├── SubscribeAllRoomControllers() → FindObjectsOfType<RoomEntryRuntimeController>
   │           └── TryTransition(InRun)
   ↓
[InRun] ── 일반방 RoomCleared → 무시 (다음 방 자연 진행, CL-105 영역)
   │     ── 보스방 RoomCleared → TryTransition(RunCleared) → 2s delay → TryTransition(Resulting)
   │     ── KhiPlayerState.Defeated → TryTransition(RunFailed) → 2s delay → TryTransition(Resulting)
   ↓
[Resulting] ── RunResultPanelView.gameObject.SetActive(true) (stub, 데이터 wiring 후속)
   │       ── RunManager.CloseResulting() (외부 호출) → TryTransition(None)
   ↓
[None]
```

## test_khi.unity 셋업 가이드 (사용자 수동 작업)

### 1. RunManager GameObject 생성

1. Hierarchy 우클릭 → **Create Empty** → 이름 `RunManager`
2. Inspector 에서 **Add Component** → `Lost Memory / Stage / Run Manager`

### 2. Inspector 슬롯 wiring

`RunManager` 컴포넌트의 *Refs* 섹션:

| 슬롯 | 연결 대상 |
|---|---|
| **Dungeon Run Bootstrap** | scene 안의 `DungeonRunBootstrap` 가 붙은 GameObject (보통 DA Dungeon GameObject) |
| **Player State Aggregator** | Player 의 `KhiPlayerStateAggregator` 컴포넌트 (보통 Player GameObject 자식 또는 본인) |
| **Run Result Panel View** | UI Canvas 안의 `RunResultPanel.prefab` 인스턴스의 `RunResultPanelView` 컴포넌트 (없으면 비워둠 — stub 동작) |

*Behavior* 섹션:
- **Auto Start On Awake** = ✅ (검증 시 편의용. 실제 게임 흐름에서는 UI 시작 버튼이 호출)
- **Resulting Delay Seconds** = `2` (기본값)

### 3. RunResultPanel 배치 (선택)

- Project 에서 `Assets/_Project/Prefabs/UI/RunResultPanel.prefab` 을 Canvas 에 드래그
- 초기에는 `SetActive(false)` 상태로 두기 (Inspector 에서 GameObject 체크 해제)
- RunManager 의 `runResultPanelView` 슬롯에 연결

### 4. DungeonRunBootstrap 이미 셋업된 부분 (변경 X)

`DungeonRunBootstrap` 컴포넌트의 시드 / 빌드 동작은 *그대로*. CL-105 의 셋업 (`Randomize Seed On Start`, `Fixed Seed`, `Auto Build On Start`, `Warp Player To First Room`) 유지.

> 단, `Auto Build On Start = true` 인 상태에서 `RunManager.Auto Start On Awake = true` 면 *두 번 BuildRun 호출* 가능성. **둘 중 하나만 ✅ 권장**. 추천: RunManager 가 흐름 owner 이므로 `RunManager.AutoStartOnAwake = ✅` + `DungeonRunBootstrap.AutoBuildOnStart = ❌`.

## 검증 시나리오

Console 로그를 보며 진행. RunManager 가 `[RunManager] X -> Y` 형식으로 모든 전이 로그.

### 시나리오 A — 정상 클리어 (한 사이클)

1. `test_khi.unity` Play
2. **기대 로그**:
   ```
   [RunManager] None -> Initializing
   [DungeonRunBootstrap] DA spawned N managed objects (seed=...)
   [RunManager] Subscribed to N room controllers.
   [RunManager] Initializing -> InRun
   [DungeonRunBootstrap] Warped player to first room ...
   ```
3. 방을 진행하며 적 처치 → 일반방 RoomCleared 시 *RunManager 로그 없음* (정상, InRun 유지)
4. 보스방 진입 → 보스 처치 → RoomCleared
5. **기대 로그**:
   ```
   [RunManager] InRun -> RunCleared
   (2초 후)
   [RunManager] RunCleared -> Resulting
   [RunManager] Resulting state — RunResultPanelView activated (stub, no data binding).
   ```
6. RunManager.CloseResulting() 호출 (디버그 키 또는 RunResultPanelView 의 Lobby 버튼) → `Resulting → None` 전이

### 시나리오 B — Player 사망

1. `test_khi.unity` Play → InRun 도달
2. Player HP 0 으로 만들기 (적에게 맞기) → `KhiPlayerState.Defeated`
3. **기대 로그**:
   ```
   [RunManager] InRun -> RunFailed
   (2초 후)
   [RunManager] RunFailed -> Resulting
   ```

### 시나리오 C — 가드 매트릭스

EditMode 또는 Play 중 콘솔에서:

```csharp
RunManager.Instance.StateMachine.TryTransition(RunState.Resulting);
// InRun 상태에서 직접 Resulting 시도 → reject
```

**기대 로그**: `[RunStateMachine] Invalid transition: InRun -> Resulting`. 상태 변경 없음.

### 시나리오 D — Singleton + DontDestroyOnLoad

1. Play 시작
2. RunManager GameObject 가 *DontDestroyOnLoad* scene 으로 이동 확인 (Hierarchy 에서 별도 표시)
3. (옵션) scene 전환 시 Instance 유지 확인

## 검증 체크리스트

- [ ] enum 6개 + 가드 매트릭스 동작
- [ ] TryTransition 가드 통과 / reject 모두 동작
- [ ] StateChanged(prev, current) 이벤트 발행
- [ ] RunManager Singleton + DontDestroyOnLoad
- [ ] StartRun() → Initializing → BuildRun() 체인
- [ ] DungeonBuilt event → InRun 전이
- [ ] 일반방 RoomCleared → 무시 (InRun 유지)
- [ ] 보스방 RoomCleared → RunCleared → Resulting (2초 delay)
- [ ] KhiPlayerState.Defeated → RunFailed → Resulting
- [ ] Resulting → None 복귀 (CloseResulting 호출)
- [ ] 잘못된 transition reject + 로그
- [ ] RunResultPanelView 활성화 (stub, 데이터 바인딩 없음)

## 위험 / 결정 보류

1. **CL-014 미구현 — Defeated 트리거 임시** — `KhiPlayerStateAggregator.StateChanged` 구독, `Defeated` 검출 시 즉시 RunFailed. CL-014 도입 시 *부활 deadline* 로직 추가 (`// TODO(CL-014)` 명시). 멀티 도입 시 *파티 전원 다운 + deadline 만료* 로 변경.
2. **결과 UI 데이터 미연결** — `RunResultData` 집계 (KillCount / TotalDamage / PlayTime 등) 미구현. 본 CL 은 `runResultPanelView.gameObject.SetActive(true)` 까지. 데이터 wiring 은 후속 CL.
3. **Bootstrap scene 부재** — test scene 수동 배치로 우회. RunManager 의 Singleton 패턴 + DontDestroyOnLoad 라 향후 Bootstrap scene 도입 시 *해당 scene 으로 GameObject 이동*만.
4. **AutoBuildOnStart 충돌** — `DungeonRunBootstrap.AutoBuildOnStart` 와 `RunManager.AutoStartOnAwake` 둘 다 ✅ 면 BuildRun 두 번 호출. 셋업 가이드에 *둘 중 하나만* 명시. RunManager 가 흐름 owner 이므로 RunManager 쪽 우선 추천.
5. **InRun 단일화 → CL-047 v1.1 갱신 필요** — 별도 commit 으로 cl047 plan 의 위험 1번 결정 반영. Bridge 가 StageRoomType 에 없다는 발견도 같이 명시.
6. **멀티 도입 시 Authority gate** — `RunManager.IsAuthority` 와 `RunStateMachine.TryTransition` 모두 호스트 권위 진입점 후보. 멀티 wiring 시점에 *RPC 호출 위치* 결정 (RunManager.StartRun → RunStateMachine.TryTransition 라인).
7. **FindObjectsOfType 호출 비용** — `SubscribeAllRoomControllers` 가 `FindObjectsOfType<RoomEntryRuntimeController>()` 호출. DA build 직후 1회만이라 비용 무시 가능. Unity 6+ 에서는 `FindObjectsByType` 권장이지만 호환성 위해 현 API 사용.

## 후속 CL

| CL | 본 CL 과의 관계 |
|---|---|
| CL-014 (다운/부활) | `InRun → RunFailed` 트리거 보강 (부활 deadline). 본 CL 임시 코드의 `// TODO(CL-014)` 라인 제거 |
| CL-049 (보스방 진입 조건) | `InRun` 내부 sub-state (`InRun_Boss` 진입 가드). 본 CL 의 enum 단일화 결정 위에 *조건 강화* 만 |
| CL-106 (유물 효과) | `InRun` 진입 시 `RelicInventory` 라이프사이클 hook (StateChanged 구독) |
| 네트워크 CL (CL-021~026 + 후속) | `RunStateMachine.TryTransition` 에 RPC wiring + `RunManager.IsAuthority` 호스트 권위 분기 |

## 작업량 추정 (실제)

- 코드 작성 (RunState/RunStateMachine/RunManager + DungeonRunBootstrap 수정): 4시간
- docs/khi 정리: 1시간
- Unity 셋업 가이드: 1시간
- (사용자) test_khi.unity 셋업 + Play 검증: 1~2시간
- 총: 1일 이내

## 핵심 파일 (변경 / 신규)

### 신규
- [client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunState.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunState.cs)
- [client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunStateMachine.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunStateMachine.cs)
- [client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs)

### 수정
- [client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs) — DungeonBuilt event 추가

### 참고 (변경 없음)
- [client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs) — `RoomCleared` event 사용
- [client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeEvents.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeEvents.cs) — `RoomClearedPayload.Data` 사용
- [client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerStateAggregator.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerStateAggregator.cs) — `StateChanged` 사용
- [client/LostMemory/Assets/_Project/Scripts/Runtime/UI/RunResultPanelView.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/UI/RunResultPanelView.cs) — gameObject 활성화 stub
- [client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/StageRoomType.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/StageRoomType.cs) — `Boss` 비교 사용

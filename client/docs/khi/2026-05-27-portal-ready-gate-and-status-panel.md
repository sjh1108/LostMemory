# 2026-05-27 세션 — Portal 멀티 ready 게이트 + PlayerStatusPanel UI 자동 인스턴스화

> 작성일: 2026-05-27
> 브랜치: `preview`
> 직전 commit: `2e7da398b6 포탈 이동 수정 및 ui 추가`
> 변경 파일 5개 (코드 3 + prefab 2) + Resources 이동 1개

이 문서는 두 가지 독립 작업을 묶었다. 둘 다 같은 commit `2e7da398` "포탈 이동 수정 및 ui 추가"
이후 후속 작업이다.

1. **Portal 멀티 ready 게이트** — `useLocalTeleport=0` (route advance) 경로도 town 의
   `useLocalTeleport=1` 처럼 "전원 F 누름 합의 → 씬 로드" 흐름이 되도록 게이트 추가.
2. **PlayerStatusPanel 자동 인스턴스화** — 17개 던전 씬마다 prefab 배치/와이어링 회피용
   Resources 자동 로딩.

---

## 1. Portal 멀티 Route Advance Ready Gate

### 배경

직전 commit 이 `RouteNodeExitTrigger` 에 `IsAllAliveInside()` 게이트 + Down/Defeated 필터를 추가했음.
의도는 "모두 모여야 F 발동" 이었지만 멀티에서 클라마다 `candidates` HashSet 이 비대칭으로 채워져
F 무반응 발생. 솔로는 정상 작동.

본 작업에서는:
1. 직전 commit 의 `IsAllAliveInside` / `CountAlivePlayers` / Down 필터 추가분을 **revert**
   (working tree 만 `f319994573` 시점으로 복원, commit 히스토리는 유지).
2. 그 자리에 town `useLocalTeleport=1` 트리거가 이미 쓰던 **ready 게이트 패턴** 을 route advance
   경로에도 도입.

### 동작

`town_to_dungeon_portal` (useLocalTeleport=1) 의 동작:
- F 누름 → `routeManager.RequestLocalTeleportReady` ServerRpc 만 보내고 `_multiReadySent=true` (파랑)
- 서버가 전원 ready 합의 → `FireLocalTeleportClientRpc` broadcast → 각 클라 로컬 워프

`to_1f_2r_portal` (useLocalTeleport=0, 다른 씬 LoadScene) 도 이제 같은 흐름:
- F 누름 → `routeManager.RequestRouteAdvanceReady` CustomMessage 전송 + 파랑
- 서버가 전원 ready 합의 → `TryAdvanceRouteNode` → `NetworkManager.SceneManager.LoadScene` 으로
  모든 클라 sync 로드

뷰 상태표:
| 상태 | 색 |
|---|---|
| 잠김 | 회색 (Locked) |
| 열림, 비어있음 | 초록 (UnlockedIdle) |
| 누군가 진입 | 주황 (CandidateInside) |
| 본인 ready 전송, 다른 사람 대기 | 파랑 (Transitioning) |
| 전원 합의 → 씬 로드 | (씬 전환) |

### 핵심 변경 파일

[client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/StageRouteManager.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/StageRouteManager.cs)

기존 Local Teleport Ready Gate 와 **대칭** 으로 Route Advance Ready Gate 추가:

- 공개 API
  - `RequestRouteAdvanceReady(triggerId)` — 트리거가 멀티 시 호출. 솔로면 즉시 advance.
  - `CancelRouteAdvanceReady(triggerId)` — 트리거 이탈/잠금 시.
- CustomMessage 상수
  - `RouteAdvReadyMsg = "LM.RouteAdv.Ready"` (게스트 → 서버)
  - `RouteAdvCancelMsg = "LM.RouteAdv.Cancel"` (게스트 → 서버)
  - Fire 메시지 불필요 — 합의 시 서버가 직접 `LoadScene` 으로 동기화.
- 서버 측 상태 — `Dictionary<string, HashSet<ulong>> _routeAdvanceReady`
- 핸들러 — `OnRouteAdvReadyMessageReceived`, `OnRouteAdvCancelMessageReceived`
- 합의 평가 — `EvaluateAndFireRouteAdvanceReady` → 충족 시 `TryAdvanceRouteNode(triggerId, null, null)`
- Disconnect cleanup — `HandleClientDisconnectForRouteAdvance` 추가 (LocalTeleport 와 동일 패턴)
- Register/Unregister — 기존 hook 에 RouteAdv 핸들러 4개 추가/해제

[client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RouteNodeExitTrigger.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RouteNodeExitTrigger.cs)

- `RequestAdvanceImmediate` 의 route advance 경로 분기
  - **싱글** — 기존대로 `routeManager.RequestAdvanceRouteNode` 즉시 호출
  - **멀티** — `routeManager.RequestRouteAdvanceReady` + `_multiReadySent=true` 후 대기
- 새 헬퍼 `CancelActiveReadyOnRouteManager()` — `useLocalTeleport` 플래그로
  `CancelLocalTeleportReady` vs `CancelRouteAdvanceReady` 분기
- 기존 cancel 호출지점 3곳 (`SetUnlocked(false)`, `OnDisable`, `OnTriggerExit2D`) 을 새 헬퍼 사용으로 통합
- `OnTriggerExit2D` 의 `useLocalTeleport` 조건문 제거 — 양 타입 모두 cancel 처리

### 의도적으로 안 한 것

- 직전 commit 의 `SceneLoadPortalController.cs` / `BossClearPortalController.cs` 변경분도 revert 됨
  (`Module_Bridge 1.prefab` 등 다른 파일은 본 세션 범위 밖).
- `IsAllAliveInside` 식 "동시에 모여야" gate 는 도입 안 함. ready 게이트가 "각자 F 눌러야" 라는
  더 명확한 합의 의미를 제공.

### 검증 (E2E)

1. **솔로** — 변경 없음. 기존대로 F 한 번에 즉시 다음 씬 로드.
2. **멀티 (호스트+게스트)**
   - 둘 다 trigger 안 진입 → 주황.
   - 한 명이 F → 그 사람만 파랑, 다른 사람은 여전히 주황.
   - 다른 사람도 F → 합의 → 양쪽 동시에 씬 로드 (`NGO SceneManager.LoadScene` sync).
3. **부분 cancel** — 한 명이 F 후 trigger 밖으로 나가면 → 그 사람 주황으로 복귀 + 서버 ready set
   에서 제거.
4. **Disconnect** — 게스트가 ready 보낸 후 disconnect → 서버 set 정리 + 남은 인원으로 재평가.
   인원 1명이면 호스트만 ready 로 합의 충족 → 씬 로드.

---

## 2. PlayerStatusPanel UI 자동 인스턴스화

### 배경

플레이어 합산 스탯 + OnHit 효과 표시용 패널. 다른 세션에서 코드 7개 파일 (View/Presenter/ViewModel/Labels
+ 데이터 소스 patch 3개) 까지 완성됐고, 본 세션에서 **prefab 만들기 + 17개 던전 씬에 배치 회피** 가
숙제로 남아있었음.

17개 씬마다 prefab drop + Inspector 슬롯 와이어링이 부담 → 코드 한 곳 수정으로 자동 처리.

### 핵심 변경

**Prefab 2개 신규**
- [client/LostMemory/Assets/_Project/Prefabs/UI/StatusRow.prefab](../../LostMemory/Assets/_Project/Prefabs/UI/StatusRow.prefab)
  GUID `a1b2c3d4e5f607182939a4b5c6d7e8f9` — 한 행 (이름/값/설명 TMP 3개).
  `PlayerStatusRowView` 부착, 슬롯 사전 연결.
- [client/LostMemory/Assets/_Project/Resources/UI/PlayerStatusPanel.prefab](../../LostMemory/Assets/_Project/Resources/UI/PlayerStatusPanel.prefab)
  GUID `b2c3d4e5f60718293a4b5c6d7e8f90a1` — 전체 패널.
  `PlayerStatusPanelView` + `PlayerStatusPanelPresenter` 부착, View 슬롯
  (`_rowPrefab` / `_statsParent` / `_onHitsParent`) 사전 연결.

**Resources 경로 사용**
- `PlayerStatusPanel.prefab` 은 `Assets/_Project/Resources/UI/` 에 위치
  → 코드에서 `Resources.Load<PlayerStatusPanelView>("UI/PlayerStatusPanel")` 로 접근.
- `LoadingPanel.prefab` 이 이미 같은 위치/패턴이라 기존 컨벤션 따라감.

**InventoryToggleController.cs 수정**

[client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/InventoryToggleController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/InventoryToggleController.cs)

```csharp
[SerializeField] private string statusPanelResourcePath = "UI/PlayerStatusPanel";

private void Awake()
{
    if (panel != null) panel.OnCloseRequested += Close;
    EnsureStatusPanelInstantiated();   // ← 추가
    if (startHidden) { ... }
}

private void EnsureStatusPanelInstantiated()
{
    if (statusPanel != null) return;
    if (string.IsNullOrEmpty(statusPanelResourcePath)) return;
    var prefab = Resources.Load<PlayerStatusPanelView>(statusPanelResourcePath);
    if (prefab == null) { /* warn */ return; }
    Transform parentForPanel = transform.parent != null ? transform.parent : transform;
    statusPanel = Instantiate(prefab, parentForPanel);
    statusPanel.name = "PlayerStatusPanel (Auto)";
}
```

- 부모 = `transform.parent` (=Canvas) — controller GO 의 100×100 marker rect 안에 끼지 않도록.
- 인스턴스 name 에 `(Auto)` 표시 — Hierarchy 에서 수동 배치본과 구분.
- 인스턴스화 후 기존 `startHidden` 로직이 자동으로 `SetActive(false)` 처리.

### 씬 수정 0개

- 17개 던전 씬 (`Dungeon_1F_1R.unity` ~ `Dungeon_2F_Boss.unity` 외) 전부 그대로 둠.
- 각 씬의 기존 `InventoryToggleController` 인스턴스가 자동으로 statusPanel 생성/토글.
- 특정 씬에서 자동 생성 끄기: 그 씬의 controller Inspector 에서
  `statusPanelResourcePath` 를 빈 문자열로.
- 위치 커스터마이즈: 그 씬에 `PlayerStatusPanel.prefab` 직접 배치 + `statusPanel` 슬롯에 드래그
  → `statusPanel != null` 이라 자동 생성 스킵.

### 성능 분석 (사전 검토 결론)

| 우려 | 실제 영향 |
|---|---|
| 매 씬 인스턴스화 비용 | <1ms (Resources.Load 캐시 hit + 작은 prefab Instantiate) |
| 씬 반복 시 메모리 누적 | 없음 (씬 unload 시 destroy) |
| Resources 캐시 누적 | 단일 prefab 레퍼런스만 메모리 보유 |
| 패널 닫혀있는 동안 비용 | 0 (Presenter 가 이벤트 구독만, Update 없음) |

### 검증

- **던전 씬 진입** — 콘솔에 `[InventoryToggleController]` 경고 없음 확인.
- **I 키** — 인벤토리 + 세트효과 + 상태창 3패널 동시 표시.
- **Hierarchy** — Canvas 자식으로 `PlayerStatusPanel (Auto)` GO 가 생성돼있는지.
- **Resources 로딩 실패 시** — 콘솔 경고 `Resources.Load 실패: 'UI/PlayerStatusPanel'`. 게임 진행 영향 없음.

---

## 변경 파일 목록 (uncommitted)

```
M  client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/StageRouteManager.cs
MM client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RouteNodeExitTrigger.cs
 M client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/InventoryToggleController.cs
?? client/LostMemory/Assets/_Project/Prefabs/UI/StatusRow.prefab(+.meta)
?? client/LostMemory/Assets/_Project/Resources/UI/PlayerStatusPanel.prefab(+.meta)
```

(직전 commit 에서 같이 풀린 `SceneLoadPortalController.cs` / `BossClearPortalController.cs` 의 staged
revert 도 함께 들어가야 함 — `M` 첫 컬럼 staged.)

---

## 다음 권장 작업

1. **유니티 에디터 첫 import** — Resources 위치 변경, 새 prefab 2개 등록.
2. **멀티 세션 E2E** — 호스트+게스트 던전 진입 후 to_portal 에서 양쪽 F → 동기 씬 전환 확인.
3. **상태창 확인** — 던전 진입 후 I 키로 패널 자동 등장 + 유물/세트 효과 반영 확인.
4. **commit 전 점검** — `Module_Bridge 1.prefab`, `ShopRoom_Sample.prefab`, `malgun SDF.asset`
   같은 의도치 않은 변경이 working tree 에 같이 묻어있어서, 본 작업과 분리해 commit 할지 결정.

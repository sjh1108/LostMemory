# CL-213 Scene Transition Trigger Placement Plan

## 작업 개요

- Epic: Run Flow / Dungeon Route
- 영역: Stage, SceneFlow, Dungeon
- 작업코드: CL-213
- 작업명: 씬 이동 트리거 배치
- 우선순위: P0
- 산출물: 던전 큰 노드 이동 트리거 배치 계획, 라우트 전환 설계, Unity Editor 배치 절차, 검증 체크리스트

## 현재 판단

현재 던전 구조에서는 `1-1`, `1-2`, `상점`, `1-3`, `1-4`, `보스방`을 큰 진행 노드로 보고, 각 큰 노드 내부의 작은 방들은 씬이 아니라 `RoomEntryRuntimeController` 기반 전투 단위로 유지하는 것이 적절하다.

```text
Stage 1
-> 1-1 large node
   -> small room: enter trigger starts combat
   -> small room: enter trigger starts combat
-> 1-2 large node
-> shop node
-> 1-3 large node
-> 1-4 large node
-> boss node
```

따라서 CL-213의 트리거는 작은 방마다 씬을 바꾸는 용도가 아니라, 현재 진행 중인 작은 방 전투가 없을 때 다음 큰 노드로 넘어가는 출구 트리거로 배치한다.

## 핵심 결정

- 작은 방 전투 시작은 기존 `RoomEntryZone` + `RoomEntryRuntimeController`로 처리한다.
- 큰 노드 이동은 포탈/출구 트리거 UX를 사용한다.
- 포탈 트리거가 `SceneManager.LoadScene()`을 직접 호출하지 않게 한다.
- 트리거는 `RunManager` 또는 별도 `StageRouteManager`에 "다음 노드 이동"을 요청한다.
- `Dungeon_1F_1R.unity`, `Dungeon_1F_2R.unity` 같은 씬은 큰 노드 단위로만 사용한다.
- 장기적으로는 씬 이름 하드코딩 대신 `StageRouteData` 같은 라우트 데이터로 관리한다.

## 배경 근거

현재 프로젝트에는 이미 다음 구조가 있다.

- `SceneLoadPortalController`: 씬 이름을 받아 `SceneManager.LoadScene()`을 직접 호출한다.
- `RunManager`: `DontDestroyOnLoad` 싱글톤이며 런 상태, 방 클리어, 보스 클리어 포탈 흐름을 관리한다.
- `DungeonRunBootstrap`: 던전 빌드/프리빌트 레이아웃을 준비하고 `DungeonBuilt`를 발행한다.
- `RoomEntryRuntimeController`: 작은 방 진입, 전투 시작, 클리어 이벤트, 출구 벽 개방을 관리한다.
- `Dungeon_1F_1R.unity`, `Dungeon_1F_2R.unity`: 각각 큰 던전 레이아웃 씬으로 보이며, 둘 다 `RunManager`와 `DungeonRunBootstrap`을 포함한다.

주의할 점은 `SceneLoadPortalController`를 그대로 던전 내부 이동에 쓰면 기존 `RunManager`가 살아남고 다음 씬의 새 `RunManager`가 duplicate로 제거될 수 있다는 점이다. 그러면 살아남은 `RunManager`가 이전 씬의 `DungeonRunBootstrap`, 플레이어, UI 참조를 들고 있어서 진행 상태가 꼬일 수 있다.

## 목표 플로우

```text
Town
-> Dungeon route start
-> StageRouteManager starts Stage 1
-> load 1-1 large node
-> player clears all required small rooms in 1-1
-> exit trigger becomes usable
-> player enters/interacts with exit trigger
-> StageRouteManager advances to 1-2
-> load 1-2 large node and place player at spawn point
-> repeat until boss
-> boss clear
-> result or town return flow
```

## CL-213 범위

이번 작업의 직접 범위는 "트리거 배치와 연결 기준 확정"이다.

- `Dungeon_1F_1R`의 마지막 출구 위치에 다음 노드 이동 트리거를 배치한다.
- 트리거는 임시라도 `Dungeon_1F_2R`로 가는 목적을 가진다.
- 단, 최종 코드 경로는 직접 씬 로드가 아니라 라우트 전환 API를 호출하도록 설계한다.
- `Dungeon_1F_2R` 진입 지점에는 플레이어 스폰 기준점을 둔다.
- 1-1 내부 작은 방의 전투 시작 트리거는 기존 구조를 유지한다.
- Build Settings에 필요한 큰 노드 씬 등록 여부를 확인한다.

## 멀티플레이어 고려사항

CL-213은 멀티플레이어를 고려해야 한다. 단, 이번 작업의 목표는 멀티 전체 완성이 아니라 "나중에 멀티에서 깨지지 않는 전환 구조"를 잡는 것이다.

기본 원칙:

- 씬 전환 결정권은 host/server만 가진다.
- 클라이언트는 트리거 입력을 직접 씬 로드로 처리하지 않고, 서버에 "다음 노드 이동 요청"만 보낸다.
- 네트워크 세션 중에는 `SceneManager.LoadScene()` 대신 `NetworkManager.Singleton.SceneManager.LoadScene()` 경로를 사용한다.
- 싱글 플레이 또는 네트워크 미시작 상태에서는 일반 `SceneManager.LoadScene()` 경로를 사용할 수 있다.
- 라우트 진행 인덱스, 현재 스테이지 번호, 현재 큰 노드 id는 서버 상태를 기준으로 한다.
- 클라이언트가 임의로 `currentNodeIndex`를 바꾸거나 다음 씬 이름을 지정할 수 없게 한다.

권장 요청 흐름:

```text
Client player enters/interacts with RouteNodeExitTrigger
-> if local client is not server, send ServerRpc: RequestAdvanceRouteNodeServerRpc(triggerId)
-> server validates trigger, player, clear condition, current route node
-> server calls StageRouteManager.AdvanceRouteNode()
-> NGO SceneManager loads next node scene for all clients
-> after scene load, server places/spawns players at RouteNodeSpawnPoint
-> clients receive synchronized player positions through NetworkTransform or existing network player flow
```

싱글/호스트 공통 코드 흐름:

```text
RouteNodeExitTrigger
-> StageRouteManager.RequestAdvanceRouteNode(triggerId, player)
-> if network active and not server: send ServerRpc and return
-> if server or offline: validate and load next route node
```

네트워크 검증 조건:

- 요청한 플레이어가 실제 트리거 범위 안에 있었는지 확인한다.
- 현재 큰 노드 안에서 진행 중인 전투가 없는지 확인한다.
- 요청한 triggerId가 현재 노드의 출구와 일치하는지 확인한다.
- 다음 노드가 route data 안에 존재하는지 확인한다.
- 이미 전환 중이면 중복 요청을 무시한다.

플레이어 배치 정책:

- 서버가 씬 로드 완료 후 `RouteNodeSpawnPoint`를 찾는다.
- 모든 플레이어를 같은 진입 지점 또는 플레이어별 offset 지점에 배치한다.
- Netcode 사용 중이면 서버가 위치를 갱신하고, 클라이언트는 동기화 결과를 따른다.
- 다운/사망/부활 상태를 씬 전환 전에 어떻게 처리할지는 별도 작업에서 확정한다.

CL-213에서 반드시 피할 것:

- 클라이언트에서 직접 `SceneManager.LoadScene()` 호출
- 트리거에 다음 씬 이름만 넣고 로컬에서 바로 로드
- 씬마다 새 `RunManager`가 살아남거나 기존 `RunManager`가 이전 씬 참조를 계속 들고 있는 구조
- 클라이언트가 클리어 조건을 단독 판정하는 구조

## CL-213 제외 범위

- 2스테이지 전체 구현
- 랜덤 라우트 생성
- 분기 선택 UI
- 상점 기능 구현
- 보스방 최종 연출
- 세이브/로드 연동
- 멀티플레이어 클라이언트 동기화 완성
- 모든 던전 씬의 최종 아트 배치

## 권장 구현 단위

### 1. 라우트 데이터 정의

초기에는 ScriptableObject까지 가지 않고 C# 직렬화 필드 또는 씬 매니저 설정으로 시작해도 된다. 다만 최종 형태는 데이터 기반을 권장한다.

```text
StageRouteData_Stage01
  0: Dungeon_1F_1R
  1: Dungeon_1F_2R
  2: Shop
  3: Dungeon_1F_3R
  4: Dungeon_1F_4R
  5: Boss
```

필드 예시:

```text
routeNodeId: stage1_1r
sceneName: Dungeon_1F_1R
entrySpawnId: default
nodeType: CombatLarge
requiredClearPolicy: NoActiveCombat
```

### 2. 라우트 전환 관리자

새 컴포넌트 후보:

```text
Assets/_Project/Scripts/Runtime/Stage/StageRouteManager.cs
```

책임:

- 현재 스테이지 번호와 현재 노드 인덱스를 가진다.
- 다음 노드가 있는지 판단한다.
- 트리거에서 요청한 이동을 검증한다.
- 씬 로드를 한 곳에서 수행한다.
- 씬 로드 후 스폰 위치에 플레이어를 배치한다.
- 기존 `RunManager`와 충돌하지 않도록 참조 재바인딩 정책을 정한다.
- 네트워크 세션 중이면 NGO scene loading 경로를 사용한다.
- 클라이언트 요청은 ServerRpc로 받아 서버에서 검증한다.

초기 API 후보:

```csharp
public bool CanAdvanceRouteNode();
public void RequestAdvanceRouteNode(string triggerId);
public void LoadRouteNode(int nodeIndex);
```

### 3. 트리거 컨트롤러

새 컴포넌트 후보:

```text
Assets/_Project/Scripts/Runtime/Stage/RouteNodeExitTrigger.cs
```

책임:

- `Collider2D` trigger를 가진다.
- 플레이어만 감지한다.
- 필요하면 상호작용 입력을 요구한다.
- 직접 씬 이름을 로드하지 않는다.
- `StageRouteManager.RequestAdvanceRouteNode()`만 호출한다.

기존 `SceneLoadPortalController`는 마을에서 던전으로 들어가는 단순 진입 포탈에는 계속 쓸 수 있다. 던전 내부 큰 노드 이동에는 새 전용 트리거를 쓰는 것이 안전하다.

### 4. 스폰 포인트

새 컴포넌트 후보:

```text
Assets/_Project/Scripts/Runtime/Stage/RouteNodeSpawnPoint.cs
```

필드 예시:

```text
spawnId: default
facing: north/east/south/west
```

씬 로드 후 `StageRouteManager`가 `spawnId`로 위치를 찾고 플레이어를 배치한다.

## Unity Editor 배치 절차

CLI에서 `.unity`, `.prefab`, `.asset`, `.meta`를 직접 수정하지 않는다. 아래 작업은 Unity Editor에서 수행한다.

1. `Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity`를 연다.
2. 1-1 큰 노드의 마지막 출구 위치에 빈 GameObject를 만든다.
3. 이름은 `ExitTrigger_To_1F_2R`로 둔다.
4. `BoxCollider2D` 또는 `CircleCollider2D`를 추가하고 `Is Trigger`를 켠다.
5. `RouteNodeExitTrigger`를 붙인다.
6. `requireInteractInput` 여부를 결정한다. MVP는 `true`와 `E` 입력을 권장한다.
7. 기본 상태에서는 트리거를 사용할 수 있게 둔다.
8. 작은 방 전투가 시작되면 트리거가 잠기고, 해당 전투가 끝나면 다시 열리도록 연결한다.
9. `Assets/_Project/Scenes/Dungeon/Dungeon_1F_2R.unity`를 연다.
10. 진입 위치에 `RouteNodeSpawnPoint`를 배치하고 `spawnId = default`로 둔다.
11. Build Settings에 `Dungeon_1F_2R`가 등록되어 있는지 확인한다.

## 이동 가능 조건 연결

1-1 큰 노드 안의 작은 방을 전부 클리어하지 않아도 다음 큰 노드로 이동할 수 있다. 단, 플레이어가 작은 방 전투를 시작한 상태라면 해당 전투가 끝나기 전까지 큰 노드 출구를 사용할 수 없어야 한다.

초기 정책:

```text
no active small-room combat
-> route exit trigger usable

small-room combat started
-> route exit trigger lock

small-room combat cleared
-> route exit trigger unlock
```

구현 후보:

```text
LargeNodeClearTracker
  - 씬 안의 RoomEntryRuntimeController들을 구독
  - NoActiveCombat 정책에서는 RoomCombatStarted / RoomCleared 이벤트를 감시
  - 전투 시작 시 RouteNodeExitTrigger.Lock()
  - 진행 중인 전투가 0개가 되면 RouteNodeExitTrigger.Unlock()
```

기존 `RunManager`가 모든 `RoomEntryRuntimeController.RoomCleared`를 구독하고 있으므로, CL-213에서는 중복 책임을 만들지 않도록 다음 중 하나를 선택한다.

- A안: `RunManager`가 큰 노드 이동 가능 여부까지 관리하고 트리거를 lock/unlock한다.
- B안: `LargeNodeClearTracker`를 별도 컴포넌트로 두고, `RunManager`는 런 전체 상태만 관리한다.

권장안은 B안이다. 큰 노드 내부의 전투 진행 여부는 씬/노드 로컬 책임이고, `RunManager`는 런 상태와 결과 흐름에 집중하는 편이 안전하다.

## 파일 계획

문서/코드 후보:

```text
docs/planning/CL-213-scene-transition-trigger-placement-plan.md
Assets/_Project/Scripts/Runtime/Stage/StageRouteManager.cs
Assets/_Project/Scripts/Runtime/Stage/RouteNodeExitTrigger.cs
Assets/_Project/Scripts/Runtime/Stage/RouteNodeSpawnPoint.cs
Assets/_Project/Scripts/Runtime/Stage/LargeNodeClearTracker.cs
```

구현 상태:

- `StageRouteManager.cs`: 추가됨. 오프라인/네트워크 세션 분기, ServerRpc 요청, 다음 route node 씬 로드, 로드 후 스폰 배치 담당.
- `RouteNodeExitTrigger.cs`: 추가됨. 플레이어 감지, 상호작용 입력, lock/unlock, route manager 요청 담당.
- `RouteNodeSpawnPoint.cs`: 추가됨. 씬 로드 후 플레이어 배치와 방향 정렬 담당.
- `LargeNodeClearTracker.cs`: 추가됨. 기본 정책은 `NoActiveCombat`이며, 작은 방 전투 중에는 출구 트리거를 잠그고 전투 종료 후 다시 열어준다.

Unity Editor 배치 대상:

```text
Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity
Assets/_Project/Scenes/Dungeon/Dungeon_1F_2R.unity
ProjectSettings/EditorBuildSettings.asset
```

주의: 위 Unity serialized 파일은 사용자가 명시 승인하거나 Unity Editor에서 직접 작업한다.

## 단계별 작업 계획

### Phase 1: 안전한 트리거 코드 준비

- [ ] `RouteNodeExitTrigger` 추가
- [ ] 플레이어 감지 로직 구현
- [ ] 상호작용 입력 옵션 구현
- [ ] `SceneManager.LoadScene()` 직접 호출 금지
- [ ] `StageRouteManager`가 없을 때 경고 로그 출력

### Phase 2: 라우트 이동 최소 관리자

- [ ] `StageRouteManager` 추가
- [ ] `stage1` 초기 route list를 인스펙터에서 설정 가능하게 구현
- [ ] 현재 노드 인덱스 관리
- [ ] 다음 노드 씬 로드 구현
- [ ] `Application.CanStreamedLevelBeLoaded()`로 로드 가능 여부 확인
- [ ] 네트워크 세션에서는 서버만 씬 로드를 시작하도록 분기
- [ ] 클라이언트 요청용 ServerRpc 경로 정의
- [ ] 씬 로드 후 `RouteNodeSpawnPoint`로 플레이어 배치

### Phase 3: 작은 방 전투 상태와 트리거 lock/unlock

- [ ] `LargeNodeClearTracker` 추가
- [ ] 씬 내 전투방 `RoomEntryRuntimeController` 구독
- [ ] `RoomCombatStarted` 시 `RouteNodeExitTrigger.Lock()` 호출
- [ ] `RoomCleared` 시 진행 중 전투가 없으면 `RouteNodeExitTrigger.Unlock()` 호출
- [ ] 중복 전투 시작/종료 이벤트 방지

### Phase 4: Unity Editor 배치

- [ ] `Dungeon_1F_1R` 출구 위치에 `ExitTrigger_To_1F_2R` 배치
- [ ] `Dungeon_1F_2R` 진입 위치에 `RouteNodeSpawnPoint_Default` 배치
- [ ] `StageRouteManager`를 어디에 둘지 결정
- [ ] `Dungeon_1F_2R` Build Settings 등록
- [ ] 트리거 visual placeholder 배치

### Phase 5: 검증

- [ ] Town에서 `Dungeon_1F_1R` 진입
- [ ] 1-1 작은 방 진입 시 전투 시작
- [ ] 작은 방 전투 중에는 다음 노드 트리거 사용 불가
- [ ] 작은 방 전투 종료 후 다음 노드 트리거 사용 가능
- [ ] 작은 방을 모두 클리어하지 않아도 전투 중이 아니라면 다음 노드 이동 가능
- [ ] 트리거 상호작용 시 `Dungeon_1F_2R` 로드
- [ ] 2R 진입 시 플레이어가 지정 스폰 위치에 배치
- [ ] `RunManager` duplicate / stale reference 경고가 없는지 확인
- [ ] 빌드 실행에서도 `Dungeon_1F_2R` 로드 가능
- [ ] Host + Client 세션에서 client가 트리거를 사용하면 server가 검증 후 모든 클라이언트 씬을 전환
- [ ] Client 단독 로컬 씬 로드가 발생하지 않음
- [ ] 씬 전환 중 중복 입력을 여러 번 보내도 한 번만 전환됨

## 리스크

- `RunManager`가 `DontDestroyOnLoad`라서 씬마다 `RunManager`를 둔 현재 구조와 충돌할 수 있다.
- `Dungeon_1F_2R`은 현재 Build Settings에 등록되어 있지 않을 수 있다.
- 직접 `SceneManager.LoadScene()`을 쓰면 기존 런 상태 정리와 새 씬 참조 바인딩이 빠질 수 있다.
- 네트워크 세션에서는 서버/호스트 권한에서만 씬 전환을 시작해야 한다.
- NGO scene loading과 오프라인 scene loading을 같은 API에서 분기하지 않으면 싱글/멀티 동작이 갈라질 수 있다.
- 클라이언트 요청 검증이 없으면 전투 중인 큰 노드에서도 다음 노드로 넘어갈 수 있다.
- 트리거를 씬 YAML로 직접 편집하면 prefab/scene 참조가 깨질 수 있다.

## 완료 기준

- 1-1 큰 노드에서 다음 노드 이동 트리거의 위치와 lock/unlock 조건이 정의되어 있다.
- 트리거가 직접 씬 로드를 호출하지 않는 구조가 문서와 코드에 반영되어 있다.
- 1-1 내부 작은 방 전투 시작 구조는 기존 `RoomEntryRuntimeController` 흐름을 유지한다.
- 1-1에서 전투 중이 아닐 때 1-2로 이동하는 최소 흐름을 Unity Editor에서 배치할 수 있다.
- `Dungeon_1F_2R` 진입 스폰 위치가 명확하다.
- Build Settings 등록 필요성이 체크리스트에 포함되어 있다.

## 후속 작업 후보

| 작업코드 | 작업명 | 설명 |
|---|---|---|
| CL-214 | StageRouteManager 최소 구현 | Stage 1 route list와 다음 노드 이동 API 구현 |
| CL-215 | RouteNodeExitTrigger 구현 | 플레이어 감지, 상호작용, unlock/lock 상태 구현 |
| CL-216 | RouteNodeSpawnPoint 구현 | 씬 로드 후 플레이어 진입 위치 배치 |
| CL-217 | LargeNodeClearTracker 구현 | 큰 노드 안의 작은 방 전투 진행 여부 판단 |
| CL-218 | Stage 1 route data 구성 | 1-1, 1-2, 상점, 1-3, 1-4, 보스방 순서 데이터화 |

# CL-213 씬 이동 트리거 구현 현황

## 개요

- 작업 코드: CL-213
- 영역: 던전 라우트, 씬 이동, 포탈 트리거, 보스방 진입
- 상태: 현재 임시 라우트 기준 MVP 구현 및 Play Mode 검증 완료
- 마지막 갱신일: 2026-05-10

CL-213은 처음에는 `Dungeon_1F_1R`에서 `Dungeon_1F_2R`로 이동하는 씬 이동
트리거 배치 작업으로 시작했다. 현재는 1스테이지 전체 방 구성이 아직 완성되지
않아서 아래 임시 라우트까지 같이 정리된 상태다.

```text
Dungeon_1F_1R
-> Dungeon_1F_2R
-> Dungeon_1F_Boss
-> 결과창
```

`1F_3R`, `1F_4R`, `Shop` 씬은 아직 없으므로 지금은 `Dungeon_1F_2R`에서 바로
보스방으로 이동하도록 구성했다.

## 현재 설계 결정

큰 던전 노드는 Unity 씬 단위로 분리한다. 큰 노드 안의 작은 방들은 기존
`RoomEntryRuntimeController` 기반 진입/전투 구조를 유지한다.

큰 노드 출구 규칙은 다음과 같다.

- 큰 노드 안의 모든 작은 방을 클리어하지 않아도 다음 큰 노드로 이동할 수 있다.
- 단, 현재 작은 방 전투가 진행 중이면 큰 노드 출구 포탈은 잠긴다.
- 진행 중인 전투가 끝나면 출구 포탈이 다시 열린다.

현재 로그라이크 흐름에서는 "모든 작은 방 클리어 필수"보다 "전투 중 이동 금지"가
더 적절하다고 판단했다.

## 구현된 런타임 컴포넌트

### StageRouteManager

파일:

```text
Assets/_Project/Scripts/Runtime/Stage/StageRouteManager.cs
```

역할:

- 인스펙터에서 라우트 노드 목록을 관리한다.
- 라우트 노드 필드:
  - `Node Id`
  - `Scene Name`
  - `Editor Scene Path`
  - `Entry Spawn Id`
  - `Exit Trigger Id`
  - `Load Scene Mode`
- 현재 노드와 출구 트리거 id가 맞는지 검증한 뒤 다음 노드로 진행한다.
- 다음 라우트 노드 씬을 로드한다.
- 네트워크 세션이 켜져 있으면 Host/Server에서 NGO SceneManager로 씬을 로드한다.
- 오프라인 플레이에서는 일반 `SceneManager.LoadScene`을 사용한다.
- Editor Play Mode에서는 `EditorScenePath`를 fallback 경로로 사용할 수 있다.
- `DontDestroyOnLoad`로 씬 이동 후에도 같은 라우트 매니저가 유지된다.
- 씬 로드 후 `RouteNodeSpawnPoint` 위치로 플레이어를 배치한다.

### RouteNodeExitTrigger

파일:

```text
Assets/_Project/Scripts/Runtime/Stage/RouteNodeExitTrigger.cs
```

역할:

- `Collider2D` trigger로 플레이어 후보를 감지한다.
- 상호작용 입력을 지원한다.
- 캐릭터의 `InputManager.InteractButton`을 우선 사용하고, fallback 키는 `E`다.
- 트리거 감지가 불안정한 경우를 위해 거리 기반 fallback도 지원한다.
- 씬을 직접 로드하지 않는다.
- `StageRouteManager.RequestAdvanceRouteNode(...)`로 라우트 진행만 요청한다.
- `Lock()` / `Unlock()`으로 사용 가능 상태를 제어한다.
- 씬 전환 중 중복 요청을 막는다.

### RouteNodeExitTriggerView

파일:

```text
Assets/_Project/Scripts/Runtime/Stage/RouteNodeExitTriggerView.cs
```

역할:

- 포탈의 현재 사용 가능 상태를 색상과 테두리로 보여준다.
- 지원 상태:
  - `Locked`
  - `UnlockedIdle`
  - `CandidateInside`
  - `Transitioning`
- 보스룸 포탈처럼 플레이어가 현재 들어갈 수 있는지 시각적으로 구분할 수 있다.

### RouteNodeSpawnPoint

파일:

```text
Assets/_Project/Scripts/Runtime/Stage/RouteNodeSpawnPoint.cs
```

역할:

- `spawnId`로 라우트 진입 지점을 정의한다.
- 캐릭터를 스폰 지점 위치로 이동시킨다.
- 진입 방향도 함께 적용한다.
- `StageRouteManager`를 통해 간단한 멀티 플레이어 offset 배치를 지원한다.

### LargeNodeClearTracker

파일:

```text
Assets/_Project/Scripts/Runtime/Stage/LargeNodeClearTracker.cs
```

역할:

- 큰 노드 안의 작은 방 전투 상태를 추적한다.
- 현재 사용 정책은 `NoActiveCombat`이다.
- 작은 방 전투가 시작되면 라우트 출구 포탈을 잠근다.
- 진행 중인 전투 수가 0이 되면 라우트 출구 포탈을 다시 연다.
- 모든 작은 방 클리어를 요구하지 않는다.

## 관련 Run Flow 변경

### RunManager 씬 라우트 복구

파일:

```text
Assets/_Project/Scripts/Runtime/Stage/RunManager.cs
```

역할:

- 라우트 씬 전환 후 현재 씬의 `RoomEntryRuntimeController`들을 다시 구독한다.
- 테스트 편의를 위해 직접 로드된 라우트 씬도 활성 런으로 채택할 수 있다.
- 라우트 씬 채택 시 상태 흐름:

```text
None -> Initializing -> InRun
```

- 씬 이동 후 현재 씬의 `RunResultPanelView`를 다시 찾는다.
- 비활성화된 결과창 오브젝트도 찾도록 처리했다.
- 결과창에서 다시시작을 누르면 현재 보스방을 다시 로드하지 않고
  `Dungeon_1F_1R`로 이동한다.

### 보스 씬 자동 진입

파일:

```text
Assets/_Project/Scripts/Runtime/Stage/BossRoomLocalTransitionDriver.cs
Assets/_Project/Scripts/Runtime/Stage/BossSceneAutoStarter.cs
```

역할:

- `BossRoomLocalTransitionDriver.TryStartRouteEntry(...)`가 라우트로 로드된 보스
  씬에서 기존 보스방 진입 전환을 시작할 수 있게 한다.
- `BossSceneAutoStarter`가 보스 씬 로드 직후 잠깐 재시도하면서 보스 진입을 자동
  시작한다.
- 이로 인해 `Dungeon_1F_2R`의 보스 포탈을 타면 `Dungeon_1F_Boss` 씬으로 이동한
  뒤, 기존 보스방 내부 포탈을 수동으로 다시 타지 않아도 보스 흐름이 시작된다.

## 씬 배치 현황

### Dungeon_1F_1R

씬:

```text
Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity
```

상태:

- 현재 테스트 라우트용 `StageRouteManager` / route node 설정이 배치되어 있다.
- `Dungeon_1F_2R`로 이동하는 출구 포탈이 배치되어 있다.
- 출구 트리거 id는 현재 route node의 `Exit Trigger Id`와 맞아야 한다.
- 포탈 시각 상태 표시가 연결되어 있다.
- 작은 방 전투 시작/종료에 따라 `LargeNodeClearTracker`가 포탈을 잠그거나 연다.

### Dungeon_1F_2R

씬:

```text
Assets/_Project/Scenes/Dungeon/Dungeon_1F_2R.unity
```

상태:

- 진입 스폰 포인트가 구성되어 있다.
- 임시 보스 라우트 포탈이 구성되어 있다.
- 현재 다음 목적지는 `Dungeon_1F_Boss`다.
- `Shop`, `1F_3R`, `1F_4R`이 생기면 라우트 순서를 다시 수정해야 한다.

### Dungeon_1F_Boss

씬:

```text
Assets/_Project/Scenes/Dungeon/Dungeon_1F_Boss.unity
```

상태:

- 기존 던전/보스룸 구성을 복사해서 보스방 씬으로 사용한다.
- 보스 자동 시작 컴포넌트가 추가되어 있다.
- 보스 클리어 포탈을 통해 결과창까지 도달한다.
- 씬 전환 후에도 결과창을 다시 찾아 표시하도록 보완했다.

## Build Settings

현재 Build Settings에 등록된 관련 씬:

```text
Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity
Assets/_Project/Scenes/Dungeon/Dungeon_1F_2R.unity
Assets/_Project/Scenes/Dungeon/Dungeon_1F_Boss.unity
Assets/_Project/Scenes/Town/Town.unity
```

따라서 현재 라우트 씬들은 런타임에서 씬 이름으로 로드할 수 있다.

## 검증된 동작

Play Mode에서 확인한 동작:

- `Dungeon_1F_1R` 포탈로 `Dungeon_1F_2R` 이동 가능.
- `Dungeon_1F_2R` 보스 포탈로 `Dungeon_1F_Boss` 이동 가능.
- 보스 씬 진입 후 보스 전투 흐름 자동 시작.
- 보스 클리어 포탈 사용 시 `RunCleared -> Resulting` 도달.
- 보스 클리어 후 결과창 표시.
- 결과창 다시시작 클릭 시 `Dungeon_1F_1R` 로드.

정상 흐름에서 기대되는 로그 예:

```text
[RunManager] None -> Initializing
[RunManager] Adopted loaded route scene as active run.
[RunManager] Initializing -> InRun
[RunManager] Boss room cleared. Boss clear portal is ready.
[RunManager] InRun -> RunCleared
[RunManager] RunCleared -> Resulting
[RunManager] Resulting state - RunResultPanelView shown.
[RunManager] Restart requested. Loading 'Dungeon_1F_1R'.
```

## 검증 메모

- 변경된 C# 파일 기준 `git diff --check`는 통과했다.
- 로컬 PC에 .NET SDK가 없어 CLI `dotnet build`는 실행하지 못했다.
- Unity 프로젝트 특성상 최종 컴파일/동작 검증은 Unity Editor 기준으로 확인해야 한다.

## 멀티플레이어 현황

멀티플레이어를 고려한 구조는 들어가 있지만, Host + Client 실기 검증은 아직
완료되지 않았다.

현재 반영된 멀티 고려 사항:

- `StageRouteManager`는 `NetworkBehaviour`를 상속한다.
- 클라이언트의 라우트 이동 요청은 `ServerRpc` 경로로 보낼 수 있다.
- 네트워크 세션 중에는 `NetworkManager.SceneManager.LoadScene(...)`을 사용한다.
- Non-server client가 직접 네트워크 씬 로드를 시작하지 못하게 막는다.

남은 멀티 검증:

- Host + Client 상태에서 라우트 포탈 사용.
- Client 요청 -> Server 검증 -> 전체 클라이언트 씬 이동.
- 네트워크 씬 로드 후 플레이어 배치.
- 라우트 전환 중 중복 입력 처리.
- 네트워크 세션에서 보스 씬 자동 진입.
- 네트워크 세션에서 결과창 다시시작.

## 현재 임시 처리

- `Dungeon_1F_2R -> Dungeon_1F_Boss`는 임시 라우트다.
- 라우트 데이터는 아직 `ScriptableObject`가 아니라 인스펙터 배열 기반이다.
- `RunManager`의 직접 라우트 씬 채택 로직은 현재 테스트 편의를 위한 실용적 처리다.
- 결과창 다시시작은 `Dungeon_1F_1R`을 직접 대상으로 한다.
- 추후 Town -> Dungeon 흐름이 완성되면 재시작/마을 복귀/던전 진입은 중앙
  `SceneFlowManager` 또는 런 플로우 API 뒤로 옮기는 것이 좋다.

## 남은 작업

- `Shop`, `Dungeon_1F_3R`, `Dungeon_1F_4R` 씬 추가.
- 1스테이지 최종 라우트로 route node 목록 갱신:

```text
Dungeon_1F_1R
-> Dungeon_1F_2R
-> Shop
-> Dungeon_1F_3R
-> Dungeon_1F_4R
-> Dungeon_1F_Boss
```

- 라우트 데이터를 `StageRouteData` ScriptableObject로 분리할지 결정.
- Town -> Dungeon 진입 흐름 확정 후 재시작/마을 복귀 흐름 정리.
- 멀티플레이어 Host + Client 검증.
- 포탈 시각/상호작용 규칙이 확정되면 반복 배치용 prefab 또는 prefab variant 생성.

## 현재 파일 목록

CL-213 핵심 런타임 파일:

```text
Assets/_Project/Scripts/Runtime/Stage/StageRouteManager.cs
Assets/_Project/Scripts/Runtime/Stage/RouteNodeExitTrigger.cs
Assets/_Project/Scripts/Runtime/Stage/RouteNodeExitTriggerView.cs
Assets/_Project/Scripts/Runtime/Stage/RouteNodeSpawnPoint.cs
Assets/_Project/Scripts/Runtime/Stage/LargeNodeClearTracker.cs
```

관련 후속 작업 파일:

```text
Assets/_Project/Scripts/Runtime/Stage/RunManager.cs
Assets/_Project/Scripts/Runtime/Stage/BossRoomLocalTransitionDriver.cs
Assets/_Project/Scripts/Runtime/Stage/BossSceneAutoStarter.cs
```

관련 씬:

```text
Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity
Assets/_Project/Scenes/Dungeon/Dungeon_1F_2R.unity
Assets/_Project/Scenes/Dungeon/Dungeon_1F_Boss.unity
```

## 완료 판단

현재 임시 라우트 기준 CL-213 MVP는 완료로 볼 수 있다.

```text
1F_1R -> 1F_2R -> Boss -> Result -> Restart to 1F_1R
```

다만 아래 항목은 아직 남아 있으므로 "1스테이지 전체 라우트 완료"로 보기는 이르다.

- `Shop` 씬 없음
- `1F_3R` 씬 없음
- `1F_4R` 씬 없음
- 전체 1스테이지 최종 route node 미구성
- 멀티플레이어 Host + Client 검증 미완료

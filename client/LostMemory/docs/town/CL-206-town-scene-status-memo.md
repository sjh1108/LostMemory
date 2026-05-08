# CL-206 기존 마을씬 현황 파악 메모

- 작성일: 2026-05-07
- Epic: Epic R. 마을/귀환 흐름
- 작업 코드: CL-206
- 작업명: 기존 마을씬 현황 파악
- 산출물: 마을씬 점검 메모

## 확인 범위

이번 점검은 기존 마을씬을 수정하지 않고, Unity serialized 파일과 관련 C# 스크립트를 읽어서 현재 상태를 정리한 것이다.

확인한 주요 파일:

- `Assets/_Project/Scenes/Town/Town.unity`
- `Assets/_Project/Scenes/Town/Town.unity.meta`
- `Assets/_Project/Scripts/Editor/Stage/TownSceneBuilder.cs`
- `Assets/_Project/Scripts/Runtime/SceneFlow/SceneLoadPortalController.cs`
- `Assets/_Project/Scripts/Runtime/Stage/RunManager.cs`
- `ProjectSettings/EditorBuildSettings.asset`
- `ProjectSettings/TagManager.asset`

참고한 프로젝트 규칙:

- `../docs/commonness/agent-unity-safety-rules.md`
- `../docs/commonness/project-structure-and-namespace.md`
- `../docs/commonness/scene-ownership-and-prefab-edit-rules.md`
- `../docs/commonness/bootstrap-scene-and-initialization-flow.md`
- `../docs/commonness/topdown-engine-extension-and-original-protection.md`

## 씬 식별

마을씬은 프로젝트 소유 경로 아래에 있다.

```text
Assets/_Project/Scenes/Town/Town.unity
```

씬 GUID:

```text
fe918667d9819414891445c712f56716
```

이 씬은 `TownSceneBuilder`의 `Create Minimal Town Scene` 메뉴로 생성된 최소 마을씬 구조로 보인다.

## 현재 Hierarchy 단서

현재 `Town.unity`에서 확인된 주요 오브젝트는 다음과 같다.

```text
TownRoot
TownMap_Temp
Floor
Walls
Main Camera
TownSpawnPoint
Player
Town Input Manager
Portal_ToDungeon
PortalVisual
```

## 구성 요소 점검

### 마을 맵

- `TownMap_Temp`가 존재한다.
- `Floor` Tilemap이 존재한다.
- `Walls` Tilemap이 존재한다.
- `Floor`와 `Walls` 모두 `TilemapCollider2D`가 붙어 있다.
- `Floor`는 `Ground` 레이어로 보인다.
- `Walls`는 `Obstacles` 레이어로 보인다.
- 맵은 임시 타일 기반의 작은 직사각형 테스트 공간으로 보인다.

주의할 점:

- `Floor`에도 Collider가 있어 플레이어와 충돌하는지 Play Mode에서 확인이 필요하다.
- 현재 구조는 임시 맵이므로 CL-208 마을 레이아웃 보강 대상이다.

### 플레이어 시작 위치

- `TownSpawnPoint`가 존재한다.
- 위치는 `(0, 0, 0)`이다.
- `Player`도 현재 `(0, 0, 0)`에 배치되어 있다.
- `Player`는 `Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` 인스턴스로 보인다.
- 플레이어 프리팹 쪽에는 `PlayerID: Player1` 값이 있다.

주의할 점:

- 최초 진입 위치는 확인되지만, 귀환 전용 위치는 아직 없다.
- CL-209에서 `TownSpawnPoint` 기준을 확정하고, CL-210에서 별도 귀환 지점을 추가하는 흐름이 맞다.

### 귀환 위치

- `TownReturnSpawnPoint`는 현재 씬에서 확인되지 않는다.
- `ReturnSpawn`, `Portal_ToTown`, `ToTown` 계열 오브젝트도 마을씬 안에서는 확인되지 않는다.

판단:

- 던전/전투 종료 후 마을로 돌아오는 씬 이동 이름은 준비되어 있지만, 마을 안에서 귀환 위치를 분리하는 장치는 아직 없다.
- CL-210에서 `TownReturnSpawnPoint`를 추가하고, CL-214에서 귀환 시 해당 위치를 사용하도록 연결해야 한다.

### 카메라

- `Main Camera`가 존재한다.
- 태그는 `MainCamera`이다.
- 위치는 `(0, 0, -10)`이다.
- Orthographic 카메라이고 Size는 `5.5`이다.
- `AudioListener`가 붙어 있다.
- `Town Input Manager` 설정에서 `followPlayerWithMainCamera: 1`이 확인된다.
- 카메라 팔로우 관련 값은 `cameraOffset (0,0,-10)`, `cameraOrthographicSize 5`, `cameraFollowSharpness 12`, `cameraSmoothTime 0.12`로 설정되어 있다.

주의할 점:

- `Cinemachine`, `Confiner`, `Boundary` 계열 설정은 확인되지 않는다.
- 현재 카메라는 플레이어 추적 위주이고, 맵 밖 시야 제한은 아직 없는 상태로 봐야 한다.
- CL-211에서 마을 크기에 맞춘 카메라 경계 보정이 필요하다.

### 입력

- `Town Input Manager`가 존재한다.
- 컴포넌트는 `LostMemory.TestKhi.TestKhiInputManager`이다.
- `PlayerID`는 `Player1`이다.
- Input Actions 경로는 `Assets/InputSystem_Actions.inputactions`이다.
- Action Map은 `TestKhi`이다.
- Interact 액션명은 `Interact`이다.
- `configureSceneForMovement: 1`과 `followPlayerWithMainCamera: 1`이 켜져 있다.

판단:

- 현재 마을씬은 테스트 KHI 입력 체계를 기준으로 플레이어 이동과 상호작용을 처리하는 구조다.
- MVP1에서는 우선 이 구조를 유지하고, 추후 정식 입력/캐릭터 구조가 확정되면 교체하는 편이 안전하다.

### 던전 이동 포탈

- `Portal_ToDungeon`이 존재한다.
- 위치는 `(3, 0, 0)`이다.
- `CircleCollider2D`가 붙어 있고 `isTrigger: 1`이다.
- 반지름은 `0.65`이다.
- `SceneLoadPortalController`가 붙어 있다.
- `targetSceneName`은 `Dungeon`이다.
- `requireInteractInput`은 `true`이다.
- fallback 키는 `KeyCode.E`이다.
- 허용 플레이어 ID는 `Player1`이다.
- 시각 오브젝트로 `PortalVisual`이 존재한다.

판단:

- 마을에서 던전으로 이동하는 최소 포탈은 이미 있다.
- CL-213에서는 위치, 상호작용 범위, 표시 방식, 대상 씬명을 확정하는 작업이 필요하다.

### UI

- `Canvas`, `HUD`, `PlayerHUD`, `BossHealthBar`, `RunResultPanel`, `ShopPanel`, `TalentPanel` 등 UI 오브젝트는 마을씬에서 확인되지 않는다.
- 마을 전용 UI 표시 규칙도 현재 씬 자체에는 보이지 않는다.

판단:

- 현재 마을씬은 UI가 거의 없는 테스트 진입 씬으로 보는 것이 맞다.
- CL-215에서 마을 진입 시 전투 UI가 남지 않는지 Play Mode 기준 확인이 필요하다.

### NPC와 상호작용 자리

- `NPC`, `Shop`, `Talent` 이름의 마을 오브젝트는 확인되지 않는다.
- 상호작용 스크립트 이름은 입력 매니저와 포탈 쪽에서만 확인된다.

판단:

- NPC 기능이나 상호작용 자리는 아직 없다.
- CL-216에서 상점, 회복, 재능, 퀘스트 같은 기능별 Placeholder 위치만 먼저 확보하면 된다.

### 사운드

- `Main Camera`에 `AudioListener`는 있다.
- 별도 `AudioSource`, `BGM`, 환경음 오브젝트는 확인되지 않는다.

판단:

- 마을 사운드는 아직 연결되지 않은 상태다.
- CL-217에서 임시 BGM 또는 환경음 연결 여부를 정하면 된다.

## 씬 이동 및 귀환 흐름 단서

`RunManager`에는 `townSceneName` 값이 있고, Dungeon 씬 기준 값은 `Town`으로 설정되어 있다.

```text
townSceneName: Town
```

`RunManager.ReturnToTown()`은 다음 조건을 확인한 뒤 마을씬을 로드한다.

```text
Application.CanStreamedLevelBeLoaded(townSceneName)
SceneManager.LoadScene(townSceneName, LoadSceneMode.Single)
```

네트워크 세션이 살아 있으면 `NetworkManager.SceneManager.LoadScene`을 사용한다.

판단:

- 전투/던전 종료 후 마을씬 이름으로 돌아가는 코드 흐름은 있다.
- 다만 귀환 후 플레이어를 `TownReturnSpawnPoint`에 배치하는 별도 처리는 현재 확인되지 않는다.

## Build Settings 점검

`ProjectSettings/EditorBuildSettings.asset`에 다음 경로가 보인다.

```text
Assets/_Project/Scenes/Town/Town.unity
Assets/_Project/Scenes/Dungeon/Dungeon.unity
```

주의할 점:

- 텍스트상 `Town.unity` 항목 앞에 독립적인 `- enabled: 1` 라인이 보이지 않는다.
- `Town.unity` 경로가 `Test_Network_AD.unity` 항목 아래에 이어진 형태로 보여, Build Settings YAML이 정상 엔트리인지 Unity Editor에서 확인해야 한다.
- `RunManager.ReturnToTown()`이 `Application.CanStreamedLevelBeLoaded("Town")`에 의존하므로, Town 씬이 Build Settings에 정상 등록되어 있지 않으면 귀환이 실패한다.

권장 확인:

```text
File > Build Settings
-> Assets/_Project/Scenes/Town/Town.unity가 enabled 상태로 등록되어 있는지 확인
-> Assets/_Project/Scenes/Dungeon/Dungeon.unity가 enabled 상태로 등록되어 있는지 확인
```

## 현재 상태 요약

이미 되어 있는 것:

- 프로젝트 소유 마을씬 파일 존재
- 임시 타일맵 기반 마을 공간 존재
- 기본 플레이어 인스턴스 존재
- 기본 시작 위치 `TownSpawnPoint` 존재
- 메인 카메라 존재
- 테스트 입력 매니저 존재
- 던전 이동 포탈 `Portal_ToDungeon` 존재
- 던전/전투 종료 후 `Town` 씬명으로 돌아가려는 `RunManager` 흐름 존재

아직 필요한 것:

- 마을 보강 범위 확정
- 정식 또는 MVP 기준 레이아웃 보강
- 귀환 전용 위치 `TownReturnSpawnPoint`
- 귀환 시 해당 위치로 플레이어를 배치하는 처리
- 카메라 경계 제한
- 이동 가능 영역과 충돌 검증
- Build Settings 정상 등록 확인
- 마을 UI 노출 규칙
- NPC/상호작용 Placeholder
- 마을 BGM 또는 환경음
- Play Mode 통합 테스트

## CL-207 이후 권장 순서

바로 다음 작업은 CL-207에서 보강 범위를 확정하는 것이다. 현 상태 기준으로는 다음 항목을 MVP1 필수로 잡는 것이 적절하다.

```text
1. Town 씬 Build Settings 정상 등록 확인
2. TownReturnSpawnPoint 추가 여부 확정
3. 귀환 시 Spawn Point 선택 방식 확정
4. Portal_ToDungeon 대상 씬명을 Dungeon으로 유지할지 확정
5. 카메라 경계 보정 방식 확정
6. Floor/Walls Collider가 플레이어 이동에 문제 없는지 Play Mode 확인
7. UI와 NPC Placeholder는 최소 범위만 지정
```

## 작업 주의사항

- 이번 CL-206에서는 Unity 씬, 프리팹, 에셋, 메타 파일을 수정하지 않았다.
- 이후 `.unity`, `.prefab`, `.asset`, `.meta` 파일을 변경해야 하면 Unity Editor에서 작업하는 것을 우선한다.
- 외부 에셋 원본인 `Assets/TopDownEngine`, `Assets/CodeRespawn`, `Assets/MMData`는 직접 수정하지 않는다.
- 실제 게임 씬인 `Assets/_Project/Scenes/Town/Town.unity`를 CLI로 직접 수정해야 할 경우, 변경 범위와 위험을 먼저 공유하고 승인 후 진행한다.

# 공용 부트스트랩 씬 및 초기화 흐름

> 대상 프로젝트: `client/LostMemory`  
> 목적: 게임 실행 시 공통 시스템을 한 번만 초기화하고, 로비와 스테이지 씬으로 안정적으로 진입하도록 한다.

## 1. 한 줄 정의

Bootstrap 씬은 게임 실행 시 가장 먼저 로드되어 전역 매니저를 초기화하고 다음 씬으로 넘기는 공통 진입점이다.

```text
Game Start
  -> Bootstrap
  -> Lobby
  -> Stage
```

## 2. 왜 필요한가

- 매니저 중복 생성을 막는다.
- 씬마다 초기화 순서가 달라지는 문제를 줄인다.
- 저장, 설정, 오디오, 런 상태 같은 전역 데이터를 한 곳에서 관리한다.
- 로비, 스테이지, 테스트 씬이 같은 초기화 흐름을 공유할 수 있다.
- TopDown Engine의 씬 단위 매니저와 우리 게임 전역 매니저의 책임을 분리한다.

## 3. 씬 책임 분리

| 위치 | 책임 |
|---|---|
| Bootstrap 씬 | 전역 매니저 생성, 저장 데이터 로드, 설정 초기화, 첫 씬 결정 |
| Lobby 씬 | 캐릭터 선택, 무기 선택, 재능 투자, 시작 UI |
| Stage 씬 | TopDown Engine 레벨 구성, 플레이어, 적, 방 진행, 전투 |
| Test 씬 | 특정 기능 검증, 개인 실험 |

Bootstrap 씬은 게임 전체 공통 상태를 준비한다. 실제 플레이 구성은 Lobby 또는 Stage 씬에서 담당한다.

## 4. 초기화 순서

권장 초기화 흐름은 다음과 같다.

```text
Bootstrap 씬 로드
  -> AppRoot.Awake
  -> 중복 AppRoot 검사
  -> DontDestroyOnLoad 등록
  -> 서비스와 매니저 생성
  -> SaveManager 초기화
  -> SettingsManager 초기화
  -> AudioManager 초기화
  -> RunManager 초기화
  -> TalentManager / MemoryManager 초기화
  -> SceneFlowManager가 다음 씬 로드
  -> Lobby 씬 진입
```

스테이지 진입 시에는 다음 흐름을 사용한다.

```text
Lobby
  -> SceneFlowManager.StartRun
  -> RunManager가 런 상태 생성
  -> Stage 씬 로드
  -> Stage 초기화
  -> TopDown Engine 컴포넌트 탐색
  -> TopDownPlayerStatApplier가 런 상태를 플레이어에 적용
```

## 5. Bootstrap에 둘 것

Bootstrap 씬에는 게임 전체에서 하나만 존재해야 하는 오브젝트를 둔다.

```text
AppRoot
SceneFlowManager
SaveManager
SettingsManager
AudioManager
RunManager
TalentManager
MemoryManager
DataRegistry
```

권장 계층 예시:

```text
Bootstrap
  AppRoot
    SceneFlowManager
    SaveManager
    SettingsManager
    AudioManager
    RunManager
    TalentManager
    MemoryManager
    DataRegistry
```

## 6. Bootstrap에 두지 않을 것

Bootstrap 씬에는 특정 씬이나 특정 레벨에만 필요한 오브젝트를 두지 않는다.

```text
Player
Enemy
EnemySpawner
RoomCombatController
StageRouteManager
TopDown Engine LevelManager
TopDown Engine GUIManager
Stage 전용 Camera
Stage 전용 Canvas
```

TopDown Engine의 `LevelManager`, `GUIManager`, 카메라, 플레이어는 보통 현재 레벨 씬에 둔다. Bootstrap은 이들을 직접 소유하지 않고, 씬 로드 후 필요한 전역 상태만 전달한다.

## 7. 권장 파일 위치

```text
LostMemory/Assets/_Project/Scenes/Bootstrap/Bootstrap.unity
LostMemory/Assets/_Project/Scenes/Lobby/Lobby.unity
LostMemory/Assets/_Project/Scenes/Stages/Stage01.unity
LostMemory/Assets/_Project/Scenes/Test/
```

```text
LostMemory/Assets/_Project/Scripts/Runtime/Core/AppRoot.cs
LostMemory/Assets/_Project/Scripts/Runtime/SceneFlow/SceneFlowManager.cs
LostMemory/Assets/_Project/Scripts/Runtime/Save/SaveManager.cs
LostMemory/Assets/_Project/Scripts/Runtime/Core/SettingsManager.cs
LostMemory/Assets/_Project/Scripts/Runtime/Core/AudioManager.cs
LostMemory/Assets/_Project/Scripts/Runtime/Core/DataRegistry.cs
LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs
LostMemory/Assets/_Project/Scripts/Runtime/Talents/TalentManager.cs
LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemoryManager.cs
LostMemory/Assets/_Project/Scripts/Runtime/Integrations/TopDownEngine/TopDownPlayerStatApplier.cs
```

## 8. 네임스페이스

```csharp
namespace LostMemory.Core
namespace LostMemory.SceneFlow
namespace LostMemory.Save
namespace LostMemory.Stage
namespace LostMemory.Talents
namespace LostMemory.Memory
namespace LostMemory.Integrations.TopDownEngine
```

## 9. 작업 절차

### 9.1 Unity Editor 작업

1. `Assets/_Project/Scenes/Bootstrap/Bootstrap.unity` 씬을 만든다.
2. 씬에 빈 GameObject `AppRoot`를 만든다.
3. `AppRoot`에 `AppRoot.cs`를 붙인다.
4. 필요한 전역 매니저를 `AppRoot` 하위 오브젝트로 만들거나 코드에서 생성한다.
5. `Build Settings`에서 `Bootstrap` 씬을 0번에 등록한다.
6. `Lobby` 씬을 1번에 등록한다.
7. 스테이지 씬들을 그 뒤에 등록한다.

권장 Build Settings 순서:

```text
0. Bootstrap
1. Lobby
2. Stage01
3. Test_CombatRoom
```

### 9.2 C# 작업

1. `AppRoot`에서 중복 인스턴스를 검사한다.
2. 최초 인스턴스만 `DontDestroyOnLoad`로 유지한다.
3. 전역 매니저를 초기화한다.
4. `SceneFlowManager`를 통해 첫 씬을 로드한다.
5. 씬 이동은 가급적 `SceneFlowManager`를 통해서만 수행한다.

예시 구조:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Core
{
    public sealed class AppRoot : MonoBehaviour
    {
        private static AppRoot _instance;

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeServices();
            LoadInitialScene();
        }

        private void InitializeServices()
        {
            // SaveManager, SettingsManager, RunManager 등을 초기화한다.
        }

        private void LoadInitialScene()
        {
            SceneManager.LoadScene("Lobby");
        }
    }
}
```

## 10. 씬 이동 규칙

씬 이동은 가능한 한 한 곳에서 관리한다.

권장 API 예시:

```text
SceneFlowManager.LoadLobby()
SceneFlowManager.StartRun()
SceneFlowManager.LoadStage(stageId)
SceneFlowManager.ReturnToLobby()
SceneFlowManager.LoadTestScene(sceneName)
```

피해야 할 방식:

```text
아무 스크립트에서나 SceneManager.LoadScene 직접 호출
각 씬이 독자적으로 SaveManager나 RunManager 생성
Stage 씬에서 Bootstrap 전역 상태를 새로 초기화
```

## 11. TopDown Engine 연동 흐름

TopDown Engine은 현재 레벨 씬의 실행 기반으로 사용하고, Bootstrap은 게임 전체 상태만 관리한다.

```text
Bootstrap
  -> RunManager가 현재 런 상태 보유

Stage
  -> TopDown Engine GameManager / LevelManager 실행
  -> Player 생성 또는 배치
  -> TopDownPlayerStatApplier가 RunManager 상태를 TDE 컴포넌트에 적용
```

적용 예시:

```text
RunState
  -> 체력 보정
  -> 공격력 보정
  -> 이동속도 보정
  -> 대시 쿨타임 보정

TopDownPlayerStatApplier
  -> Health
  -> Weapon
  -> CharacterMovement
  -> CharacterDash2D
```

TopDown Engine 원본 코드를 수정하지 않고, 공개 필드나 메서드를 통해 값을 적용한다.

## 12. CLI/AI 작업 규칙

CLI, Codex, AI 에이전트는 다음 규칙을 따른다.

- `.unity`, `.prefab`, `.asset`, `.meta` 파일은 사용자가 명시하지 않으면 직접 수정하지 않는다.
- Bootstrap 씬 생성과 GameObject 연결은 Unity Editor에서 수행할 단계로 안내한다.
- CLI는 기본적으로 C# 스크립트와 문서만 수정한다.
- 씬이나 프리팹 연결이 필요한 경우 작업 절차를 문서화한다.
- TopDown Engine 원본 경로는 직접 수정하지 않는다.
- 테스트용 씬이 필요하면 `Assets/_Project/Scenes/Test/` 아래 생성을 제안한다.

## 13. 체크리스트

Bootstrap 흐름을 만들기 전에 확인한다.

- `Bootstrap` 씬이 Build Settings 0번인가?
- `AppRoot`가 중복 생성 방지를 하는가?
- `AppRoot`가 `DontDestroyOnLoad`로 유지되는가?
- 저장, 설정, 오디오, 런 상태 초기화 순서가 정해져 있는가?
- 씬 이동이 `SceneFlowManager`를 통해 이뤄지는가?
- Stage 씬 전용 오브젝트가 Bootstrap에 들어가 있지 않은가?
- TopDown Engine 원본을 직접 수정하지 않았는가?
- 테스트 씬과 실제 게임 씬이 분리되어 있는가?

## 14. 관련 문서

- `docs/commonness/agent-unity-safety-rules.md`
- `docs/commonness/project-structure-and-namespace.md`
- `docs/commonness/scene-ownership-and-prefab-edit-rules.md`
- `docs/commonness/topdown-engine-extension-and-original-protection.md`
- `docs/khi/topdown-engine-adoption-plan.md`

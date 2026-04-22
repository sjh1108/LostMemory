# TestKhi 프로토타입 상태

대상 씬: `LostMemory/Assets/Scenes/test_khi.unity`

## 목적

`test_khi.unity`는 TopDown Engine 기반으로 다음 흐름을 검증하기 위한 테스트 씬이다.

```text
입력 액션 맵
  -> TDE InputManager 연결
  -> TDE Character 이동
  -> E 상호작용 입력
  -> 상호작용 대상 액션 호출
```

이 씬은 최종 게임 씬이 아니라, KHI 작업자가 입력/캐릭터/상호작용 구조를 검증하기 위한 독립 테스트 씬으로 본다.

## 현재 구성

### 입력

입력 에셋:

```text
LostMemory/Assets/InputSystem_Actions.inputactions
```

추가된 액션 맵:

```text
TestKhi
  Move
  Interact
```

현재 바인딩:

| 액션 | 키보드 | 게임패드 |
|---|---|---|
| `Move` | WASD, 방향키 | leftStick |
| `Interact` | E | buttonSouth |

### 런타임 스크립트

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiInputManager.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiSceneBootstrap.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiInteractionZone.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiDoorAction.cs
```

역할:

| 스크립트 | 역할 |
|---|---|
| `TestKhiInputManager` | `TestKhi` 액션 맵을 TDE `InputManager` 입력으로 변환 |
| `TestKhiSceneBootstrap` | 테스트 씬에 필요한 기본 오브젝트가 없으면 생성 |
| `TestKhiInteractionZone` | TDE `ButtonActivatedZone` 기반 상호작용 감지 |
| `TestKhiDoorAction` | 상호작용 액션 예시. 문 Collider on/off 테스트 |

### 캐릭터

테스트용 프리팹:

```text
LostMemory/Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab
```

원본 기준:

```text
LostMemory/Assets/TopDownEngine/Demos/Minimal2D/Prefabs/PlayableCharacters/MinimalCharacter2D.prefab
```

원칙:

- TopDown Engine 원본 프리팹은 수정하지 않는다.
- `_Project/Prefabs/Characters` 아래 복제본을 테스트용으로 사용한다.
- 나중에 실제 캐릭터 프리팹이 준비되면 교체한다.

## 완료된 것

- `TestKhi` 액션 맵 생성
- `Move` 입력 연결
- `Interact` 입력 연결
- TDE 캐릭터 이동 확인
- 카메라가 캐릭터를 따라오도록 구성
- `E` 입력이 TDE `CharacterButtonActivation`까지 전달되는 것 확인
- 테스트 상호작용 오브젝트 생성
- 상호작용 시 액션 컴포넌트 호출 확인

확인된 로그 예시:

```text
[TestKhiInput] Move source=ActionMap
[TestKhiInteract] Activated 'TestKhi Interaction Test'
[TestKhiDoor] TestKhi Door is now open
```

## 보류된 것

### 문 통과 테스트

현재 `E` 입력으로 `TestKhiDoorAction`이 호출되는 것은 확인됐다. 다만 테스트 문 Collider와 Tilemap 벽 배치가 꼬이면서 실제 통과 검증은 보류했다.

보류 이유:

- 씬에 자동 생성/정리 로직이 겹치면 Tilemap 또는 상호작용 오브젝트가 중복/삭제될 수 있다.
- 문 위치와 Tilemap 벽 Collider가 정확히 분리되어야 통과 여부를 안정적으로 검증할 수 있다.
- 지금 단계의 핵심 목표는 `E` 입력 연결 확인이며, 문 통과는 씬 배치 확정 후 처리해도 된다.

현재 판단:

```text
E 입력 연결: 완료
상호작용 액션 호출: 완료
문 Collider/Tilemap 통과: 보류
```

## 주의 사항

- 런타임에서 기존 씬 오브젝트를 자동 삭제하는 코드는 피한다.
- `TestKhiSceneBootstrap`은 "없으면 생성" 중심으로 둔다.
- 중복 오브젝트가 생기면 코드로 강제 삭제하기보다 Unity Hierarchy에서 상태를 먼저 확인한다.
- `Entered`/`Exited` 로그가 중복으로 찍힐 수 있다. 실제 액션 호출이 한 번씩만 되는지 별도로 확인한다.
- UI 작업은 별도 담당자가 있으므로, 현재 단계에서는 월드 오브젝트 액션 중심으로 검증한다.

## 다음 작업 후보

1. 테스트 문/장애물 통과 검증은 나중에 씬 배치 정리 후 재시도
2. 테스트 캐릭터 교체 조건 정리
3. 실제 캐릭터 프리팹 준비 시 `TestKhi_MinimalCharacter2D`와 교체
4. 상호작용 액션을 문이 아닌 조사/획득/장치 작동 같은 실제 기획 액션으로 분리


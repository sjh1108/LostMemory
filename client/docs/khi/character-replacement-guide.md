# TestKhi 캐릭터 교체 가이드

## 목적

현재 `test_khi.unity`는 TopDown Engine의 `MinimalCharacter2D` 복제본을 테스트 캐릭터로 사용한다.

이 문서는 나중에 프로젝트 실제 캐릭터 프리팹으로 교체할 때 필요한 조건과 절차를 정리한다.

현재 테스트 캐릭터:

```text
LostMemory/Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab
```

## 교체의 의미

"캐릭터 교체"는 지금 당장 최종 캐릭터로 바꾸는 작업이 아니다.

의미는 다음과 같다.

```text
현재:
TestKhi_MinimalCharacter2D
  -> 이동
  -> 카메라 추적
  -> E 상호작용

나중:
ProjectPlayerCharacter
  -> 같은 입력 구조 유지
  -> 같은 상호작용 구조 유지
```

즉 입력/상호작용 구조는 유지하고, 캐릭터 프리팹만 바꿀 수 있게 준비하는 것이 목표다.

## 새 캐릭터 프리팹 필수 조건

새 캐릭터 프리팹은 최소한 아래 조건을 만족해야 한다.

| 항목 | 필요 값/상태 |
|---|---|
| TDE `Character` | 붙어 있어야 함 |
| `CharacterType` | `Player` |
| `PlayerID` | `Player1` |
| `TopDownController2D` | 2D 이동용 컨트롤러 |
| `CharacterMovement` | 이동 능력 |
| `CharacterButtonActivation` | E 상호작용 능력 |
| `Rigidbody2D` | TDE 이동/충돌에 맞게 설정 |
| Collider2D | 캐릭터 충돌 범위 |
| Layer | `Player` 권장 |
| Tag | `Player` 권장 |

## 입력 연결 조건

`TestKhiInputManager`는 `PlayerID = Player1`인 TDE 플레이어 캐릭터를 찾아 입력을 연결한다.

따라서 새 캐릭터가 움직이지 않으면 먼저 아래를 확인한다.

```text
Character.PlayerID == Player1
Character.CharacterType == Player
CharacterMovement.InputAuthorized == true
CharacterMovement.enabled == true
```

## 상호작용 연결 조건

`E` 상호작용은 TDE의 `CharacterButtonActivation` 능력을 통해 동작한다.

새 캐릭터에서 상호작용이 안 되면 아래를 확인한다.

```text
CharacterButtonActivation 컴포넌트가 있는가?
AbilityAuthorized가 true인가?
캐릭터 Collider가 상호작용 Trigger에 들어가는가?
상호작용 대상 TargetLayerMask가 Player 레이어를 포함하는가?
```

## 교체 절차

1. 실제 캐릭터 프리팹을 `_Project/Prefabs/Characters` 아래에 둔다.
2. 프리팹에 필수 TDE 컴포넌트를 붙인다.
3. `PlayerID`를 `Player1`로 맞춘다.
4. `Layer`와 `Tag`를 `Player`로 맞춘다.
5. `test_khi.unity`에서 기존 `TestKhi_MinimalCharacter2D`를 비활성화하거나 제거한다.
6. 새 캐릭터 프리팹 인스턴스를 같은 시작 위치에 배치한다.
7. Play 후 이동, 카메라 추적, E 상호작용을 확인한다.

## 교체 전 체크리스트

- 새 캐릭터가 단독 씬에서 이동 가능한가?
- TDE `Character`가 정상 초기화되는가?
- `PlayerID`가 `Player1`인가?
- `CharacterMovement`가 입력을 받을 수 있는 상태인가?
- `CharacterButtonActivation`이 붙어 있는가?
- 충돌 Collider가 너무 크거나 작지 않은가?
- Sprite/Animator만 바꾼 것인지, TDE 루트 구조까지 바꾼 것인지 확인했는가?

## 권장 방식

처음에는 전체 프리팹을 새로 만들기보다, 현재 테스트 프리팹을 복제해서 외형만 바꾸는 방식이 안전하다.

권장 흐름:

```text
TestKhi_MinimalCharacter2D 복제
  -> Sprite/Animator 교체
  -> 이동/상호작용 유지 확인
  -> 필요한 능력만 추가
  -> 최종 캐릭터 프리팹으로 승격
```

이 방식은 TDE 컴포넌트 연결이 깨질 가능성이 낮다.

## 피해야 할 것

- TopDown Engine 원본 `MinimalCharacter2D.prefab` 직접 수정
- TDE 컴포넌트를 한 번에 많이 제거
- `PlayerID`를 임의 값으로 바꾸고 입력 매니저를 같이 수정하지 않는 것
- 캐릭터 루트와 모델 child 구조를 동시에 크게 바꾸는 것
- 씬 오브젝트 자동 삭제 로직으로 캐릭터를 교체하려는 것

## 관련 파일

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiInputManager.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiSceneBootstrap.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiInteractionZone.cs
LostMemory/Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab
LostMemory/Assets/InputSystem_Actions.inputactions
LostMemory/Assets/Scenes/test_khi.unity
```


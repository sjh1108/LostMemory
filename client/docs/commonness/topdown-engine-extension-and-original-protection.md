# TopDown Engine 확장 원칙 및 원본 수정 금지 규칙

> 대상 프로젝트: `client/LostMemory`  
> 목적: TopDown Engine을 활용하면서도 외부 에셋 원본을 보호하고, 업데이트와 팀 협업 시 충돌을 줄인다.

## 1. 핵심 결론

TopDown Engine은 우리 게임의 기반 기능으로 활용하되, 원본 에셋은 직접 수정하지 않는다.

```text
TopDown Engine 원본
  -> 그대로 보존

우리 프로젝트 코드와 프리팹
  -> Assets/_Project 아래에서 확장
```

이 방식은 다음 두 극단 사이의 절충안이다.

| 방식 | 장점 | 문제 |
|---|---|---|
| TopDown Engine 원본 직접 수정 | 당장은 빠름 | 업데이트 충돌, 수정 추적 어려움 |
| 전부 직접 구현 | 완전한 통제 가능 | 에셋 활용 가치 감소, 구현 비용 증가 |
| 원본 보존 + 프로젝트 확장 | 에셋 활용과 유지보수 균형 | 연결 계층 설계가 필요 |

## 2. 원본 수정 금지 대상

다음 경로는 외부 에셋 원본으로 본다.

```text
LostMemory/Assets/TopDownEngine/
LostMemory/Assets/CodeRespawn/
LostMemory/Assets/MMData/
```

규칙:

- 위 경로의 스크립트, 프리팹, 씬, ScriptableObject는 직접 수정하지 않는다.
- 데모 씬과 데모 프리팹은 참고용으로 사용한다.
- 실제 게임용 파일은 `LostMemory/Assets/_Project/` 아래에 새로 만들거나 복제해서 사용한다.
- 원본 수정이 불가피해 보이면 먼저 대체 방법을 검토하고, 변경 이유를 문서화한다.

## 3. 확장 방식 판단 기준

### 3.1 그대로 사용

TopDown Engine 기능이 우리 요구사항과 거의 맞으면 원본 컴포넌트를 그대로 사용한다.

예시:

- `Health`
- `CharacterMovement`
- `CharacterDash2D`
- `CharacterHandleWeapon`
- `MeleeWeapon`
- `ProjectileWeapon`
- `AIBrain`
- `DamageOnTouch`

이 경우 원본 코드를 복제하지 않는다. 씬이나 프리팹에서 컴포넌트로 연결해 사용한다.

### 3.2 프리팹과 씬은 복제 후 수정

TopDown Engine 데모 프리팹이나 씬을 기반으로 우리 게임용 구성이 필요하면 원본을 복제한다.

예시:

```text
Assets/TopDownEngine/Demos/Koala2D/Prefabs/PlayableCharacters/Koala.prefab
  -> Assets/_Project/Prefabs/Characters/Player_Sephiria.prefab
```

복제 후에는 `_Project` 아래 복제본만 수정한다.

권장 위치:

```text
LostMemory/Assets/_Project/Prefabs/
LostMemory/Assets/_Project/Scenes/Test/
LostMemory/Assets/_Project/Scenes/
```

### 3.3 C# 코드는 상속, 래퍼, 어댑터 우선

TopDown Engine C# 원본을 직접 고치거나 복사해서 수정하는 것은 마지막 수단이다.

우선순위:

1. 공개 필드, 프로퍼티, 메서드를 이용해 외부에서 제어한다.
2. 필요한 경우 `_Project` 아래에 래퍼 또는 어댑터를 만든다.
3. 상속이 적절하면 파생 클래스를 만든다.
4. 그래도 불가능하면 원본 수정 대신 기능 일부를 우리 코드로 별도 구현한다.

권장 구조:

```text
RelicManager
  -> TopDownPlayerStatApplier
      -> Health / Weapon / CharacterDash2D 값 조정
```

피해야 할 구조:

```text
RelicManager
  -> TopDown Engine 원본 Health.cs 수정
  -> TopDown Engine 원본 Weapon.cs 수정
  -> TopDown Engine 원본 CharacterDash2D.cs 수정
```

권장 위치:

```text
LostMemory/Assets/_Project/Scripts/Runtime/Integrations/TopDownEngine/
```

권장 네임스페이스:

```csharp
namespace LostMemory.Integrations.TopDownEngine
```

### 3.4 게임 고유 시스템은 직접 구현

TopDown Engine이 제공하지 않거나, 우리 게임 규칙이 강하게 들어가는 기능은 직접 구현한다.

예시:

- 유물 3택 보상
- 유물 중복 제외
- 유물 세트 태그 효과
- 재능 포인트 투자
- 기억, 기억의 조각, 기억의 파편
- 무기 강화 해금
- 상점 상품 선정과 가격 규칙
- 스테이지 방 배치 규칙

이 기능들은 TopDown Engine 원본을 수정해서 넣지 않고, `_Project` 아래의 우리 시스템으로 만든다.

## 4. 실무 판단 예시

| 상황 | 권장 방식 |
|---|---|
| 플레이어 이동이 필요함 | `CharacterMovement`를 그대로 사용 |
| 대시 쿨타임을 유물 효과로 줄여야 함 | `TopDownPlayerStatApplier`에서 `CharacterDash2D` 값 조정 |
| Koala 플레이어 프리팹을 참고하고 싶음 | `_Project/Prefabs` 아래로 복제 후 수정 |
| TDE 무기 데미지 계산식을 일부 바꾸고 싶음 | 먼저 외부에서 수치 조정 가능 여부 확인 |
| TDE 코드 중 한 줄만 바꾸면 쉬워 보임 | 원본 수정하지 말고 래퍼, 상속, 별도 구현 검토 |
| 유물 보상 UI가 필요함 | `_Project/Scripts/Runtime/Relics`, `_Project/Scripts/Runtime/UI`에 직접 구현 |
| 데모 씬에서 방 구조를 가져오고 싶음 | 테스트 씬으로 복제해서 검증 |

## 5. 원본 수정이 필요한 것처럼 보일 때

다음 순서로 확인한다.

1. 원본 컴포넌트의 인스펙터 설정으로 해결 가능한가?
2. 공개 API로 런타임 제어가 가능한가?
3. `_Project` 아래 어댑터로 연결할 수 있는가?
4. 상속해서 필요한 동작만 바꿀 수 있는가?
5. 우리 시스템으로 별도 구현하는 편이 더 안전한가?
6. 정말 원본 수정이 필요한가?

6번까지 도달한 경우에는 바로 수정하지 않고 팀에 공유한다.

공유할 내용:

- 어떤 원본 파일을 수정하려는지
- 왜 다른 방식으로 해결하기 어려운지
- 업데이트 시 충돌 가능성이 있는지
- 대체 구현 비용이 어느 정도인지

## 6. 작업 전 체크리스트

- 수정 대상이 `Assets/TopDownEngine` 아래인가?
- 수정 대상이 외부 에셋 데모 씬이나 데모 프리팹인가?
- `_Project` 아래 복제본으로 처리할 수 있는가?
- C# 원본 수정 대신 래퍼, 어댑터, 상속으로 해결할 수 있는가?
- 이 기능이 TopDown Engine 기반 기능인가, 우리 게임 고유 규칙인가?
- 나중에 TopDown Engine 업데이트가 들어와도 유지 가능한 구조인가?

## 7. 관련 문서

- `docs/khi/topdown-engine-adoption-plan.md`
- `docs/khi/topdown-engine-feature-inventory.md`
- `docs/commonness/project-structure-and-namespace.md`
- `docs/commonness/agent-unity-safety-rules.md`
- `docs/commonness/scene-ownership-and-prefab-edit-rules.md`

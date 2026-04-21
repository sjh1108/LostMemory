# Unity 프로젝트 폴더 구조 및 네임스페이스 가이드

> 대상 프로젝트: `client/LostMemory`  
> 목적: 팀원이 새 파일을 만들 때 위치와 이름을 일관되게 판단할 수 있도록 한다.

## 1. 기본 원칙

- 우리 게임 코드와 외부 에셋 코드를 분리한다.
- 외부 에셋 폴더는 가능하면 직접 수정하지 않는다.
- 테스트용 파일과 실제 게임 파일을 분리한다.
- 스크립트 네임스페이스는 폴더 구조와 최대한 맞춘다.
- 런타임 코드, 에디터 코드, 테스트 코드는 물리적으로 분리한다.
- 새 기능은 먼저 적절한 시스템 폴더를 정하고 생성한다.

## 2. 현재 Assets 상태

현재 `LostMemory/Assets`에는 다음 성격의 폴더가 존재한다.

| 경로 | 성격 | 처리 방침 |
|---|---|---|
| `Assets/TopDownEngine` | More Mountains TopDown Engine 외부 에셋 | 직접 수정 최소화 |
| `Assets/CodeRespawn` | Dungeon Architect 계열 외부 에셋/샘플 | 직접 수정 최소화 |
| `Assets/MMData` | More Mountains 런타임/세이브 데이터 | 필요 여부 확인 후 관리 |
| `Assets/Scenes` | 현재 씬 폴더 | 실제 씬/테스트 씬 분리 필요 |
| `Assets/Settings` | URP/Unity 설정 | 설정 파일 유지 |
| `Assets/Prepabs` | 프리팹 폴더로 보이나 오타 가능성 있음 | `Prefabs`로 정리 검토 |
| `Assets/Main Camera.prefab` | 루트에 있는 프리팹 | 추후 `Project/Prefabs` 하위로 이동 권장 |

## 3. 권장 폴더 구조

우리 프로젝트 전용 파일은 `Assets/_Project` 아래에 모은다.

```text
Assets/
  _Project/
    Scripts/
      Runtime/
        Core/
        Characters/
        Combat/
        Weapons/
        Enemies/
        Stage/
        Rooms/
        Relics/
        Talents/
        Memory/
        Items/
        Shop/
        UI/
        Data/
        Save/
        Integrations/
          TopDownEngine/
      Editor/
      Tests/

    Prefabs/
      Characters/
      Enemies/
      Weapons/
      Rooms/
      Items/
      UI/
      Managers/

    Scenes/
      Lobby/
      Stages/
      Test/

    ScriptableObjects/
      Characters/
      Enemies/
      Weapons/
      Relics/
      Talents/
      Memory/
      Stages/
      Items/
      Shop/

    Art/
      Sprites/
      Materials/
      Animations/
      VFX/

    Audio/
      BGM/
      SFX/

  TopDownEngine/
  CodeRespawn/
  Settings/
```

## 4. 폴더별 역할

| 폴더 | 역할 |
|---|---|
| `_Project/Scripts/Runtime/Core` | 게임 공통 기반, 이벤트, 공통 유틸 |
| `_Project/Scripts/Runtime/Characters` | 플레이어/캐릭터 관련 게임 로직 |
| `_Project/Scripts/Runtime/Combat` | 피해 계산, 전투 판정, 스탯 적용 |
| `_Project/Scripts/Runtime/Weapons` | 우리 게임의 무기 선택, 강화, 무기 데이터 |
| `_Project/Scripts/Runtime/Enemies` | 적 종류, 적 스폰, 적 전용 로직 |
| `_Project/Scripts/Runtime/Stage` | 스테이지 진행, 큰방/상점/보스방 흐름 |
| `_Project/Scripts/Runtime/Rooms` | 작은방 진입, 잠금, 클리어, 탈출구 |
| `_Project/Scripts/Runtime/Relics` | 유물, 세트 태그, 유물 효과 적용 |
| `_Project/Scripts/Runtime/Talents` | 재능 포인트, 재능 효과 |
| `_Project/Scripts/Runtime/Memory` | 기억, 기억의 조각, 기억의 파편 |
| `_Project/Scripts/Runtime/Items` | 포션, 코인, 일반 아이템 |
| `_Project/Scripts/Runtime/Shop` | 상점 상품, 가격, 구매 |
| `_Project/Scripts/Runtime/UI` | UI 화면과 UI 프레젠터 |
| `_Project/Scripts/Runtime/Data` | ScriptableObject 데이터 모델 |
| `_Project/Scripts/Runtime/Save` | 저장/로드 |
| `_Project/Scripts/Runtime/Integrations/TopDownEngine` | TopDownEngine과 우리 코드 사이의 연결 계층 |
| `_Project/Scripts/Editor` | Unity Editor 전용 코드 |
| `_Project/Scripts/Tests` | 테스트 코드 |

## 5. 네임스페이스 규칙

기본 루트 네임스페이스는 `LostMemory`를 사용한다.

```csharp
namespace LostMemory.Core
namespace LostMemory.Characters
namespace LostMemory.Combat
namespace LostMemory.Weapons
namespace LostMemory.Enemies
namespace LostMemory.Stage
namespace LostMemory.Rooms
namespace LostMemory.Relics
namespace LostMemory.Talents
namespace LostMemory.Memory
namespace LostMemory.Items
namespace LostMemory.Shop
namespace LostMemory.UI
namespace LostMemory.Data
namespace LostMemory.Save
```

TopDownEngine에 직접 의존하는 코드는 별도 네임스페이스를 사용한다.

```csharp
namespace LostMemory.Integrations.TopDownEngine
```

에디터 전용 코드는 다음 형태를 사용한다.

```csharp
namespace LostMemory.Editor
```

테스트 코드는 다음 형태를 사용한다.

```csharp
namespace LostMemory.Tests
```

## 6. TopDownEngine 연동 원칙

TopDownEngine 컴포넌트를 게임 규칙 코드에서 직접 많이 만지지 않는다.

권장 방식:

```text
RelicManager
  -> PlayerStatApplier
      -> TopDownEngine의 Health / Weapon / CharacterDash2D 값 변경
```

피해야 할 방식:

```text
RelicManager
  -> Health, Weapon, CharacterDash2D를 여기저기 직접 수정
```

TopDownEngine 연동 코드는 다음 위치에 둔다.

```text
Assets/_Project/Scripts/Runtime/Integrations/TopDownEngine/
```

예시 클래스:

```csharp
namespace LostMemory.Integrations.TopDownEngine
{
    public sealed class TopDownPlayerStatApplier
    {
    }
}
```

## 7. 파일 이름 규칙

### 7.1 스크립트

| 종류 | 예시 |
|---|---|
| 매니저 | `RelicManager.cs`, `StageRouteManager.cs` |
| 컨트롤러 | `RoomCombatController.cs`, `ShopController.cs` |
| 데이터 | `RelicData.cs`, `WeaponUpgradeData.cs` |
| 런타임 상태 | `RunState.cs`, `RelicInventory.cs` |
| UI 화면 | `RelicRewardView.cs`, `TalentSelectionView.cs` |
| UI 연결 | `RelicRewardPresenter.cs`, `ShopPresenter.cs` |
| TopDownEngine 연결 | `TopDownPlayerStatApplier.cs` |

### 7.2 ScriptableObject

파일명은 데이터 타입과 실제 이름을 함께 쓴다.

```text
RelicData_WarriorsString.asset
RelicData_RedFang.asset
WeaponData_Sword.asset
WeaponData_Bow.asset
TalentData_AttackSpeed.asset
StageData_Stage01.asset
```

### 7.3 프리팹

```text
Player_Sephiria.prefab
Enemy_Ninja.prefab
Weapon_Sword.prefab
Weapon_Bow.prefab
Room_Combat_Small_01.prefab
UI_RelicReward.prefab
Manager_Run.prefab
```

### 7.4 씬

```text
Lobby.unity
Stage01.unity
Stage_Test_CombatRoom.unity
Test_RelicReward.unity
Test_WeaponPrototype.unity
```

테스트 씬은 반드시 `Scenes/Test` 아래에 둔다.

## 8. asmdef 권장 구조

프로젝트가 커지면 asmdef를 사용해 컴파일 경계를 나눈다.

```text
LostMemory.Runtime.asmdef
LostMemory.Editor.asmdef
LostMemory.Tests.asmdef
```

초기에는 `LostMemory.Runtime.asmdef` 하나부터 시작해도 된다.

권장 위치:

```text
Assets/_Project/Scripts/Runtime/LostMemory.Runtime.asmdef
Assets/_Project/Scripts/Editor/LostMemory.Editor.asmdef
Assets/_Project/Scripts/Tests/LostMemory.Tests.asmdef
```

## 9. 기능 추가 예시

### 9.1 유물 하나 추가

```text
Scripts/Runtime/Relics/
  RelicManager.cs
  RelicEffect.cs
  RelicInventory.cs

Scripts/Runtime/Data/
  RelicData.cs

ScriptableObjects/Relics/
  RelicData_WarriorsString.asset

Prefabs/UI/
  UI_RelicReward.prefab
```

네임스페이스:

```csharp
namespace LostMemory.Relics
namespace LostMemory.Data
namespace LostMemory.UI
```

### 9.2 작은방 전투 루프 추가

```text
Scripts/Runtime/Rooms/
  RoomCombatController.cs
  RoomExitController.cs

Scripts/Runtime/Stage/
  StageRouteManager.cs

Prefabs/Rooms/
  Room_Combat_Small_01.prefab

Scenes/Test/
  Test_CombatRoom.unity
```

네임스페이스:

```csharp
namespace LostMemory.Rooms
namespace LostMemory.Stage
```

### 9.3 TopDownEngine 스탯 연결 추가

```text
Scripts/Runtime/Integrations/TopDownEngine/
  TopDownPlayerStatApplier.cs
  TopDownWeaponStatApplier.cs
```

네임스페이스:

```csharp
namespace LostMemory.Integrations.TopDownEngine
```

## 10. 외부 에셋 수정 규칙

외부 에셋 폴더 예시:

```text
Assets/TopDownEngine
Assets/CodeRespawn
```

원칙:

- 외부 에셋 원본 스크립트는 직접 수정하지 않는다.
- 필요한 경우 상속, 래퍼, 어댑터를 만든다.
- 수정이 불가피하면 변경 이유를 문서화한다.
- 외부 에셋 데모 씬은 참고용으로만 사용한다.
- 우리 게임용 프리팹은 `_Project/Prefabs` 아래에 새로 만든다.

## 11. 정리 필요 항목

현재 구조에서 추후 정리가 필요한 항목이다.

| 항목 | 권장 조치 |
|---|---|
| `Assets/Prepabs` | 오타라면 `Assets/_Project/Prefabs`로 정리 |
| `Assets/Main Camera.prefab` | `Assets/_Project/Prefabs/Managers` 또는 `Prefabs/Camera`로 이동 |
| `Assets/Scenes` | 실제 씬과 테스트 씬을 `_Project/Scenes` 아래로 분리 |
| `Assets/MMData` | 런타임 생성 데이터인지, 커밋 대상인지 확인 |
| TopDownEngine 데모 프리팹 직접 사용 | 우리 프로젝트 프리팹으로 복제 후 수정 권장 |

## 12. 팀 공통 체크리스트

새 파일을 만들기 전에 다음을 확인한다.

- 이 파일은 우리 코드인가, 외부 에셋 수정인가?
- 런타임 코드인가, 에디터 코드인가, 테스트 코드인가?
- 어느 시스템에 속하는가?
- 네임스페이스가 폴더 구조와 맞는가?
- 테스트용이면 `Test` 폴더에 있는가?
- ScriptableObject라면 타입별 폴더에 있는가?
- TopDownEngine에 직접 의존한다면 `Integrations/TopDownEngine`에 둘 수 있는가?


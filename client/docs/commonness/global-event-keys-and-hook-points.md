# 전역 이벤트 키 및 주요 훅 포인트 정리

> 대상 프로젝트: `client/LostMemory`  
> 목적: 런, 스테이지, 방, 적, 보상, UI, 멀티플레이 구현이 같은 사건 기준으로 연결되도록 전역 이벤트 키와 주요 훅 포인트를 정리한다.

## 1. 결론

- 우리 프로젝트 전용 코드와 리소스는 `Assets/_Project` 아래에 둔다.
- 현재 `LostMemory/Assets/_Project` 폴더는 아직 존재하지 않는다. 구현 시 새로 만든다.
- 전역 이벤트 키는 문서와 로그에서 `Enemy.Died`, `Room.Cleared` 같은 점 표기 문자열로 정리한다.
- 실제 C# 구현은 가능하면 `EnemyDiedEvent` 같은 타입 기반 이벤트로 보호한다.
- TopDown Engine 원본 이벤트는 게임 규칙에서 직접 구독하지 않고, Adapter 또는 Reporter가 받아 우리 게임 이벤트로 변환한다.
- 멀티플레이를 고려해 모든 전역 이벤트에 네트워크 범위를 함께 기록한다.

## 2. 왜 필요한가

전역 이벤트 기준이 없으면 같은 사건을 시스템마다 다르게 판단하게 된다.

예를 들어 적 사망은 하나의 사건이지만 실제로는 여러 시스템이 동시에 반응한다.

```text
Health.OnDeath
  -> EnemyCombatReporter
  -> Enemy.Died
  -> RoomCombatController
  -> RewardManager
  -> UI
  -> SFX / VFX
```

이 기준이 없으면 다음 문제가 생긴다.

- `Health.OnDeath`, `MMLifeCycleEvent.Death`, `KillsManager.OnLastDeath`가 섞여 중복 처리될 수 있다.
- 방 클리어, 보상, 다음 방 이동 순서가 시스템마다 달라질 수 있다.
- 새 기능을 붙일 때 기존 전투 코드나 TopDown Engine 원본을 계속 건드리게 된다.
- 멀티플레이 구현 시 어떤 사건을 서버가 확정해야 하는지 구분하기 어려워진다.

따라서 전역 이벤트는 시스템 간 계약으로 보고, 발행자와 구독자를 문서로 먼저 고정한다.

## 3. 구현 경로 기준

우리 프로젝트 전용 코드와 리소스는 `Assets/_Project` 아래에 둔다.

```text
Assets/
  _Project/
    Scripts/
      Runtime/
        Core/
        SceneFlow/
        Stage/
        Rooms/
        Enemies/
        Rewards/
        Relics/
        UI/
        Integrations/
          TopDownEngine/
      Editor/
      Tests/

    Prefabs/
    Scenes/
    ScriptableObjects/
    Art/
    Audio/

  TopDownEngine/
  CodeRespawn/
  MMData/
```

권장 위치:

| 책임 | 권장 경로 |
|---|---|
| 전역 이벤트 키, 이벤트 버스, 공통 이벤트 타입 | `Assets/_Project/Scripts/Runtime/Core` |
| Bootstrap 이후 씬 이동 흐름 | `Assets/_Project/Scripts/Runtime/SceneFlow` |
| 런, 스테이지 진행 상태 | `Assets/_Project/Scripts/Runtime/Stage` |
| 방 진입, 잠금, 클리어 판정 | `Assets/_Project/Scripts/Runtime/Rooms` |
| 적 피격, 사망 Reporter | `Assets/_Project/Scripts/Runtime/Enemies` |
| 보상 선택, 보상 적용 | `Assets/_Project/Scripts/Runtime/Rewards` |
| 유물 보유, 유물 효과 적용 | `Assets/_Project/Scripts/Runtime/Relics` |
| HUD, 보상 UI, 전투 UI | `Assets/_Project/Scripts/Runtime/UI` |
| TopDown Engine 이벤트 연결 어댑터 | `Assets/_Project/Scripts/Runtime/Integrations/TopDownEngine` |

주의:

- `Assets/TopDownEngine` 원본은 직접 수정하지 않는다.
- `Assets/CodeRespawn` 원본은 직접 수정하지 않는다.
- `Assets/Game`은 사용하지 않고 신규 프로젝트 코드는 `Assets/_Project`로 통일한다.
- 기존 문서에 `Assets/Game/...` 예시가 있더라도, 앞으로의 기준은 `Assets/_Project`로 본다.

## 4. 이벤트 방식 권장안

게임 전체 규칙에는 직접 만든 얇은 EventBus 또는 타입 기반 이벤트를 권장한다.

| 방식 | 용도 | 장점 | 주의점 |
|---|---|---|---|
| C# `event` | 같은 시스템 내부의 가까운 객체 연결 | 빠르고 타입 안전하며 추적이 쉽다 | Inspector 연결이 안 되고 구독 해제 실수가 날 수 있다 |
| `UnityEvent` | 씬, 프리팹, 버튼, 연출 연결 | Inspector에서 연결할 수 있다 | 전역 규칙에 쓰면 추적과 리팩터링이 어렵다 |
| `MMEventManager` | TopDown Engine 내부 이벤트 수신 | TDE와 궁합이 좋고 이미 엔진에서 사용한다 | 우리 게임 규칙이 TDE 이벤트에 종속될 수 있다 |
| 프로젝트 EventBus | 런, 방, 적, 보상 같은 게임 규칙 연결 | 키, payload, 로그, 멀티 범위를 통일하기 좋다 | 직접 설계하고 유지해야 한다 |

권장 흐름:

```text
TopDown Engine event / Unity callback
  -> _Project Adapter or Reporter
  -> LostMemory game event
  -> Game systems subscribe
```

예시:

```text
Health.OnDeath
  -> EnemyCombatReporter
  -> Enemy.Died
  -> RoomCombatController / RewardManager / UI
```

## 5. 네이밍 규칙

문서와 로그에서는 점 표기 이벤트 키를 사용한다.

```text
Domain.Action
Domain.Subject.Action
```

예시:

```text
Run.Started
Stage.Entered
Room.Cleared
Enemy.Died
Reward.Selected
```

규칙:

- 과거형은 이미 확정된 사건에 사용한다. 예: `Enemy.Died`, `Room.Cleared`
- 요청은 `Requested`를 사용한다. 예: `Scene.LoadRequested`
- 시작과 종료는 `Started`, `Completed`, `Failed`, `Ended`를 사용한다.
- 구현 세부사항 이름은 피한다. 예: `OnTriggerEntered`, `OnHealthCallback` 같은 이름은 전역 키로 쓰지 않는다.
- 코드에서는 문자열 직접 입력을 피하고 상수, enum, 또는 타입 기반 이벤트로 감싼다.

권장 구현 방향:

```csharp
public static class GameEventKeys
{
    public const string EnemyDied = "Enemy.Died";
    public const string RoomCleared = "Room.Cleared";
}
```

또는:

```csharp
public readonly struct EnemyDiedEvent
{
    public readonly string EnemyId;
    public readonly string RoomId;
}
```

문서 키는 `Enemy.Died`, 구현 타입은 `EnemyDiedEvent`처럼 병행하는 방식을 권장한다.

## 6. 네트워크 범위

멀티플레이 구현을 고려해 이벤트마다 네트워크 범위를 구분한다.

| 범위 | 의미 | 예시 |
|---|---|---|
| `Local` | 현재 클라이언트에서만 처리해도 되는 사건 | VFX, SFX, HUD 애니메이션 |
| `ServerAuthority` | 서버가 최종 판정해야 하는 사건 | 피격, 사망, 방 클리어, 보상 확정 |
| `Broadcast` | 서버 확정 후 모든 클라이언트에 알려야 하는 사건 | 방 클리어, 문 열림, 보상 후보 표시 |
| `ClientRequest` | 클라이언트가 서버에 요청하는 사건 | 상호작용, 보상 선택, 이동 요청 |

멀티플레이에서 중요한 원칙:

- 체력, 사망, 보상 획득, 방 클리어는 서버 권한으로 확정한다.
- SFX, VFX, 카메라 흔들림, UI 애니메이션은 로컬 이벤트로 분리한다.
- `Enemy.Died` 같은 게임 상태 이벤트와 `Enemy.DeathVfxRequested` 같은 연출 이벤트를 섞지 않는다.

## 7. 전역 이벤트 키 목록

아래 표는 1차 권장안이다. 구현 중 실제 시스템 이름이 정해지면 발행자와 payload 이름을 갱신한다.

| 이벤트 키 | 발생 시점 | 발행 주체 | 주요 구독자 | 주요 payload | 네트워크 범위 |
|---|---|---|---|---|---|
| `App.Bootstrapped` | Bootstrap 전역 초기화 완료 | `AppRoot` | `SceneFlowManager`, 초기 UI | app version, initial scene | `Local` |
| `Scene.LoadRequested` | 씬 이동 요청 | `SceneFlowManager` | 로딩 UI, 저장 시스템 | target scene, reason | `ClientRequest` 또는 `ServerAuthority` |
| `Scene.Loaded` | 씬 로드 완료 | `SceneFlowManager` | Stage, UI, Audio | scene name | `Local` |
| `Run.StartRequested` | 런 시작 요청 | Lobby UI | `RunManager` | selected character, weapon | `ClientRequest` |
| `Run.Started` | 런 상태 생성 완료 | `RunManager` | `StageRouteManager`, UI | run id, seed, loadout | `ServerAuthority` |
| `Run.Ended` | 런 종료 확정 | `RunManager` | UI, Save, Memory | result, elapsed time | `ServerAuthority` |
| `Stage.Entered` | 스테이지 씬 진입 완료 | `StageRouteManager` | Room, UI, Audio | stage id, route index | `Broadcast` |
| `Stage.Completed` | 스테이지 목표 완료 | `StageRouteManager` | Reward, SceneFlow | stage id | `ServerAuthority` |
| `Room.Entered` | 플레이어가 방에 진입 | `RoomEntryDetector` | Room, Camera, Door | room id, actor id | `ServerAuthority` |
| `Room.Locked` | 방 입구/출구 잠금 | `RoomCombatController` | Door, UI, Audio | room id | `Broadcast` |
| `Room.CombatStarted` | 방 전투 시작 | `RoomCombatController` | EnemySpawner, UI, Audio | room id, wave id | `Broadcast` |
| `Room.Cleared` | 방 전투 클리어 확정 | `RoomCombatController` | Door, Reward, StageRoute | room id, clear time | `ServerAuthority` |
| `Room.ExitOpened` | 출구 활성화 | `RoomExitController` | UI, VFX | room id, exit id | `Broadcast` |
| `Wave.Started` | 웨이브 시작 | `RoomCombatController` | EnemySpawner, UI | room id, wave index | `Broadcast` |
| `Wave.Completed` | 웨이브 내 적 전멸 | `RoomCombatController` | Room, Reward | room id, wave index | `ServerAuthority` |
| `Enemy.Spawned` | 적 생성 완료 | `EnemySpawner` | RoomCombatController, UI | enemy id, room id | `ServerAuthority` |
| `Enemy.Hit` | 적 피격 확정 | `EnemyCombatReporter` | UI, VFX, Relic effects | enemy id, damage, source id | `ServerAuthority` |
| `Enemy.Died` | 적 사망 확정 | `EnemyCombatReporter` | RoomCombatController, Reward, UI | enemy id, room id, killer id | `ServerAuthority` |
| `Player.Spawned` | 플레이어 생성 또는 배치 완료 | `PlayerSpawnController` | Camera, UI, StatApplier | player id, spawn point | `Broadcast` |
| `Player.Hit` | 플레이어 피격 확정 | `PlayerCombatReporter` | UI, VFX, Audio | player id, damage, source id | `ServerAuthority` |
| `Player.Died` | 플레이어 사망 확정 | `PlayerCombatReporter` | RunManager, UI, SceneFlow | player id, cause | `ServerAuthority` |
| `Reward.Offered` | 보상 후보 생성 | `RewardManager` | Reward UI | reward ids, source room | `ServerAuthority` |
| `Reward.Selected` | 플레이어가 보상 선택 | Reward UI | RewardManager | player id, reward id | `ClientRequest` |
| `Reward.Applied` | 보상 적용 완료 | `RewardManager` | Relic, UI, Save | player id, reward id | `ServerAuthority` |
| `Relic.Acquired` | 유물 획득 확정 | `RelicManager` | UI, StatApplier | player id, relic id | `ServerAuthority` |
| `Relic.EffectApplied` | 유물 효과 적용 완료 | `RelicManager` | UI, Debug | player id, relic id, target stat | `ServerAuthority` |
| `Currency.Changed` | 재화 값 변경 | `CurrencyManager` | UI, Save | player id, delta, total | `ServerAuthority` |
| `Shop.Opened` | 상점 UI 열림 | `ShopController` | Shop UI, Input | shop id, player id | `Local` 또는 `Broadcast` |
| `Shop.PurchaseRequested` | 구매 요청 | Shop UI | ShopManager | player id, item id | `ClientRequest` |
| `Shop.PurchaseCompleted` | 구매 확정 | ShopManager | UI, Inventory, Save | player id, item id, price | `ServerAuthority` |
| `Save.Loaded` | 저장 데이터 로드 완료 | `SaveManager` | Run, Talent, Memory | slot id | `Local` |
| `Save.Written` | 저장 완료 | `SaveManager` | UI | slot id, timestamp | `Local` |

## 8. 주요 훅 포인트

TopDown Engine과 Unity에서 들어오는 이벤트는 아래 지점에서 우리 이벤트로 변환한다.

| 원본 훅 포인트 | 변환 이벤트 | Adapter 또는 Reporter | 설명 |
|---|---|---|---|
| `AppRoot.Awake` | `App.Bootstrapped` | `AppRoot` | 전역 매니저 생성과 초기화가 끝난 뒤 발행 |
| `SceneManager.sceneLoaded` | `Scene.Loaded`, `Stage.Entered` | `SceneFlowManager` | 씬 로드 완료를 게임 흐름 이벤트로 변환 |
| `CharacterDetector` | `Room.Entered` | `RoomEntryDetector` | 방 진입 감지 |
| `ButtonActivatedZone`, `Switch` | `Room.ExitOpened`, `Shop.Opened` | 목적별 Controller | 문, 포탈, 상점 상호작용 연결 |
| `KillsManager.OnLastDeath` | `Wave.Completed` 또는 `Room.Cleared` | `RoomCombatController` | TDE kill 집계를 사용할 경우 방 클리어 후보로 사용 |
| `Health.OnHit` | `Enemy.Hit`, `Player.Hit` | `EnemyCombatReporter`, `PlayerCombatReporter` | 피격 확정 지점 |
| `Health.OnDeath` | `Enemy.Died`, `Player.Died` | `EnemyCombatReporter`, `PlayerCombatReporter` | 사망 확정 지점 |
| `MMLifeCycleEvent.Death` | `Enemy.Died`, `Player.Died` | `TopDownLifeCycleEventAdapter` | MMEventManager 기반 사망 감지가 필요할 때 사용 |
| `PickableItemEvent` | `Reward.Applied`, `Currency.Changed` | `TopDownPickableEventAdapter` | TDE pickable을 사용할 경우 획득 이벤트로 변환 |
| `MMInventoryEvent` | `Reward.Applied`, `Shop.PurchaseCompleted` | `TopDownInventoryEventAdapter` | InventoryEngine을 사용할 경우 인벤토리 변경 연결 |
| `FinishLevel`, `GoToLevelEntryPoint` | `Scene.LoadRequested`, `Stage.Completed` | `StageRouteManager` | 다음 방 또는 다음 스테이지 이동 연결 |
| `MMSceneLoadingManager` 진행 이벤트 | `Scene.Loaded` 관련 UI 이벤트 | `SceneFlowManager` | 로딩 UI가 필요할 경우만 사용 |

원칙:

- 훅 포인트는 원본 이벤트를 감지하는 자리일 뿐, 게임 규칙의 최종 기준은 우리 이벤트로 둔다.
- 같은 사건을 여러 훅에서 동시에 발행하지 않는다.
- 중복 가능성이 있는 사건은 Reporter 내부에서 한 번만 발행되도록 보호한다.

예시:

```text
Health.OnDeath
  -> EnemyCombatReporter.OnDeath()
  -> alreadyReported 검사
  -> Enemy.Died 발행
```

## 9. 시스템별 발행/구독 관계

| 시스템 | 주로 발행하는 이벤트 | 주로 구독하는 이벤트 |
|---|---|---|
| `AppRoot` | `App.Bootstrapped` | 없음 |
| `SceneFlowManager` | `Scene.LoadRequested`, `Scene.Loaded` | `Run.Started`, `Stage.Completed`, `Run.Ended` |
| `RunManager` | `Run.Started`, `Run.Ended` | `Run.StartRequested`, `Player.Died`, `Stage.Completed` |
| `StageRouteManager` | `Stage.Entered`, `Stage.Completed` | `Scene.Loaded`, `Room.Cleared` |
| `RoomCombatController` | `Room.Locked`, `Room.CombatStarted`, `Room.Cleared`, `Wave.Started`, `Wave.Completed` | `Room.Entered`, `Enemy.Spawned`, `Enemy.Died` |
| `EnemySpawner` | `Enemy.Spawned` | `Room.CombatStarted`, `Wave.Started` |
| `EnemyCombatReporter` | `Enemy.Hit`, `Enemy.Died` | TDE `Health` 이벤트 |
| `PlayerCombatReporter` | `Player.Hit`, `Player.Died` | TDE `Health` 이벤트 |
| `RewardManager` | `Reward.Offered`, `Reward.Applied` | `Room.Cleared`, `Reward.Selected` |
| `RelicManager` | `Relic.Acquired`, `Relic.EffectApplied` | `Reward.Applied`, `Run.Started` |
| `ShopManager` | `Shop.PurchaseCompleted`, `Currency.Changed` | `Shop.PurchaseRequested` |
| `UI Presenter` | `Reward.Selected`, `Shop.PurchaseRequested` | 상태 변경 이벤트 전반 |

## 10. 멀티플레이 구현 시 주의사항

나중에 멀티플레이를 구현할 때 전역 이벤트를 그대로 네트워크 메시지로 보내면 안 된다.

구분:

```text
Game event
  게임 내부 시스템 간 계약

Network message
  서버와 클라이언트 사이에 동기화할 데이터 계약
```

권장 흐름:

```text
Client input
  -> ClientRequest event
  -> Network request
  -> Server validation
  -> ServerAuthority game event
  -> Broadcast state result
  -> Local UI / VFX event
```

예시:

```text
Reward.Selected
  -> client requests selected reward
  -> server validates reward candidate
  -> Reward.Applied
  -> clients update UI
```

서버 권한으로 둬야 하는 사건:

- `Enemy.Hit`
- `Enemy.Died`
- `Player.Hit`
- `Player.Died`
- `Room.Cleared`
- `Reward.Applied`
- `Relic.Acquired`
- `Currency.Changed`
- `Shop.PurchaseCompleted`

로컬로 둬도 되는 사건:

- 피격 VFX 요청
- 사운드 재생
- 카메라 흔들림
- HUD 애니메이션
- 버튼 hover, 선택 강조

## 11. 문서 작업 범위

이번 작업은 구현 없이 문서 정리만 수행한다.

포함:

- 전역 이벤트 키 명명 규칙
- 주요 이벤트 키 목록
- TopDown Engine / Unity 훅 포인트
- 권장 구현 경로
- 멀티플레이 대비 네트워크 범위

제외:

- `GameEventBus.cs` 구현
- `GameEventKeys.cs` 구현
- Unity scene, prefab, asset 직접 수정
- TopDown Engine 원본 코드 수정
- 네트워크 라이브러리 선정

## 12. Open Issues

구현 전에 아래 항목을 팀에서 다시 확정한다.

| 항목 | 현재 권장안 | 확정 필요 시점 |
|---|---|---|
| 이벤트 구현 방식 | 타입 기반 이벤트 + 얇은 EventBus | 첫 전역 이벤트 구현 전 |
| 네트워크 라이브러리 | 미정 | 멀티플레이 프로토타입 전 |
| 방 클리어 기준 | `Enemy.Died` 집계 우선, 필요 시 `KillsManager.OnLastDeath` 보조 | CL-035 구현 전 |
| 보상 흐름 기준 | `Room.Cleared` 이후 `Reward.Offered` | CL-043 구현 전 |
| TDE 인벤토리 사용 여부 | 직접 구현 우선, 필요 시 Adapter 연결 | 상점/아이템 구현 전 |

## 13. 체크리스트

전역 이벤트를 새로 추가할 때 다음을 확인한다.

- 이 사건이 정말 여러 시스템이 알아야 하는 전역 사건인가?
- 단일 컴포넌트 내부 이벤트로 충분하지 않은가?
- 발행자는 하나로 정해져 있는가?
- 중복 발행 방지가 필요한가?
- payload에 필요한 ID가 포함되어 있는가?
- 멀티플레이에서 `Local`, `ClientRequest`, `ServerAuthority`, `Broadcast` 중 어디에 속하는가?
- TopDown Engine 원본 이벤트를 직접 퍼뜨리지 않고 Adapter 또는 Reporter를 거치는가?
- 문서 키와 구현 타입 이름이 서로 대응되는가?

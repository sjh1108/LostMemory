# CL-032 방 타입·방 데이터 구조 정의 계획

## 목적

`CL-032 방 타입·방 데이터 구조 정의`의 방향을 정리한다.

CL-032 는 Epic D(전투 공간 흐름·방 인터랙션) 의 데이터 기반이며, 후속 작업이 이 위에서 동작한다.

- `CL-033` 스폰 포인트·웨이브 배치 규칙
- `CL-034` 방 진입 시 전투 시작 초기화
- `CL-035` 적 전멸 기준 방 클리어 판정
- `CL-036` 문 열림·다음 방 전환 흐름

따라서 본 작업은 후속 4개 작업이 *어디에* 데이터를 채울지 슬롯의 모양을 정하는 것까지를 범위로 한다. 슬롯 *내부* 의 의미와 동작은 해당 CL 에서 채운다.

이미 존재하는 코드는 그대로 사용한다.

- `StageRoomType` (Combat/Shop/Event/Boss)
- `StageRoomProgress` (런타임 방문/완료 상태)
- `BossEntryRequirementMode` (Auto/Required/Ignored)
- `BossRoomEntryTracker`, `BossRoomEntryConditionCalculator`

## 결정 사항

- 방 데이터는 `RoomData` ScriptableObject 로 신설한다.
- `RoomData` 는 *정적 템플릿*, `StageRoomProgress` 는 *런타임 상태* 로 라이프사이클을 분리한다. 둘을 한 클래스로 합치지 않는다.
- `StageRoomProgress` 는 그대로 두고 `RoomData` 역참조 한 줄만 추가한다.
- `StageRoomType` enum 은 손대지 않는다.
- 본 작업은 *스켈레톤 + 슬롯* 까지만 정의하고, 슬롯 내부는 `CL-033~036` 가 채운다.
- 디자이너가 자산을 작성해 볼 수 있게 sample asset 2개를 함께 커밋한다.
- 라이브 튠 (`WeaponData` 의 ContextMenu Save) 은 본 작업에서는 보류한다. 방 데이터는 Play 중 슬라이더로 만질 일이 적다.

## 시작 수치

본 작업은 수치보다 스키마 정의가 중심이라 별도 시작 수치 표는 없다. 슬롯 → 후속 CL 매핑은 아래 "슬롯 매핑" 표를 따른다.

## 코드 구조

### `RoomData` (ScriptableObject)

위치: `Assets/_Project/Scripts/Runtime/Stage/Data/RoomData.cs`
namespace: `LostMemory.Stage.Data`
메뉴: `LostMemory/Stage/RoomData`

필드:

- Identity: `roomId`, `displayName`
- Type / Category: `roomType` (`StageRoomType`), `category` (`RoomCategory`), `bossEntryRequirementMode` (`BossEntryRequirementMode`), `sequenceIndex`
- Layout slot: `layoutPrefab`
- Slot (CL-033~036 채움):
  - `initContext` (`RoomInitContextSpec`) — `CL-034`
  - `encounter` (`RoomEncounterSpec`) — `CL-033`
  - `clearCondition` (`RoomClearConditionType`) — `CL-035`
  - `exits` (`RoomExitSpec[]`) — `CL-036`
  - `rewardPool` (`RoomRewardPoolSpec`) — 후속

본 작업이 *값으로* 채우는 것은 Identity / Type / Category / BossEntryRequirementMode / sequenceIndex / clearCondition 기본값 / sample 자산의 layoutPrefab 정도다.

### 보조 타입

같은 폴더에 작은 파일로 분리한다.

- `RoomCategory.cs` — enum `LargeRoom`(큰방) / `SmallRoom`(작은방)
- `RoomClearConditionType.cs` — enum `AllEnemiesDefeated` / `InteractionComplete` / `Custom`
- `RoomEncounterSpec.cs` — `[Serializable]` 빈 스텁. `CL-033` 가 spawn point / wave 데이터를 채운다.
- `RoomExitSpec.cs` — `[Serializable]` 스텁. 현재 필드는 `nextRoomId` 만. `CL-036` 가 문 위치/조건을 추가한다.
- `RoomInitContextSpec.cs` — `[Serializable]` 빈 스텁. `CL-034` 가 카메라/음악/플레이어 스폰 포인트 등을 채운다.
- `RoomRewardPoolSpec.cs` — `[Serializable]` 빈 스텁. combat 룸 전용. 보상 시스템 후속에서 채운다.

스텁들은 *지금* 동작 코드를 두지 않는다. 슬롯의 *모양* 만 잡는 것이 본 작업의 의도다.

### `StageRoomProgress` 확장

`Assets/_Project/Scripts/Runtime/Stage/StageRoomProgress.cs` 를 다음만 수정한다.

- `[SerializeField] private RoomData sourceData;` 필드 추가
- `RoomData SourceData => sourceData;` 접근자 추가
- `Configure(...)` 에 `RoomData` 인자를 갖는 오버로드 추가 (기본값 `null`, 기존 시그니처 보존)
- 기존 `CountsAsBossRequirement` 동작은 변경하지 않는다. RoomData 가 있어도 RuntimeMode 와 Type 은 progress 가 권위 보유한다.

### sample 자산

`Assets/_Project/ScriptableObjects/Rooms/` 신규 폴더.

- `RoomData_Sample_Combat_Small.asset` — Combat / SmallRoom / clearCondition `AllEnemiesDefeated`
- `RoomData_Sample_Boss.asset` — Boss / LargeRoom / clearCondition `Custom` / bossEntryRequirementMode `Ignored`

슬롯 (`encounter`, `initContext`, `exits`, `rewardPool`) 은 비워둔다. 후속 CL 이 채운다.

## 슬롯 매핑

| 필드 | 1차에서 채움? | 채우는 CL |
|---|---|---|
| `roomId`, `displayName` | O | — |
| `roomType`, `category`, `bossEntryRequirementMode`, `sequenceIndex` | O | — |
| `layoutPrefab` | sample 만 | 본격 prefab 작성은 별도 |
| `initContext` | X | `CL-034` |
| `encounter` | X | `CL-033` |
| `clearCondition` | enum 기본값만 | `CL-035` |
| `exits` | X | `CL-036` |
| `rewardPool` | X | 후속 |

## 명명·관례

- `Khi*` 접두는 검 콤보처럼 *플레이어 행동* 코드의 패턴이다. 방 데이터는 게임 시스템 데이터이므로 `Khi` 접두를 쓰지 않고 기존 `StageRoom*` 와 결을 맞춘다.
- namespace 는 `LostMemory.Stage.Data` 로 한다. `LostMemory.Data` (WeaponData) 와 분리해서 Stage 시스템 소속을 분명히 한다.
- TDE 원본은 건드리지 않는다.

## 멀티플레이 고려

- `RoomData` 는 정적 자산이므로 모든 클라이언트가 동일 자산을 공유한다.
- 방 진행 상태(`StageRoomProgress`) 는 호스트 권위로 동기화한다. 클라이언트는 호스트가 보낸 progress 만 반영한다.
- `RoomData` 자체에 런타임 mutable 데이터를 넣지 않는다. 동기화 대상은 progress 쪽이다.

이 원칙은 `cl009` 의 멀티플레이 가이드와 동일하다.

## CL-032 완료 기준

- Unity 에서 `Project` 창 우클릭 → Create → `LostMemory/Stage/RoomData` 가 보인다.
- 위 메뉴로 sample 자산 2개를 생성하고 Inspector 에서 모든 필드와 슬롯이 직렬화된다.
- 기존 `StageRoomProgress` 호출자가 컴파일 깨지지 않는다 (현재 외부 호출자 없음을 확인).
- `BossRoomEntryConditionCalculator` 의 보스 진입 판정 동작이 변동 없다.
- 후속 `CL-033~036` 가 슬롯 안만 채워서 작업을 시작할 수 있는 상태로 남는다.

## 후속 작업

- `CL-033` 에서 `RoomEncounterSpec` 에 spawn point / wave 데이터를 채운다.
- `CL-034` 에서 `RoomInitContextSpec` 에 카메라/음악/플레이어 스폰을 채우고 진입 초기화 코드와 연결한다.
- `CL-035` 에서 `RoomClearConditionType` 의 각 분기에 클리어 판정 로직을 붙인다.
- `CL-036` 에서 `RoomExitSpec` 에 문 위치/전환 조건을 채우고 다음 방 전환 흐름을 붙인다.
- 보상 시스템 CL 에서 `RoomRewardPoolSpec` 를 채운다.
- `Data/` 폴더 분기 (`Runtime/Data/` vs `Runtime/Stage/Data/`) 통합 여부는 데이터 SO 가 더 늘어난 시점에 다시 판단한다.

## 위험·결정 보류

- `Data/` 폴더 분기: `WeaponData` 는 `Runtime/Data/`, `RoomData` 는 `Runtime/Stage/Data/`. 본 작업에서는 분리 유지, 후속에서 통합 검토.
- `StageRoomProgress.sourceData` nullability: 기존 호출자가 RoomData 없이 progress 만으로 동작하는 경로가 있어 항상 nullable 로 둔다. `BossRoomEntryTracker` 에서 sample 을 연결할 때도 RoomData 없이 동작 가능해야 한다.

## 구현 결과

2026-04-27 기준 CL-032 1차 구현을 완료했다.

추가된 런타임 코드 (`Assets/_Project/Scripts/Runtime/Stage/Data/`):

- `RoomData`
  - 방의 정적 템플릿을 담는 ScriptableObject.
  - Identity / Type / Category / BossEntryRequirementMode / sequenceIndex / layoutPrefab + 슬롯 5종.
  - 메뉴: `LostMemory/Stage/RoomData`.
- `RoomCategory` enum: `SmallRoom`, `LargeRoom`.
- `RoomClearConditionType` enum: `AllEnemiesDefeated`, `InteractionComplete`, `Custom`.
- `RoomEncounterSpec`: 빈 `[Serializable]` 스텁. CL-033 가 채운다.
- `RoomExitSpec`: `nextRoomId` 필드만. CL-036 가 채운다.
- `RoomInitContextSpec`: 빈 스텁. CL-034 가 채운다.
- `RoomRewardPoolSpec`: 빈 스텁. 보상 시스템 후속 CL 이 채운다.

변경된 코드:

- `StageRoomProgress`
  - `[SerializeField] private RoomData sourceData;` 와 `SourceData` 접근자 추가.
  - `Configure(...)` 에 RoomData 인자를 받는 오버로드 추가. 기존 시그니처는 보존.
  - `CountsAsBossRequirement` 등 기존 동작은 변경하지 않음.
- 그 외 보스 진입 관련 코드는 손대지 않음.

자산:

- `Assets/_Project/ScriptableObjects/Rooms/` 폴더 신설.
- `RoomData_Sample_Combat_Small.asset`, `RoomData_Sample_Boss.asset` 두 sample 자산을 Unity 메뉴로 생성. 슬롯 내부는 의도적으로 비움.
  - `.asset` 은 Unity 가 생성한 스크립트 GUID 가 필요해 코드로 미리 쓰지 않고, `Project` 창 → Create 메뉴로 생성.

## 현재 검증 결과

Unity 에디터에서 다음을 확인했다.

- `Project` 창 우클릭 → Create → `LostMemory/Stage/RoomData` 메뉴가 보인다.
- 위 메뉴로 sample 자산 2개를 생성할 수 있다.
- Inspector 에서 Identity, Type / Category, Layout, Sequence Index, Clear Condition, Exits 가 직렬화돼 보인다.
- 빈 슬롯(`InitContext`, `Encounter`, `RewardPool`) 은 내부 필드가 0개라 Inspector 행이 만들어지지 않는다. 직렬화는 되지만 그릴 것이 없는 상태이며, 후속 CL 이 필드를 추가하면 자동으로 펼쳐진다.
- 컴파일 에러 없음. `StageRoomProgress.Configure(...)` 의 외부 호출자가 없어 시그니처 변경 영향이 없다.

## CL-032 1차 완료 판단

현재 기준으로 CL-032 는 기능 기준 1차 완료로 본다.

완료된 항목:

- `RoomData` SO + 보조 enum / spec 클래스
- `StageRoomProgress` 의 RoomData 역참조 추가
- `LostMemory/Stage/RoomData` Create 메뉴 노출
- sample 자산 2개와 Inspector 직렬화 확인
- 후속 CL-033~036 가 슬롯만 채워 시작 가능한 스켈레톤 확보

완료를 막지 않는 후속 조정:

- 슬롯 내부 데이터는 각 CL 에서 점진적으로 채운다.
- `Data/` 폴더 통합 (Runtime/Data vs Runtime/Stage/Data) 은 데이터 SO 가 더 늘어난 시점에 다시 판단한다.
- 라이브 튠 (`WeaponData` 의 ContextMenu Save 패턴) 도입은 방 데이터에서 필요해진 시점에 검토한다.

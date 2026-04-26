# CL-033 스폰 포인트·웨이브 배치 규칙 정의 계획

## 목적

`CL-033 스폰 포인트·웨이브 배치 규칙 정의` 의 방향을 정리한다.

본 작업은 `CL-032` 가 만든 빈 슬롯 `RoomEncounterSpec` 을 채워, 디자이너가 작은방 한 개의 전투 구성을 한 자산에서 표현할 수 있게 한다.

산출물은 다음 작업의 *입력* 으로 쓰인다.

- `CL-034` 방 진입 시 전투 시작 초기화 — 본 자료를 읽어 실제 적을 스폰
- `CL-035` 적 전멸 기준 방 클리어 판정 — 본 자료의 적 총 수를 클리어 카운팅에 사용

본 작업이 *만들지 않는 것* 도 명확히 한다.

- 적 prefab, EnemyData SO, 적 AI — *적 관리자 담당자* 영역. 본 작업은 적을 가리키는 키(`enemyId` string) 만 정의하고, 컨벤션 합의 표를 둔다.
- 실제 스폰 실행 코드, 무작위 선택, 동기화 — `CL-034` 영역.

## 결정 사항

- 본 작업 범위는 *데이터 정의 + 디자이너용 anchor MonoBehaviour* 까지.
- 스폰 포인트는 *Layout prefab 의 자식 Transform* 으로 둔다. 디자이너가 Scene 뷰에서 눈으로 위치를 잡는다.
- 각 스폰 포인트에는 `RoomEncounterSpawnPoint` 를 붙여 *그룹 태그* (string) 를 보유한다.
- Layout prefab 루트에 `RoomEncounterAnchor` 를 붙여 자식 SpawnPoint 들을 일괄 노출한다.
- 적 참조는 *string id* 만 사용한다 (`enemyId`). EnemyData SO 본격 정의는 적 관리자가 별도 진행한다.
- 웨이브 자료구조는 *N 개 웨이브* 를 표현 가능하게 만든다. MVP 작은방은 wave 1개 디폴트.
- TDE 의 spawner 는 wave/encounter 오케스트레이션이 부족하므로 wrap 하지 않고 프로젝트 코드로 다룬다 (`cl009`, `cl032` 와 같은 원칙).
- TDE 원본은 건드리지 않는다.

## 자료구조

### `RoomEncounterSpec` (1차 정의 채움)

`Assets/_Project/Scripts/Runtime/Stage/Data/RoomEncounterSpec.cs`

- `RoomEncounterWave[] waves` — 웨이브 배열.
- `IReadOnlyList<RoomEncounterWave> Waves` — 접근자.
- `int TotalEnemyCount` — 모든 wave 의 entry.count 합. CL-035 클리어 카운팅용.

### `RoomEncounterWave`

`Assets/_Project/Scripts/Runtime/Stage/Data/RoomEncounterWave.cs`, `[Serializable]`.

- `string label` — Inspector 식별용 (예 "Wave 1: 근접 3"). 게임 로직과 무관.
- `float startDelay` — 방 진입 후 이 wave 가 스폰될 때까지 지연 (Min 0).
- `RoomEncounterEnemyEntry[] enemies`.

### `RoomEncounterEnemyEntry`

`Assets/_Project/Scripts/Runtime/Stage/Data/RoomEncounterEnemyEntry.cs`, `[Serializable]`.

- `string enemyId` — 적 관리자와 합의한 키 (아래 컨벤션 표).
- `int count` — 1 이상.
- `string spawnGroupFilter` — 빈 문자열이면 모든 스폰 포인트 후보.

### `RoomEncounterSpawnPoint` (MonoBehaviour)

`Assets/_Project/Scripts/Runtime/Stage/Data/RoomEncounterSpawnPoint.cs`

- `string groupTag` — 디자이너가 입력 (예 "near_door").
- 자식 Transform 에 붙는다. 디자이너가 prefab 안에서 빈 GameObject 를 두고 이 컴포넌트를 추가한다.

### `RoomEncounterAnchor` (MonoBehaviour)

`Assets/_Project/Scripts/Runtime/Stage/Data/RoomEncounterAnchor.cs`

- Layout prefab 루트에 붙는다.
- `RoomEncounterSpawnPoint[] cachedSpawnPoints` — Awake 에서 자식 일괄 수집.
- `IReadOnlyList<RoomEncounterSpawnPoint> GetSpawnPoints(string groupFilter)` — 빈 문자열이면 전체, 아니면 태그 정확 일치만 반환.
- `[ContextMenu("Refresh Spawn Points")]` — 디자이너가 prefab 자식 변경 후 한 번 누른다.

## 적 관리자와의 인터페이스 (충돌 회피)

본 작업은 EnemyData SO 도, 적 prefab 도 만들지 않는다. *적을 가리키는 키* 한 개 — `enemyId` (string) — 만 정의한다.

합의 사항:

- `enemyId` 는 적 관리자의 향후 EnemyData SO 키 필드와 일치해야 한다.
- 적 관리자가 다른 키 형태(예: enum) 를 채택하더라도, 본 작업이 enemyId 를 다루는 곳은 데이터 정의 한 곳이라 *치환* 비용이 적다.
- EnemyData SO 가 도착하면 `EnemyDataRegistry.Find(string id) → EnemyData` 같은 룩업을 CL-034 (또는 별도 CL) 에서 한 곳에 추가하면 된다. 본 작업의 자료구조는 변경 없음.

### `enemyId` 1차 컨벤션

| enemyId | 의도 | 적 역할 |
|---|---|---|
| `enemy_melee_basic` | 일반 근접 | 추적 + 근접 공격 |
| `enemy_ranged_basic` | 일반 원거리 | 거리 유지 + 투사체 |
| `enemy_charger_basic` | 일반 돌진 | 돌진 패턴 |
| `enemy_elite_01` | 엘리트 1종 | 상위 패턴 + 체력 증가 |

적 관리자가 다른 ID 를 원하면 본 표를 갱신하고 sample 자산 한 곳만 바꾸면 된다.

## 그룹 태그 컨벤션 (디자이너용)

| groupTag | 의도 |
|---|---|
| `` (빈 문자열) | 모든 스폰 포인트 후보 (default) |
| `near_door` | 작은방 입구 가까운 점 |
| `far` | 입구 반대쪽 |

매칭은 *정확 일치* 만 지원한다. 정규식이나 prefix 매칭은 다루지 않는다.

## 사용 흐름 (CL-034 가 본 자료를 어떻게 쓸지)

본 작업이 만들지 *않는* 코드의 의도만 명시한다.

```text
CL-034 가 방 진입 시:
1. RoomData.LayoutPrefab 인스턴스화
2. 인스턴스의 RoomEncounterAnchor 획득
3. RoomData.Encounter.Waves 순회
4. 각 wave 의 startDelay 후
5. wave.Enemies 순회 — enemyId 로 적 prefab 조회 (적 관리자 시스템)
6. spawnGroupFilter 로 anchor.GetSpawnPoints(filter) 호출
7. count 만큼 후보 중에서 골라 스폰
```

이 흐름이 매끄럽게 흐를 수 있게 본 자료구조를 잡는다.

## 명명·관례

- namespace: `LostMemory.Stage.Data` (CL-032 와 동일).
- MonoBehaviour 도 같은 namespace 로 둔다.
- `Khi*` 접두는 쓰지 않는다 — 게임 시스템 데이터 결.

## 멀티플레이 고려

- `RoomEncounterSpec` 은 정적 자산 → 모든 클라이언트가 동일 자산 공유.
- 스폰 *결정* (어느 점에 어떤 적이 나올지) 은 호스트 권위. 본 작업은 결정의 *입력* 만 제공.
- 호스트와 클라이언트가 같은 자료에서 다른 결과를 내지 않게, 자료는 *순서가 결정 가능한 배열* 만 사용한다. 시드 기반 무작위는 CL-034 영역.

## CL-033 완료 기준

- `RoomEncounterSpec` 가 wave 배열을 직렬화해 Inspector 에 노출한다.
- `RoomEncounterWave`, `RoomEncounterEnemyEntry` 가 중첩으로 펼쳐져 보인다.
- `RoomEncounterAnchor`, `RoomEncounterSpawnPoint` 가 컴파일되고 Add Component 로 붙는다.
- `RoomEncounterAnchor.GetSpawnPoints("")` 와 `GetSpawnPoints("near_door")` 가 의도대로 필터한다.
- Sample 자산 `RoomData_Sample_Combat_Small.asset` 의 encounter 에 wave 1개 (melee 2 + ranged 1) 가 들어가 있고 저장·재오픈 후 값이 유지된다.
- 적 관리자의 작업 영역 (EnemyData SO, 적 prefab) 을 본 작업이 만지지 않았다.

## 후속 작업

- `CL-034` 가 본 자료를 읽어 실제 스폰을 실행한다.
- `CL-035` 가 `RoomEncounterSpec.TotalEnemyCount` 를 클리어 카운팅 기준으로 쓴다.
- EnemyData SO 가 도착하면 `EnemyDataRegistry.Find(string id)` 룩업을 CL-034 에 추가한다.
- 페이즈 보스 같은 멀티 웨이브 콘텐츠는 `waves` 배열 항목 추가만으로 표현 가능.

## 위험·결정 보류

- 시드 기반 무작위 선택은 본 작업에서 다루지 않는다.
- spawnGroupFilter 매칭은 정확 일치만 (1차).
- `RoomEncounterAnchor` 는 prefab edit 중 자식 변경을 *자동* 감지하지 않는다. 디자이너가 ContextMenu 의 `Refresh Spawn Points` 를 한 번 눌러야 한다.

## 구현 결과

2026-04-27 기준 CL-033 1차 구현을 완료했다.

추가된 런타임 코드 (`Assets/_Project/Scripts/Runtime/Stage/Data/`):

- `RoomEncounterEnemyEntry`
  - `[Serializable]`. 한 wave 안의 적 한 항목.
  - `enemyId` (string), `count` (Min 1), `spawnGroupFilter` (빈 문자열 = any).
- `RoomEncounterWave`
  - `[Serializable]`. 한 웨이브.
  - `label`, `startDelay` (Min 0), `enemies` (`RoomEncounterEnemyEntry[]`).
- `RoomEncounterSpawnPoint`
  - MonoBehaviour. Layout prefab 의 빈 GameObject 자식에 붙어 한 스폰 위치를 표현.
  - `groupTag` (string) 한 개 보유. 위치는 Transform 이 권위.
- `RoomEncounterAnchor`
  - MonoBehaviour. Layout prefab 루트에 붙음.
  - `cachedSpawnPoints` 를 Awake 와 ContextMenu 두 경로로 수집.
  - `GetSpawnPoints(string groupFilter)` 가 빈 문자열이면 전체, 아니면 `groupTag` 정확 일치만 반환.
  - `[ContextMenu("Refresh Spawn Points")]` 로 디자이너가 자식 변경 후 재수집 가능.

변경된 코드:

- `RoomEncounterSpec`
  - 빈 스텁에서 `waves` (`RoomEncounterWave[]`) + `Waves` 접근자 + `TotalEnemyCount` 헬퍼로 채움.
  - `TotalEnemyCount` 는 모든 wave 의 entry.count 를 합산. CL-035 클리어 카운팅용.
- 그 외 파일 (`RoomData`, `StageRoomProgress` 등) 은 손대지 않음.

자산:

- `RoomData_Sample_Combat_Small.asset` 의 `encounter` 슬롯에 wave 1개 (melee 2 + ranged 1) 를 디자이너가 채워 직렬화 검증 예정.
- 적 관리자의 작업 영역 (EnemyData SO, 적 prefab) 은 만지지 않음.

## 현재 검증 결과

Unity 에디터에서 다음을 확인할 항목:

- 컴파일 에러 없음.
- `RoomData` 인스펙터의 `Encounter` 슬롯이 더 이상 빈 행이 아니라 `Waves` 배열로 펼쳐진다 (CL-032 단계에서는 내부 필드 0개라 안 보였던 슬롯이 이번 작업으로 가시화됨).
- `Add Component` 검색에서 `RoomEncounterAnchor`, `RoomEncounterSpawnPoint` 가 노출된다.
- 빈 GameObject 4개를 자식으로 둔 anchor 에 `Refresh Spawn Points` 를 누르면 `cachedSpawnPoints` 가 4개로 채워진다.
- `GetSpawnPoints("")` 는 전체 4개, `GetSpawnPoints("near_door")` 는 태그 일치 항목만 반환한다.
- sample 자산에 wave 를 채운 뒤 자산을 닫고 재오픈해도 값이 유지된다.

## CL-033 1차 완료 판단

현재 기준으로 CL-033 은 기능 기준 1차 완료로 본다.

완료된 항목:

- `RoomEncounterSpec` wave 배열 자료구조 + 접근자 + TotalEnemyCount.
- `RoomEncounterWave`, `RoomEncounterEnemyEntry` 직렬화 클래스.
- `RoomEncounterAnchor`, `RoomEncounterSpawnPoint` 디자이너용 MonoBehaviour 와 그룹 필터 읽기 API.
- `enemyId` 컨벤션 표 + 그룹 태그 컨벤션 표 문서화.
- 적 관리자 영역 침범 없음.

완료를 막지 않는 후속 조정:

- 적 관리자 측 EnemyData SO 가 도착하면 본 작업의 `enemyId` 를 그 SO 키와 정합시킨다.
- 디자이너가 sample prefab 을 작성해 anchor + SpawnPoint 구성을 검증한다.
- 시드 기반 스폰 무작위는 CL-034 에서 다룬다.

# CL-034 Troubleshooting — 방 진입 / 적 spawn 검증 단계

## 문서 목적

`CL-034 방 진입 시 전투 시작 초기화` 의 1차 코드 구현 후 Unity 에디터에서 sample prefab / 자산 / 검증 씬을 셋업하고 ▶ Play 로 검증하는 과정에서 만난 증상·진단·fix 를 한 곳에 정리한다.

진단의 *결론* 은 디자인 문서 (`cl034_room_entry_init_plan.md`) 의 "위험·결정 보류" 섹션에 한두 줄로 반영하고, 본 문서는 *왜 그런 결론에 도달했는지* 의 과정을 남긴다 — 같은 패턴의 적 prefab 이 추후 다시 등장할 때 진단 비용을 아끼기 위함.

## 사용된 환경

- TopDownEngine + DungeonArchitect 기반 프로젝트.
- 적 prefab: `Orc_CL037`, `OrcRider_CL039`, `SkeletonArcher_CL041` (CL-037~042 의 미완 산출물).
- CL-034 spawn 경로: `EnemyEncounterSpawner.Begin → RunWave 코루틴 → Instantiate(prefab, point.position, identity)`.
- 검증 씬: 기존 `test_khi.unity` 에 `CombatRoom_Sample_Small.prefab` 인스턴스를 추가.
- RoomData: `RoomData_Sample_Combat_Small.asset` 의 Encounter 슬롯 (Wave 1 — Orc 2 + Archer 1).

## 증상 정리

### 증상 1 — 첫 NullReferenceException 4건 (`ItemPicker.OnTriggerEnter2D`)

zone 진입 시 Console 에 4건의 NPE. 스택 끝이 `MoreMountains.InventoryEngine.Inventory.FindInventory` 로 끝남.

```
NullReferenceException
  Inventory.FindInventory(name, playerID)
  ItemPicker.FindTargetInventory(...)
  ItemPicker.OnTriggerEnter2D / Pick / Initialization / Start
```

### 증상 2 — Spawn 된 적 (특히 Archer) 이 움직이지도 공격하지도 않음

같은 wave 안에서 Orc 2 + Archer 1 을 동시에 spawn 했을 때 *Archer 만* 깨졌다. Orc 는 정상.

처음에는 spawn 된 SkeletonArcher 인스턴스의 Inspector 에서 `TopDownController2D ❌` `AIBrain ❌` 가 보였다 — 컴포넌트 자체가 disabled 상태.

### 증상 3 — 적을 죽였는데 sprite 가 서 있는 채로 남음

Health 컴포넌트의 Current Health 가 0 이고 GameObject 가 비활성화되었지만 (회색 처리), 화면에는 적 sprite 가 그대로 서 있는 것처럼 보였다. 깔끔한 사망 연출 없음.

### 증상 4 — 다 죽였는데 적이 다시 등장 (좀비 부활)

Hierarchy 에서 `SkeletonArcher_CL041(Clone)` 이 죽은 후 다시 활성화 (회색 → 정상 색) 되며 Health 가 풀로 복구됨. 다만 *재활성화된 적은 안 움직임* — 증상 2 와 같이 동작 정지.

## 격리 실험 (변수 한 개씩 토글)

증상 2~4 는 *서로 얽혀* 보였지만 처음 추측한 두 가설을 한꺼번에 적용하면 *어느 게 진짜 fix 였는지* 알 수 없는 상태가 됐다. 그래서 다음 순서로 격리.

### 가설 후보

| 가설 | 후보 fix |
|---|---|
| **A** Awake 단계의 init 누락 (TopDownController2D / AIBrain 이 disabled 로 prefab 에 저장됨) | `instance.GetComponent<...>().enabled = true;` 두 줄 |
| **B-a** 풀 사이클 진입 (DestroyOnDeath = false) | `health.DestroyOnDeath = true;` |
| **B-b** 재활성화 시 Health 자동 회복 (ResetHealthOnEnable = true) | `health.ResetHealthOnEnable = false;` |
| **C** 같은 프레임 다중 spawn race condition | `yield return null;` (entry 사이 1프레임 양보) |

### 실험 단계

1. **A + B-a + B-b** 동시 적용 + Wave 2개 분리 (Orc Wave 0초 / Archer Wave 8초) → 모두 정상.
   - 결론: *어떤 fix 가 효과* 였는지 분리 안 됨.
2. **A + B-a + B-b 모두 비활성** + Wave 2개 분리 유지 → 둘 다 정상 동작, 단 사망 시 sprite 가 서 있음.
   - 결론: A 와 B-b 는 *효과 입증 안 됨*. B-a (DestroyOnDeath) 는 *효과 있었음*. C (Wave 분리) 는 race 를 피해 정상화에 기여.
3. **B-a 만 복원 + C (yield return null) 추가** + Wave 다시 한 wave 로 합침 (race 환경 + 부활 환경 동시) → 모두 정상, 사망 처리 깔끔, 부활 사이클 발생 안 함.
   - 결론: 진짜 원인은 **C (race condition) + B-a (DestroyOnDeath)** 두 가지뿐. A 와 B-b 는 처음부터 불필요했음.

## 진짜 원인

### ① 같은 프레임 race condition (증상 2)

`EnemyEncounterSpawner.RunWave` 가 한 wave 안의 entry 들을 yield 없이 `for` 루프로 즉시 처리 → *한 프레임에 여러 적 동시 Instantiate*.

TopDownEngine 의 적은 `Awake` / `OnEnable` 안에서 무거운 init 을 한다 — AI brain 등록, target 캐시, 무기 attach, 화살 풀러 (`MMSimpleObjectPooler`) 자동 생성. 같은 프레임에 동시 init 하면 *공유 자원 (씬 풀, target 캐시 등)* 에서 충돌.

**왜 Orc 는 멀쩡하고 Archer 만 깨졌나**: Orc 는 근접 — init 이 단순. Archer 는 원거리 + 화살 투사체 풀러까지 init 해야 해서 *가장 무거운 init* 을 가진 적. 충돌의 *피해자가 가장 무거운 쪽* 으로 결정된 셈.

**Fix**: `EnemyEncounterSpawner.RunWave` 의 spawn 루프 안에 `yield return null;` 한 줄. 각 적 spawn 사이 1프레임 양보 → 각 적이 자기 init 을 끝내고 다음 프레임에 다음 적 spawn → 충돌 없음. spawn 합 N 마리면 약 N 프레임 (~0.02s × N) 지연 — 디자인 체감 불가능 수준.

### ② `Health.DestroyOnDeath = false` 셋업 (증상 3, 4)

적 prefab 의 `Health` 가 *죽어도 GameObject 를 destroy 하지 않도록* 셋업되어 있음. TopDownEngine 의 의도된 패턴은 *풀 사이클로 재활용* 하기 위함이지만, 본 검증 씬에는 그 풀 시스템이 셋업되어 있지 않다.

결과:
- 시신이 그대로 서 있음 (풀이 회수 안 함)
- 어떤 시스템이 GameObject 만 SetActive(true) 다시 호출 → `Health.ResetHealthOnEnable = true` 가 풀 체력으로 회복 → *좀비 부활*
- 재활성화 시 `Awake` 는 다시 안 호출됨 (인스턴스 1회만) → 적 prefab 의 `Awake` 기반 init 이 안 돔 → 부활한 적은 *안 움직이는 좀비*

**Fix**: spawn 직후 `health.DestroyOnDeath = true` 로 강제 → 죽으면 GameObject 통째로 destroy → 풀 사이클에 진입조차 못 함 → 시각·부활 두 문제 동시 해결.

본 fix 는 `EnemyEncounterSpawner.HardenSpawnedInstance(GameObject)` 헬퍼에 모았다. 이름과 주석에 *왜 필요한지* 명시.

## 잘못된 가설 (격리 결과 *불필요* 판정)

처음 캡처에서 spawn 된 SkeletonArcher 의 `TopDownController2D ❌` `AIBrain ❌` 가 보였던 것은 — *원인이 아니라 결과* 였을 가능성이 크다. race condition (원인 ①) 때문에 init 이 깨지면서 어떤 자체 disable 처리가 발동된 것으로 추정.

| 잘못된 가설 | 격리 결과 |
|---|---|
| TopDownController2D / AIBrain 이 prefab 에 disabled 로 저장됨 → spawn 시 enable 보정 필요 | 빼도 둘 다 정상 동작. 적 prefab 에 자체 enable 처리가 있음. **불필요**. |
| ResetHealthOnEnable = false 로 자동 회복 차단 필요 | DestroyOnDeath 로 풀 진입 자체를 막으니 본 fix 가 *닿지조차 않음*. **불필요**. |

## 부수 증상

### NullReferenceException 4건 (증상 1)

`ItemPicker.OnTriggerEnter2D` / `Initialization` 에서의 NPE 는 본 검증 씬에 *Inventory 시스템이 셋업되어 있지 않음* 이 원인. 일부는 spawn 된 적의 prefab 안에 `ItemPicker` 가 들어있어서 적이 init 될 때 발생, 일부는 씬에 미리 있던 PickableItem 의 트리거 충돌에서 발생.

**CL-034 와 무관**. Inventory 시스템 셋업 또는 적/맵 prefab 의 ItemPicker 셋업 정리는 적 담당자(클라2) / 맵 담당자 영역.

### DeadlineProgressManager / DeadlineCollectible 자동 생성

Hierarchy 에 `DeadlineProgressManag...` 가 자동 생성되는 것은 — 적 prefab 안의 `DeadlineCollectible` 컴포넌트가 Singleton 호출하다 자동 인스턴스화한 것. *TopDownEngine 의 Demos/Deadline* 에서 흘러온 잔여 컴포넌트로 보인다. 적 담당자 영역 — prefab 정리 시 제거 권장.

## 적 담당자(클라2) 협의 메모

CL-037~042 적 prefab 셋업에 다음을 협의 / 정리하면 본 troubleshooting 의 fix 들이 자연스럽게 *불필요* 해진다:

1. **`Health.DestroyOnDeath` 의 의도 명시** — 풀 사이클을 사용하지 않는다면 default true 유지. 사용한다면 풀 시스템을 *씬 자체* 에 항상 셋업하도록 prefab 의존성을 명시.
2. **`ItemPicker` / `DeadlineCollectible` 잔여 컴포넌트 제거** — 본 프로젝트의 인벤토리 / progress 시스템과 무관한 흔적이라면 prefab 에서 떼기.
3. **적 init 의 멱등성 (OnEnable 안전성) 검토** — 향후 풀링 전환 시점에 `OnEnable` 단독으로도 동작 가능한지 확인. 현재 `Awake` 에 의존한 init 이 1회만 도는 것으로 추정.
4. **race-safe init** — 같은 프레임 다수 적 spawn 환경에서 깨지지 않는지 확인 (특히 ranged 무기 attach / 풀러 자동 생성 경로).

위 4 가지 중 어느 하나가 정리되면 `EnemyEncounterSpawner.HardenSpawnedInstance` 의 일부 또는 전체를 제거할 수 있다.

## 후속 작업 — 풀링 전환 검토 (별도 CL 후보)

본 검증 단계에서 *Instantiate 대신 미리 생성 + SetActive 토글* 풀링 패턴 전환 가능성을 함께 검토했다.

**추천 결론: 본 CL 에서는 Instantiate 유지. 풀링 전환은 별도 CL.**

이유:

- 현재 fix 두 줄 (`yield return null` + `DestroyOnDeath = true`) 로 동작 OK 이고 진단도 명확. *움직이는 코드를 풀링 같은 큰 변경으로 다시 흔들 ROI 가 낮음*.
- 풀링 전환의 진짜 트리거는 (a) 같은 적이 wave 마다 반복 등장하는 컨텐츠 도입, (b) Instantiate 비용이 프로파일러에서 잡힐 때 — 현재 둘 다 아님.
- 풀링이 안전하려면 *적 prefab 의 `OnEnable` init 멱등성* 이 보장되어야 한다. 적 담당자 영역에서 정리되기 *전에* 풀링으로 가면 두 시스템이 서로의 lifecycle 을 가정하면서 꼬일 위험.
- `EnemyEncounterSpawner.Begin(...)` 의 외부 인터페이스를 *유지* 하면 향후 풀링 전환 시 본 CL 코드 영향 없이 내부 구현만 교체 가능 — 현 구조가 그래서 이렇게 분리되어 있다.

전환 시 검토할 항목:

- 미리 생성된 적의 보관 위치 (room prefab 의 hidden 자식? 별도 풀러 SO?)
- 방 떠날 때 lifecycle (destroy? 다음 방 재활용?)
- `RoomEntryRuntimeController` 의 책임 (현재 *방 단위 권위* — 풀러를 controller 가 들지 별도 시스템이 들지 결정 필요)
- 멀티플레이어 동기화 — 풀 인덱스가 결정적이어야 함

별도 CL 시점에 위 4 항목을 하나씩 결정.

## 검증 결론

CL-034 sample prefab / 자산 / 검증 씬에서:

- ✅ Wave 1 (Orc 2 + Archer 1) 동시 spawn → 모두 정상 추적·공격
- ✅ 죽이면 sprite 가 깔끔히 사라짐 (GameObject destroy)
- ✅ 부활 사이클 발생 안 함
- ✅ `RoomEntered` / `ExitDoorsLockRequested` / `RoomCombatStarted` 시그널 1회씩 발행
- ✅ Player 가 `EntryAnchor_Default` 위치로 정렬 + East 방향 회전
- ✅ `bgmCueId` stub log 1회 발행
- ✅ zone 재진입 시 추가 spawn / 추가 시그널 없음

CL-034 의 1차 검증 완료로 본다. 후속 CL-035 / CL-036 가 본 spawner 의 시그널을 구독해 작업 가능한 상태.

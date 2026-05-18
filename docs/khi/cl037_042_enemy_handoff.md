# CL-037~042 적 prefab 셋업 이슈 — 협업 문서 (적 담당자 hand-off)

## 문서 목적

CL-034~036 (방 진입·전투·클리어·문 흐름) 검증 중 *런타임 spawn 된 적* 에서 관찰된 이슈 집합을 적 담당자(클라2)에게 인계한다.

우리(khi) 측에는 임시 우회 fix 가 이미 적용되어 *Epic D 검증은 통과* 했지만, 그 fix 들은 *적 prefab 셋업이 정리되면 제거 가능* 하다 — 본 문서가 정리될 항목을 식별한다.

## 환경

- Unity 2D, TopDownEngine + DungeonArchitect.
- 검증 흐름:
  - `test_khi.unity` 또는 비슷한 검증 씬에 `CombatRoom_Sample_Small.prefab` 인스턴스 배치
  - zone 진입 → wave 자동 spawn → player 가 적과 전투 → 클리어 → 문 열림 → 다음 방
- 검증 적 prefab:
  - `Assets/_Project/Prefabs/Enemies/Orc_CL037.prefab`
  - `Assets/_Project/Prefabs/Enemies/OrcRider_CL039.prefab`
  - `Assets/_Project/Prefabs/Enemies/SkeletonArcher_CL041.prefab`
- 적은 `EnemyEncounterSpawner` (CL-034 산출물) 로 *런타임 Instantiate*. 씬에 미리 배치되지 않은 *복제 인스턴스*.

## 증상 정리

### 증상 1 — 공격 시 적 애니메이션 미동작

플레이어가 spawn 된 적을 공격해 *데미지는 정상 들어감* (Health 카운트 감소 OK), *죽이는 것도 정상* (DestroyOnDeath 우회 fix 적용 후). 다만 적 측의 **피격 애니메이션 / 사망 애니메이션** 이 재생되지 않음.

추정 원인:
- 적 prefab 의 Animator 가 `Health.OnHit` / `Health.OnDeath` 이벤트를 받지 않음
- CL-037 plan 의 `MeleeEnemyHitReaction.cs` 가 *미작성* 이라 Animator 연결 누락

영향: 게임 동작은 됨 (데미지·사망 카운트 정상). *시각적 피드백 없음* — UX 문제.

### 증상 2 — SkeletonArcher 의 화살이 직선으로만 발사

`SkeletonArcher_CL041` 이 player 를 *aim 하지 않음*. 화살이 *고정된 한 방향* (아마 spawn 시점의 facing) 으로만 발사됨.

추정 원인:
- `AIActionAimWeaponAtTarget2D` 의 `Target` 이 null 인 상태
- `AIDecisionDetectTargetRadius2D` 가 *씬 시작 시 1회* 만 player 를 캐시하는 셋업이면, *런타임 spawn 된* 인스턴스는 player 참조를 영영 못 잡음
- (이전 race condition / DestroyOnDeath 우회 fix 와 *별개 영역*. Aim Target 셋업 자체의 문제)

영향: 원거리 적이 무력화. *AI 작동 불완전*.

### 증상 3 — 콘솔 에러 / 경고 dump (3 종류)

zone 진입 후 다음 에러들이 *반복적으로* 떠 콘솔이 노이즈로 가득 참.

#### 3-1. `SerializedObjectNotCreatableException` / `MissingReferenceException`

```
SerializedObjectNotCreatableException: Object at index 0 is null
  UnityEditor.Editor.CreateSerializedObject ()
  ...
  MoreMountains.TopDownEngine.CharacterAbilityInspector.OnEnable ()

MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore.
  UnityEditor.GameObjectInspector.OnEnable ()
```

스택 끝이 `UnityEditor.Editor.OnEnable` — **TDE 자체의 Editor Inspector 결함**. Play 중 GameObject destroy / spawn 사이 Inspector 가 stale 참조를 들고 있다가 OnEnable 호출 시 NPE.

영향: **게임플레이 영향 0**. 콘솔 노이즈만.

대응: 무시 가능. Hierarchy 빈 곳 클릭 → 다시 클릭으로 정리. *적 담당자 측 fix 불필요* — TDE 업그레이드 시 자연 해소.

#### 3-2. `NullReferenceException` — ItemPicker / Inventory

```
NullReferenceException
  MoreMountains.InventoryEngine.Inventory.FindInventory (line 125)
  MoreMountains.InventoryEngine.ItemPicker.FindTargetInventory (line 242)
  MoreMountains.InventoryEngine.ItemPicker.Initialization (line 53)
  MoreMountains.InventoryEngine.ItemPicker.Start (line 45)
```

원인: 적 prefab 안에 `ItemPicker` 컴포넌트가 *잔재로 남음*. 본 검증 씬에 *Inventory 시스템이 셋업되어 있지 않아* `FindInventory` 가 null 반환 → NPE.

**영향: 매우 큼**. NPE 가 적의 `Start()` 단계에서 던져지면 *그 GameObject 의 후속 init (AI brain target 캐시, Animator 초기화, 무기 attach 등) 이 중단* 됨. **증상 1·2 의 간접 원인** 일 가능성이 큼 — *Start() 가 끝까지 완주 안 했기 때문에 Animator·Aim 셋업이 먹지 않은* 것으로 추정.

대응 우선순위: ⭐ 최우선. 적 prefab 에서 `ItemPicker` 제거 또는 비활성화.

#### 3-3. `NullReferenceException` — DeadlineProgressManager / DeadlineCollectible

```
NullReferenceException
  MoreMountains.TopDownEngine.DeadlineProgressManager.LoadSavedProgress (line 163)
  MoreMountains.TopDownEngine.DeadlineProgressManager.Awake (line 81)
  UnityEngine.GameObject:AddComponent ()
  MoreMountains.Tools.MMSingleton`1:get_Instance ()
  MoreMountains.TopDownEngine.DeadlineCollectible.DisableIfAlreadyCollected (line 36)
  MoreMountains.TopDownEngine.DeadlineCollectible.Start (line 20)
```

원인: 적 prefab 안에 `DeadlineCollectible` 컴포넌트가 *잔재로 남음*. 본 프로젝트는 *Deadline 데모* (TDE 샘플) 와 무관한데, 컴포넌트가 살아 있어 Singleton 자동 생성 시도 → NPE.

**영향**: 3-2 와 동일 메커니즘 (Start() init 중단).

대응 우선순위: ⭐ 최우선. 적 prefab 에서 `DeadlineCollectible` 제거.

## 적 담당자 측 권장 작업 (체크리스트)

| 우선순위 | 작업 | 영향 |
|---|---|---|
| ⭐ 최우선 | `ItemPicker` 컴포넌트를 Orc/OrcRider/SkeletonArcher prefab 에서 제거 | 증상 1, 2 의 *간접 원인* 해소 가능성. 콘솔 노이즈 제거 |
| ⭐ 최우선 | `DeadlineCollectible` 컴포넌트를 같은 prefab 들에서 제거 | 위와 동일 |
| 높음 | `Health.OnHit` / `Health.OnDeath` → Animator 파라미터 연결 (CL-037 plan 의 `MeleeEnemyHitReaction` 작성) | 증상 1 직접 해소 |
| 높음 | `AIActionAimWeaponAtTarget2D` 의 Target 이 *런타임 spawn* 인스턴스에서도 player 를 캐시하도록 검토 (SkeletonArcher 무기 셋업) | 증상 2 직접 해소 |
| 중 | `EnemyCombatReporter` 작성 (CL-037 plan 산출물) | 우리(khi) 측 `Health.OnDeath` 직접 구독 우회 정리 |
| 중 | `Health.DestroyOnDeath` 의 의도 명시. 풀 사이클 미사용 → default `true` 유지. 사용 → 씬 자체에 풀 시스템 항상 셋업 | 우리 `HardenSpawnedInstance` 우회 fix 제거 가능 |
| 낮 | 같은 프레임 다중 spawn 시 적 init 안전성 검토 (race-safe init) | 우리 `yield return null` 우회 fix 제거 가능 |

## 우리(khi) 측 임시 fix 현황

본 이슈들을 우회하기 위해 우리 측에 다음이 적용되어 있음. **적 prefab 정리 후 제거 예정**:

1. **`EnemyEncounterSpawner.HardenSpawnedInstance`** — `Assets/_Project/Scripts/Runtime/Combat/EnemyEncounterSpawner.cs`
   - spawn 직후 `health.DestroyOnDeath = true` 강제. 풀 사이클 진입 차단.
   - 적 prefab 의 `Health.DestroyOnDeath` 가 default `true` 로 정리되면 제거 가능.

2. **`EnemyEncounterSpawner.RunWave` 안 `yield return null`** — 같은 파일.
   - 한 wave 의 entry 사이 1프레임 양보. race condition 회피.
   - 적 prefab 의 init 이 race-safe 해지면 제거 가능.

3. **`AllEnemiesDefeatedTracker` 가 적의 `Health.OnDeath` 직접 구독** — `Assets/_Project/Scripts/Runtime/Stage/AllEnemiesDefeatedTracker.cs`
   - `EnemyCombatReporter` 가 미작성이라 우리 측 tracker 가 우회.
   - `EnemyCombatReporter` 도착 시 reporter 이벤트로 마이그레이션 가능. 단 *현재 동작도 정상* 이므로 필수 아님.

자세한 진단·격리 실험 과정은 [cl034_troubleshooting.md](cl034_troubleshooting.md) 참고.

## 검증 방법 — 적 prefab 정리 후

위 ItemPicker / DeadlineCollectible 잔재 제거만 적용하고 다음을 확인:

1. ▶ Play → zone 진입 → spawn 적이 등장.
2. **Console 에 NullReferenceException 이 *사라짐*** (Inventory / Deadline 두 종류 모두).
3. **(가능성 있음) 증상 1 (피격 애니메이션) / 증상 2 (화살 조준) 도 자연스럽게 해소** — Start() init 이 완주하면서.
4. 만약 증상 1·2 가 남는다면 그건 *Animator 트리거 연결* / *Aim Target 셋업* 의 별도 작업 영역.

## 참조 문서

- [cl034_room_entry_init_plan.md](cl034_room_entry_init_plan.md) — 방 진입 시 전투 시작 초기화 (CL-034)
- [cl034_troubleshooting.md](cl034_troubleshooting.md) — 검증 단계 트러블슈팅 (race / DestroyOnDeath 진단·격리 실험)
- [cl035_room_clear_judgement_plan.md](cl035_room_clear_judgement_plan.md) — 적 전멸 클리어 판정 (CL-035)
- [cl036_door_open_next_room_plan.md](cl036_door_open_next_room_plan.md) — 문 열림·다음 방 전환 (CL-036)
- [../cl037_cl038_melee_enemy_plan.md](../cl037_cl038_melee_enemy_plan.md) — 적 담당자 측 CL-037/038 plan
- [../cl039_cl040_charge_enemy_plan.md](../cl039_cl040_charge_enemy_plan.md) — CL-039/040 plan

## 작성 정보

- 작성: khi (Epic D 담당)
- 작성일: 2026-04-27
- 본 문서 갱신 트리거: 적 prefab 정리 후 검증 결과 (증상 1·2 해소 여부) 추가.

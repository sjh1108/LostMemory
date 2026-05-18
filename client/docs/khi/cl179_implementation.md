# CL-179 PlayerStatsData SO 정의 + base 값 마이그레이션 — 구현 기록

작성일: 2026-05-07

브랜치: `feat/S14P31C201-441/cl-179-player-stats-data-so-정의`

기준 plan: [cl179_plan.md](cl179_plan.md), 선행 ticket 구현 기록: [cl178_implementation.md](cl178_implementation.md) (흡수 기록 — CL-179 와 동일 브랜치에 함께 진행)

**상태**: 🟢 **검증 완료** — 컴파일 OK / Khi_Stats.asset 신설 + 4필드 입력 / Save Current Values ContextMenu 발화 / Khi 회귀 0 (게임 미반영 정상). 사용자 "1, 2 둘다 했어" 확인.

---

## 목적

캐릭터 base 능력치 (MoveSpeed/MaxHealth/DashCooldown) 통합 SO 신설. master plan §9 Q1 의 보류 결정이었으나 사용자 결정으로 진행. **시나리오 B 패턴** (cl172 모방) — SO 정의 + 인스턴스만, **코드 영향 0**. 적용 어댑터는 CL-180 (별도 ticket, 보류 유지).

**해결되는 문제**:
- TDE 컴포넌트 Inspector 인라인 + KhiDashController 코드 등 흩어진 base 값을 SO 1개로 모음
- CL-181 (PlayerStatsCategoryProvider) 진입을 위한 SO 토대 마련
- 디자이너가 향후 캐릭터별 (`Misonyo_Stats.asset` 등) 변경 시 동일 SO 클래스 재사용 가능

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항 (cl179_plan.md 그대로)

- **시나리오 B 패턴 (cl172 모방)** — 코드 영향 0. 적용 어댑터는 CL-180 (보류)
- **필드 3개 (사용자 결정 a)** — MoveSpeed / MaxHealth / DashCooldown + DisplayName. 점프 placeholder 미추가 (Khi 미사용)
- **namespace** = `LostMemory.Combat`
- **파일 위치** = `Runtime/Combat/PlayerStatsData.cs`
- **PlayerStatsDataEvents 정적 hook** — cl172 의 EnemyDataEvents 패턴 모방. CL-180 어댑터 진입점
- **인스턴스 파일명** = `Khi_Stats.asset` (cl172 의 `Bertha_Boss.asset` 패턴)
- **인스턴스 위치** = `Assets/_Project/ScriptableObjects/Player/` (사용자 신설)
- **CreateAssetMenu** = `LostMemory/Player/Player Stats Data`
- **Save Current Values ContextMenu** = `#if UNITY_EDITOR` + `EditorUtility.SetDirty` + `AssetDatabase.SaveAssetIfDirty` + `PlayerStatsDataEvents.RaiseAssetSaved`

### 작업 중 사용자 결정 사항

- **plan 그대로 진행** — 추가 결정 없음. 사용자 결정 기록은 plan 단계에서 마무리
- **CL-178 정리 작업과 동일 브랜치 진행** — `feat/S14P31C201-441/cl-179-...` 브랜치에 CL-179 코드 + CL-178 doc + draft 비활성 보관 (`#if false`) 함께 묶음. 본인 단독 작업 + Jira 미등록이라 통합 MR 권장 (cl178_implementation §메모)

### 작업 중 발견 사항

- **★ TestKhi prefab 의 실제 base 값 ↔ plan 기본값 차이** — cl179_plan §2 의 SerializeField 기본값:
  - `_baseMoveSpeed = 7f` / `_baseMaxHealth = 100f` / `_baseDashCooldown = 1.5f`

  실제 TestKhi prefab (Net_AD / MinimalCharacter2D 둘 다 동일):
  | Khi_Stats 필드 | TestKhi prefab 값 (실제) |
  |---|---|
  | BaseMoveSpeed | **6** (CharacterMovement2D.WalkSpeed — `MovementSpeed` 가 아닌 `WalkSpeed`) |
  | BaseMaxHealth | **100** |
  | BaseDashCooldown | **0.8** (KhiDashController.Cooldown.ConsumptionDuration) |

  → SerializeField 기본값은 그대로 유지 (신규 SO 생성 시 default). 디자이너는 Khi_Stats.asset 신설 후 위 실제 값 (6/100/0.8) 으로 덮어씀. **검증 단계에 사용자가 정확히 입력 완료**.

- **★ plan §1.3 의 "MoveSpeed | CharacterMovement.MovementSpeed" 표기 부정확** — 실제 직렬화 필드명은 `WalkSpeed`. TDE CharacterMovement2D 가 `WalkSpeed` 를 base 로 사용 (`MovementSpeed` 는 multiplier 포함 현재값). 향후 디자이너 안내 시 `WalkSpeed` 라고 정정 권장. cl179_plan.md 갱신 (사용자 영역).

- **★ DashCooldown 출처 = KhiDashController** (CharacterDash2D 상속) — plan §1.3 에 "CharacterDash2D.Cooldown.ConsumptionDuration" 으로 표기됐으나 정확히는 KhiDashController 가 CharacterDash2D 를 상속한 컴포넌트로서 Cooldown 보유. prefab YAML 의 GUID `9f47b34d4ea84fbfba2cb67c6db59740` 매칭 = `KhiDashController.cs.meta`. 동작 차이 X — 표기만 명확화.

- **TestKhi prefab 2개 동일 값** — Net_AD / MinimalCharacter2D 둘 다 6 / 100 / 0.8. cl179_plan §위험 #5 의 "두 prefab 의 Inspector 값이 다르면 디자이너 결정 필요" 는 본 시점엔 무관. 향후 prefab 분기 발생 시 재검토.

---

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 시나리오 패턴 | B (cl172 모방, 코드 영향 0) | 회귀 위험 0, CL-180 어댑터로 분리 |
| 필드 수 | 3개 + DisplayName | 사용자 결정 (점프 미사용) |
| namespace | `LostMemory.Combat` | StatId / Container / Applier 동일 ns |
| 코드 위치 | `Runtime/Combat/PlayerStatsData.cs` | 코드 도메인 (Combat) |
| asset 위치 | `Assets/_Project/ScriptableObjects/Player/` | asset 카테고리 (Player) |
| asset 파일명 | `Khi_Stats.asset` | `<캐릭터>_<종류>` 컨벤션 |
| Events hook | `PlayerStatsDataEvents.OnAssetSaved` 정적 클래스 | cl172 EnemyDataEvents 패턴 |
| Save 패턴 | `[ContextMenu("Save Current Values")]` + `EditorUtility.SetDirty` + `SaveAssetIfDirty` + `RaiseAssetSaved` | cl172 / WeaponData 패턴 일관 |
| Editor 의존 격리 | `#if UNITY_EDITOR` ContextMenu 만 | Runtime → Editor 어셈블리 직접 참조 회피 |
| Min(0f) 클램프 | 3개 float 필드 모두 | 음수 입력 방지 |
| 점프 처리 | 미포함 | Khi 탑다운 + 디자이너 혼동 회피 |
| CreateAssetMenu order | 20 | LostMemory/Player/ 하위 정렬 |

---

## 수정 파일

### 신규 (Claude — 1)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerStatsData.cs` | `PlayerStatsDataEvents` 정적 클래스 + `PlayerStatsData` SO 클래스 (3 필드 + DisplayName + Save ContextMenu) |

### 신규 (사용자 Unity Editor)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/ScriptableObjects/Player/Khi_Stats.asset` (+ .meta) | Khi 캐릭터 base 값 인스턴스. DisplayName=Khi / BaseMoveSpeed=6 / BaseMaxHealth=100 / BaseDashCooldown=0.8 |

### 무수정 (참고)

- `Runtime/Combat/StatId.cs` / `PlayerStatModifierContainer.cs` — multiplier 시스템 (본 CL 외)
- `Runtime/Combat/PlayerHealthStatApplier.cs` / `PlayerMovementStatApplier.cs` — base 값 SO 참조 전환은 CL-180
- `Runtime/TestKhi/KhiDashController.cs` — base 값 SO 참조 전환은 CL-180
- `Prefabs/Characters/TestKhi_Net_AD.prefab` / `TestKhi_MinimalCharacter2D.prefab` — Inspector 값 무수정 (CL-180 진입 시에도 어댑터 패턴으로 prefab 무수정 가능성 검토)

### 동일 브랜치 함께 변경 (CL-178 정리)

| 경로 | 변경 |
|---|---|
| `Runtime/Enemies/BerthaBossDataInjector.cs` (+ .meta) | `#if false` 비활성 보관 — 헤더에 사유 + 활성화 절차 |
| `Runtime/Enemies/BossDataRegistry.cs` (+ .meta) | `#if false` 비활성 보관 — 짝 클래스 안내 |
| `docs/khi/cl178_implementation.md` | CL-178 흡수 기록 (CL-197/198/199 처리) + draft 비활성 사유 |

> CL-178 은 본인 단독 작업 + Jira 미등록 → CL-179 와 통합 MR 권장 (cl178_implementation §메모).

---

## 발견·해소된 이슈

### 1. Plan §1.3 의 prefab 필드명 부정확 (정정 권장)

**문제**: cl179_plan §1.3 line 51-54 의 "MoveSpeed | CharacterMovement.MovementSpeed" 표기.

**실제**: TDE CharacterMovement2D 가 base 로 사용하는 직렬화 필드는 `WalkSpeed` (line 414 prefab YAML). `MovementSpeed` 는 multiplier 포함 현재값. 직렬화 필드 = `WalkSpeed`.

**해소**: 코드 동작 영향 X — 본 CL 의 PlayerStatsData 가 prefab 의 직렬화 필드를 직접 참조하지 않음 (디자이너가 Inspector 값을 보고 SO 에 입력). 표기만 정정 권장. cl179_plan.md 갱신은 사용자 영역.

### 2. DashCooldown 출처의 정확한 컴포넌트

**문제**: cl179_plan §1.3 의 "CharacterDash2D.Cooldown.ConsumptionDuration" 표기.

**실제**: TestKhi prefab 에 부착된 컴포넌트는 `KhiDashController : CharacterDash2D`. Cooldown 은 KhiDashController 가 보유 (상속받음). prefab YAML GUID `9f47b34d4ea84fbfba2cb67c6db59740` = `KhiDashController.cs.meta`.

**해소**: 동작 차이 X — 상속 관계라 base.Cooldown 동일. 표기만 명확화.

### 3. (CL-178 정리) draft 파일 처리 정책

**문제**: untracked 로 남아 있던 `BerthaBossDataInjector.cs` / `BossDataRegistry.cs` 가 CL-197/199 와 진입점 중복.

**해소**: 사용자 결정 (2026-05-07) 으로 **삭제 대신 `#if false` 비활성 보관**. 컴파일 영향 0 + CL-180 진입 시 패턴 참조 가능. 자세한 사유: cl178_implementation.md.

---

## 검증 결과

### 1. CS 빌드 ✅

- `PlayerStatsData.cs` 컴파일 OK (콘솔 빨간 에러 0)
- 다른 코드 무수정 — Grep `PlayerStatsData` / `PlayerStatsDataEvents` 검색 시 본 신규 파일 1곳만 매치 (구독자 없음 — CL-180 진입 전이라 정상)
- `#if false` 로 감싼 CL-178 draft 도 컴파일 영향 0

### 2. Unity Editor 동작 ✅

- Create > LostMemory > Player > Player Stats Data 메뉴 표시
- `Assets/_Project/ScriptableObjects/Player/Khi_Stats.asset` 신설 → Inspector 모든 필드 (DisplayName / BaseMoveSpeed / BaseMaxHealth / BaseDashCooldown) 편집 가능
- 디자이너 입력값: DisplayName=Khi / BaseMoveSpeed=6 / BaseMaxHealth=100 / BaseDashCooldown=0.8 (TestKhi prefab Inspector 값과 정확 일치)

### 3. ★ Save Current Values ContextMenu ✅

사용자 "1, 2 둘다 했어" 확인:
- Inspector ⫶ → Save Current Values 클릭 → 콘솔 `[PlayerStatsData] Saved: Khi_Stats` 메시지 발화
- `PlayerStatsDataEvents.OnAssetSaved` 발화 정상 (구독자 0이라 발화만, CL-180 진입 시 어댑터 라이브 튜닝 진입점)

### 4. ★ Play 모드 회귀 없음 ✅

사용자가 Play 모드 진입 후 던전 빌드 → Khi 이동 → 적 공격 → RoomCleared 까지 정상 동작 확인 (콘솔 로그 캡처 검증):
- `[PlayerDamageReceiver] OnEnable — wiring: health=OK ... container=OK` (host=TestKhi_MinimalCharacter2D)
- `[MagicalGirlSpawner] OnEnable — wiring: anchor=self, playerStat=OK, playerCombat=OK, inventory=OK, playerAim=OK`
- `[BuildManager] OnEnable — inventory 구독 시작`
- `[RunManager] None -> Initializing -> InRun`
- `[KhiMeleeComboController.RunAttack] → KhiMeleeHitbox.Sample → Health.Kill()` (적 처치)
- `[Controller] RoomCleared: roomId='room_combat_small_sample'`

→ Khi_Stats.asset 의 SO 값은 **게임 미반영** 상태 (CL-180 어댑터 전까지 정상). prefab Inspector 값 그대로 동작.

### 미검증 (CL-180 영역)

- Khi_Stats.asset 의 BaseMoveSpeed/BaseMaxHealth/BaseDashCooldown 변경 → 게임 반영 (CL-180 어댑터 진입 후)
- 라이브 튜닝 (Save Current Values 시 게임 즉시 반영) (CL-180 어댑터 진입 후)
- Balance Editor 좌측 트리에 Player Stats 카테고리 표시 (CL-181 진입 후)

---

## 위험 / 결정 미정

### 위험

1. **데이터-코드 비동기**: Khi_Stats.asset 변경해도 게임 미반영 — CL-180 어댑터 전까지. cl172 §위험 #1 동일. 디자이너 안내 필요 ("값 입력 가능, 게임 반영은 CL-180 이후").
2. **SO 값 ↔ prefab Inspector 값 차이**: 본 시점엔 무관 (SO 게임 미반영). CL-180 진입 시 SO 우선 정책 명시 — TestKhi prefab Inspector 값을 SO 가 덮어쓰는 정책 권장.
3. **CL-180 회귀 위험 大**: master plan §9 Q1 보류 근거 (적용 어댑터 실행 타이밍 / TDE 초기화 / NGO 동기화). 본 CL 자체는 안전.
4. **TestKhi prefab 2개 동일 값** — 현재 OK. 향후 prefab 분기 시 cl179_plan §위험 #5 재검토.
5. **OnAssetSaved 발화 시점** — ContextMenu Save Current Values 만. CL-181 (Provider 등록) 후 Balance Editor 일반 저장 (Ctrl+S) 에서도 발화시킬지 별도 결정 (cl181_plan §위험 #3 참조).
6. **다른 캐릭터 인스턴스 없음** — Khi 만. 미소녀 등 추가 시 별도 인스턴스. SO 클래스는 generic.
7. **legacy WalkSpeed 표기** — cl179_plan.md 의 "MovementSpeed" 표기는 정정 권장 (사용자 영역).

### 결정 미정 (본 CL 외)

- [ ] CL-180: PlayerStatsApplier 정리 + base × multiplier 통합 (보류 유지)
- [ ] CL-181: PlayerStatsCategoryProvider — Balance Editor 노출 ([cl181_plan.md](cl181_plan.md) 작성됨, 진입 가능)
- [ ] master plan epic_uv ticket 시트 line 67 의 CL-181 의존 = "CL-180" → 사실상 CL-179 의존. 시트 갱신 권장 (사용자 영역)
- [ ] CL-180 진입 결정 시점에 어댑터 패턴 결정 (Applier 직접 수정 / 정적 Injector / Binding 컴포넌트 — cl178_implementation.md §"패턴 재활용 가능성" 참조)

---

## 후속 인계

| Ticket | CL-179 와의 관계 |
|---|---|
| **CL-180** PlayerStatsApplier | 본 CL 의 SO base 값을 Applier / KhiDashController 가 참조하도록 전환. 회귀 위험 大 (master plan §9 Q1). cl178 의 정적 Injector 패턴 / Applier 직접 수정 / Binding 컴포넌트 중 결정 필요 |
| **CL-181** PlayerStatsCategoryProvider | Balance Editor 좌측 트리에 Player Stats 카테고리 노출. cl181_plan.md 작성됨. CL-180 보류 무관하게 본 CL 직후 진입 가능 |
| **다른 캐릭터 추가 ticket** (미소녀 등) | PlayerStatsData 클래스 재사용. 새 인스턴스 (`Misonyo_Stats.asset` 등) 작성 |
| **PlayerStatsData 상속 도입 시** (캐릭터별 SO 변형) | cl173 의 EnemyData/BossData 패턴 적용 — `GetType().Name` 필터 + 카테고리 분리 (현재는 단일 클래스 → 필터 불필요, cl181_plan §1.5) |
| **cl179_plan.md 표기 정정** | §1.3 의 "MovementSpeed" → "WalkSpeed" + "CharacterDash2D.Cooldown" → "KhiDashController.Cooldown" 갱신 권장 (사용자 영역) |

---

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| PlayerStatsData.cs 작성 | 15분 | 약 5분 (plan 코드 그대로) |
| 컴파일 / 검증 (Claude) | 5분 | 약 5분 (Grep / git status) |
| (추가) CL-178 정리 — draft 비활성 + cl178_implementation.md 작성 | (plan 외) | 약 15분 |
| 사용자 Unity Editor — 폴더 + asset + 값 입력 + ContextMenu + Play 검증 | 10분 | 약 15분 (Play 모드 회귀 검증 포함) |
| **합계** | **약 30분** | **약 40분** |

CL-179 자체는 plan §예상 (30분) 일치. CL-178 정리 작업 (`#if false` 비활성 + implementation doc) 추가 분량 약 15분. cl172 (3점, 40분) 와 비슷.

---

## 메모

- 본 CL 자체 **회귀 위험 0** (코드 영향 X). CL-180 어댑터 진입 시점에 회귀 위험 大 (master plan §9 Q1)
- master plan §9 Q1 의 보류 근거 (회귀 / NGO 동기화 / TDE 초기화 등) 는 **CL-180 진입 결정 시점에 재검토**. 본 CL 진행해도 CL-180 보류 유지 가능
- cl178 의 정적 Injector + Registry SO 패턴이 CL-180 에서도 유효 (PlayerStatsRegistry + PlayerStatsInjector). cl178_implementation §"패턴 재활용 가능성" 참조 — 본 CL 시점에 draft 비활성 보관 결정으로 코드도 보존
- 디자이너 안내:
  - Khi_Stats.asset 의 값은 **현재 게임 미반영** (CL-180 진입 전까지). cl172 의 Bertha_Boss.asset 와 동일 상태
  - CL-181 진입 후 Balance Editor 의 Player Stats 카테고리에서 편집 가능
  - 라이브 튜닝: ContextMenu "Save Current Values" → CL-180 어댑터가 즉시 반영 (CL-180 진입 후)
- master plan epic_uv ticket 시트 의 CL-179/180/181 의존 표기 갱신 권장 (사용자 영역, cl173 / cl181 §메모 정책 동일)
- 본 CL 완료 후 다음 진입은 **CL-181** (Balance Editor Provider 등록) 권장 — cl181_plan.md 이미 상세, ~30분

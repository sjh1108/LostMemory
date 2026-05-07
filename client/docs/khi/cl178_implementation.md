# CL-178 — EnemyData/BossData 적용 어댑터: 별도 구현 X (이용호 영역 흡수)

작성일: 2026-05-07

브랜치: `feat/S14P31C201-441/cl-179-player-stats-data-so-정의` (CL-179 진행 중 정리 작업으로 함께 처리)

기준 plan: 본 ticket 은 김회인의 별도 plan 미작성. 이용호의 CL-197/198/199 가 흡수해 처리.

**상태**: 🟢 **흡수 완료** — 김회인 측 신규 코드 0. 이용호 머지 3건이 시트 정의의 모든 acceptance 충족.

---

## 결론

CL-178 시트 정의 = "EnemyData / BossData 적용 어댑터 — 게임 내 사용 (SO 값을 Bertha / 일반 몹 컴포넌트에 주입. 일반 몹 등장 시점에 매칭)" — master plan epic_uv §2 ticket 시트 line 64.

→ **이용호의 3개 ticket 으로 완전히 처리됨**. 김회인 별도 plan / 코드 작성 X. 본 doc 은 사유 영속 기록.

---

## 흡수 구조 (3중)

| Ticket | Jira | 머지 커밋 | 작업 |
|---|---|---|---|
| **CL-197** | S14P31C201-435 | `5a99f30af` (merge) / `a2e12e680` (소스) | Bertha BossData 런타임 적용 — `BerthaLightAttack1Bootstrap.cs` +107줄 + `BerthaRoot.prefab` + `Bertha_Boss.asset` wiring |
| **CL-198** | S14P31C201-436 | `136ea2886` (merge) / `f55f64ca3` (소스) | EnemyData ↔ EnemyCatalog/Spawner 런타임 연동 — `EnemyDataRuntimeAdapter.cs` 신설 (+331줄) + `EnemyData_*.asset` 5개 (Moose1/Orc/OrcRider/SkeletonArcher/StoneGolem) + `EnemyCatalog.cs` / `EnemyEncounterSpawner.cs` / `EnemyData.cs` 수정 |
| **CL-199** | S14P31C201-437 | `21ed55cef` (merge) / `0187e9a20` (소스) | Balance Editor Enemy/Boss 라이브 튜닝 — `EnemyDataLiveTuneHook.cs` 신설 (+52줄) + `BalanceEditorWindow.cs` +33줄 + Bertha 라이브 적용 (`BerthaLightAttack1Bootstrap.cs` +54줄, `EnemyDataRuntimeAdapter.cs` +34줄) |

→ CL-178 시트 acceptance ("SO → 컴포넌트 주입" + "일반 몹 매칭" + "라이브 튜닝") 모두 위 3개로 충족. cl172 / cl173 (SO 정의 + Provider) 위에 적용 layer 가 이용호 영역으로 정착.

---

## 김회인 draft — 비활성 보관 (`#if false`)

본 doc 작성 시점에 untracked 로 남아 있던 draft 2개. 삭제 대신 **`#if false` 블록으로 감싸서 비활성 보관**:

| 파일 | 내용 | 비활성 사유 |
|---|---|---|
| `Runtime/Enemies/BerthaBossDataInjector.cs` | `[RuntimeInitializeOnLoadMethod]` + `EnemyDataEvents.OnAssetSaved` 구독 + `BerthaBossPhaseController.Configure(...)` 호출 | CL-197 이 다른 패턴 (`BerthaLightAttack1Bootstrap` 직접 수정) 으로 처리. Injector 진입점 중복 |
| `Runtime/Enemies/BossDataRegistry.cs` | `[CreateAssetMenu]` `BossData[] _entries` registry SO | CL-197 이 prefab/asset 직접 wiring 으로 처리. 별도 registry SO 불필요 |

처리:
```csharp
#if false
// 기존 코드 그대로
#endif
```

각 파일 헤더에 비활성 사유 주석 + 활성화 절차 명시.

### 비활성 결정 근거

1. **진입점 중복** — `BerthaLightAttack1Bootstrap` (CL-197) 이 이미 Bertha 인스턴스에서 BossData 값을 직접 적용. Injector 가 별도로 `Configure()` 를 또 호출하면 동일 동작 2번 실행 또는 충돌
2. **이용호 영역 침범 가능성** — Bertha 컨트롤러 / Bootstrap 은 이용호 영역. Injector 가 `FindAnyObjectByType<BerthaBossPhaseController>()` 로 접근 → 이용호의 디버깅 흐름과 꼬일 위험
3. **PlayerStats 영역 단순 카피 불가** — Bertha 의 `Configure(health, p2, p3, debug)` 시그니처는 BossData 전용. PlayerStats 의 적용 대상 (CharacterMovement / Health / CharacterDash2D) 은 호출 방식 자체 다름 — Injector 패턴 자체는 보편적이지만 본 코드는 재활용 X
4. **삭제 대신 보관 선택** — 사용자 결정 (2026-05-07). `#if false` 로 컴파일 영향 0 + 향후 활성화 절차 명시. 패턴 포인터 ([cl179_plan.md §메모 line 340](cl179_plan.md)) + 실코드 둘 다 유지 → CL-180 진입 시 참조 자료 풍부

### 활성화 절차 (필요 시)

1. `BerthaBossDataInjector.cs` / `BossDataRegistry.cs` 의 `#if false` → `#if true` 변경 (또는 `#if false ... #endif` 전체 제거)
2. Unity Editor: Create → LostMemory → Enemies → Boss Data Registry → `Resources/BossDataRegistry.asset` 신설
3. asset 의 `_entries` 에 `Bertha_Boss.asset` 추가
4. ⚠️ **CL-197 의 `BerthaLightAttack1Bootstrap.ApplySavedBossData` 와 이중 Configure 호출 발생** — 활성화 전 정책 결정 (한쪽 비활성 / 분기 / merge)

---

## CL-180 진입 시 패턴 재활용 가능성

CL-180 (PlayerStatsApplier 정리 + base × multiplier 통합) 은 master plan §9 Q1 의 **보류 상태** 유지. 진입 시 적용 패턴 선택지:

| 옵션 | 패턴 | 장단점 |
|---|---|---|
| (a) Applier 직접 수정 | `PlayerHealthStatApplier.Awake` 에서 `PlayerStatsData` 참조 + 인라인 값 덮어쓰기 | 단순. prefab 수정 필요 (SO 참조 필드) |
| (b) 정적 Injector + Registry SO | 본 비활성 보관 draft 패턴. `[RuntimeInitializeOnLoadMethod]` + `Resources.Load<PlayerStatsRegistry>` + 컴포넌트 검색 + 적용 | prefab 무수정. Resources 폴더 + Registry SO 필요. 초기화 타이밍 보장 |
| (c) ScriptableObject 보유 컴포넌트 | TestKhi prefab 에 `PlayerStatsBinding : MonoBehaviour { [SerializeField] PlayerStatsData _stats; void Awake() {...} }` 추가 | prefab 1회 수정. 명시적 / 디버깅 쉬움 |

→ CL-180 진입 결정 시점에 cl179_plan §메모 line 340 / 본 doc / NGO 동기화 / TDE 초기화 타이밍 종합 검토. 본 CL 시점엔 결정 X.

---

## 메모

- 김회인 별도 코드 0. cl178_plan.md 미작성 (본인 영역 X). 본 implementation doc 단독으로 결론 영속화
- master plan epic_uv §0 / §11 line 354-355 / §C 의 CL-178 표기 갱신은 사용자 영역 (cl173 / cl181 §메모 정책 동일). 권장: "이용호 흡수 (CL-197/198/199)" 또는 ~~CL-178~~ 취소선
- Jira S14P31C201 의 CL-178 ticket 상태는 사용자 영역. 권장: "Closed as Duplicate of CL-197/198/199" 또는 동등
- 흡수 ticket 들의 1차 근거: CL-197 (`a2e12e680`) / CL-198 (`f55f64ca3`) / CL-199 (`21ed55cef`) 의 git log + 시트 line 64 (CL-178 정의)
- CL-180 진입 시 본 doc + cl179_plan §메모 line 340 + master plan §9 Q1 종합 — Injector / Applier 직접 수정 / Binding 컴포넌트 중 결정. CL-180 자체는 보류 유지

---

## 후속

| Ticket | 본 doc 와의 관계 |
|---|---|
| **CL-179** (PlayerStatsData SO) | 본 doc 와 같은 브랜치 / 같은 정리 작업 흐름. cl179_plan.md 그대로 진행 |
| **CL-180** (PlayerStatsApplier 정리) | 보류. 진입 시 본 doc §"패턴 재활용 가능성" 참조 |
| **CL-181** (PlayerStatsCategoryProvider) | cl181_plan.md 작성됨. CL-180 보류 무관하게 CL-179 직후 진입 가능 |
| **추가 일반 몹 EnemyData 추가** | 이용호 영역. CL-198 의 EnemyDataRuntimeAdapter 패턴 그대로 신규 asset 추가만으로 동작 |
| **추가 보스 BossData 추가** | 이용호 영역. CL-197 의 Bootstrap 직접 적용 패턴 또는 EnemyDataRuntimeAdapter 확장 검토 |

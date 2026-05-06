# CL-172 EnemyData / BossData SO + Bertha 인스턴스 — 구현 기록

작성일: 2026-05-06

브랜치: `feat/S14P31C201-407/cl-172-enemy-data-boss-data-so`

기준 plan: [cl172_plan.md](cl172_plan.md), 후속 ticket: [cl173_plan.md](cl173_plan.md)

**상태**: 🟢 **검증 완료 (구조)** — EnemyData / BossData 클래스 컴파일 OK, Bertha_Boss.asset 인스펙터 정상,
OnValidate phase3 ≤ phase2 클램프 작동 확인. **실제 운영값 입력 (Display Name / MaxHealth / MoveSpeed / Phase3=0.3) 은 사용자 후속 작업** (Bertha Prefab 값 파악 필요).

---

## 목적

Enemy 트랙 첫 ticket. EnemyCatalog.cs (CL-034) 주석에 예약돼 있던 "EnemyData SO 도입" 의 진입점 클래스 신설. CL-166(데이터 카테고리 연결) 완료 후, CL-173(Provider 등록) 의 의존 클래스를 본 CL 에서 마련.

**시나리오 B — 코드 전환 X**: 클래스 + Bertha 인스턴스만 생성. 기존 BerthaBossPhaseController / EnemyCatalog 코드 무수정. 실 어댑터 연결은 CL-180.

**해결되는 문제**:
- `EnemyCatalog.cs` 21~22줄 주석의 SO 도입 예약 해소
- CL-173 의 Balance Editor Provider 등록을 위한 `t:EnemyData` / `t:BossData` 필터 진입점 마련
- Bertha 의 phase threshold / 기본 스탯 값을 SO 로 분리할 토대

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항
- **BossData : EnemyData 상속** — 공통 스탯(displayName/maxHealth/moveSpeed/expReward/dropWeight)을 base, phase 임계값만 BossData 에 추가
- **필드 범위**: attackDamage / detectionRange 등은 본 CL 제외 (Bertha 공격 6종 이상 → 단일 값 어색, AI 트랙 보류)
- **namespace**: `LostMemory.Enemies` (기존 Enemies 폴더 컨벤션 일치)
- **expReward / dropWeight 포함**: 시스템 미존재 상태에서 데이터만 선행. CL-180 이후 어댑터/보상 시스템 ticket 에서 연결

### 작업 중 사용자 결정 사항
- **CreateAssetMenu menuName 컨벤션 = `LostMemory/Enemies/Enemy Data` / `LostMemory/Enemies/Boss Data`**
  - 근거: 사용자 요청 — Enemy 트랙 담당자가 헷갈리지 않게 Enemies 카테고리로 통일. 클래스 namespace(`LostMemory.Enemies`) 와 일치.
  - 대안 (doc 명세 그대로 `LostMemory/Enemy Data`) 는 prefix 없어 다른 SO 와 분류 어색.
- **fileName 속성 = `EnemyData` / `BossData`** (suffix 없음)
  - 근거: doc 명세 그대로. WeaponData 의 `WeaponData_New` suffix 패턴 미채택.
- **WeaponData Save 패턴 채택 = 본 CL 에 함께 추가**
  - 근거: 사용자 요청. `[ContextMenu("Save Current Values")]` + `EnemyDataEvents.OnAssetSaved` 정적 이벤트.
  - 현재 Runtime 구독자는 없음 (CL-180 어댑터에서 추가 예상). 이벤트 미구독 상태에서 무해.

### 작업 중 발견·결정 사항
- **BossData.SaveCurrentValues 메서드명 충돌 회피**: base(EnemyData) 의 `private SaveCurrentValues()` 와 동명 메서드 정의 시 컴파일 가능하나 의도 불명확 → BossData 는 `SaveBossCurrentValues()` 로 분리. **`[ContextMenu]` 라벨은 동일 "Save Current Values"** (사용자 UX 일관성).
- **EnemyDataEvents 위치**: WeaponDataEvents 가 WeaponData.cs 와 같은 파일에 정의된 패턴 그대로 → EnemyData.cs 안에 정의. 별도 파일 분리하지 않음.
- **BossDataEvents 별도 클래스 불필요**: BossData 는 EnemyData 이므로 `EnemyDataEvents.RaiseAssetSaved(this)` 호출로 충분. 구독자가 타입 분기 필요하면 `if (asset is BossData boss)` 패턴.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 클래스 상속 | BossData : EnemyData | 공통 스탯 재사용, Balance Editor 카테고리 분리 표시 |
| namespace | `LostMemory.Enemies` (둘 다) | 기존 Enemies 폴더 컨벤션 |
| menuName | `LostMemory/Enemies/Enemy Data` / `LostMemory/Enemies/Boss Data` | Enemy 담당자 분류 명확성 |
| fileName | `EnemyData` / `BossData` (suffix 없음) | doc 명세 |
| 필드 (EnemyData) | displayName, maxHealth, moveSpeed, expReward, dropWeight | doc 명세 (Bertha 기본 스탯 + 보상 데이터 선행) |
| 필드 (BossData 추가) | phase2ThresholdNormalized, phase3ThresholdNormalized | BerthaBossPhaseController 동일 값 |
| 접근자 패턴 | `[SerializeField] private _xxx` + `public Xxx => _xxx` | WeaponData 컨벤션 |
| OnValidate 보정 | `_phase3 = Mathf.Min(_phase3, _phase2)` | BerthaBossPhaseController 동일 로직 |
| Save 패턴 | `[ContextMenu]` + `EnemyDataEvents.OnAssetSaved` 정적 이벤트 | WeaponData 패턴 동일 |
| Save 메서드명 (BossData) | `SaveBossCurrentValues()` | base 동명 메서드 분리 |
| EnemyDataEvents 위치 | EnemyData.cs 내부 | WeaponDataEvents 패턴 |

---

## 수정 파일

### 신규 (Claude — 2)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyData.cs` | `EnemyDataEvents` 정적 이벤트 클래스 (OnAssetSaved) + `EnemyData : ScriptableObject` (5개 필드, getter, ContextMenu Save) |
| `LostMemory/Assets/_Project/Scripts/Runtime/Enemies/BossData.cs` | `BossData : EnemyData` (phase2/3 임계값, OnValidate 클램프, ContextMenu Save) |

### 신규 (사용자 Unity Editor — 1)

| 경로 | 상태 |
|---|---|
| `LostMemory/Assets/_Project/ScriptableObjects/Enemies/Bertha_Boss.asset` | 생성 ✅, 기본값 상태. 운영값 입력은 후속 작업 |

### 수정 없음

- `BerthaBossPhaseController.cs` (CL-180 보류)
- `EnemyCatalog.cs` (본 CL 범위 외)
- `EnemyCatalog_Default.asset` (영향 없음)

### 재사용 (참고)

- WeaponData (`Runtime/Data/WeaponData.cs`) — Save 패턴 / 접근자 컨벤션 참고
- WeaponDataEvents — EnemyDataEvents 의 모델

---

## 발견·해소된 이슈

### 1. BossData.SaveCurrentValues 메서드명 충돌
**문제**: base(EnemyData) 의 `private SaveCurrentValues()` 와 동명 메서드를 BossData 에 정의 시 두 ContextMenu 항목이 모두 우클릭 메뉴에 노출되어 어느 메서드가 호출되는지 사용자 혼란.

**해소**: BossData 측 메서드명을 `SaveBossCurrentValues()` 로 변경. `[ContextMenu("Save Current Values")]` 라벨은 동일 유지 → 사용자 UX 측면에서는 BossData asset 우클릭 시 "Save Current Values" 한 항목만 표시 (BossData 구현 호출).

### 2. Phase 3 Threshold 표시 = 0.7 (사용자 검증 중 발견 — 정상 동작 증거)
**상황**: 사용자가 인스펙터에서 Phase 3 슬라이더에 Phase 2(0.7) 보다 큰 값 입력 → 화면에는 0.7 로 표시.

**해석**: 이건 OnValidate 의 `_phase3 = Mathf.Min(_phase3, _phase2)` 클램프가 작동했다는 증거. **의도된 동작**. (실제 운영값은 0.3 으로 입력 필요 — 사용자 후속 작업.)

---

## 검증 결과

### 1. CS 빌드 ✅
- `EnemyData.cs` 컴파일 오류 없음 (Grep 으로 다른 파일 영향 없음 확인 — `EnemyData` / `BossData` 토큰은 EnemyCatalog.cs 21~22줄 주석에만 존재)
- `BossData.cs` 컴파일 오류 없음
- `BerthaBossPhaseController.cs` / `EnemyCatalog.cs` 변경 없음 (git status 에서 untracked 신규 2개만)

### 2. Unity Editor 메뉴 ✅
- `Create > LostMemory > Enemies > Enemy Data` / `Boss Data` 메뉴 표시 (사용자 보고)

### 3. Bertha_Boss.asset 인스펙터 ✅
사용자 스크린샷 확인:
- Script 슬롯: BossData ✅
- Display Name (string)
- Max Health = 100 (기본값, 입력 필요)
- Move Speed = 5 (기본값, 입력 필요)
- Exp Reward = 0
- Drop Weight = 1 (slider)
- Phase 2 Threshold = 0.7 (slider, BerthaBossPhaseController 동일)
- Phase 3 Threshold = 0.7 (사용자 클램프 테스트 결과 — 운영값 0.3 입력 필요)

→ 모든 필드 편집 가능, Range slider 정상.

### 4. OnValidate 클램프 작동 ✅
Phase 3 에 0.7 보다 큰 값 입력 → 자동으로 0.7 (Phase 2 와 동일) 로 보정 확인.

### 5. EnemyCatalog_Default.asset 영향 없음 ✅
- 코드 변경 없으므로 회귀 가능성 없음.
- 명시적 Play 검증은 본 CL 범위 외 (CL-180 어댑터에서 검증).

### 미검증 (사용자 후속 작업)

- Display Name = "Bertha" 입력
- Max Health / Move Speed = Bertha Prefab 의 Health.MaximumHealth / CharacterMovement.MovementSpeed 값
- Phase 3 Threshold = 0.3 (운영값)
- "Save Current Values" ContextMenu 실행 시 `[BossData] Saved: Bertha_Boss` 로그 출력 확인

---

## 위험 / 결정 미정

### 위험
1. **데이터-코드 비동기 (의도된 상태)**: Bertha_Boss.asset 의 값은 CL-180 어댑터 작업 전까지 게임에 미반영. 사용자가 SO 만 수정하고 게임 동작이 안 바뀐다고 오해할 수 있음. **팀 공유 필요**.
2. **expReward / dropWeight 시스템 미존재**: 값 입력해도 게임 효과 없음. 보상/드롭 시스템 ticket 에서 연결 예정.
3. **일반 EnemyData asset 부재**: CL-173 Provider 등록 후 Balance Editor 의 Enemies 카테고리는 Bertha_Boss 만 표시 (Boss Data 카테고리에 분류). 일반 몹 등장 ticket 에서 EnemyData asset 추가.
4. **EnemyDataEvents 구독자 없음**: 현재 Runtime 어디에서도 OnAssetSaved 구독하지 않음. ContextMenu Save 는 SaveAssetIfDirty 효과만 발생, Runtime 반영 X. CL-180 어댑터에서 구독자 추가.
5. **BossData / EnemyData asset 의 Inspector EffectType 류 검증 미존재**: 향후 필드 확장 시 잘못된 enum 입력 가능 (CL-146 의 회피 SO 입력 오류 사례 참고). 자동 검증 도구 — 별도 ticket.

### 결정 미정 (본 CL 외)
- [ ] CL-173: EnemyDataCategoryProvider / BossDataCategoryProvider 신설 + BalanceEditorWindow 등록
- [ ] CL-180: BerthaBossPhaseController 가 BossData 를 참조하도록 어댑터 (Configure() API 활용)
- [ ] 일반 EnemyData asset 추가 (일반 몹 등장 ticket)
- [ ] expReward / dropWeight 시스템 연결 (보상/드롭 ticket)
- [ ] EnemyDataPlayModeIsolator 류 Editor 보조 (CL-180 어댑터 후 라이브 튠 필요 시)

---

## 후속 인계

| Ticket | CL-172 와의 관계 |
|---|---|
| **CL-173 (EnemyData/BossData Provider 등록)** | 본 CL 산출물(`t:EnemyData` / `t:BossData` 필터 진입점) 즉시 사용. `IBalanceCategoryProvider` 구현체 2개 + `BalanceEditorWindow._providers` 에 추가 |
| **CL-180 (Enemy 적용 어댑터)** | BerthaBossPhaseController.Configure() 가 BossData 의 phase 임계값을 받도록 연결. EnemyDataEvents.OnAssetSaved 구독으로 라이브 튠 |
| **일반 몹 등장 ticket** | EnemyData.cs 그대로 사용, 인스턴스 .asset 만 추가 |
| **보상/드롭 시스템 ticket** | expReward / dropWeight 필드 연결 |

---

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| EnemyData.cs / BossData.cs 작성 | 15분 | 약 10분 |
| 컴파일 영향 범위 Grep 검증 | (포함) | 5분 |
| Bertha_Boss.asset 생성 (사용자) | 10분 | 약 5분 (스크린샷 시점) |
| 인스펙터 / OnValidate 클램프 검증 (사용자) | 15분 | 약 5분 (구조 검증만, 운영값 입력 후속) |
| 클래스 컨벤션 결정 (menuName / Save 패턴) | (plan 단계) | 추가 약 5분 (사용자 질의응답) |
| **합계 (구조 검증까지)** | **약 40분** | **약 30분** |

운영값 입력 (Display Name / MaxHealth / MoveSpeed / Phase3=0.3) 은 별도. Bertha Prefab 의 Health / CharacterMovement Inspector 확인 시간 포함 시 추가 약 10분 예상.

3점 ticket 적정 규모. 코드 작업 자체는 가벼웠고, doc/khi/cl172_plan.md 의 명세도가 높아 결정 사항이 적었음. 사용자 결정 (menuName 컨벤션 / Save 패턴) 만 추가로 발생.

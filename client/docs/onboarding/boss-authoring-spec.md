# Boss Authoring Spec — 보스 양산 표준 명세

## 0. 이 문서의 목적

보스는 일반 몹 명세 [mob-authoring-spec.md](mob-authoring-spec.md) 의 8 카테고리를 모두 따르고, 추가로 **페이즈 / 패턴 / 입장·클리어 게이트 / 룸·카메라·UI 통합** 5 항목을 더 갖춰야 한다. 이 문서는 그 추가 항목과 Bertha 1 종의 충족도를 정리한다.

향후 보스를 1 종 이상 추가하려면 BerthaBossPhase 같은 enum 의 일반화 등 구조 결정이 더 필요하다 — 7 절의 미정 항목에 정리.

---

## 1. 보스 = 일반 몹 + 5 추가 카테고리

```
[일반 몹 명세 8 카테고리 모두 적용]
            +
┌──────────────────────────────────────────────┐
│ A. 페이즈 시스템                             │
│    BossData.Phase2/3 ThresholdNormalized     │
│    XxxBossPhaseController                    │
├──────────────────────────────────────────────┤
│ B. 패턴 시스템                               │
│    XxxXxxAttackController + Bootstrap        │
│    XxxCombatPatternSelector                  │
├──────────────────────────────────────────────┤
│ C. 입장 / 클리어 게이트                      │
│    BossDoor + RoomEntryRuntimeController     │
│    XxxBossEncounterController                │
│    BossClearPortal                           │
├──────────────────────────────────────────────┤
│ D. 보스 전용 비주얼 / 사운드                 │
│    Intro 애니메이션 / 페이즈 전환 이펙트     │
│    보스 BGM 키                               │
├──────────────────────────────────────────────┤
│ E. 룸 / 카메라 / UI 통합                     │
│    XxxBossRoom.prefab (카메라 lock)          │
│    BossHealthBarView (HUD)                   │
│    RunResultPanelView (결과 화면)            │
└──────────────────────────────────────────────┘
```

---

## 2. 보스 추가 카테고리 표준

### 2.A 페이즈 시스템

[BossData.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/BossData.cs) 가 EnemyData 를 상속하면서 추가하는 임계 필드.

| 필드 | 타입 | 허용 범위 | 코드 default | 의미 |
|---|---|---|---|---|
| `_phase2ThresholdNormalized` | float | 0~1 | 0.7 | Phase1 → Phase2 전환 체력 비율 |
| `_phase3ThresholdNormalized` | float | 0~1 (≤ Phase2) | 0.3 | Phase2 → Phase3 전환 체력 비율 |

`OnValidate` 에서 `Phase3 ≤ Phase2` 가 강제된다. SO 인스턴스에서 위 default 와 다른 값을 쓰면 코드 default 의 가정과 어긋날 수 있으므로 CL 문서에 명시한다.

**필수 컴포넌트**

- `XxxBossPhaseController` — Health.OnHit 이벤트로 임계 체크 후 Phase enum 전환
- `XxxBossPhase` enum — `Phase1` / `Phase2` / `Phase3`
- `XxxHealthThresholdReactionController` (권장) — 임계 도달 시 외형/이펙트 변화 트리거
- `XxxBrainAnimationController` (권장) — 페이즈별 애니메이션 동기화

**페이즈별 변화 표 (양산 시 작성)**

| Phase | 체력 구간 | 사용 패턴 | 외형 변화 | 이속/공격속도 |
|---|---|---|---|---|
| 1 | 100% ~ Phase2 임계 | 기본 패턴 N 종 | 기본 외형 | 기본 |
| 2 | Phase2 ~ Phase3 임계 | 기본 + 강화 N 종 | 외형 변화 1 | +α |
| 3 | Phase3 ~ 0% | 강화 + 풀콤보 | 외형 변화 2 | +β |

### 2.B 패턴 시스템

각 패턴은 별도 Controller 클래스로 분리. 매니저와의 결합 코드는 Bootstrap 래퍼로 분리한다 (양산 시 prefab 수정 빈도 감소).

**파일 구성 패턴**

```
XxxYyyAttackController.cs       # 패턴 본체 로직
XxxYyyAttackBootstrap.cs        # AIBrain / Health / Animator 결합
```

**패턴 선택자**

`XxxCombatPatternSelector.cs` 가 매 프레임 또는 패턴 종료 후 다음 패턴을 결정한다.

**선택 입력**

| 입력 | 용도 |
|---|---|
| 현재 페이즈 | 페이즈별 패턴 풀 한정 |
| 플레이어 거리 | 근/중/원 거리에 맞는 패턴 |
| 패턴별 쿨다운 | 동일 패턴 연속 방지 |
| 이전 패턴 | 콤보 가능 여부 |

**패턴 카드 (양산 시 패턴 1 개당 작성)**

| 항목 | 값 |
|---|---|
| 이름 | XxxAttack |
| 사용 가능 페이즈 | Phase1+ / Phase2+ / Phase3+ |
| 거리 조건 | 근 / 중 / 원 |
| 텔레그래프 시간 | 0.x 초 |
| 실행 시간 | 0.x 초 |
| 후딜레이 | 0.x 초 |
| 쿨다운 | x 초 |
| 데미지 | (Melee/Charge/Projectile/Slam 별) |
| VFX | prefab 참조 |
| 사운드 | 키 |

### 2.C 입장 / 클리어 게이트

**입장 게이트 (CL-049 / 050)**

- `RoomEntryRuntimeController` — 선행 룸 클리어 조건 검사
- `BossDoor Variant.prefab` — 잠김 / 열림 비주얼
- 잠김 상태: 선행 조건 미충족 시 도어 비활성 + UI 알림
- 열림 상태: 충족 시 도어 활성 + 입장 가능

**입장 시퀀스 (CL-051)**

- `XxxBossEncounterController` — 보스방 입장 시 Intro 애니메이션 → 전투 시작 트리거
- 카메라 lock + 음악 전환

**처치 / 클리어 (CL-054 / 055)**

- Health.OnDeath → RoomCleared 이벤트
- `BossClearPortal.prefab` 등장
- `RunResultPanelView` 결과 화면 데이터 채움 (RunManager 의 RunCleared 상태 전이)

### 2.D 보스 전용 비주얼 / 사운드

**필수 애니메이션 클립** (일반 몹 6 클립 + 보스 추가 3 클립)

| 클립 | 비고 |
|---|---|
| Intro | 보스방 입장 시 1 회 |
| PhaseTransition | 페이즈 전환 시 1 회 (또는 페이즈별 별도 clip) |
| DefeatLong | 처치 연출 (일반 Death 보다 길게, 1.5~3 초) |

**사운드**

- 보스 BGM 키 (전투 시 음악 전환)
- 인트로 사운드 / 페이즈 전환 사운드 / 처치 사운드
- 패턴별 사운드 (텔레그래프 / 실행 / 임팩트)

### 2.E 룸 / 카메라 / UI 통합

**보스방 prefab**

- `XxxBossRoom.prefab` — 보스방 전체 (타일맵, 카메라 lock 영역, 입장 트리거)
- `XxxRoom2.prefab` 등 변형 — 같은 보스의 다른 레이아웃

**HUD**

- `BossHealthBarView.cs` — 화면 상단 보스 체력바 + DisplayName 표시
- `BossData.MaxHealth` 와 현재 체력으로 비율 계산
- DisplayName 이 비어있으면 라벨이 빈 칸으로 표시 (즉시 결함)

**결과 화면**

- `RunResultPanelView.cs` — 보스 클리어 후 RunManager 의 통계로 결과 패널 채움
- 통계: 처치 적 수, 사용 시간, 받은 데미지, 획득 유물 등

---

## 3. 신규 보스 추가 체크리스트 (일반 몹 19 단계 + 보스 추가)

먼저 [mob-authoring-spec.md 의 신규 몹 체크리스트 1~19](mob-authoring-spec.md#3-신규-몹-추가-체크리스트) 를 모두 진행. 이후 다음을 추가 진행한다.

- [ ] **20. BossData_<Name>.asset 생성** — `Create > LostMemory > Enemies > Boss Data` (EnemyData 가 아니라 BossData)
- [ ] **21. Phase2 / Phase3 임계 결정** — default 0.7 / 0.3 외 값 사용 시 CL 문서에 사유 기록
- [ ] **22. 페이즈별 변화 표 작성** — 사용 패턴 / 외형 / 이속 (위 2.A 표 형식)
- [ ] **23. 패턴 N 종 결정** — 페이즈/거리 매트릭스로 N 결정 (Bertha 는 6 종)
- [ ] **24. 패턴 카드 N 장 작성** — 위 2.B 패턴 카드 형식
- [ ] **25. XxxXxxAttackController + Bootstrap 작성** — 패턴 1 종당 2 클래스
- [ ] **26. XxxBossPhaseController 작성**
- [ ] **27. XxxCombatPatternSelector 작성** — 거리/페이즈/쿨다운 룰 구현
- [ ] **28. XxxHealthThresholdReactionController 작성** — 임계 도달 외형 변화
- [ ] **29. Intro / PhaseTransition / DefeatLong 애니메이션 클립 추가**
- [ ] **30. 보스 BGM 키 + 패턴 사운드 매핑**
- [ ] **31. XxxBossRoom.prefab 생성** — 카메라 lock, 입장 트리거
- [ ] **32. BossDoor + RoomEntryRuntimeController 셋업** — 진입 조건
- [ ] **33. BossClearPortal 등장 위치 셋업**
- [ ] **34. BossHealthBarView 연결** — DisplayName + MaxHealth 바인딩
- [ ] **35. RunResultPanel 결과 데이터 흐름 검증**
- [ ] **36. CL 보스 핸드오프 문서 작성** — 페이즈/패턴/임계/UI 통합 결정 사항

---

## 4. Bertha 충족도 격자

`O` = 채워짐 / `X` = 누락 / `△` = 부분 / `?` = Unity 에디터 확인 필요

### 4.1 일반 몹 명세 항목 (mob-authoring-spec 의 8 카테고리)

| 항목 | Bertha | 비고 |
|---|:-:|---|
| **2.1 식별** — DisplayName | **X 빈 문자열** | UI 보스바 라벨이 빈 칸으로 표시될 위험. 즉시 채울 것 |
| **2.2 데이터** — MaxHealth | O 200 | 보스 권장 1500~3000 대비 매우 낮음. 프로토타입용 값으로 추정 |
| **2.2 데이터** — MoveSpeed | O 5 | 권장 범위 |
| **2.2 데이터** — AttackDamages | **X 빈 배열** | 패턴 6 종의 데미지가 SO 가 아니라 컨트롤러에 분산. 표준화 필요 |
| **2.2 데이터** — ExpReward | **X 0** | 보상 시스템 미가동 |
| **2.2 데이터** — DropWeight | △ default 1 | 미적용 |
| **2.4 AI** — Controller / Selector | O | BerthaCombatPatternSelector + 6 패턴 컨트롤러 |
| **2.5 비주얼** — 6 클립 | ? | Unity 확인 필요 |
| **2.5 비주얼** — Intro | ? | BerthaBrainAnimationController 존재로 추정 |
| **2.6 사운드 / VFX** | ? | Unity 확인 필요 |
| **2.7 물리** — Body / Hitbox / Layer | ? | Unity 확인 필요 |
| **2.8 통합** — Catalog 등록 | ? | EnemyCatalog_Default 의 entries 에 별도 entry 가 있는지 확인 필요 |

### 4.2 보스 추가 카테고리

| 항목 | Bertha | 비고 |
|---|:-:|---|
| **2.A 페이즈** — Phase2 임계 | △ **0.8** | 코드 default 0.7 과 다름. 0.8 이 의도인지 확인 필요 |
| **2.A 페이즈** — Phase3 임계 | O 0.3 | default 일치 |
| **2.A 페이즈** — Phase enum | O | BerthaBossPhase (Phase1/2/3) |
| **2.A 페이즈** — PhaseController | O | BerthaBossPhaseController |
| **2.A 페이즈** — ThresholdReaction | O | BerthaHealthThresholdReactionController |
| **2.A 페이즈** — BrainAnimation | O | BerthaBrainAnimationController |
| **2.B 패턴** — 패턴 6 종 | O | LightAttack1 / LightAttack2 / Heavy / NormalDash / DashAttack / FullCombo |
| **2.B 패턴** — Bootstrap 래퍼 | O | LightAttack1Bootstrap / LightAttack2Bootstrap / HeavyBootstrap / DashPatternBootstrap |
| **2.B 패턴** — PatternSelector | O | BerthaCombatPatternSelector |
| **2.B 패턴** — 패턴 카드 N 장 | **X** | 위 2.B 표 형식의 패턴 카드 명세 미존재. 사후 작성 권장 |
| **2.B 패턴** — 데미지 SO 화 | **X** | EnemyData.AttackDamages 에 안 채워져 있음 |
| **2.C 입장** — RoomEntryController | O | RoomEntryRuntimeController.prefab |
| **2.C 입장** — BossDoor | O | BossDoor Variant.prefab |
| **2.C 입장** — EncounterController | O | BerthaBossEncounterController |
| **2.C 클리어** — BossClearPortal | O | BossClearPortal.prefab |
| **2.C 클리어** — RunResultPanel | O | CL-055 |
| **2.D 비주얼** — Intro 애니메이션 | ? | 코드 존재 / 클립 확인 필요 |
| **2.D 비주얼** — PhaseTransition | ? | HealthThresholdReactionController 존재 / 클립 확인 필요 |
| **2.D 사운드** — 보스 BGM 키 | ? | Unity 확인 필요 |
| **2.E 룸** — BossRoom prefab | O | BerthaBossRoom.prefab + BerthaRoom2.prefab |
| **2.E HUD** — BossHealthBarView | O | UI 도메인에 존재 |
| **2.E HUD** — DisplayName 바인딩 | **X** | DisplayName 빈 문자열 → 라벨 빈 칸 |
| CL 문서 | O | CL-049/050 (입장), CL-051/052 (패턴), CL-053 (페이즈), CL-054 (처치), CL-055 (결과) |

---

## 5. 격자에서 즉시 드러난 누락 (우선 처리 권장)

### 5.1 DisplayName 빈 문자열

`Bertha_Boss.asset` 의 `_displayName` 이 빈 문자열. BossHealthBarView 가 이 값을 라벨로 표시하면 빈 칸이 노출된다. **"Bertha"** 또는 게임 내 정식 명칭으로 채울 것.

### 5.2 Phase2 임계 0.8 — 코드 default 와 불일치

코드 `BossData._phase2ThresholdNormalized` 의 default 는 0.7 이지만 인스턴스는 0.8 로 오버라이드. 의도라면 OK, 실수라면 0.7 로 되돌릴 것.

영향: 기획 문서 / 캡처 컷 리스트의 "70% 임계" 가 실제 게임에서는 80% 임계로 동작. [capture-shot-list.md](capture-shot-list.md) 의 #11 도 80% 로 일관시킴.

### 5.3 AttackDamages 빈 배열 — 패턴 6 종 데미지가 SO 에 없음

각 패턴 컨트롤러의 인스펙터 값에 분산되어 있을 가능성. 보스 밸런싱이 6 군데 인스펙터를 동시에 봐야 가능한 상태. SO 또는 별도 BossPatternData SO 로 통합 권장.

### 5.4 보스 패턴 카드 명세 부재

위 2.B 패턴 카드 형식의 표가 6 종 각각에 대해 작성되어 있지 않음. 사후라도 1 페이지짜리 패턴 명세를 [client/docs/](../) 에 작성하면 신규 합류자가 보스 밸런싱을 단독으로 시도 가능.

### 5.5 MaxHealth 200 — 프로토타입 값

권장 보스 체력 1500~3000 대비 매우 낮음. 프로토타입 단계에서 빠른 처치 테스트용으로 추정됨. 정식 밸런싱 시점에 상향 + Phase 임계 재검증.

---

## 6. 향후 보스 2 호 추가 시 결정 필요

현재는 Bertha 1 종만 있어 Bertha 전용 클래스 (`BerthaXxx`) 가 그대로 보스 시스템 코드 노릇을 하고 있다. 보스가 2 종 이상 되면 다음을 결정해야 한다.

- **Phase enum 일반화**: `BerthaBossPhase` enum 을 `BossPhase` 로 일반화할지, 보스마다 별도 enum 으로 둘지
- **PatternSelector 일반화**: `BerthaCombatPatternSelector` 를 `BossCombatPatternSelector<TPattern>` 류 제네릭으로 끌어올릴지
- **패턴 데이터 SO 화**: Bertha 의 6 패턴이 컨트롤러 인스펙터에 흩어진 상태. 보스 2 호 진입 전에 `BossPatternData.cs` SO 도입 검토
- **공통 보스 유틸**: BrainAnimationController / HealthThresholdReactionController 를 보스 공통 추상화로 끌어올릴지

---

## 7. BalanceEditor 를 통한 보스 데이터 편집

BalanceEditor 의 공통 메커니즘 — Provider 패턴, AutoSave (Debounce), AssetWatcher 자동 새로고침, JSON Import/Export, 검색 — 은 [mob-authoring-spec.md §8](mob-authoring-spec.md#8-balanceeditor-를-통한-데이터-편집) 에 정리되어 있다. 본 절은 **보스 한정 추가 사항** 만 다룬다.

### 7.1 구조 (보스 노드 노출 경로)

```
BalanceEditorWindow
        │
        ├── _providers 리스트
        │     ├─ EnemyDataCategoryProvider  ── "Enemies" (일반 몹)
        │     │      └─ filter: GetType().FullName.EndsWith(".EnemyData")
        │     │         → BossData 인스턴스는 제외
        │     └─ BossDataCategoryProvider   ── "Bosses"
        │            ├─ AssetTypeFilter: "t:BossData"
        │            ├─ SearchFolders: Assets/_Project/ScriptableObjects/Enemies
        │            └─ LoadAll() → 모든 BossData 인스턴스 반환
        │
        └── 우측 InspectorElement (선택된 BossData)
              └─ BossData 의 SerializeField 자동 노출
                  ├─ EnemyData 상속 6 필드
                  │   (DisplayName / MaxHealth / MoveSpeed
                  │    / AttackDamages / ExpReward / DropWeight)
                  └─ BossData 추가 2 필드
                      ├─ _phase2ThresholdNormalized  (Range 0~1)
                      └─ _phase3ThresholdNormalized  (Range 0~1)
```

`Bertha_Boss.asset` 은 **Bosses 트리에만 표시되고 Enemies 트리에는 표시되지 않는다.** EnemyDataCategoryProvider 의 정확 타입 필터로 차단되기 때문.

### 7.2 BossData 의 보스 전용 필드 편집

| 필드 | UI | 코드 default | 자동 보정 |
|---|---|---|---|
| `_phase2ThresholdNormalized` | 0~1 슬라이더 | 0.7 | `Mathf.Clamp01` |
| `_phase3ThresholdNormalized` | 0~1 슬라이더 | 0.3 | `Mathf.Clamp01` + `Mathf.Min(_phase3, _phase2)` |

`BossData.OnValidate` 가 두 임계의 정합성을 강제한다. 즉 BalanceEditor 에서 `_phase3` 를 `_phase2` 보다 큰 값으로 입력하면 저장 시점에 `_phase3` 가 자동으로 `_phase2` 와 같은 값으로 보정된다. 이 동작은 인스펙터에 즉시 반영되지 않을 수 있어, 슬라이더로 입력 후 다른 SO 를 클릭했다 돌아오면 보정된 값이 보인다.

### 7.3 BalanceEditor 가 다루지 못하는 영역 (보스의 한계)

**중요**: 보스의 완전한 밸런싱은 BalanceEditor 만으로는 불가능하다. 다음은 BalanceEditor 외부에 있다.

| 항목 | 현재 위치 | 이유 | 표준화 시 BalanceEditor 통합 방안 |
|---|---|---|---|
| 패턴 6 종 데미지 | 각 `BerthaXxxAttackController` 의 인스펙터 값 | SO 가 아니라 컴포넌트 인스펙터 | `BossPatternData` SO 도입 후 별도 카테고리로 노출 |
| 패턴 텔레그래프 시간 / 후딜레이 / 쿨다운 | 패턴 컨트롤러 인스펙터 | 동상 | 위와 같이 `BossPatternData` 에 통합 |
| 패턴 선택 룰 (거리 / 페이즈 / 쿨다운) | `BerthaCombatPatternSelector` 인스펙터 | 동상 | `BossPatternSelectorData` SO 분리 |
| 페이즈 전환 이펙트 강도 | `BerthaHealthThresholdReactionController` | 동상 | `BossPhaseReactionData` SO 분리 |
| 보스방 카메라 lock 영역 | 보스방 prefab 의 컴포넌트 값 | 룸 prefab 영역 | BalanceEditor 범위 외 (별도 룸 도구 필요) |

위 4 항목 (앞 4 행) 은 [§5.3](#53-attackdamages-빈-배열--패턴-6-종-데미지가-so-에-없음) 의 "AttackDamages 빈 배열" 누락과 직결된다. SO 통합이 결정되면 BalanceEditor 에서 패턴 1 종을 선택해 데미지 / 시간 / 쿨다운을 한 화면에서 튜닝 가능해진다.

### 7.4 라이브 디버그 (Play 중 보스 즉시 조작) 미구현

"보스 전투 중 체력을 절반으로 줄여 페이즈 2 로 즉시 진입" / "패턴 1 회 강제 시전" 같은 라이브 디버그는 BalanceEditor 가 다루지 않는다.

- **BalanceEditor 의 한계**: SO 값 편집 도구. Play 중 SO 를 수정해도 이미 스폰된 보스 인스턴스에는 적용되지 않음 ([mob-authoring-spec.md §8.8](mob-authoring-spec.md#88-실시간-수정의-한계--정확한-의미) 참조)
- **필요 시 도입 후보**: TopDown Engine 의 `MMDebugMenu` 확장, 또는 보스 전용 디버그 윈도우 (예: `BertaDebugWindow.cs` Editor 윈도우)
- **본 명세 범위**: 향후 미정 항목 ([§6](#6-향후-보스-2-호-추가-시-결정-필요)) 으로 추적

### 7.5 Bertha 인스턴스의 현재 BalanceEditor 노출 상태

BalanceEditor 에서 `Bosses > Bertha_Boss` 를 선택했을 때 우측에 보이는 값. `[§4 격자](#4-bertha-충족도-격자)` 의 누락은 **모두 이 화면에서 즉시 수정 가능**하다.

| 필드 | BalanceEditor 노출 | 현재 값 | 수정 권장 |
|---|:-:|---|---|
| DisplayName | O | (빈 문자열) | **`Bertha`** 또는 정식 명칭 즉시 입력 |
| MaxHealth | O | 200 | 정식 밸런싱 시점에 1500~3000 권장값으로 상향 |
| MoveSpeed | O | 5 | 권장 범위 내 |
| AttackDamages | O | (빈 배열) | 패턴 데미지 SO 통합 결정 후 일괄 입력 |
| ExpReward | O | 0 | 보상 시스템 가동 시 일괄 입력 |
| DropWeight | O | 1 | 차등 드롭 정책 결정 후 |
| Phase2ThresholdNormalized | O | **0.8** | 코드 default 0.7 와 다름. 의도 확인 후 0.7 로 되돌리거나 코드 default 갱신 |
| Phase3ThresholdNormalized | O | 0.3 | default 일치 |

### 7.6 신규 보스 추가 시 BalanceEditor 측 별도 작업 — 없음

`BossDataCategoryProvider` 가 이미 등록되어 있으므로 신규 `BossData_<Name>.asset` 을 `Assets/_Project/ScriptableObjects/Enemies/` 폴더에 만들면 BalanceEditor 가 열려있는 동안 자동으로 Bosses 트리에 추가된다 (BalanceEditorAssetWatcher 가 새로고침). [§3 신규 보스 추가 체크리스트](#3-신규-보스-추가-체크리스트-일반-몹-19-단계--보스-추가) 외에 BalanceEditor 측 추가 등록 작업은 불필요.

---

## 8. 참고

- 일반 몹 표준: [mob-authoring-spec.md](mob-authoring-spec.md)
- BossData 정의: [BossData.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/BossData.cs)
- 보스 컨트롤러 군: [Boss/Bertha/](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/)
- 보스 SO: [Bertha_Boss.asset](../../LostMemory/Assets/_Project/ScriptableObjects/Enemies/Bertha_Boss.asset)
- 보스 prefab: [BerthaRoot.prefab](../../LostMemory/Assets/_Project/Prefabs/Enemies/Boss/) / [BerthaBossRoom.prefab](../../LostMemory/Assets/_Project/Prefabs/Rooms/Boss/)
- 캡처 가이드: [capture-shot-list.md](capture-shot-list.md)
- CL 문서: CL-049/050 (입장 게이트), CL-051 (패턴 1), CL-052 (패턴 2), CL-053 (페이즈 전환), CL-054 (처치), CL-055 (결과 화면)

# Mob Authoring Spec — 일반 몹 양산 표준 명세

## 0. 이 문서의 목적

지금까지 적은 (1) 규격 정의 → (2) 양산 흐름이 아니라 (1) 만든 후 (2) 사후 표준화 흐름으로 진행되었다. 이 문서는 사후 표준화의 결과물이며, 향후 신규 몹을 추가할 때 빠짐없이 갖춰야 하는 기준선이다.

세 가지를 한 문서에서 다룬다.

1. 몹 1 마리에 들어가야 하는 8 카테고리 표준 항목
2. 신규 몹 추가 시 따라야 할 순차 체크리스트
3. 현재 6 종 적의 충족도 격자 (이미 드러난 누락 포함)

보스 한정 추가 항목은 [boss-authoring-spec.md](boss-authoring-spec.md) 에서 다룬다.

---

## 1. 몹 1 마리 = 무엇인가

```
┌───────────────────────────────────────────────────────────┐
│  (1) Catalog ID                                           │
│      EnemyCatalog_Default.asset 의 entry                  │
│      예: enemy_melee_basic                                │
│         ├─ id (snake_case)                                │
│         ├─ prefab (참조)                                  │
│         └─ data  (참조)                                   │
└──────────┬───────────────────────────┬────────────────────┘
           ▼                           ▼
  ┌────────────────────┐   ┌────────────────────────────────┐
  │ (2) EnemyData(SO)  │   │ (3) Prefab                     │
  │  - DisplayName     │   │  - Health                      │
  │  - MaxHealth       │   │  - CharacterMovement           │
  │  - MoveSpeed       │   │  - AIBrain                     │
  │  - AttackDamages[] │   │  - SpriteRenderer (main + 보조)│
  │  - ExpReward       │   │  - Animator (Idle/Walk/...)    │
  │  - DropWeight      │   │  - Body Collider / Hitbox      │
  └─────────┬──────────┘   │  - 행동 Controller (옵션)      │
            │              └──────┬─────────────────────────┘
            │                     │
            └──────► 런타임 ◄─────┘
                  EnemyDataRuntimeAdapter
                  가 SO 값을 prefab 인스턴스에 적용
```

몹 1 마리 = **카탈로그 entry** + **EnemyData (SO)** + **Prefab** 의 3 단 구성. 이 셋이 모두 일관되게 채워져야 신규 합류 개발자가 단독으로 양산 가능한 상태가 된다.

---

## 2. 표준 카테고리 8 가지

### 2.1 식별 / 메타데이터

**필수**

| 항목 | 위치 | 설명 |
|---|---|---|
| Catalog Id | `EnemyCatalog_Default.asset` entry.id | snake_case. 코드/스폰 풀에서 적을 지칭하는 안정 키 |
| DisplayName | `EnemyData._displayName` | UI / 디버그 로그 표시용. 비어있으면 즉시 결함 |
| Prefab Name | 파일명 | `<PascalCase>_CL<티켓번호>.prefab` |
| SO Name | 파일명 | `EnemyData_<PascalCase>.asset` |

**권장**

- 적 카테고리 (근접 / 돌진 / 원거리 / 자폭 / 중장 / 정찰)
- 담당자 + 작업 상태 (prototype / done / re-balancing) — CL 문서 헤더에 기재
- 관련 CL 티켓 번호 (양산 추적용)

**위반 시 결과**: DisplayName 누락 → UI 보스/적 라벨이 빈 칸으로 표시. Catalog Id 변경 → 기존 룸 인카운터 풀 깨짐.

### 2.2 데이터 (SO 필수 필드)

[EnemyData.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyData.cs) 가 정의하는 필수 필드.

| 필드 | 타입 | 허용 범위 | 미설정 시 결함 |
|---|---|---|---|
| `_displayName` | string | non-empty | UI 라벨 빈 칸 |
| `_maxHealth` | float | > 0 (default 100) | 즉사 또는 무한 체력 |
| `_moveSpeed` | float | ≥ 0 (default 5) | 정지형은 0 허용 |
| `_attackDamages` | `EnemyAttackDamage[]` | 길이 ≥ 1 (자폭형 예외) | 공격이 데미지를 못 줌 |
| `_expReward` | int | ≥ 0 | 처치해도 경험치 0 |
| `_dropWeight` | float | 0~1 (default 1) | 드롭 풀에서 가중치 미적용 |

**적 카테고리별 권장 기본값** (양산 시작점 가이드, 밸런싱은 별도)

| 카테고리 | MaxHealth | MoveSpeed | 주 AttackType | ExpReward |
|---|---|---|---|---|
| 근접 (Orc 류) | 50~80 | 3~4 | Melee | 5 |
| 돌진 (OrcRider 류) | 20~40 | 3 | Charge | 4 |
| 원거리 (SkeletonArcher 류) | 40~60 | 3~4 | Projectile | 5 |
| 자폭 (Chobomb 류) | 30~40 | 2~3 | (자폭 — Slam 또는 별도 컨트롤러) | 3 |
| 중장 (StoneGolem 류) | 200~400 | 2 | Slam | 10 |

### 2.3 공격 정의

`EnemyAttackType` enum 4 종: **Melee / Charge / Projectile / Slam**.

각 타입에 대해 `_damage` ≥ 0. 동일 적이 여러 타입을 가질 수 있다 (예: 보스가 Melee + Projectile 모두 보유).

**보강 필요 항목 (현재 SO 에 없음 — 향후 추가 검토)**

- 사거리 (range) — 현재 컨트롤러 인스펙터 값에 분산
- 공격 쿨다운 — 컨트롤러 분산
- 텔레그래프 시간 — 컨트롤러 분산

자폭형(Chobomb) 의 충돌 데미지는 `ChobombExplosionKnockback.cs` 가 직접 적용한다. SO 의 AttackDamages 배열은 보조 정보로만 쓰이거나 비어있을 수 있다. **단, 비어있는 경우는 명시적 의도라는 주석을 SO 또는 CL 문서에 남긴다.**

### 2.4 AI 행동 (TopDown Engine)

**필수 컴포넌트** (Prefab 에 모두 부착)

| 컴포넌트 | 역할 |
|---|---|
| `Health` (TDE) | 체력, OnHit/OnDeath 이벤트 |
| `CharacterMovement` (TDE) | 이동 속도, MovementSpeedMultiplier |
| `AIBrain` (TDE) | 상태 머신 |
| `Character` (TDE) | TDE 표준 진입점 |
| 행동 Controller (옵션) | 적 고유 동작 (예: ChobombSelfDestructController) |

**필수 AI 상태** (AIBrain 의 States 목록)

- Idle (대기)
- Detect (플레이어 감지)
- Chase (추격)
- Attack (공격 시퀀스)
- Hit (피격 반응)
- Death (사망 처리)

각 상태는 동일 이름의 애니메이션 클립과 1:1 매핑되어야 한다. 누락 시 애니메이션 미재생 or AnimatorController 워닝.

**감지 / 공격 거리 권장값 (TDE AIDecision 셋업 시작점)**

| 카테고리 | DetectRadius | AttackRange |
|---|---|---|
| 근접 | 5 | 1.5 |
| 돌진 | 8 | 6 |
| 원거리 | 7 | 5 |
| 자폭 | 4 | 1.0 (자폭 트리거) |
| 중장 | 5 | 2.0 |

**Bootstrap 래퍼 패턴**

보스 패턴 Controller 들이 `BerthaXxxBootstrap` 으로 매니저와 결합 코드를 분리한 패턴은 일반 몹의 복잡한 행동에도 적용 가능하다. 행동 코드와 prefab 결합 코드를 분리하면 prefab 수정 빈도를 줄일 수 있다.

### 2.5 비주얼 자산

**필수 애니메이션 클립 6 종**

| 클립 | 길이 권장 | 비고 |
|---|---|---|
| Idle | 반복 | 1~2 초 루프 |
| Walk | 반복 | 0.5~1 초 루프 |
| Telegraph | 0.3~0.6 초 | 공격 직전 전조 (선택, 공격에 통합 가능) |
| Attack | 0.4~0.8 초 | 1 회 |
| Hit | 0.15~0.3 초 | 1 회, 짧게 |
| Death | 0.5~1 초 | 1 회, 끝나면 prefab destroy |

**스프라이트 구성**

- 본체 SpriteRenderer: 1 개. EnemyStatusEffect 의 휴리스틱이 가장 큰 bounds 를 본체로 인식하므로 보조 스프라이트보다 커야 한다.
- 보조 SpriteRenderer 의 GameObject 이름에 다음 키워드 중 하나가 포함되어야 EnemyStatusEffect 의 tint 휴리스틱에서 자동 제외된다: `shadow`, `vfx`, `effect`, `status`, `iceblock`. 즉, 그림자는 `Shadow`, 이펙트는 `VFX_xxx` 식으로 명명한다.

**텔레그래프 비주얼 표준** (양산 시 일관된 UX 를 위한 약속)

| 공격 종류 | 텔레그래프 색 | 시간 |
|---|---|---|
| Melee | 빨강 #E74C3C | 0.3~0.4 초 |
| Charge | 노랑 #F1C40F | 0.5~0.7 초 (전조 길게) |
| Projectile | 파랑 #3498DB | 0.4 초 |
| Slam | 보라 #9B59B6 | 0.5 초 (광역) |

### 2.6 사운드 / VFX 훅

**사운드 키 매핑** (네이밍 컨벤션: `enemy_<카테고리>_<이벤트>`)

- `enemy_<cat>_spawn`
- `enemy_<cat>_hit`
- `enemy_<cat>_attack` (또는 패턴별)
- `enemy_<cat>_death`

**VFX**

- 텔레그래프 VFX (공격 종류별 prefab)
- 임팩트 VFX (피격 시 빛 번짐)
- 사망 VFX

**상태이상 VFX 자동 부착**

[EnemyStatusEffect.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs) 가 OnHitEffectRegistry 에 의해 첫 hit 시점에 자동 부착된다. 따라서 신규 몹이라도 **prefab 수정 없이** Slow / Burn / Freeze VFX 가 자동 적용된다 (정책 D 우선순위 Freeze > Burn > Slow).

단, 본체 SpriteRenderer 가 휴리스틱(보조 sprite 제외, 가장 큰 bounds) 으로 잡혀야 Slow / Burn 의 sprite tint 가 정상 동작한다. 위 2.5 의 명명 규칙 미준수 시 tint 가 보조 sprite 에 적용되거나 적용 실패 (Awake 시 워닝 로그).

### 2.7 물리 / 콜라이더 / 레이어

**필수**

| 항목 | 컴포넌트 | 비고 |
|---|---|---|
| Body Collider | BoxCollider2D 또는 CapsuleCollider2D | 적 본체 크기. 피격 판정용 |
| Hitbox | 별도 Collider2D (`isTrigger=true`) | 공격 판정용. 비활성 후 Attack 상태에서만 활성 |
| Rigidbody2D | Dynamic 또는 Kinematic | TDE CharacterMovement 가 요구 |

**Layer / Tag**

- Layer: `Enemies`
- Tag: `Enemy`
- Hitbox 는 별도 Layer (`EnemyAttack`) 권장

**Knockback**

- 일반 적: TDE Health 의 knockback 파라미터 사용
- 특수형: 별도 컴포넌트 (예: `StoneGolemAttackKnockback.cs`, `ChobombExplosionKnockback.cs`)

### 2.8 통합 / 등록

**1. EnemyCatalog 등록**

[EnemyCatalog_Default.asset](../../LostMemory/Assets/_Project/ScriptableObjects/Enemies/EnemyCatalog_Default.asset) 의 `entries` 배열에 1 개 entry 추가.

```yaml
- id: enemy_<role>_<variant>
  prefab: { ... GUID ... }
  data:   { ... GUID ... }
```

**2. 룸 인카운터 풀 등록**

스테이지/룸 매니저(예: Stage 도메인의 RoomEncounter 데이터) 가 어느 룸에서 어떤 적을 얼마나 스폰할지 정한다. 카탈로그에 등록만 하고 룸 풀에 안 넣으면 게임에 등장하지 않는다.

**3. 보상 풀 (선택)**

처치 시 드롭 가능한 아이템/유물 풀에 적 ID 를 등록.

**4. (멀티) 호스트 권위 / 동기화 대상**

[networking-integration-rules.md](../commonness/networking-integration-rules.md) 에 정의된 호스트 권위 모델 적용:

- 호스트만 적 AI / 체력 / 사망 결정
- 클라이언트는 위치 / 애니메이션 상태만 수신
- 동기화 대상 필드 명시 (CL 문서에 표로)

---

## 3. 신규 몹 추가 체크리스트

새 몹을 추가할 때 위에서 아래로 순차 진행한다.

- [ ] **1. CL 티켓 발급** — 지라/깃랩 (예: CL-220 새 적 Wraith)
- [ ] **2. 적 카테고리 결정** — 근접/돌진/원거리/자폭/중장 중 1 개
- [ ] **3. EnemyData_<Name>.asset 생성** — `Create > LostMemory > Enemies > Enemy Data`
- [ ] **4. SO 필수 6 필드 모두 채움** — DisplayName / MaxHealth / MoveSpeed / AttackDamages / ExpReward / DropWeight
- [ ] **5. 행동 Controller 작성 (필요 시)** — TDE 만으로 부족할 때만. 위치: `Scripts/Runtime/Enemies/`
- [ ] **6. <Name>_CL<n>.prefab 생성** — `Prefabs/Enemies/`
- [ ] **7. Prefab 필수 컴포넌트 부착** — Character / Health / CharacterMovement / AIBrain / Animator
- [ ] **8. AnimatorController 셋업** — 6 클립 (Idle / Walk / Telegraph / Attack / Hit / Death)
- [ ] **9. AIBrain 상태 + 트랜지션 셋업** — 6 상태 (Idle / Detect / Chase / Attack / Hit / Death)
- [ ] **10. AIDecision 거리값 셋업** — DetectRadius, AttackRange (위 2.4 권장값)
- [ ] **11. Body Collider + Hitbox + Layer / Tag 설정**
- [ ] **12. 본체 SpriteRenderer 명명 확인** — 보조 sprite 는 Shadow/VFX/Effect/Status 키워드 포함
- [ ] **13. 텔레그래프 VFX 부착** — 공격 종류 표준색 적용
- [ ] **14. 사운드 키 매핑** — spawn/hit/attack/death 4 종
- [ ] **15. EnemyCatalog_Default 에 entry 추가** — id / prefab GUID / data GUID
- [ ] **16. 룸 인카운터 풀에 추가** — 어느 스테이지의 어느 룸에 등장할지
- [ ] **17. 단독 룸 디버그 스폰 테스트** — 단독에서 정상 동작 확인
- [ ] **18. 멀티(2 인) 동기화 테스트** — 호스트/클라이언트 동작 일치 확인
- [ ] **19. CL 작업 완료 핸드오프 문서 작성** — 결정사항 / 자료구조 / 알려진 이슈

---

## 4. 명명 규칙 / 파일 위치 요약

| 항목 | 명명 | 위치 |
|---|---|---|
| SO | `EnemyData_<PascalCase>.asset` | `Assets/_Project/ScriptableObjects/Enemies/` |
| Prefab | `<PascalCase>_CL<n>.prefab` | `Assets/_Project/Prefabs/Enemies/` |
| 행동 Controller | `<PascalCase><Behavior>Controller.cs` | `Assets/_Project/Scripts/Runtime/Enemies/` |
| Catalog Id | `enemy_<role>_<variant>` (snake_case) | `EnemyCatalog_Default.asset` |
| 사운드 키 | `enemy_<cat>_<event>` | 사운드 매니저 |
| 텔레그래프 VFX prefab | `VFX_Telegraph_<AttackType>.prefab` | `Prefabs/Effect/` |

**role 예시**: `melee` / `charger` / `ranged` / `bomb` / `golem`
**variant 예시**: `basic` / `elite` / `champion`

---

## 5. 현재 6 종 적 충족도 격자

`O` = 채워짐 / `X` = 누락 / `△` = 부분 / `?` = Unity 에디터에서 확인 필요

| 카테고리 / 항목 | Orc | OrcRider | SkeletonArcher | Chobomb | StoneGolem | Moose1 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| **2.1 식별** — DisplayName | O | O | O | O | O | O |
| **2.2 데이터** — MaxHealth | O 60 | O 20 | O 50 | O 35 | O 300 | O 200 |
| **2.2 데이터** — MoveSpeed | O 3 | O 3 | O 4 | O 2.8 | O 2 | O 3 |
| **2.2 데이터** — AttackDamages | O Melee:10 | **X 빈 배열** | **X 빈 배열** | **X 빈 배열** | **X 빈 배열** | **X 빈 배열** |
| **2.2 데이터** — ExpReward | **X 0** | **X 0** | **X 0** | **X 0** | **X 0** | **X 0** |
| **2.2 데이터** — DropWeight | △ default 1 | △ default 1 | △ default 1 | △ default 1 | △ default 1 | △ default 1 |
| **2.4 AI** — Controller | ? | ? | ? | O Chobomb*Controller | O StoneGolem*Knockback | ? |
| **2.5 비주얼** — Animator 6 클립 | ? | ? | ? | ? | ? | ? |
| **2.5 비주얼** — 본체 sprite 명명 | ? | ? | ? | ? | ? | ? |
| **2.6 사운드** — 4 키 매핑 | ? | ? | ? | ? | ? | ? |
| **2.6 VFX** — 텔레그래프 prefab | ? | ? | ? | ? | ? | ? |
| **2.7 물리** — Body / Hitbox / Layer | ? | ? | ? | ? | ? | ? |
| **2.8 통합** — Catalog 등록 | O `enemy_melee_basic` | O `enemy_charger_basic` | O `enemy_ranged_basic` | O `enemy_chobomb` | ? | ? |
| **2.8 통합** — 룸 인카운터 풀 | ? | ? | ? | ? | ? | ? |
| CL 문서 — 구현 계획 | O CL-037/038 | O CL-039/040 | O CL-041/042 | O CL-212 (추정) | **X 없음** | **X 없음** |

**`?` 표기 항목은 Unity 에디터에서 prefab 인스펙터를 열어 직접 확인해야 한다.** 텍스트로 git 추적이 어려운 GUID 참조 / Animator 클립 / Layer 등이 해당된다.

---

## 6. 격자에서 즉시 드러난 누락 (우선 처리 권장)

### 6.1 AttackDamages 비어있음 — 5 종 + 보스

Orc 만 `Melee:10` 으로 채워져 있고 OrcRider / SkeletonArcher / Chobomb / StoneGolem / Moose1 + Bertha 모두 빈 배열.

- **현 상태 해석**: 데미지 값이 SO 가 아니라 Controller 컴포넌트의 인스펙터 값에 분산되어 있을 가능성. 또는 데미지 값이 미정인 상태.
- **표준화 결정 필요**: SO 에 모두 채울 것인가 (집중) vs Controller 인스펙터에 두고 SO 는 비워둘 것인가 (분산).
- **권장**: SO 가 단일 진실의 원천(SoT). 컨트롤러는 SO 의 AttackDamages 를 참조해 사용한다. 분산을 허용할 경우 SO 의 AttackDamages 가 비어있는 것이 의도임을 SO Description 또는 CL 문서에 명시.

### 6.2 ExpReward 모두 0 — 6 종 + 보스

보상 시스템이 정식 가동 전이라 일괄 0. 가동 시점에 위 2.2 의 카테고리별 권장값으로 일괄 채우는 PR 1 회 필요.

### 6.3 DropWeight 모두 default 1 — 차등 드롭 정책 부재

모든 적이 동일 가중치라 드롭 풀의 의미가 약함. 차등 정책 결정 후 일괄 적용.

### 6.4 StoneGolem / Moose1 의 Catalog 등록 미확인

`EnemyCatalog_Default.asset` 의 entries 가 4 개까지만 텍스트로 확인됨. 5~6 번째 entry 존재 여부는 Unity 에디터에서 검증 필요.

### 6.5 StoneGolem / Moose1 의 CL 구현 계획 문서 부재

`client/docs/khi/` 와 `client/docs/` 에서 CL-037~055, CL-172~177 외에 StoneGolem / Moose1 전용 문서 미발견. 사후라도 작성 권장.

---

## 7. 미정 / 향후 결정 필요

- **적 등급/티어 시스템**: `basic` / `elite` / `champion` 의 의미와 차등 룰. (현재 카탈로그 ID 의 variant 자리만 잡혀있음)
- **자폭형의 ExpReward 정책**: 자폭으로 죽으면 처치 인정인가? 인정 시 누가 처치자인가?
- **Catalog ID 와 SO 의 통합**: 현재 카탈로그 ID 와 SO 파일명이 별도. 자동 매칭 룰 도입 가능 (예: SO Description 에 ID 명시).
- **보상 풀과 EnemyData.DropWeight 의 관계**: DropWeight 는 무엇에 대한 가중치인가? (드롭 발생 자체 / 풀 안에서의 비중 / 둘 다)
- **공격 데이터의 SO 화 범위**: 사거리 / 쿨다운 / 텔레그래프 시간을 SO 로 끌어올릴지.

---

## 8. BalanceEditor 를 통한 데이터 편집

`Assets/_Project/Scripts/Editor/BalanceEditor/` 의 BalanceEditor 가 모든 EnemyData / BossData SO 를 자동 노출한다. 신규 합류자가 SO 값을 튜닝할 때의 표준 도구이며, ProjectExplorer 에서 .asset 을 일일이 클릭할 필요가 없다.

### 8.1 구조

```
┌─────────────────────────────────────────────────────────┐
│  BalanceEditorWindow (좌: 카테고리 트리 / 우: Inspector)│
├─────────────────────────────────────────────────────────┤
│  IBalanceCategoryProvider (인터페이스)                  │
│   ├─ CategoryName       — 트리 라벨                     │
│   ├─ AssetTypeFilter    — "t:EnemyData" 등 검색 필터    │
│   └─ LoadAll()          — SO 인스턴스 목록 반환         │
├─────────────────────────────────────────────────────────┤
│  Providers/ (각 도메인 1 클래스)                        │
│   ├─ EnemyDataCategoryProvider   ── "Enemies"           │
│   ├─ BossDataCategoryProvider    ── "Bosses"            │
│   ├─ WeaponCategoryProvider      ── "Weapons"           │
│   ├─ RelicCategoryProvider       ── "Relics"            │
│   ├─ BuildSetCategoryProvider    ── "BuildSets"         │
│   ├─ SkillCategoryProvider       ── "Skills"            │
│   ├─ ShopConfigCategoryProvider  ── "ShopConfig"        │
│   └─ PlayerStatsCategoryProvider ── "PlayerStats"       │
├─────────────────────────────────────────────────────────┤
│  부속 시스템                                            │
│   ├─ DirtyTracker        — 변경된 SO 추적 (* 마커)      │
│   ├─ AutoSaveController  — Debounce 자동 저장           │
│   ├─ BalanceEditorAssetWatcher — 외부 변경 자동 새로고침│
│   └─ JsonImportExport    — JSON 라운드트립              │
└─────────────────────────────────────────────────────────┘
```

신규 SO 도메인을 추가할 때는 `Providers/` 에 1 클래스를 추가하고 `BalanceEditorWindow._providers` 리스트에 한 줄 등록하면 끝난다 (CL-163 / CL-166 / CL-173 의 패턴).

### 8.2 일반 몹의 BalanceEditor 노출

`EnemyDataCategoryProvider` 가 다음을 한다.

| 항목 | 값 |
|---|---|
| CategoryName | `Enemies` |
| AssetTypeFilter | `t:EnemyData` |
| SearchFolders | `Assets/_Project/ScriptableObjects/Enemies` |
| 정확 타입 필터 | `GetType().FullName.EndsWith(".EnemyData")` — 상속 BossData 제외 |

`BossData : EnemyData` 상속 구조 때문에 `t:EnemyData` 검색은 BossData 인스턴스도 매칭한다. EnemyDataCategoryProvider 가 정확 타입 필터를 적용해 보스가 Enemies 트리에 중복 표시되지 않도록 막는다.

### 8.3 BalanceEditor 에서 편집 가능한 필드

`EnemyData` 의 모든 `[SerializeField]` 가 우측 InspectorElement 에 자동 노출된다.

- `_displayName`
- `_maxHealth`
- `_moveSpeed`
- `_attackDamages` (`EnemyAttackType` 별 배열 항목 추가/삭제)
- `_expReward`
- `_dropWeight`

값 변경 시 TreeNode 라벨에 `*` dirty 마커가 붙는다.

### 8.4 저장 메커니즘

| 방식 | 동작 |
|---|---|
| **수동 저장** | `Ctrl+S` — dirty 인 모든 SO 를 SaveAll |
| **자동 저장** (AutoSaveController) | Debounce 방식. 마지막 변경 후 N 초 (30~600 초, default 60 초) 추가 변경 없으면 자동 SaveAll. 활성화/지연 값은 EditorPrefs 에 사용자별 저장 |
| **가드** | Play 모드 / 컴파일 / asset 처리 중에는 자동 저장 발화 안 됨 |

### 8.5 외부 변경 자동 새로고침

`BalanceEditorAssetWatcher` 가 `AssetPostprocessor` 를 상속해 다음 경로의 `.asset` 파일 추가/삭제/이동/이름변경을 감지하고 카테고리 트리를 자동 갱신한다.

- `/Relics/Generated/`
- `/BuildSets/`
- **`/Enemies/`** (일반 몹/보스 둘 다 포함)

신규 `EnemyData_<Name>.asset` 을 ProjectExplorer 에서 만들면 BalanceEditor 가 열려있는 동안 즉시 Enemies 트리에 추가된다.

### 8.6 JSON Import / Export

각 SO 의 값을 JSON 으로 export 하거나 외부에서 편집한 JSON 을 import 해 SO 에 다시 적용 가능 (CL-165). 텍스트 백업 / 비-Unity 편집 워크플로우 / 밸런싱 데이터 버전 관리에 활용.

### 8.7 검색 / 필터

상단 검색창에 키워드 입력 시 매칭되는 SO 가 있는 카테고리만 펼쳐 표시. "Orc" 검색 → Enemies 카테고리만 펼쳐져 OrcRider 도 함께 노출된다.

### 8.8 실시간 수정의 한계 — 정확한 의미

"실시간" 의 두 가지 해석을 구분한다.

| 시나리오 | 지원 여부 | 비고 |
|---|---|---|
| **Edit 모드 빠른 일괄 편집** — 트리에서 적 여러 마리를 빠르게 선택하며 값 변경, Play 진입 시 적용 | ✅ | 정상 워크플로우 |
| Play 모드 중 SO 자체 값 수정 | △ | SO 변경은 가능하지만 AutoSave 는 가드로 발화 안 됨. 수동 `Ctrl+S` 만 가능 |
| Play 모드 중 **이미 스폰된 적**에게 새 SO 값 즉시 반영 | ❌ | `EnemyDataRuntimeAdapter` 가 **스폰 시점 1 회만** SO 값을 prefab 인스턴스에 적용. 새로 스폰되는 적부터 새 값 반영 |
| "보스 전투 중 체력 절반으로 즉시 줄이기" 같은 라이브 디버그 | ❌ | BalanceEditor 범위 밖. 별도 디버그 메뉴 필요 (현재 미구현) |

요약: BalanceEditor 는 **Edit 시간 빠른 일괄 편집 도구**이지 **런타임 라이브 튜닝 콘솔**이 아니다.

### 8.9 보스 한정 추가 사항

`BossData` 가 추가하는 페이즈 임계 필드의 BalanceEditor 노출과 보스 패턴 데이터의 BalanceEditor 미통합 영역은 [boss-authoring-spec.md §7](boss-authoring-spec.md#7-balanceeditor-를-통한-보스-데이터-편집) 에서 다룬다.

---

## 9. 참고

- 데이터 정의 코드: [EnemyData.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyData.cs), [BossData.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/BossData.cs)
- 상태이상 코드: [EnemyStatusEffect.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs)
- 카탈로그: [EnemyCatalog_Default.asset](../../LostMemory/Assets/_Project/ScriptableObjects/Enemies/EnemyCatalog_Default.asset)
- 폴더 / 네임스페이스 규칙: [project-structure-and-namespace.md](../commonness/project-structure-and-namespace.md)
- TopDown Engine 확장 원칙: [topdown-engine-extension-and-original-protection.md](../commonness/topdown-engine-extension-and-original-protection.md)
- 보스 명세: [boss-authoring-spec.md](boss-authoring-spec.md)
- 인게임 캡처 가이드: [capture-shot-list.md](capture-shot-list.md)
- 기존 구현 계획 문서: CL-037/038 (근접), CL-039/040 (돌진), CL-041/042 (원거리), CL-172/173 (데이터 정식화)

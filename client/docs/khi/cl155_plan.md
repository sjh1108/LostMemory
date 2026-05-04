# CL-155 — 무기 4속성 시스템 + 인챈트 결합 규칙

## Context

Epic K (무기 확장)의 첫 ticket. **WeaponData 에 속성 추가 + Epic S 인챈트와의 결합 규칙 정의**.

3점 P2, 클라1, CL-090 의존.

### 기존 상태 (확인 완료)

- ✅ `WeaponData` SO (CL-090) — `_displayName`, `_baseDamage`, `_steps[]`, **속성 필드 없음**
- ✅ `AttackStepData` — combo step 별 damage/hitbox/visual
- ✅ Epic S CL-142 의 OnHit 인챈트 트리거 — `BurnOnHit` / `SlowOnHit` / `FreezeOnHit` / `ChainOnHit` / `WindAOE`
- ✅ `OnHitEffectRegistry` (CL-142) — on-hit 발동 진입점
- ❌ 무기 속성 enum / 필드 — **본 CL 신설**
- ❌ 무기 속성 ↔ 빌드 인챈트 결합 규칙 — **본 CL 정의**

### 회의록 미정 사항 (tickets-master §결정 미정)

> 본 plan 에서 디폴트 결정 → 사용자 확인.

1. **무기 시작 속성: 고정 vs 선택**
2. **인챈트가 무기 속성 덮어쓰기 vs 추가**

### 본 CL 책임 범위

1. `WeaponElement` enum (None/Fire/Ice/Lightning/Wind) 정의
2. `WeaponData` 에 속성 필드 추가
3. **인챈트 결합 규칙** 코드 + 문서화
4. `OnHitEffectRegistry` 에 무기 속성 컨텍스트 전달
5. 시각/사운드 hook (placeholder)

→ **무기 트리 (검/활/봉/스태프) 구현은 CL-156~160**. 본 CL 은 **데이터 + 결합 로직** 만.

---

## 결정사항

### 1. WeaponElement enum

```csharp
public enum WeaponElement
{
    None = 0,    // 무속성 (대부분 기본 무기)
    Fire,        // 불 - BurnOnHit (도트)
    Ice,         // 얼음 - SlowOnHit + FreezeOnHit
    Lightning,   // 전기 - ChainOnHit
    Wind,        // 바람 - WindAOE
}
```

→ Epic S 의 `RelicTag.Fire/Ice/Lightning/Wind` 와 1:1 대응. 매핑 헬퍼 신설.

### 2. WeaponData 속성 필드

```csharp
[Header("Element (CL-155)")]
[Tooltip("무기 자체의 속성. 평타/스킬 적중 시 본 속성의 OnHit 트리거 함께 발동.")]
[SerializeField] private WeaponElement _element = WeaponElement.None;

[Tooltip("무기 속성의 OnHit magnitude (속성 빌드 1스택 기본값과 동일).")]
[SerializeField, Min(0f)] private float _elementMagnitude = 0.10f;

public WeaponElement Element => _element;
public float ElementMagnitude => _elementMagnitude;
```

→ 무기에 속성 있으면 평타마다 해당 속성의 OnHit 1티어 효과 자동 발동 (빌드 무관).

### 3. 무기 시작 속성 정책 — **무기별 고정** (회의록 결정)

**옵션**:
- (a) **무기별 고정** ⭐ — WeaponData SO 의 `_element` 가 무기 정체성
- (b) 선택 (런 시작 시 플레이어 선택) — 메뉴 UI 추가 필요
- (c) 무기 강화 분기에 따라 결정 (CL-156 의 강화 트리)

**채택: (a) + (c) 보강**.
- 기본 무기 = `WeaponElement.None` (대부분)
- 강화 분기 시 속성 부여 (예: 활 → 화염방사 = Fire, 활 → 유탄 = None+폭발)
- 런 중 변경 X

→ CL-156~160 의 무기 트리 prefab 마다 SO 별 _element 미리 설정.

### 4. 인챈트 결합 규칙 — **추가 (덮어쓰기 X)** ⭐

**옵션**:
- (a) **빌드 인챈트가 무기 속성에 추가** ⭐ — 무기 불 + 빌드 얼음 → 둘 다 발동
- (b) 빌드 인챈트가 무기 속성 덮어쓰기 — 무기 불 무시하고 빌드 얼음만
- (c) 한 속성만 가능 (충돌 시 우선순위)

**채택: (a)**.
- 빌드 자유도 높음 (무기 속성과 빌드 인챈트 별개)
- 시너지 가능 (무기 불 + 빌드 불 → 같은 속성 중복 처리 §5)
- 단점: OP 우려 (모든 속성 동시 발동) → §5 중복 회피

### 5. 같은 속성 중복 처리 — **자동 합산**

무기 불 (magnitude 0.10) + 빌드 불 3스택 (magnitude 0.30) → 같은 BurnOnHit 발동.

```
final BurnOnHit magnitude = 무기 0.10 + 빌드 0.30 = 0.40
```

→ OnHitEffectRegistry 가 동일 EffectType 합산 처리. 두 번 발동 X.

### 6. 다른 속성 동시 발동 — 모두 발동

무기 불 + 빌드 얼음 5세트 → 평타 적중 시:
- BurnOnHit (도트) — 무기 0.10
- SlowOnHit + FreezeOnHit — 빌드 magnitudes

→ 두 속성 모두 발동. 서로 영향 X.

### 7. OnHitEffectRegistry 통합

```csharp
// KhiMeleeHitbox 또는 평타 적중 처리 시
public void OnHit(Health target, AttackContext ctx)
{
    var weapon = ctx.WeaponData;

    // 1. 무기 속성 OnHit (있으면)
    if (weapon.Element != WeaponElement.None)
    {
        var weaponEffectType = WeaponElementMapping.ToOnHitEffect(weapon.Element);
        onHitRegistry.Trigger(weaponEffectType, weapon.ElementMagnitude, target);
    }

    // 2. 빌드 인챈트 OnHit (기존 CL-142 흐름)
    onHitRegistry.TriggerAll(target);   // 빌드에서 등록된 모든 OnHit 효과
}
```

`WeaponElementMapping.ToOnHitEffect`:
```csharp
public static class WeaponElementMapping
{
    public static RelicEffectType ToOnHitEffect(WeaponElement e) => e switch
    {
        WeaponElement.Fire      => RelicEffectType.BurnOnHit,
        WeaponElement.Ice       => RelicEffectType.SlowOnHit,    // Freeze 는 빌드만
        WeaponElement.Lightning => RelicEffectType.ChainOnHit,
        WeaponElement.Wind      => RelicEffectType.WindAOE,
        _                       => RelicEffectType.None,
    };

    public static RelicTag ToTag(WeaponElement e) => e switch
    {
        WeaponElement.Fire      => RelicTag.Fire,
        WeaponElement.Ice       => RelicTag.Ice,
        WeaponElement.Lightning => RelicTag.Lightning,
        WeaponElement.Wind      => RelicTag.Wind,
        _                       => RelicTag.None,
    };
}
```

⚠️ **얼음 무기**는 SlowOnHit 만 (Freeze 는 빌드 3티어 전용). 무기 단독으로 빙결까지는 OP.

### 8. 시각 / 사운드 — placeholder

무기 속성별 시각:
- 평타 적중 시 속성별 색상 파티클 (Fire=빨강, Ice=파랑, Lightning=노랑, Wind=초록)
- placeholder: SpriteRenderer.color tint 또는 단순 ParticleSystem

본 CL: **AttackStepData.slashTint 를 element 색으로 자동 오버라이드 (옵션)** 또는 별도 ParticleSystem.

→ 정식 시각은 별도 ticket. 본 CL 은 hook 만.

### 9. 빌드 카운트 영향 X

무기 속성은 BuildManager 의 RelicTag 카운트에 **영향 없음**. 빌드 인챈트 카운트는 보유 ItemData 기준만.

```
무기: Fire
빌드 보유: Fire 태그 ItemData 2개
→ BuildManager.GetSetCount(Fire) = 2 (무기 미포함)
```

이유: 무기는 1개고 빌드는 다수 ItemData 카운트 기반. 합치면 카운트 의미 헷갈림.

→ 무기 속성은 OnHit 직접 발동만 (BurnOnHit magnitude 0.10 추가).

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Data/WeaponElement.cs` | enum (None/Fire/Ice/Lightning/Wind) |
| `Assets/_Project/Scripts/Runtime/Data/WeaponElementMapping.cs` | enum ↔ RelicEffectType / RelicTag 변환 |
| `docs/khi/cl155_weapon_enchant_combination_rules.md` | 결합 규칙 문서 (디자이너용) |

### 수정

| 경로 | 변경 |
|---|---|
| `WeaponData.cs` | `_element` / `_elementMagnitude` 필드 + 프로퍼티 |
| `KhiMeleeHitbox.cs` 또는 `KhiMeleeComboController.cs` | 평타 적중 시 무기 속성 OnHit 트리거 |
| `OnHitEffectRegistry.cs` (CL-142) | `Trigger(EffectType, magnitude, target)` 단일 발동 메서드 추가 (이미 있으면 skip) |

### 영향 (디자이너 작업, 후속 ticket 포함)

| 경로 | 변경 |
|---|---|
| 기존 WeaponData asset (몇 개 있는지 미정) | 모두 `_element = None` 디폴트 |
| CL-156~160 의 신규 무기 SO | 트리 분기에 따라 `_element` 설정 |

---

## 구현 단계

### 1단계: WeaponElement enum (10분)

`WeaponElement.cs` 작성. 5개 값.

### 2단계: WeaponData 필드 + 프로퍼티 (15분)

§2 코드. SerializedField + 프로퍼티.

### 3단계: WeaponElementMapping 유틸 (15분)

§7 코드. switch expression 2개.

### 4단계: 평타 적중 hook (45분)

1. `KhiMeleeHitbox` 또는 적중 처리 컴포넌트 위치 확인
2. 적중 시점에 무기 속성 OnHit 추가 트리거:

```csharp
// KhiMeleeHitbox.OnHit (또는 그 호출 측)
private void OnEnemyHit(Health target)
{
    // 기존 빌드 OnHit (CL-142)
    onHitRegistry.TriggerAll(target);

    // 신규: 무기 속성 OnHit
    if (weaponData.Element != WeaponElement.None)
    {
        var effectType = WeaponElementMapping.ToOnHitEffect(weaponData.Element);
        onHitRegistry.Trigger(effectType, weaponData.ElementMagnitude, target);
    }
}
```

3. `OnHitEffectRegistry.Trigger(type, mag, target)` 단일 발동 메서드 신설 (없으면)

### 5단계: 같은 속성 합산 (15분)

`onHitRegistry.TriggerAll` 와 단일 `Trigger` 가 같은 EffectType 두 번 발동하면 중복:
- (a) Registry 가 자동 합산 (1번만 발동, magnitude 합)
- (b) 호출 측에서 중복 검사

→ **(a) 채택**. Registry 내부에서 turn 단위로 같은 EffectType 들의 magnitude 합산 후 1번만 발동.

```csharp
// OnHitEffectRegistry 내부
private void Resolve()
{
    var grouped = _pendingTriggers.GroupBy(t => t.Type);
    foreach (var g in grouped)
    {
        float totalMag = g.Sum(t => t.Magnitude);
        ApplyEffect(g.Key, totalMag, _currentTarget);
    }
    _pendingTriggers.Clear();
}
```

→ Registry 구조 변경 영향. 기존 CL-142 trigger 흐름 점검 필요.

⚠️ **위험**: CL-142 OnHitEffectRegistry 가 즉시 발동 구조면 합산 로직 추가 부담. → MVP 는 **합산 X, 각각 발동** (BurnOnHit 두 번 발동 = 도트 두 번 등록 → 데미지 두 번). 후속에서 합산 도입.

→ **MVP 결정: 각각 발동** (단순). 합산은 후속 ticket.

### 6단계: 시각 placeholder (15분)

```csharp
// AttackStepData 의 slashTint 가 element 색으로 자동? 또는 별도 파티클?
// MVP: 별도 ParticleSystem (속성별 prefab) 또는 단순 Color flash

// 평타 발동 시
if (weapon.Element != WeaponElement.None)
{
    var color = WeaponElementMapping.ToColor(weapon.Element);
    PlayHitParticle(target.transform.position, color);
}
```

`WeaponElementMapping.ToColor`:
```csharp
public static Color ToColor(WeaponElement e) => e switch
{
    WeaponElement.Fire      => new Color(1f, 0.3f, 0f),
    WeaponElement.Ice       => new Color(0.3f, 0.8f, 1f),
    WeaponElement.Lightning => new Color(1f, 1f, 0.3f),
    WeaponElement.Wind      => new Color(0.4f, 1f, 0.5f),
    _                       => Color.white,
};
```

### 7단계: 결합 규칙 문서 (20분)

`cl155_weapon_enchant_combination_rules.md`:
- 무기 속성 = 평타마다 자동 OnHit 발동
- 빌드 인챈트 = ItemData 카운트 기반
- 둘 다 있으면 **각각 발동** (MVP)
- 같은 속성 중복 시 두 번 발동 (MVP, 후속에서 합산)
- 빌드 카운트에 무기 속성은 계산 안 됨

→ 디자이너용 1페이지.

### 8단계: 검증 (45분)

```
시나리오 1: 무기 None + 빌드 None
- 평타 시 OnHit 효과 없음

시나리오 2: 무기 Fire + 빌드 None
- 평타 시 BurnOnHit 발동 (도트 시작) — magnitude 0.10
- 적 HP 도트 감소 확인

시나리오 3: 무기 None + 빌드 Fire 5세트
- 평타 시 BurnOnHit 발동 (빌드 magnitude)

시나리오 4: 무기 Fire + 빌드 Fire 5세트
- 평타 시 BurnOnHit 두 번 발동 (각각 0.10 + 빌드 magnitude)
- MVP: 데미지 두 배. 합산 후속 ticket

시나리오 5: 무기 Fire + 빌드 Ice 5세트
- 평타 시 BurnOnHit + SlowOnHit + FreezeOnHit 모두 발동
- 적 슬로우 + 빙결 + 도트

시나리오 6: 무기 Ice
- 평타 시 SlowOnHit 발동 (FreezeOnHit X — 빌드 3티어 전용)

시나리오 7: 무기 Lightning + 인접 적 다수
- 평타 시 ChainOnHit 발동 (인접 1마리 추가 데미지)

시나리오 8: 무기 Wind
- 평타 시 WindAOE 발동 (광역)

시나리오 9: 시각 확인
- 무기 Fire → 적 적중 시 빨간 파티클
- 무기 Ice → 파란 파티클
- 무기 Lightning → 노란 파티클
- 무기 Wind → 초록 파티클
```

---

## 위험 / 결정 미정

### 위험

1. **OnHitEffectRegistry 구조 변경 부담**: 합산 로직이 기존 즉시 발동 흐름과 충돌 가능. → MVP 각각 발동, 합산 후속. 테스트로 OP/약체 판단 후 결정.
2. **무기 단독 빙결 OP**: Ice 무기가 매 평타 SlowOnHit + FreezeOnHit 모두 발동하면 적 영구 빙결. → §7 Mapping 에서 Ice → SlowOnHit 만. Freeze 는 빌드 3티어 전용 명시.
3. **시각 파티클 prefab 미존재**: placeholder Color flash 또는 단순 ParticleSystem. 정식 VFX 별도 ticket.
4. **무기 속성 변경 시점 X**: 본 CL 은 런 중 변경 X 가정. CL-156 강화 시스템에서 무기 변경 시 평타 hitbox 재바인딩 필요 (별도 plan).
5. **AttackContext / 적중 처리 위치**: KhiMeleeHitbox 가 정확히 어디서 적중 처리하는지 확인 후 hook. CL-142 수정 위치 참조.
6. **빌드 카운트에 무기 미포함 정책**: 디자이너 직관과 다를 수 있음 ("무기 불 + 빌드 불 4 = 5스택?" 같은 기대). 명시적 문서화 (§7 결합 규칙).
7. **AttackStepData per-step element**: 콤보 1타/2타/3타 마다 다른 속성 가능? 본 plan: **무기 단위만 (step 단위 X)**. 후속 확장 가능.

### 결정 미정

- [ ] 무기 시작 속성 — 본 plan: **무기별 고정 (대부분 None, 속성 무기는 SO에 명시)**
- [ ] 인챈트 결합 — 본 plan: **추가 (덮어쓰기 X)**
- [ ] 같은 속성 중복 — 본 plan: **MVP 각각 발동, 합산 후속**
- [ ] Ice 무기 Freeze 가능 — 본 plan: **불가능 (Slow 만)**
- [ ] 빌드 카운트에 무기 포함 — 본 plan: **포함 X**
- [ ] 시각 파티클 정식 — 본 plan: **placeholder, 별도 ticket**
- [ ] AttackStep 단위 속성 — 본 plan: **무기 단위만**

---

## 후속 ticket 영향

| Ticket | CL-155 와의 관계 |
|---|---|
| **CL-156 (단계 강화)** | 강화 분기에 따라 무기 속성 변경 (예: 활 → 화염방사 = Fire) |
| **CL-157 (검 트리)** | 단검/방패검/대검 SO 의 _element 설정 |
| **CL-158 (활 트리)** | 노/총/화염방사·유탄 SO — 화염방사 = Fire |
| **CL-159 (봉 트리)** | 여의봉/창/망치 SO |
| **CL-160 (스태프 트리)** | 공속/마법사/비숍 SO |
| **CL-161 (무기 QA)** | 본 CL 의 결합 규칙 검증 |
| **별도 ticket: OnHit 합산** | 같은 속성 중복 시 magnitude 합산 |
| **별도 ticket: 정식 속성 VFX** | placeholder 파티클 → 정식 |
| **별도 ticket: AttackStep 단위 속성** | 콤보 step 별 다른 속성 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (WeaponElement enum) | 10분 |
| 2단계 (WeaponData 필드) | 15분 |
| 3단계 (WeaponElementMapping) | 15분 |
| 4단계 (평타 적중 hook) | 45분 |
| 5단계 (중복 처리 — MVP 각각) | 15분 |
| 6단계 (시각 placeholder) | 15분 |
| 7단계 (결합 규칙 문서) | 20분 |
| 8단계 (검증 9 시나리오) | 45분 |
| **합계** | **약 3시간** |

→ 3점 ticket 에 부합.

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 무기 시작 속성 | **고정** / 선택 | **고정** (무기 정체성) |
| 2 | 인챈트 결합 | **추가** / 덮어쓰기 / 충돌해결 | **추가** (자유도) |
| 3 | 같은 속성 중복 | **각각 발동** (MVP) / 합산 | **각각** (단순) |
| 4 | Ice 무기 Freeze | 가능 / **불가능** | **불가능** (OP 회피) |
| 5 | 빌드 카운트 무기 포함 | 포함 / **미포함** | **미포함** (직관 분리) |
| 6 | 시각 파티클 | placeholder / 정식 | **placeholder** |
| 7 | AttackStep 단위 속성 | 본 CL / **별도** | **별도** |

전부 추천대로면 **고정 + 추가 + 각각 + Freeze불가 + 카운트미포함 + placeholder + step별도**.

---

## Epic K 진행률 (CL-155 후)

| Ticket | Plan |
|---|---|
| CL-090 무기 SO 구조 | ✅ (사전 완료) |
| **CL-155 4속성 + 결합** | ✅ ← 방금 |
| CL-156 단계 강화 | ⏳ |
| CL-157 검 트리 | ⏳ |
| CL-158 활 트리 | ⏳ |
| CL-159 봉 트리 | ⏳ |
| CL-160 스태프 트리 | ⏳ |
| CL-161 무기 QA | ⏳ |

**Epic K: 1/7 (CL-090 제외)**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-156 단계 강화 시스템 | 3점 | 본 CL 직접 후속, 강화 분기에 속성 부여 |
| B | CL-157 검 트리 | 5점 | CL-156 의존 (강화 시스템 먼저) |
| C | Epic U 진입 (CL-162) | - | 디자이너 도구 (병렬 가능) |

**추천: A (CL-156)** — Epic K 핵심 인프라 (강화 = 트리 분기). CL-157~160 의 전제.

뭐로 갈까요?

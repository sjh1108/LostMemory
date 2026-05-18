# CL-160 — 스태프 트리 (공속 / 마법사 / 비숍) + 스킬 2종

## Context

Epic K 마지막 콘텐츠 ticket. **활 Projectile 인프라 활용 + 스킬 시스템 신설**.

5점 P2, 클라1, CL-156 의존.

### 본 CL 의 두 축

1. **스태프 트리 7개 SO** — 활 Projectile (CL-158) 패턴 활용
2. **스킬 시스템 신설** — 평타와 별도 입력 + 쿨다운 + 발동 (Q/E 키)

→ 스킬 시스템은 **무기 트리 4개 모두 사용 가능한 인프라**. 스태프에서 첫 활용 + 스킬 2종 추가.

### 검 vs 활 vs 스태프 비교

| 항목 | 검 (CL-157) | 활 (CL-158) | 스태프 (본 CL) |
|---|---|---|---|
| 공격 방식 | Melee 평타 | Projectile | **Projectile (활 재활용)** |
| 컨트롤러 | KhiMeleeComboController | KhiBowController | **KhiBowController 재활용** |
| 스킬 | X | X | **신규 (Q/E 2종)** |

→ 스태프 평타는 활과 동일 (Projectile). 차별화는 스킬 시스템.

### 기존 상태 (확인 완료)

- ✅ `WeaponData` + AttackKind enum (CL-158)
- ✅ `KhiBowController` + `KhiProjectile` (CL-158) — 스태프 평타에 재활용
- ✅ `WeaponElement` (CL-155), `_upgrades` (CL-156)
- ✅ Epic S CL-143 의 스킬 친화 인챈트 (Cooldown / Fire / Wind on-hit)
- ❌ 스태프 트리 7개 SO — **본 CL 신설**
- ❌ **스킬 시스템 (PlayerSkillController + SkillData SO)** — **본 CL 신설**
- ❌ 스킬 2종 prefab + 발동 효과 — **본 CL 신설**

### 본 CL 책임 범위 (3축)

1. **스태프 트리 7개 SO** (Stage 0 + Stage 1 ×3 + Stage 2 ×3)
2. **스킬 시스템 인프라** — Q/E 키 입력 + 쿨다운 + 발동
3. **스킬 2종** — 메테오 (광역 폭발) + 힐 (자가 회복)
4. PlayerWeaponLoadout 통합 — 스태프 장착 시 스킬 활성

→ **5점 표 초과 예상** (스킬 인프라 신설). 분할 권장.

---

## 결정사항

### 1. 스태프 트리 구조 (확정)

```
스태프 (Staff_Default, Stage 0)            _element: None      / 균형형 마법구체
 ├─ 공속 스태프 (Stage 1)                  _element: None      / 빠른 연사 (Bow_Gun 패턴)
 │   └─ 라피드 스태프 (Stage 2)            _element: Lightning / 더 빠름 + 체인
 ├─ 마법사 스태프 (Stage 1)                _element: Fire      / 강력한 단발 + Burn
 │   └─ 아크메이지 스태프 (Stage 2)        _element: Fire      / 더 강한 + 큰 폭발
 └─ 비숍 스태프 (Stage 1)                  _element: None      / 지원 (스킬 쿨감)
     └─ 하이 비숍 스태프 (Stage 2)         _element: None      / 지원 ↑↑ (스킬 데미지 +)
```

**Stage 2 속성**:
- 라피드 = Lightning (Chain on-hit)
- 아크메이지 = Fire (강한 Burn)
- 비숍/하이비숍 = None (지원 정체성, 속성 X)

### 2. 무기별 수치 차별화

| 무기 | Stage | Element | ProjPerShot | Cooldown | ProjDmg | SkillCDMul | SkillDmgMul | UpgradeCost |
|---|---|---|---|---|---|---|---|---|
| **스태프** | 0 | None | 1 | 0.5s | 7 | 1.0× | 1.0× | 0 |
| **공속 스태프** | 1 | None | 1 | 0.2s | 5 | 1.0× | 1.0× | 100 |
| **마법사 스태프** | 1 | Fire | 1 | 0.6s | 12 | 1.0× | 1.2× | 100 |
| **비숍 스태프** | 1 | None | 1 | 0.5s | 6 | 0.7× (-30%) | 1.0× | 100 |
| **라피드 스태프** | 2 | Lightning | 1 | 0.15s | 5 | 1.0× | 1.0× | 300 |
| **아크메이지 스태프** | 2 | Fire | 1 | 0.6s | 16 | 1.0× | 1.5× | 300 |
| **하이 비숍 스태프** | 2 | None | 1 | 0.5s | 7 | 0.6× | 1.3× | 300 |

**`SkillCDMul`** / **`SkillDmgMul`** 은 본 CL 의 신규 무기 필드 — 스킬에 영향:
- 비숍/하이비숍은 스킬 쿨감 ↑ (지원 정체성)
- 마법사/아크메이지는 스킬 데미지 ↑ (마법 정체성)

### 3. WeaponData 신규 필드 — 스킬 영향

```csharp
[Header("Skill Modifiers (CL-160)")]
[Tooltip("이 무기 장착 시 스킬 쿨다운 배율. 1.0 = 기본, 0.7 = 30% 감소.")]
[SerializeField, Min(0f)] private float _skillCooldownMultiplier = 1f;

[Tooltip("이 무기 장착 시 스킬 데미지 배율. 1.0 = 기본, 1.5 = 50% 증가.")]
[SerializeField, Min(0f)] private float _skillDamageMultiplier = 1f;

public float SkillCooldownMultiplier => _skillCooldownMultiplier;
public float SkillDamageMultiplier => _skillDamageMultiplier;
```

→ 검/활/봉 트리는 디폴트 1.0× (영향 X).

### 4. 스킬 시스템 — SkillData SO + PlayerSkillController

**SkillData SO**:
```csharp
[CreateAssetMenu(fileName = "Skill_New", menuName = "LostMemory/Combat/SkillData")]
public class SkillData : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField] private SkillKind _kind;          // Damage / Heal / Buff
    [SerializeField] private float _baseCooldown = 5f;
    [SerializeField] private float _baseDamage;        // Damage 스킬 전용
    [SerializeField] private float _baseHealAmount;    // Heal 스킬 전용
    [SerializeField] private float _radius;            // 광역 반경
    [SerializeField] private GameObject _vfxPrefab;    // 시각

    public string DisplayName => _displayName;
    public SkillKind Kind => _kind;
    public float BaseCooldown => _baseCooldown;
    public float BaseDamage => _baseDamage;
    public float BaseHealAmount => _baseHealAmount;
    public float Radius => _radius;
    public GameObject VfxPrefab => _vfxPrefab;
}

public enum SkillKind { Damage, Heal, Buff }
```

**PlayerSkillController**:
```csharp
public class PlayerSkillController : MonoBehaviour
{
    [SerializeField] private SkillData _skill1;        // Q 키
    [SerializeField] private SkillData _skill2;        // E 키
    [SerializeField] private PlayerWeaponLoadout _loadout;
    [SerializeField] private Health _playerHealth;

    private float _cd1, _cd2;

    public event Action<int, float> OnCooldownStarted;  // (slot, duration)
    public event Action<int> OnSkillReady;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q)) TryActivate(0);
        if (Input.GetKeyDown(KeyCode.E)) TryActivate(1);
        TickCooldowns();
    }

    private void TryActivate(int slot)
    {
        var skill = (slot == 0) ? _skill1 : _skill2;
        var cd = (slot == 0) ? _cd1 : _cd2;
        if (skill == null || cd > 0) return;

        Activate(skill);

        float effectiveCd = skill.BaseCooldown * _loadout.CurrentWeapon.SkillCooldownMultiplier;
        if (slot == 0) _cd1 = effectiveCd;
        else _cd2 = effectiveCd;
        OnCooldownStarted?.Invoke(slot, effectiveCd);
    }

    private void Activate(SkillData skill)
    {
        switch (skill.Kind)
        {
            case SkillKind.Damage:
                ApplyDamage(skill);
                break;
            case SkillKind.Heal:
                _playerHealth.ReceiveHealth(skill.BaseHealAmount, gameObject);
                break;
        }
        SpawnVfx(skill);
    }

    private void ApplyDamage(SkillData skill)
    {
        float dmg = skill.BaseDamage * _loadout.CurrentWeapon.SkillDamageMultiplier;
        var hits = Physics2D.OverlapCircleAll(ResolveTargetPoint(skill), skill.Radius);
        foreach (var h in hits)
        {
            var hh = h.GetComponentInParent<Health>();
            if (hh != null) hh.Damage(dmg, gameObject, ...);
        }
    }
}
```

### 5. 스킬 2종 (확정)

#### 스킬 1: 메테오 (Q)
- Kind: Damage
- BaseCooldown: 8초
- BaseDamage: 30
- Radius: 2.5 유닛
- 발동 위치: 마우스 위치 (또는 가장 가까운 적)
- Vfx: 빨간 폭발 ParticleSystem (placeholder)

#### 스킬 2: 힐 (E)
- Kind: Heal
- BaseCooldown: 15초
- BaseHealAmount: 30
- Vfx: 초록 빛 흡수 ParticleSystem (placeholder)

→ 메테오는 Damage 정체성, 힐은 Survival 정체성. 명확한 둘의 역할 분리.

### 6. 스킬 활성 조건 — 스태프 장착 시

본 CL: **스태프 무기 장착 시에만 스킬 사용 가능**.

```csharp
private void TryActivate(int slot)
{
    var weapon = _loadout.CurrentWeapon;
    bool isStaff = weapon != null && weapon.name.StartsWith("Staff_");
    if (!isStaff)
    {
        ToastNotifier.Show("스킬은 스태프 장착 시에만 사용 가능합니다");
        return;
    }
    // 기존 발동 로직
}
```

→ 향후 다른 무기에서도 사용 (별도 ticket). MVP 는 스태프 전용.

→ 또는 `WeaponData._allowsSkill: bool` 플래그 권장 — 무기 SO 에 명시. **본 plan 채택**.

```csharp
[Tooltip("이 무기 장착 시 스킬 사용 가능 여부.")]
[SerializeField] private bool _allowsSkill = false;
public bool AllowsSkill => _allowsSkill;
```

→ 스태프 7개 모두 _allowsSkill = true. 검/활/봉은 false.

### 7. 스킬 슬롯 정책 — 무기 무관 고정 2종

본 CL: **메테오 + 힐 고정 2종**. 무기 따라 스킬 변경 X.

향후 확장:
- 스태프별 다른 스킬 (마법사 → 메테오, 비숍 → 힐 강화)
- 보상으로 스킬 추가/교체

→ 별도 ticket.

### 8. 시각 — 활 Projectile 패턴 + 스킬 vfx placeholder

스태프 평타: `Projectile_MagicOrb.prefab` (마법구체)
- 활의 Projectile_Bullet 패턴 + 보라색 sprite

스킬:
- 메테오 vfx: 빨간 ParticleSystem 또는 단순 Color flash
- 힐 vfx: 초록 ParticleSystem

정식 vfx 별도 ticket.

### 9. UI — 스킬 쿨다운 표시 (placeholder)

기존 HUD (CL-148) 옆에 스킬 슬롯 2개:
```
┌──────────┐
│ Q  E     │
│ 메테오 힐 │
│ [▒▒8s] [▒15s] │
└──────────┘
```

→ 본 CL: **placeholder UI** (단순 텍스트 + 쿨다운 카운트다운). 정식 UI 별도 ticket.

### 10. SO 파일 명명

```
Assets/_Project/ScriptableObjects/Weapons/
  Staff_Default.asset
  Staff_RapidFire.asset       (공속 스태프)
  Staff_Mage.asset             (마법사 스태프)
  Staff_Bishop.asset           (비숍 스태프)
  Staff_Rapid.asset            (라피드, Stage 2)  ← 명명 충돌 회피
  Staff_Archmage.asset         (아크메이지)
  Staff_HighBishop.asset       (하이 비숍)

Assets/_Project/ScriptableObjects/Skills/
  Skill_Meteor.asset           (메테오)
  Skill_Heal.asset             (힐)
```

→ 공속 스태프 / 라피드 스태프 명명 충돌 회피: `Staff_RapidFire` (공속) vs `Staff_Rapid` (라피드, Stage 2).

---

## 핵심 파일

### 신규 (SO + Prefab)

| 경로 | 내용 |
|---|---|
| `Staff_Default.asset` | 스태프 (Stage 0) |
| `Staff_RapidFire.asset` | 공속 스태프 (Stage 1) |
| `Staff_Mage.asset` | 마법사 스태프 (Stage 1, Fire) |
| `Staff_Bishop.asset` | 비숍 스태프 (Stage 1) |
| `Staff_Rapid.asset` | 라피드 (Stage 2, Lightning) |
| `Staff_Archmage.asset` | 아크메이지 (Stage 2, Fire) |
| `Staff_HighBishop.asset` | 하이 비숍 (Stage 2) |
| `Skill_Meteor.asset` | 메테오 SkillData |
| `Skill_Heal.asset` | 힐 SkillData |
| `Projectile_MagicOrb.prefab` | 마법구체 |

### 신규 코드

| 경로 | 내용 |
|---|---|
| `Runtime/Combat/SkillData.cs` | SkillData SO 정의 |
| `Runtime/Combat/SkillKind.cs` | enum (Damage/Heal/Buff) |
| `Runtime/Combat/PlayerSkillController.cs` | 입력 + 쿨다운 + 발동 |
| `Runtime/UI/SkillSlotView.cs` | UI 쿨다운 표시 (placeholder) |
| `docs/khi/cl160_staff_skill_balance.md` | 수치 가이드 |

### 수정

| 경로 | 변경 |
|---|---|
| `WeaponData.cs` | `_skillCooldownMultiplier` / `_skillDamageMultiplier` / `_allowsSkill` 필드 (3개) |
| `PlayerWeaponLoadout` (CL-156) | (변경 없음 — 컨트롤러는 활 KhiBowController 재활용) |
| 기존 검/활/봉 SO 모두 | `_allowsSkill = false` 확인 (디폴트라 안전) |

---

## 구현 단계

### 작업 단위 A — 스킬 인프라 (3시간)

#### 1단계: SkillData SO + SkillKind enum (30분)

§4 코드. SO + enum.

#### 2단계: WeaponData 스킬 필드 추가 (15분)

§3 + §6 코드. 3개 필드.

#### 3단계: PlayerSkillController (1시간 30분)

§4 코드:
- Update 입력 (Q/E)
- 쿨다운 tick
- _allowsSkill 가드
- Damage/Heal 분기
- vfx spawn
- 이벤트 발화 (UI 용)

#### 4단계: SkillSlotView UI placeholder (45분)

- Q/E 슬롯 2개
- 스킬 이름 + 쿨다운 카운트다운
- OnCooldownStarted 구독
- 단순 Image fillAmount + Text

### 작업 단위 B — 스태프 콘텐츠 (2시간 30분)

#### 5단계: Skill_Meteor / Skill_Heal SO (15분)

§5 수치 입력.

#### 6단계: Projectile_MagicOrb prefab (15분)

활 Projectile 패턴 + 보라색 sprite.

#### 7단계: 스태프 SO 7개 (1시간 30분)

§2 표 입력. 활 SO 패턴 그대로 + _allowsSkill = true + skill multiplier.

#### 8단계: _upgrades 입력 (15분)

§1 트리.

#### 9단계: 수치 가이드 문서 (20분)

`cl160_staff_skill_balance.md`.

### 작업 단위 C — 검증 (1시간 30분)

```
시나리오 1: 스태프 시작 (디버그 swap)
- Staff_Default 로 swap
- 평타 → MagicOrb 발사 (보라)
- Q → 메테오 발동, 적 광역 데미지 30
- E → 힐 발동, HP 30 회복

시나리오 2: 검 장착 시 스킬 X
- 검으로 swap
- Q/E 눌러도 발동 X (allowsSkill = false)
- 토스트 표시 또는 무반응

시나리오 3: 비숍 스태프
- Q 메테오 쿨다운 8 × 0.7 = 5.6초
- 빠른 메테오

시나리오 4: 마법사 스태프
- Q 메테오 데미지 30 × 1.2 = 36
- 강한 메테오

시나리오 5: 아크메이지 스태프 (Stage 2)
- Q 메테오 데미지 30 × 1.5 = 45
- 가장 강한 메테오

시나리오 6: 하이 비숍 (Stage 2)
- Q 메테오 쿨다운 8 × 0.6 = 4.8초
- E 힐 쿨다운 15 × 0.6 = 9초
- 데미지도 1.3× 증가

시나리오 7: 빌드 인챈트와 결합
- 마법사 스태프 + 빌드 Fire 5세트 → 평타 BurnOnHit 두 번 (CL-155 MVP)
- 메테오는 자체 데미지 (BurnOnHit X — Damage 스킬과 OnHit 분리)

시나리오 8: 빌드 쿨감 (CL-143 Cooldown 인챈트)
- 빌드 쿨감 3세트 → 스킬 쿨다운 추가 감소 (CL-143 효과)
- 비숍 스태프와 합치면 더 빠름
- ⚠️ 본 plan 결정: 빌드 쿨감과 무기 SkillCDMul 곱연산 (1 × 0.7 × 0.5 = 0.35배 = -65%)

시나리오 9: 라피드 스태프 (Stage 2 Lightning)
- 평타 빠른 발사 + ChainOnHit (인접 적 추가)

시나리오 10: 시각
- 메테오 vfx (빨간 폭발)
- 힐 vfx (초록 빛)
- 평타 MagicOrb (보라)
```

---

## 위험 / 결정 미정

### 위험

1. **스킬 시스템 신설 부담 — 5점 ticket 표 초과**: 인프라 (3h) + 콘텐츠 (2.5h) + 검증 (1.5h) = **7시간**. 분할 권장.
2. **빌드 쿨감과 무기 SkillCDMul 곱연산 명확화 필요**: 둘 다 적용 시 어떻게? 곱셈 vs 합산. → **본 plan: 곱셈** (스택 가능). 디자이너 의도와 다를 수 있음.
3. **메테오 광역 friendly fire**: 본 plan 폭발 friendly fire OFF (CL-158 동일 정책).
4. **힐이 OP 우려**: 15초 쿨다운 + 30 회복 = DPS 영향 적지만 보스전 시 강함. CL-161 후 조정.
5. **_allowsSkill 디폴트 false**: 기존 SO 들 (Sword_Default, Bow_*, Polearm_*) 영향 X. 안전.
6. **PlayerSkillController 입력 Q/E 충돌**: 다른 입력 (대시 등) 과 충돌? Input.GetKeyDown 직접 사용 → InputAction asset 추가 시 마이그레이션.
7. **메테오 발동 위치 (마우스 vs 가까운 적)**: 본 plan: **마우스 위치**. UX 직관적.
8. **공속 스태프 / 라피드 스태프 비슷함**: Stage 2 의 Lightning 외 차별 약함. → CL-161 QA 후 조정.
9. **스킬 데미지에 OnHit 인챈트 적용 X**: 메테오 데미지가 BurnOnHit 트리거 안 함. 평타만 OnHit. → 명시적 결정 (스킬과 평타 분리).
10. **비숍/하이비숍 정체성 약함**: 평타 데미지 베이스 + 스킬 쿨감 만. 능동적 차별화 약함. → 후속 ticket 에서 비숍 전용 스킬 추가.

### 결정 미정

- [ ] 스킬 2종 — 본 plan: **메테오 + 힐**
- [ ] 스킬 활성 조건 — 본 plan: **`_allowsSkill` 플래그 (스태프만 true)**
- [ ] 빌드 쿨감 + 무기 SkillCDMul 결합 — 본 plan: **곱셈**
- [ ] 메테오 발동 위치 — 본 plan: **마우스**
- [ ] 스킬 데미지에 OnHit 적용 — 본 plan: **X (평타만)**
- [ ] 비숍 전용 스킬 — 본 plan: **별도 ticket**
- [ ] 무기별 스킬 변경 — 본 plan: **고정 2종 (별도 ticket)**

---

## 후속 ticket 영향

| Ticket | CL-160 과의 관계 |
|---|---|
| **CL-161 (무기 QA)** | 4 트리 28개 무기 + 스킬 2종 일괄 검증 |
| **별도 ticket: 무기별 스킬 변경** | 마법사 = 메테오, 비숍 = 힐 강화 등 |
| **별도 ticket: 보상으로 스킬 추가** | 스킬 슬롯 확장 |
| **별도 ticket: 정식 스킬 vfx** | placeholder → 정식 |
| **별도 ticket: 스킬 슬롯 정식 UI** | placeholder → 정식 |
| **별도 ticket: 다른 무기에서 스킬 사용** | _allowsSkill 확장 |
| **별도 ticket: 비숍 능동 스킬 (실드/버프)** | 비숍 정체성 강화 |

---

## 예상 시간

### 작업 단위 A (스킬 인프라) — 약 3시간
| 단계 | 시간 |
|---|---|
| 1단계 (SkillData + enum) | 30분 |
| 2단계 (WeaponData 필드) | 15분 |
| 3단계 (PlayerSkillController) | 1시간 30분 |
| 4단계 (SkillSlotView UI) | 45분 |

### 작업 단위 B (스태프 콘텐츠) — 약 2시간 30분
| 단계 | 시간 |
|---|---|
| 5단계 (Skill SO 2개) | 15분 |
| 6단계 (MagicOrb prefab) | 15분 |
| 7단계 (스태프 SO 7개) | 1시간 30분 |
| 8단계 (_upgrades) | 15분 |
| 9단계 (수치 가이드 문서) | 20분 |

### 작업 단위 C (검증) — 약 1시간 30분
| 단계 | 시간 |
|---|---|
| 10 시나리오 | 1시간 30분 |

| **합계** | **약 7시간** |

→ **5점 ticket 표 초과**. 분할 권장 (A 단독 PR + B+C 단독).

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 스킬 2종 | **메테오+힐** / 다른 조합 | **메테오+힐** |
| 2 | 스킬 활성 조건 | _allowsSkill 플래그 / 무기 이름 prefix | **플래그** |
| 3 | 메테오 발동 위치 | **마우스** / 가까운 적 / 자기 위치 | **마우스** |
| 4 | 빌드 쿨감 결합 | **곱셈** / 합산 | **곱셈** |
| 5 | 스킬 데미지 OnHit 적용 | 적용 / **X** | **X** (평타만) |
| 6 | 분할 PR | A 단독 + B+C / 단일 | **분할** |
| 7 | 무기별 스킬 변경 | 본 CL / **별도** | **별도** |
| 8 | Stage 2 속성 | 본 plan §1 / 모두 None | **본 plan** |

전부 추천대로면 **메테오+힐 + 플래그 + 마우스 + 곱셈 + OnHit X + 분할 + 별도 + 본plan**.

---

## Epic K 진행률 (CL-160 후)

| Ticket | Plan |
|---|---|
| CL-090 무기 SO | ✅ |
| CL-155 4속성 + 결합 | ✅ |
| CL-156 단계 강화 | ✅ |
| CL-157 검 트리 | ✅ |
| CL-158 활 트리 | ✅ |
| CL-159 봉 트리 | ✅ |
| **CL-160 스태프 트리 + 스킬** | ✅ ← 방금 |
| CL-161 무기 QA | ⏳ |

**Epic K: 6/7 (콘텐츠 4/4 완료, QA 1개 남음)**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-161 무기 QA + 1차 밸런스 | 2점 | Epic K 마무리. 4 트리 28개 무기 + 스킬 2종 검증 |
| B | Epic U 진입 (CL-162 EditorWindow 셸) | - | 디자이너 도구 시작 |

**추천: A (CL-161)** — Epic K 마무리. CL-153 패턴 따라 시나리오 체크리스트 + 이슈 로그.

뭐로 갈까요?

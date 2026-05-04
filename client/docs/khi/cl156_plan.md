# CL-156 — 무기 단계 강화 시스템 (기본 → 파생 → 강화)

## Context

Epic K 두 번째 ticket. **CL-157~160 무기 트리의 전제 인프라**.

3점 P2, 클라1, CL-090 의존.

회의록 모델: **3단계 강화**
- **Stage 0 (기본)**: 검/활/봉/스태프 4종 — 무속성, 단순
- **Stage 1 (파생)**: 기본에서 1단계 분기 — 예: 검 → 단검 / 방패검 / 대검 (각 무기당 3 분기)
- **Stage 2 (강화)**: 파생에서 1단계 더 — 예: 활 → 화염방사 (Fire 속성) / 유탄 (폭발)

총 무기 수 (회의록 기반):
- 검: 1 + 3 + N — 단검 / 방패검 / 대검 (CL-157)
- 활: 1 + 3 + N — 노 / 총 / 화염방사·유탄 (CL-158)
- 봉: 1 + 3 + N — 여의봉 / 창 / 망치 (CL-159)
- 스태프: 1 + 3 + N — 공속 / 마법사 / 비숍 (CL-160)

### 기존 상태 (확인 완료)

- ✅ `WeaponData` SO (CL-090) — 무기 단일 단위 표현
- ✅ `WeaponElement` (CL-155) — 강화 분기에 속성 부여
- ❌ 강화 트리 (parent-child 관계) — **본 CL 신설**
- ❌ 강화 트리거 / 비용 — **본 CL 신설**
- ❌ 런타임 무기 변경 처리 — **본 CL 신설**
- ❌ WeaponData asset 자체 미존재 — CL-157~160 에서 생성

### 본 CL 책임 범위

1. **트리 데이터 모델**: WeaponData 에 `_upgrades` 필드 (다음 강화 후보 N개)
2. **강화 컨트롤러**: 플레이어 보유 무기 + 강화 트리거 처리
3. **비용 정책**: 골드 / 특수 재료 (디폴트 골드)
4. **런타임 무기 변경**: KhiMeleeComboController 의 `weaponData` 교체 흐름
5. **시각/UI 진입점** (placeholder, 정식 UI 별도 ticket)

→ **CL-157~160 의 SO 인스턴스 / 콤보 데이터는 별도**. 본 CL 은 **시스템 + 인프라** 만.

---

## 결정사항

### 1. 트리 모델 — WeaponData 에 `_upgrades` 배열 ⭐

**옵션**:
- (a) **WeaponData._upgrades: WeaponData[]** — 다음 강화 후보 직접 참조 ⭐
- (b) WeaponTreeData (별도 SO) — 트리 전체를 별도 SO 로
- (c) Editor 자동 추론 (이름 패턴)

**채택: (a)**.
- 단순 (각 무기 SO 가 자기 다음 단계만 알면 됨)
- Inspector 명시적 (디자이너가 트리 시각 확인 가능)
- (b) 의 별도 SO 불필요 — 트리 자체가 SO 그래프

```csharp
// WeaponData.cs 추가
[Header("Upgrade Tree (CL-156)")]
[Tooltip("이 무기에서 강화 가능한 다음 무기 후보. 빈 배열이면 최종 강화.")]
[SerializeField] private WeaponData[] _upgrades = Array.Empty<WeaponData>();

[Tooltip("이 무기의 강화 단계 (0=기본, 1=파생, 2=강화). UI 분류용.")]
[SerializeField] private int _stage;

[Tooltip("이 무기로 강화하는 데 드는 골드 (Stage 0 → 1 또는 1 → 2). Stage 0 무기는 0.")]
[SerializeField, Min(0)] private int _upgradeCost = 100;

public IReadOnlyList<WeaponData> Upgrades => _upgrades;
public int Stage => _stage;
public int UpgradeCost => _upgradeCost;
```

→ Stage 0 의 _upgrades = [Stage 1 무기 N개]. Stage 2 의 _upgrades = []. 디자이너가 명시 입력.

### 2. 강화 트리 예시 (CL-157~160 입력 양식)

```
검 (Stage 0)
 └ _upgrades = [단검, 방패검, 대검]

단검 (Stage 1)
 └ _upgrades = [그림자칼, 기타...]

방패검 (Stage 1)
 └ _upgrades = [..., ...]

대검 (Stage 1)
 └ _upgrades = [그레이트소드, 기타...]
```

본 CL 은 위 구조를 **지원하는 시스템만** 만듦. 실제 SO 인스턴스는 CL-157.

### 3. 강화 트리거 — 상점에서 강화 슬롯

**옵션**:
- (a) **상점에 강화 슬롯 추가** ⭐ — 골드 지불 + 후보 중 1개 선택
- (b) 보스 처치 시 자동
- (c) 특별 NPC 만남
- (d) 풀에서 보상 카드로 등장

**채택: (a) + (b)**.
- 상점에서 골드 강화 (메인)
- 보스 처치 시 무료 강화 1회 (보너스)
- (c)(d) 는 후속 확장

본 CL: **(a) 만 구현**, (b) 는 별도 ticket.

### 4. 비용 정책 — 골드 only (MVP)

```
Stage 0 → 1: 100 골드
Stage 1 → 2: 300 골드
```

→ WeaponData._upgradeCost 가 **목표 무기**에 적힘. 즉 단검._upgradeCost = 100 (검에서 단검으로 갈 때 비용).

수치 조정은 별도 ticket. 본 plan: 디폴트 100 / 300.

### 5. PlayerWeaponLoadout 컴포넌트 신설

플레이어가 현재 어떤 무기를 보유 중인지 + 변경 흐름 단일 진입점:

```csharp
public class PlayerWeaponLoadout : MonoBehaviour
{
    [SerializeField] private KhiMeleeComboController combat;
    [SerializeField] private WeaponData _currentWeapon;
    [SerializeField] private GoldWallet wallet;

    public WeaponData CurrentWeapon => _currentWeapon;
    public event Action<WeaponData, WeaponData> OnWeaponChanged;   // (old, new)

    public bool CanUpgradeTo(WeaponData target)
    {
        if (_currentWeapon == null || _currentWeapon.Upgrades == null) return false;
        if (!_currentWeapon.Upgrades.Contains(target)) return false;
        if (wallet.Current < target.UpgradeCost) return false;
        return true;
    }

    public bool TryUpgrade(WeaponData target, bool free = false)
    {
        if (!CanUpgradeTo(target)) return false;

        if (!free) wallet.Spend(target.UpgradeCost);
        var old = _currentWeapon;
        _currentWeapon = target;
        combat.SetWeaponData(target);   // 신규 메서드 (§7)
        OnWeaponChanged?.Invoke(old, target);
        Debug.Log($"[CL-156] 강화: {old.DisplayName} → {target.DisplayName}");
        return true;
    }
}
```

→ 인벤토리에는 무기 안 들어감 (인벤토리는 RelicData 만). 무기는 별도 슬롯 (Loadout).

### 6. 시작 무기 정책

**옵션**:
- (a) **검 (Stage 0) 고정** — 매 런 검으로 시작
- (b) 4종 중 선택 (런 시작 시 메뉴)
- (c) 클래스 시스템 (CL-157~160 후 결정)

**채택: (a) MVP**. (b)(c) 는 별도 ticket.

→ `PlayerWeaponLoadout._currentWeapon` 디폴트 = 검 SO ref (Inspector 에서 직접 할당).

### 7. KhiMeleeComboController.SetWeaponData 신설

런타임 무기 변경:

```csharp
// KhiMeleeComboController.cs 추가
public void SetWeaponData(WeaponData newWeapon)
{
    _weaponData = newWeapon;
    // 콤보 인덱스 리셋 (강화 도중 콤보 중간이면 처음부터)
    _comboStep = 0;
    // hitbox / animator 재바인딩 (필요 시)
    OnWeaponChanged();   // 내부 hook (있으면)
}
```

⚠️ **위험**: 기존 `_weaponData` 가 `[SerializeField]` 만 있고 setter 없으면 신설 필요. 기존 콤보 흐름이 weaponData 캐시했을 가능성 → 점검.

### 8. 상점 강화 슬롯 통합 — ShopGenerator 확장

```csharp
// ShopGenerator.Generate 에 강화 슬롯 추가
if (loadout.CurrentWeapon != null && loadout.CurrentWeapon.Upgrades.Count > 0)
{
    // 가능한 강화 후보 중 랜덤 1~2 (최대 N)
    var candidates = loadout.CurrentWeapon.Upgrades.ToList();
    int slotCount = Mathf.Min(candidates.Count, config.UpgradeSlotCount);
    for (int i = 0; i < slotCount; i++)
    {
        var picked = candidates[Random.Range(0, candidates.Count)];
        candidates.Remove(picked);
        items.Add(new ShopItemData
        {
            // 신규 ItemKind = WeaponUpgrade
            WeaponUpgrade = picked,
            Price = picked.UpgradeCost,
        });
    }
}
```

`ShopItemData` 확장 — 기존 RelicData 기반 + WeaponData 추가 케이스.

⚠️ **상점 패널 UI 수정 필요** — RelicData 만 표시하던 ShopItemView 가 WeaponData 표시도 필요. → MVP 는 단순히 무기 이름 + 가격만 표시.

### 9. 보상 카드 강화 슬롯 — 후속

회의록의 강화 트리거 후보 중 보상 카드는 별도 ticket. 본 CL 은 상점만.

### 10. 시각 / 무기 변경 시 sprite 교체

KhiMeleeComboController 의 콤보 sprite 는 WeaponData.Steps[i].slashFrames 로 데이터 주도. 무기 변경 → 다음 콤보부터 신규 sprite 자동 적용 (CL-090 의 SO 데이터 주도 설계 덕).

추가 시각 (예: 무기 변경 알림 토스트) → `OnWeaponChanged` 이벤트로 hook.

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Combat/PlayerWeaponLoadout.cs` | 무기 보유 + 강화 흐름 |
| `Assets/_Project/Scripts/Runtime/Shop/ShopWeaponUpgradeView.cs` | 상점 강화 슬롯 UI (placeholder) |
| `docs/khi/cl156_weapon_upgrade_tree_design.md` | 트리 디자인 가이드 (CL-157~160 작업자용) |

### 수정

| 경로 | 변경 |
|---|---|
| `WeaponData.cs` | `_upgrades` / `_stage` / `_upgradeCost` 필드 + 프로퍼티 |
| `KhiMeleeComboController.cs` | `SetWeaponData(WeaponData)` 메서드 (런타임 변경) |
| `ShopGenerator.cs` (CL-152) | 강화 슬롯 추가 (기존 유물/소모품 슬롯 옆) |
| `ShopItemData.cs` | `WeaponUpgrade: WeaponData` 필드 추가 (또는 ItemKind enum) |
| `ShopController.cs` / `ShopPanelView.cs` | 강화 슬롯 구매 흐름 (loadout.TryUpgrade 호출) |
| `ShopConfig.cs` | `UpgradeSlotCount` 필드 (디폴트 1~2) |

---

## 구현 단계

### 1단계: WeaponData 트리 필드 (15분)

§1 코드. 3 필드 + 프로퍼티.

### 2단계: PlayerWeaponLoadout 컴포넌트 (45분)

§5 코드. CanUpgradeTo / TryUpgrade / OnWeaponChanged.

플레이어 prefab 에 부착. Inspector 에서 시작 무기 (검 SO) 할당. — 단, 검 SO 는 CL-157 산출물이라 본 CL 시점엔 placeholder 또는 미할당 가능.

### 3단계: KhiMeleeComboController.SetWeaponData (30분)

기존 _weaponData 필드 점검 후 setter 신설:
1. _weaponData 교체
2. 콤보 인덱스 / 타이머 리셋
3. (필요 시) hitbox / animator 재바인딩

⚠️ KhiMeleeComboController 가 Awake/Start 에서 weaponData 의존 캐싱 했을 가능성 → 점검 후 동적 갱신 보장.

### 4단계: ShopItemData 확장 (15분)

```csharp
public enum ShopItemKind { Relic, WeaponUpgrade }

public class ShopItemData
{
    public ShopItemKind Kind;
    public RelicData Relic;
    public WeaponData WeaponUpgrade;
    public int Price;
}
```

기존 사용처 (ShopGenerator, ShopController, ShopItemView) 의 Kind 분기 처리.

### 5단계: ShopGenerator 강화 슬롯 (30분)

§8 코드. 시작/현재 무기의 _upgrades 가 비었으면 슬롯 X.

`ShopConfig.UpgradeSlotCount` 디폴트 1.

### 6단계: ShopItemView 분기 표시 (30분)

기존 RelicData 표시 흐름 옆에 WeaponData 표시:
- 아이콘 = WeaponData.preview sprite (또는 placeholder)
- 이름 = `[강화] {WeaponData.DisplayName}`
- 가격 표시
- Tooltip — 후속 ticket

`ShopController.OnPurchase` 에서 Kind 분기:
```csharp
if (item.Kind == ShopItemKind.WeaponUpgrade)
{
    if (loadout.TryUpgrade(item.WeaponUpgrade))
    {
        // 구매 성공
    }
    else
    {
        ToastNotifier.Show("강화 불가 — 골드 부족 또는 트리 불일치");
    }
}
else
{
    // 기존 RelicData 흐름
}
```

### 7단계: 트리 디자인 가이드 문서 (20분)

`cl156_weapon_upgrade_tree_design.md`:
- WeaponData._upgrades 입력 방법 (Inspector 드래그)
- _stage 0/1/2 가이드
- _upgradeCost 권장 (Stage 1=100, Stage 2=300)
- 강화 시 속성 부여 (CL-155 _element 함께 설정)
- CL-157~160 작업자 체크리스트

→ 디자이너 / CL-157~160 작업자용 1~2페이지.

### 8단계: 검증 (45분)

```
시나리오 1: 빈 트리
- 시작 무기 = WeaponData (Stage 0, _upgrades = [])
- 상점에 강화 슬롯 표시 X

시나리오 2: 트리 1단계
- 시작 무기 _upgrades = [무기A, 무기B] 디버그 SO
- 상점에 강화 슬롯 1개 (랜덤 무기)
- 골드 100 보유 시 강화 가능 (회색 X)
- 골드 50 보유 시 강화 불가 (회색 / 토스트)

시나리오 3: 강화 흐름
- 강화 슬롯 클릭 → 골드 100 차감
- _currentWeapon 변경
- KhiMeleeComboController.weaponData 변경 확인
- 다음 콤보부터 신규 무기 sprite/damage 적용

시나리오 4: 2단계 강화
- 강화된 무기 _upgrades = [무기X] 가지면 다음 상점에 표시

시나리오 5: 최종 강화
- 강화 무기 _upgrades = [] → 다음 상점에 강화 슬롯 X

시나리오 6: WeaponData 속성 부여 (CL-155 연동)
- Stage 1 무기 _element = Fire 설정
- 강화 후 평타 시 BurnOnHit 발동 확인

시나리오 7: 런 종료
- Run 끝나면 _currentWeapon 디폴트 검 (Stage 0) 으로 복원
```

---

## 위험 / 결정 미정

### 위험

1. **WeaponData asset 미존재**: CL-157~160 산출물 의존. 본 CL 은 placeholder SO (디버그용 더미) 로 검증.
2. **KhiMeleeComboController 가 weaponData 캐싱 가능성**: Awake/Start 시 weaponData.Steps 등 캐싱 후 _weaponData 변경되어도 반영 X 위험. → SetWeaponData 가 캐시 무효화 책임.
3. **상점 슬롯 UI 변경 부담**: ShopItemView 가 RelicData 가정으로 만들어져 있음. WeaponData 분기 처리 추가 시 전체 view 수정. → MVP 는 단순 텍스트 표시.
4. **순환 참조 위험**: WeaponData._upgrades 가 자기 자신을 가리키면 무한 루프. → Editor validation 또는 OnValidate 검사.
5. **무기 변경 중 평타 입력 충돌**: 강화 직후 평타 1타 진행 중이면 새 무기로 어떻게? → SetWeaponData 가 콤보 step=0 강제 리셋. 입력 중간 끊김 가능.
6. **상점 강화 후 즉시 반영**: 다음 평타부터인지, 즉시인지? → 즉시 (UX). SetWeaponData 호출 시점에 즉시 변경.
7. **중복 강화 슬롯**: 같은 무기 후보가 여러 번 등장할 수 있음 → ShopGenerator 에서 candidates.Remove(picked) 로 회피.
8. **무기 강화 후 인벤토리 / HUD 표시**: HUD 에 현재 무기 표시 X 상태. → 본 CL 미포함, 별도 ticket.
9. **Loadout SO 영구 변경 위험**: WeaponData SO 자체는 불변. _currentWeapon 은 ref 만 변경 (런타임 instance X).

### 결정 미정

- [ ] 트리 모델 — 본 plan: **WeaponData._upgrades 직접 참조**
- [ ] 강화 트리거 — 본 plan: **상점 슬롯 (보상 카드 후속)**
- [ ] 비용 — 본 plan: **골드 only, 100/300 디폴트**
- [ ] 시작 무기 — 본 plan: **검 고정 (선택은 별도 ticket)**
- [ ] 상점 슬롯 표시 — 본 plan: **단순 텍스트 (정식 UI 별도)**
- [ ] 보스 무료 강화 — 본 plan: **별도 ticket**
- [ ] 무기 강화 시 콤보 step 처리 — 본 plan: **0 리셋**
- [ ] 런 종료 시 무기 복원 정책 — 본 plan: **검 디폴트 복원** (RunManager 후속)

---

## 후속 ticket 영향

| Ticket | CL-156 과의 관계 |
|---|---|
| **CL-157 (검 트리)** | 검 + 단검/방패검/대검 SO 작성, _upgrades 입력, _element 설정 |
| **CL-158 (활 트리)** | 활 + 노/총/화염방사·유탄 SO, 화염방사 = Fire |
| **CL-159 (봉 트리)** | 여의봉/창/망치 SO |
| **CL-160 (스태프 트리)** | 공속/마법사/비숍 SO |
| **CL-161 (무기 QA)** | 본 CL 의 강화 흐름 검증 |
| **별도 ticket: 보스 무료 강화** | (b) 트리거 |
| **별도 ticket: 시작 무기 선택 메뉴** | 4종 중 선택 |
| **별도 ticket: 상점 강화 슬롯 정식 UI** | preview sprite + tooltip |
| **별도 ticket: HUD 현재 무기 표시** | 무기 아이콘/이름 |
| **별도 ticket: 보상 카드 강화 슬롯** | (d) 트리거 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (WeaponData 트리 필드) | 15분 |
| 2단계 (PlayerWeaponLoadout) | 45분 |
| 3단계 (SetWeaponData) | 30분 |
| 4단계 (ShopItemData 확장) | 15분 |
| 5단계 (ShopGenerator 강화 슬롯) | 30분 |
| 6단계 (ShopItemView 분기) | 30분 |
| 7단계 (트리 디자인 가이드) | 20분 |
| 8단계 (검증 7 시나리오) | 45분 |
| **합계** | **약 3시간 30분** |

→ 3점 ticket 에 부합.

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 트리 모델 | **_upgrades 직참조** / 별 SO | **직참조** |
| 2 | 강화 트리거 | **상점만** / 상점+보스 | **상점만** (MVP) |
| 3 | 비용 | **골드 only** / 골드+재료 | **골드** |
| 4 | 비용 디폴트 | 100/300 / 50/200 / 200/500 | **100/300** |
| 5 | 시작 무기 | **검 고정** / 4종 선택 | **검 고정** |
| 6 | 상점 슬롯 표시 | **단순 텍스트** / 정식 UI | **단순** |
| 7 | 콤보 중 강화 | step 리셋 / 입력 무시 | **리셋** |

전부 추천대로면 **직참조 + 상점만 + 골드 + 100/300 + 검고정 + 단순 + 리셋**.

---

## Epic K 진행률 (CL-156 후)

| Ticket | Plan |
|---|---|
| CL-090 무기 SO 구조 | ✅ |
| CL-155 4속성 + 결합 | ✅ |
| **CL-156 단계 강화** | ✅ ← 방금 |
| CL-157 검 트리 | ⏳ |
| CL-158 활 트리 | ⏳ |
| CL-159 봉 트리 | ⏳ |
| CL-160 스태프 트리 | ⏳ |
| CL-161 무기 QA | ⏳ |

**Epic K: 2/7** (인프라 ticket 완료, 콘텐츠 ticket 시작 가능)

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-157 검 트리 | 5점 | 가장 자연스러운 다음. 본 CL 의 _upgrades 첫 사용 |
| B | CL-158 활 트리 | 5점 | A 와 병렬 가능 |
| C | CL-159 봉 트리 | 5점 | A 와 병렬 가능 |
| D | CL-160 스태프 트리 | 5점 | 스킬 2종 추가 부담 |
| E | Epic U 진입 (CL-162) | - | 디자이너 도구 |

**추천: A (CL-157 검 트리)** — 4 트리 중 가장 단순 (근접 무기 베이스). 본 CL 흐름 첫 검증으로 적합. 이후 B/C/D 는 동일 패턴.

뭐로 갈까요?

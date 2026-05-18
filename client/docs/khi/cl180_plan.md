# CL-180 — PlayerStatsApplier 정리 + base × multiplier 통합 (Binding 패턴)

## Context

CL-179 의 `Khi_Stats.asset` (PlayerStatsData) 의 base 값을 TDE 컴포넌트 (Health / CharacterMovement / CharacterDash2D) 의 인라인 값에 자동 주입. **PlayerStatsBinding** 단일 컴포넌트 패턴으로 기존 Applier (PlayerHealthStatApplier / PlayerMovementStatApplier / KhiDashController) **무수정**. 라이브 튜닝 PlayerStatsDataEvents.OnAssetSaved hook 활용.

**2점 P3, CL-179 의존**. master plan §9 Q1 의 보류 결정 — NGO 동기화 우려 X 확인됨 (탐색).

### ★ 사용자 결정 (확정)

| 항목 | 결정 |
|---|---|
| **어댑터 패턴** | **(A2) PlayerStatsBinding 신설 + Applier 무수정** |
| **TestKhi prefab 변경** | 본인 영역 — Binding 컴포넌트 추가 + Khi_Stats.asset 할당 |
| **Execution order** | `[DefaultExecutionOrder(-100)]` — Binding 이 다른 컴포넌트보다 먼저 Awake |
| **라이브 튜닝** | PlayerStatsDataEvents.OnAssetSaved 구독 (cl179 hook) |
| **SO 미할당 fallback** | TDE 인라인 값 그대로 (cl179 와 동일) |

### 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| `PlayerStatsBinding.cs` 신설 (MonoBehaviour) | Applier 3개 무수정 (회귀 위험 회피) |
| TestKhi prefab 변경 (사용자 — Binding 추가 + SO 할당) | Provider 등록 (CL-181) |
| 라이브 튜닝 (OnAssetSaved 구독) | NGO 동기화 (Player Stat 시스템 NGO 무관 확인) |
| Khi_Stats 적용 검증 | 다른 캐릭터 (미소녀 등 미존재) |

---

## 현황 (탐색)

### 1.1 작성자 매트릭스 — 모두 김회인 ★ 협업 X

| 파일 | 작성자 |
|---|---|
| TestKhi_Net_AD.prefab / TestKhi_MinimalCharacter2D.prefab | 김회인 |
| PlayerHealthStatApplier.cs / PlayerMovementStatApplier.cs / KhiDashController.cs | 김회인 |
| PlayerStatsData.cs (CL-179) | 김회인 |

→ 모두 본인. cl178 처럼 prefab 회피 패턴 불필요. 가장 깔끔한 패턴 자유 선택 가능.

### 1.2 NGO 무관 확인

```bash
grep "NetworkBehaviour|NetworkObject|using Unity.Netcode" Runtime/TestKhi → 0 hit
grep PlayerStatModifierContainer.cs → MonoBehaviour (NetworkBehaviour X)
```

→ master plan §9 Q1 의 "NGO 동기화" 우려 **무효**. 호스트/클라가 같은 SO asset 로드 → 자동 동일 (asset 자체가 동기화 단위).

### 1.3 TDE 컴포넌트 setter 확인

| TDE 필드 | setter | 확인 위치 |
|---|---|---|
| `Health.MaximumHealth` | public set | [PlayerHealthStatApplier.cs:41](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerHealthStatApplier.cs:41) `health.MaximumHealth = ...` 사용 |
| `CharacterMovement.MovementSpeed` | public (TDE 표준) | (PlayerMovementStatApplier 는 Multiplier 만 변경, base setter 직접 사용 X — TDE 표준 public 가정) |
| `CharacterDash2D.Cooldown.ConsumptionDuration` | public set | [KhiDashController.cs:78](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDashController.cs:78) `Cooldown.ConsumptionDuration = ...` 사용 |

→ 3개 setter 모두 사용 가능. Binding 이 Awake 에서 직접 변경 가능.

### 1.4 Applier base 캐싱 시점 (Binding 의 execution order 결정 근거)

| Applier | base 캐싱 시점 | 캐싱 코드 |
|---|---|---|
| PlayerHealthStatApplier | OnEnable | `_baseMaxHealth = health.MaximumHealth` |
| PlayerMovementStatApplier | (캐싱 X) | 매 LateUpdate `characterMovement.MovementSpeedMultiplier = mul` |
| KhiDashController (Initialization) | TDE Character.Awake 안 | `_baseCooldownDuration = Cooldown.ConsumptionDuration` |

→ 모두 **Awake / OnEnable / Initialization 시점**. Binding 이 -100 으로 가장 먼저 Awake → 이 시점들에 SO 값이 이미 적용되어 있음 → Applier 들은 자연스럽게 SO 값을 base 로 사용.

### 1.5 Khi prefab 2개

`LostMemory/Assets/_Project/Prefabs/Characters/`:
- TestKhi_Net_AD.prefab (네트워크 버전)
- TestKhi_MinimalCharacter2D.prefab (단일 버전)

→ **둘 다 PlayerStatsBinding 추가 필요** (사용자 작업 §사용자 작업 가이드).

---

## 설계 결정

### 1. ★ A2 패턴 — PlayerStatsBinding 신설 + Applier 무수정

```
[본 CL]
PlayerStatsBinding.cs (신규 MonoBehaviour, [DefaultExecutionOrder(-100)])
    ↓ Awake 에서 Khi_Stats.asset 의 base 값 → TDE 인라인 값 덮어씀
    ↓ OnEnable: PlayerStatsDataEvents.OnAssetSaved 구독
    ↓ OnAssetSaved: 매칭 SO 면 재주입 (라이브 튜닝)

[무수정 — 본 CL 외]
PlayerHealthStatApplier (OnEnable 시 health.MaximumHealth 캐싱 — Binding 이 변경한 값)
PlayerMovementStatApplier (LateUpdate, base 캐싱 X)
KhiDashController.Initialization (Cooldown.ConsumptionDuration 캐싱 — Binding 이 변경한 값)
```

→ Binding 만 신규. Applier 회귀 위험 0.

### 2. Execution order — `[DefaultExecutionOrder(-100)]`

Binding.Awake 가 다른 모든 본인 컴포넌트보다 먼저 실행 보장.

| 컴포넌트 | ExecutionOrder | Awake 순서 |
|---|---|---|
| **PlayerStatsBinding** (본 CL) | **-100** | **1번** (가장 먼저) |
| TDE Character / Health / CharacterMovement / CharacterDash2D | 0 (default) | 2번 |
| KhiDashController.Initialization (TDE Character.Awake 안) | 0 | 2번 |
| BerthaBossPhaseController (cl178 비교용) | +300 | 3번 (Bertha 영역, 본 CL 무관) |

→ -100 충분히 빠름. 다른 본인 컴포넌트가 음수 ExecutionOrder 사용 시 충돌 가능성 — 검증 시 확인.

### 3. PlayerStatsBinding 의 필드 (Inspector 할당)

```csharp
[SerializeField] private PlayerStatsData _statsData;
[SerializeField] private Health _health;
[SerializeField] private CharacterMovement _characterMovement;
[SerializeField] private CharacterDash2D _characterDash;
```

- Reset / OnValidate: 같은 GameObject 의 컴포넌트 자동 GetComponent (디자이너 편의)
- _statsData = Khi_Stats.asset (사용자 할당)

### 4. Awake 시점 주입 로직

```csharp
private void Awake()
{
    AutoFindReferences();
    ApplyStatsToTdeComponents();
}

private void ApplyStatsToTdeComponents()
{
    if (_statsData == null) return;   // SO 미할당 fallback — 인라인 값 그대로

    if (_health           != null) _health.MaximumHealth                 = _statsData.BaseMaxHealth;
    if (_characterMovement != null) _characterMovement.MovementSpeed     = _statsData.BaseMoveSpeed;
    if (_characterDash != null && _characterDash.Cooldown != null)
        _characterDash.Cooldown.ConsumptionDuration                       = _statsData.BaseDashCooldown;
}
```

### 5. 라이브 튜닝 — PlayerStatsDataEvents.OnAssetSaved 구독

```csharp
private void OnEnable() => PlayerStatsDataEvents.OnAssetSaved += OnStatsAssetSaved;
private void OnDisable() => PlayerStatsDataEvents.OnAssetSaved -= OnStatsAssetSaved;

private void OnStatsAssetSaved(PlayerStatsData saved)
{
    if (saved != _statsData) return;   // 다른 캐릭터의 SO 면 무시
    ApplyStatsToTdeComponents();
}
```

**라이브 튜닝의 한계**:
- PlayerHealthStatApplier 의 `_baseMaxHealth` = OnEnable 시점 캐싱 (Play 시작 1회)
- 라이브 튜닝 시 health.MaximumHealth 만 변경됨 → Applier 의 _baseMaxHealth 는 옛 값
- 다음 multiplier 변경 시 Applier 가 옛 _baseMaxHealth × newMul 적용 → **라이브 튜닝 효과 부분적**
- → KhiDashController 도 동일 (`_baseCooldownDuration` Initialization 캐싱)
- → MovementSpeed 만 100% 라이브 (캐싱 X)

**해결 옵션**:
- (i) MovementSpeed 만 라이브 — MaxHealth/DashCooldown 은 다음 Play 시작 시 반영 (단순)
- (ii) Reflection 으로 Applier 의 private _baseMaxHealth 직접 변경 (추잡, 회피)
- (iii) Applier 에 RefreshBase() public 메서드 추가 (Applier 수정 — A2 패턴 깨짐)

→ **(i) 채택** (본 CL MVP). 라이브 튜닝 안내: "MoveSpeed 는 즉시, MaxHealth/DashCooldown 은 다음 Play 시작 시". CL-184 또는 별도 후속에서 (iii) 검토.

### 6. SO 미할당 fallback — 인라인 값 그대로

`_statsData == null` → ApplyStatsToTdeComponents 조기 return. TDE 인라인 값 영향 0. Khi_Stats.asset 미할당 시에도 게임 정상 동작 (cl179 와 동일 정책).

### 7. 다른 캐릭터 확장

미소녀 등 추가 시:
- 각 캐릭터 prefab 에 PlayerStatsBinding 추가
- 캐릭터별 SO (Misonyo_Stats.asset 등) 할당
- Binding 클래스는 그대로 재사용

KhiDashController 는 Khi 전용 — 다른 캐릭터는 다른 Dash 컴포넌트 사용. Binding 의 _characterDash = generic CharacterDash2D 라 호환.

### 8. namespace / 파일 위치

| 파일 | 경로 | namespace |
|---|---|---|
| `PlayerStatsBinding.cs` | `Runtime/Combat/` | `LostMemory.Combat` |

→ cl179 의 PlayerStatsData 와 같은 폴더 / namespace.

---

## 신규 파일 — 코드

### PlayerStatsBinding.cs

```csharp
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// CL-180: PlayerStatsData 의 base 값을 TDE 컴포넌트 (Health / CharacterMovement /
    /// CharacterDash2D) 의 인라인 값에 자동 주입. Awake 시점 1회 + 라이브 튜닝.
    ///
    /// Applier (PlayerHealthStatApplier / PlayerMovementStatApplier / KhiDashController)
    /// 무수정 — Binding 이 Awake 에 변경한 TDE 값을 Applier 가 자연스럽게 base 로 캐싱.
    ///
    /// DefaultExecutionOrder(-100): 다른 본인 컴포넌트보다 먼저 Awake → TDE 인라인 값 덮어씀.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Lost Memory/Combat/Player Stats Binding")]
    public sealed class PlayerStatsBinding : MonoBehaviour
    {
        [Tooltip("CL-179: 적용할 캐릭터 base 능력치 SO (Khi_Stats 등). null 이면 인라인 값 fallback.")]
        [SerializeField] private PlayerStatsData _statsData;

        [Header("Target TDE Components (Reset/OnValidate 자동 채움)")]
        [SerializeField] private Health _health;
        [SerializeField] private CharacterMovement _characterMovement;
        [SerializeField] private CharacterDash2D _characterDash;

        private void Reset()        => AutoFindReferences();
        private void OnValidate()   => AutoFindReferences();

        private void Awake()
        {
            AutoFindReferences();
            ApplyStatsToTdeComponents();
        }

        private void OnEnable()
        {
            PlayerStatsDataEvents.OnAssetSaved -= OnStatsAssetSaved;
            PlayerStatsDataEvents.OnAssetSaved += OnStatsAssetSaved;
        }

        private void OnDisable()
        {
            PlayerStatsDataEvents.OnAssetSaved -= OnStatsAssetSaved;
        }

        private void OnStatsAssetSaved(PlayerStatsData saved)
        {
            if (saved != _statsData) return;
            ApplyStatsToTdeComponents();
            Debug.Log($"[PlayerStatsBinding] Live tuning applied: {saved.name}. " +
                      "MoveSpeed 즉시 / MaxHealth · DashCooldown 은 다음 Play 시작 시 반영.");
        }

        private void AutoFindReferences()
        {
            if (_health == null)            _health = GetComponent<Health>();
            if (_characterMovement == null) _characterMovement = GetComponent<CharacterMovement>();
            if (_characterDash == null)     _characterDash = GetComponent<CharacterDash2D>();
        }

        private void ApplyStatsToTdeComponents()
        {
            if (_statsData == null) return;

            if (_health != null)
                _health.MaximumHealth = _statsData.BaseMaxHealth;

            if (_characterMovement != null)
                _characterMovement.MovementSpeed = _statsData.BaseMoveSpeed;

            if (_characterDash != null && _characterDash.Cooldown != null)
                _characterDash.Cooldown.ConsumptionDuration = _statsData.BaseDashCooldown;
        }
    }
}
```

---

## 사용자 작업 (Unity Editor)

### A. TestKhi_Net_AD.prefab

| 순서 | 작업 |
|---|---|
| 1 | Project 창 → TestKhi_Net_AD.prefab 더블클릭 (또는 Open Prefab 모드) |
| 2 | 루트 GameObject 선택 → Add Component → "Player Stats Binding" 검색 + 추가 |
| 3 | Inspector 의 PlayerStatsBinding 필드: |
|   | - Stats Data = Khi_Stats.asset 할당 |
|   | - Health / Character Movement / Character Dash = (Reset 자동 채움 — 빈 칸이면 수동) |
| 4 | 저장 |

### B. TestKhi_MinimalCharacter2D.prefab

A 와 동일 작업 반복.

→ 두 prefab 의 Binding 컴포넌트가 같은 Khi_Stats.asset 참조. Asset 변경 시 양쪽 모두 적용.

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **TestKhi prefab Inspector 값 vs Khi_Stats.asset 값 차이** | Binding 이 Awake 에 SO 로 덮어씀. 디자이너 안내 — "Khi_Stats.asset 가 진실. prefab Inspector 값은 표시만". cl178 와 동일 정책 |
| 2 | **라이브 튜닝 부분 적용** — MoveSpeed 즉시, MaxHealth/DashCooldown 은 다음 Play 시작 | Applier private 캐싱 변경 X. OnAssetSaved 핸들러에서 Console 안내. CL-184 또는 후속에서 Applier RefreshBase() public 메서드 추가 검토 |
| 3 | **Execution order 충돌** — 본 CL 의 -100 이 다른 본인 컴포넌트와 충돌 | BerthaBossPhaseController = +300, KhiDashController = default. 검증 시 확인. 필요 시 -200 등으로 조정 |
| 4 | **TDE Health.SetHealth 동기화** — MaximumHealth 변경 시 CurrentHealth 비율 처리 | Awake 시점 = 게임 시작 = CurrentHealth 미설정 (Awake 후 OnEnable 에서 SetHealth(MaximumHealth) 보통). 영향 0. PlayerHealthStatApplier 의 ratio 로직 (line 39-43) 참조 |
| 5 | **CharacterDash2D.Cooldown null 가능성** | `_characterDash.Cooldown != null` 가드. Cooldown 미할당 prefab 이면 DashCooldown 적용 X (LogWarning 추가 권장) |
| 6 | **Khi_Stats.asset 미할당** | _statsData null 가드 → ApplyStatsToTdeComponents 조기 return. 인라인 값 fallback. cl179 와 동일 |
| 7 | **PlayerStatsDataEvents.OnAssetSaved 발화 시점** | "Save Current Values" ContextMenu 만. CL-181 (Provider) 후 Balance Editor 일반 저장에서도 발화 추가 검토 |
| 8 | **다른 캐릭터 추가 시** | Binding 재사용. KhiDashController 는 Khi 전용 — 다른 캐릭터는 다른 dash 컴포넌트 (Binding 의 _characterDash 호환) |
| 9 | **NGO 동기화** | PlayerStatModifierContainer / TestKhi 모두 NetworkBehaviour X. 호스트/클라가 같은 Khi_Stats.asset 로드 → 자동 동일 (asset 동기화 단위) |
| 10 | **prefab 2개 양쪽 작업 누락 위험** | Binding 추가 = TestKhi_Net_AD + TestKhi_MinimalCharacter2D 양쪽 필요. 사용자 §사용자 작업 명시 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. PlayerStatsBinding.cs 컴파일 OK
2. Applier 3개 (PlayerHealthStatApplier / PlayerMovementStatApplier / KhiDashController) 무수정 확인
3. PlayerStatsData.cs (CL-179) 무수정 확인
```

### Unity Editor 검증 (사용자)

```
4. TestKhi 양쪽 prefab 에 PlayerStatsBinding 추가 + Khi_Stats.asset 할당
5. Khi_Stats.asset 의 BaseMoveSpeed = 10f (테스트 값으로 prefab 인라인 7f 와 차이) 변경
6. Play 모드 진입 → Khi 이동 속도 = 10f (SO 값 적용 확인)
7. Khi 데미지 받기 → MaxHealth = SO 값 기반 (Inspector 의 Health.MaximumHealth 가 SO 값으로 변경됐는지 확인)
8. 대시 → Cooldown = SO 값 (DashCooldown 적용 확인)
9. ★ 라이브 튜닝 검증:
   - Play 모드 유지
   - Khi_Stats.asset 의 BaseMoveSpeed = 15f 변경 → "Save Current Values" ContextMenu
   - Console: "[PlayerStatsData] Saved: Khi_Stats" + "[PlayerStatsBinding] Live tuning applied..."
   - Khi 이동 속도 즉시 변경 (15f)
   - MaxHealth / DashCooldown 변경 시 — 즉시 적용 X (다음 Play 시작 시 반영). Console 안내 메시지 확인
10. Khi_Stats.asset 미할당 (Binding 의 _statsData = None) → Play → TDE Inspector 인라인 값 그대로 (fallback 정상)
11. multiplier 시스템 검증: PlayerStatModifierContainer.AddPermanent(StatId.MoveSpeed, 0.2f) 호출 → 이동 속도 = SO base × 1.2 (multiplier 위에 base 정상)
12. 두 prefab (Net + MinimalCharacter2D) 모두 동일 SO 적용 확인
```

---

## 핵심 파일

### 신규 (Claude 작성)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerStatsBinding.cs` | SO → TDE 컴포넌트 자동 주입 + 라이브 튜닝 |

### 변경 (사용자 Unity Editor)

| 경로 | 변경 |
|---|---|
| `LostMemory/Assets/_Project/Prefabs/Characters/TestKhi_Net_AD.prefab` | PlayerStatsBinding 컴포넌트 추가 + Khi_Stats 할당 |
| `LostMemory/Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` | 동일 |

### 무수정 (참고)

| 파일 | 이유 |
|---|---|
| `PlayerHealthStatApplier.cs` (본인) | OnEnable 시점에 Binding 이 변경한 health.MaximumHealth 를 자연스럽게 캐싱 |
| `PlayerMovementStatApplier.cs` (본인) | base 캐싱 X — 매 LateUpdate 직접 사용. Binding 이 변경한 MovementSpeed 자동 반영 |
| `KhiDashController.cs` (본인) | Initialization 시점에 Binding 이 변경한 ConsumptionDuration 캐싱 |
| `PlayerStatsData.cs` (CL-179) | Events hook 그대로 사용 |
| `PlayerStatModifierContainer.cs` (본인) | multiplier 시스템 그대로 |

> 이용호 / 노소연 협업 0. 본인 코드 + TestKhi prefab (본인 영역). 회귀 위험 — Binding 1개 신설 + prefab 컴포넌트 추가만이라 작음.

---

## 후속 ticket 영향

| Ticket | 관계 |
|---|---|
| **CL-181** PlayerStatsCategoryProvider | Balance Editor 좌측 트리에 Player Stats 카테고리. Khi_Stats 등 PlayerStatsData asset 표시. cl173 패턴 그대로 |
| **CL-184 라이브 튜닝 강화** | Applier 에 RefreshBase() public 메서드 추가 → MaxHealth/DashCooldown 도 즉시 라이브 튜닝. 본 CL 의 (i) 한계 해소 |
| **다른 캐릭터 추가 ticket** (미소녀 등) | Binding 컴포넌트 재사용. SO + prefab 추가 |
| **Balance Editor 저장 시 OnAssetSaved 발화** (CL-173 갱신) | Save Current Values ContextMenu 외에 일반 저장도 라이브 튜닝 트리거 |

---

## 작업 순서

| 순서 | 담당 | 작업 |
|---|---|---|
| 1 | (선결) | CL-179 머지 확인 (PlayerStatsData.cs + Khi_Stats.asset) |
| 2 | Claude | `PlayerStatsBinding.cs` 작성 |
| 3 | Claude | 컴파일 오류 없음 확인 (Grep / Read) |
| 4 | 사용자 | TestKhi_Net_AD.prefab 에 PlayerStatsBinding 추가 + Khi_Stats 할당 |
| 5 | 사용자 | TestKhi_MinimalCharacter2D.prefab 동일 작업 |
| 6 | 사용자 | §검증 5-12 실행 |
| 7 | 사용자 | MR 생성 |
| 8 | — | CL-181 (Provider 등록) 진입 |

---

## 예상 시간

| 단계 | 담당 | 시간 |
|---|---|---|
| PlayerStatsBinding.cs 작성 | Claude | 25분 |
| 컴파일 / 검증 (Claude) | Claude | 10분 |
| 사용자 prefab 2개 작업 | 사용자 | 10분 |
| Play 검증 (multiplier × base 통합 / 라이브 튜닝) | 사용자 | 20분 |
| **합계** | | **약 65분** |

→ 2점 ticket. cl178 (3점, 65분) 와 비슷. 회귀 위험 검토 시간 포함.

---

## 메모

- **회귀 위험 大 우려 (master plan §9 Q1) → A2 패턴으로 최소화**: Applier 무수정. Binding 만 신설.
- **NGO 동기화 우려 무효** — 탐색 결과 Player Stat 시스템 NGO 무관 (NetworkBehaviour 0). asset 자체가 동기화 단위
- **TDE 초기화 타이밍** — DefaultExecutionOrder(-100) + Awake 보장. Applier 의 base 캐싱이 Binding 변경 후 발생
- **라이브 튜닝 부분 적용** — MoveSpeed 즉시, MaxHealth/DashCooldown 다음 Play 시작 시. CL-184 또는 후속에서 강화 검토
- 디자이너 안내:
  - **Khi_Stats.asset 가 진실의 출처** — TestKhi prefab Inspector 의 Health/CharacterMovement 인라인 값은 게임 미반영
  - 라이브 튜닝: BossData 와 동일 — Inspector ⫶ → "Save Current Values"
  - MoveSpeed 외엔 다음 Play 진입 시 반영
- master plan epic_uv §11 line 354: "KhiDashController.cs / PlayerHealthStatApplier.cs / PlayerMovementStatApplier.cs (base 값 SO 참조로 전환 — CL-180)" — **본 CL 의 A2 패턴에 따라 갱신 필요**: "Applier 무수정 — PlayerStatsBinding 신설로 회피"

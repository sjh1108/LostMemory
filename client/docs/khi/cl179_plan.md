# CL-179 — PlayerStatsData SO 정의 + base 값 마이그레이션

## Context

캐릭터 base 능력치 (MoveSpeed/MaxHealth/DashCooldown) 통합 SO. master plan §9 Q1 의 **"보류" 결정** 이었으나 사용자 결정으로 진행. **시나리오 B 패턴** (cl172 모방) — SO 정의 + 인스턴스만, **코드 영향 0**. 적용 어댑터는 CL-180 (별도 ticket).

**3점 P3, CL-166 의존**.

### ★ 사용자 결정 (확정)

| 항목 | 결정 |
|---|---|
| **책임 범위** | SO 정의 + Khi 인스턴스만 (cl172 시나리오 B 패턴) |
| **필드 범위** | **3개** — MoveSpeed / MaxHealth / DashCooldown |
| **점프 제외** | Khi 점프 컴포넌트 0건 (탑다운). future-proof 필드도 미추가 (입력 후 미반영 혼동 회피) |
| **어댑터** | 본 CL 외 (CL-180) |
| **Provider 등록** | 본 CL 외 (CL-181) |

### 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| `PlayerStatsData.cs` SO 신설 (3개 필드) | 적용 어댑터 (CL-180) — Applier / KhiDashController 의 base 값 SO 참조 전환 |
| `Khi_Stats.asset` 인스턴스 (사용자 Unity Editor) | Provider 등록 (CL-181) — Balance Editor Player Stats 카테고리 |
| `PlayerStatsDataEvents` 정적 이벤트 hook (CL-180 라이브 튜닝 진입점) | 다른 캐릭터 인스턴스 (미소녀 등 — 아직 없음) |
| | 점프 필드 (Khi 미사용) — 향후 캐릭터 추가 시 별도 작업 |

---

## 현황 (탐색)

### 1.1 작성자 매트릭스 — 모두 김회인 (본인) ★ 협업 X

```bash
git log: StatId.cs / PlayerStatModifierContainer.cs / PlayerHealthStatApplier.cs /
         PlayerMovementStatApplier.cs / KhiDashController.cs
→ 7개 커밋 모두 김회인 (2e72fb79d / 2f97c51da / 772e90366 / 94a645d10 / 9c2bf60db / b43e8495d / f5b132493)
```

### 1.2 StatId enum 12종 ([StatId.cs:8-25](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/StatId.cs:8))

```csharp
AttackPower, AttackSpeed, MoveSpeed, MaxHealth, FinisherDamage,
DashCooldown, HealReceived, Critical, Cooldown, Range, Dodge, Defense
```

→ 본 CL = **base 값 있는 3개만 SO 화** (MoveSpeed/MaxHealth/DashCooldown). 다른 stat 은 base = 1.0 (multiplier 만).

### 1.3 현재 base 값 위치 (TDE Inspector 인라인)

| Stat | 현재 base 위치 | Applier 의 base 캐시 |
|---|---|---|
| MoveSpeed | CharacterMovement.MovementSpeed (TDE) | (캐시 X — 매 LateUpdate `MovementSpeedMultiplier = mul`) |
| MaxHealth | Health.MaximumHealth (TDE) | `_baseMaxHealth = health.MaximumHealth` ([PlayerHealthStatApplier.cs:24](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerHealthStatApplier.cs:24)) |
| DashCooldown | CharacterDash2D.Cooldown.ConsumptionDuration (TDE) | `_baseCooldownDuration = Cooldown.ConsumptionDuration` ([KhiDashController.cs:39](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDashController.cs:39)) |

→ 본 CL 시점엔 SO 가 위 값과 별개로 존재 (디자이너가 같은 값 입력). CL-180 어댑터가 SO → TDE 인라인 덮어씀.

### 1.4 점프 컴포넌트 — 0건

```bash
grep "CharacterJump|JumpHeight|jumpHeight" Runtime/ → No files found
```

→ Khi 점프 X (탑다운). master plan 시트의 "이속/체력/대시쿨/점프" 의 "점프" 는 misnomer. 본 CL 제외.

### 1.5 EnemyDataEvents 패턴 (cl172 가 미리 설계)

cl178 plan §1.2 — `EnemyDataEvents.OnAssetSaved` 가 어댑터 hook 진입점. 동일 패턴으로 `PlayerStatsDataEvents.OnAssetSaved` 마련 → CL-180 어댑터가 즉시 활용.

### 1.6 Khi prefab

`LostMemory/Assets/_Project/Prefabs/Characters/`:
- `TestKhi_Net_AD.prefab`
- `TestKhi_MinimalCharacter2D.prefab`
- `KhiParrySpark.prefab` (이펙트)

→ Khi prefab 2개 (Net 버전, 비-Net 버전). Inspector 의 base 값 (MoveSpeed/MaxHealth/DashCooldown) 디자이너가 Khi_Stats.asset 에 복사 입력.

---

## 설계 결정

### 1. 시나리오 B 패턴 (CL-172 모방) — 코드 영향 0

```
[본 CL]
PlayerStatsData.cs (신규 SO) — 3개 필드
Khi_Stats.asset (신규 인스턴스, 사용자 Unity Editor)
PlayerStatsDataEvents.OnAssetSaved 정적 이벤트 hook (CL-180 진입점)

[무수정 — 본 CL 외]
Applier / KhiDashController / TestKhi prefab — CL-180 책임
```

→ 본 CL 자체는 회귀 위험 0. CL-180 진입 시 회귀 위험 大 (master plan §9 Q1).

### 2. 필드 3개 (사용자 결정 a)

```csharp
[SerializeField] private string _displayName;            // 캐릭터명 (Khi 등)
[SerializeField, Min(0f)] private float _baseMoveSpeed = 7f;
[SerializeField, Min(0f)] private float _baseMaxHealth = 100f;
[SerializeField, Min(0f)] private float _baseDashCooldown = 1.5f;
```

기본값:
- MoveSpeed = 7f (TDE CharacterMovement 표준)
- MaxHealth = 100f
- DashCooldown = 1.5f

→ 디자이너가 Khi prefab 인라인 값 조회 후 정확히 입력 (사용자 §인스턴스 가이드).

### 3. namespace / 파일 위치

| 파일 | 경로 | namespace |
|---|---|---|
| `PlayerStatsData.cs` | `Runtime/Combat/` | `LostMemory.Combat` |
| `Khi_Stats.asset` | `Assets/_Project/ScriptableObjects/Player/` (사용자 신설) | — |

근거:
- StatId / PlayerStatModifierContainer / Applier 들이 `LostMemory.Combat` ns. 같은 ns 채택 (어댑터 호환)
- 폴더는 `Combat/` (코드) vs `Player/` (asset) 분리 — Combat 은 코드 도메인, Player 는 asset 카테고리

### 4. PlayerStatsDataEvents — Live tuning hook (cl172 패턴)

```csharp
public static class PlayerStatsDataEvents
{
    public static event Action<PlayerStatsData> OnAssetSaved;
    internal static void RaiseAssetSaved(PlayerStatsData asset) => OnAssetSaved?.Invoke(asset);
}
```

- "Save Current Values" ContextMenu → 발화
- CL-180 어댑터가 OnAssetSaved 구독 → Play 모드 라이브 튜닝
- Runtime → Editor 어셈블리 직접 참조 회피용

### 5. 인스턴스 파일명

- 컨벤션: cl172 의 `Bertha_Boss.asset` 패턴 = `<캐릭터>_<종류>.asset`
- 본 CL: **`Khi_Stats.asset`**
- 향후 캐릭터: `Misonyo_Stats.asset` 등

### 6. 점프 placeholder 미추가 — 디자이너 혼동 회피

옵션 비교 (사용자 결정 a):
- (a) 3개만 — 입력 가능 = 게임 적용. 일치
- (b) 4개 (점프 포함) — 점프 입력해도 미반영 → 디자이너 혼동

→ (a) 채택. 향후 점프 추가 시 필드 추가는 FormerlySerializedAs 안전 (cl172 BossData 와 동일).

### 7. CreateAssetMenu 위치

`menuName = "LostMemory/Player/Player Stats Data"` — `LostMemory/Enemies/Boss Data` 와 일관 (Player 카테고리 신설).

---

## 신규 파일 — 코드

### PlayerStatsData.cs

```csharp
using System;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// CL-179: 캐릭터 base 능력치 통합 SO. CL-180 어댑터가 OnEnable 시점에 TDE 컴포넌트
    /// (CharacterMovement / Health / CharacterDash2D) 의 인라인 값에 주입 예정.
    /// 본 CL 시점엔 SO 만 존재 — 게임 미반영 (디자이너 안내 필요).
    /// 점프는 Khi 미사용으로 제외 (탑다운). 향후 점프 캐릭터 추가 시 필드 추가.
    /// </summary>
    public static class PlayerStatsDataEvents
    {
        public static event Action<PlayerStatsData> OnAssetSaved;
        internal static void RaiseAssetSaved(PlayerStatsData asset) => OnAssetSaved?.Invoke(asset);
    }

    [CreateAssetMenu(fileName = "PlayerStatsData",
                     menuName = "LostMemory/Player/Player Stats Data",
                     order = 20)]
    public class PlayerStatsData : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField, Min(0f)] private float _baseMoveSpeed = 7f;
        [SerializeField, Min(0f)] private float _baseMaxHealth = 100f;
        [SerializeField, Min(0f)] private float _baseDashCooldown = 1.5f;

        public string DisplayName     => _displayName;
        public float  BaseMoveSpeed   => _baseMoveSpeed;
        public float  BaseMaxHealth   => _baseMaxHealth;
        public float  BaseDashCooldown => _baseDashCooldown;

#if UNITY_EDITOR
        [ContextMenu("Save Current Values")]
        private void SaveCurrentValues()
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
            PlayerStatsDataEvents.RaiseAssetSaved(this);
            Debug.Log($"[PlayerStatsData] Saved: {name}");
        }
#endif
    }
}
```

---

## 인스턴스 가이드 (Unity Editor — 사용자)

| 순서 | 작업 |
|---|---|
| 1 | `Assets/_Project/ScriptableObjects/Player/` 폴더 신설 (없으면) |
| 2 | Project 창 → 우클릭 → Create → LostMemory → Player → Player Stats Data |
| 3 | 파일명: `Khi_Stats` |
| 4 | TestKhi prefab (TestKhi_Net_AD.prefab 또는 MinimalCharacter2D) Inspector 에서 값 조회 |
| 5 | Khi_Stats.asset Inspector 에 값 입력: |

| 필드 | 값 출처 |
|---|---|
| DisplayName | `Khi` |
| BaseMoveSpeed | TestKhi prefab → CharacterMovement → MovementSpeed 값 |
| BaseMaxHealth | TestKhi prefab → Health → MaximumHealth 값 |
| BaseDashCooldown | TestKhi prefab → CharacterDash2D → Cooldown → ConsumptionDuration 값 |

→ TestKhi prefab Inspector 값과 정확히 일치시킴. CL-180 어댑터 진입 시점에 SO 가 prefab 인라인 값을 덮어쓰는데, 일치하면 게임 영향 0.

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **데이터-코드 비동기** — Khi_Stats.asset 변경해도 게임 미반영 (CL-180 어댑터 전까지) | cl172 §위험 #1 와 동일 패턴. 디자이너 안내 필요 |
| 2 | **SO 값과 prefab Inspector 값 차이** | 본 CL 시점엔 무관 (SO 게임 미반영). CL-180 진입 시 SO 우선 정책 명시 |
| 3 | **CL-180 회귀 위험 大** | master plan §9 Q1 보류 근거 (적용 어댑터 실행 타이밍 / TDE 초기화 / NGO 동기화 등). 본 CL 자체는 코드 영향 0 라 안전 |
| 4 | **점프 미포함** | Khi 미사용. 향후 점프 캐릭터 추가 시 필드 추가 (FormerlySerializedAs 안전) |
| 5 | **TestKhi prefab 2개 — Net 버전 vs MinimalCharacter2D** | Khi_Stats.asset 1개로 둘 다 적용 (CL-180 어댑터가 어떤 prefab 의 인스턴스 든 동일 SO 사용). 두 prefab 의 Inspector 값이 다르면 디자이너 결정 필요 |
| 6 | **PlayerStatsDataEvents.OnAssetSaved 발화 시점** | ContextMenu Save Current Values 만. CL-181 (Provider 등록) 후 Balance Editor 저장에서도 발화시킬지 별도 결정 |
| 7 | **다른 캐릭터 인스턴스 없음** | Khi 만. 미소녀 등 추가 시 별도 인스턴스. SO 클래스는 generic |
| 8 | **NGO 동기화** | 본 CL 시점엔 무관 (게임 미반영). CL-180 어댑터에서 호스트/클라가 같은 SO 로드 → 자동 동기화 가정 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. PlayerStatsData.cs 컴파일 OK
2. PlayerStatsDataEvents 정적 클래스 노출 확인
3. 다른 코드 무수정 (Applier / KhiDashController / StatId / Container) 확인
```

### Unity Editor 검증 (사용자)

```
4. Create → LostMemory → Player → Player Stats Data 메뉴 표시
5. Khi_Stats.asset 신설 → Inspector 모든 필드 (DisplayName / BaseMoveSpeed / BaseMaxHealth / BaseDashCooldown) 편집 가능
6. Min(0f) 검증 — 음수 입력 시 0 으로 clamp
7. Save Current Values ContextMenu 클릭 → Console: "[PlayerStatsData] Saved: Khi_Stats"
8. CL-181 이전이라 Balance Editor 의 Player Stats 카테고리는 미표시 (정상)
9. 게임 미반영 — TestKhi prefab Inspector 값 그대로 동작 (정상, CL-180 전까지)
```

---

## 핵심 파일

### 신규 (Claude 작성)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerStatsData.cs` | base 값 통합 SO + Events hook |

### 신규 (사용자 Unity Editor)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/ScriptableObjects/Player/Khi_Stats.asset` | Khi 캐릭터 base 값 인스턴스 |

### 무수정 (참고)

| 파일 | 이유 |
|---|---|
| `StatId.cs` / `PlayerStatModifierContainer.cs` | multiplier 시스템 — 본 CL 외 |
| `PlayerHealthStatApplier.cs` / `PlayerMovementStatApplier.cs` | base 값 SO 참조 전환은 CL-180 |
| `KhiDashController.cs` | base 값 SO 참조 전환은 CL-180 |
| TestKhi prefab (Net_AD / MinimalCharacter2D) | 본 CL 시점엔 prefab 무수정. CL-180 진입 시에도 어댑터 패턴 (cl178 와 유사) 으로 무수정 가능성 검토 |

> 다른 사람 코드 침범 0. 모두 본인 영역. 본 CL = 신규 SO + 사용자 인스턴스 1개.

---

## 후속 ticket 영향

| Ticket | 관계 |
|---|---|
| **CL-180** PlayerStatsApplier 정리 + base × multiplier 통합 | 본 CL 의 SO base 값을 Applier / KhiDashController 가 참조하도록 전환. 회귀 위험 大 (master plan §9 Q1). cl178 와 같은 정적 Injector 패턴으로 prefab 무수정 가능 검토 권장 |
| **CL-181** PlayerStatsCategoryProvider | Balance Editor 좌측 트리에 Player Stats 카테고리 노출. Khi_Stats 등 PlayerStatsData asset 표시. cl173 패턴 그대로 |
| **다른 캐릭터 추가 ticket** (미소녀 등) | PlayerStatsData 클래스 재사용. 새 인스턴스 (Misonyo_Stats.asset 등) 작성. Future-proof |

---

## 작업 순서

| 순서 | 담당 | 작업 |
|---|---|---|
| 1 | (선결) | CL-166 머지 확인 |
| 2 | Claude | `PlayerStatsData.cs` 작성 |
| 3 | Claude | 컴파일 오류 없음 확인 |
| 4 | 사용자 | `Assets/_Project/ScriptableObjects/Player/` 폴더 신설 |
| 5 | 사용자 | Khi_Stats.asset 신설 + TestKhi prefab Inspector 값 조회 + 입력 |
| 6 | 사용자 | §검증 4-9 실행 |
| 7 | 사용자 | MR 생성 |
| 8 | — | CL-180 (회귀 위험 검토 후) 또는 CL-181 진입 |

---

## 예상 시간

| 단계 | 담당 | 시간 |
|---|---|---|
| PlayerStatsData.cs 작성 | Claude | 15분 |
| 컴파일 / 검증 (Claude) | Claude | 5분 |
| 사용자 Unity Editor (폴더 + asset + 값 입력) | 사용자 | 10분 |
| **합계** | | **약 30분** |

→ 3점 ticket 의 단순 SO 정의 — 매우 빠름. cl172 (3점, 40분) 와 비슷.

---

## 메모

- 본 CL 자체는 **회귀 위험 0** (코드 영향 X). CL-180 어댑터 진입 시점에 회귀 위험 大 (master plan §9 Q1)
- master plan §9 Q1 의 보류 근거 (회귀 / NGO 동기화 / TDE 초기화 등) 는 **CL-180 진입 결정 시점에 재검토**. 본 CL 진행해도 CL-180 보류 유지 가능
- cl178 의 정적 Injector + Registry SO 패턴이 CL-180 에서도 유효 (PlayerStatsRegistry + PlayerStatsInjector). 이용호 영역 X (모두 본인) 라 패턴 자유 — Applier 직접 수정도 OK
- 디자이너 안내:
  - Khi_Stats.asset 의 값은 **현재 게임 미반영** (CL-180 진입 전까지). cl172 의 Bertha_Boss.asset 와 동일 상태
  - CL-181 진입 후 Balance Editor 의 Player Stats 카테고리에서 편집 가능
  - 라이브 튜닝: ContextMenu "Save Current Values" → CL-180 어댑터가 즉시 반영 (CL-180 진입 후)
- master plan epic_uv §11 line 354: "KhiDashController.cs / PlayerHealthStatApplier.cs / PlayerMovementStatApplier.cs (base 값 SO 참조로 전환 — CL-180)" — 본 CL 패턴 결정에 따라 CL-180 의 어댑터 전략 (직접 수정 vs Injector) 검토 필요

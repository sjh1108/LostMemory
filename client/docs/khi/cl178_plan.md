# CL-178 — EnemyData/BossData 적용 어댑터 (Bertha 만, 이용호 무관 패턴)

## Context

CL-172 의 `Bertha_Boss.asset` 값을 게임에 반영. Balance Editor (CL-173) 에서 디자이너가 phase 임계값 변경 → 게임에 즉시 적용. cl172_plan §위험 #1 "데이터-코드 비동기" 해소 (Bertha 만).

**3점 P3, CL-172 의존**. master plan 의 "보류" 상태였으나 사용자 결정으로 진행.

### ★ 사용자 결정 (확정)

| 항목 | 결정 |
|---|---|
| **책임 범위** | Bertha 만 (일반 몹 별도 ticket 보류) |
| **이용호 영역 정책** | **0 침범** — BerthaBossPhaseController.cs, Bertha prefab, 일반 몹 prefab 모두 무수정 |
| **패턴** | **정적 Injector + RuntimeInitializeOnLoadMethod + Registry SO** (이용호 prefab/씬 변경 X) |
| **Asset 로드** | (B) BossDataRegistry SO + Resources 폴더 |
| **라이브 튜닝** | EnemyDataEvents.OnAssetSaved 구독 (CL-172 hook) |

### 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| `BerthaBossDataInjector.cs` 정적 클래스 (자동 hook + 라이브 튜닝) | BerthaBossPhaseController.cs 변경 (이용호 영역) |
| `BossDataRegistry.cs` SO (Bertha 등 등록) | Bertha prefab 변경 (이용호 영역) |
| 사용자: `Resources/BossDataRegistry.asset` 신설 + Bertha_Boss 등록 | 일반 몹 어댑터 (별도 ticket — 일반 몹 등장 시점) |
| Bertha 의 phase 임계값 BossData 주입 | Health 컴포넌트 (TDE) MaxHealth 주입 (TDE 영역, 별도 결정) |
| | 씬 변경 |

---

## 현황 (탐색)

### 1.1 BerthaBossPhaseController (이용호 작성, 99d842c90)

[BerthaBossPhaseController.cs:74-87](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/BerthaBossPhaseController.cs:74) — **Configure() public API 이미 존재**:

```csharp
public void Configure(
    Health configuredHealth,
    float configuredPhase2ThresholdNormalized,
    float configuredPhase3ThresholdNormalized,
    bool configuredDebugLogging)
{
    health = configuredHealth;
    phase2ThresholdNormalized = configuredPhase2ThresholdNormalized;
    phase3ThresholdNormalized = configuredPhase3ThresholdNormalized;
    debugLogging = configuredDebugLogging;
    NormalizeThresholds();
    _currentPhase = ResolvePhase(GetNormalizedHealth());
}
```

→ 외부에서 호출 가능. 이용호 코드 무수정으로 어댑터 가능.

### 1.2 EnemyDataEvents (CL-172 가 미리 설계, 본인 작성)

[EnemyData.cs:8-14](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyData.cs:8):
```csharp
public static class EnemyDataEvents
{
    public static event Action<EnemyData> OnAssetSaved;
    internal static void RaiseAssetSaved(EnemyData asset) => OnAssetSaved?.Invoke(asset);
}
```

발화 지점:
- [EnemyData.cs:32-39](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyData.cs:32) — "Save Current Values" ContextMenu
- [BossData.cs:21-29](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/BossData.cs:21) — 동일

→ ★ **CL-172 가 어댑터 hook 진입점을 미리 설계**. CL-178 어댑터가 OnAssetSaved 구독 → 라이브 튜닝.

### 1.3 작성자 매트릭스

| 파일/asset | 작성자 | 본 CL 처리 |
|---|---|---|
| BerthaBossPhaseController.cs | **이용호** (99d842c90) | **무수정** ★ |
| Bertha prefab (BerthaRoot.prefab / BerthaRoot2.prefab) | **이용호** | **무수정** ★ |
| 일반 몹 prefab (Orc/Skeleton/StoneGolem/Moose 등) | **이용호** | **무수정** (본 CL 외) |
| EnemyCatalog.cs | **김회인** (4b02b1e27) | 무수정 (본 CL 외) |
| EnemyData.cs / BossData.cs / Bertha_Boss.asset | **김회인** (CL-172) | 무수정 |
| BerthaBossDataInjector.cs / BossDataRegistry.cs | **김회인 (신규)** | ✅ |
| Resources/BossDataRegistry.asset | **사용자 신규** | ✅ |

→ 이용호 협업 0. 본인 코드 + 사용자 Editor 작업 (Registry asset 1개 신설) 만.

### 1.4 Bertha 현재 동작

- BerthaBossPhaseController.Awake (line 41-46): `RefreshReferences` + `NormalizeThresholds` + `ResolvePhase`. **Inspector 인라인 값** (phase2=0.7, phase3=0.3) 사용
- BossData 미참조 — Bertha_Boss.asset 의 값은 게임 미반영 상태 (cl172_plan §위험 #1)

→ 본 CL 어댑터가 Awake 후 Configure() 호출하여 BossData 값으로 덮어씀.

### 1.5 Resources 폴더

본 CL 의 Registry 가 처음 Resources 폴더 사용일 수도. 폴더 없으면 사용자가 신설.

---

## 설계 결정

### 1. ★ 정적 Injector 패턴 (이용호 회피)

```
[Claude 신규] BerthaBossDataInjector.cs (정적 클래스)
    ↓ Resources.Load<BossDataRegistry>("BossDataRegistry")
[Claude 신규] BossDataRegistry SO (Resources/)
    ↓ Entries[]
[CL-172] Bertha_Boss.asset (BossData)
    ↓ Configure() 인자 변환
[이용호 무수정] BerthaBossPhaseController.Configure(...)
```

→ 본인 코드만 신설. 이용호 영역 0 침범.

### 2. RuntimeInitializeOnLoadMethod(AfterSceneLoad)

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
private static void Initialize() { ... }
```

- Unity 가 모든 Awake 후 자동 호출 (씬 로드 시)
- 씬 / prefab / GameObject 변경 X
- 본인 정적 메서드 1개

순서 보장:
- BerthaBossPhaseController.Awake (line 41) → Inspector 인라인 값으로 초기화
- BerthaBossPhaseController.OnEnable (line 48) → Health 이벤트 구독 + 첫 EvaluatePhase
- **AfterSceneLoad → Injector.Initialize → Configure() 호출 → BossData 값으로 덮어씀**

→ Inspector 값은 잠깐 사용되지만 즉시 BossData 덮어씀. 게임플레이 영향 X.

### 3. EnemyDataEvents.OnAssetSaved 구독 — 라이브 튜닝

```csharp
EnemyDataEvents.OnAssetSaved -= OnBossDataSaved;   // 중복 구독 방지
EnemyDataEvents.OnAssetSaved += OnBossDataSaved;
```

발화 시점:
- 디자이너가 BossData asset 의 Inspector ⫶ → "Save Current Values" 클릭
- → 어댑터가 즉시 Configure() 재호출 → 게임 반영

라이브 튜닝 흐름:
```
디자이너: Bertha_Boss.asset 의 phase2 = 0.5 변경
        → "Save Current Values" 클릭
        → EnemyDataEvents.OnAssetSaved 발화
        → BerthaBossDataInjector.OnBossDataSaved
        → BerthaBossPhaseController.Configure() 재호출
        → 즉시 phase2 = 0.5 반영 (다음 데미지부터)
```

### 4. BossDataRegistry SO 패턴

확장성 + Bertha_Boss.asset 위치 그대로 + CL-173 영향 0.

```csharp
[CreateAssetMenu(fileName = "BossDataRegistry",
                 menuName = "LostMemory/Enemies/Boss Data Registry")]
public class BossDataRegistry : ScriptableObject
{
    [SerializeField] private BossData[] _entries;
    public IReadOnlyList<BossData> Entries => _entries;
}
```

사용자 작업: `Resources/BossDataRegistry.asset` 신설 + Inspector 에서 Bertha_Boss 추가.

### 5. 매칭 전략 — MVP 컴포넌트 타입

본 CL = Bertha 1개. 단순 매칭:

```csharp
private static void ApplyForBoss(BossData boss)
{
    // MVP: BerthaBossPhaseController 단일 매칭
    var controller = Object.FindAnyObjectByType<BerthaBossPhaseController>();
    if (controller == null) return;
    // ... Configure() 호출
}
```

→ 일반 몹 추가 시 매칭 전략 확장 필요 (예: BossData 에 `targetComponentType` 필드 추가, 또는 별도 EnemyDataApplier 분리). 본 CL 외.

### 6. Bertha 인라인 값 vs BossData 우선순위

**BossData 우선** — Configure() 가 인라인 값 덮어씀.

대안 검토:
- (a) BossData 우선 (본 CL 채택) — 단순, 디자이너 의도 명확
- (b) 인라인 값 우선 — 어댑터 의미 X
- (c) 인라인 값 비어있으면 BossData fallback — phase 임계값은 항상 값 있음 (Range 0~1)

→ (a) 채택. 디자이너 안내: "Bertha_Boss.asset 의 값이 게임에서 사용됨. Bertha prefab 의 Inspector phase 임계값은 표시만, 게임 미반영"

### 7. Health / MaxHealth — 본 CL 외

PhaseController.Configure() 가 Health 인자 받음. 어댑터가 GetComponent<Health>() 로 Health 컴포넌트 가져와 전달.

**MaxHealth 자체 (BossData.MaxHealth) 는 Health 컴포넌트에 주입 X** (TDE Health 의 MaximumHealth 변경은 별도 정책 — 본 CL 외).

→ 본 CL = phase 임계값만 주입. MaxHealth 는 향후 ticket.

### 8. 매칭 안전성 — 시점 / 멀티 인스턴스

- AfterSceneLoad 시점 = 모든 Awake/OnEnable 후. PhaseController 의 Health 이벤트 구독 후. Configure() 재호출 안전.
- FindAnyObjectByType — 단일 Bertha 가정 (현재). 멀티 인스턴스 시 1개만 매칭 → 향후 매칭 전략 확장.
- 씬 전환 시 — RuntimeInitializeOnLoadMethod(AfterSceneLoad) 가 매 씬 로드 후 호출. Bertha 가 다른 씬에 등장하면 자동 처리.

### 9. namespace / 파일 위치

| 파일 | 경로 | namespace |
|---|---|---|
| `BossDataRegistry.cs` | `Runtime/Enemies/` | `LostMemory.Enemies` |
| `BerthaBossDataInjector.cs` | `Runtime/Enemies/` | `LostMemory.Enemies` |
| `BossDataRegistry.asset` | `Assets/_Project/Resources/` (사용자 신설) | — |

→ Runtime 어셈블리 (Assembly-CSharp). EnemyData/BossData 와 같은 namespace.

---

## 신규 파일 — 코드

### A. `BossDataRegistry.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Enemies
{
    /// <summary>
    /// CL-178: 게임에 적용할 BossData 등록 SO. Resources 폴더에 두고 BerthaBossDataInjector 가 로드.
    /// 일반 몹 추가 시 EnemyDataRegistry 분리 또는 본 SO 확장.
    /// </summary>
    [CreateAssetMenu(fileName = "BossDataRegistry",
                     menuName = "LostMemory/Enemies/Boss Data Registry",
                     order = 12)]
    public class BossDataRegistry : ScriptableObject
    {
        [Tooltip("게임에 적용할 BossData 인스턴스. Resources 폴더에 두고 Injector 가 자동 로드.")]
        [SerializeField] private BossData[] _entries = Array.Empty<BossData>();

        public IReadOnlyList<BossData> Entries => _entries;
    }
}
```

### B. `BerthaBossDataInjector.cs`

```csharp
using LostMemory.Enemies.Boss.Bertha;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    /// <summary>
    /// CL-178: Bertha_Boss.asset 값을 BerthaBossPhaseController 에 자동 주입.
    /// 정적 hook 패턴 — Bertha prefab / 씬 무수정 (이용호 영역 회피).
    /// 라이브 튜닝: EnemyDataEvents.OnAssetSaved 구독 → Save Current Values 시 즉시 재적용.
    /// </summary>
    public static class BerthaBossDataInjector
    {
        private const string RegistryResourcePath = "BossDataRegistry";
        private static BossDataRegistry _registry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            _registry = Resources.Load<BossDataRegistry>(RegistryResourcePath);
            if (_registry == null)
            {
                Debug.LogWarning(
                    "[BerthaBossDataInjector] Resources/BossDataRegistry.asset 미발견. " +
                    "BossData 값 게임 미반영. CL-178 §사용자 작업 참조.");
                return;
            }

            // 중복 구독 방지 (도메인 리로드 후 재진입)
            EnemyDataEvents.OnAssetSaved -= OnBossDataSaved;
            EnemyDataEvents.OnAssetSaved += OnBossDataSaved;

            ApplyAll();
        }

        private static void OnBossDataSaved(EnemyData data)
        {
            if (data is BossData boss) ApplyForBoss(boss);
        }

        private static void ApplyAll()
        {
            if (_registry == null) return;
            foreach (var boss in _registry.Entries)
            {
                if (boss != null) ApplyForBoss(boss);
            }
        }

        private static void ApplyForBoss(BossData boss)
        {
            // MVP: BerthaBossPhaseController 단일 매칭. 일반 몹 추가 시 매칭 전략 확장 필요.
            var controller = Object.FindAnyObjectByType<BerthaBossPhaseController>();
            if (controller == null)
            {
                // Bertha 가 씬에 없는 경우 (다른 씬 / 보스 미등장) — 정상
                return;
            }

            var health = controller.GetComponent<Health>();
            controller.Configure(
                configuredHealth: health,
                configuredPhase2ThresholdNormalized: boss.Phase2ThresholdNormalized,
                configuredPhase3ThresholdNormalized: boss.Phase3ThresholdNormalized,
                configuredDebugLogging: false);

            Debug.Log(
                $"[BerthaBossDataInjector] Applied {boss.name} → BerthaBossPhaseController " +
                $"(phase2={boss.Phase2ThresholdNormalized}, phase3={boss.Phase3ThresholdNormalized})");
        }
    }
}
```

---

## 사용자 작업 (Unity Editor)

| 순서 | 작업 |
|---|---|
| 1 | `Assets/_Project/Resources/` 폴더 신설 (없으면) |
| 2 | Project 창에서 위 폴더 선택 → 우클릭 → Create → LostMemory → Enemies → Boss Data Registry |
| 3 | 파일명: `BossDataRegistry` (Resources.Load 경로와 정확히 일치) |
| 4 | Inspector 에서 Entries 배열 size = 1 → element 0 = `Bertha_Boss.asset` 할당 |
| 5 | 저장 (Ctrl+S) |

향후 일반 몹 추가 시: Entries 에 추가 등록 (코드 변경 X — 단, BerthaBossDataInjector 의 매칭 전략은 BerthaBossPhaseController 만이라 일반 몹은 매칭 X. 일반 몹 어댑터 별도 ticket 에서 처리).

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **Resources/BossDataRegistry.asset 미신설** | Injector Initialize 가 LogWarning + 조기 return. Bertha 값 게임 미반영. 사용자가 §사용자 작업 진행 필요 |
| 2 | **Resources 폴더 안티패턴** | Registry 1개만 두는 패턴이라 빌드 부담 작음. Addressables 이전은 별도 ticket |
| 3 | **EnemyDataEvents.OnAssetSaved 가 ContextMenu 만 발화** | Balance Editor (CL-173) 의 일반 저장 (Ctrl+S) 으론 발화 X. 디자이너 안내 — "라이브 튜닝은 BossData asset Inspector 의 ⫶ → Save Current Values" |
| 4 | **Bertha Inspector 인라인 값 vs BossData 차이** | Configure() 가 덮어씀. 디자이너 안내 — Bertha prefab Inspector 의 phase 임계값은 게임 미반영. Bertha_Boss.asset 만 의미 |
| 5 | **FindAnyObjectByType — 멀티 Bertha 인스턴스** | 1개만 매칭. 현재 단일 Bertha 가정. 향후 멀티 인스턴스 시 매칭 전략 확장 (예: 컴포넌트별 직접 참조) |
| 6 | **AfterSceneLoad 시점 vs OnEnable 충돌** | Unity 보장 — AfterSceneLoad 는 모든 OnEnable 후. PhaseController 의 Health 이벤트 구독 후 Configure 재호출. 안전 |
| 7 | **도메인 리로드 후 정적 변수 reset** | RuntimeInitializeOnLoadMethod 가 도메인 리로드 후 매번 호출. _registry 재로드. 중복 구독은 -= 후 += 로 방지 |
| 8 | **일반 몹 어댑터 X** | 본 CL 외. master plan §위험 #1 의 "일반 몹" 은 보류 유지. 일반 몹 등장 ticket 시점에 별도 작업 |
| 9 | **이용호의 BerthaBossPhaseController 변경 시** | Configure() 시그니처 변경 시 본 CL 어댑터 컴파일 오류 — 빠른 발견. 시그니처 안정 가정 (이용호 무수정 약속) |
| 10 | **Health 컴포넌트 미존재** | controller.GetComponent<Health>() 가 null 반환 시 Configure 의 health 인자 null. PhaseController 의 OnEnable 가드 (line 53-56) 가 처리. Bertha prefab 에 Health 항상 있다고 가정 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. BossDataRegistry.cs / BerthaBossDataInjector.cs 컴파일 OK
2. BerthaBossPhaseController.cs (이용호) 무수정 확인
3. Bertha prefab / 일반 몹 prefab 무수정 확인
4. EnemyData.cs / BossData.cs / Bertha_Boss.asset 무수정 확인
```

### Unity Editor 검증 (사용자)

```
5. Resources/BossDataRegistry.asset 신설 + Bertha_Boss 등록 (§사용자 작업)
6. Bertha_Boss.asset 의 phase2 = 0.7, phase3 = 0.3 확인
7. Bertha 가 등장하는 씬 (보스방) 진입
8. Console: "[BerthaBossDataInjector] Applied Bertha_Boss → BerthaBossPhaseController (phase2=0.7, phase3=0.3)"
9. Bertha 의 Inspector 에서 phase2/phase3 = 0.7 / 0.3 (BossData 값 적용 확인)
10. Bertha 데미지 → phase 전환 동작 확인 (게임플레이)
11. ★ 라이브 튜닝 검증:
   - Play 모드 유지
   - Bertha_Boss.asset 의 phase2 = 0.5 변경
   - Inspector ⫶ → "Save Current Values" 클릭
   - Console: "[BossData] Saved: Bertha_Boss" + "[BerthaBossDataInjector] Applied Bertha_Boss → BerthaBossPhaseController (phase2=0.5, ...)"
   - 다음 데미지 시 phase 전환 = 0.5 임계값 (확인 가능 시)
12. Resources/BossDataRegistry.asset 삭제 → 다시 Play → Console: "[BerthaBossDataInjector] Resources/BossDataRegistry.asset 미발견..." LogWarning + 미반영 (Inspector 인라인 값 사용)
```

---

## 핵심 파일

### 신규 (Claude 작성)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/Enemies/BossDataRegistry.cs` | BossData 등록 SO |
| `LostMemory/Assets/_Project/Scripts/Runtime/Enemies/BerthaBossDataInjector.cs` | 정적 hook + 라이브 튜닝 |

### 신규 (사용자 Unity Editor)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Resources/BossDataRegistry.asset` | Bertha_Boss 등록 |

### 무수정 — 이용호 영역 (★ 핵심)

| 파일 | 이유 |
|---|---|
| `BerthaBossPhaseController.cs` (이용호 99d842c90) | Configure() public API 만 호출. 코드 0 변경 |
| `Bertha prefab` (BerthaRoot/BerthaRoot2) | 정적 hook 패턴 — prefab 무수정 |
| 일반 몹 prefab (Orc/Skeleton/StoneGolem 등) | 본 CL 외 (별도 ticket) |
| 씬 (보스방 등) | 어댑터가 FindAnyObjectByType — 씬 무수정 |

### 무수정 — 본인 영역

| 파일 | 이유 |
|---|---|
| `EnemyData.cs` / `BossData.cs` (CL-172) | EnemyDataEvents.OnAssetSaved hook 그대로 사용 |
| `EnemyCatalog.cs` (CL-034) | 본 CL 외 (id → prefab 매핑은 별도 정책) |
| `BossDataCategoryProvider.cs` (CL-173) | Bertha_Boss.asset 위치 그대로 (B 패턴 채택) — Balance Editor 영향 0 |

> 이용호 협업 0. 본인 코드 신규 2개 + 사용자 Editor asset 1개 신설.

---

## 후속 ticket 영향

| Ticket | 관계 |
|---|---|
| **일반 몹 등장 ticket** | BerthaBossDataInjector 의 매칭 전략 (FindAnyObjectByType<BerthaBossPhaseController>) 일반화 필요. EnemyDataApplier 분리 또는 매칭 SO 등록 |
| **CL-173 라이브 튜닝 강화** | BossData "Save Current Values" ContextMenu 외에 Balance Editor 의 Ctrl+S 저장에서도 EnemyDataEvents.OnAssetSaved 발화 (CL-173 갱신) |
| **Health/MaxHealth 주입** | TDE Health.MaximumHealth 어댑터 — 별도 정책 결정 필요. 본 CL 외 |
| **Bertha 리팩 ticket** (이용호) | BerthaBossPhaseController.Configure() 시그니처 변경 시 본 CL Injector 갱신 필요 — 컴파일 오류로 빠른 발견 |

---

## 작업 순서

| 순서 | 담당 | 작업 |
|---|---|---|
| 1 | (선결) | CL-172 머지 확인 (EnemyData/BossData/Bertha_Boss.asset 존재) |
| 2 | Claude | `BossDataRegistry.cs` 작성 |
| 3 | Claude | `BerthaBossDataInjector.cs` 작성 |
| 4 | Claude | 컴파일 오류 없음 확인 |
| 5 | 사용자 | Unity Editor — `Assets/_Project/Resources/` 폴더 신설 (없으면) |
| 6 | 사용자 | Resources/BossDataRegistry.asset 신설 + Bertha_Boss 할당 |
| 7 | 사용자 | Play 모드 — §검증 8-12 실행 |
| 8 | 사용자 | MR 생성 |
| 9 | — | (보류) 일반 몹 어댑터 / Health MaxHealth 주입 / CL-173 라이브 튜닝 강화 — 별도 ticket |

---

## 예상 시간

| 단계 | 담당 | 시간 |
|---|---|---|
| BossDataRegistry.cs 작성 | Claude | 10분 |
| BerthaBossDataInjector.cs 작성 | Claude | 25분 |
| 컴파일 / 검증 (Claude) | Claude | 10분 |
| Unity Editor 작업 (Resources 폴더 + Registry asset) | 사용자 | 5분 |
| Play 검증 | 사용자 | 15분 |
| **합계** | | **약 65분** |

→ 3점 ticket. 협업 X 라 빠름. cl175 (~80분) 와 비슷.

---

## 메모

- 본 CL 의 가장 큰 기술 결정 = **정적 Injector + Registry SO 패턴**. 이용호 영역 0 침범 + 본인 코드만. 협업 부채 X
- master plan §위험 #1 ("데이터-코드 비동기") 해소 — Bertha 만. 일반 몹은 향후 ticket
- 라이브 튜닝 패턴은 CL-172 가 미리 설계 (EnemyDataEvents.OnAssetSaved). CL-178 어댑터가 즉시 활용
- 디자이너 안내 (사용자가 디자이너에게):
  - Bertha_Boss.asset (Balance Editor 의 Bosses 카테고리) 가 게임 적용
  - Bertha prefab Inspector 의 phase 임계값은 표시만, 게임 미반영
  - 라이브 튜닝: BossData asset Inspector ⫶ → "Save Current Values" 클릭
  - 일반 몹은 본 CL 미적용 (별도 ticket 보류)
- master plan epic_uv §11 의 "BerthaBossPhaseController.cs (SO 참조 전환 — CL-178)" 는 **본 CL 패턴 변경에 따라 갱신 필요**: "BerthaBossPhaseController 무수정 — 정적 Injector 패턴으로 회피"

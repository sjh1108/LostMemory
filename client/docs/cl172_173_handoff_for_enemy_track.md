# CL-172 / CL-173 산출물 → Enemy 트랙 핸드오프 (2026-05-06)

## 문서 목적

Balance Editor 트랙(김회인) 에서 Enemy 데이터를 위한 ScriptableObject 토대를 마련했다. 본 문서는 **Enemy 트랙 담당자가 후속 작업(CL-178 적용 어댑터, 일반 몹 등장 ticket 등)을 시작할 때 필요한 정보**를 한 장에 모은다.

CL-172 / CL-173 작업 자체의 결정 근거나 디테일은 별도 문서(아래 참고). 본 문서는 진입점 안내.

## 한 줄 요약

EnemyData / BossData ScriptableObject 클래스가 신설됐고 Balance Editor 에 카테고리로 노출됐다. **단, asset 값은 게임에 자동 반영되지 않는다 — 어댑터(CL-178)를 작성해야 실제 동작에 연결된다.**

---

## 1. 산출물 — 무엇이 준비됐나

### 1.1 SO 클래스 (Runtime)

| 파일 | 클래스 | 역할 |
|---|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyData.cs` | `EnemyData : ScriptableObject` | 일반 몹 기본 스탯 (displayName / maxHealth / moveSpeed / expReward / dropWeight) |
| (같은 파일 내) | `static EnemyDataEvents` | `OnAssetSaved` 정적 이벤트 — Editor → Runtime hook 점 |
| `LostMemory/Assets/_Project/Scripts/Runtime/Enemies/BossData.cs` | `BossData : EnemyData` | 보스 전용 추가 (phase2ThresholdNormalized / phase3ThresholdNormalized) |

namespace: 둘 다 `LostMemory.Enemies`.

### 1.2 인스턴스 (Editor 작업 결과)

| 경로 | 종류 |
|---|---|
| `LostMemory/Assets/_Project/ScriptableObjects/Enemies/Bertha_Boss.asset` | BossData 인스턴스 (Bertha) |

일반 EnemyData asset 은 아직 없음. (Enemy 등장 ticket 에서 추가 예정)

### 1.3 Balance Editor 통합

- 좌측 트리에 `Bosses` / `Enemies` 카테고리 추가됨.
- Inspector 우측에서 Bertha_Boss 모든 필드 편집 가능, dirty 마커 / Ctrl+S / JSON Import-Export 동작.
- asset 추가/삭제/이동 자동 새로고침 (BalanceEditorAssetWatcher 가 `/Enemies/` 폴더 추적).

Editor Provider 파일 위치(참고용):
```
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Providers/
  EnemyDataCategoryProvider.cs
  BossDataCategoryProvider.cs
```

---

## 2. ★ 가장 중요한 주의사항 — 데이터-코드 비동기

**현재 상태에서는 Bertha_Boss.asset 의 값을 인스펙터에서 바꿔도 게임 동작은 변하지 않는다.**

이유:
- `BerthaBossPhaseController.cs` 는 **자체 SerializeField 값**(`phase2ThresholdNormalized = 0.7f`, `phase3ThresholdNormalized = 0.3f`)을 그대로 사용 중.
- BossData asset 을 참조하지 않음. CL-172/173 은 데이터 토대만 만들었고, 적용 코드(어댑터)는 의도적으로 분리.

**결과적으로 디자이너가 "Bertha 체력 100 → 200" 으로 SO 값 수정해도 게임 안 Bertha 는 그대로다.** 이걸 모르면 디버그가 길어진다. 어댑터(CL-178) 작업 전까지는 팀에 공유 필요.

---

## 3. 일반 몹 추가 시 절차 (코드 변경 없이 가능)

EnemyData asset 만 추가하면 Balance Editor 에 자동 노출됨. 별도 Provider 작성 / 등록 불필요.

1. Project: `Assets/_Project/ScriptableObjects/Enemies/` 선택.
2. 우클릭 → Create > LostMemory > Enemies > **Enemy Data**.
3. 파일명: `예: SmallSlime`.
4. Inspector 에 displayName / maxHealth / moveSpeed / expReward / dropWeight 입력.
5. Balance Editor 트리의 `Enemies` 카테고리에 자동 표시 확인.

> 단, **이 시점에도 게임 안 SmallSlime 의 동작은 SO 값과 무관**. EnemyCatalog 통합 + AI/스폰 코드가 EnemyData 를 참조하도록 어댑터(CL-178 또는 별도 ticket) 작업이 별개로 필요.

### Bertha_Boss 운영값 입력 미완 (참고)

본 핸드오프 시점에 Bertha_Boss.asset 의 다음 값은 **기본값 상태**:
- Display Name = (빈 값) — 운영값 `Bertha`
- Max Health = `100` — Bertha Prefab 의 `Health.MaximumHealth` 실제 값으로 교체 필요
- Move Speed = `5` — Bertha Prefab 의 `CharacterMovement.MovementSpeed` 실제 값으로 교체 필요
- Phase 3 Threshold = `0.7` (OnValidate 클램프 결과) — 운영값 `0.3`

→ CL-178 어댑터 작업 전 또는 작업 중 함께 입력 권장.

---

## 4. CL-178 어댑터 작업 진입점 (선택지 제시 — 결정은 어댑터 작업자)

여기서부터는 **결정 사항을 강제하지 않는다.** 어댑터 설계는 작업자 영역. 이 섹션은 활용 가능한 후크와 트레이드오프만 정리.

### 4.1 BerthaBossPhaseController.Configure() — 이미 준비된 진입점

위치: `LostMemory/Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/BerthaBossPhaseController.cs`

시그니처:
```csharp
public void Configure(
    Health configuredHealth,
    float configuredPhase2ThresholdNormalized,
    float configuredPhase3ThresholdNormalized,
    bool configuredDebugLogging)
```

→ BossData asset 을 어디선가 읽어서 `Configure()` 호출하면 된다. 호출자만 정하면 됨.

### 4.2 BossData asset 참조 패턴 — 선택지

| 방식 | 장점 | 단점 |
|---|---|---|
| (A) 호출자 컴포넌트에 `[SerializeField] BossData _bossData` 슬롯 + Inspector 드래그 | Unity 표준, 명시적 | 매 사용처마다 wiring 필요 |
| (B) `Resources.Load<BossData>("Enemies/Bertha_Boss")` (asset 을 Resources/ 로 이동 시) | 코드만으로 로드 | Resources 폴더 추가 + 빌드 사이즈 고려 |
| (C) BerthaBossEncounterController 가 BossData 슬롯을 갖고 자식 PhaseController 에 주입 | 책임 한 곳 모음 | 기존 EncounterController 시그니처 변경 |
| (D) EnemyCatalog 확장해서 `id → BossData` 매핑 추가 | catalog 일원화 | EnemyCatalog 가 본 트랙 외 영역, 변경 영향 점검 필요 |

→ 어댑터 작업자가 Bertha 외 다른 보스 / 일반 몹 확장 계획에 맞춰 결정.

### 4.3 라이브 튠 (선택) — `EnemyDataEvents.OnAssetSaved`

WeaponData 패턴과 동일하게, BossData asset 우클릭 → "Save Current Values" ContextMenu 실행 시 `EnemyDataEvents.OnAssetSaved` 가 발화한다 (현재 구독자 0).

라이브 튠을 원하면 Runtime 측에서 구독해서 Configure() 재호출:

```csharp
// 컴포넌트 OnEnable
EnemyDataEvents.OnAssetSaved += HandleBossDataSaved;
// OnDisable 에서 해제

void HandleBossDataSaved(EnemyData asset)
{
    if (asset is BossData boss && asset == _bossData)
    {
        _phaseController.Configure(_health,
            boss.Phase2ThresholdNormalized,
            boss.Phase3ThresholdNormalized,
            _debugLogging);
    }
}
```

> 본 hook 이 없어도 어댑터 자체는 동작 — 다만 Play 중 Inspector 값 변경이 즉시 반영 안 됨. 라이브 튠은 디자이너 워크플로 기능이라 우선순위는 어댑터 작업자가 판단.

### 4.4 expReward / dropWeight 필드

EnemyData 에 포함시켰으나 **현재 보상/드롭 시스템이 미존재**. 어댑터에서 무시해도 됨. 보상 시스템 ticket 시점에 연결.

---

## 5. 알려진 한계 / 컨벤션

### 5.1 Editor asmdef 정책 — `references = []` (변경 비추천)

`LostMemory.BalanceEditor.Editor.asmdef` 가 Runtime 어셈블리(`Assembly-CSharp`)를 직접 참조하지 않는 정책.

이유: Editor 빌드 그래프 단순화 + Runtime 변경 시 Editor 재컴파일 회피.

영향: Provider 들이 SO 타입을 클래스로 직접 참조하지 못함 → `t:EnemyData` / `t:BossData` 같은 **문자열 필터**로만 매칭.

EnemyDataCategoryProvider 의 `s.GetType().FullName?.EndsWith(".EnemyData") == true` 도 같은 이유 (BossData 가 `t:EnemyData` 에 같이 잡히는 걸 차단).

→ 어댑터 작업은 Runtime 영역이라 무관. **Provider 측에서 클래스 타입 직접 참조 추가가 필요해지면 정책 변경 검토 → 별도 ticket** (5개 Provider 전부 영향).

### 5.2 클래스명 변경 시 카테고리 깨짐

`EnemyData` 클래스명을 다른 이름으로 변경하면:
- EnemyDataCategoryProvider 의 `FullName.EndsWith(".EnemyData")` 매칭 false → Enemies 카테고리에서 사라짐.
- BossData 도 동일.

→ SO 클래스명 변경은 매우 드문 작업이지만, 변경 시 본 Provider 도 같이 갱신 필요.

### 5.3 SO 인스펙터 EffectType 입력 오류 패턴 (다른 트랙 사례)

CL-146 에서 디자이너가 SO 인스펙터 enum 드롭다운을 잘못 선택해 디버그 길어진 사례 있음. EnemyData / BossData 도 향후 enum 필드 추가 시 같은 위험.

→ enum 드롭다운 추가 시 자동 검증 도구 / SO 별 whitelist 도입 권장 (별도 polish ticket 으로 다뤄질 예정).

---

## 6. 검증 / 회귀 시 도움 되는 정보

### 본 핸드오프 직전 검증 통과 항목 (Balance Editor 트랙 완료)
- ✅ EnemyData / BossData 컴파일 OK
- ✅ Balance Editor 에 Enemies / Bosses 카테고리 표시
- ✅ Bertha_Boss leaf 가 Bosses 단일 카테고리에만 노출 (중복 없음)
- ✅ Inspector 7필드 편집 가능, dirty 마커 / 디스크 저장 동작
- ✅ JSON Export / Import 라운드트립 OK
- ✅ Watcher 자동 새로고침 (asset 추가/삭제/이동 시)

### 무수정 영역 (회귀 점검 시 살펴볼 필요 없음)
- `BerthaBossPhaseController.cs` (Configure() API 추가도 본 작업 이전부터 존재)
- `EnemyCatalog.cs`
- 기존 5개 Balance Editor Provider

---

## 7. 관련 문서

| 문서 | 내용 |
|---|---|
| [docs/khi/cl172_plan.md](khi/cl172_plan.md) | EnemyData / BossData SO 설계 결정 근거 |
| [docs/khi/cl172_implementation.md](khi/cl172_implementation.md) | CL-172 구현 기록 (사용자 결정 + 발견 이슈) |
| [docs/khi/cl173_plan.md](khi/cl173_plan.md) | CategoryProvider 등록 설계 |
| [docs/khi/cl173_implementation.md](khi/cl173_implementation.md) | CL-173 구현 기록 (FullName 필터 결정 / Watcher 함께 처리) |
| [docs/commonness/project-structure-and-namespace.md](commonness/project-structure-and-namespace.md) | 프로젝트 폴더 구조 / namespace 컨벤션 |

### 참고할 만한 기존 패턴
- WeaponData (`Runtime/Data/WeaponData.cs`) + WeaponDataEvents — EnemyData/EnemyDataEvents 와 동일 패턴, 라이브 튠 hook 사례
- WeaponDataPlayModeIsolator (Editor 측) — Play 모드 종료 시 자동 복원 시스템. EnemyDataPlayModeIsolator 가 필요할 시 동일 패턴 모방 가능

---

## 8. 질문 발생 시 연락

본 핸드오프 작성자: 김회인 (Balance Editor 트랙)

- **CL-172/173 결정 근거 / 변경 영향 범위 질문** → 이쪽으로
- **CL-178 어댑터 설계 결정** → Enemy 트랙 담당자 영역. Balance Editor 트랙은 결정 강제하지 않음. 다만 `EnemyDataEvents.OnAssetSaved` / `BossData` 클래스 변경이 필요하면 사전 협의 권장 (Balance Editor 측 Provider 영향 가능)

---

## 부록 — 빠른 체크리스트 (어댑터 작업 시작 전)

- [ ] 본 문서 §2 데이터-코드 비동기 항목 팀 공유
- [ ] Bertha_Boss.asset 의 운영값 입력 책임자 정하기 (§3)
- [ ] BossData 참조 보유 패턴 결정 (§4.2 (A)~(D) 중 선택)
- [ ] 라이브 튠 hook 도입 여부 결정 (§4.3)
- [ ] 일반 EnemyData asset 추가 ticket 과의 작업 순서 협의

# CL-139 — BuildManager 구현 (스택 카운트, 최고 단계 적용 규칙, dirty 마커)

## Context

Epic S Phase 1의 두 번째 ticket. **CL-138에서 정의한 데이터 구조를 사용하는 런타임 매니저** 구축.

CL-138 결과물:
- `RelicData` 가 듀얼 태그(`_tagPrimary`, `_tagSecondary`) 보유
- 16개 `BuildSetData` SO (각 세트의 티어별 효과 정의)
- `RelicEffectType` 31개 enum 값

**CL-139의 책임 (단일)**:
플레이어가 보유한 RelicData들의 태그를 **카운트**하고, 각 세트의 **현재 도달 티어**를 계산하고, **티어 변화 이벤트**를 발화. 실제 효과 적용은 CL-140 (EffectApplicator).

→ **본 CL은 "어느 세트가 몇 티어 도달했는지"를 알려주는 시스템**. 효과 적용 X.

```
┌─────────────────────────────────────────────────────┐
│ CL-138 (data) → CL-139 (count/tier) → CL-140 (apply) │
│ ─────────────                                        │
│  RelicData                                           │
│  BuildSetData         BuildManager                   │
│      ↓                  ↓ 계산                       │
│  태그 정의            태그 카운트                    │
│  티어 임계치          현재 티어 결정                 │
│  효과 정의            티어 변화 이벤트 발화          │
└─────────────────────────────────────────────────────┘
                                ↓
                    EffectApplicator (CL-140)
                    이벤트 받아서 효과 적용
```

---

## 결정사항

### 1. 매니저 위치 — `Player` GameObject에 부착

`PlayerRelicInventory`와 같은 GameObject에 컴포넌트로 부착. 기존 `RelicEffectRegistry` 패턴 준수.

```
[Player GameObject]
  ├─ PlayerRelicInventory (CL-110)
  ├─ RelicEffectRegistry (CL-107~109) — 개별 유물 효과
  ├─ BuildManager (CL-139, 신규) — 세트 효과 카운트 ⭐
  └─ EffectApplicator (CL-140, 신규) — 세트 효과 적용
```

### 2. 카운트 알고리즘 — 듀얼 태그 합산

각 RelicData가 2개 태그 카운트에 +1.

```csharp
// 의사코드
Dictionary<RelicTag, int> tagCounts = new();

foreach (var relic in inventory.OwnedRelics)
{
    if (relic.TagPrimary != RelicTag.None)
        tagCounts[relic.TagPrimary] += 1;
    if (relic.TagSecondary != RelicTag.None)
        tagCounts[relic.TagSecondary] += 1;
}
```

→ 태그가 None이면 카운트 안 함 (소모품·예외 케이스).

**중복 처리 정책**:
- `_tagPrimary == _tagSecondary` 인 경우 (예: 둘 다 AttackPower) → +2 카운트
- 의도된 디자인: 단일 강한 빌드용 아이템 가능 (현재 75개 풀에는 없지만 향후)

### 3. 티어 결정 — 최고 단계만 적용

회의록 결정대로. 카운트가 도달한 가장 높은 티어 1개만 활성화.

예시 (행운 빌드):
```
임계치: 1 → 3 → 5 → 7
카운트 5인 경우 → 활성 티어 = 3 (인덱스 2). 1티어·2티어 효과는 미적용.
```

```csharp
// 의사코드
public int GetActiveTier(BuildSetData set, int count)
{
    int activeIdx = -1;
    for (int i = 0; i < set.Tiers.Count; i++)
    {
        if (count >= set.Tiers[i].RequiredCount)
            activeIdx = i;
    }
    return activeIdx;  // -1 = 미발동
}
```

### 4. 빈 스택 처리

회의록 결정: **빈 스택 = 효과 없음** (다음 단계 도달해야 발동).

위 알고리즘으로 자연스럽게 해결 — 카운트가 다음 임계치 미만이면 활성 티어 변동 없음.

예: 행운 카운트 2개 → 활성 티어 = 0 (임계치 1만 충족), 다음 카운트 3 도달 시 활성 티어 1로 상승.

### 5. 이벤트 발화 — 티어 변화 시점

```csharp
public event Action<RelicTag, int oldTier, int newTier> OnSetTierChanged;
```

발화 조건:
- 아이템 획득/제거로 카운트 변화 → 활성 티어 재계산 → 변화 있으면 이벤트 발화
- 티어 변화 없으면 이벤트 안 발화 (불필요한 효과 적용 방지)

EffectApplicator (CL-140)가 이 이벤트 구독해서:
- 새 티어 → 이전 티어 효과 제거 + 새 티어 효과 적용

### 6. 인벤토리 변화 감지 — 기존 이벤트 활용

`PlayerRelicInventory`의 기존 이벤트 구독:

```csharp
inventory.OnRelicAcquired += HandleInventoryChanged;
// PlayerRelicInventory.Remove() 시점도 필요 → 이벤트 추가 필요
inventory.OnRelicRemoved += HandleInventoryChanged;  // 신규 추가
inventory.OnCleared += HandleInventoryCleared;
```

**기존 인벤토리 수정 필요**: `OnRelicRemoved` 이벤트가 현재 없음. CL-139 작업 시 PlayerRelicInventory.Remove()에 이벤트 발화 추가 (1줄).

### 7. Dirty 마커 — 매 변화마다 즉시 갱신 vs 다음 프레임

**옵션 A: 즉시 갱신** (변화 발생 즉시 카운트·티어 계산)
- 장점: 단순, 동기적
- 단점: 한 프레임에 여러 변화 시 중복 계산

**옵션 B: Dirty 마커 + LateUpdate에서 한 번만**
- 장점: 한 프레임 내 N번 변화도 1번만 계산
- 단점: 약간 복잡

**채택: 옵션 B (Dirty 마커)**. 보상 다중 선택 같은 시나리오 대비.

```csharp
private bool _isDirty = false;

private void HandleInventoryChanged(...) { _isDirty = true; }
private void LateUpdate()
{
    if (!_isDirty) return;
    _isDirty = false;
    RecalculateAllTiers();
}
```

### 8. 인증 게이트 — `IRelicEffectAuthority` 재활용

기존 `RelicEffectRegistry`처럼 host/client 분리 패턴 재활용.

```csharp
private readonly IRelicEffectAuthority _authority = new NetworkRelicEffectAuthority();

private void HandleInventoryChanged(...)
{
    if (!_authority.IsAuthority) return;
    _isDirty = true;
}
```

### 9. 공개 API

```csharp
public class BuildManager : MonoBehaviour
{
    // 조회
    public int GetTagCount(RelicTag tag);
    public int GetActiveTier(RelicTag tag);   // -1 = 미발동
    public SetTier? GetActiveTierData(RelicTag tag);   // null 가능
    public IReadOnlyDictionary<RelicTag, int> AllCounts { get; }
    public IReadOnlyDictionary<RelicTag, int> AllActiveTiers { get; }

    // 이벤트
    public event Action<RelicTag, int oldTier, int newTier> OnSetTierChanged;

    // 디버그 (Inspector)
    [SerializeField] private bool logTierChanges = false;
}
```

---

## 핵심 파일

### 신규

| 경로 | 내용 | 점수 영향 |
|---|---|---|
| `Assets/_Project/Scripts/Runtime/Relics/BuildManager.cs` | 본 ticket의 메인 컴포넌트 | 메인 작업 |

### 수정

| 경로 | 변경 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs` | `OnRelicRemoved` 이벤트 추가 (1줄), `Remove()`에서 발화 |

### 참조 (수정 X)

| 경로 | 사용 목적 |
|---|---|
| `RelicData.cs` | TagPrimary/TagSecondary 조회 (CL-138 결과) |
| `BuildSetData.cs` | 16개 SO + 티어 임계치 (CL-138 결과) |
| `IRelicEffectAuthority.cs` | host gating |

---

## 구현 단계

### 1단계: PlayerRelicInventory에 OnRelicRemoved 이벤트 추가 (5분)

```csharp
public event Action<RelicData> OnRelicRemoved;

public bool Remove(RelicData relic)
{
    if (relic == null) return false;
    int idx = _ownedRelics.FindIndex(r => r != null && r.name == relic.name);
    if (idx < 0) return false;
    _ownedRelics[idx] = null;
    OnRelicRemoved?.Invoke(relic);   // ← 추가
    Debug.Log($"[PlayerRelicInventory] 유물 버림: {relic.DisplayName}");
    return true;
}
```

### 2단계: BuildManager 골격 작성 (30분)

- 클래스 선언, SerializeField (inventory 참조, 16 BuildSetData 참조)
- 카운트·티어 dictionary 선언
- OnEnable / OnDisable에서 inventory 이벤트 구독/해제

### 3단계: 카운트 + 티어 계산 로직 (30분)

- `RecalculateAllTiers()` 구현
- 듀얼 태그 합산
- 각 BuildSetData에 대해 GetActiveTier 계산
- 변화 있는 세트만 OnSetTierChanged 발화

### 4단계: Dirty 마커 + LateUpdate (15분)

- `_isDirty` flag
- 인벤토리 이벤트 핸들러는 flag만 set
- LateUpdate에서 dirty면 recalculate

### 5단계: 디버그 로그 + Inspector 검증 (15분)

- `logTierChanges` flag
- 로그 형식: `[BuildManager] AttackPower: tier 0 → 1 (count=3)`
- Inspector 우상단 ⋮ → "Debug — Print all counts/tiers"

### 6단계: 단위 테스트 (선택, 30분)

만약 EditMode 테스트 환경이 있으면:
- 인벤토리에 RelicData 3개 추가 → 태그 카운트 검증
- 티어 임계치 통과 시 OnSetTierChanged 발화 검증
- 아이템 제거 시 티어 하락 검증

---

## 검증 방법 (e2e)

```
1. Unity Editor에서 Play 모드 진입
2. Player GameObject Inspector 열기 → BuildManager 컴포넌트 확인
3. PlayerRelicInventory 컴포넌트의 Debug — Add all assigned relics 사용 (CL-107)
   - 18개 SO 중 적당히 6~8개 등록
4. Console 로그 확인:
   - [BuildManager] AttackPower: tier 0 → 1 (count=3)
   - [BuildManager] Health: tier 0 → 0 (count=2, 임계치 미달)
5. BuildManager Inspector → "Debug — Print all counts/tiers" 우클릭 메뉴
   - 16개 세트 각각의 카운트·활성 티어 출력
6. 인벤토리에서 1개 제거 → 카운트 변화 → 티어 변화 시 로그 발화 확인
7. Run 종료 (PlayerRelicInventory.Clear) → 모든 카운트 0 → 모든 티어 -1로 리셋
```

**효과 적용은 본 CL 범위 외**. 단지 "이벤트가 정확히 발화되는지"만 검증.

---

## 위험 / 결정 미정

### 위험
1. **PlayerRelicInventory.OnRelicRemoved 추가가 기존 코드 영향**: 기존엔 이벤트 없었으니 Remove() 호출처들 영향 없음 (단지 새 이벤트만 추가). 단 다른 시스템이 이벤트 구독하기 시작하면 영향 (CL-139 외엔 없을 듯).
2. **16개 BuildSetData를 SerializeField로 보유 vs Resources.LoadAll 자동 로드**:
   - SerializeField: Inspector에서 명시적 할당, 누락 검출 쉬움. 16개 드래그 부담.
   - Resources.LoadAll: 자동 로드, 16개 누락 가능성, 폴더 구조 의존
   - **채택: SerializeField + 자동 검증** (Awake에서 16개 다 할당됐는지 체크)
3. **태그 enum 변경 시 영향**: 향후 RelicTag에 새 값 추가하면 카운트 dictionary에서 누락 안 됨 (런타임 dictionary는 동적). 단 BuildSetData가 새 태그용으로 필요.

### 결정 미정 (CL-139 진행 시)
- [ ] 한 RelicData가 같은 태그 두 번 가질 때 (TagPrimary == TagSecondary) +2 카운트인지 +1인지 — 현재 +2로 결정
- [ ] BuildManager Inspector에 16개 BuildSetData 드래그 vs Resources.LoadAll — 현재 SerializeField로 결정
- [ ] OnSetTierChanged의 oldTier·newTier 의미 — `-1`이 미발동, `0+`이 활성 티어 인덱스 (회의록의 1/3/5/7 같은 raw 카운트가 아님). 이 점 명확히 문서화

---

## 후속 ticket 영향

| Ticket | CL-139와의 관계 |
|---|---|
| **CL-140 (EffectApplicator)** | OnSetTierChanged 구독 → 이전 티어 효과 제거 + 새 티어 효과 적용. 본 CL의 이벤트가 핵심 인터페이스 |
| **CL-141 (75 ItemData)** | 본 CL과 무관 (데이터만) |
| **CL-142~CL-147 (각 효과)** | EffectApplicator를 통해 본 CL의 이벤트와 간접 연결 |
| **CL-148 (인벤토리 UI)** | 본 CL의 `AllCounts`, `AllActiveTiers` 조회해서 빌드 진척 표시 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (OnRelicRemoved 추가) | 5분 |
| 2단계 (BuildManager 골격) | 30분 |
| 3단계 (카운트/티어 로직) | 30분 |
| 4단계 (Dirty 마커) | 15분 |
| 5단계 (디버그) | 15분 |
| 6단계 (단위 테스트, 선택) | 30분 |
| 검증 | 30분 |
| **합계** | **2~2.5시간** (티켓 점수 3점에 부합) |

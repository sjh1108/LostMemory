# CL-172 — EnemyData / BossData SO 정의 + Bertha 인스턴스

## Context

Enemy 트랙 첫 ticket. `EnemyCatalog.cs` 주석(CL-034)에 이미 "후속에 EnemyData SO 도입 시 prefab 자리를 EnemyData 로 교체" 예약돼 있음.

**3점 P1, CL-166 의존.**

### 본 CL 책임 범위 (시나리오 B — 코드 전환 X)

| 포함 | 제외 |
|---|---|
| `EnemyData.cs` SO 클래스 신설 | 기존 Bertha 컨트롤러 코드 수정 |
| `BossData.cs` SO 클래스 신설 (EnemyData 상속) | EnemyCatalog.cs 변경 |
| `Bertha_Boss.asset` 인스턴스 생성 | 적용 어댑터 (→ CL-180 보류) |
| Balance Editor Provider 등록 (→ CL-173) | Player 트랙 (→ CL-188~190 보류) |

---

## 현황

### 1.1 EnemyData 관련 기존 코드

```
Runtime/Combat/EnemyCatalog.cs (CL-034)
  - EnemyCatalogEntry: string id → GameObject prefab 매핑
  - 주석: "EnemyData SO 도입 시 prefab 자리를 EnemyData로 교체 예정"
  - 현재 인스턴스: ScriptableObjects/Enemies/EnemyCatalog_Default.asset

Runtime/Enemies/Boss/Bertha/BerthaBossPhaseController.cs (CL-034~)
  - 필드: phase2ThresholdNormalized = 0.7f, phase3ThresholdNormalized = 0.3f
  - Health 컴포넌트 직접 참조 (SO 참조 없음)
  - Configure() API 존재 → 후속 어댑터(CL-180) 진입점
```

### 1.2 EnemyData.cs 미존재 확인

```
grep -r "class EnemyData" Assets/_Project/Scripts/ → No matches
```

→ 완전 신설.

### 1.3 SO 폴더 현황

```
Assets/_Project/ScriptableObjects/Enemies/
  EnemyCatalog_Default.asset   ← 기존 (본 CL 무수정)
```

→ 신규 인스턴스는 같은 폴더에 추가.

---

## 설계 결정

### 1. BossData : EnemyData 상속

```
EnemyData : ScriptableObject
  공통 스탯 (displayName, maxHealth, moveSpeed, expReward, dropWeight)

BossData : EnemyData
  + 보스 전용 (phase2ThresholdNormalized, phase3ThresholdNormalized)
```

**근거:**
- Bertha 도 기본 스탯(체력/이속) + 보상 데이터가 필요함
- Balance Editor 에서 Enemy / Boss 두 카테고리 분리 표시 (Provider 2개, CL-173)
- 독립 클래스면 Bertha 에서 기본 스탯 필드 중복

### 2. EnemyData 필드 범위

| 필드 | 타입 | 포함 | 이유 |
|---|---|---|---|
| `_displayName` | string | ✅ | Balance Editor 표시명 |
| `_maxHealth` | float | ✅ | 기본 체력 |
| `_moveSpeed` | float | ✅ | 기본 이속 |
| `_expReward` | int | ✅ | 보상 시스템 데이터 준비 |
| `_dropWeight` | float | ✅ | 드롭 가중치 데이터 준비 |
| `attackDamage` | — | ❌ | Bertha 는 공격 타입 6종 이상, 단일 값이 어색함. 일반 몹 등장 시점에 재설계 |
| `detectionRange` / `patrolSpeed` | — | ❌ | AI 트랙 보류 결정 |

> **expReward / dropWeight 주의**: 보상·드롭 시스템이 현재 없음. Balance Editor 에서 값을 넣어도 게임에 반영되지 않는 기간 발생. 팀 공유 필요.

### 3. namespace / 파일 위치

| 파일 | 경로 | namespace |
|---|---|---|
| `EnemyData.cs` | `Runtime/Enemies/EnemyData.cs` | `LostMemory.Enemies` |
| `BossData.cs` | `Runtime/Enemies/BossData.cs` | `LostMemory.Enemies` |

→ WeaponData 는 `LostMemory.Data` 이지만, Enemy는 이미 `LostMemory.Enemies` 네임스페이스로 통일돼 있어 맞춤.

---

## 신규 파일 — 코드

### EnemyData.cs

```csharp
using UnityEngine;

namespace LostMemory.Enemies
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "LostMemory/Enemy Data", order = 10)]
    public class EnemyData : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField, Min(0f)] private float _maxHealth = 100f;
        [SerializeField, Min(0f)] private float _moveSpeed = 5f;
        [SerializeField, Min(0)] private int _expReward;
        [SerializeField, Range(0f, 1f)] private float _dropWeight = 1f;

        public string DisplayName => _displayName;
        public float MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public int ExpReward => _expReward;
        public float DropWeight => _dropWeight;
    }
}
```

### BossData.cs

```csharp
using UnityEngine;

namespace LostMemory.Enemies
{
    [CreateAssetMenu(fileName = "BossData", menuName = "LostMemory/Boss Data", order = 11)]
    public class BossData : EnemyData
    {
        [SerializeField, Range(0f, 1f)] private float _phase2ThresholdNormalized = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _phase3ThresholdNormalized = 0.3f;

        public float Phase2ThresholdNormalized => _phase2ThresholdNormalized;
        public float Phase3ThresholdNormalized => _phase3ThresholdNormalized;

        private void OnValidate()
        {
            _phase2ThresholdNormalized = Mathf.Clamp01(_phase2ThresholdNormalized);
            _phase3ThresholdNormalized = Mathf.Clamp01(_phase3ThresholdNormalized);
            // phase3 는 항상 phase2 이하
            _phase3ThresholdNormalized = Mathf.Min(_phase3ThresholdNormalized, _phase2ThresholdNormalized);
        }
    }
}
```

---

## 인스턴스 생성 가이드 (Unity Editor 작업 — 사용자)

Claude 는 .asset 파일을 직접 생성하지 않음. Unity Editor 에서 진행.

### Bertha_Boss.asset (BossData)

1. Project 창에서 `Assets/_Project/ScriptableObjects/Enemies/` 폴더 선택
2. 우클릭 → Create > LostMemory > **Boss Data**
3. 파일명: `Bertha_Boss`
4. Inspector 에서 값 입력:

| 필드 | 값 | 출처 |
|---|---|---|
| Display Name | `Bertha` | — |
| Max Health | `BerthaBossPhaseController` Prefab 의 TDE Health.MaximumHealth | Health 컴포넌트 Inspector |
| Move Speed | Bertha Prefab 의 TDE CharacterMovement.MovementSpeed | CharacterMovement Inspector |
| Exp Reward | (디자이너 결정) | — |
| Drop Weight | (디자이너 결정, 기본 1) | — |
| Phase 2 Threshold | `0.7` | BerthaBossPhaseController 기존 값 |
| Phase 3 Threshold | `0.3` | BerthaBossPhaseController 기존 값 |

> BerthaBossPhaseController 코드 변경 없음. asset 값과 Inspector 값이 동기화되지 않아도 CL-172 범위에서는 무방. 실제 연결은 CL-180.

### EnemyData 일반 인스턴스 (선택)

일반 몹이 아직 없으므로 **CL-172 에서 생성 불필요**. Balance Editor 의 Enemies 카테고리는 CL-173 이후 SO 추가 시점에 자동 채워짐.

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **데이터-코드 비동기**: Bertha_Boss.asset 값이 게임에 미반영 | CL-180 적용 어댑터 때 연결. 팀 공유 필요 |
| 2 | **expReward/dropWeight 시스템 미존재**: 값 넣어도 게임 효과 없음 | 보상 시스템 ticket 때 연결 |
| 3 | **MaxHealth / MoveSpeed 값 파악 필요**: Bertha Prefab Inspector 직접 확인 필요 | 인스턴스 생성 가이드 §참조 |
| 4 | **BossData.OnValidate 실행 시점**: phase3 > phase2 입력 시 자동 보정 (BerthaBossPhaseController 동일 로직) | 의도된 동작 |
| 5 | **일반 EnemyData asset 없음**: CL-173 이후 Balance Editor Enemy 카테고리 비어있음 | 일반 몹 등장 ticket 에 asset 작성 위임 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. EnemyData.cs 컴파일 오류 없음
2. BossData.cs 컴파일 오류 없음
3. 기존 BerthaBossPhaseController.cs 변경 없음 확인
4. EnemyCatalog.cs 변경 없음 확인
```

### Unity Editor 검증 (사용자)

```
5. Create > LostMemory > Enemy Data 메뉴 표시
6. Create > LostMemory > Boss Data 메뉴 표시
7. Bertha_Boss.asset 생성 → Inspector 에서 모든 필드 편집 가능
8. Phase 3 임계값 > Phase 2 임계값 입력 시 OnValidate 로 자동 보정
9. EnemyCatalog_Default.asset 영향 없음 (정상 동작 유지)
10. Balance Editor 에서 Enemy / Boss 카테고리 미표시 (CL-173 이후 표시됨)
```

---

## 핵심 파일

### 신규 (Claude 작성)

| 경로 | 내용 |
|---|---|
| `Runtime/Enemies/EnemyData.cs` | 일반 몹 기본 스탯 SO |
| `Runtime/Enemies/BossData.cs` | 보스 전용 SO (EnemyData 상속) |

### 신규 (사용자 Unity Editor 작업)

| 경로 | 내용 |
|---|---|
| `ScriptableObjects/Enemies/Bertha_Boss.asset` | Bertha BossData 인스턴스 |

### 무수정 (참고)

| 파일 | 이유 |
|---|---|
| `BerthaBossPhaseController.cs` | 코드 전환 X (CL-180 보류) |
| `EnemyCatalog.cs` | 본 CL 범위 외 |

---

## 후속 ticket 영향

| Ticket | 관계 |
|---|---|
| **CL-173** EnemyData/BossData Provider 등록 | 본 CL 완료 후 즉시 진행. CL-172 산출물(클래스)이 `t:EnemyData` / `t:BossData` 필터의 진입점 |
| **CL-180** Enemy 적용 어댑터 | 보류. Bertha 리팩 또는 일반 몹 등장 시점에 매칭 |
| **일반 몹 등장 ticket** | EnemyData.cs 클래스를 그대로 사용. 인스턴스만 추가 |
| **보상/드롭 시스템 ticket** | expReward / dropWeight 필드 연결 |

---

## 작업 순서

| 순서 | 담당 | 내용 |
|---|---|---|
| 1 | Claude | `EnemyData.cs` / `BossData.cs` 작성 |
| 2 | Claude | 컴파일 오류 없음 확인 (Grep / Read) |
| 3 | 사용자 | Unity Editor 에서 Bertha_Boss.asset 생성 + 값 입력 |
| 4 | 사용자 | OnValidate 보정 동작 확인 |
| 5 | — | CL-173 으로 이동 (Provider 등록) |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| CS 파일 2개 작성 | 15분 |
| Bertha_Boss.asset 생성 (사용자) | 10분 |
| 검증 | 15분 |
| **합계** | **약 40분** |

→ 3점 ticket 중 코드 작업 자체는 가볍고, 인스턴스 입력 시 Bertha Prefab 값 파악이 실질 소요 시간.

# CL-173 — EnemyData / BossData CategoryProvider 등록

## Context

CL-172 에서 신설한 `EnemyData` / `BossData` SO 클래스를 Balance Editor 좌측 트리에 노출하는 ticket. **Provider 클래스 2개 작성 + `BalanceEditorWindow._providers` 리스트에 2줄 추가** = 거의 그게 전부.

**1점 P1, CL-172 의존.**

cl172_plan.md 의 후속 ticket 표 (line 246) 에서 이미 예약: "본 CL 완료 후 즉시 진행. CL-172 산출물(클래스)이 `t:EnemyData` / `t:BossData` 필터의 진입점."

---

## 현황

### 1.1 Balance Editor Provider 패턴 (CL-163 / CL-166 확정)

```
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/
├─ IBalanceCategoryProvider.cs           (인터페이스, ns: LostMemory.Editor.BalanceEditor)
├─ BalanceEditorWindow.cs                (정적 등록, line 125-132)
├─ LostMemory.BalanceEditor.Editor.asmdef (references = [])
└─ Providers/
    ├─ RelicCategoryProvider.cs           ★ SearchFolders 사용
    ├─ BuildSetCategoryProvider.cs        ★ SearchFolders 사용
    ├─ WeaponCategoryProvider.cs          (전체 검색)
    ├─ SkillCategoryProvider.cs           (전체 검색)
    └─ ShopConfigCategoryProvider.cs      (전체 검색)
```

### 1.2 인터페이스 시그니처

```csharp
public interface IBalanceCategoryProvider
{
    string CategoryName { get; }
    string AssetTypeFilter { get; }   // "t:" 필터 — Editor asmdef 가 SO 타입 직접 참조 회피
    IEnumerable<ScriptableObject> LoadAll();
}
```

### 1.3 표준 Provider 형태 (RelicCategoryProvider 모방)

```csharp
namespace LostMemory.Editor.BalanceEditor.Providers
{
    public class RelicCategoryProvider : IBalanceCategoryProvider
    {
        private static readonly string[] SearchFolders =
        {
            "Assets/_Project/ScriptableObjects/Relics/Generated"
        };
        public string CategoryName => "Relics";
        public string AssetTypeFilter => "RelicData";
        public IEnumerable<ScriptableObject> LoadAll()
        {
            var guids = AssetDatabase.FindAssets($"t:{AssetTypeFilter}", SearchFolders);
            return guids.Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                          AssetDatabase.GUIDToAssetPath(g)))
                        .Where(s => s != null);
        }
    }
}
```

### 1.4 등록 위치 — `BalanceEditorWindow.cs:125-132`

```csharp
_providers = new List<IBalanceCategoryProvider>
{
    new RelicCategoryProvider(),
    new BuildSetCategoryProvider(),
    new WeaponCategoryProvider(),
    new SkillCategoryProvider(),
    new ShopConfigCategoryProvider(),
};
```

> 카테고리 표시 순서는 `BuildTreeData()` 의 `_providers.OrderBy(p => p.CategoryName)` (line 475) 로 자동 정렬. 리스트 내 순서는 시각 영향 없음.

### 1.5 CL-172 산출물

| 파일 | 클래스 | namespace |
|---|---|---|
| `Runtime/Enemies/EnemyData.cs` | `EnemyData : ScriptableObject` | `LostMemory.Enemies` |
| `Runtime/Enemies/BossData.cs` | `BossData : EnemyData` | `LostMemory.Enemies` |
| `ScriptableObjects/Enemies/Bertha_Boss.asset` | (사용자 작성 BossData 인스턴스) | — |

### 1.6 본 CL 에서 손대는 코드의 작성자 — 다른 사람 코드 침범 0

| 파일 | 작성자 / 작업 |
|---|---|
| `EnemyDataCategoryProvider.cs` | (신규) |
| `BossDataCategoryProvider.cs` | (신규) |
| `BalanceEditorWindow.cs` | **김회인 본인** (CL-162~166 의 5개 커밋 전부 본인 작성). 2줄 추가 |

> `git log --pretty=format:"%h %an" -- BalanceEditorWindow.cs` 결과: 17d8e252b / 82ba8ba06 / 66762cb34 / fe1251fea / d7d81689c — 모두 김회인.

---

## 설계 결정

### 1. CategoryName 컨벤션

| Provider | CategoryName |
|---|---|
| 기존 5개 | `Weapons` / `Skills` / `Relics` / `BuildSets` / `ShopConfig` (영어, 대부분 복수형) |
| **신규** | **`Enemies` / `Bosses`** (복수형 영어로 일관성) |

### 2. ★ EnemyData ↔ BossData 중복 매칭 처리

**문제**: `BossData : EnemyData` 상속 구조. Unity `AssetDatabase.FindAssets("t:EnemyData", ...)` 는 **BossData 인스턴스도 같이 반환**. 그대로 두면 `Bertha_Boss.asset` 이 Enemies / Bosses 두 카테고리에 동시 표시됨.

**해결안 비교**:

| 안 | 방법 | 장점 | 단점 |
|---|---|---|---|
| (a) `is BossData` 캐스팅 필터 | `Where(s => !(s is BossData))` | 명확 | **Editor asmdef 가 BossData 클래스 직접 참조 필요 → asmdef references=[] 정책 위배** |
| (b) `GetType().Name` 문자열 매칭 | `Where(s => s.GetType().Name == "EnemyData")` | asmdef 영향 없음, 한 줄 | string 비교 |
| (c) 폴더 분리 (Bosses 전용 폴더) | SearchFolders 차이로 분리 | 폴더가 카테고리와 1:1 | 폴더 정책 변경, 디자이너 헷갈림 |

→ **(b) 채택**. 한 줄, asmdef 정책 그대로, 명확.

```csharp
// EnemyDataCategoryProvider.LoadAll() 내부
.Where(s => s != null)
.Where(s => s.GetType().Name == "EnemyData");  // BossData 제외
```

> BossDataCategoryProvider 는 필터 불필요. `t:BossData` 는 BossData 만 매칭 (상위 클래스 EnemyData 인스턴스는 제외됨).

### 3. SearchFolders 정책

cl172_plan.md §1.3 — 인스턴스 폴더는 `Assets/_Project/ScriptableObjects/Enemies/`. 동일 폴더에 EnemyCatalog_Default.asset (별도 `EnemyCatalog` 클래스) 존재하지만 `t:EnemyData` / `t:BossData` 매칭에 안 잡힘.

| 안 | 결정 |
|---|---|
| 전체 검색 (Weapon/Skill 패턴) | ❌ |
| **SearchFolders 명시** (Relic/BuildSet 패턴) | ✅ |

→ **SearchFolders 명시**. 실수로 다른 폴더에 같은 타입 SO 가 만들어져도 트리에 오염 안 됨. 인스턴스 위치 정책이 분명한 도메인은 명시하는 게 cl163 의 Relic/BuildSet 결정과 일관.

```csharp
private static readonly string[] SearchFolders =
{
    "Assets/_Project/ScriptableObjects/Enemies"
};
```

### 4. Provider 등록 순서

`_providers` 리스트 내 순서는 트리 시각에 영향 없음 (BuildTreeData 가 알파벳 정렬). 가독성을 위해 **마지막에 2줄 추가**.

### 5. namespace / 파일 위치

| 파일 | 경로 | namespace |
|---|---|---|
| `EnemyDataCategoryProvider.cs` | `LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Providers/` | `LostMemory.Editor.BalanceEditor.Providers` |
| `BossDataCategoryProvider.cs` | (동일 폴더) | (동일 ns) |

→ 기존 5개 Provider 와 동일 위치 / 동일 ns.

---

## 신규 파일 — 코드

### EnemyDataCategoryProvider.cs

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor.Providers
{
    public class EnemyDataCategoryProvider : IBalanceCategoryProvider
    {
        private static readonly string[] SearchFolders =
        {
            "Assets/_Project/ScriptableObjects/Enemies"
        };

        public string CategoryName => "Enemies";
        public string AssetTypeFilter => "EnemyData";

        public IEnumerable<ScriptableObject> LoadAll()
        {
            var guids = AssetDatabase.FindAssets($"t:{AssetTypeFilter}", SearchFolders);
            return guids
                .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                // BossData : EnemyData 상속이라 t:EnemyData 에 BossData 도 매칭됨.
                // Bosses 카테고리와 중복 표시 방지를 위해 정확 타입만 통과.
                .Where(s => s.GetType().Name == "EnemyData");
        }
    }
}
```

### BossDataCategoryProvider.cs

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor.Providers
{
    public class BossDataCategoryProvider : IBalanceCategoryProvider
    {
        private static readonly string[] SearchFolders =
        {
            "Assets/_Project/ScriptableObjects/Enemies"
        };

        public string CategoryName => "Bosses";
        public string AssetTypeFilter => "BossData";

        public IEnumerable<ScriptableObject> LoadAll()
        {
            var guids = AssetDatabase.FindAssets($"t:{AssetTypeFilter}", SearchFolders);
            return guids
                .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null);
        }
    }
}
```

---

## 수정 파일 — 코드

### `BalanceEditorWindow.cs:125-132`

기존:
```csharp
_providers = new List<IBalanceCategoryProvider>
{
    new RelicCategoryProvider(),
    new BuildSetCategoryProvider(),
    new WeaponCategoryProvider(),
    new SkillCategoryProvider(),
    new ShopConfigCategoryProvider(),
};
```

수정 후 (2줄 추가):
```csharp
_providers = new List<IBalanceCategoryProvider>
{
    new RelicCategoryProvider(),
    new BuildSetCategoryProvider(),
    new WeaponCategoryProvider(),
    new SkillCategoryProvider(),
    new ShopConfigCategoryProvider(),
    new EnemyDataCategoryProvider(),
    new BossDataCategoryProvider(),
};
```

> `using LostMemory.Editor.BalanceEditor.Providers;` 는 line 4 에 이미 존재 → 추가 import 불필요.

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **EnemyData ↔ BossData 중복 매칭** (Bertha_Boss 가 Enemies / Bosses 양쪽에 표시) | EnemyDataCategoryProvider 의 `GetType().Name == "EnemyData"` 필터로 차단 (§설계 2) |
| 2 | **Enemies 카테고리가 빈 트리** (CL-172 시점에 일반 EnemyData asset 없음) | 정상. 일반 몹 등장 ticket 에서 asset 추가 시 자동 채워짐. 카테고리 라벨에 "(0)" 표시됨 |
| 3 | **클래스명 변경 시 필터 깨짐** (`EnemyData` 이름 바꾸면 GetType().Name 비교 실패) | nameof(EnemyData) 가 더 안전하지만 asmdef 정책상 직접 참조 X. 향후 SO 이름 변경은 매우 드문 작업, 그때 본 Provider 도 같이 갱신 |
| 4 | **JSON Export/Import 라운드트립** — Enemy/Boss 카테고리도 자동 포함됨 (BalanceEditorWindow 가 _providers 리스트로 순회) | CL-165 동작 그대로. Bertha_Boss.json 등 자동 생성·복원 |
| 5 | **AssetPostprocessor 자동 새로고침** (CL-164) | BalanceEditorAssetWatcher 가 SO 변경 감지 → 카테고리 트리 갱신. 신규 Enemy/Boss asset 추가/삭제 시 자동 반영 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. EnemyDataCategoryProvider.cs / BossDataCategoryProvider.cs 컴파일 오류 없음
2. BalanceEditorWindow.cs 수정 후 컴파일 오류 없음
3. asmdef references = [] 유지 (수정 X)
```

### Unity Editor 검증 (사용자)

```
4. Tools > LostMemory > Balance Editor 열기
5. 좌측 트리에 "Bosses (1)" 카테고리 표시 — Bertha_Boss leaf 노드 1개
6. 좌측 트리에 "Enemies (0)" 카테고리 표시 — leaf 없음 (정상)
7. Bertha_Boss 선택 → 우측 InspectorElement 에 BossData 필드 표시
   (DisplayName / MaxHealth / MoveSpeed / ExpReward / DropWeight
    / Phase2ThresholdNormalized / Phase3ThresholdNormalized)
8. 필드 변경 → 트리 라벨에 "*" dirty 마커 표시
9. Ctrl+S → 디스크 저장, dirty 마커 제거
10. 검색창에 "Bertha" 입력 → Bosses 카테고리만 펼쳐져 표시
11. JSON Export > Selected SO → Bertha_Boss.json 생성 확인
12. 파일 외부 수정 후 JSON Import > To Selected SO → 값 복원 확인
13. ★ Bertha_Boss 가 Enemies 카테고리에 중복 표시 안 됨 (중요)
```

---

## 핵심 파일

### 신규 (Claude 작성)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Providers/EnemyDataCategoryProvider.cs` | Enemies 카테고리 (BossData 제외 필터 포함) |
| `LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Providers/BossDataCategoryProvider.cs` | Bosses 카테고리 |

### 수정 (Claude 작성)

| 경로 | 변경 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs:125-132` | `_providers` 리스트에 2줄 추가 |

### 무수정 (참고)

| 파일 | 이유 |
|---|---|
| `IBalanceCategoryProvider.cs` | 인터페이스 변경 없음 |
| `LostMemory.BalanceEditor.Editor.asmdef` | references = [] 정책 유지 |
| 기존 5개 Provider | 본 CL 범위 외 |
| `EnemyData.cs` / `BossData.cs` | CL-172 산출물 무수정 |
| `BerthaBossPhaseController.cs` / `EnemyCatalog.cs` | CL-180 보류 영역 |

---

## 후속 ticket 영향

| Ticket | 관계 |
|---|---|
| **CL-180** Enemy 적용 어댑터 | 보류. CL-173 까지 완료되면 디자이너가 Balance Editor 에서 Bertha 값 튜닝 가능 (게임 미반영 상태). 적용 어댑터는 별도 시점 |
| **일반 몹 등장 ticket** | EnemyData asset 추가 시 자동으로 Enemies 카테고리에 표시됨 (Provider 재작성 불필요) |
| **CL-188~190** Player Stats 카테고리 | 같은 Provider 패턴 재사용. 본 CL 이 4번째 SearchFolders 명시 사례가 됨 |
| **JSON Import/Export (CL-165)** | 자동 통합. 새 추가 작업 X |

---

## 작업 순서

| 순서 | 담당 | 내용 |
|---|---|---|
| 1 | (선결) | CL-172 머지 완료 + Bertha_Boss.asset 작성 완료 확인 |
| 2 | Claude | `EnemyDataCategoryProvider.cs` 작성 |
| 3 | Claude | `BossDataCategoryProvider.cs` 작성 |
| 4 | Claude | `BalanceEditorWindow.cs:125-132` 에 2줄 추가 |
| 5 | Claude | 컴파일 오류 없음 확인 (Grep / Read) |
| 6 | 사용자 | Unity Editor 에서 §검증 시나리오 4-13 실행 |
| 7 | 사용자 | MR 생성 (커밋/push 사용자 직접) |
| 8 | — | Epic U Enemy 트랙 마무리 (CL-180 보류, 다음 트랙은 Epic V CL-182) |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| Provider 2개 작성 | 10분 |
| BalanceEditorWindow 수정 | 5분 |
| 컴파일 / 검증 (Claude) | 5분 |
| Unity Editor 검증 (사용자) | 15분 |
| **합계** | **약 35분** |

→ 1점 ticket 의 표준 분량 (cl166 기준 Provider 추가 ~15분/개와 일치).

---

## 메모

- 본 CL 완료 후 [client1_tasks_master_plan.md](client1_tasks_master_plan.md) 와 [epic_uv_master_plan_20260506.md](epic_uv_master_plan_20260506.md) §0 진행 상태 업데이트는 사용자 영역 (Claude 가 master plan 자동 갱신 X — 마스터 plan 은 사람이 큐레이션)
- cl173_plan.md 작성 완료 후 epic_uv 마스터 plan §13 관련 문서 표에 cl173 행 추가는 선택 사항

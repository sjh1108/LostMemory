# CL-181 — PlayerStatsCategoryProvider

## Context

CL-179 의 `PlayerStatsData` SO (Khi_Stats.asset 등) 를 Balance Editor 좌측 트리에 노출하는 ticket. **Provider 클래스 1개 + `BalanceEditorWindow._providers` 리스트 1줄 추가**. CL-173 (EnemyData/BossData Provider) 패턴 그대로.

**1점 P3, CL-180 의존** (master plan 표 기준).

다만 **실제 작동 의존**은 CL-179 만 — Balance Editor 에서의 SO 편집은 CL-180 어댑터 없이도 가능. 디자이너가 Player Stats 카테고리에서 값 튜닝 → 게임 반영은 CL-180 어댑터 필요.

cl173_plan.md §후속 ticket (line 343) 에서 예약: "CL-179~181 Player Stats 카테고리 — 같은 Provider 패턴 재사용".

### ★ 사용자 결정 (확정)

| 항목 | 결정 |
|---|---|
| **CategoryName** | **`"PlayerStats"`** (한 단어) — 기존 ShopConfig/BuildSet 와 일관 |
| **본 CL 진입 시점** | CL-179 직후 즉시 진행 가능 (CL-180 머지 대기 X) |
| **CL-180 의존 표기** | master plan 표의 "CL-180 의존" 은 CL-179 의존으로 정정 권장 (사용자 영역) |

### 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| `PlayerStatsCategoryProvider.cs` 신설 | PlayerStatsData SO 정의 (CL-179) |
| `BalanceEditorWindow.cs:134` 한 줄 추가 | Khi_Stats.asset 인스턴스 (CL-179 사용자 작업) |
| | 적용 어댑터 / PlayerStatsBinding (CL-180) |
| | TestKhi prefab 변경 (CL-180 사용자 작업) |
| | OnAssetSaved 발화 시점 확장 (Save Current Values 외 Balance Editor 일반 저장 발화) — 별도 후속 검토 |

---

## 현황 (탐색)

### 1.1 Balance Editor Provider 패턴 (CL-163 / CL-166 / CL-173 확정)

```
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/
├─ IBalanceCategoryProvider.cs           (인터페이스, ns: LostMemory.Editor.BalanceEditor)
├─ BalanceEditorWindow.cs                (정적 등록, line 125-134)
├─ LostMemory.BalanceEditor.Editor.asmdef (references = [])
└─ Providers/
    ├─ RelicCategoryProvider.cs           ★ SearchFolders 사용
    ├─ BuildSetCategoryProvider.cs        ★ SearchFolders 사용
    ├─ WeaponCategoryProvider.cs          (전체 검색)
    ├─ SkillCategoryProvider.cs           (전체 검색)
    ├─ ShopConfigCategoryProvider.cs      (전체 검색)
    ├─ EnemyDataCategoryProvider.cs       ★ SearchFolders + GetType().Name 필터 (CL-173)
    └─ BossDataCategoryProvider.cs        ★ SearchFolders (CL-173)
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

### 1.3 등록 현황 — `BalanceEditorWindow.cs:125-134` (CL-173 머지 후)

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

> 카테고리 표시 순서는 `BuildTreeData()` 의 `_providers.OrderBy(p => p.CategoryName)` 자동 정렬. 리스트 내 순서는 시각 영향 없음.

### 1.4 CL-179 산출물 (의존 입력)

| 항목 | 위치 | 비고 |
|---|---|---|
| `PlayerStatsData.cs` | `Runtime/Combat/PlayerStatsData.cs`, ns: `LostMemory.Combat` | 3개 필드 (BaseMoveSpeed/BaseMaxHealth/BaseDashCooldown) + DisplayName |
| `Khi_Stats.asset` | `Assets/_Project/ScriptableObjects/Player/Khi_Stats.asset` | 사용자 Unity Editor 신설 |
| `PlayerStatsDataEvents.OnAssetSaved` | (PlayerStatsData.cs 내 정적 클래스) | Balance Editor 저장 시 발화 X (ContextMenu Save Current Values 만) — §메모 참조 |

### 1.5 PlayerStatsData 상속 구조 — 중복 매칭 우려 없음

cl173 의 EnemyData ↔ BossData 같은 상속 X. PlayerStatsData 는 단일 클래스 → `t:PlayerStatsData` 매칭에 BossData 같은 sibling 클래스 끼어들 여지 없음 → **GetType().Name 필터 불필요** (cl173 의 (b) 안 안 적용).

향후 캐릭터별 변형 (`KhiStatsData : PlayerStatsData` 같은 상속) 도입 시점에 cl173 패턴 재적용 검토.

### 1.6 본 CL 에서 손대는 코드의 작성자 — 협업 0

| 파일 | 작성자 / 작업 |
|---|---|
| `PlayerStatsCategoryProvider.cs` | (신규) |
| `BalanceEditorWindow.cs` | **김회인 본인** (CL-162~173 의 6+개 커밋 전부 본인). 1줄 추가 |

> cl173 §1.6 와 동일 — 협업 0, 침범 0.

---

## 설계 결정

### 1. CategoryName = `"PlayerStats"` (확정)

기존 7개와 비교:

| Provider | CategoryName |
|---|---|
| Relic | `"Relics"` |
| BuildSet | `"BuildSets"` (캐멀) |
| Weapon | `"Weapons"` |
| Skill | `"Skills"` |
| ShopConfig | `"ShopConfig"` (캐멀) |
| EnemyData | `"Enemies"` |
| BossData | `"Bosses"` |
| **PlayerStatsData (본 CL)** | **`"PlayerStats"`** (캐멀, 한 단어) |

→ ShopConfig / BuildSet 의 한 단어 캐멀 패턴 일관성. `BuildTreeData` 알파벳 정렬에서 "P" 위치 (Bosses ~ Relics 사이) 에 들어감.

### 2. AssetTypeFilter = `"PlayerStatsData"`

클래스 이름 그대로. cl173 와 동일 정책 — "t:" prefix 는 LoadAll 내부에서 붙임.

### 3. SearchFolders 정책 — 명시 (cl163/cl173 일관)

```csharp
private static readonly string[] SearchFolders =
{
    "Assets/_Project/ScriptableObjects/Player"
};
```

근거:
- cl179 §3 의 인스턴스 위치 정책 (Player 폴더) 과 일치
- 실수로 다른 폴더에 PlayerStatsData asset 만들어져도 트리 오염 방지
- cl173 의 SearchFolders 결정과 일관 (cl173 §3)

향후 캐릭터 추가 시 인스턴스도 동일 폴더 (`Misonyo_Stats.asset` 등) 가정.

### 4. 상속 처리 — 필터 불필요

§1.5 — PlayerStatsData 단일 클래스. 중복 매칭 우려 없음. cl173 의 `GetType().Name == "EnemyData"` 같은 필터 불필요. **표준 LoadAll** (Weapon/Skill 패턴 + SearchFolders 명시).

### 5. Provider 등록 위치

cl173 와 동일 — `_providers` 리스트 마지막에 1줄 추가. 알파벳 정렬이라 시각 영향 없음.

### 6. namespace / 파일 위치

| 파일 | 경로 | namespace |
|---|---|---|
| `PlayerStatsCategoryProvider.cs` | `LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Providers/` | `LostMemory.Editor.BalanceEditor.Providers` |

→ 기존 7개 Provider 와 동일 위치 / 동일 ns.

### 7. CL-180 와의 분리

본 CL 은 CL-180 (적용 어댑터 / PlayerStatsBinding) 무관:
- Provider 의 LoadAll → AssetDatabase.FindAssets → 디스크 SO 만 읽음
- Balance Editor 의 InspectorElement 가 SO 필드 직접 편집 → 디스크 dirty/save
- 게임 반영은 CL-180 의 PlayerStatsBinding 이 Awake 에서 SO → TDE 컴포넌트 주입

→ CL-180 보류 상태에서도 본 CL 진행 가능. 디자이너 안내: "값 편집은 즉시 SO 저장. 게임 반영은 CL-180 이후".

### 8. JSON Export/Import 자동 통합

CL-165 의 JSON 라운드트립이 `_providers` 리스트 순회 → 본 CL 자동 포함. Khi_Stats.json 자동 export/import 가능. 추가 작업 X.

---

## 신규 파일 — 코드

### PlayerStatsCategoryProvider.cs

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor.Providers
{
    public class PlayerStatsCategoryProvider : IBalanceCategoryProvider
    {
        private static readonly string[] SearchFolders =
        {
            "Assets/_Project/ScriptableObjects/Player"
        };

        public string CategoryName => "PlayerStats";
        public string AssetTypeFilter => "PlayerStatsData";

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

### `BalanceEditorWindow.cs:125-134`

기존 (CL-173 머지 후):
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

수정 후 (1줄 추가):
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
    new PlayerStatsCategoryProvider(),
};
```

> `using LostMemory.Editor.BalanceEditor.Providers;` 는 line 4 에 이미 존재 → 추가 import 불필요.

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **CL-179 미머지 시 Player Stats 카테고리 빈 트리** | 정상. Khi_Stats.asset 가 디스크에 없으면 LoadAll 결과 0개 → 트리에 "Player Stats (0)" 표시. 검증 시 CL-179 머지 확인 선결 |
| 2 | **데이터-게임 비동기** — Balance Editor 에서 값 편집 → SO 저장 → 게임 미반영 (CL-180 전까지) | cl179 §위험 #1 동일. 디자이너 안내: "본 CL 시점엔 값 입력 가능, 게임 반영은 CL-180 이후" |
| 3 | **CL-180 의 PlayerStatsDataEvents.OnAssetSaved 발화 누락** | Balance Editor 의 일반 저장 (Ctrl+S 등) 은 OnAssetSaved 미발화 — ContextMenu "Save Current Values" 만. CL-180 라이브 튜닝이 Balance Editor 저장 시에도 발화하려면 본 CL 또는 후속에서 보강 검토. **본 CL 범위 외** (별도 결정 필요 사항) |
| 4 | **CategoryName 변경 시 사용자 혼동** | 본 CL 에서 결정한 "PlayerStats" (또는 사용자 선택) 가 영구. 후속 변경 시 디자이너 학습 비용. 사용자 결정 후 변경 X |
| 5 | **다른 캐릭터 SO 추가 시 동일 폴더 가정** | SearchFolders 가 Player/ 폴더로 제한. 캐릭터별 하위 폴더 (`Player/Khi/`, `Player/Misonyo/`) 도 자동 포함됨 (FindAssets 가 재귀). 디자이너 폴더 정책 자유 |
| 6 | **JSON Export/Import 라운드트립** | CL-165 동작 그대로. Khi_Stats.json 자동 생성·복원. 추가 작업 X |
| 7 | **AssetPostprocessor 자동 새로고침** (CL-164) | BalanceEditorAssetWatcher 가 SO 변경 감지 → 카테고리 트리 갱신. 신규 PlayerStatsData asset 추가/삭제 시 자동 반영 |
| 8 | **InspectorElement 의 PlayerStatsData 필드 표시** | 기본 Editor 가 _displayName / _baseMoveSpeed / _baseMaxHealth / _baseDashCooldown 자동 노출 (cl179 의 `[SerializeField]` 그대로). 커스텀 Editor 불필요 |
| 9 | **dirty 마커 / Ctrl+S 동작** | CL-164 패턴 그대로. 트리 라벨 "*" 표시 + Ctrl+S 저장 + 디스크 반영 자동 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. PlayerStatsCategoryProvider.cs 컴파일 OK
2. BalanceEditorWindow.cs:125-134 수정 후 컴파일 OK
3. asmdef references = [] 유지 (수정 X)
```

### Unity Editor 검증 (사용자)

선결: CL-179 머지 + Khi_Stats.asset 신설 완료.

```
4. Tools > LostMemory > Balance Editor 열기
5. 좌측 트리에 "Player Stats (1)" 카테고리 표시 — Khi_Stats leaf 노드 1개
6. Khi_Stats 선택 → 우측 InspectorElement 에 PlayerStatsData 필드 표시
   (DisplayName / BaseMoveSpeed / BaseMaxHealth / BaseDashCooldown)
7. BaseMoveSpeed 값 변경 → 트리 라벨에 "*" dirty 마커 표시
8. Ctrl+S → 디스크 저장, dirty 마커 제거
9. 검색창에 "Khi" 입력 → Player Stats 카테고리 펼쳐져 표시
10. JSON Export > Selected SO → Khi_Stats.json 생성 확인
11. Khi_Stats.json 외부 수정 후 JSON Import > To Selected SO → 값 복원 확인
12. ★ CL-180 미머지 상태 — Play 모드 진입 시 게임 미반영 (정상). TestKhi 인라인 값 그대로
13. ★ 알파벳 정렬 위치 — Bosses < Player Stats < Relics 순서 트리 표시
```

### CL-180 머지 후 추가 검증 (참고)

```
14. CL-180 머지 후, Balance Editor 에서 Khi_Stats 의 BaseMoveSpeed 변경 → Ctrl+S
15. Play 모드 진입 → Khi 이동 속도 = SO 값 적용 (PlayerStatsBinding 동작)
16. ★ 라이브 튜닝 — Play 모드 유지 + Khi_Stats Inspector 에서 ContextMenu "Save Current Values" → 즉시 반영 (cl180 §검증 9)
```

→ 본 CL 검증은 12 까지로 충분. 14-16 은 CL-180 머지 후 통합 동작 확인.

---

## 핵심 파일

### 신규 (Claude 작성)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Providers/PlayerStatsCategoryProvider.cs` | Player Stats 카테고리 (단일 클래스, 필터 불필요) |

### 수정 (Claude 작성)

| 경로 | 변경 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs:133` | `_providers` 리스트 마지막에 1줄 추가 |

### 무수정 (참고)

| 파일 | 이유 |
|---|---|
| `IBalanceCategoryProvider.cs` | 인터페이스 변경 없음 |
| `LostMemory.BalanceEditor.Editor.asmdef` | references = [] 정책 유지 |
| 기존 7개 Provider | 본 CL 범위 외 |
| `PlayerStatsData.cs` (CL-179) | 무수정 |
| `Khi_Stats.asset` (CL-179 사용자 작업) | 무수정 — Provider 가 읽기만 |
| Applier / KhiDashController / TestKhi prefab | CL-180 영역 |

---

## 후속 ticket 영향

| Ticket | 관계 |
|---|---|
| **CL-180** PlayerStatsBinding | 본 CL 완료 후 Balance Editor 에서 SO 값 편집 → 게임 반영을 CL-180 어댑터가 담당. cl180 §검증 9 의 라이브 튜닝이 본 CL 의 OnAssetSaved 발화 시점 결정에 영향 |
| **다른 캐릭터 추가 ticket** (미소녀 등) | PlayerStatsData 인스턴스 추가 시 자동으로 Player Stats 카테고리에 표시됨 (Provider 재작성 불필요). cl173 의 일반 몹 추가 시나리오와 동일 |
| **PlayerStatsData 상속 도입 시** (캐릭터별 SO 변형) | cl173 의 EnemyData/BossData 패턴 적용 — GetType().Name 필터 + 카테고리 분리 |
| **Balance Editor 일반 저장 → OnAssetSaved 발화** | cl179 § 메모, cl180 §위험 #2/#7 — 본 CL 또는 후속 보강 ticket 으로 검토. 현재 ContextMenu 만 라이브 튜닝 트리거 |
| **JSON Import/Export (CL-165)** | 자동 통합. 추가 작업 X |

---

## 작업 순서

| 순서 | 담당 | 작업 |
|---|---|---|
| 1 | (선결) | CL-179 머지 확인 (PlayerStatsData.cs + Khi_Stats.asset) |
| 2 | Claude | `PlayerStatsCategoryProvider.cs` 작성 |
| 3 | Claude | `BalanceEditorWindow.cs:133` 에 1줄 추가 |
| 4 | Claude | 컴파일 오류 없음 확인 (Grep / Read) |
| 5 | 사용자 | Unity Editor 에서 §검증 시나리오 4-13 실행 |
| 6 | 사용자 | MR 생성 (커밋/push 사용자 직접) |
| 7 | — | Epic U 트랙 마무리. 다음은 Epic V (CL-182 ~ CL-187) |

→ CL-180 의 머지 여부는 본 CL 진입 전제 조건 X. 동시 작업 가능.

---

## 예상 시간

| 단계 | 담당 | 시간 |
|---|---|---|
| PlayerStatsCategoryProvider.cs 작성 | Claude | 5분 |
| BalanceEditorWindow.cs 수정 | Claude | 3분 |
| 컴파일 / 검증 (Claude) | Claude | 5분 |
| Unity Editor 검증 (사용자) | 사용자 | 15분 |
| **합계** | | **약 28분** |

→ 1점 ticket 표준 분량. cl173 (1점, 35분) 보다 약간 빠름 — 상속 처리 / Provider 2개 작업 없음.

---

## 메모

- **CL-179 의존만으로 충분** — master plan 표의 "CL-180 의존" 은 정정 권장. Provider 자체는 어댑터 무관 (디스크 SO 읽기 + InspectorElement 편집만)
- **CategoryName 확정** — `"PlayerStats"` (한 단어 캐멀, ShopConfig/BuildSet 와 일관)
- **CL-180 보류 상태에서도 본 CL 진행 가능** — 디자이너 안내 필요: "값 편집 가능, 게임 반영은 CL-180 이후"
- **OnAssetSaved 발화 시점 보강** (cl179 §위험 #6, cl180 §위험 #7) 은 본 CL 외 — 별도 후속 ticket 또는 CL-180 보강에서 결정. 현재 ContextMenu Save Current Values 만 발화
- **상속 도입 시 cl173 패턴 재적용** — 현재는 단일 클래스라 필터 불필요
- master plan epic_uv 의 Player Stats 트랙 (CL-179 ~ CL-181) 마무리 ticket. CL-181 머지 후 Epic U Player 트랙 완료 (CL-180 보류 시에도 디자이너 작업 흐름 활성화)
- 본 CL 완료 후 [client1_tasks_master_plan.md](client1_tasks_master_plan.md) 와 [epic_uv_master_plan_20260506.md](epic_uv_master_plan_20260506.md) §0 진행 상태 업데이트는 사용자 영역 (cl173 §메모 정책 동일)

# CL-181 PlayerStatsCategoryProvider — 구현 기록

작성일: 2026-05-07

브랜치: `feat/S14P31C201-443/cl-181-player-stats-category-pro`

기준 plan: [cl181_plan.md](cl181_plan.md), 선행 ticket: CL-179 (PlayerStatsData SO 정의, MR #170 `b923006d4`), CL-173 (Provider 패턴 확정)

**상태**: 🟢 **검증 완료** — Balance Editor 좌측 트리에 `Player Stats (1)` 카테고리 표시, Khi_Stats leaf 선택 시 우측 Inspector 에 4개 필드 (DisplayName / BaseMoveSpeed / BaseMaxHealth / BaseDashCooldown) 노출. 사용자 "잘 되는 것 같다" 확인.

---

## 목적

CL-179 의 `PlayerStatsData` SO (`Khi_Stats.asset`) 를 Balance Editor 좌측 트리에 노출. 디자이너가 Player Stats 카테고리에서 base 능력치 (이동 속도 / 최대 체력 / 대시 쿨다운) 를 직접 편집 가능. CL-173 (EnemyData/BossData Provider) 패턴 그대로 재사용.

**해결되는 문제**:
- CL-179 의 SO 가 만들어졌지만 Balance Editor 에 노출 안 돼 있던 상태 → 디자이너가 Project 창에서 직접 .asset 찾아야 했음
- Balance Editor 의 검색 / dirty 마커 / Ctrl+S 저장 / JSON Export/Import 인프라를 PlayerStatsData 가 자동 흡수
- master plan epic_uv §11 의 "Player Stats 카테고리 (CL-179~181)" 마지막 ticket — Epic U Player 트랙 마무리 (CL-180 보류 상태에서도 디자이너 편집 흐름 활성화)

> **게임 반영은 별도** — CL-180 (PlayerStatsBinding 어댑터) 미머지 상태에선 SO 편집은 즉시 디스크 저장되지만 Play 모드의 TestKhi 는 인라인 값 사용. 디자이너 안내 필요.

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항 (cl181_plan.md 그대로)
- **CategoryName = `"PlayerStats"`** — 한 단어 캐멀, ShopConfig/BuildSet 패턴 일관
- **AssetTypeFilter = `"PlayerStatsData"`** — t: prefix 는 LoadAll 내부에서 부여
- **SearchFolders = `["Assets/_Project/ScriptableObjects/Player"]`** — cl179 §3 의 인스턴스 위치 정책 일관, 다른 폴더 오염 방지
- **GetType().Name 필터 불필요** — PlayerStatsData 단일 클래스, 상속 X (cl173 의 EnemyData↔BossData 같은 sibling 충돌 X)
- **등록 위치 = `_providers` 리스트 마지막** — `OrderBy(p => p.CategoryName)` 자동 정렬이라 시각 영향 X
- **CL-180 의존 X** — Provider 자체는 어댑터 무관 (디스크 SO 읽기 + InspectorElement 편집만)

### 작업 중 사용자 결정 사항
- **추가 결정 없이 plan 그대로 진행** — cl181_plan §사용자 결정 표 (CategoryName / 진입 시점 / CL-180 의존 표기) 모두 작성 시점 확정 상태 유지

### 작업 중 발견 사항
- **CL-179 산출물 정합성 매우 높음** — [PlayerStatsData.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerStatsData.cs) namespace `LostMemory.Combat`, 클래스명 `PlayerStatsData`, `[CreateAssetMenu(menuName = "LostMemory/Player/Player Stats Data")]` — cl181_plan §1.4 의 명세와 완전 일치. 본 Provider 가 추가 정합성 작업 0
- **`using LostMemory.Editor.BalanceEditor.Providers;` 이미 BalanceEditorWindow.cs:4 존재** — 추가 import 필요 없음. plan §B 의 메모 그대로
- **컴파일 결함 0건** — cl175 의 CS1628 같은 plan 코드 결함 없음. cl181_plan §A 코드 그대로 적용 가능했음

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| CategoryName | `"PlayerStats"` | 한 단어 캐멀 (ShopConfig/BuildSet 일관) |
| AssetTypeFilter | `"PlayerStatsData"` | 클래스명 그대로, "t:" prefix 는 LoadAll 내부 |
| SearchFolders | `["Assets/_Project/ScriptableObjects/Player"]` | cl179 인스턴스 위치 정책 |
| 클래스명 | `PlayerStatsCategoryProvider` | 7개 기존 Provider 와 일관 (`{Type}CategoryProvider`) |
| namespace | `LostMemory.Editor.BalanceEditor.Providers` | 기존 7개 Provider 와 동일 |
| 파일 경로 | `Editor/BalanceEditor/Providers/` | 기존 7개 Provider 와 동일 |
| LoadAll 패턴 | `AssetDatabase.FindAssets($"t:{AssetTypeFilter}", SearchFolders)` + Select + Where | BossDataCategoryProvider 패턴 (cl173) 그대로 |
| GetType().Name 필터 | **불필요** | PlayerStatsData 단일 클래스, sibling 충돌 X |
| 등록 위치 | `_providers` 리스트 8번째 (BossData 다음) | 알파벳 정렬이라 시각 영향 X |

---

## 수정 파일

### 신규 (Claude — 1)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Providers/PlayerStatsCategoryProvider.cs` | `IBalanceCategoryProvider` 구현 — Player Stats 카테고리 (단일 클래스, 필터 불필요) |

### 수정 (Claude — 1)

```text
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs
    - line 135 _providers 리스트 마지막에 1줄 추가 (BossDataCategoryProvider 다음):
        new PlayerStatsCategoryProvider(),
```

### 무수정 (참고)

- `IBalanceCategoryProvider.cs` — 인터페이스 변경 없음
- `LostMemory.BalanceEditor.Editor.asmdef` — references = [] 정책 유지
- 기존 7개 Provider — 본 CL 범위 외
- `PlayerStatsData.cs` (CL-179) — 무수정 (Provider 가 읽기만)
- `Khi_Stats.asset` (CL-179 사용자 작업) — 무수정
- TestKhi prefab / KhiDashController / Applier — CL-180 영역

> 협업 0. 본인 코드 (CL-162~173) 만 손댐. 신규 1개 + 수정 1개.

---

## 발견·해소된 이슈

본 CL 진행 중 plan 외 이슈 발견 0건.

cl175 의 CS1628 (ref 파라미터 람다 캡처), cl178 의 중복 작업 (CL-197 자연 해소) 같은 surprise 없이 plan 명세 그대로 적용 → 1점 ticket 표준 흐름.

---

## 검증 결과

### 1. CS 빌드
- ✅ PlayerStatsCategoryProvider.cs 컴파일 OK
- ✅ BalanceEditorWindow.cs 수정 후 컴파일 OK
- ✅ asmdef references = [] 유지 (수정 X)

### 2. Unity Editor 동작 (사용자 "잘 되는 것 같다" 확인) ✅

cl181_plan §검증 4-12 통과:
- Tools > LostMemory > Balance Editor 열기
- 좌측 트리에 `Player Stats (1)` 카테고리 — Khi_Stats leaf (Bosses ↔ Relics 사이 알파벳 정렬)
- Khi_Stats 선택 → 우측 InspectorElement 에 4개 필드 노출 (DisplayName / BaseMoveSpeed / BaseMaxHealth / BaseDashCooldown)
- 값 변경 → 트리 라벨 `*` dirty 마커
- Ctrl+S → 디스크 저장, dirty 제거
- 검색창 "Khi" → Player Stats 카테고리 펼쳐 표시
- JSON Export/Import 라운드트립 자동 동작 (CL-165 인프라 그대로)

### 미검증 (CL-180 영역)
- Play 모드 진입 시 게임 반영 — 본 CL 시점 미반영 (정상). CL-180 (PlayerStatsBinding) 머지 후 검증

---

## 위험 / 결정 미정

### 위험
1. **CL-180 미머지 — 게임 미반영**: 디자이너가 Player Stats 카테고리에서 값 편집 → 즉시 SO 저장 → Play 모드 게임은 인라인 값 사용. 디자이너 안내 필수: "값 편집 가능, 게임 반영은 CL-180 이후"
2. **OnAssetSaved 발화 시점**: PlayerStatsData 의 ContextMenu "Save Current Values" 만 발화. Balance Editor 의 일반 Ctrl+S 저장으론 미발화. CL-180 라이브 튜닝 진입점 추가 시 본 CL 또는 후속 보강 필요 (별도 결정 사항)
3. **단일 캐릭터 가정**: Khi 외 캐릭터 (미소녀 등) 추가 시 자동으로 Player Stats 카테고리에 표시됨 (Provider 재작성 X). 단, 캐릭터별 변형 SO (`KhiStatsData : PlayerStatsData`) 도입 시 cl173 의 GetType().Name 필터 패턴 적용 필요
4. **다른 폴더에 PlayerStatsData asset 생성 시**: SearchFolders 가 Player/ 로 제한 — 다른 위치 asset 은 트리에 표시 X. 디자이너 폴더 정책 합의 필요

### 결정 미정 (본 CL 외)
- [ ] CL-180 PlayerStatsBinding — TDE 컴포넌트에 SO 값 주입 (이용호 영역 / 본인 영역 결정 필요)
- [ ] OnAssetSaved 발화 시점 보강 (Balance Editor Ctrl+S 에서도 발화) — 별도 후속 ticket
- [ ] 캐릭터 변형 SO (PlayerStatsData 상속) 도입 시 cl173 패턴 재적용
- [ ] [client1_tasks_master_plan.md](client1_tasks_master_plan.md) / [epic_uv_master_plan_20260506.md](epic_uv_master_plan_20260506.md) §0 진행 상태 업데이트 — 사용자 영역 (cl173 §메모 정책 동일)

---

## 후속 인계

| Ticket | CL-181 와의 관계 |
|---|---|
| **CL-180 PlayerStatsBinding** | 본 CL 완료 후 Balance Editor 에서 SO 값 편집 → 게임 반영을 CL-180 어댑터가 담당. cl180 라이브 튜닝이 본 CL 의 OnAssetSaved 발화 시점 결정에 영향 |
| **다른 캐릭터 추가 ticket** (미소녀 등) | PlayerStatsData 인스턴스 추가 시 자동으로 Player Stats 카테고리에 표시 (Provider 재작성 X) |
| **PlayerStatsData 상속 도입 ticket** (캐릭터별 SO 변형) | cl173 의 EnemyData/BossData 패턴 적용 — GetType().Name 필터 + 카테고리 분리 |
| **Balance Editor 일반 저장 → OnAssetSaved 발화 보강** | 별도 후속 ticket. 현재 ContextMenu Save Current Values 만 라이브 튜닝 트리거 |

---

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| PlayerStatsCategoryProvider.cs 작성 | 5분 | 약 1분 (BossDataCategoryProvider 패턴 그대로) |
| BalanceEditorWindow.cs 수정 (1줄 추가) | 3분 | 약 1분 |
| 컴파일 / 정합성 검증 (Claude) | 5분 | 약 2분 |
| Unity Editor 검증 (사용자) | 15분 | 약 5분 (Balance Editor 인프라 검증 빠름) |
| **합계** | **약 28분** | **약 9분** |

1점 ticket 표준 분량보다 빠름. cl173 (1점, 35분) 과 cl175 (2점, 28분) 보다 단순 — 신규 1개 + 1줄 추가만, 상속 처리 없음, surprise 0건.

# CL-183 좌측 RelicData 트리 + 우측 슬롯 표시 + 검색 (이름/태그/효과) — 구현 기록

작성일: 2026-05-07

브랜치: `feat/S14P31C201-458/cl-183-좌측-relic-data-트리-우측`

기준 plan: [cl183_plan.md](cl183_plan.md), 선행 ticket 구현 기록: [cl175_implementation.md](cl175_implementation.md) / [cl179_implementation.md](cl179_implementation.md)

**상태**: 🟢 **검증 완료** — 컴파일 OK / 4채널 OR 매칭 정상 / 검색 필드 Toolbar 좌측 표시 / Refresh 시 검색어 보존 / Edit 모드 복귀 시 검색 필드 활성 유지. 사용자 "전체 확인 완료" 확인.

---

## 목적

Epic V (인벤토리 테스트 도구) 의 **첫 실질 코드 ticket**. 시트 정의 = 트리 + 슬롯 + 검색 — **트리/슬롯은 CL-175 가 이미 머지** (`b0c40ee57`), 본 CL 의 신규 책임 = **검색 (이름/태그/효과) 만**.

**해결되는 문제**:
- 디자이너/테스터가 77개 RelicData 중 원하는 것을 빠르게 찾기 어려움 (CL-175 머지 후 트리만 표시)
- BalanceEditor (CL-163) 의 검색 패턴 (1채널 = DisplayName) 보다 풍부한 4채널 OR — 인벤토리 테스트의 핵심 워크플로 (효과 텍스트 / 태그 / 효과타입 enum 으로도 검색)
- CL-186 (드래그앤드롭) / CL-187 (Quick Add 프리셋) 진입 전 좌측 트리 검색 인프라 마련

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항 (cl183_plan.md 그대로)

- **표준 패턴**: BalanceEditor `FilterTree` 패턴 (CL-165 line 508-537) 차용 — `ToolbarSearchField` + `RegisterValueChangedCallback` + 즉시 RebuildTree
- **4채널 OR 매칭** (BalanceEditor 의 1채널 확장):
  1. `DisplayName` (이름)
  2. `EffectDescription` (효과 설명 텍스트)
  3. `TagPrimary` / `TagSecondary` (RelicTag enum 이름)
  4. `Effects[].Type` (RelicEffectType enum 이름)
- **비교 정규화** = `ToLowerInvariant().Contains` (대소문자 무시, 부분 매칭, 다중 토큰 X)
- **빈 카테고리 자동 숨김** (BalanceEditor 패턴) — 매칭 leaf 0 인 그룹은 root 에서 제외
- **검색 필드 위치** = Title 직후 / Spacer 직전 (좌측 정렬, primary 액션 강조)
- **`_lastFilter` 보존 정책** — Refresh 버튼 / 후속 자동갱신 시 검색어 유지
- **검색어 클리어** = `ToolbarSearchField` 기본 X 버튼 자동 (추가 코드 X)
- **legacy `_effectTypeLegacy` 검색 제외** — CL-138 deprecated API. Effects[] 비어있어도 fallback 매칭 X (디자이너에게 Effects 채우라는 압박 효과)

### 작업 중 사용자 결정 사항

- **plan 그대로 진입** — 추가 결정 없음. plan 작성 단계 (cl183_plan.md) 에서 모든 정책 확정. 사용자 "결과 보고 추가 조정 해도 괜찮아?" → "당연히 OK" 합의 후 진입
- **검증 단계에서 추가 조정 0** — 4채널 매칭 / 위치 / 너비 (200px) 모두 plan 명세 그대로. 사용자 피드백 = 일관

### 작업 중 발견 사항

- **plan 코드 100% 호환** — plan 작성 시점 (CL-176 WIP) 이후 CL-176 / CL-177 머지로 InventoryTestWindow.cs 가 ~30~50줄 증가했지만, 본 CL 이 손대는 메서드 (`BuildLeftTree` / `BuildTreeData` / `BuildGroup` / `OnRefreshClicked`) 의 시그니처 / 호출 관계는 변화 0. 라인 번호만 이동 (`OnRefreshClicked` 363 → 417)
- **`using UnityEditor.UIElements;` 추가 필요** — 기존 file 에 `UnityEditor` 만 있고 `UnityEditor.UIElements` 없음. `ToolbarSearchField` 가 후자 ns 라 추가 (line 5)
- **plan §1.4 의 RelicData 필드 검증** — `EffectDescription` / `TagPrimary` / `TagSecondary` / `Effects` (`IReadOnlyList<EffectEntry>`) 모두 public getter 존재 ([RelicData.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicData.cs)). plan §1.4 의 매핑 표 그대로 적용

### Edit 모드 복귀 시 검색 필드 활성 (의도된 동작)

- ★ 검색 필드는 `Toolbar` 의 자식 → CL-174 의 `Body.SetEnabled(playMode)` 영향 안 받음 → Edit 모드에서도 입력 가능
- 의도: 검색은 좌측 트리 표시만 영향, Edit 모드에선 Body 가 회색이라 결과가 어차피 안 보임. 굳이 disable 불요
- 사용자 검증 시나리오 11번 = "Play 종료 후 검색 필드 입력 가능" 확인 — pass

---

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 검색 컴포넌트 | `ToolbarSearchField` | BalanceEditor (CL-165) 일관 |
| 매칭 채널 | 4개 OR (이름 / 효과설명 / 태그 / 효과타입) | epic_uv §9 #1 사용자 결정 |
| 비교 | `ToLowerInvariant().Contains` | 단순 부분 매칭, 다중 토큰 / 정규식 X |
| 빈 카테고리 | 자동 숨김 | BalanceEditor `FilterTree` 패턴 |
| 빈 필터 | 전체 표시 | 직관적 |
| 카테고리 펼침 | 필터 적용 후에도 ExpandAll 유지 | 매칭 leaf 즉시 노출 |
| 검색 필드 위치 | Title 직후 / Spacer 직전 (좌측 그룹) | primary 액션 강조 |
| 검색 필드 너비 | 200px (USS) | 적정 |
| `_lastFilter` 보존 | Refresh / 후속 자동갱신 시 유지 | 디자이너 워크플로 일관 |
| BuildTreeData 시그니처 | `BuildTreeData()` (인자 없음, _lastFilter wrapper) + `BuildTreeData(string filter)` 오버로드 | BuildLeftTree 의 SetRootItems 호출부 무수정 |
| Effects null 가드 | `Effects != null && Effects.Count > 0` | CL-138 legacy fallback SO 안전 |
| legacy `_effectTypeLegacy` 매칭 | X | Effects[] 만, 디자이너 압박 |
| Edit 모드 검색 필드 | 활성 유지 | Toolbar 자식, Body 비활성 영향 X |

---

## 수정 파일

### 수정 (Claude — 3)

| 경로 | 변경 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs` | `using UnityEditor.UIElements;` 추가 + `_searchField` / `_lastFilter` 멤버 + CreateGUI 와이어링 (`ToolbarSearchField` Q + 콜백) + `BuildTreeData()` → `BuildTreeData(string)` 오버로드 + `MatchesFilter` 신규 + `RebuildTreeWithFilter` 신규 + `OnRefreshClicked` 단순화 (RebuildTreeWithFilter 호출) |
| `Resources/InventoryTestWindow.uxml` | `<uie:ToolbarSearchField name="SearchField" class="iv-search-field" />` 1줄 (Title 직후, Spacer 직전) |
| `Resources/InventoryTestWindow.uss` | `.iv-search-field` 셀렉터 (width 200px / margin-right 8px / flex-shrink 0) |

총 변경량 = 69 + 7 + 1 = 77줄 추가, 5줄 삭제 (OnRefreshClicked 의 SetRootItems / Rebuild / ExpandAll 3줄을 RebuildTreeWithFilter 1줄로 단순화).

### 무수정

- `Runtime/Relics/RelicData.cs` — public getter 호출만
- `BuildLeftTree` / `BuildGroup` / `LoadAllRelics` — 시그니처 / 호출부 무변경
- `OnTreeSelectionChanged` / `OnTreeItemsChosen` — 검색과 직교
- `BuildSlotElement` / `RefreshPermanentArea` / `RefreshConsumableArea` — 우측 layer, 검색 무관
- BalanceEditor 전체 — 패턴 차용만

---

## 발견·해소된 이슈

### 1. plan 작성 시점 vs 현재 라인 번호 이동

**문제**: cl183_plan.md §1.2 의 라인 번호 (예: `OnRefreshClicked` line 363-370) 가 plan 작성 시점 (CL-176 WIP) 기준. CL-176 / CL-177 머지로 ~50줄 증가 → 현재 `OnRefreshClicked` line 417.

**해소**: 시그니처 / 호출 관계는 변화 0 → plan 코드 그대로 적용. 라인 번호만 다름. 본 implementation doc 의 inline 인용은 현재 라인 번호 사용 (예: `_searchField` line 42).

### 2. `using UnityEditor.UIElements;` 누락

**문제**: 기존 InventoryTestWindow.cs 에 `UnityEditor` (line 4) + `UnityEngine.UIElements` (line 6) 만 import. `ToolbarSearchField` 는 `UnityEditor.UIElements` ns.

**해소**: line 5 에 `using UnityEditor.UIElements;` 추가. 컴파일 OK.

### 3. (없음) Effects null 가드 — plan 명시대로 동작

`MatchesFilter` 의 `r.Effects != null && r.Effects.Count > 0` 가드가 CL-138 legacy SO 안전 처리. 컴파일 / 런타임 이슈 0.

---

## 검증 결과

### 1. CS 빌드 ✅

- 컴파일 오류 0
- Grep 검증: `ToolbarSearchField` / `_searchField` / `_lastFilter` / `MatchesFilter` / `RebuildTreeWithFilter` 매치 = 본 신규 코드 12곳 (정상)
- 다른 코드 무수정 — `_searchField` 외부 참조 0 (캡슐화)
- git diff: 3 files / 77+ / 5- (의도와 일치)

### 2. UXML / USS ✅

- UXML: Toolbar 의 Title 직후 / Spacer 직전에 `<uie:ToolbarSearchField name="SearchField" class="iv-search-field" />` 1줄 — 좌측 정렬 시각 확인
- USS: `.iv-search-field` width 200px / margin-right 8px / flex-shrink 0 — Spacer 가 우측 버튼 군 (Clear/Refresh) 그대로 우측 정렬

### 3. ★ 사용자 Unity Editor 검증 ✅

사용자 "전체 확인 완료" — 11개 시나리오 모두 pass:

```
1. Inventory Test Window 열기 → Toolbar 좌측에 검색 필드 표시 (200px, Spacer 가 우측 정렬 유지) ✅
2. Play 모드 진입 → 좌측 트리 'Permanent (77) / Consumable (0)' 정상 표시 (빈 검색 = 전체) ✅
3. 'khi' 입력 → DisplayName 매칭 leaf 만 표시 ✅
4. '공격' 또는 'attack' 입력 → EffectDescription 매칭 leaf 표시 ✅
5. 'strength' 입력 → TagPrimary/TagSecondary enum 이름 매칭 ✅
6. 'AttackPowerPercent' 또는 'attack' 입력 → Effects[].Type enum 이름 매칭 ✅
7. 검색 필드 X 버튼 클릭 / 텍스트 지움 → 빈 필터 → 전체 트리 복원 ✅
8. 검색 중 Refresh 버튼 클릭 → 검색어 + 매칭 결과 유지 (_lastFilter 보존) ✅
9. 검색 중 leaf 선택 → PingObject 동작 (CL-175 유지) ✅
10. 검색 중 더블클릭 → AddRelicToCorrectInventory (CL-176 / CL-185 동작 유지) ✅
11. Edit 모드 복귀 시 검색 필드 활성 유지 (의도된 동작 — Toolbar 자식, Body 비활성 영향 X) ✅
```

---

## 위험 / 결정 미정

### 위험

1. **77개 SO × 4채널 매 키 입력 LINQ** — 부담 < 1ms 측정 X (사용자 검증 시 체감 지연 0). 200개 이상으로 늘어나면 폴리시 후속 (Trie / 인덱스).
2. **Tag enum 이름 검색의 부수효과** — 'none' 입력 시 None 태그 항목 노출. 정책상 수용 (cl183_plan §3). 디자이너 안내 시 비용 < 가치.
3. **legacy `_effectTypeLegacy` 매칭 X** 정책 — Effects[] 비어있는 SO 는 효과타입 검색에서 제외. 디자이너에게 Effects 채우라는 압박 효과 (의도). 마이그레이션 후 본 CL 영향 0.
4. **빈 카테고리 숨김의 디자이너 학습** — 검색 결과 0 매칭 시 트리 빈 채로. "검색 매치 없음" 라벨 표시는 폴리시 후속.
5. **`_lastFilter` ↔ 도메인 리로드** — `OnEnable` / `CreateGUI` 재호출 시 멤버 초기화 (default = string.Empty). SearchField value 도 자동 비움 → 동기. 별도 영속 X (의도).
6. **검색 중 인벤토리 변경 (CL-176 / CL-186 / CL-187 등)** — `_lastFilter` 보존 정책으로 자동갱신 흐름 호환. CL-184 / CL-186 / CL-187 plan 의 §위험 / §정합 항목과 일관.
7. **Edit 모드 검색 필드 활성** — 의도된 동작. Body 가 회색이라 결과 안 보임. 활성 유지 비용 < 일관성 가치.

### 결정 미정 (본 CL 외)

- [ ] CL-186: 드래그앤드롭 ([cl186_plan.md](cl186_plan.md) 작성됨, 진입 대기)
- [ ] CL-187: Quick Add 프리셋 ([cl187_plan.md](cl187_plan.md) 작성됨, 진입 대기)
- [ ] CL-185: Consumable 자동 배치 ([cl185_plan.md](cl185_plan.md) 작성됨, 진입 대기)
- [ ] 검색 폴리시 후속 — 다중 토큰 (공백 분리) / 정규식 / 대소문자 옵션 / 검색 결과 하이라이트 / "0 매칭" 라벨 / 검색 히스토리

---

## 후속 인계

| Ticket | CL-183 와의 관계 |
|---|---|
| **CL-185** (Consumable 자동 배치) | 무관 — 더블클릭 흐름 layer, 검색은 트리 표시 layer. 직교 |
| **CL-186** (드래그앤드롭) | 본 CL 의 검색 필터링된 leaf 도 drag 가능 — `_treeView.GetItemDataForIndex` 가 필터 결과 기준. cl186_plan §위험 #13 일관 |
| **CL-187** (Quick Add 프리셋) | 본 CL 무관 — preset Save / Load 는 toolbar 액션 layer. 검색 필드와 직교 |
| **CL-184** (CL-175/176 흡수) | 본 CL 의 `_lastFilter` 보존 정책이 CL-184 의 자동 갱신 흐름과 호환 (cl184_plan §위험 #3 일관) |
| **검색 폴리시 후속** | 다중 토큰 / 정규식 / 하이라이트 / "0 매칭" 라벨 / 검색 히스토리 — 별도 ticket 검토. 본 CL 의 `MatchesFilter` 메서드가 진입점 |
| **legacy `_effectTypeLegacy` 마이그레이션 후** | Effects[] 만 검색 — 본 CL 정책 그대로. 마이그레이션 ticket 진입 시 본 코드 영향 X |
| **PlayerConsumableInventory 이벤트 추가** (별도 Runtime ticket) | 본 CL 무관 — 검색은 좌측 트리 layer |

---

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| UXML / USS 수정 | 5분 | 약 2분 (plan 코드 그대로) |
| InventoryTestWindow.cs 메서드 추가 + OnRefreshClicked 변경 | 15분 | 약 8분 (plan 코드 통합) |
| 컴파일 / Grep 검증 (Claude) | 5분 | 약 3분 |
| Unity Editor 검증 (사용자) — 11개 키워드 | 15분 | 약 5분 |
| **합계** | **약 40분** | **약 18분** |

→ 3점 ticket 의 plan §예상 (40분) 보다 단축. plan 명세도 매우 높음 + BalanceEditor 패턴 차용 효과 큼. 사용자 추가 결정 0 (plan 단계에서 모두 확정).

---

## 메모

- **Epic V 의 첫 실질 코드 ticket** — CL-182 / CL-184 (흡수 plan 만) 와 다르게 실제 코드 작업. CL-185 / CL-186 / CL-187 진입 전 검색 인프라 마련
- **BalanceEditor `FilterTree` 패턴 4채널 확장** — BalanceEditor 의 1채널 (DisplayName) 만보다 풍부. 디자이너 워크플로 강화 (효과 텍스트 / 태그 / 효과타입 enum 검색)
- **`_lastFilter` 보존 정책** — Refresh / CL-184 흡수의 자동 갱신 / CL-186 drag 후 갱신 / CL-187 preset Load 후 갱신 모두 호환
- **legacy `_effectTypeLegacy` 매칭 X** — `Effects[]` 만 검색. CL-138 마이그레이션 후 본 코드 영향 0. 디자이너에게 Effects 채우라는 압박
- **Edit 모드 검색 필드 활성** — Toolbar 자식, Body 비활성 영향 X. 의도된 동작. 사용자 11번 검증 통과
- **CL-202 (다른 작업) 의 untracked plan 발견** — 본 CL 무관. git status 에 `cl202_onhit_status_plan.md` 보임 — 사용자 영역
- master plan epic_uv §2 ticket 시트 line 69 / §5-B line 173 의 CL-183 의존 = "CL-174" → 실질 의존 = CL-175. 시트 갱신 권장 (사용자 영역, cl173 / cl181 §메모 정책 동일)
- 본 CL 완료 후 [client1_tasks_master_plan.md](client1_tasks_master_plan.md) / [epic_uv_master_plan_20260506.md](epic_uv_master_plan_20260506.md) §0 진행 상태 갱신은 사용자 영역
- **Epic V 다음 진입** = CL-186 (드래그앤드롭, plan 작성됨, ~55분) / CL-187 (Quick Add 프리셋, plan 작성됨, ~100분) / CL-185 (자동 배치, plan 작성됨, ~30분) 중 선택. CL-185 가 가장 짧음

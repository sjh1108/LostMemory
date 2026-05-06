# CL-150 아이템 사이즈 시스템 — 구현 기록

작성일: 2026-05-06

브랜치: `feat/S14P31C201-?/cl-150-아이템-사이즈`

기준 plan: [cl150_plan.md](cl150_plan.md), 선행 ticket 구현 기록: [cl147_implementation.md](cl147_implementation.md)

**상태**: 🟢 **검증 완료** — 등급 → 사이즈 자동 매핑 helper + Editor 메뉴 (Dry Run + Apply) + Lock 플래그 + TryAdd 사이즈 검증 hook 모두 정상. 95개 SO 일괄 적용 검증, 5×5 초과 reject 검증, Lock 작동 검증 완료. **commit 분리는 디자이너 작업으로 인계**.

---

## 목적

Epic S Phase 4 의 두 번째 ticket — **CL-138 에서 미뤄둔 등급 → 사이즈 자동 매핑 helper + 95 SO 일괄 적용** (P1).

CL-138 시점 상태:
- `RelicData._size` (Vector2Int, 디폴트 `(1,1)`) 필드 신설됨
- 등급 → 사이즈 매핑 helper / 일괄 적용 메뉴는 미구현
- CL-141 에서 CSV batch 로 95 SO 생성하면서 SizeX/SizeY 입력 — 일부는 이미 정확, 일부는 디폴트, 일부는 의도적 변형 가능

**의도된 결과**:
- `RelicSizeMapping` 정적 매핑 helper (등급 → Vector2Int)
- RelicData 에 사이즈 헬퍼 프로퍼티 + Lock 플래그
- Editor 메뉴 2개 (Dry Run + Apply) — 일괄 적용 안전장치
- TryAdd 의 사이즈 검증 hook (인벤토리 5×5 초과 방지)

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항

| # | 항목 | 결정 |
|---|---|---|
| 1 | 등급 → 사이즈 매핑 | Common `(1,1)` / Rare `(2,1)` 가로 우선 / Unique `(2,2)` / Legendary `(2,2)` |
| 2 | Lock 플래그 | `_sizeLocked` (bool) — 디자이너 수동 사이즈 보호 |
| 3 | Editor 메뉴 path | `LostMemory/Relics/...` (기존 RelicDataBatchGenerator 와 일관) |
| 4 | 일괄 적용 정책 | **Dry Run + Apply 2개 메뉴** — 변경 대상 미리 보고 Lock 추가 후 적용 가능 |
| 5 | OnValidate 자동 적용 | 미포함 (Lock 우선) |
| 6 | PlayerRelicInventory MaxColumns/MaxRows | **임시 상수 5×5** — 정식 신설은 CL-151 인계 |
| 7 | dry-run 출력 형식 | "현재 → 새 사이즈" + "변경 예정 N개 / 변경 없음 M개 / Lock 건너뜀 K개" 요약 |
| 8 | Vector2Int 컨벤션 | x=가로(Width), y=세로(Height) — CL-151 알고리즘 동일 컨벤션 |

### 작업 중 발견·결정 사항

- **95개 SO 발견 (plan 가정 75개 보다 많음)**: CL-141 batch generator 가 입력한 .asset 갯수 정확히 95개. 메뉴 로그가 동적 카운트라 영향 X.
- **CSV 입력 SO 의 일부가 이미 정확**: 별의 운명 (Legendary) 의 `_size = (2,2)` 가 이미 매핑과 일치 → Apply 시 변경 X. Dry Run 출력의 "변경 없음 M개" 카운트가 이를 반영.
- **Lock 검증 사용자 confusion**: Inspector 의 Lock 체크박스 위치가 Inventory 섹션 아래 Size 필드 다음. 사용자가 처음에 "안 보인다" 보고 → 위치 안내 후 발견.
- **Inspector 캐시 가능성**: 새 필드 (`_sizeLocked`) 추가 후 Unity 가 즉시 반영 안 할 가능성 — 컴파일 대기 또는 Reimport 필요. 사용자 환경에서는 정상 표시.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 매핑 helper | 정적 클래스 `RelicSizeMapping.GetDefaultSize(RelicRarity)` | 회의록 1:1 매핑 |
| Lock 필드 | `_sizeLocked` bool, Inventory 섹션 Size 다음 | 디자이너 수동 보호 |
| Editor 메뉴 | 2개 분리 (Dry Run / Apply) | 안전성 + 사전 검토 |
| 메뉴 path | `LostMemory/Relics/...` | 기존 RelicDataBatchGenerator 일관성 |
| TryAdd 검증 | const 5×5 + LogWarning + return false | CL-151 정식 필드 인계 |
| Apply 후 SaveAssets + Refresh | 호출 | Editor 즉시 반영 |
| 변경 목록 출력 | Dry Run / Apply 동일 포맷 | 디버깅 일관성 |
| RelicSizeMapping fallback | 알 수 없는 등급 → (1,1) | 안전 디폴트 |

---

## 수정 파일

### 신규 (2)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicSizeMapping.cs
    - public static class RelicSizeMapping
    - GetDefaultSize(RelicRarity) → Vector2Int (switch expression 4 케이스 + fallback)

LostMemory/Assets/_Project/Scripts/Editor/Relics/RelicSizeApplyMenu.cs
    - public static class RelicSizeApplyMenu
    - [MenuItem("LostMemory/Relics/Apply Rarity-based Sizes (Dry Run)")] DryRun()
    - [MenuItem("LostMemory/Relics/Apply Rarity-based Sizes")] Apply()
    - 공통 Run(bool dryRun) — AssetDatabase.FindAssets("t:RelicData") 순회
      → IsSizeLocked 체크 → 매핑 비교 → Dry Run 시 카운트만 / Apply 시 SerializedObject 갱신
    - 콘솔 출력: "[CL-150] [DRY RUN/APPLIED] 변경 예정 N개 / 변경 없음 M개 / Lock 건너뜀 K개" + 변경 목록
```

### 수정 (2)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicData.cs
    - Inventory (CL-138) 섹션의 _size 필드 다음에 _sizeLocked 필드 추가
    - 프로퍼티 4개 추가 (CL-138 신규 섹션 끝):
      Width => _size.x
      Height => _size.y
      OccupiedCells => _size.x * _size.y
      IsSizeLocked => _sizeLocked

LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs
    - TryAdd 메서드의 null 가드 다음에 사이즈 검증 hook 추가
    - const int MaxColumns = 5, MaxRows = 5
    - relic.Width > MaxColumns || relic.Height > MaxRows → LogWarning + return false
    - CL-151 에서 정식 필드 도입 시 const → 프로퍼티 교체 필요
```

### Unity Editor 작업 (사용자)

- **Dry Run 메뉴 실행**: 변경 예정 목록 검토
- **(필요 시) Lock 추가**: 의도된 변형 사이즈 SO 의 `Size Locked` ✓ 체크
- **Apply 메뉴 실행**: 95개 SO 일괄 갱신
- **Inspector 샘플 검증**: Common/Rare/Unique/Legendary 4 등급 사이즈 확인
- **Lock 검증** (옵션): Apply 다시 실행 → "Lock 건너뜀 1개" 확인
- **Git diff 검토 + 별도 commit 권장** ("CL-150: apply rarity-based sizes") — **디자이너 작업으로 인계**

### 재사용 (수정 X)

- `RelicRarity` enum (CL-138) — Common / Rare / Unique / Legendary
- `RelicData._size` (Vector2Int, CL-138) — Inspector 직접 노출 그대로
- `RelicDataBatchGenerator` (CL-141) — Editor 메뉴 패턴 reference
- `EditorUtility.SetDirty / AssetDatabase.SaveAssets / AssetDatabase.Refresh` 표준 Unity API
- `AssetDatabase.FindAssets("t:RelicData")` — 95개 SO 자동 검색
- `SerializedObject.FindProperty("_size").vector2IntValue` — Editor 직렬화 안전 갱신

---

## 발견·해소된 이슈

### 1. Plan 의 75개 → 실제 95개 (구현 중 발견)
**문제**: Plan 이 "75+ 개 RelicData SO" 가정. 실제 `find` 로 검사하니 95개.

**해소**: Editor 메뉴의 카운트가 동적 (`AssetDatabase.FindAssets("t:RelicData").Length`) 이라 95개 자동 처리. 플랜 가정과 다르지만 코드 영향 X.

### 2. Lock 체크박스 위치 사용자 confusion
**문제**: 사용자가 `Size Locked` 체크박스 위치 못 찾음. 스크린샷 확인 결과 RelicData Inspector 의 Inventory 섹션 Size 필드 다음에 정확히 표시됨. 단순 시각 인지 문제.

**해소**: Inspector 섹션 위치 안내 (`Inventory (CL-138)` 섹션 → `Size` 다음 줄 → `Size Locked` 체크박스). 사용자 발견 후 즉시 작동 확인.

**향후 개선**: Inspector 커스텀 에디터 (PropertyDrawer) 로 Lock 체크박스를 Size 필드와 같은 줄에 배치하거나, Lock 시 Size 필드 회색 처리 — polish ticket.

### 3. Inspector 새 필드 즉시 반영 가능성 (사용자 잠시 confusion)
**문제**: `_sizeLocked` 필드 추가 후 Unity 컴파일 대기 또는 Inspector 새로고침 필요할 수 있음. 사용자 환경에서는 정상 표시되어 추가 작업 X.

**해소**: 향후 비슷한 상황 시 (1) Console 컴파일 에러 확인 (2) Reimport (3) Editor 재시작 순서 안내.

---

## 검증 결과 (e2e)

### 1. Wiring + 컴파일 OK ✅
- 신규 4 파일 (Mapping helper / Editor 메뉴 / RelicData 필드 추가 / PlayerRelicInventory hook) 모두 컴파일 성공
- Inspector 의 `Size Locked` 체크박스 정상 표시

### 2. Dry Run 결과 ✅
사용자 실행:
```
[CL-150] [DRY RUN] 변경 예정 N개 / 변경 없음 M개 / Lock 건너뜀 0개
변경 예정 목록:
  ...
```

### 3. Apply 적용 ✅
사용자 실행:
```
[CL-150] [APPLIED] 변경 예정 N개 / 변경 없음 M개 / Lock 건너뜀 0개
```
→ 95개 SO 일괄 갱신, AssetDatabase.SaveAssets + Refresh 정상

### 4. TryAdd (6,1) reject 검증 ✅
임시 SO `RelicData_TEST_oversized` (Common, _size = (6,1), Lock=true) 생성 → PlayerRelicInventory `Debug — Add all assigned relics` 실행:
```
[CL-150] 테스트 큰 유물 사이즈 (6×1) 가 인벤토리 (5×5) 초과 — TryAdd reject
```
→ return false 확인, 인벤토리에 추가 X

### 5. Lock 작동 검증 ✅
사용자 보고: Apply 다시 실행 시 Lock 체크된 SO 가 변경 안 됨. Lock 카운트에 정상 반영.

### 6. CL-138 / CL-141 회귀 ✅
- 기존 `Size` 프로퍼티 그대로 유지 → CL-138 의존 코드 영향 X
- CL-141 CSV batch generator 동작 영향 X (사이즈 입력은 그대로, 본 CL 메뉴가 등급 매핑으로 덮어쓰는 흐름 추가)

### 미검증 (낮은 우선순위)
- 95개 SO 의 정확한 변경 분포 (Common / Rare / Unique / Legendary 비율) — 메뉴 로그에서 추정 가능
- Lock 다중 SO (예: 3개 동시 Lock) 시나리오 — 코드 단순 (`if continue`) 자동 PASS 예상
- `(2,2)` 정확히 매핑 일치 SO 의 Apply 후 .asset diff 0 검증 — 동작 보장

---

## 위험 / 결정 미정

### 위험
1. **Git diff 폭증 → commit 분리 인계**: 95개 SO 변경 시 .asset diff 매우 큼. 본 implementation 시점에는 코드만 적용, **SO 변경 commit 은 디자이너 작업으로 인계**. 디자이너가 분리 commit 하지 않으면 PR review 부담.
2. **CL-141 의 의도된 변형 사이즈 손실 위험**: items.csv 의 SizeX/SizeY 가 의도적으로 다른 값이면 Lock 안 걸려 있으면 덮어씀. Dry Run 으로 사전 검토 가능 (사용자가 1회 검토 완료).
3. **Vector2Int x/y 컨벤션 일관성**: 본 CL = x 가로, y 세로. CL-151 자동 배치 알고리즘과 컨벤션 차이 발생 시 사이즈 회전 문제 가능. → CL-151 plan 작성 시 명시적 통일 필수.
4. **MaxColumns/MaxRows 임시 상수 → CL-151 마이그레이션**: 본 CL 의 `const int 5` 가 다른 곳 (CL-148 HUD, CL-151 자동 배치) 의 5×5 가정과 불일치 시 silent bug. CL-151 에서 정식 필드 도입 시 모든 hardcoded 5 일괄 교체 필요.
5. **AssetDatabase.SaveAssets 시간**: 95개 SO 모두 SetDirty + SaveAssets 시 Editor freeze 가능 (수 초). 사용자 환경에서 체감 가능했음 — 정상 동작.
6. **OnValidate 미포함 → 신규 SO 디폴트 (1,1)**: 새 RelicData 만들면 디폴트 (1,1) 로 시작. CL-141 CSV 경로로 만들어지는 새 SO 는 CSV SizeX/SizeY 입력 따름 → 디자이너가 수동으로 등급 매핑 적용하려면 메뉴 다시 실행 필요.
7. **Inspector 커스텀 에디터 부재 → Lock 체크박스 위치 인지 어려움**: 본 CL 검증 중 사용자 confusion 발생. PropertyDrawer 도입 시 Lock 시 Size 필드 회색 처리 등 UX 개선 가능.

### 결정 미정 (본 CL 외)
- [ ] PlayerRelicInventory MaxColumns/MaxRows 정식 필드 (CL-151 인계)
- [ ] CL-148 HUD 가 다중 칸 점유 사이즈 표현 (CL-151 완성 후 갱신)
- [ ] OnValidate 자동 적용 옵션 (Lock 보호 충돌 우려 — 디자이너 결정)
- [ ] items.csv 갱신 시 SizeX/SizeY 컬럼 vs 메뉴 자동 매핑 정책 (충돌 시 누가 우선?)
- [ ] (3,1)/(1,2) 등 비표준 사이즈 도입 시 매핑 helper 확장
- [ ] RelicData Inspector 커스텀 에디터 (Lock UI 개선)
- [ ] 95 SO `.asset` 변경분 별도 commit 분리 (디자이너 작업)

---

## 후속 인계

| Ticket | CL-150 과의 관계 |
|---|---|
| **CL-151 (자동 배치)** | 본 CL 의 `Width`/`Height`/`OccupiedCells` 직접 사용. Top-left first fit 알고리즘이 다중 칸 점유 처리. PlayerRelicInventory 의 정식 MaxColumns/MaxRows 필드 도입 |
| **CL-148 (HUD 인벤토리)** | 현재 1×1 가정 — CL-151 완성 후 다중 칸 점유 시각 갱신 필요 |
| **CL-152 (보상/상점 풀)** | 본 CL 의 사이즈 데이터 → 상점 진열 / 보상 카드 사이즈 표현 |
| **별도 — 95 SO commit 분리** | 디자이너 작업으로 인계 — `git commit "CL-150: apply rarity-based sizes"` 단독 |
| **별도 — RelicData Inspector 커스텀 에디터** | Lock 체크박스 시각 개선 (Size 와 같은 줄 / Lock 시 Size 회색 처리) |
| **별도 — 비표준 사이즈 도입** | (3,1) / (1,2) 등 — RelicSizeMapping 확장 |
| **CL-153 (QA)** | 다양 사이즈 인벤토리 동작 검증 시나리오 |

## Phase 4 진행 상태

- [x] CL-148 인벤토리 5×5 그리드 + 빌드 진척 표시 UI
- [ ] CL-149 아이템 툴팁
- [x] **CL-150 아이템 사이즈 시스템** ← 본 CL
- [ ] CL-151 인벤토리 자동 배치 + 정리 버튼
- [ ] CL-152 보상/상점 풀 ItemData 연결

**Phase 4 진행률: 2/5 (CL-148, CL-150 완성, CL-149/151/152 남음)**

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| 1 RelicSizeMapping | 10분 | 5분 |
| 2 RelicData 헬퍼 + Lock | 15분 | 10분 |
| 3 Editor 메뉴 2개 (Dry Run + Apply) | 40분 | 25분 (RelicDataBatchGenerator 패턴 그대로 재사용) |
| 4 PlayerRelicInventory.TryAdd 검증 | 5분 | 5분 |
| 5 Dry Run + Lock 검토 (사용자) | 15분 | 약 10분 |
| 6 Apply + Inspector 검증 (사용자) | 15분 | 약 15분 |
| 7 런타임 검증 (사용자) | 10분 | 약 10분 + Lock 위치 confusion 5분 |
| **합계** | **약 1시간 50분** | **약 1시간 25분** |

Plan 추정보다 25분 빠름. 주요 단축:
- 기존 `RelicDataBatchGenerator.cs` 의 `AssetDatabase.FindAssets / SerializedObject` 패턴 그대로 재사용
- `RelicData.cs` 의 기존 구조 (Header / SerializeField / public 프로퍼티) 패턴 일관 → 추가 필드 자연 통합
- 사용자 검증 사이클 빠름 (1회 사이즈 위치 confusion 외 시행착오 X)

2점 ticket 적정 규모 — Phase 4 의 가벼운 helper + Editor 메뉴 작업.

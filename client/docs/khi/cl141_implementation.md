# CL-141 77개 RelicData SO 일괄 생성 — 구현 기록

작성일: 2026-05-04

브랜치: `S14P31C201-385-cl-141-75-itemdata-so`

기준 plan: [cl141_plan.md](cl141_plan.md), 사전 작업 plan: [`~/.claude/plans/139-plan-compiled-eclipse.md`](C:/Users/SSAFY/.claude/plans/139-plan-compiled-eclipse.md)

**상태**: 🟢 **코드 + 데이터 + 검증 완료** — Editor 스크립트 + items.csv 77 행 +
Dry-run 통과 (Validated 77, Skipped 0) + 실제 생성 통과 (77 .asset 파일) + 샘플
SO 구조 검증. **Foundation Phase 2 완성**.

---

## 목적

Epic S Phase 2 의 단일 ticket. CL-138~140 Foundation 위에 items_draft.md 의 **77개
아이템 데이터**를 RelicData SO 로 일괄 채워넣음. 이게 끝나야 CL-142~147 의 효과
시스템 구현·검증 시 실제 데이터로 동작 확인 가능.

**해결되는 문제**: 기존 18 SO (CL-138 마이그레이션 결과) 외에 items_draft.md V0.4 의
77개 컨텐츠는 종이 설계 상태. CL-142~147 가 자기 도메인 효과 구현해도 검증할 보유
풀 부족.

---

## 설계 기준

- **데이터·코드 분리**: items.csv 가 데이터 소스. Editor 스크립트는 단순 파싱 +
  SerializedObject 채우기. 데이터 수정 시 CSV 만 고치면 됨.
- **자동 매핑 회피**: 정규식 등으로 효과 텍스트 자동 파싱하지 않고 Claude 가
  사전에 결정한 EffectType+Magnitude 를 CSV 에 명시. 오매핑 위험 0.
- **기존 SO 보호**: 명명 충돌 시 신규 SO 스킵 + 경고. 기존 18 SO 마이그레이션 결과
  보호.
- **Dry-run 우선**: 실제 생성 메뉴와 별개로 Validate 메뉴 제공. 에러 발생 시
  abort (부분 생성 X).
- **CL-138 EffectEntry[] 활용**: 78 행 중 76 개가 2개 효과를 가지므로 배열 구조 필수.
  본 CL 작업으로 CL-138 의 다중 효과 설계가 처음으로 본격 사용됨.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 데이터 소스 | CSV (`_source/items.csv`) | Excel/Sheets 친화, 디자이너 편집 쉬움, git diff readable |
| CSV 형식 | 컬럼 분할 (Effect1/2 Type+Mag) | JSON 셀보다 가독성 ↑. 효과 최대 2개 (items_draft 분석상 충분) |
| enum 표기 | 이름 (e.g. AttackSpeedPercent) | 인덱스보다 robust + 오타 검출 쉬움 |
| Effect 매핑 책임 | Claude (의사결정) → CSV 명시 | 정규식 자동 파싱 위험 회피 |
| 출력 폴더 | `Generated/` 분리 | 기존 18 SO 보호 + 일괄 재생성 안전 |
| 명명 충돌 처리 | 기존 SO 우선 (스킵 + 경고) | 마이그레이션 작업 보호 |
| AttackSpeedPercent enum | 본 CL 추가 | 77개 중 10+ 아이템 사용. 빠지면 매핑 절반 무의미 |
| 전설 5개 트리거 | 매핑 가능한 부분만 + EffectDescription 텍스트 | 트리거 메커니즘은 CL-142~145·147 |
| 아이콘 처리 | null (placeholder) | 별도 ticket — CL-148 또는 디자이너 매핑 |
| Dry-run 메뉴 | 별도 추가 | 실제 생성 전 검증 + 에러 미리 잡음 |
| CSV 파싱 | RFC 4180 준수 (따옴표 안 콤마 보호) | EffectDescription 의 콤마 안전 처리 |
| 헤더 컬럼명 | `ID`/`Description` (기존 csv 따름) | 사전 작성된 csv 그대로 활용 |

---

## 수정 파일

### 신규 (3)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/Relics/RelicDataBatchGenerator.cs` | Editor 스크립트 (Validate Dry-run + Generate 2 메뉴) |
| `LostMemory/Assets/_Project/ScriptableObjects/Relics/Generated/README.md` | Generated 폴더 안내 (재생성 절차) |
| `LostMemory/Assets/_Project/ScriptableObjects/Relics/_source/items.csv` | 77 행 데이터 (사전 존재 — 그대로 활용) |

### 수정 (2)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectType.cs
    - enum 1개 추가: AttackSpeedPercent (인덱스 31, 마지막)
    - 값 추가만 (인덱스 시프트 X) → 기존 SO 직렬화 안전

LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs
    - ApplyTierEffect switch 에 case 1줄 추가:
      AttackSpeedPercent → statContainer.AddPermanent(StatId.AttackSpeed, mag, source)
    - StatId.AttackSpeed 는 CL-106 부터 존재
```

### 자동 생성 (77 .asset)

`LostMemory/Assets/_Project/ScriptableObjects/Relics/Generated/RelicData_*.asset` × 77

각 SO 에 채워진 필드:
- `_displayName` (한국어), `_isConsumable=false`, `_isInstantUse=false`
- `_rarity`, `_tagPrimary`, `_tagSecondary`, `_size`
- `_effectDescription` (CSV 의 Description)
- `_effects[]` (Effect1+Effect2 배열, 둘 다 None/0 이면 빈 배열)

채우지 않은 필드:
- `_icon = null` (별도 ticket 에서 매핑)
- `_effectTypeLegacy` 등 4개 (모두 0/None — `_effects[]` 사용)

### 재사용 (수정 X)

- [`RelicData.cs`](LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicData.cs) — CL-138 EffectEntry[] + FormerlySerializedAs legacy
- [`RelicTag.cs`](LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicTag.cs) — 16 enum 매핑
- [`BuildSetData.cs`](LostMemory/Assets/_Project/Scripts/Runtime/Relics/BuildSetData.cs) — 무관 (참고용)

---

## Editor 스크립트 핵심 기능

### CSV 파싱 안전장치

- **RFC 4180 준수**: 따옴표 안의 콤마를 필드 구분자로 취급 X. `"공속 +5%, 치명타 +5%"`
  같은 셀 안전.
- **헤더 검증**: 컬럼 수 + 이름 모두 검사. 불일치 시 line:N 에러 + abort.
- **enum 검증**: `Enum.TryParse` 로 잡고 잘못된 값 시 line + 컬럼명 + 가능한 enum 값
  리스트 출력.
- **Float invariant culture**: 로케일 영향 없이 `0.05` 파싱.
- **에러 발생 시 abort**: SO 0개 생성하고 종료 (부분 생성 X).

### 명명 충돌 정책

`AssetDatabase.FindAssets("t:RelicData")` 로 기존 RelicData 이름 수집 → 동명이면 스킵
+ Warning. 기존 18 SO 보호.

검증 결과 충돌 0 — items.csv 77 이름이 기존 18 이름 어느 것과도 안 겹침. 가장 가까운
케이스 "바람의 깃털" (#41) vs 기존 "바람 깃털" — 띄어쓰기 차이로 둘 다 생성됨.

### Effects[] 자동 채움 로직

```csharp
int effectCount = (Effect2 != None or Mag != 0) ? 2
                : (Effect1 != None or Mag != 0) ? 1
                : 0;
```

- 일반 케이스 (대부분): 2개
- 미소녀+원소 같은 단일 효과: 1개
- 전설 트리거 (#9 폭풍의 검만 해당): 0개 (빈 배열, RelicData 의 deprecated property
  가 legacy 필드 fallback 으로 None 반환 → SetEffectApplicator 에서 None case break)

---

## 검증 결과 (e2e)

### 1. Dry-run 통과 ✅
```
[Dry] Would create RelicData_노련한 검술서 (rarity=Common, tags=AttackSpeed+Critical, size=1x1, effects=[AttackSpeedPercent/0.05, CriticalChancePercent/0.05])
... (77줄)
[RelicDataBatchGenerator] Validated 77, Skipped 0 (name conflict).
```

77/77 행 모두 파싱 성공. 명명 충돌 0. enum 오타 0.

### 2. 실제 생성 통과 ✅

`Created 77, Skipped 0` — 77개 .asset 파일 Generated/ 에 정상 생성.

### 3. 샘플 SO 검증 (#1 노련한 검술서 .asset YAML)
```yaml
_displayName: 노련한 검술서
_rarity: 0          → Common ✅
_tagPrimary: 1      → AttackSpeed ✅
_tagSecondary: 2    → Critical ✅
_size: {x: 1, y: 1} ✅
_effectDescription: "공속 +5% / 치명타 +5%" ✅
_effects:
  - Type: 31, Magnitude: 0.05  → AttackSpeedPercent (CL-141 신규 enum) ✅
  - Type: 12, Magnitude: 0.05  → CriticalChancePercent ✅
_effectTypeLegacy: 0   → CL-138 마이그레이션 안전 (legacy 비움)
```

CL-138 의 `EffectEntry[]` + `FormerlySerializedAs` legacy 보존 패턴 그대로 동작.

### 4. 매핑 정확도 spot-check

| 아이템 | items_draft 텍스트 | 변환 결과 | 정확? |
|---|---|---|---|
| #1 노련한 검술서 | 공속 +5%, 치명타 +5% | AttackSpeedPercent 0.05 + CriticalChancePercent 0.05 | ✅ |
| #6 빙결 부적 | 치명타 +15%, 얼음 빙결 시간 +0.5초 | CriticalChancePercent 0.15 + FreezeOnHit 0.5 | ✅ |
| #36 시간의 종말 (전설) | 모든 스킬 쿨감 +50%, 불 도트 데미지 +100% | CooldownReductionPercent 0.5 + BurnOnHit 1 | ✅ |
| #65 별의 운명 (전설) | 행운 +7, 타로 두 배 | LuckPoints 7 + TarotProc 1 | ✅ |
| #74 전사의 폐활량 | 체력 +10%, 공속 +5% | MaxHealthPercent 0.1 + AttackSpeedPercent 0.05 | ✅ (CL-141 신규 enum 동작) |

---

## 무관한 컴파일 경고들 (CL-141 책임 외)

본 CL 컴파일 시 다음 경고 다수 발생. **모두 기존 코드** 의 issue 라 별도 ticket 에서
처리 권장.

| 경고 | 출처 | 비고 |
|---|---|---|
| `CS0618 RelicData.EffectType/Magnitude/Duration/Threshold` (RelicEffectRegistry.cs 11건) | CL-138 deprecated property | RelicEffectRegistry 가 legacy fallback 으로 사용 중 (의도). 청소는 별도 ticket |
| `CS0618 Object.FindObjectsOfType` (3건) | RewardController, RunManager, BossClearPortalController | Unity 6 신규 API 권장. 기존 코드 |
| `CS0618 Physics2D.OverlapBoxNonAlloc` (3건) | Bertha 보스, KhiMeleeHitbox | 동일 |
| `CS0618 TMP_Text.enableWordWrapping` (3건) | ShopUIBuilder | Editor only |
| `Input Manager deprecated` | Unity 6 일반 안내 | 무관 |
| `CS0414 DungeonRunBootstrap.buildRequested` | unused 필드 | 무관 |
| `CS0162 Unreachable code` (WeaponDataPlayModeIsolator) | Editor 스크립트 | 무관 |

→ Phase 3 (CL-142~) 어느 시점에 일괄 청소 ticket 권장.

---

## 후속 인계

| Ticket | 본 CL과의 관계 |
|---|---|
| **CL-142 (평타 5세트)** | Generated/ 의 검사·궁수·무도가 SO 들로 검증. AttackSpeedPercent 가 본 CL 에 추가됐으므로 즉시 동작 |
| **CL-143 (스킬 3세트)** | Generated/ 의 마법사 SO 들로 검증 |
| **CL-144~145 (미소녀)** | Generated/ 의 마법소녀 SO + #76, 77 빛/어둠 미소녀. **트리거 메커니즘** (#9 폭풍의 검, #35 폭풍의 부름, #48 합체석 등) 결정 시 EffectType=None 인 SO Tiers 보강 가능 |
| **CL-146 (공통 7세트)** | Generated/ 의 수호자·운명·교차 SO 들로 검증. **DefenseFlat 합산 정책** 결정 (CL-140 인계) |
| **CL-147 (타로)** | Generated/ 의 운명 카테고리 SO 들 (#54~65) |
| **CL-148 (인벤토리 UI)** | 77 SO 의 Icon / EffectDescription / Rarity 표시. **아이콘 매핑 ticket** 신규 필요 (현재 _icon = null) |
| **CL-152 (보상/상점 풀)** | 77 SO 를 RewardPool / ShopRotation 에 등록 |
| **CL-153 (QA)** | 77 아이템 동작 검증 |
| **데이터 수정 시** | `_source/items.csv` 만 수정 → 메뉴 재실행으로 재생성 |
| **legacy 청소 ticket** | RelicEffectRegistry CS0618 + 기타 Unity 6 deprecated API 일괄 정리 (Phase 3 어느 시점) |

## 위험 / 제약

- **트리거 효과 미구현**: 전설 #9 폭풍의 검 만 진정한 None/None. #35, #36, #48, #65 는
  부분 매핑됨 (CooldownReductionPercent, BurnOnHit, MagicalGirlSummon 등). 진짜 트리거
  메커니즘 (예: "평타 시 동시 트리거") 은 CL-142~147 시점에 별도 시스템으로 구현 필요.
- **아이콘 0 매핑**: 모든 _icon = null. CL-148 인벤토리 UI 작업 전에 디자이너가
  아이콘 폴더 정리 + CSV 에 IconPath 컬럼 추가 + 재생성 필요.
- **DefenseFlat % 합산 정책**: PlayerStatModifierContainer 가 `1 + Σ(percents)` 합산.
  Defense 의 magnitude=4 같은 flat 값을 % 로 처리하면 +400% 버그. CL-146 에서 분기
  결정 (CL-140 인계 그대로).
- **명명 충돌은 0 이지만 띄어쓰기 차이**: "바람 깃털" (기존) vs "바람의 깃털" (#41
  items_draft) 둘 다 존재. 의도된 별개 아이템인지 사용자 검수 필요.
- **DefenseFlat 의 magnitude=1, 3, 5 처리**: items.csv 에선 1.0, 3.0, 5.0 로 들어감.
  StatModifier 가 이걸 1.0+1.0=2.0 (=200%) 같이 % 로 누적하면 안 되니 CL-146 정책
  필수.

## Phase 2 완성 체크 ✅

본 CL 완료로 Phase 2 끝:
- [x] CL-138 (데이터 구조 — RelicData 듀얼 태그 + EffectEntry[] + BuildSetData)
- [x] CL-139 (BuildManager — 카운트 + 티어 + 이벤트)
- [x] CL-140 (SetEffectApplicator — 라우터 + StatModifier 8 본격 처리)
- [x] **CL-141 (77 RelicData 충원)** ← 본 CL

다음 ticket: **CL-142** (평타 5세트 본격 구현 — Critical/AttackSpeed/AttackPower stat
hook + Slow/Freeze/Chain OnHit 시스템 + BuildSet_공속/치명타/일반뎀/얼음/전기 Tiers
입력).

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| 1단계 enum + switch 1줄씩 | 5분 | 3분 |
| 2단계 items.csv 작성 | 30~40분 | 0분 (사전 존재 활용) |
| 3단계 Editor 스크립트 | 90분 | 40분 |
| 4단계 폴더 + README | 5분 | 5분 |
| 5단계 Dry-run | 15분 | 5분 |
| 6단계 실제 생성 | 5분 | 1분 |
| 7단계 Inspector 검수 | 15분 | 5분 (샘플 1개로 충분 검증) |
| **합계 (코드)** | **약 3시간** | **약 60분** |

items.csv 사전 존재 + Editor 스크립트 단순화 (자동 파싱 X, 단순 매핑) 로 plan 예상보다
훨씬 빠르게 완료.

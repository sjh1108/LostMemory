# CL-154 — V0.3 컷 (77 → 68) + 효과 수치 조정

## Context

Epic S Phase 5의 마지막 ticket. **Epic S 전체 마무리**.

CL-153 의 이슈 로그 (카테고리 B/C) 가 직접 인풋:
- **C: V0.3 컷** → 풀에서 9개 제거 (77 → 68)
- **B: 수치 조정** → 효과 magnitude 미세 조정

### items_draft V0.4 컷 후보 (확정)

> 인풋 자료: `docs/khi/items_draft.md` §6 컷 후보

| 세트 | 초과 | 컷 권장 |
|---|---|---|
| **범위** | +3 | 미소녀+범위 짝 일부 |
| **타로** | +3 | 운명 풀에서 통합 |
| **미소녀** | +3 | 미소녀+원소 짝 일부 |
| **얼음/전기** | +2 | 검사/궁수 중복 정리 |

→ **9개 컷 → 68개 (1.8× 정확히 도달)**

### 본 CL 책임 범위

1. **컷 대상 9개 확정** (CL-153 이슈 로그 + items_draft V0.4 후보 교차)
2. **컷 처리** — SO 삭제 vs Disable flag (정책 결정)
3. **수치 조정** — CL-153 카테고리 B 이슈 반영
4. **풀 재등록** — CL-152 의 메뉴 재실행
5. **회귀 audit** — 분포/태그 카운트 1.8× 유지 검증

→ **2점 ticket**, 약 2~3시간.

---

## 결정사항

### 1. 컷 처리 방식 — Disable flag (SO 삭제 X)

**옵션**:
- (a) SO 파일 삭제 (asset + meta 삭제)
- (b) **Disable flag 추가** ⭐ — `_isDisabled: bool`
- (c) RewardPool._allRewards 에서만 제외 (SO 유지)

**채택: (b)**.
- SO 삭제 시 git history / Inspector 참조 깨짐 위험
- Disable flag 는 풀에서 자동 제외 + 디자이너 복원 가능
- (c) 와 차이: flag 가 RelicData 자체에 있어 다른 곳에서도 참조 가능 (예: 디버그 메뉴)

```csharp
// RelicData.cs 추가
[Tooltip("V0.3 컷 (CL-154): 체크 시 RewardPool 등록에서 제외.")]
[SerializeField] private bool _isDisabled;

public bool IsDisabled => _isDisabled;
```

### 2. RewardPool 자동 필터링

CL-152 의 `Populate RewardPool from Folder` 메뉴 수정:

```csharp
var relics = guids
    .Select(g => AssetDatabase.LoadAssetAtPath<RelicData>(...))
    .Where(r => r != null && !r.IsDisabled)   // CL-154: Disabled 제외
    .ToArray();
```

→ 컷한 SO 가 풀에서 자동 제외. 디자이너가 disable 만 토글하면 됨.

### 3. 컷 9개 디폴트 후보 (items_draft V0.4 기반)

> 실제 SO 이름은 CL-141 의 산출물 확인 필요. 본 plan 은 items_draft 의 #번호로 표기. **CL-153 이슈 로그가 다른 우선순위 제시 시 그쪽 우선**.

| # | 이름 (items_draft) | 등급 | 짝 | 컷 사유 |
|---|---|---|---|---|
| 컷1 | 미소녀+범위 일반 짝 1 | Common | 미소녀+범위 | 범위 풀 +3 초과 |
| 컷2 | 미소녀+범위 일반 짝 2 | Common | 미소녀+범위 | 범위 풀 +3 초과 |
| 컷3 | 미소녀+범위 레어 1 | Rare | 미소녀+범위 | 범위 풀 +3 초과 |
| 컷4 | 미소녀+얼음 일반 | Common | 미소녀+얼음 | 미소녀 +3 + 얼음 +2 |
| 컷5 | 미소녀+전기 일반 | Common | 미소녀+전기 | 미소녀 +3 + 전기 +2 |
| 컷6 | 미소녀+불 레어 (중복 효과) | Rare | 미소녀+불 | 미소녀 +3 |
| 컷7 | 운명 (행운+탐욕) 일반 1 | Common | 행운+탐욕 | 타로 +3 (간접) |
| 컷8 | 운명 (탐욕+타로) 레어 | Rare | 탐욕+타로 | 타로 +3 |
| 컷9 | 운명 (행운+타로) 일반 1 | Common | 행운+타로 | 타로 +3 |

→ **CL-153 이슈 로그 카테고리 C (실 플레이 약체) 와 교차** 후 최종 9개 확정.

### 4. 등급 분포 영향

77 → 68 컷 시 등급 분포 변화 검증:

| 등급 | V0.4 (77) | V0.3 (68) 목표 |
|---|---|---|
| Common | 47 | 41 (-6) |
| Rare | 20 | 17 (-3) |
| Unique | 5 | 5 |
| Legendary | 5 | 5 |

→ 디폴트 9 컷 = Common 6 + Rare 3 + Unique 0 + Legendary 0. 분포 비율 유지 (Common 60%, Rare 25%, Unique 7%, Legendary 7% → 큰 차이 X).

### 5. 수치 조정 — CL-153 이슈 로그 인풋 의존

본 plan 작성 시점에 CL-153 결과 미정 → **수치 조정 항목 placeholder**.

**예상 조정 후보** (items_draft V0.4 + 회의록 일반 가이드):

| 효과 | 현재 | 조정안 | 사유 (예상) |
|---|---|---|---|
| 미소녀 데미지 비율 | 0.30 | 0.20~0.25 | CL-144 결정 미정, 실 플레이 OP 의심 |
| 행운 1당 가중치 | × 2 | × 1 또는 × 3 | CL-146 결정 미정 |
| 방어력 magnitude | 4 / 8 | 3 / 6 또는 5 / 10 | CL-146 정확값 미정 |
| 회피 4스택 % | 25% | 20% 또는 30% | 체감 따라 |
| 탐욕 3스택 % | 30% | 20% 또는 50% | 골드 인플레 영향 |
| 타로 발동률 magnitude | 0.05 / 0.10 | 0.03 / 0.07 | 발동 빈도 체감 |

→ **CL-153 이슈 로그가 우선**. 본 plan 항목은 CL-153 결과 없을 시의 디폴트.

### 6. 작업 순서

```
1. CL-153 이슈 로그 카테고리 C → 컷 9개 확정 (디폴트 + 이슈 인풋 교차)
2. RelicData 에 _isDisabled 필드 추가
3. 9개 SO 의 _isDisabled = true 토글
4. Populate 메뉴 재실행 → RewardPool 68개 등록
5. Audit 메뉴 → 등급/태그 분포 확인 (1.8× 유지)
6. CL-153 카테고리 B → BuildSetData / RelicData 의 magnitude 조정
7. 빠른 sanity 검증 (1런 또는 단위 회귀 일부)
```

### 7. 1.8× 도달 검증 — 태그 카운트 후 audit

CL-153 의 `Audit BuildSet Tag Distribution` 메뉴 재실행 → 컷 후 모든 16세트가 여전히 1.8× 도달하는지 확인.

| 세트 | V0.4 풀 | V0.3 목표 (1.8×) | 컷 후 예상 |
|---|---|---|---|
| 체력 | 9 | 7 | 9 (영향 X) |
| 미소녀 | 12+3 | 12 | 12 (3 컷) ✅ |
| 범위 | 12+3 | 9 | 9 (3 컷) ✅ |
| 타로 | 8+3 | 8 | 8 (3 컷 간접) ✅ |
| 얼음 | (V0.4) | 9 | -2 컷 후 7 ⚠️ 부족 가능 |
| 전기 | (V0.4) | 9 | -2 컷 후 7 ⚠️ 부족 가능 |

⚠️ **얼음/전기 컷 시 1.8× 미달 가능**. → **얼음/전기 컷은 -1 씩만** 또는 **컷 대상에서 제외** 고려.

→ 컷 9개 확정 시 audit 결과로 재조정.

---

## 핵심 파일

### 신규

(없음 — 기존 메뉴 재활용)

### 수정

| 경로 | 변경 |
|---|---|
| `RelicData.cs` | `_isDisabled` 필드 + `IsDisabled` 프로퍼티 |
| `Editor/Rewards/RewardPoolPopulateMenu.cs` (CL-152) | `IsDisabled` 필터 추가 |
| 9개 RelicData SO | `_isDisabled = true` |
| 일부 RelicData SO | magnitude 조정 (CL-153 인풋) |
| 일부 BuildSetData SO | Tier magnitude 조정 |
| `RewardPool.asset` | 메뉴 재실행 결과 (77 → 68) |

### 영향 (의존 ticket)

| Ticket | 영향 |
|---|---|
| CL-152 | Populate 메뉴 IsDisabled 필터 추가 |
| CL-150 | 무관 (Lock 와 Disabled 는 직교) |

---

## 구현 단계

### 1단계: RelicData._isDisabled 필드 (10분)

```csharp
[Tooltip("V0.3 컷 (CL-154): 체크 시 RewardPool 등록에서 제외.")]
[SerializeField] private bool _isDisabled;
public bool IsDisabled => _isDisabled;
```

### 2단계: Populate 메뉴 필터 추가 (5분)

CL-152 메뉴에 `.Where(r => !r.IsDisabled)` 한 줄.

### 3단계: 컷 9개 확정 (30분)

1. CL-153 의 `cl153_qa_findings.md` 카테고리 C 항목 확인
2. items_draft V0.4 §컷 후보 + CL-153 카테고리 C 교차
3. 9개 SO 결정 (디자이너 협의 권장)
4. 각 SO 의 `_isDisabled` 토글 (수동 Inspector)

### 4단계: Populate + Audit 재실행 (10분)

1. `Tools > Populate RewardPool from Folder` → "68개 등록" 콘솔 확인
2. `Tools > Audit RewardPool Distribution` → 등급 분포 출력
3. `Tools > Audit BuildSet Tag Distribution` → 16세트 1.8× 유지 확인
4. ⚠️ 얼음/전기 미달 시 컷 9개 재조정 → 1단계로 돌아감

### 5단계: 수치 조정 (1시간)

1. CL-153 의 카테고리 B 이슈 목록 확인
2. 각 이슈에 대해:
   - **RelicData.Effects[i].Magnitude** 조정 (Inspector)
   - 또는 **BuildSetData.Tiers[i].Magnitude** 조정
3. 변경 사항 commit 메시지에 이슈 ID 매핑

### 6단계: 회귀 sanity (30분)

- 단위 회귀 일부 (CL-153 §A 체크리스트 중 컷/조정 영향 항목만)
- 1런 sanity (보스까지 안 가도, 첫 보상 + 첫 상점만)
- audit 결과 재출력

### 7단계: 이슈 로그 closeout (15분)

`cl153_qa_findings.md` 의 카테고리 B/C 항목에 처리 결과 표기:
- "처리: CL-154 commit abc1234"
- 미처리 항목은 별도 ticket / V0.4 로 raise

---

## 위험 / 결정 미정

### 위험

1. **CL-153 미완 시점에 시작**: 본 CL 은 CL-153 의 이슈 로그가 인풋. CL-153 미완료 시 디폴트 컷 9개로 진행 → 후속 재조정 필요. → **CL-153 완료 후 시작 권장**.
2. **얼음/전기 컷 시 1.8× 미달**: §7 audit 표 참조. 컷 9개 디폴트 안에서 얼음/전기 빼는 게 안전.
3. **컷한 SO 가 다른 곳에서 참조 중**: 디버그 메뉴, 테스트 코드, 인스펙터 직접 참조 등. Disable flag 방식이라 SO 자체는 유지 → 참조는 깨지지 않음 (단 풀 등장만 X).
4. **기존 보유 유물에 컷 적용 시점**: 런 진행 중 V0.3 적용 시 기존 보유 컷 유물 처리? → **본 CL 영향 없음** (런 종료 후 적용 또는 새 런부터). 단 SaveData 호환성 별도 ticket.
5. **수치 조정 git diff**: SO asset 변경이라 PR review 가독성 떨어짐. → commit 메시지에 변경 항목 명시.
6. **Audit 메뉴 재실행 잊음**: 컷 후 메뉴 안 돌리면 RewardPool 가 77 그대로. → 본 plan §4단계 명시 + checklist.

### 결정 미정

- [ ] 컷 처리 방식 — 본 plan: **Disable flag**
- [ ] 9개 디폴트 vs CL-153 인풋 우선 — 본 plan: **CL-153 인풋 우선, 미완 시 디폴트**
- [ ] 얼음/전기 컷 포함 여부 — 본 plan: **CL-153 audit 결과 따라 -0~-2 조정 가능**
- [ ] 수치 조정 항목 — 본 plan: **CL-153 카테고리 B 의존, 디폴트 안은 §5 표**
- [ ] 회귀 검증 깊이 — 본 plan: **sanity (CL-153 §A 일부 + 1런 첫 보상)**
- [ ] CL-153 미완 시 본 CL 시작 정책 — 본 plan: **권장 X, 단 시간 압박 시 디폴트로**

---

## 후속 ticket 영향

| Ticket | CL-154 와의 관계 |
|---|---|
| **Epic S 마무리** | 본 CL 후 Epic S 모든 ticket 완료 ✅ |
| **별도 ticket: V0.4 컷 / 추가 (필요 시)** | V0.3 플레이테스트 후 다음 사이클 |
| **별도 ticket: SaveData 호환성** | 진행 중 런에 V0.3 적용 시 |
| **별도 ticket: Disabled SO 폴더 정리** | 디자이너가 _isDisabled SO 를 별도 폴더로 이동 (선택) |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (_isDisabled 필드) | 10분 |
| 2단계 (메뉴 필터) | 5분 |
| 3단계 (컷 9개 확정) | 30분 |
| 4단계 (Populate + Audit 재실행) | 10분 |
| 5단계 (수치 조정) | 1시간 |
| 6단계 (회귀 sanity) | 30분 |
| 7단계 (이슈 로그 closeout) | 15분 |
| **합계** | **약 2시간 40분** |

→ 2점 ticket 에 부합.

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 컷 처리 방식 | SO 삭제 / **Disable flag** / 풀에서만 제외 | **Disable flag** |
| 2 | 컷 9개 확정 시점 | 본 CL 시작 시 디자이너 협의 / CL-153 인풋만 | **CL-153 + 디자이너 협의** |
| 3 | 얼음/전기 컷 | 포함 (-2) / 부분 (-1) / 제외 | **audit 결과 따라** |
| 4 | 수치 조정 범위 | CL-153 항목만 / 디폴트 §5 표 | **CL-153 우선 + 디폴트 fallback** |
| 5 | 회귀 검증 | sanity / 단위 회귀 전체 | **sanity** (시간 절약) |
| 6 | 컷 SO 별도 폴더 이동 | 본 CL / 별도 ticket | **별도** |

전부 추천대로면 **Disable flag + CL-153 + audit따라 + CL-153우선 + sanity + 별도**.

---

## Phase 5 진행률 (CL-154 후)

| Ticket | Plan | Code |
|---|---|---|
| CL-152 보상/상점 풀 연결 | ✅ | - |
| CL-153 통합 QA | ✅ | - |
| **CL-154 V0.3 컷 + 수치** | ✅ ← 방금 | - |

**Phase 5: 3/3 ✅**

---

## 🎉 Epic S 전체 진행률 (CL-154 후)

| Phase | Tickets | Plan |
|---|---|---|
| Phase 1 (CL-138/139/140) | 데이터/매니저/효과 | ✅ |
| Phase 2 (CL-141) | 75 ItemData 생성 | ✅ |
| Phase 3 (CL-142~147) | 16세트 효과 | ✅ |
| Phase 4 (CL-148/150/151) | 인벤토리 UI/사이즈/배치 | ✅ (CL-149 별도) |
| Phase 5 (CL-152~154) | 통합 + QA + 컷 | ✅ |

**Epic S 빌드 시스템 plan 100% 완료** (CL-149 툴팁 제외, 클라3 별도)

---

## 다음 plan

Epic S 완료. 다음 Epic 진입:

| 옵션 | Epic | 시작 ticket | 비고 |
|---|---|---|---|
| **A** | Epic K (무기 확장) | CL-155 무기 4속성 시스템 + 인챈트 결합 | 7개 ticket, 가장 무거운 다음 작업 |
| B | Epic U (밸런스 에디터) | CL-162 EditorWindow 셸 | 5개 ticket, 디자이너 도구 |
| C | Epic M (몬스터) | CL-148/149 (다른 번호 매핑) | 클라2 영역 |

**추천: A (Epic K CL-155)** — Epic S 빌드와 직접 연결 (무기 4속성 = 빌드 인챈트 트리거). 흐름 자연스러움.

뭐로 갈까요?

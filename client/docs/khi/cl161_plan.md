# CL-161 — 무기 시스템 QA + 1차 밸런스 조정

## Context

Epic K 의 마지막 ticket. **4 트리 × 7 무기 = 28개 + 스킬 2종 + 인프라 검증**.

2점 P2, 클라1, CL-157~160 의존.

CL-153 (Epic S 통합 QA) 패턴 그대로:
- **시나리오 체크리스트** (본 plan)
- **이슈 로그** (`docs/khi/cl161_qa_findings.md` 별도 파일)
- 카테고리 분류 (즉시 수정 / 수치 조정 / 별도 ticket)

### 본 CL 책임 범위

1. **단위 회귀** — 4 트리 28개 무기, 스킬 2종, 강화 흐름, 4속성 결합
2. **DPS 실측** — 디버그 메뉴로 100타 평균 데미지 측정
3. **5런 플레이테스트** — 각 무기 트리 1런씩 + 멀티 sanity
4. **이슈 로그 정리** — 카테고리 B/C 항목 1차 수치 조정 직접 적용
5. **별도 ticket 제안** — D 카테고리 항목

→ **2점 ticket 표 적합** (수치 조정만, 큰 메커니즘 X). 단 5런 포함 시 6~8h 가능.

---

## 검증 대상 (Epic K 전체)

| Ticket | 검증 핵심 |
|---|---|
| CL-090 | WeaponData 라이브 튠 + Save Current Values |
| CL-155 | 4속성 enum + 인챈트 결합 (무기 속성 ↔ 빌드) |
| CL-156 | 강화 트리 + 상점 강화 슬롯 + Loadout |
| CL-157 | 검 트리 7개 (단검/방패검/대검 + Stage 2) |
| CL-158 | 활 트리 7개 + Projectile + KhiBowController |
| CL-159 | 봉 트리 7개 + 넉백 메커니즘 |
| CL-160 | 스태프 트리 7개 + 스킬 시스템 + 메테오/힐 |

---

## 결정사항

### 1. QA 양식 — CL-153 패턴 그대로

산출물 2개:
- 본 plan §검증 시나리오 (체크리스트)
- `docs/khi/cl161_qa_findings.md` (이슈 로그)

이슈 로그 양식:
```markdown
## #001 — 폭풍여의봉 OP (모든 면 강함)
- 영역: CL-159
- 심각도: 높음 / 중간 / 낮음
- 재현: 1) Polearm_StormRuyiBang 강화 → 2) 보스전
- 기대: 다른 Stage 2 와 비슷한 클리어 시간
- 실측: 다른 Stage 2 의 70% 시간으로 클리어
- 처리: 1차 본 CL fix (Wind magnitude 0.10 → 0.07) / 별도 ticket
```

### 2. 5런 플레이테스트 — 트리당 1런

- 런 1: 검 트리 (Sword_Default → Stage 2 까지)
- 런 2: 활 트리
- 런 3: 봉 트리
- 런 4: 스태프 트리 + 스킬 검증
- 런 5: 멀티 sanity (호스트 검 + 클라이언트 활)

각 런 30~60분.

### 3. DPS 측정 — 디버그 메뉴

```csharp
[MenuItem("Tools/LostMemory/Audit Weapon DPS (100 hits)")]
public static void AuditWeaponDps()
{
    // 1. 모든 WeaponData asset 수집
    // 2. 각 무기 100타 발동 (콤보 평균 또는 단발)
    // 3. 총 데미지 / 총 시간 = DPS
    // 4. 콘솔 출력 (등급순)
}
```

→ 무기별 DPS 객관 측정. 디자이너 직관 검증.

### 4. 무기 비교 audit — Stage 별

같은 Stage 끼리 DPS 비교:
- Stage 0: 검 / 활(노) / 봉 / 스태프 — 비슷해야 (베이스라인)
- Stage 1 9개: 적당한 분산 (단검 빠름, 대검 강함, 화염방사 OP 우려, 비숍 약함 정상)
- Stage 2 12개: Stage 1 보다 명확히 강함

→ DPS 표를 이슈 로그에 첨부.

### 5. 이슈 분류 (CL-153 패턴)

- **A. 즉시 수정** — 본 CL fix (사소한 버그)
- **B. 수치 조정** — 본 CL 에서 magnitude 슬쩍 조정 (작은 변경)
- **C. 큰 밸런스 변경** — 별도 ticket
- **D. 신규 메커니즘** — 별도 ticket

→ B 까지는 본 CL 수치 조정 (2점 범위). C/D 는 별도.

---

## 검증 시나리오

### A. 단위 회귀 (1.5시간)

#### A-1. 강화 흐름 (CL-156)

- [ ] 시작 무기 = Sword_Default 확인
- [ ] 상점 진입 → 검 트리 강화 슬롯 등장
- [ ] 골드 100 보유 시 강화 가능, 50 시 불가 (회색 / 토스트)
- [ ] 강화 후 _currentWeapon 변경 + 콤보 즉시 반영
- [ ] Stage 2 강화 후 상점 강화 슬롯 X (최종)

#### A-2. 검 트리 7개 (CL-157)

각 무기:
- [ ] Sword_Default — 평타 3타 정상
- [ ] Sword_Dagger — 데미지 7, 빠름, 짧은 사거리
- [ ] Sword_ShieldSword — 데미지 11, 균형
- [ ] Sword_Greatsword — 데미지 16, 느린 강타
- [ ] Sword_TwinDagger — 데미지 8, 더 빠름
- [ ] Sword_HolySword — 데미지 14, 균형 강화
- [ ] Sword_FireGreatsword — 데미지 18, **BurnOnHit 발동 확인**

#### A-3. 활 트리 7개 (CL-158)

- [ ] Bow_Default — 단발 화살, 마우스 방향
- [ ] Bow_Gun — 빠른 단발 (0.25s)
- [ ] Bow_Flamethrower — 산탄 3 + Spread 30° + Fire
- [ ] Bow_Grenade — 폭발 (반경 1.5)
- [ ] Bow_MachineGun — 빠른 + 약한 Spread
- [ ] Bow_Inferno — 큰 산탄 + Fire
- [ ] Bow_Rocket — 큰 폭발 + Fire

#### A-4. 봉 트리 7개 (CL-159)

- [ ] Polearm_Default — 긴 hitbox 1.2
- [ ] Polearm_RuyiBang — 가장 긴 사거리
- [ ] Polearm_Spear — 좁은 hitbox (세로 0.3)
- [ ] Polearm_Hammer — **넉백 발동 확인** (적 뒤로 밀림)
- [ ] Polearm_StormRuyiBang — Wind 광역
- [ ] Polearm_Trident — damageMultiplier 강화
- [ ] Polearm_ThunderHammer — 큰 넉백 + Lightning Chain

#### A-5. 스태프 트리 7개 (CL-160)

- [ ] Staff_Default — MagicOrb 발사 (보라)
- [ ] Staff_RapidFire — 빠른 연사
- [ ] Staff_Mage — 강한 단발 + Fire
- [ ] Staff_Bishop — 평타 평범 + 스킬 쿨감 0.7×
- [ ] Staff_Rapid — 더 빠른 + Lightning
- [ ] Staff_Archmage — 가장 강한 마법 + Fire
- [ ] Staff_HighBishop — 스킬 쿨감 0.6× + 데미지 1.3×

#### A-6. 스킬 시스템 (CL-160)

- [ ] 검 장착 시 Q/E 무반응 (allowsSkill = false)
- [ ] 스태프 장착 시 Q → 메테오 발동 (광역 30 데미지)
- [ ] 스태프 장착 시 E → 힐 발동 (HP 30 회복)
- [ ] 쿨다운 UI 표시 (Q 8s / E 15s)
- [ ] 비숍 → 메테오 쿨다운 5.6s
- [ ] 마법사 → 메테오 데미지 36
- [ ] 빌드 쿨감 3세트 + 비숍 → 추가 단축 (곱셈 검증)

#### A-7. 4속성 결합 (CL-155)

- [ ] 화염대검 + 빌드 None → BurnOnHit 1번 발동
- [ ] 화염대검 + 빌드 Fire 5세트 → BurnOnHit 2번 발동 (MVP 각각)
- [ ] 천둥망치 + 빌드 Lightning → ChainOnHit 2번
- [ ] 폭풍여의봉 + 빌드 Wind → WindAOE 2번

---

### B. DPS 실측 (1시간)

#### B-1. Audit 메뉴 실행
- [ ] `Tools > Audit Weapon DPS` 실행
- [ ] 콘솔 28개 무기 DPS 출력
- [ ] 표를 이슈 로그에 첨부

#### B-2. Stage 별 DPS 비교

| Stage | 무기 | DPS (실측) | 기대 범위 |
|---|---|---|---|
| 0 | 검 / 노 / 봉 / 스태프 | (실측) | 비슷 (10~15) |
| 1 (단발) | 단검 / 방패검 / 대검 / 총 / 유탄 / 여의봉 / 창 / 망치 / 공속/마법사/비숍 스태프 | (실측) | 18~30 |
| 1 (다발) | 화염방사 | (실측) | 명목 60, 실측 30~40 |
| 2 (단발) | Stage 2 | (실측) | 25~40 |
| 2 (다발) | 인페르노 | (실측) | 명목 100+, 실측 40~60 |

→ 명백한 OP/약체 식별.

#### B-3. 무기 vs 빌드 시너지 점검

- 화염대검 + Fire 빌드 5세트 vs 화염대검 + 빌드 X → 시너지 효과 측정
- 폭풍여의봉 + Wind 빌드 vs 단독

---

### C. 5런 플레이테스트 (각 30~60분, 총 3~5시간)

#### 런 1 — 검 트리 보스까지
- 시작 검 → 강화 (단검/방패검/대검 중 선택) → Stage 2 도달
- 보스 클리어 가능?
- 단검 vs 대검 체감 차이

#### 런 2 — 활 트리
- 화염방사 vs 유탄 체감 비교
- 원거리 무기 적응

#### 런 3 — 봉 트리
- 망치 넉백 체감
- 창 좁은 hitbox 적응

#### 런 4 — 스태프 트리 + 스킬
- 메테오 발동 빈도 (보스전 몇 번 가능?)
- 힐 의존도 (생존 영향)
- 비숍 vs 마법사 vs 공속 체감

#### 런 5 — 멀티 sanity
- 호스트 검 + 클라이언트 활
- 스킬 동기화 (메테오 위치 일치?)
- 강화 동기화

---

### D. UX/시각 (30분)

- [ ] 무기 강화 시 알림 / 토스트?
- [ ] 무기별 slashTint 구분 가능
- [ ] 스킬 쿨다운 UI 가독성
- [ ] 메테오 vfx 위치 명확
- [ ] 넉백 시각 (망치 강타 시 적이 밀리는 것 시각 확인)
- [ ] HUD 에 현재 무기 표시 (CL-156 후속에 명시 — 아직 없으면 이슈 raise)

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `docs/khi/cl161_qa_findings.md` | 이슈 로그 (양식 + 빈 항목) |
| `Assets/_Project/Scripts/Editor/QA/WeaponDpsAuditMenu.cs` | DPS audit 메뉴 |

### 수정 (이슈 로그 카테고리 B 처리 결과)

| 경로 | 변경 (예상) |
|---|---|
| 일부 WeaponData asset | _baseDamage / startupDuration / _projectileDamage 등 magnitude 미세 조정 |
| 일부 Skill SO | _baseCooldown / _baseDamage / _baseHealAmount 조정 |
| 일부 AttackStepData | knockbackForce / damageMultiplier 조정 |

### 영향 (의존 ticket)

| Ticket | 영향 |
|---|---|
| CL-157~160 | 본 CL 결과로 SO 수치 일부 변경 |

---

## 구현 단계

### 1단계: WeaponDpsAuditMenu (45분)

```csharp
public static class WeaponDpsAuditMenu
{
    [MenuItem("Tools/LostMemory/Audit Weapon DPS (100 hits)")]
    public static void Audit()
    {
        var guids = AssetDatabase.FindAssets("t:WeaponData");
        var rows = new List<(string name, float dps)>();
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>(path);

            // Melee 평균 DPS = (BaseDamage * avg(damageMultiplier)) / avg(cycle time)
            // Projectile DPS = (ProjectileDamage * ProjectilesPerShot + ExplosionDamage) / FireCooldown
            float dps = (w.AttackKind == WeaponAttackKind.Melee)
                      ? CalcMeleeDps(w)
                      : CalcProjectileDps(w);
            rows.Add((w.name, dps));
        }
        foreach (var r in rows.OrderByDescending(r => r.dps))
            Debug.Log($"  {r.name}: DPS = {r.dps:F1}");
    }

    private static float CalcMeleeDps(WeaponData w)
    {
        float totalDmg = 0, totalTime = 0;
        foreach (var step in w.Steps)
        {
            totalDmg += w.BaseDamage * step.damageMultiplier;
            totalTime += step.startupDuration + step.activeDuration + step.recoveryDuration;
        }
        return (totalTime > 0) ? totalDmg / totalTime : 0;
    }

    private static float CalcProjectileDps(WeaponData w)
    {
        float perShot = w.ProjectileDamage * w.ProjectilesPerShot + w.ExplosionDamage;
        return (w.FireCooldown > 0) ? perShot / w.FireCooldown : 0;
    }
}
```

### 2단계: A 단위 회귀 (1.5시간)

A-1 ~ A-7 체크리스트 모두 실행. 즉시 수정 (카테고리 A) fix.

### 3단계: B DPS 실측 (1시간)

audit 메뉴 → 표 → 이슈 로그.

### 4단계: C 5런 플레이테스트 (3~5시간)

각 런 30~60분.

→ **시간 압박 시**: 런 4 (스태프 + 스킬) 우선 → 런 1~3 단축. 런 5 sanity 마지막.

### 5단계: D UX 검토 (30분)

### 6단계: 이슈 로그 정리 + 1차 수치 조정 (1시간)

발견된 카테고리 B 이슈 직접 반영:
- WeaponData magnitude 슬쩍 조정
- 변경 사항 commit 메시지에 이슈 ID 매핑
- C/D 카테고리는 별도 ticket 제안

---

## 위험 / 결정 미정

### 위험

1. **시간 초과**: A+B+C+D+이슈정리 = **6~9시간**. 2점 ticket 표보다 큼. 분할 권장 (A+B+D 단독 + C 단독).
2. **DPS 계산 단순화 문제**: cycle time 만 고려, 적 에 도달 시간/적중률 무시. 명목 DPS 가 실제와 차이. → 5런 플레이테스트로 보정.
3. **5런 시간 부담**: 보스까지 30~60분. 5런 = 3~5시간. → **단축 옵션**: 보스 미도달 시점 (3 stage 클리어) 까지만.
4. **수치 조정 권한 모호**: 본 CL 에서 magnitude 슬쩍 조정 vs 별도 ticket. → **B 카테고리 (작은 변경)** 까지만 본 CL. C 이상은 별도.
5. **멀티 sanity 환경 부담**: 호스트+클라 두 인스턴스. 단순 sanity 만 (보스 미진입).
6. **CL-148 (HUD) / CL-149 (툴팁) 미완 시 영향**: 무기 HUD 표시 미존재 가능. 이슈 로그에 dependency 표시.
7. **balance 디자이너 협의**: 본 CL 의 수치 조정이 디자이너 의도 무시 가능. → 변경 전 협의 또는 변경 사항 PR 검토.

### 결정 미정

- [ ] 5런 본 CL / 분할 — 본 plan: **A+B+D 단독 + C 단독 분할 권장**
- [ ] 멀티 검증 — 본 plan: **1런 sanity**
- [ ] DPS 계산 정밀도 — 본 plan: **단순 (cycle time 만)**
- [ ] 수치 조정 권한 — 본 plan: **B 카테고리까지만 본 CL**
- [ ] 보스 미도달 시 — 본 plan: **3 stage 클리어 시점 OK**

---

## 후속 ticket 영향

| Ticket | CL-161 과의 관계 |
|---|---|
| **Epic K 마무리** | 본 CL 후 Epic K 모두 완료 ✅ |
| **별도 ticket: 큰 밸런스 변경 (C 카테고리)** | 본 CL 이슈 로그 인풋 |
| **별도 ticket: 신규 메커니즘 (D 카테고리)** | 비숍 능동 스킬 / 다중 hitbox / etc |
| **별도 ticket: 멀티 동기화 정식 QA** | sanity 외 정식 검증 |
| **별도 ticket: 시작 무기 선택 메뉴** | 4종 선택 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (DPS audit 메뉴) | 45분 |
| 2단계 (A 단위 회귀) | 1시간 30분 |
| 3단계 (B DPS 실측) | 1시간 |
| 4단계 (C 5런 플레이테스트) | 3~5시간 |
| 5단계 (D UX 검토) | 30분 |
| 6단계 (이슈 정리 + 1차 수치 조정) | 1시간 |
| **합계** | **약 7~10시간** |

→ **2점 ticket 표 초과** (CL-153 동일 패턴).

→ **분할 옵션**:
- **CL-161A (3~4시간)**: 1+2+3+5+6 = audit + 단위 회귀 + DPS + UX + 정리 (플레이 제외)
- **CL-161B (3~5시간)**: 4 = 5런 플레이테스트 + 추가 정리

ticket 단일 유지, plan 단위 분할.

---

## 작업 단위 가이드 (Plan 분할)

### 작업 단위 A — 자동화 검증 (3~4시간)
- WeaponDpsAuditMenu
- 단위 회귀 (A-1 ~ A-7)
- DPS 실측 + 비교
- UX 검토 (정적)
- 이슈 로그 초안

### 작업 단위 B — 플레이테스트 (3~5시간)
- 4 트리 1런씩 + 멀티 sanity
- 이슈 로그 보완 + 수치 조정 적용

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 5런 분할 | 본 CL 단일 / **A+B 분할** | **분할** |
| 2 | 멀티 검증 | 미포함 / **1런 sanity** | **sanity** |
| 3 | 수치 조정 권한 | A만 / **A+B 까지** / 모두 | **A+B** |
| 4 | 보스 미도달 시 | OK / 보스까지 강제 | **OK (3 stage)** |
| 5 | DPS 계산 정밀도 | **단순** / 적중률 포함 | **단순** |

전부 추천대로면 **분할 + sanity + A+B + 3stageOK + 단순**.

---

## Epic K 진행률 (CL-161 후)

| Ticket | Plan | Code |
|---|---|---|
| CL-090 무기 SO | ✅ | ✅ |
| CL-155 4속성 + 결합 | ✅ | - |
| CL-156 단계 강화 | ✅ | - |
| CL-157 검 트리 | ✅ | - |
| CL-158 활 트리 | ✅ | - |
| CL-159 봉 트리 | ✅ | - |
| CL-160 스태프 트리 + 스킬 | ✅ | - |
| **CL-161 무기 QA + 1차 밸런스** | ✅ ← 방금 | - |

**Epic K: 7/7 ✅**

---

## 🎉 Epic K 전체 완료 + 클라1 plan 진행률

| Epic | Tickets | Plan |
|---|---|---|
| **Epic S 빌드 시스템** | 17 (CL-138 ~ CL-154) | ✅ (CL-149 클라3 별도) |
| **Epic K 무기 확장** | 7 (CL-155 ~ CL-161) | ✅ |
| Epic U 밸런스 에디터 | 5 (CL-162 ~ CL-166) | ⏳ |

**클라1 plan: 24/29 (Epic S+K 100%, Epic U 0/5)**

---

## 다음 plan

Epic K 완료. Epic U 진입:

| 옵션 | Epic | 시작 ticket | 비고 |
|---|---|---|---|
| **A** | Epic U (밸런스 에디터) | CL-162 EditorWindow 셸 | 5개 ticket, 디자이너 도구 |
| B | 다른 Epic / Epic 외 ticket | - | - |

**추천: A (CL-162)** — 디자이너 친화 도구. CL-162 ~ CL-166 다섯 개로 에디터 완성. 본 ticket 들이 게임 데이터 (RelicData/WeaponData/SkillData/BuildSetData) 일괄 관리 도구.

뭐로 갈까요?

# CL-142 평타 친화 5세트 효과 — 구현 기록

작성일: 2026-05-04

브랜치: `feat/S14P31C201-386/cl-142-평타-친화-5-세트-효과`

기준 plan: [cl142_plan.md](cl142_plan.md), 사전 작업 plan: [cl142_plan_implementation.md](cl142_plan_implementation.md)
(plan mode 단일 plan 파일 제약으로 본 CL plan 은 별도 복사 보존)

**상태**: 🟢 **단위 A + B 완전 검증** — Unity Editor Play 테스트에서 5세트 모두 본격
동작 (공속 +100% / 일반뎀 +30% / 치명타 100% / 얼음 빙결 + 슬로우 / 전기 체인 3마리).
빙결+체인 placeholder 시각화 추가.

---

## 목적

Epic S Phase 3 의 첫 ticket. CL-138~141 Foundation 위에 **평타 친화 5세트 효과를
본격 적용**. CL-140 의 SetEffectApplicator 가 OnHit 3 case (Slow/Freeze/Chain) 를
LogWarning 으로만 두던 것을 본 CL 이 채우고, OnHitEffectRegistry + EnemyStatusEffect
인프라 신설.

**해결되는 문제**: CL-141 이 만든 Generated/ SO 들 중 평타 친화 5세트의 효과가 부분
적용 또는 미적용 상태:
- 공속·일반뎀: SetEffectApplicator 라우팅 작성됐지만 BuildSet Tiers 미입력
- 치명타: SetEffectApplicator case 있지만 KhiMeleeComboController 의 데미지 hook 없음
- 얼음·전기: SetEffectApplicator 가 LogWarning, 실제 적용 시스템 0

**의도된 결과**:
- 5세트 본격 동작 (공속 ×2 / 치명타 100% / 일반뎀 +30% / 얼음 슬로우+빙결 / 전기 체인 3마리)
- OnHit 인프라 (CL-143 burn/wind 가 그대로 재활용)
- 5 BuildSetData SO Tiers 본격 입력
- (계획 외) 빙결·체인 placeholder 시각화 추가

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항
- **분할 B**: 같은 ticket 단일 branch, plan 만 작업 단위 a/b 로 분리. 커밋 단위별 분리
- **치명타 ×2 고정** (StatId.CriticalDamage 별도 ticket)
- **체인 3마리** (사용자 변경 — plan 추천 1마리 → 변경): 첫 hit 위치 기준 반경 내 가장 가까운 최대 3마리 동시 데미지, 점프 메커니즘 X
- **빙결 갱신** 정책 (이미 빙결 중에 새 빙결 = 시간 교체)
- **EnemyStatusEffect 자동 부착** (Awake 에서 GetComponent, 없으면 OnHitEffectRegistry 가 첫 hit 시 AddComponent)

### 작업 중 발견·결정 사항
- **Inspector 수동 wiring 의 UX 약점 발견**: 같은 GameObject 에 OnHitEffectRegistry 컴포넌트가 있어도 SetEffectApplicator 의 SerializeField 슬롯에 자동 연결 안 됨 — 사용자가 4번이나 헷갈림. 차후 자동 wiring (Awake 에서 GetComponent fallback) 패턴 검토
- **OnEnable wiring 진단 로그 추가**: Play 진입 즉시 모든 슬롯 상태 한 줄 출력 → wiring 누락 즉시 가시화
- **URP shader fallback 패턴 학습**: `Shader.Find("Sprites/Default")` 가 URP 환경에서 null 반환 가능 → 4단계 fallback chain 도입
- **Placeholder 시각화 추가** (계획 외, 사용자 요청): 빙결 = 파란 투명 박스, 체인 = 노란 LineRenderer. 정식 VFX 는 후속 ticket

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 작업 분할 | B (a/b 단위, 같은 ticket) | plan 권장, 검증 2회로 위험 분산 |
| 치명타 배율 | ×2 고정 | MVP 단순 |
| 체인 마릿수 | **3 (사용자 변경)** | plan 추천 1마리 → 3마리. 동시 데미지, 점프 X |
| 빙결 중첩 | 갱신 (이미 빙결 중 새 빙결 = 시간 교체) | 단순 |
| 슬로우 중첩 | 가장 강한 magnitude 유지 + 시간 max | 디자이너 의도 |
| EnemyStatusEffect 부착 | OnHitEffectRegistry 가 첫 hit 시 자동 (GetOrAdd) | 적 prefab 수정 불필요 |
| 체인 무한루프 방지 | combat.TargetHit 만 구독, 체인은 Health.Damage 직접 | 자체 트리거 X |
| 얼음 Tier 3 = Slow+Freeze 동시 | 본 CL 미구현 (Tier 3 = FreezeOnHit 만) | SetTier 단일 EffectType 제약. 별도 ticket |
| 빙결 시각화 | 절차적 SpriteRenderer (파란 투명 박스) | placeholder, 정식 VFX 후속 |
| 체인 시각화 | 절차적 LineRenderer (노란 직선, 0.3초) | placeholder, 정식 VFX 후속 |
| URP shader fallback | 4단계 (Sprites/Default → URP 변형들) | URP 환경 호환 |

---

## 수정 파일

### 신규 (2)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs` | OnHit 라우터 + 체인 시각화 (LineRenderer placeholder) |
| `LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs` | 적 측 Slow/Freeze 관리 + 빙결 시각화 (SpriteRenderer placeholder) |

### 수정 (2)

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs
    - finalDamage 계산 직후 치명타 처리 5줄 추가 (line 222 부근)
      Mathf.Max(0, GetTotalMultiplier(StatId.Critical) - 1) → critChance
      UnityEngine.Random.value < critChance 시 finalDamage *= 2

LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs
    - onHitRegistry SerializeField 추가 (line 31)
    - OnEnable wiring 진단 로그 추가 (한 줄에 buildManager/statContainer/onHitRegistry 상태 표시)
    - OnHit case 본격 라우팅: SlowOnHit/FreezeOnHit/ChainOnHit → onHitRegistry.Register
    - BurnOnHit/WindAOE 만 LogWarning 유지 (CL-143)
    - RemoveTierEffect 에 onHitRegistry.UnregisterBySource 추가
```

### Unity Editor 작업 (사용자)

- 5개 BuildSetData SO Tiers 입력:
  - `BuildSet_공속` (1/3/5 → 0.10/0.50/1.0)
  - `BuildSet_치명타` (2/4/6 → 0.25/0.50/1.0)
  - `BuildSet_일반뎀` (1/3 → 0.10/0.30) — 작업 중 EffectType=None 누락 발견 → AttackPowerPercent 로 fix
  - `BuildSet_얼음` (1/2/3 → SlowOnHit 0.01/0.02 + FreezeOnHit 1.0)
  - `BuildSet_전기` (1/2/3 → ChainOnHit 0.10/0.20/0.30)
- TestKhi_MinimalCharacter2D.prefab 에 OnHitEffectRegistry 컴포넌트 부착 + 슬롯 2개 wiring (combat / statContainer)
- SetEffectApplicator 의 새 `On Hit Registry` 슬롯에 OnHitEffectRegistry 드래그

### 재사용 (수정 X)

- BuildManager (CL-139) — 무관
- KhiMeleeComboController.TargetHit 이벤트 — 구독만
- PlayerStatModifierContainer.GetTotalMultiplier — Critical multiplier 조회
- TDE CharacterMovement.MovementSpeedMultiplier — 적 이속 조작

---

## 발견·해소된 UX/UX 이슈

### 1. SerializeField 자동 wiring 부재로 인한 디버깅 사이클
**문제**: 사용자가 "OnHitEffectRegistry 컴포넌트는 같은 GameObject 에 붙였다" 고 했는데도 `[SetEffectApplicator] onHitRegistry null` 경고 다수 발화. Unity 는 같은 GameObject 에 컴포넌트가 있다고 SerializeField 슬롯에 자동 연결해주지 않음.

**해소**:
- OnEnable 진단 로그 추가 — Play 진입 즉시 wiring 상태 한 줄 출력
- 사용자가 슬롯 비어있음을 즉시 인지 가능

**향후 개선 (별도 ticket)**: `Awake` 에서 `GetComponent<OnHitEffectRegistry>()` fallback 추가 — Inspector 미설정 시 자동 wiring. 명시 설정과 자동 fallback 둘 다 지원.

### 2. URP 환경에서 `Sprites/Default` shader null 반환
**문제**: `Shader.Find("Sprites/Default")` 가 URP 프로젝트 일부 빌드에서 null 반환 → LineRenderer 가 magenta 또는 invisible 처리.

**해소**: 4단계 fallback chain 도입:
```
Sprites/Default
  → Universal Render Pipeline/2D/Sprite-Lit-Default
  → Universal Render Pipeline/Unlit
  → Unlit/Color
```

후속 ticket 에서 정식 VFX 작업 시 동일 패턴 적용.

### 3. Material 메모리 누수 위험
**문제**: 매 체인 발화마다 `new Material(shader)` 생성 → 0.3초 후 GameObject Destroy 시 정리되지만 garbage 발생.

**해소**: `_chainMaterial` static field 로 캐싱 — 첫 호출 시 1회 생성 후 모든 ChainBolt 가 sharedMaterial 로 재사용.

---

## 검증 결과 (e2e)

### 1. Wiring 진단 OK ✅
```
[SetEffectApplicator] OnEnable — wiring: buildManager=OK, statContainer=OK, onHitRegistry=OK (host=TestKhi_MinimalCharacter2D)
```

### 2. 단위 A — Stat 효과 5세트 ✅
```
[SetEffect] APPLY AttackSpeed t2: AttackSpeedPercent mag=1
[StatModifier] AddPermanent AttackSpeed +100.0% src=SetEffect(AttackSpeed, t2)

[SetEffect] APPLY AttackPower t1: AttackPowerPercent mag=0.3
[StatModifier] AddPermanent AttackPower +30.0% src=SetEffect(AttackPower, t1)

[SetEffect] APPLY Critical t2: CriticalChancePercent mag=1
[StatModifier] AddPermanent Critical +100.0% src=SetEffect(Critical, t2)
```

### 3. 단위 B — OnHit Register ✅
```
[SetEffect] APPLY Ice t2: FreezeOnHit mag=1
[OnHit] Register FreezeOnHit mag=1 src=SetEffect(Ice, t2)

[SetEffect] APPLY Lightning t2: ChainOnHit mag=0.3
[OnHit] Register ChainOnHit mag=0.3 src=SetEffect(Lightning, t2)
```

### 4. 단위 B — 평타 시 OnHit 발동 ✅
```
[OnHit] Freeze for 1s → Orc_CL037(Clone)
[OnHit] Chain 30% → 3 targets, 5.3 each
[OnHit] Freeze for 1s → SkeletonArcher_CL041(Clone)
... (다수 반복, 적 종류·체인 횟수 정상)
```

- Orc, SkeletonArcher 둘 다 EnemyStatusEffect 자동 부착 동작
- 체인 = 3마리 동시 데미지 (5.3 each — 본인 공격력 × 0.3)
- 체인 쿨다운 0.5s — 매 평타마다 freeze 는 발화하지만 chain 은 0.5s 마다 1회

### 5. 시각화 (placeholder) ✅
- 빙결: 적 위치에 파란 투명 박스 표시 → 1초 후 사라짐 → 재 hit 시 다시 표시
- 체인: 첫 hit 적에서 3마리 타깃까지 노란 직선 3개 동시 그려짐 → 0.3초 후 사라짐

### 6. 무한 루프 방지 ✅
체인 데미지로 죽은 적이 또 다른 체인 트리거 X (combat.TargetHit 만 구독, 체인은 Health.Damage 직접 호출).

---

## 위험 / 결정 미정

### 위험
1. **얼음 Tier 3 미완전 구현**: 회의록 의도 (Slow+Freeze 동시) 100% 구현 X. 본 CL 은 Tier 3 = FreezeOnHit 만. SetTier 다중 효과 구조는 별도 ticket.
2. **체인 데미지 계산 단순화**: BaseDamage × StatId.AttackPower multiplier (step.damageMultiplier 무시). 디자이너 검수 필요.
3. **빙결 시 이속 0**: TDE CharacterMovement 기준 적이 멈춤. AI 가 추격 시도하면서 못 움직이는 형태 — 검증에서는 자연스러웠음.
4. **Material 캐싱이 ApplicationDomain reload 시 dangling**: Domain reload 후 첫 chain 호출 시 새 material 생성. Unity Editor PlayMode 진입/종료 사이클에서 메모리 정리 정상 (검증됨).
5. **placeholder 시각화 일관성**: 빙결+체인만 시각화, 슬로우/치명타는 미시각화. 일관성 약간 깨짐 — 정식 VFX ticket 에서 5세트 통일 권장.

### 결정 미정 (본 CL 외)
- [ ] 슬로우 시각화 (적 발 밑 particle?)
- [ ] 치명타 시각 피드백 (데미지 텍스트 색상, 사운드, 화면 zoom/shake)
- [ ] StatId.CriticalDamage (×2 고정 외 가변) — 별도 ticket
- [ ] 체인 점프 메커니즘 (체인 → 그 적 기준 다음 체인) — 후속 확장
- [ ] SetTier 다중 효과 구조 (얼음 Tier 3 Slow+Freeze 동시) — 별도 ticket
- [ ] SetEffectApplicator 자동 wiring (Awake fallback) — 별도 ticket

## 후속 인계

| Ticket | CL-142 와의 관계 |
|---|---|
| **CL-143 (스킬 3세트: 쿨감/불/바람)** | 본 CL 의 OnHitEffectRegistry 재사용. BurnOnHit/WindAOE case 만 추가. CooldownReductionPercent stat 적용 hook 위치 결정 |
| **CL-144 (미소녀)** | 본 CL 의 EnemyStatusEffect 활용 가능 (미소녀가 슬로우 부여 등) |
| **CL-146 (공통 7세트)** | StatId.Range/Dodge/Defense 적용 hook 위치 결정 + DefenseFlat % 충돌 정책 결정 (CL-140 인계) |
| **별도 ticket — 정식 OnHit VFX** | 빙결: 얼음 결정 sprite + 적 cyan tint / 체인: lightning particle / 슬로우: 발 밑 particle / 치명타: 데미지 텍스트 + 화면 효과. 본 CL placeholder 교체 |
| **별도 ticket — SetTier 다중 효과 구조** | 얼음 Tier 3 Slow+Freeze 동시 구현 위해 SetTier 배열화 또는 enum 신설 |
| **별도 ticket — SerializeField 자동 wiring 패턴** | OnHitEffectRegistry 처럼 같은 GameObject 컴포넌트 자동 fallback. 본 CL 의 진단 로그 패턴 + Awake GetComponent fallback 표준화 |
| **CL-153 (QA)** | 본 CL 의 5세트 검증 시나리오 |

## Phase 3 진행 상태

- [x] CL-138~141 (Foundation Phase 1+2)
- [x] **CL-142 (평타 5세트)** ← 본 CL
- [ ] CL-143 (스킬 3세트)
- [ ] CL-144~145 (미소녀)
- [ ] CL-146 (공통 7세트)
- [ ] CL-147 (타로)
- [ ] CL-148 (인벤토리 UI)

## 예상 vs 실제 시간

| 단위 | 예상 (plan) | 실제 |
|---|---|---|
| A 합계 (Stat 효과) | 1.5h | 약 50분 (Inspector wiring 시간 포함) |
| B 합계 (OnHit 시스템) | 3.5h | 약 1.5h (코드) |
| **소계** | **5h** | **2.3h** |
| Wiring 디버깅 사이클 (계획 외) | - | **약 30분** (사용자 SerializeField 슬롯 누락 4회 반복) |
| 시각화 placeholder (계획 외) | - | **약 40분** (빙결 박스 + 체인 LineRenderer + URP shader fallback) |
| **총 합계** | **5h** | **약 3.5h** |

코드 자체는 plan 예상보다 빠름. 다만 사용자 wiring 디버깅 + 시각화 추가로 약 1시간 추가 — 결과물은 plan 보다 풍부 (visual placeholder 포함).

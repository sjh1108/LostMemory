# CL230 — 단검 모드 우클릭 텔레포트 + 슬래시 + 마나

## Context

단검 모드(Sword_Dagger 또는 Sword_DaggerNinja 활성) 일 때 우클릭 입력으로:
- aim 방향 짧은 거리 텔레포트 (회피기 겸 어쌔신 시그니처 무브)
- 도착 지점에 단검 1타 슬래시 표시 (시각 + 데미지 판정)
- 마나 소비 (스킬 비용)
- 짧은 무적 + 쿨다운

현재 우클릭은 KhiParryController(패링) 가 사용 → 단검 모드일 때만 우클릭이 텔레포트로 가야 함 (충돌 해결).

기존 시스템 활용:
- KhiDashController 패턴 (위치 조작 + 무적)
- PlayerMana.Consume/HasEnough (마나)
- KhiSlashAnimator (슬래시 표시)
- WeaponUpgradeService.CurrentKind (단검 모드 판정)
- KhiMeleeHitbox.Sample (히트박스 데미지)

## 결정 사항 (default — 사용자 미세 조정 가능)

| 항목 | 값 | 비고 |
|---|---|---|
| 텔레포트 거리 | **3 unit** | aim 방향 |
| 무적 시간 | **0.15초** | 대쉬와 유사 |
| 쿨다운 | **0.8초** | 대쉬보다 살짝 김 |
| 마나 코스트 | **15** | Inspector 조정 가능 |
| 슬래시 step | **1타 (Steps[0])** | 메인 + extra 슬롯 함께 |
| 데미지 | **단검 1타 hitbox 그대로** | 적 명중 시 데미지 |
| 단검 모드 패링 | **비활성** | 우클릭 = 텔레포트만 |
| 닌자 단검(F8) | **동일 동작** | 같은 텔레포트 |

모두 Inspector 노출. 사용자가 Play 중 조정 가능.

## 신규/수정 파일

### 신규 (1개)

**`Assets/_Project/Scripts/Runtime/TestKhi/KhiDaggerTeleportController.cs`**

- Update에서 우클릭 + 단검 모드 체크 + 마나 체크 + 쿨다운 체크
- aim 방향으로 transform.position 직접 변경 (텔레포트)
- 마나 Consume + 무적 (health.DamageDisabled + 코루틴 복귀)
- KhiSlashAnimator.PlaySlashFromExternal(1) 호출 (슬래시 표시)
- KhiMeleeHitbox.Sample 호출 (적 데미지 판정)
- 쿨다운 _nextAllowedAt 갱신

핵심 필드 (Inspector):
- Refs: aim, combo, hitbox, slashAnimator, mana, health
- Params: teleportDistance, invulnerabilityDuration, cooldown, manaCost, slashStepIndex

### 수정 (2개)

**`Assets/_Project/Scripts/Runtime/TestKhi/KhiSlashAnimator.cs`**
- `public void PlaySlashFromExternal(int comboStep)` 메서드 추가
- 내부에서 가짜 KhiAttackRequest 생성 + HandleAttackActiveStarted 호출
- 외부 (KhiDaggerTeleportController 등) 가 슬래시 표시 트리거 가능

**`Assets/_Project/Scripts/Runtime/Combat/WeaponUpgradeService.cs`**
- `[SerializeField] private KhiParryController swordParry` 슬롯 추가
- `ApplyWeapon` 내부에 패링 토글 추가:
  - 단검/닌자 모드 → swordParry.enabled = false
  - 검 모드 → swordParry.enabled = true

## 사용자 액션 (Unity Editor)

1. Khi prefab 모드 진입
2. 캐릭터 root GameObject 에 **KhiDaggerTeleportController 컴포넌트 부착**
3. Inspector ref 슬롯 채움 (aim, combo, hitbox, slashAnimator, mana, health 모두 같은 GameObject)
4. **WeaponUpgradeService** Inspector → **Sword Parry** 슬롯에 KhiParryController 드래그
5. Ctrl+S
6. Sword_Dagger.asset 의 1타 step 이 텔레포트 슬래시 데이터로 자동 사용됨

## 검증 (Play 모드)

1. 검 모드 → 우클릭 = 패링 (기존 그대로)
2. F9 단검 → 우클릭:
   - aim 방향 3 unit 텔레포트
   - 마나 -15 (UI 확인)
   - 도착 지점에 단검 1타 슬래시 (메인 + extra 슬롯)
   - 적 있으면 데미지
3. 마나 부족 시 → 우클릭 무시 (텔레포트 안 됨)
4. 0.8초 쿨다운 동안 우클릭 무시
5. F10 검 복귀 → 우클릭 = 패링 (다시 활성)
6. F8 닌자 단검 → 우클릭 = 텔레포트 (동일 동작)

## 핵심 파일 경로

- `Assets/_Project/Scripts/Runtime/TestKhi/KhiDaggerTeleportController.cs` (신규)
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiSlashAnimator.cs`
- `Assets/_Project/Scripts/Runtime/Combat/WeaponUpgradeService.cs`
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiDashController.cs` (참조 패턴)
- `Assets/_Project/Scripts/Runtime/Combat/PlayerMana.cs` (Consume/HasEnough API)
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeHitbox.cs` (Sample 호출)

## 후속 확장 (본 plan 범위 외)

- 텔레포트 경로의 적도 관통 데미지
- 텔레포트 잔상 (afterimage) 시각 효과
- 닌자 단검 F8 전용 다른 효과 (양쪽 슬래시)
- 텔레포트 후 추가 콤보 (3 연속 텔레포트 콤보)

## 위험 / 주의

- 우클릭 패링과의 충돌 → WeaponUpgradeService 의 swordParry.enabled 토글로 해결
- 텔레포트로 벽 너머 이동 → 후속에 raycast 막기 (Wall layer)
- 매 우클릭마다 마나 부족 시 SFX/UI 피드백 없음 → 후속 작업

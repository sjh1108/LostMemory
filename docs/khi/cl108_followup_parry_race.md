# [Followup] KhiParryController permit race condition

CL-108 검증 도중 발견된 *기존 시스템 인터리빙 race*. 본 CL 의 직접 결과는 X (KhiParryController.cs 자체는 CL-108 에서 *디버그 ContextMenu* 만 추가, state machine 로직 미수정). race 진단/fix 는 본 ticket 에서 *후속* 으로 처리.

## 증상

| 조건 | 결과 |
|---|---|
| 패링 1 회 후 발생 (재현 빈도 = 간헐적) | 영구 차단 |
| 공격 (마우스 좌클릭) | 안 됨 |
| 대시 (LShift) | 안 됨 |
| 패링 (그 후) | 가능 |
| timeScale | 1 (정상) |

→ **공격 + 대시만 영구 차단, 패링은 정상**. 자연 회복 안 됨 (시간 지나도 풀림 X).

## CL-108 검증 시점에 시도한 진단

- KhiParry state 전이 (logStateTransitions 켜고) 추적 → **모든 RestorePermits 호출 path 정상** (FailureRecovery → Cooldown, ParrySuccess → Cooldown, Cooldown → Idle)
- `Debug — Dump permit state` ContextMenu 추가 → 사용자 *재현 시점* dump 시도했으나 **재현 안 됨** (간헐적). 정상 시점 dump 결과:
  ```
  [KhiParry-DBG] state=Idle, hasCache=False,
                 cached(weapon=False, dash=False, melee=False),
                 current(weapon=True, dash=True, melee=False),
                 timeScale=1
  ```

→ KhiParry state machine 자체는 문제 없음. 외부 시스템 인터리빙 race 강력 의심.

## 의심 원인 — 외부 시스템 ↔ Cache 인터리빙

`KhiParryController.cs:304-358` 의 `CachePermitsIfNeeded` / `ApplyBlockingPermits` / `RestorePermits` 패턴:

```csharp
// 패링 진입
CachePermitsIfNeeded() {
    _cachedMeleeExternalBlock = meleeCombo.ExternalBlock;  // ← 현재 값 캐시
    _cachedDashPermitted = dashController.AbilityPermitted;
    _cachedHandleWeaponPermitted = handleWeapon.AbilityPermitted;
}
ApplyBlockingPermits() { /* 모두 차단 */ }

// 패링 성공/실패 종료 후
RestorePermits() {
    meleeCombo.ExternalBlock = _cachedMeleeExternalBlock;  // ← 캐시 시점 값으로 복원
    dashController.AbilityPermitted = _cachedDashPermitted;
    handleWeapon.AbilityPermitted = _cachedHandleWeaponPermitted;
}
```

**race 시나리오**:
1. 외부 시스템 X (예: KhiHitStunController, KhiDownController, 미확인 시스템) 가 `meleeCombo.ExternalBlock = true` 또는 `dashController.AbilityPermitted = false` 로 set
2. 그 차단 도중 사용자 패링 입력 → `CachePermitsIfNeeded` 가 *현재 차단된 값을 그대로 캐시* (cached.melee=True 또는 cached.dash=False)
3. 패링 윈도우 내 외부 시스템 X 가 자기 cleanup 실행 (정상 흐름) → ExternalBlock=false, AbilityPermitted=true
4. 패링 종료 시 `RestorePermits` → `meleeCombo.ExternalBlock = _cachedMeleeExternalBlock = true` (3번에서 풀린 것을 *다시 차단*)
5. 외부 시스템 X 는 이미 cleanup 완료 → 다시 풀어주지 않음 → **영구 차단**

## 후보 외부 시스템 (조사 대상)

| 시스템 | 차단 set 시점 | cleanup 시점 | race 가능성 |
|---|---|---|---|
| `KhiHitStunController` | hit stun 진입 시 ExternalBlock=true 가능성 | stun 종료 시 false | 높음 (stun 도중 패링) |
| `KhiDownController` | 다운 진입 시 권한 set | 부활 시 RestorePermits | 중간 (다운 도중 패링은 IsBlockedByPlayerState 로 차단되지만, 부활 직후 race 가능) |
| `KhiMeleeComboController` 자체 | `_isAttacking` 시 ExternalBlock 사용 안 함 (내부 _isAttacking 만 사용) | — | 낮음 |
| TDE 의 다른 system (Character.ConditionState 등) | ? | ? | 미조사 |

## 진단 도구 (이미 추가됨)

`KhiParryController.cs` 끝부분 (L370-394) 에 **임시 디버그 ContextMenu 2 개** 유지:

```csharp
[ContextMenu("Debug — Dump permit state")]
private void DebugDumpPermitState() { /* state, hasCache, cached, current, timeScale dump */ }

[ContextMenu("Debug — Force restore permits (panic)")]
private void DebugForceRestorePermits() { /* 모든 permit 강제 복원 */ }
```

→ 본 ticket 진행 시 그대로 활용 가능. fix 후 제거.

### 사용법

1. ▶ Play
2. race 재현 시 (공격/대시 안 되는 시점)
3. Hierarchy → `TestKhi_MinimalCharacter2D` → Inspector → `KhiParryController` ︙ → **"Debug — Dump permit state"**
4. Console 의 `[KhiParry-DBG] ...` 줄로 *어떤 변수가 잠겼는지* 식별
5. 임시 우회: ︙ → **"Debug — Force restore permits (panic)"** → 즉시 풀림

## Fix 방향 후보

진단 완료 후 다음 중 선택:

### A) RestorePermits 를 *현재 외부 상태 보존* 으로 변경
캐시된 값과 *현재 값* 중 *덜 차단된 쪽* 채택:
```csharp
private void RestorePermits()
{
    if (handleWeapon != null)
        handleWeapon.AbilityPermitted = _cachedHandleWeaponPermitted || handleWeapon.AbilityPermitted;
    // 비슷하게 다른 변수들
}
```
→ 외부 시스템이 *풀어둔* 변경 보존. 단 *외부가 새로 차단한* 경우엔 패링 cleanup 이 그것을 풀어버림 (반대 방향 race).

### B) Cache 시점 외부 시스템 *snapshot* 후 fix
*cleanup 시점에 외부 시스템에 알림* — 외부 시스템이 *자기 의도가 살아있는지* 자체 재평가. 복잡.

### C) 더 단순한 정책 — *RestorePermits 는 무조건 모두 unlock*
*외부 차단 보존* 포기. 패링 종료 시 모든 권한 unlock. 외부 시스템이 *영구 차단 의도* 면 자체 Update 에서 다시 set. 단 외부가 의존하는 *temporary 차단* 패턴 깨질 위험.

### D) 외부 시스템이 차단 set 할 때 *event 발화* → KhiParryController 가 *현재 cache 무효화*
구조적 fix. 가장 안정적. 단 외부 시스템 모두 수정 필요.

→ 진단 결과 (어떤 외부 시스템 + 어떤 변수) 따라 A/B/C/D 결정.

## 작업 단계 (제안)

1. **재현 절차 안정화** — 어떤 입력 시퀀스로 100% 재현되는지 식별 (현재는 간헐적)
2. **dump 로 잠긴 변수 + cached 값 확인** — `current(weapon=?, dash=?, melee=?)` 로 어떤 변수가 잠겼는지
3. **외부 시스템 설치 → grep/log** — `ExternalBlock = true` / `AbilityPermitted = false` set 위치 모두 찾기
4. **fix 후보 결정** (A/B/C/D 중)
5. **patch + 회귀 검증**
6. **임시 디버그 코드 제거** (`KhiParryController.cs:370-394`)

## 참고 — CL-108 검증 결과

본 race 와 무관하게 CL-108 의 8 시나리오 모두 통과. CL-108 commit 에 *임시 디버그 코드 포함* 한 채 진행. 본 ticket fix 시 제거.

## 핵심 파일

- [client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs) — race 의심 코드 (L304-358 cache/restore) + 임시 디버그 (L370-394)
- [client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiHitStunController.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiHitStunController.cs) — 외부 차단 후보
- [client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs) — 외부 차단 후보

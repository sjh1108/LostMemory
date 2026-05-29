# Down/Defeated 상태 넉백 차단 계획

## Context

`TestKhi_MinimalCharacter2D.prefab` 캐릭터가 **Down(빈사)** 또는 **Defeated(사망)** 상태일 때도 적의 접촉 데미지에 의해 계속 밀쳐지며, Defeated 시점엔 시체가 화면 밖으로 날아가는 시각적 버그가 있다. 협력 부활(R) 거리 체크가 휘청거리고, 보스전 마무리 연출이 망가진다.

원인을 추적해보면 — `EnemyContactDamage`가 데미지보다 넉백을 **먼저** 적용하고(`TryApplyContactDamage` 흐름에서 line 154 → 162), 넉백 차단 게이트는 `Health.CanGetKnockback()`(즉 `ImmuneToKnockback` 플래그)만 본다. 현재 `KhiDownController.ApplyBlockingPermits()`는 이동/공격 권한과 Rigidbody2D Kinematic 전환은 막지만 `ImmuneToKnockback`을 세팅하지 않는다. TDE `TopDownController.Impact()`는 Kinematic 상태에서도 transform 이동을 일부 우회 적용할 수 있고, FixedUpdate 폴링(`EnemyContactDamage.ScanOverlappingTargets`)이 매 프레임 호출되기 때문에 Down floor HP(=1f)인 동안 contact가 유지되면 매 cooldown마다 새 넉백이 들어온다.

목표: KhiDownController 진입/복귀 라이프사이클에 `Health.ImmuneToKnockback` 토글을 끼워, Down·Defeated 동안 외부 접촉 넉백을 깔끔하게 차단한다. TDE 원본은 건드리지 않는다.

## 핵심 파일

- `client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs` — 유일한 수정 대상
- 참고(읽기만):
  - `client/LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyContactDamage.cs:154,357` — 넉백 진입점과 게이트 (`CanGetKnockback`)
  - `client/LostMemory/Assets/TopDownEngine/Common/Scripts/Characters/Health/Health.cs:769-787` — `CanGetKnockback` 구현 (`ImmuneToKnockback` 체크)

## 변경 사항 (KhiDownController.cs)

### 1. 넉백 상태 캐시 필드 추가 (필드 선언부, ~line 136 근처)

```csharp
// Down/Defeated 진입 시 외부 넉백을 차단하기 위한 ImmuneToKnockback 캐시.
private bool _cachedImmuneToKnockback;
private bool _hasCachedImmuneToKnockback;
```

### 2. `ApplyBlockingPermits()`에 1줄 추가 (`FreezeRigidbody2D()` 호출 바로 위, line 1185 근처)

```csharp
// 외부 knockback 차단 — EnemyContactDamage 등에서 CanGetKnockback() 게이트로 막힘.
// Rigidbody2D Kinematic 전환과 이중 안전망. (Impact()가 transform 이동을 직접 쓰는 케이스도 차단)
FreezeKnockback();
FreezeRigidbody2D();
```

### 3. `RestorePermits()`에 복구 1줄 추가 (`UnfreezeRigidbody2D()` 호출 직전, line 1221 근처)

```csharp
UnfreezeKnockback();
UnfreezeRigidbody2D();
```

### 4. 헬퍼 메서드 2개 추가 (`FreezeRigidbody2D/UnfreezeRigidbody2D` 옆, line 1226~1249 근처)

```csharp
private void FreezeKnockback()
{
    if (health == null || _hasCachedImmuneToKnockback) return;
    _cachedImmuneToKnockback = health.ImmuneToKnockback;
    health.ImmuneToKnockback = true;
    _hasCachedImmuneToKnockback = true;
}

private void UnfreezeKnockback()
{
    if (health == null || !_hasCachedImmuneToKnockback) return;
    health.ImmuneToKnockback = _cachedImmuneToKnockback;
    _hasCachedImmuneToKnockback = false;
}
```

> **왜 ImmuneToKnockback 토글로 충분한가**
> `EnemyContactDamage.ApplyContactKnockback`(line 357)이 `targetHealth.CanGetKnockback(null)`로 가드한다. `Health.CanGetKnockback`은 `ImmuneToKnockback`이 true면 즉시 false 반환. 즉 데미지/넉백 호출 순서와 무관하게 넉백 경로 자체가 닫힌다.

## 의도적으로 손대지 않는 것

- `EnemyContactDamage.cs` — 적측 코드는 다른 적 프리팹에 모두 영향. 플레이어 단일 케이스 위해 호출 순서 바꾸지 않는다.
- TDE `Health.cs` — CLAUDE.md 원칙(원본 비변경) 준수.
- "빈사 = 낮은 HP" 해석(예: HP < 30%에서 넉백 감소) — 이 프로젝트의 "빈사"는 `KhiDownState.Down`이 정답. HP 임계 분기는 별도 요구사항이며 이번 범위 밖.
- `KnockbackForceMultiplier = 0` 같은 보강 — `ImmuneToKnockback` 한 줄이 같은 게이트를 차단하므로 중복. 추가하면 복구 로직만 늘어남.

## 검증

Unity 에디터에서 다음 3가지 시나리오를 실행해 확인:

1. **Down 진입 직후 넉백 차단**
   - TestKhi 씬 진입 → 적 1마리 옆에서 F5(debugForceDown)로 Down 진입
   - 적 contact 유지 → 캐릭터가 밀려나지 않고 그 자리에 멈춰 있어야 함
   - 협력 부활(R) 거리 안에서 안정적으로 progress 차오르는지 확인

2. **Down → Defeated 자동 전이 후**
   - F5 Down 진입 → `downDuration`(30s) 만료까지 적 옆 유지
   - Defeated 전이 → `defeatObjectDisableDelay`(5s) 동안 시체가 그대로 멈춰 있어야 함 (날아가지 않음)

3. **부활(Revive) 후 넉백 정상 복귀**
   - Down 상태에서 협력 부활 / F6 디버그 부활
   - 부활 후 적과 접촉 → 정상적으로 밀쳐져야 함 (regression 체크)
   - `health.ImmuneToKnockback`이 원래 값(보통 false)으로 복원됐는지 인스펙터로 확인

추가로, `EnemyContactDamage`에 `debugLogging`을 일시 켜서 Down 동안 "pushed ... but damage was skipped" 로그가 뜨더라도 캐릭터 위치 변화가 없는지 확인.

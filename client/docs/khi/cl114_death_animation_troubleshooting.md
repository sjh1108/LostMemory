# cl114 — Death 애니메이션 디버깅 기록

본 문서는 [`cl114_death_animation_fix.md`](./cl114_death_animation_fix.md) 의 진단 과정을 시간순으로 기록한 트러블슈팅 노트이다. 미래에 비슷한 Animator state 잠금 이슈가 다시 발생할 때의 길잡이.

## 증상 진화

각 단계는 직전 단계의 fix 를 적용한 후 **추가로 드러난** 증상이다.

### 1차: 무한 루프
- Dead 애니메이션이 0.58초 모션을 끝없이 반복
- 직관적으로 클립 LoopTime 문제 의심 → 확인 → 일치

### 2차: 마우스 커서가 위쪽이면 Dead 미표시, Back_Idle 표시
- 1차 fix (LoopTime=0, outgoing/CanTransitionToSelf 정리) 후 발생
- 처음에는 "Animator state machine 우선순위로 Back_Idle 의 outgoing transition 이 Down 트리거보다 먼저 평가된다" 로 잘못 추측
- 실제 원인은 다음 단계에서 밝혀짐

### 3차: Dead 잠깐 떴다가 Back_Idle 로 강제 전환
- 솔로 즉사 경로의 EnterDown 우회 fix 적용 후 Dead 가 잠깐 보이긴 함
- 하지만 곧바로 Back_Idle 로 밀려남
- Dead state 의 outgoing 은 이미 비웠는데도 발생 → **누군가 명시적으로 state 를 변경** 하고 있음

### 4차: 마우스 장시간 회전 시 결국 Idle 로
- `Animator.Play("Dead", 0, 0f)` 로 ExecuteDefeat 마지막에 1회 강제 → 즉시 Back_Idle 로 가는 케이스는 차단됨
- 하지만 시간이 흐르면서 외부 컴포넌트들이 매 프레임 Animator 를 흔들어 결국 Dead 에서 이탈

## 잘못된 가설 (기록용)

**Animator state machine 우선순위 이슈**
- 가설: Back_Idle 의 outgoing transition (`MovementY < -0.1` 등) 이 AnyState Down 트리거보다 먼저 평가되어 Dead 진입 차단
- 검증 결과: **틀림**. Unity Animator 는 AnyState transition 을 모든 state 에서 항상 평가한다. 트리거가 set 되었으면 Dead 로 즉시 전이된다.
- 진짜 원인: 솔로 즉사 경로에서 `SetTrigger("Down")` 자체가 호출되지 않음.

**Animator Layer override**
- 가설: 여러 Layer 가 있어 override 가 일어남
- 검증 결과: **틀림**. Hero_Animator 는 Base Layer 1개만 존재.

**`Animator.Rebind()` 또는 `runtimeAnimatorController =` 강제 호출**
- 가설: 어딘가에서 강제로 Animator 를 reset 하는 코드가 있음
- 검증 결과: 코드 grep 으로 플레이어용 호출은 발견되지 않음. 적 캐릭터용 [`EnemyDeathAnimationLock.cs:119`](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/EnemyDeathAnimationLock.cs:119) 의 `animator.Play(deathStateHash, 0, 0f)` 만 존재.

## 검증된 원인들

### A) 솔로 즉사 경로의 EnterDown 우회

**위치**: [`KhiDownController.cs HandleHealthHit`](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs) 라인 219-223

```csharp
if (!ResolveAllyContext())
{
    EnterDefeatedSolo();   // EnterDown 우회
    return;
}
```

`EnterDefeatedSolo()` → `ExecuteDefeat()` → `health.Kill()` 로 직행. **`SetTrigger("Down")` 호출이 없으므로** Animator 는 죽기 직전 state (Back_Idle / Front_Idle) 에 그대로 남아 있음.

증상 2차의 진짜 원인. 마우스 위 = MovementY > 0 = 죽기 직전 Back_Idle 상태였다.

**fix**: `ExecuteDefeat()` 에서 `TrySetAnimatorTrigger(downAnimatorTriggerName)` 호출 추가.

### B) `Character.Reset()` 의 Animator 파라미터 reset

**흐름**:
- `health.Kill()` 내부 (TDE Health.cs:802~803): `Character.ConditionState.ChangeState(Dead)` → `Character.Reset()`
- `Character.Reset()` 은 모든 `CharacterAbility.ResetAbility()` 호출
- 이 과정에서 Animator parameter 들 (Speed, Walking 등) 이 초기화됨
- Animator state machine 이 재평가되며 Dead 가 다른 state 로 밀려나는 케이스 발생

증상 3차의 원인. SetTrigger 기반 transition 은 다른 코드가 Animator 를 흔들면 가로채질 수 있다.

**fix**: `ExecuteDefeat()` 에서 `health.Kill()` **이후** `Animator.Play("Dead", 0, 0f)` 로 transition 우회 강제 진입.

### C) 외부 컴포넌트의 지속적 Animator parameter 갱신

**관련 컴포넌트**:
- `KhiAnimatorMovementBinder.LateUpdate()` — 마우스 기반 `MovementY` 와 velocity 기반 `Speed` 매 프레임 갱신
- TDE `Character.UpdateAnimators()` — `Alive` Bool, `CharacterMovement.UpdateAnimator()` 호출
- TDE `CharacterMovement.UpdateAnimator()` — `Speed`, `Walking` 갱신

이들은 죽음 상태를 인지하지 않고 매 프레임 파라미터를 업데이트한다. 시간이 지나면서 transition 조건이 우연히 충족되거나, 어떤 시점에 강제 state 변경이 일어나면 Dead 에서 이탈.

증상 4차의 원인.

**fix**:
1. `KhiAnimatorMovementBinder` 에 `IsDown || IsDefeated` 가드 추가 — 마우스/이동 입력 차단.
2. `KhiDownController.LateUpdate()` 에 매 프레임 Dead state 강제 잠금 추가 — 가드를 우회하는 어떤 코드도 catch.

## 핵심 학습

### Unity Animator 에서 "들어오면 못 나가는 state" 만들기

다음을 모두 충족해야 한다:

1. **Outgoing transition 모두 제거** — 정상 transition 차단
2. **AnyState→해당 state transition 의 `CanTransitionToSelf=0`** — 트리거 재발동 시 reset 방지
3. **모션 클립 `LoopTime=0`** — 시각적 freeze
4. **외부 컴포넌트들의 Animator parameter 갱신을 가드** — transition 조건 흔들기 차단
5. **외부 코드의 강제 state 변경(`Animator.Play`, `CrossFade`) 을 매 프레임 catch** — 마지막 보루

1~3 만으로는 부족하다. TDE 같은 큰 프레임워크가 자동으로 Animator 를 manipulate 하는 경우 4~5 가 필수.

### `SetTrigger` 와 `Animator.Play` 의 차이

| | SetTrigger | Animator.Play |
|---|---|---|
| 동작 | transition 조건 설정 → 다음 evaluate 에서 transition 평가 | 즉시 state 강제 변경 |
| 가로채임 | 다른 transition 이 먼저 평가될 수 있음 | 우회 거의 불가 |
| 모션 시작 시점 | TransitionDuration 후 | 즉시 |
| 트리거 reset | 자동으로 consume | reset 안 함 |

**결론**: 정상 흐름은 SetTrigger, **잠금이 필요한 경우는 Animator.Play**.

### 컴포넌트 트리 주의

`TestKhi_MinimalCharacter2D` 는 다음 구조:
- Root: `KhiDownController`, `Health`, `Character`, `CharacterMovement`
- Child `MinimalCharacterModel`: `Animator`, `KhiAnimatorMovementBinder`, `KhiSpriteFlipBinder`

`KhiAnimatorMovementBinder` 는 `GetComponentInParent<KhiDownController>()` 로 root 의 controller 를 찾는 패턴. 새 컴포넌트 추가 시 이 트리 구조를 깨면 가드가 무력화된다.

### 디버깅 시 유용한 도구

- **Unity Animator 창 라이브 미리보기** — 게임 실행 중 실제 어떤 state 에 있는지 시각 확인. 가설 검증의 1순위.
- **Animator parameter 변화 로깅** — `Debug.Log` 로 매 프레임 값 추적
- **Inspector 에서 컴포넌트 enabled 상태 확인** — GameObject / Animator 가 살아있는지

초기 진단에서 코드만 보고 추측하다가 길게 헤맸다. **Animator 창에서 실제 state 변화를 먼저 확인하면 잘못된 가설을 빨리 거를 수 있다**.

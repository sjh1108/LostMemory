# cl114 — Death 애니메이션 동작 수정

## 목적

[`cl114_testkhi_sprite_swap_plan.md`](./cl114_testkhi_sprite_swap_plan.md) 로 도입된 hero sprite/animator 풀셋의 **Death 애니메이션이 의도대로 동작하지 않는 문제**를 수정한다.

요구 동작:
- 캐릭터가 죽으면 Dead 애니메이션이 **1회만 재생**되고 마지막 프레임에서 정지
- 어떤 입력(마우스 회전 포함)에도 다른 애니메이션으로 전환되지 않음

## 발견된 버그 (시간순)

| # | 증상 | 원인 |
|---|---|---|
| 1 | Dead 애니메이션이 무한 루프 | `Hero_Front_Dead.anim` LoopTime=1 |
| 2 | Down 트리거 재발동 시 Dead 처음부터 재시작 | AnyState→Dead transition `CanTransitionToSelf=1` |
| 3 | `Revive` 트리거로 Dead → Front_Idle 자동 이탈 | Dead state outgoing transition 존재 |
| 4 | 마우스가 캐릭터 위에 있으면 Dead 미표시, Back_Idle 표시 | 솔로 즉사 경로(EnterDown 우회)에서 Down 트리거 미발동 |
| 5 | Dead 잠깐 떴다가 Back_Idle 로 강제 전환 | `health.Kill()` 내부 `Character.Reset()` 이 Animator 파라미터 reset |
| 6 | 마우스 장시간 회전 시 결국 Idle 로 흔들림 | 외부 컴포넌트들이 매 프레임 Animator parameter/state 를 manipulate |

진단 흐름과 잘못된 가설 기록은 [`cl114_death_animation_troubleshooting.md`](./cl114_death_animation_troubleshooting.md) 참조.

## 변경 사항

### 1. `LostMemory/Assets/_Project/Art/Characters/hero/Hero_Front_Dead.anim`

| 항목 | 변경 |
|---|---|
| `m_LoopTime` | `1` → `0` |

→ 모션이 마지막 프레임에서 자동 정지.

### 2. `LostMemory/Assets/_Project/Art/Characters/hero/Hero_Animator.controller`

- AnyState→Dead transition (Down 트리거): `m_CanTransitionToSelf: 1 → 0`
- Dead state 의 `m_Transitions` 비움 (`Revive` 트리거로 Front_Idle 가는 transition 참조 제거)
- `AnimatorStateTransition` 정의 블록 (`&-1212858163836198470`, `m_ConditionEvent: Revive`) 통째 삭제
- 참고: `Revive` 파라미터 자체는 유지 (다른 용도 가능성)

### 3. `LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiAnimatorMovementBinder.cs`

- `KhiDownController downController` 필드 추가, Awake 에서 `GetComponentInParent` 자동 탐색
- `LateUpdate` 시작부에 가드 추가 — `IsDown || IsDefeated` 시 즉시 return 으로 `MovementY` / `Speed` 파라미터 갱신 차단

### 4. `LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs`

- `ExecuteDefeat`: 솔로 즉사 경로(EnterDown 우회)에도 `TrySetAnimatorTrigger(downAnimatorTriggerName)` 호출 추가
- `ExecuteDefeat`: `health.Kill()` **이후** `ForcePlayDeadState()` 호출 — `Animator.Play("Dead", 0, 0f) + Update(0f)` 로 Dead state 강제 진입
- `LateUpdate`: Defeated 상태 동안 매 프레임 Dead state 잠금 — 현재 state 또는 transition 의 next state 가 Dead 가 아니면 즉시 `Animator.Play("Dead")` 로 끌어옴

## 4단 방어 구조

| 단계 | 위치 | 역할 |
|---|---|---|
| 1 | `Hero_Front_Dead.anim` `LoopTime=0` | 모션 자체가 1회 재생 후 정지 |
| 2 | `Hero_Animator.controller` Dead outgoing 없음 + `CanTransitionToSelf=0` | 정상 transition 경로 차단 |
| 3 | `KhiAnimatorMovementBinder` LateUpdate 가드 | 마우스/이동 입력으로 인한 파라미터 갱신 차단 |
| 4 | `KhiDownController.LateUpdate` Dead state 강제 잠금 | 외부 코드(`Animator.Play`, `Rebind`, parameter manipulation)에 의한 state 변경 catch |

각 단계는 **서로 다른 종류의 우회 경로**를 막는다. 단계 1~3 만으로는 부족하며, 4 단계까지 있어야 어떤 외부 컴포넌트도 Dead 를 흔들지 못한다.

## 검증

### 게임플레이
- [x] Dead 애니메이션이 처음부터 끝까지 1회 재생되고 마지막 프레임에서 정지
- [x] 마우스 커서가 캐릭터 위쪽에 있어도 Dead 정상 표시
- [x] 마우스를 죽음 직전·후로 계속 회전시켜도 Dead 유지
- [x] Dead 진입 후 어떤 트리거 / 시간이 지나도 다른 애니메이션으로 전환되지 않음

### Animator 창 (Unity Editor)
- [ ] Dead state 가 dead-end (outgoing transition 없음) 으로 표시
- [ ] Inspector 에서 AnyState→Dead transition 의 "Can Transition To Self" 가 해제되어 있음

## 영향 범위

- **TestKhi_MinimalCharacter2D.prefab** 만 직접 영향. Animator controller / 스크립트 변경이지만 prefab 자체는 수정 없음 (참조 관계 그대로).
- 다른 캐릭터 prefab (예: `TestKhi_Net_AD.prefab`) 이 동일 controller / 컴포넌트 조합을 사용한다면 자동으로 같은 동작을 받음. 다른 controller / 컴포넌트 사용 시 별도 검증 필요.
- 부활 메커니즘 영향: `Revive` 트리거로 Front_Idle 로 자동 복귀하던 경로 제거. `KhiDownController.CompleteRevive()` 가 `Revive` 트리거를 set 하더라도 Animator 측에서는 무시됨. 부활 시 Idle 로 시각 복귀가 필요하면 별도 처리 (예: `Animator.Play("Front_Idle")`) 필요.

## 관련 파일

- `LostMemory/Assets/_Project/Art/Characters/hero/Hero_Front_Dead.anim`
- `LostMemory/Assets/_Project/Art/Characters/hero/Hero_Animator.controller`
- `LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiAnimatorMovementBinder.cs`
- `LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs`

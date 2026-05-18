# CL-009 검 1~3타 기본 공격 구현 정리

작성일: 2026-04-22

대상 씬: `LostMemory/Assets/Scenes/test_khi.unity`

## 목적

CL-009의 목표는 검 1타, 2타, 3타 기본 공격과 기본 타격 판정 흐름을 `test_khi`에서 검증 가능한 상태로 만드는 것이다.

현재 구현은 최종 전투 시스템이 아니라 MVP 검증용 구현이다. 다만 이후 멀티플레이 전환 비용을 줄이기 위해 공격 요청, 방향, 판정, 피격 이벤트를 분리해 둔다.

## 현재 구현 상태

완료된 것:

- 마우스 기준 4방향 조준
- 공격 시작 순간 방향 고정
- 1타, 2타, 3타 콤보 진행
- 타별 다른 데미지 배율
- 타별 다른 판정 크기와 위치
- TDE `Health` 대상에게 데미지 적용
- 한 번의 공격 중 같은 `Health` 중복 타격 방지
- 공격 활성 프레임 동안 반투명 빨간 박스로 판정 범위 표시
- 공격 시작 시 타수/방향별 임시 슬래시 표시
- 테스트용 데미지 더미 생성 및 피격 로그 출력
- 3타 명중 시 `FinisherHit` 이벤트 발행

현재 보류한 것:

- 실제 검 휘두름 애니메이션
- 정식 공격 이펙트
- 히트스톱, 피격 플래시 고도화
- 네트워크 권한 기반 데미지 확정
- 무기 데이터 ScriptableObject화

## 핵심 파일

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerAim.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeTypes.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeHitbox.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiAttackVisualPresenter.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiDamageDummy.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiSceneBootstrap.cs
```

## 공격 흐름

1. 플레이어가 마우스 좌클릭 또는 게임패드 `buttonWest`를 누른다.
2. `KhiMeleeComboController.RequestAttack()`이 호출된다.
3. 공격 시작 순간 `KhiPlayerAim.GetCardinalDirection()`으로 방향을 확정한다.
4. `KhiAttackRequest`를 생성하고 `AttackStarted` 이벤트를 발행한다.
5. `StartupDuration` 이후 active 구간에 진입한다.
6. active 구간 동안 매 프레임 `KhiMeleeHitbox.Sample()`을 호출한다.
7. `Physics2D.OverlapBoxNonAlloc`으로 대상 후보를 찾는다.
8. 대상의 TDE `Health`에 `Health.Damage()`를 호출한다.
9. active 구간 종료 후 빨간 판정 프리뷰를 숨긴다.
10. recovery 종료 후 다음 콤보 단계로 넘어간다.

## 방향 규칙

조준은 마우스 월드 좌표와 캐릭터 위치의 차이로 계산한다.

```text
abs(x) >= abs(y) 이면 좌우
abs(y) > abs(x) 이면 상하
```

방향 값:

```text
Right
Up
Left
Down
```

중요한 규칙:

- 공격 중 방향은 바뀌지 않는다.
- 공격 시작 순간의 방향을 해당 공격이 끝날 때까지 유지한다.
- 캐릭터 모델의 좌우 뒤집힘이 판정 방향을 반대로 만들지 않도록, 판정은 로컬 스케일 반전이 아니라 명시적 방향 데이터로 계산한다.

## 기본 수치

기본 데미지:

```text
baseDamage = 10
```

타별 배율:

| 타수 | 배율 | 실제 기본 데미지 |
|---|---:|---:|
| 1타 | 1.0 | 10 |
| 2타 | 1.1 | 11 |
| 3타 | 1.5 | 15 |

타이밍:

| 타수 | Startup | Active | Recovery |
|---|---:|---:|---:|
| 1타 | 0.05 | 0.07 | 0.08 |
| 2타 | 0.06 | 0.08 | 0.11 |
| 3타 | 0.10 | 0.12 | 0.18 |

콤보 입력 유지 시간:

```text
comboInputWindow = 0.6
```

## 판정 형태

1타와 2타는 사선으로 베는 느낌을 주기 위해 방향별 offset을 살짝 다르게 둔다.

3타는 마무리 공격이므로 1타, 2타보다 넓은 범위를 가진다.

예시:

```text
1타 Right: offset (1.05, -0.2), size (1.7, 1.1)
2타 Right: offset (1.05,  0.2), size (1.8, 1.2)
3타 Right: offset (1.25,  0.0), size (2.5, 1.8)
```

상하 공격도 별도 offset과 size를 가진다. 좌우를 단순히 뒤집는 방식이 아니라 `KhiMeleeAttackStep` 안에 `Right`, `Up`, `Left`, `Down` 판정을 각각 둔다.

## 임시 슬래시 표시

`KhiAttackVisualPresenter`는 `AttackStarted` 이벤트를 받아 임시 슬래시를 표시한다.

이 슬래시는 판정이나 데미지와 완전히 분리된 시각 보강이다. 실제 타격 범위는 여전히 `KhiMeleeHitbox`의 빨간 박스와 `Physics2D.OverlapBoxNonAlloc` 판정이 담당한다.

현재는 별도 PNG 파일을 추가하지 않고, 코드에서 임시 브러시 텍스처를 생성해 `SpriteRenderer`로 표시한다. 표시 위치와 크기는 `KhiMeleeAttackStep.GetHitbox(direction)`의 center/size를 기준으로 잡는다.

타수별 임시 표시:

| 타수 | 모양 | 색상 | 표시 시간 |
|---|---|---|---:|
| 1타 | 짧고 얇은 사선 | 흰색 | 0.08 |
| 2타 | 1타 반대 방향 사선 | 노란색 | 0.09 |
| 3타 | 더 크고 넓은 마무리 슬래시 | 주황/빨강 | 0.13 |

방향 처리:

- 실제 히트박스의 offset, size를 기준으로 슬래시 표시 영역을 잡는다.
- 1타와 2타는 얇은 브러시 텍스처를 서로 반대 사선으로 표시한다.
- 3타는 넓은 브러시 텍스처를 사용해 타격 범위를 더 크게 채운다.
- 위, 왼쪽, 아래 공격은 방향 각도만큼 회전해서 표시한다.
- 캐릭터 모델 flip과 무관하게 `KhiAttackRequest.Direction`을 기준으로 표시한다.

정식 슬래시 PNG/스프라이트, 파티클, 사운드, 히트스톱은 이 문서 범위가 아니라 CL-017/CL-067에서 교체한다.

## TDE 사용 범위

유지하는 것:

- TDE `Character`
- TDE `Health`
- TDE 피격/사망 이벤트 일부

현재 비활성화하는 것:

- TDE `CharacterHandleWeapon`
- TDE 기본 `MeleeWeapon` 기반 공격 흐름

이유:

- CL-009는 방향 고정, 4방향 판정, 콤보 타이밍을 직접 제어해야 한다.
- TDE 무기 흐름을 그대로 쓰면 좌우 flip, weapon pivot, 애니메이션 이벤트에 따라 판정 방향이 꼬일 수 있다.
- 그래도 `Health.Damage()`는 유지해서 이후 적 전투, 피격 처리, 패링, 보상 트리거와 연결하기 쉽게 둔다.

## 테스트 더미

`TestKhiDamageDummy`는 런타임에 다음 요소를 보장한다.

- `SpriteRenderer`
- `BoxCollider2D`
- TDE `Health`
- 피격 시 노란색 flash
- 사망 후 짧은 시간 뒤 revive
- 콘솔 피격 로그

로그 예시:

```text
[TestKhiDamageDummy] Hit TestKhi Damage Dummy Right, health=89/100
[TestKhiDamageDummy] Hit TestKhi Damage Dummy Up, health=74/100
```

테스트 기준:

- Right, Up, Left, Down 더미를 각각 공격할 수 있다.
- 1타, 2타, 3타 데미지가 각각 10, 11, 15 수준으로 적용된다.
- 더미 체력이 0 이하로 내려가도 화면 검증을 위해 다시 revive된다.

## 네트워크 전환 고려

현재 코드는 솔로에서 즉시 실행된다.

멀티플레이 전환 시 목표 구조:

```text
클라이언트 입력
  -> 공격 요청 생성
  -> 호스트 또는 서버 권한자가 판정 실행
  -> Health.Damage 확정
  -> 결과 동기화
```

지금 단계에서 지켜야 할 것:

- 공격 판정 입력값을 `KhiAttackRequest`처럼 구조화한다.
- 공격 중 `FindObjectOfType` 기반 전역 검색을 넣지 않는다.
- 랜덤 데미지 확정은 공격 코드 안에 섞지 않는다.
- 피격 결과는 이벤트로 빼서 나중에 보상, 유물, 네트워크 동기화가 붙을 수 있게 한다.

## CL-009 완료 기준

완료로 보는 기준:

- 마우스 방향 기준으로 4방향 공격이 가능하다.
- 공격 시작 순간 방향이 고정된다.
- 캐릭터 좌우 반전과 관계없이 판정이 마우스 방향으로 나간다.
- 1타, 2타, 3타가 순서대로 진행된다.
- 3타는 1타, 2타보다 넓고 강하다.
- TDE `Health`를 가진 더미에게 실제 데미지가 들어간다.
- 빨간 박스로 현재 판정 범위를 확인할 수 있다.
- 임시 슬래시로 공격 방향과 타수를 구분할 수 있다.
- 같은 공격 active 구간에서 같은 대상이 중복 타격되지 않는다.

위 기준은 현재 테스트 로그 기준으로 충족했다.

## 다음 작업

CL-009에서 바로 이어서 하면 좋은 작업:

1. CL-010 공격 연계 입력 버퍼 구현
2. CL-017 히트스톱/피격 플래시
3. CL-067 실제 전투 이펙트 연결

현재 우선순위:

```text
입력 버퍼는 CL-010 본 작업
실제 이펙트와 히트 피드백은 CL-017/CL-067
```

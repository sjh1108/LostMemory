# CL-053 보스 체력 단계별 패턴 전환 처리 구현 계획

## 문서 목적

`CL-053 보스 체력 단계별 패턴 전환 처리 구현`의 작업 범위와 구현 순서를 정리한다.

CL-051, CL-052에서 Bertha 보스의 기본 전투 루프, 근접 범위 공격, 돌진, 풀콤보, 투사체, 피격 반응, 체력 임계치 연출 일부가 이미 들어갔다. CL-053은 이 구현을 바탕으로 체력 비율에 따라 보스의 전투 페이즈가 명확하게 바뀌고, 각 페이즈에서 사용할 패턴과 연출이 안정적으로 전환되도록 정리하는 작업이다.

## 대상 스토리

| Jira ID | 범위 | 담당 |
|---|---|---|
| CL-053 | 보스 체력 단계별 패턴 전환 처리 구현 | 클라2 |

## 현재 기준 정리

현재 코드베이스에는 다음 기반이 존재한다.

- `BerthaCombatPatternSelector`
  - `LightAttack1`, `LightAttack2`, `HeavyAttack`, `NormalDash`, `DashAttack`, `FullCombo` 선택 로직을 가진다.
  - `specialUnlockHealthThresholdNormalized` 이하에서 `DashAttack`, `FullCombo` 같은 특수 패턴을 열 수 있다.
  - 거리, 쿨다운, 가중치 기반 선택을 수행한다.
- `BerthaHealthThresholdReactionController`
  - 체력 `70% 이하`에서 `Stun` 계열 연출을 큐잉한다.
  - 체력 `30% 이하`에서 `Tired` 연출을 큐잉한다.
  - 임계치 연출 중 보스 AI와 이동을 잠시 멈춘 뒤 복귀시킨다.
- `BerthaLightAttack1Bootstrap`
  - Bertha 전투 상태기, 패턴 선택기, 투사체 버스트, 체력 임계치 반응을 한 번에 구성한다.
  - Inspector 값으로 체력, 패턴 거리, 쿨다운, 가중치, 임계치 연출 시간을 조절할 수 있다.

따라서 CL-053은 새 보스를 처음부터 만드는 작업이 아니라, 이미 있는 패턴 선택과 임계치 연출을 명확한 페이즈 모델로 정리하는 작업으로 본다.

## 구현 목표

### 1. 체력 구간을 명확한 페이즈로 정의한다

권장 페이즈는 3단계다.

| 페이즈 | 체력 조건 | 전투 성격 |
|---|---|---|
| Phase 1 | 100% 초과 없음, 70% 초과 | 기본 패턴 중심 |
| Phase 2 | 70% 이하, 30% 초과 | 특수 패턴 해금, 공격 빈도 상승 |
| Phase 3 | 30% 이하 | Tired 연출 후 고위험 패턴 비중 증가 |

초기 구현에서는 페이즈 수를 늘리지 않는다. 70%, 30% 기준이 이미 `Stun`, `Tired` 연출과 맞물려 있으므로 이 값을 그대로 사용한다.

### 2. 임계치 연출과 페이즈 전환을 분리한다

`Stun`, `Tired`는 페이즈 전환의 시각적 연출이고, 실제 전투 규칙 변경은 별도 페이즈 상태가 담당해야 한다.

권장 책임 분리:

| 책임 | 담당 |
|---|---|
| 체력 비율 감지 | Phase controller |
| Stun/Tired 연출 | `BerthaHealthThresholdReactionController` |
| 패턴 선택 규칙 | `BerthaCombatPatternSelector` |
| 프리팹 기본값 주입 | `BerthaLightAttack1Bootstrap` |

임계치 연출 중에는 기존처럼 AI를 멈추고, 연출이 끝난 뒤 새 페이즈 규칙으로 전투를 재개한다.

### 3. 페이즈별 패턴 테이블을 둔다

현재 `BerthaCombatPatternSelector`는 단일 가중치 세트와 `specialUnlockHealthThresholdNormalized`만 갖고 있다. CL-053에서는 페이즈별 가중치와 쿨다운을 분리한다.

권장 기본값:

| 항목 | Phase 1 | Phase 2 | Phase 3 |
|---|---:|---:|---:|
| LightAttack1Weight | 3 | 2 | 1 |
| LightAttack2Weight | 3 | 3 | 2 |
| HeavyAttackWeight | 2 | 3 | 3 |
| NormalDashWeight | 1 | 2 | 2 |
| DashAttack 사용 | Off | On | On |
| FullCombo 사용 | Off | On | On |
| NormalDashCooldown | 10s | 8s | 6s |
| SpecialPatternCooldown | 15s | 12s | 9s |

이 값은 확정 밸런스가 아니라 테스트 시작값이다. 중요한 것은 코드 수정 없이 Inspector에서 조정 가능하게 두는 것이다.

### 4. 패턴 전환은 현재 진행 중인 공격을 끊지 않는다

체력이 임계치를 넘었다고 해서 공격 중인 상태를 즉시 중단하면 전투가 부자연스럽다.

권장 흐름:

```text
Health hit
  -> phase threshold reached
  -> current attack finishes or threshold reaction takes priority
  -> Stun/Tired reaction plays once
  -> AI returns to Detecting/Moving
  -> next pattern selection uses new phase rules
```

단, 임계치 연출은 예외적으로 현재 행동을 중단해도 된다. 이미 `BerthaHealthThresholdReactionController`가 AI 정지와 Freeze를 처리하고 있으므로 이 구조를 재사용한다.

## 범위 정의

### CL-053 MVP

- Bertha 체력 비율을 3단계 페이즈로 계산한다.
- 페이즈 변경 이벤트 또는 내부 상태를 추가한다.
- `BerthaCombatPatternSelector`가 현재 페이즈에 따라 패턴 가중치, 특수 패턴 허용 여부, 쿨다운을 다르게 사용한다.
- 기존 `Stun`, `Tired` 임계치 연출과 페이즈 전환 타이밍이 충돌하지 않게 정리한다.
- `BerthaLightAttack1Bootstrap`에서 페이즈별 기본값을 주입할 수 있게 한다.
- `BossTest` 또는 `BossArea_Test`에서 체력 70%, 30% 구간을 수동 검증한다.

### CL-053 Stretch

- `돌진후 내려찍기`를 독립 조합 패턴으로 추가한다.
- Phase 3에서 `돌진후 내려찍기` 또는 `FullCombo` 우선도를 높인다.
- 보스 체력바 UI에 페이즈 전환 피드백을 연결한다.
- 페이즈 전환 시 화면 흔들림, 사운드, VFX를 추가한다.

### 이번 문서 범위 밖

- 보스 처치 후 보상 및 출구 연결
- 보스 체력바 최종 디자인 완성
- 네트워크 동기화
- 신규 보스 추가
- TopDown Engine 원본 코드 수정

## 추천 구현 구조

### 신규 또는 확장 후보

```text
Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/
  BerthaBossPhase.cs
  BerthaBossPhaseController.cs
  BerthaCombatPatternSelector.cs
  BerthaLightAttack1Bootstrap.cs
  BerthaHealthThresholdReactionController.cs
```

### BerthaBossPhase

단순 enum으로 시작한다.

```csharp
public enum BerthaBossPhase
{
    Phase1 = 1,
    Phase2 = 2,
    Phase3 = 3
}
```

### BerthaBossPhaseController

권장 책임:

- `Health`를 참조한다.
- 현재 체력 비율을 계산한다.
- 70%, 30% 기준으로 현재 페이즈를 갱신한다.
- 페이즈가 바뀔 때 이벤트를 발행한다.
- 사망 상태에서는 전환을 중단한다.

권장 이벤트:

```csharp
public event System.Action<BerthaBossPhase, BerthaBossPhase> PhaseChanged;
```

이벤트 인자는 `previousPhase`, `currentPhase` 순서로 둔다.

### BerthaCombatPatternSelector 확장

현재 내부 `PatternType`은 유지한다. 먼저 selector를 크게 갈아엎지 말고, 페이즈별 설정만 추가한다.

권장 추가 구조:

```csharp
[System.Serializable]
private struct PhasePatternSettings
{
    public BerthaBossPhase Phase;
    public bool AllowDashAttack;
    public bool AllowFullCombo;
    public float NormalDashCooldown;
    public float DashCooldown;
    public float FullComboCooldown;
    public int LightAttack1Weight;
    public int LightAttack2Weight;
    public int HeavyAttackWeight;
    public int NormalDashWeight;
}
```

선택 로직은 다음 순서로 둔다.

1. 현재 페이즈 설정 조회
2. 특수 패턴 허용 여부 확인
3. 거리와 쿨다운 확인
4. 기본 패턴 풀 생성
5. 이전 기본 패턴 반복 방지
6. 선택된 패턴의 쿨다운 예약

### BerthaHealthThresholdReactionController 연동

이 클래스는 이미 체력 임계치 연출을 담당하고 있으므로 CL-053에서 과도하게 수정하지 않는다.

필요한 보강:

- `PhaseController`와 같은 임계치 값을 쓰도록 Bootstrap에서 같은 값을 주입한다.
- 연출 중 selector가 새 패턴을 고르지 않도록 기존 `brain.BrainActive = false` 구조를 유지한다.
- 연출 종료 후 `Detecting` 또는 `Moving`으로 복귀했을 때 selector가 새 페이즈 설정을 읽도록 한다.

## 구현 순서

1. `BerthaBossPhase` enum 추가
2. `BerthaBossPhaseController` 추가
3. Phase controller가 `Health.OnHit`, `Health.OnDeath` 또는 주기적 확인으로 페이즈를 갱신하게 구현
4. `BerthaCombatPatternSelector`에 현재 페이즈 참조 추가
5. selector에 `PhasePatternSettings` 배열 추가
6. 기존 단일 가중치/쿨다운 값은 fallback으로 유지
7. `BerthaLightAttack1Bootstrap`에서 phase controller를 생성 또는 참조하고 기본 설정을 주입
8. `BerthaHealthThresholdReactionController`와 임계치 값이 어긋나지 않는지 확인
9. `BossTest`에서 100% -> 70% -> 30% -> 사망 흐름 수동 검증

## 테스트 체크리스트

- Phase 1에서는 기본 패턴 중심으로 동작하는가
- 70% 이하 진입 시 `Stun` 연출이 한 번만 재생되는가
- `Stun` 연출 중 AI, 이동, 공격이 멈추는가
- `Stun` 종료 후 Phase 2 규칙으로 패턴이 선택되는가
- Phase 2에서 `DashAttack`, `FullCombo`가 허용되는가
- 30% 이하 진입 시 `Tired` 연출이 한 번만 재생되는가
- `Tired` 종료 후 Phase 3 규칙으로 패턴 빈도와 쿨다운이 바뀌는가
- 체력이 한 번에 70%와 30%를 모두 넘을 때 연출 큐가 꼬이지 않는가
- 임계치 연출 중 사망하면 연출 코루틴이 중단되는가
- 사망 후 selector가 더 이상 패턴을 선택하지 않는가
- `BossHealthBarView`가 배치되어 있다면 체력 변화와 사망 시 hide 동작이 정상인가

## 예상 리스크와 대응

### 1. 체력이 한 번에 많이 깎일 때 임계치 연출이 중복될 수 있음

대응:

- 이미 `BerthaHealthThresholdReactionController`에 `_stunQueuedOrPlayed`, `_tiredQueuedOrPlayed` 플래그가 있다.
- Phase controller도 동일하게 이전 페이즈와 현재 페이즈를 비교해 한 번만 이벤트를 발행한다.

### 2. 임계치 연출과 패턴 선택기가 동시에 AI 상태를 바꿀 수 있음

대응:

- 연출 중에는 `brain.BrainActive = false`를 유지한다.
- selector는 `Moving` 상태일 때만 패턴을 고르므로, 연출 중에는 동작하지 않게 한다.

### 3. selector를 크게 갈아엎으면 기존 CL-051/052 패턴이 깨질 수 있음

대응:

- 기존 `PatternType`, 상태명, 거리 조건은 유지한다.
- 페이즈별 설정이 없으면 기존 단일 값으로 fallback한다.
- 패턴별 controller와 projectile driver는 수정하지 않는다.

### 4. 페이즈별 밸런스 값이 너무 공격적일 수 있음

대응:

- 모든 수치는 Inspector 조정 가능하게 둔다.
- CL-053에서는 최종 밸런스보다 전환 구조 완성을 우선한다.

## 완료 기준

CL-053은 아래 조건을 만족하면 완료로 본다.

1. Bertha가 체력 비율에 따라 Phase 1, Phase 2, Phase 3 상태를 가진다.
2. 70% 이하 진입 시 Phase 2로 전환된다.
3. 30% 이하 진입 시 Phase 3으로 전환된다.
4. Phase 2, Phase 3에서 패턴 가중치와 쿨다운이 Phase 1과 다르게 적용된다.
5. `Stun`, `Tired` 임계치 연출이 페이즈 전환과 충돌하지 않는다.
6. 임계치 연출은 각 임계치마다 한 번만 재생된다.
7. 보스 사망 후 페이즈 전환, 연출, 패턴 선택이 중단된다.
8. Unity Play Mode에서 70%, 30%, 사망 흐름을 수동 검증했다.

## 후속 연결

- CL-054: 보스 처치 확정, 보스방 클리어 처리, 출구/보상 연결
- UI 작업: 보스 체력바 배치, 페이즈 전환 시 체력바 연출 추가
- 밸런스 작업: 페이즈별 쿨다운, 가중치, 대미지, 투사체 속도 조정

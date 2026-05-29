# SkeletonArcher Telegraph — 게스트 화면 미표시 진단 & 수정

날짜: 2026-05-24
저장 위치 정책: `client/docs/khi/` (사용자 feedback 메모리 — 다른 환경에서 찾을 수 있어야 함).

## Context

게스트 화면에서 `SkeletonArcher_CL041` 의 공격 예고 장판(빨간 직사각형)이 보이지 않거나 한 프레임만 깜빡이는 것으로 추정. 본 문서는 그 원인 가설을 코드 근거로 정리하고 최소 수정 plan 을 제시한다.

상위 시스템(MonsterAttackBroadcast → ClientRpc → AttackTelegraph2DView clone) 자체는 정상 동작 중이며 다른 enemy 들의 telegraph 는 게스트에 잘 보임 (Bertha, OrcRider, SkeletonElite, StoneGolem, Chobomb, SkeletonMage AOE 등). Archer 만 특이 패턴.

전수조사 결과:
- `SkeletonArcher_CL041.prefab` 에 `AttackTelegraph2DView` (root 자식) + `MonsterAttackBroadcast` (root) 모두 부착 — sync 구조 자체는 정상.
- `AIBrainRangedTelegraphDriver` 가 Brain state-machine 의 `AimReady` 상태 동안 `telegraphView.Refresh(request)` 를 매 프레임 호출하는 driver 패턴 사용.

## 원인 가설

[AIBrainRangedTelegraphDriver.cs:186-201](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/Telegraph/AIBrainRangedTelegraphDriver.cs:186) 의 `CreateRequest()` 가 `Duration = 0f` 를 명시 전달한다.

흐름:
1. Brain 이 `AimReady` 진입 → `OnMMEvent` → `telegraphView.Show(request)` (Duration=0) — 최초 1회만 발화.
2. `AttackTelegraph2DView.Show()` 내부에서 `GetComponentInParent<MonsterAttackBroadcast>()` 자동 호출 → `BroadcastTelegraph(... warningDuration=0, impactHold=0, color)` 발화 (Show 메서드 line 134).
3. `MonsterAttackBroadcast.BroadcastTelegraphClientRpc` → 게스트 측 `SpawnVisualClone`.
4. `SpawnVisualClone` 에서 [line 136](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/Telegraph/MonsterAttackBroadcast.cs:136): `float effectiveWarning = Mathf.Max(0.01f, warningDuration)` → **0.01초로 클램프**.
5. 게스트 측 clone view 가 `Duration=0.01` 로 `Show` 호출 → `_remainingDuration = 0.01` → 한 프레임 후 `HideImmediate`.
6. 추가로 [line 151](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/Telegraph/MonsterAttackBroadcast.cs:151): `Destroy(visualGo, totalLifetime)` 의 totalLifetime = 0.01초 → GameObject 즉시 파괴.

호스트 측에서는 driver 의 `Update()` 가 매 프레임 `Refresh(request)` 호출해서 sprite 가 유지됨. `Refresh` 는 visible 인 경우 `ApplyRequest` 만 호출하고 broadcast 는 호출하지 않으므로 (`AttackTelegraph2DView.cs:138-149`) **게스트는 1회만 받음 → 즉시 사라짐**.

## 검증 절차 (수정 전 1회만)

1. 호스트로 SkeletonArcher 가 있는 씬 진입.
2. 호스트/게스트 콘솔 양쪽에서 다음 로그 필터:
   - `[DiagTelegraph-Show]` — driver 가 Show 호출 시 (호스트만).
   - `[DiagTelegraph-Spawn]` — broadcast 발화 시 (호스트만). `warning=0.00` 으로 찍히는지 확인 (가설 확인 포인트).
   - `[DiagTelegraph-RpcRecv]` — 게스트가 ClientRpc 도착 (게스트만).
3. 가설 충족 조건:
   - 호스트 Spawn 로그에 `warning=0.00`
   - 게스트 RpcRecv 도착함 (= 통로 자체는 살아있음)
   - 게스트 화면에는 한 프레임 깜빡이거나 아예 안 보임

## 수정안

driver 가 Show 시점에 **실제 telegraph 지속시간을 명시 전달** 하도록 수정. driver 가 매 프레임 Refresh 로 유지하는 호스트 측 동작은 그대로 두되, broadcast 1회의 Duration 만 의미 있게 채운다.

수정 대상: `LostMemory/Assets/_Project/Scripts/Runtime/Combat/Telegraph/AIBrainRangedTelegraphDriver.cs`

### 변경 핵심

- inspector 필드 추가: `[SerializeField] private float aimStateExpectedDuration = 1.0f;` — `AimReady` state 의 평균 체류 시간. 기본값은 archer Brain 에서 추정. SerializeField 라 prefab 별 튜닝 가능.
- `CreateRequest(...)` 의 `Duration = 0f` 를 `Duration = aimStateExpectedDuration` 으로 교체 (broadcast 1회의 게스트 측 visual 유지 시간 = AimReady 평균 길이).
- driver 가 `exitingState == telegraphStateName` 시 호스트의 `HideTelegraph()` 는 그대로 — 호스트는 즉시 사라짐. 게스트는 명시한 Duration 후 자체 만료.
- 게스트가 Duration 끝나기 전에 archer 가 죽거나 state 가 바뀌어도 시각이 잠시 남는 trade-off 는 수용 (가독성 < 정확성: 0.x초 오차).

### 대안 (선택 — 정확도 필요 시)

driver 에 별도 `BroadcastHide` 호출을 만들어 exit 시 게스트에도 즉시 hide 신호 보낼 수 있음. 단 `MonsterAttackBroadcast` 가 SpawnVisualClone 으로 새 GameObject 를 매번 생성하는 구조라 hide 신호를 보내려면 clone 추적 시스템(id 기반) 이 필요해 비용 큼. **본 수정 사이클에서는 권장 안 함.** AimReady 평균 길이로 Duration 만 채우는 1점 수정으로 충분.

## Verification

수정 후 동일 씬에서:
1. 호스트 콘솔 `[DiagTelegraph-Spawn]` 의 `warning=` 값이 `aimStateExpectedDuration` 값으로 찍히는지.
2. 게스트 화면에서 archer 의 빨간 직사각형 telegraph 가 호스트와 거의 동일한 시간 동안 표시되는지 (인지 가능한 ~0.5초 이상).
3. archer 가 telegraph 도중 죽거나 target 을 잃어도 호스트는 즉시 사라지고 게스트는 최대 `aimStateExpectedDuration` 까지만 잔상.
4. 다른 enemy (Bertha, SkeletonElite) 의 telegraph 표시에 회귀 없는지 (변경은 archer driver 단일 파일이라 회귀 가능성 낮음).

## 후속 — 동일 패턴 enemy 점검

`AIBrainRangedTelegraphDriver` 사용 prefab 또는 `Refresh(Duration=0f)` 패턴 사용 driver 가 다른 enemy 에 있는지 sanity grep:

```
grep -rn "AIBrainRangedTelegraphDriver" LostMemory/Assets/_Project/Prefabs/Enemies/
grep -rn "Duration = 0f" LostMemory/Assets/_Project/Scripts/Runtime/Combat/Telegraph/
```

발견 시 동일 패턴으로 수정. 단, dash driver (`AIBrainDashTelegraphDriver`) 는 별도 검증 필요 (구조 다를 수 있음).

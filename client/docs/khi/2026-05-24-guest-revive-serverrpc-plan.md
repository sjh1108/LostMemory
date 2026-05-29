# 게스트 부활 ServerRpc 위임 + host 측 거리 재검증

> 참고: 사용자 memory rule 에 따라 시연 후 이 파일은 `client/docs/khi/2026-05-24-guest-revive-serverrpc-plan.md` 로 이동/복사 권장.

## Context

현재 cooperative revive (1.5m R hold 2초) 는 **호스트가 누를 때만 정상 sync**. 게스트가 누르면 자기 화면에서만 부활 — NGO server-authoritative 한계 때문에 `Health.SetHealth` 가 host 의 `PlayerHealthSync` NetworkVariable 까지 도달 못 함.

**최종 원하는 동작**: 모든 팀원 ↔ 모든 팀원 서로 부활 가능. 자기 자신은 부활 불가.

| 누가 살림 | 누가 죽음 | 현재 | 변경 후 |
|---|---|---|---|
| Host | Guest1/2 | ✓ | ✓ |
| Guest1 | Host | ✗ (자기 화면만) | ✓ |
| Guest1 | Guest2 | ✗ | ✓ |
| Guest2 | Host/Guest1 | ✗ | ✓ |

**해결**: 게스트 측 차징 완료 시 `target.ForceRevive()` 직접 호출 대신 **ServerRpc 발사 → host 가 권위로 ForceRevive** → NetworkVariable sync → 모든 client 정상 부활.

**게임 자체 관점에서 host 측 거리 재검증 필수** — 클라이언트 메모리 변조로 맵 반대편에서 무한 부활 가능성 차단. lag 보정 위해 client distance 의 2배 (3.0m) tolerance.

## 변경 파일

### 1. `Assets/_Project/Scripts/Runtime/Networking/Player/PlayerMovementSync.cs` — ServerRpc 1개 추가

기존 `RequestBossStartServerRpc` 와 같은 위치/스타일로 `RequestCooperativeReviveServerRpc` 추가. **RequireOwnership=true (default)** — reviver 가 자기 자신의 PlayerMovementSync 에 호출하므로 `this.transform.position` 이 reviver 위치.

```csharp
[ServerRpc]
public void RequestCooperativeReviveServerRpc(ulong targetNetObjId)
{
    var nm = Unity.Netcode.NetworkManager.Singleton;
    if (nm == null) return;

    // 1. target 해결 — SpawnedObjects.TryGetValue 패턴 (PlayerDamageRelay 참고)
    if (!nm.SpawnManager.SpawnedObjects.TryGetValue(targetNetObjId, out var targetNo))
    {
        Debug.LogWarning($"[PlayerMovementSync] Coop revive — target {targetNetObjId} not found");
        return;
    }

    // 2. target 이 player 인지 (몬스터 NetworkObjectId 보내는 변조 차단)
    if (!targetNo.IsPlayerObject)
    {
        Debug.LogWarning($"[PlayerMovementSync] Coop revive — target {targetNo.name} not player");
        return;
    }

    // 3. 자기 자신 부활 차단 — sender (this) == target
    if (NetworkObject != null && NetworkObject.NetworkObjectId == targetNetObjId)
    {
        Debug.LogWarning($"[PlayerMovementSync] Coop revive — self target 거부");
        return;
    }

    // 4. 거리 재검증 — client distance 의 2배 tolerance (lag 보정 + 변조 방지)
    const float hostDistanceTolerance = 3.0f; // client 1.5m × 2
    float distSqr = (targetNo.transform.position - transform.position).sqrMagnitude;
    if (distSqr > hostDistanceTolerance * hostDistanceTolerance)
    {
        Debug.LogWarning($"[PlayerMovementSync] Coop revive — 거리 초과 거부 dist={Mathf.Sqrt(distSqr):F2}m (tolerance={hostDistanceTolerance})");
        return;
    }

    // 5. KhiDownController 해결 + Down 상태 확인
    var dc = targetNo.GetComponent<LostMemory.TestKhi.KhiDownController>()
          ?? targetNo.GetComponentInChildren<LostMemory.TestKhi.KhiDownController>();
    if (dc == null)
    {
        Debug.LogWarning($"[PlayerMovementSync] Coop revive — target KhiDownController 없음");
        return;
    }
    if (!dc.IsDown)
    {
        Debug.Log($"[PlayerMovementSync] Coop revive — target 이미 state={dc.CurrentState}, 무시");
        return;
    }

    // 6. 통과 — host 권위로 ForceRevive. NetworkVariable sync → 모든 client OK.
    Debug.Log($"[PlayerMovementSync] Coop revive 승인 — reviver={NetworkObjectId} target={targetNetObjId}");
    dc.ForceRevive();
}
```

### 2. `Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs` — 완료 분기에 host/guest 갈래

기존 (line 326~332):
```csharp
if (_coopReviveProgress >= cooperativeReviveChargeSeconds)
{
    var target = _coopReviveTarget;
    target.ForceRevive();
    ResetCoopReviveProgress();
}
```

변경:
```csharp
if (_coopReviveProgress >= cooperativeReviveChargeSeconds)
{
    var target = _coopReviveTarget;
    var nm = Unity.Netcode.NetworkManager.Singleton;

    if (nm != null && nm.IsServer)
    {
        // host — 직접. NetworkVariable sync 정상.
        Debug.Log($"[KhiDownController] Coop revive 완료 (host) — target={target.gameObject.name}");
        target.ForceRevive();
    }
    else
    {
        // guest — ServerRpc 위임. host 권위로 ForceRevive.
        var targetNo = target.GetComponentInParent<Unity.Netcode.NetworkObject>();
        var reviverSync = GetComponentInParent<LostMemory.Networking.Player.PlayerMovementSync>();
        if (targetNo != null && targetNo.IsSpawned && reviverSync != null)
        {
            Debug.Log($"[KhiDownController] Coop revive 완료 (guest) — ServerRpc 발사 target={targetNo.NetworkObjectId}");
            reviverSync.RequestCooperativeReviveServerRpc(targetNo.NetworkObjectId);
        }
        else
        {
            Debug.LogWarning($"[KhiDownController] Coop revive — guest ServerRpc 위임 실패 targetNo={targetNo} sync={reviverSync}");
        }
    }
    ResetCoopReviveProgress();
}
```

## 재사용 utility (검증됨)

| 위치 | 용도 |
|---|---|
| `PlayerMovementSync.RequestBossStartServerRpc` | ServerRpc 패턴 (RequireOwnership 기본값) |
| `PlayerDamageRelay.RequestDamageServerRpc` | `SpawnManager.SpawnedObjects.TryGetValue` 패턴 |
| `CombatTargetable` line 55 | `IsPlayerObject` 가드 패턴 |
| `KhiDownController.ForceRevive(float)` | 부활 실행 (기존) |
| `KhiDownController.IsDown` | Down 상태 검증 (기존) |

## Unity Editor 작업

**없음** — `PlayerMovementSync` 는 이미 player prefab 의 NetworkBehaviour. ServerRpc 메서드만 추가. prefab 자체 수정 0.

## NGO join risk 평가

| 항목 | 위험 |
|---|---|
| prefab GlobalObjectIdHash 변화 | 없음 (코드만 추가) |
| NetworkBehaviour 메서드 추가 | 낮음 — 기존 RPC 추가 사례 (Teleport, BossIntro 등) 모두 join 정상 |
| 컴파일 깨질 위험 | 매우 낮음 — 메서드 단일 추가, 기존 코드 영향 없음 |
| 회귀 (호스트 부활 깨짐) | 낮음 — host 분기 그대로 (`target.ForceRevive()`) |

이전 시연 직전 join 깨졌던 사례는 **76개 prefab/scene 일괄 변경** 이 원인. 이번엔 .cs 2개만 변경.

## 검증 시나리오

### 기본 (Host → Guest)
1. Host + Guest 멀티 진입
2. Guest 피격 Down
3. Host R hold 2초 (1.5m 내)
4. 확인:
   - [ ] Host 화면 — Guest mirror 부활
   - [ ] Guest 화면 — 자기 player 부활
   - [ ] 콘솔 — `Coop revive 완료 (host)`

### 신규 (Guest → Host)
1. Host 피격 Down (debug 으로 host 죽이기)
2. Guest R hold 2초 (Host 옆 1.5m)
3. 확인:
   - [ ] Guest 콘솔 — `Coop revive 완료 (guest) — ServerRpc 발사`
   - [ ] Host 콘솔 — `Coop revive 승인` + `ForceRevive`
   - [ ] **모든 client 화면 — Host 부활** (NetworkVariable sync)

### 신규 (Guest1 → Guest2 — 가장 중요)
1. 3인 멀티 (Host + Guest1 + Guest2) 또는 MPPM 가상 player
2. Guest2 피격 Down
3. Guest1 R hold 2초 (Guest2 옆 1.5m)
4. 확인:
   - [ ] Guest1 콘솔 — ServerRpc 발사
   - [ ] Host 콘솔 — 승인 + ForceRevive
   - [ ] Guest1/Guest2/Host 모든 화면 — Guest2 부활

### 거리 검증 (anti-cheat)
1. Guest R hold 시작 → 차징 중 멀리 이동 (3m 초과)
2. 확인 — Guest 측 거리 체크에서 client cancel (서버까지 안 감)
3. 추가 — Guest 측 거리 체크 우회 (debug 코드로) → host 측 거부 로그 출력

### 자기 부활 차단
1. Guest 자기 Down → R 누름
2. 확인 — TickCooperativeReviveSearch 가 Normal 상태에서만 작동 + `dc == this` 필터 → 부활 안 됨
3. 추가 — host 측에서도 `NetworkObjectId == targetNetObjId` 검사로 거부 (방어 깊이)

### Edge cases
- target 이 ServerRpc 도착 사이 Defeated → host `IsDown == false` 로 무시
- target 이 ServerRpc 도착 사이 자체 부활 (Memory ReviveOnce) → 동일하게 무시

## 시연 후 follow-up (이 plan 범위 밖)

1. Progress bar UI — `KhiStaffChargeBarView` 패턴 적용
2. 차징 효과음 / 시각 이펙트
3. host 측 ToleranceDistance SerializeField 화 (인스펙터 튜닝)

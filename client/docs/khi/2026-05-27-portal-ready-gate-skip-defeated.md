# Portal ready 게이트 — Defeated 플레이어 제외

## Context

이전 작업(`2026-05-27-portal-ready-gate-and-status-panel.md`) 으로 멀티에서 포탈을 "전원이
F 눌러야 발동" 하는 ready 게이트를 도입함. 그러나 한 명이 **Defeated** (영구 사망) 되면 다음과
같이 문제 발생:

- 서버가 `NetworkManager.ConnectedClientsIds.Count` 를 그대로 required 로 사용 → Defeated 도 인원에 포함.
- Defeated 플레이어는 `KhiPlayerActionGate.IsBlocked` 로 후보에서 제외되어 F 못 누름.
- → required 가 만족 불가능 → 다른 사람들이 모두 F 눌러도 게이트 영구 stuck.

**의도된 동작**:
- Defeated → required 에서 **제외** (팀이 그 사람 없이 진행 가능)
- Down → required 에 **포함** (부활까지 대기 — 의도된 차단)

## 접근

서버 측 두 가지 변경:

1. **required 계산을 alive 인원으로** — `ResolveLocalTeleportRequiredCount` 에 Defeated
   필터 추가. 두 ready 게이트(LocalTeleport / RouteAdvance) 가 같은 함수 사용하므로 한 곳만 고치면 됨.

2. **Defeated 발생 시 자동 재평가** — 이미 ready 보낸 사람이 N-1 일 때 마지막 한 명이
   Defeated 되면 required 가 N-1 로 줄어 자동으로 합의 충족돼야 함. 이 트리거가 없으면
   다음 F 누름 전까지 stuck.
   → `KhiDownController` 에 `static event AnyPlayerDefeated` 추가, `StageRouteManager` 가
   서버 측에서 구독해 pending ready set 전부 재평가.

Down → Defeated 전이만 신경 쓰면 됨 (Down 자체는 required 에서 빼지 않음 — 부활 대기 의도 유지).

## 핵심 변경 파일

### 1. `LostMemory/TestKhi/KhiDownController.cs`

**필드 추가** (~L156, 기존 `DefeatedByTimeout` / `DefeatedSolo` event 옆):
```csharp
/// <summary>
/// 임의의 KhiDownController 가 Defeated 로 전이될 때 발화 (instance event 들 보다 후행).
/// StageRouteManager 가 서버 측에서 구독해 pending ready set 재평가용.
/// owner-side ExecuteDefeat 경로 + non-owner mirror 의 ApplyDownStateFromNetwork 경로
/// 양쪽에서 발화. _state == newState 가드로 중복 발화 없음.
/// </summary>
public static event Action<KhiDownController> AnyPlayerDefeated;
```

**발화 지점 1 — `ExecuteDefeat` (L1058 직후)**:
```csharp
_state = KhiDownState.Defeated;
AnyPlayerDefeated?.Invoke(this);   // ← 추가
```

**발화 지점 2 — `ApplyDownStateFromNetwork` (L920 직후)**:
```csharp
if (_state == newState) return;
_state = newState;
if (newState == KhiDownState.Defeated) AnyPlayerDefeated?.Invoke(this);   // ← 추가
```

(early return 가드가 이미 있어서 owner 측에서 이중 발화 안 됨.)

### 2. `LostMemory/Stage/StageRouteManager.cs`

**A. Required 계산 함수 수정** (`ResolveLocalTeleportRequiredCount`):
```csharp
private static int ResolveLocalTeleportRequiredCount(ulong? excludeClientId = null)
{
    NetworkManager nm = NetworkManager.Singleton;
    if (nm == null || !nm.IsListening) return 1;

    int count = 0;
    foreach (ulong id in nm.ConnectedClientsIds)
    {
        if (excludeClientId.HasValue && id == excludeClientId.Value) continue;
        if (IsClientDefeated(nm, id)) continue;   // ← 추가: Defeated 제외
        count++;
    }
    return Mathf.Max(1, count);
}

// 서버 측 helper. PlayerObject 없거나 KhiDownController 없으면 false (alive 취급) 폴백.
private static bool IsClientDefeated(NetworkManager nm, ulong clientId)
{
    if (!nm.ConnectedClients.TryGetValue(clientId, out var nc)) return false;
    NetworkObject po = nc.PlayerObject;
    if (po == null) return false;
    var dc = po.GetComponent<KhiDownController>()
          ?? po.GetComponentInChildren<KhiDownController>(includeInactive: true);
    return dc != null && dc.IsDefeated;
}
```

`using LostMemory.TestKhi;` 추가 필요.

**B. Defeated event 구독 + 재평가** — OnEnable/OnDisable 에 등록/해제:
```csharp
private void OnEnable()
{
    SceneManager.sceneLoaded += HandleSceneLoaded;
    // 기존 NetworkManager 이벤트 구독 그대로 ...

    KhiDownController.AnyPlayerDefeated += HandleAnyPlayerDefeated;
}

private void OnDisable()
{
    SceneManager.sceneLoaded -= HandleSceneLoaded;
    // 기존 ...

    KhiDownController.AnyPlayerDefeated -= HandleAnyPlayerDefeated;
}

// 어느 클라이언트의 player 든 Defeated 되면 서버 측에서 pending ready set 전부 재평가.
// required 가 줄어들어 기존 set 만으로 합의 충족 시 fire.
private void HandleAnyPlayerDefeated(KhiDownController dc)
{
    NetworkManager nm = NetworkManager.Singleton;
    if (nm == null || !nm.IsListening || !nm.IsServer) return;

    ReevaluateAllPendingReadySets();
}

private void ReevaluateAllPendingReadySets()
{
    // 키 collection iteration 중 set 변형(dictionary remove) 안전을 위해 snapshot 후 순회.
    if (_localTeleportReady.Count > 0)
    {
        List<string> keys = new(_localTeleportReady.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            if (_localTeleportReady.TryGetValue(keys[i], out var set))
                EvaluateAndFireLocalTeleportReady(keys[i], set);
        }
    }
    if (_routeAdvanceReady.Count > 0)
    {
        List<string> keys = new(_routeAdvanceReady.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            if (_routeAdvanceReady.TryGetValue(keys[i], out var set))
                EvaluateAndFireRouteAdvanceReady(keys[i], set);
        }
    }
}
```

## 의도적으로 안 하는 것

- **Down 플레이어 required 제외 안 함** — 부활 대기 의도 유지. 사용자가 명시한 "죽은 사람"
  은 Defeated 로 해석.
- **Defeated 플레이어를 candidates HashSet 에서 즉시 제거 안 함** — `CanUseTrigger` 가 이미
  `KhiPlayerActionGate.IsBlocked` 로 후보 자격 차단. UI 상 주황 색이 잠깐 남을 순 있으나 다음
  `OnTriggerStay2D` / Update 에서 자연 정리.
- **SceneLoadPortalController / BossClearPortalController** — 별도 컨트롤러이고 이번 작업은
  RouteNodeExitTrigger 가 쓰는 StageRouteManager ready 게이트에 한정. (그 두 컨트롤러는
  자체 게이트 로직이 있고 이전 commit 에서 revert 됐던 부분.)
- **PlayerHealthSync 수정 안 함** — KhiDownController 안에서 자체 event 발화로 해결.

## 검증

1. **솔로** — NetworkManager.IsListening = false → `ResolveLocalTeleportRequiredCount` 가
   1 반환. 기존 동작 그대로 (영향 없음).

2. **멀티 2인, 둘 다 alive** — 둘 다 F → required=2, set=2 → 합의 (기존 동작).

3. **멀티 2인, 한 명 Defeated 된 직후**
   - 시나리오 A: alive 가 F 누름 → ready 보내고 EvaluateAndFire → required=1 (Defeated 제외),
     set=1 → 합의 → 씬 로드. ✓
   - 시나리오 B: 둘 다 alive 일 때 호스트가 먼저 F 누름 → ready set = {host},
     required=2 → 보류. 게스트가 Defeated 되면 `AnyPlayerDefeated` 발화 →
     `ReevaluateAllPendingReadySets` → required=1, set=1 → 합의 → 씬 로드 (게스트는
     사망 상태로 같이 다음 씬으로 진입). ✓

4. **멀티 2인, 한 명 Down (Defeated 아님)** — required=2 유지 (Down 은 alive 취급). 
   alive 가 F 눌러도 set=1 < required=2 → 보류. Down 부활하고 F 눌러야 합의. ✓

5. **둘 다 Defeated** — required = max(1, 0) = 1, set = {} → 합의 안 됨 → 게이트 stuck.
   RunFailed 경로가 별도로 처리하므로 게이트는 그냥 안 발동되면 OK.

6. **Defeated → 부활 (Down 으로 회귀 후 ForceRevive 등)** — 부활 시점에 required 다시 늘어남
   (다음 ready RPC 도착 시 자연스럽게 반영). 부활 자체로 재평가 트리거는 안 만듦 — 합의가
   "더 까다로워지는" 방향이라 기존 fire 가 잘못 발동될 위험 없음.

7. **회귀** — `ResolveLocalTeleportRequiredCount` 가 disconnect 핸들러에서도 호출됨
   (`HandleClientDisconnectForLocalTeleport` / `HandleClientDisconnectForRouteAdvance`).
   `excludeClientId` 파라미터 시그니처 그대로 유지. Defeated 필터는 추가만 됨.

## 후속 (선택)

- **시각 피드백**: Defeated 인원이 있을 때 portal 위에 "1명 사망 — 진행 가능" 같은 안내. 본 plan 범위 밖.
- **부활로 인한 required 증가 재평가**: 현재는 다음 ready RPC 가 와야 발동. 사용자가 시나리오에서
  "한 명 부활했는데 게이트 fire 됐다" 류 버그 보고하면 그때 추가.

# Reward Panel 4인 동시 표시 안 됨 — 진단 로그 추가 plan

## Context

Phase E fix 적용 후에도 보고된 버그:
- 보스/방 클리어 후 reward panel 이 **사람마다 동시에 떠야 하는데** host 에게만 보이거나 양쪽 다 안 뜨는 케이스 발생.
- 골드/기억파편 per-client 적립 (`F-1`) 은 정상 → ClientRpc 자체는 도달 중.
- 사용자가 host/guest 동시 정밀 진단 원함: "자세히 각각 별로 log 만들어주고 뭐가 문제인지 파악하고 싶어. 알아서 로그 만들고 진행해줘".

본 plan 은 **코드 동작 변경 없이 진단 로그만 추가** → 빌드 후 host & guest Player.log 비교로 끊기는 지점 식별. 식별 후 별도 fix turn 진행.

## 끊김 의심 지점 (추정 path 5개)

A. **host 의 `FireRoomCleared` 자체가 안 불림** — clearTracker 가 enemy 사망 감지 실패, 또는 `roomCleared==true` 가 이미 set 됨.

B. **host 의 `IsSpawned && IsListening` 분기 실패** — `RoomEntryRuntimeController.cs:467` 에서 ClientRpc 대신 line 473 의 로컬 invoke 만 발화 → guest 못 받음.

C. **ClientRpc 는 fan-out 됐지만 guest 측 `roomData == null`** — `RoomClearedBroadcastClientRpc` (line 481) 가 자기 인스턴스의 roomData SerializeField 사용. NGO scene sweep 후 guest 측 prefab instance 의 roomData 가 null 일 가능성. → payload.Data null → RewardController line 145-149 에서 silent return.

D. **RewardController.HandleRoomClearedFromController 진입 후 가드 중 하나에서 silent return** — `payload.Data==null`, `RoomType != Combat`, `!HasSpawnedEnemies`, `_isShowingReward==true` 중 하나.

E. **ShowReward 까지 도달했지만 panel/inventory resolve 실패 또는 panel SetActive(true) 이후 activeInHierarchy=false** — Canvas 가 비활성 상태이거나 parent GameObject 가 inactive.

위 5개 path 의 어느 line 까지 host 와 guest 각자 도달했는지 로그로 추적.

---

## 진단 로그 추가 항목

### Log 1 — `RoomEntryRuntimeController.HandleRoomCleared` 진입

**파일**: `Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs` (line 439 부근)

```csharp
private void HandleRoomCleared(RoomClearedPayload payload)
{
    var nm = NetworkManager.Singleton;
    Debug.Log($"[RoomCtrl] HandleRoomCleared on '{name}' roomId={payload.RoomId} " +
              $"spawnedEnemies={payload.SpawnedEnemyCount} alreadyCleared={roomCleared} " +
              $"NM(host={nm?.IsHost},srv={nm?.IsServer},client={nm?.IsClient},listening={nm?.IsListening}," +
              $"localId={nm?.LocalClientId}) IsSpawned={IsSpawned} IsAuthority={IsAuthority}", this);
    // ... 기존 로직
}
```

→ Path A 검증: host 에서 이 로그가 찍히는지. 안 찍히면 clearTracker / encounterSpawner 측 문제.

### Log 2 — `FireRoomCleared` 의 ClientRpc 분기

**파일**: 동 (line 449–475)

```csharp
private void FireRoomCleared(RoomClearedPayload payload)
{
    if (roomCleared)
    {
        Debug.LogWarning($"[RoomCtrl] FireRoomCleared SKIP (already cleared) roomId={payload.RoomId}", this);
        return;
    }
    roomCleared = true;
    // ... 기존 로직 ...

    NetworkManager nm = NetworkManager.Singleton;
    bool willBroadcast = IsSpawned && nm != null && nm.IsListening;
    Debug.Log($"[RoomCtrl] FireRoomCleared roomId={payload.RoomId} willBroadcastClientRpc={willBroadcast} " +
              $"IsSpawned={IsSpawned} IsListening={nm?.IsListening} " +
              $"localId={nm?.LocalClientId}", this);
    if (willBroadcast)
    {
        RoomClearedBroadcastClientRpc(payload.SpawnedEnemyCount);
    }
    else
    {
        // 싱글 fallback — host 에서만 발화.
        Debug.LogWarning($"[RoomCtrl] FireRoomCleared LOCAL-ONLY invoke (guest will NOT receive). roomId={payload.RoomId}", this);
        RoomClearedBroadcast?.Invoke(payload);
    }
}
```

→ Path B 검증: ClientRpc 발화 여부.

### Log 3 — `RoomClearedBroadcastClientRpc` 진입 (모든 클라이언트에서 발화)

**파일**: 동 (line 477–483)

```csharp
[ClientRpc]
private void RoomClearedBroadcastClientRpc(int spawnedEnemyCount)
{
    var nm = NetworkManager.Singleton;
    string id = roomData != null ? roomData.RoomId : null;
    Debug.Log($"[RoomCtrl] RoomClearedBroadcastClientRpc RECEIVED on '{name}' " +
              $"roomData={(roomData != null ? roomData.name : "NULL")} " +
              $"resolvedRoomId='{id}' spawnedEnemies={spawnedEnemyCount} " +
              $"localId={nm?.LocalClientId} isHost={nm?.IsHost}", this);
    RoomClearedBroadcast?.Invoke(new RoomClearedPayload(id, roomData, spawnedEnemyCount));
}
```

→ Path C 의 핵심 검증: guest 의 roomData 가 null 인지 / room id resolve 결과.

### Log 4 — `RewardController.HandleRoomClearedFromController` 가드 진입/통과

**파일**: `Assets/_Project/Scripts/Runtime/Stage/RewardController.cs` (line 129–176)

진단 로그가 이미 존재 (`logRewardFlow`). 다음 보강:

```csharp
// 기존 line 145 부근
if (payload.Data == null)
{
    Debug.LogError($"[RewardController] STOP — payload.Data null. roomId={payload.RoomId} " +
                   $"source='{(source != null ? source.name : "null")}' " +
                   $"localId={NetworkManager.Singleton?.LocalClientId}", this);
    return;
}
if (payload.Data.RoomType != StageRoomType.Combat)
{
    Debug.Log($"[RewardController] STOP — non-Combat room {payload.RoomId} type={payload.Data.RoomType} " +
              $"localId={NetworkManager.Singleton?.LocalClientId}");
    return;
}
if (!payload.HasSpawnedEnemies)
{
    Debug.Log($"[RewardController] STOP — no spawned enemies. {payload.RoomId} " +
              $"localId={NetworkManager.Singleton?.LocalClientId}");
    return;
}
if (_isShowingReward)
{
    Debug.LogWarning($"[RewardController] STOP — already showing reward, ignoring room {payload.RoomId} " +
                     $"localId={NetworkManager.Singleton?.LocalClientId}", this);
    return;
}
Debug.Log($"[RewardController] PASS all gates — scheduling DelayedShowReward in {rewardShowDelay}s " +
          $"localId={NetworkManager.Singleton?.LocalClientId}");
```

→ Path D 검증: 어떤 가드에서 silent return 했는지 명시.

### Log 5 — `ShowReward` 진입부 panel/inventory 상태

**파일**: 동 (line 193–250)

기존 진단 로그 (line 201–209) 보강 — panel/inventory 의 active 상태 / Canvas 상태 추가:

```csharp
public void ShowReward()
{
    ResolveRewardPanelView();
    ResolvePlayerRelicInventory();

    var nm = NetworkManager.Singleton;
    string panelActive = rewardPanelView != null
        ? $"goActive={rewardPanelView.gameObject.activeSelf} hier={rewardPanelView.gameObject.activeInHierarchy} parent={(rewardPanelView.transform.parent != null ? rewardPanelView.transform.parent.name : "ROOT")}"
        : "panel=null";
    string canvasState = "no-canvas";
    if (rewardPanelView != null)
    {
        var canvas = rewardPanelView.GetComponentInParent<Canvas>(true);
        if (canvas != null) canvasState = $"canvas='{canvas.name}' enabled={canvas.enabled} hier={canvas.gameObject.activeInHierarchy}";
    }
    Debug.Log($"[RewardController] ShowReward entered. localId={nm?.LocalClientId} " +
              $"panelResolved={rewardPanelView != null} inventoryResolved={playerRelicInventory != null} " +
              $"inventoryHost='{(playerRelicInventory != null ? playerRelicInventory.gameObject.name : "null")}' " +
              $"panel({panelActive}) {canvasState}", this);

    if (rewardPanelView == null || playerRelicInventory == null)
    {
        Debug.LogError($"[RewardController] ShowReward STOP — rewardPanelView or inventory null. localId={nm?.LocalClientId}", this);
        return;
    }
    // ... 기존 흐름 ...
}
```

→ Path E 검증: panel 객체는 있지만 canvas/parent 가 비활성인지.

### Log 6 — `RewardPanelView.Show` 진입 + SetActive 결과

**파일**: `Assets/_Project/Scripts/Runtime/Rewards/RewardPanelView.cs` (line 74–127)

```csharp
public void Show(PlayerRelicInventory inventory, int count, int picksAllowed,
                 int luckPoints, bool forceLegendary, bool relicOnly, float rarityBoostPercent)
{
    var nm = Unity.Netcode.NetworkManager.Singleton;
    Debug.Log($"[RewardPanelView] Show ENTER localId={nm?.LocalClientId} " +
              $"inventory={(inventory != null ? inventory.gameObject.name : "NULL")} " +
              $"count={count} picks={picksAllowed} " +
              $"beforeActive={gameObject.activeSelf} hier={gameObject.activeInHierarchy}", this);

    // ... 기존 흐름 (inventory fallback, draw, card setup) ...

    gameObject.SetActive(true);
    Debug.Log($"[RewardPanelView] Show EXIT — SetActive(true) called. " +
              $"afterActive={gameObject.activeSelf} hier={gameObject.activeInHierarchy} " +
              $"localId={nm?.LocalClientId}", this);
}
```

→ Path E 의 최종 검증: SetActive(true) 후에도 hierarchy 가 false 면 부모 chain 어디가 막혀 있음.

---

## 진단 hook 6개 요약

| # | 파일 | 검증 path | localId 포함 |
|---|------|-----------|--------------|
| 1 | RoomEntryRuntimeController.HandleRoomCleared | A — host clearTracker 발화 | ✓ |
| 2 | RoomEntryRuntimeController.FireRoomCleared | B — IsSpawned/IsListening 분기 | ✓ |
| 3 | RoomEntryRuntimeController.RoomClearedBroadcastClientRpc | C — guest roomData null | ✓ |
| 4 | RewardController.HandleRoomClearedFromController 가드 | D — 어떤 가드에서 STOP | ✓ |
| 5 | RewardController.ShowReward 진입 | E — panel/canvas 상태 | ✓ |
| 6 | RewardPanelView.Show 진입/SetActive | E — SetActive 결과 | ✓ |

**핵심 공통 필드**: `NetworkManager.Singleton.LocalClientId` 를 모든 로그에 포함 → host log 와 guest log 를 grep 으로 분리 가능 (`grep "localId=0"` = host, `grep "localId=1"` = guest).

---

## 변경 파일

| 파일 | 변경 종류 |
|------|-----------|
| `Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs` | Log 1, 2, 3 추가 |
| `Assets/_Project/Scripts/Runtime/Stage/RewardController.cs` | Log 4, 5 (기존 진단 보강) |
| `Assets/_Project/Scripts/Runtime/Rewards/RewardPanelView.cs` | Log 6 (진입/SetActive 결과) |

기존 `logRewardFlow` SerializeField 는 그대로 두되, **본 Log 1/2/3/6 은 무조건 발화** (진단 우선). 안정화 후 일괄 `verboseLog` 같은 토글로 묶어 false 처리.

기능 동작 (silent return 분기, ClientRpc 발화, panel SetActive) 은 **건드리지 않음** — 순수 로그만 추가.

---

## Verification (테스트 절차)

1. 위 6개 hook 적용 후 빌드.
2. host + guest 2-client smoke test:
   - host 만 / guest 만 / 양측 모두 enemy 처치 → 일반 Combat room 클리어.
   - 보스방 클리어 (Boss 는 reward skip path 가 정상 동작 — Log 4 의 "non-Combat" STOP 으로 확인).
3. 양측 Player.log 수집 후 grep:
   ```
   grep "\[RoomCtrl\]\|\[RewardController\]\|\[RewardPanelView\]" Player.log
   ```
4. host log 와 guest log 의 동일 roomId 에 대해 line 1→6 순서대로 도달 여부 비교.
5. **끊기는 첫 hook 위치 = 진짜 원인**. 예시:
   - host 에 Log 2 의 `willBroadcastClientRpc=false` → Path B (NGO 미spawn / IsListening 거부).
   - guest 에 Log 3 의 `roomData=NULL` → Path C (NGO scene placed prefab 의 roomData SerializeField wiring 누락).
   - guest 에 Log 4 의 `STOP — non-Combat` → Path D 의 roomData 가 잘못된 ScriptableObject 잡힘 (다른 room asset).
   - guest 에 Log 6 의 `afterActive=true hier=false` → Path E (parent Canvas 가 비활성).
6. 진단 결과를 다음 turn 에 사용자에게 보고 → 그때 정확한 fix path 선택해 별도 turn 에서 코드 수정.

---

## Critical Files to Modify

| 파일 | 라인 영역 |
|------|----------|
| `Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs` | 439–447, 449–475, 477–483 |
| `Assets/_Project/Scripts/Runtime/Stage/RewardController.cs` | 129–176, 193–252 |
| `Assets/_Project/Scripts/Runtime/Rewards/RewardPanelView.cs` | 74–127 |

## Reuse

- `NetworkManager.Singleton.LocalClientId` — 모든 로그의 host/guest 식별자. 이미 RewardController 의 기존 진단 (line 132–142) 에서 사용 패턴 확립.
- `Debug.Log / LogWarning / LogError` 등급 — 가드 STOP 은 LogWarning, 결정적 NULL 은 LogError 로 구분 (filter 용이).

## Open Items

- 진단 결과 분석 후 진짜 fix:
  - Path C (guest roomData null) → RoomEntryRuntimeController 가 NetworkVariable 로 roomId 만 sync 하고 guest 측은 ID → ScriptableObject lookup table 통해 자기 roomData 재구성하는 구조 필요할 수 있음. 또는 dungeon prefab generation 자체가 양측에서 동일 hierarchy 보장하는지 검증.
  - Path E (Canvas 비활성) → guest 측 RewardPanelView 의 parent Canvas 가 host scene 에만 활성 상태인 race. DontDestroyOnLoad UI Canvas 검증.
- 본 plan 의 hook 추가는 일회용 진단. **2~3 turn 내에 fix 완료 + 로그 정리** 권장.

# 인벤토리 가득 모달 — 멀티 게스트에서 안 뜨는 버그 fix

> 작성일: 2026-05-24
> 출처: plan 파일 (`~/.claude/plans/4-snazzy-canyon.md`) 을 memory rule 에 따라 이곳으로 이전.

## Context

### 증상
- 솔로 = 인벤토리 16개 가득 + 1개 더 들어오면 **교환 모달 정상 표시**
- 멀티 = **게스트 측 모달 안 뜸** (호스트는 정상 추정)

### 근본 원인 (Phase 1 탐색 결과)

`InventoryFullModal.TryBind()` 흐름:
1. `Bootstrap` (RuntimeInitializeOnLoadMethod, AfterSceneLoad) — modal GameObject 자동 생성
2. `OnEnable` → `TryBind()` 호출
3. **NGO Spawn race** — `KhiPlayerStateAggregator.Register(LocalPlayerResolver)` 호출 전 시점일 수 있음
4. `LocalPlayerResolver.LocalCharacter` 의 **fallback** (line 97~99):
   ```csharp
   _localCharacter = UnityEngine.Object.FindFirstObjectByType<Character>();
   ```
   → 게스트 측에서도 첫 번째 Character (host 가 먼저 spawn 됐으면 **host character**) 반환
5. `inventory = localCharacter.GetComponentInChildren<PlayerRelicInventory>()` → **host 의 inventory** 잡음
6. modal 이 host 의 `OnTryAddRejected` 만 구독
7. 게스트 자기 inventory full 이벤트는 발화돼도 modal 무시 (다른 인스턴스 이벤트 구독 중)

추가 문제 — `TryBind` 시작에 `if (inventory != null) return;` 가드 → **이미 잘못 bind 됐으면 LocalPlayerReady 발화 후에도 재bind 안 됨**.

### 영향 범위
- `InventoryFullModal` — 가장 직접적 (long-lived modal, 재bind 안 됨)
- `RewardController.ResolvePlayerRelicInventory` / `VendingMachineController` / `CardDrawController` — **매 호출마다 resolve 라 LocalCharacter fix 만으로 자동 개선**

---

## 해결 방안 (2개 파일)

### 1. `LostMemory/Assets/_Project/Scripts/Runtime/Networking/Player/LocalPlayerResolver.cs` — fallback 안전성

**위치**: `LocalCharacter` getter (line 84~101)

**변경**: NGO 활성 시 `FindFirstObjectByType<Character>()` fallback 차단.

```csharp
public static Character LocalCharacter
{
    get
    {
        if (_localCharacter != null) return _localCharacter;

        // 1. aggregator 등록된 경우 (기존)
        if (_localPlayer != null)
        {
            _localCharacter = _localPlayer.GetComponent<Character>();
            if (_localCharacter != null) return _localCharacter;
        }

        // 2. NGO 활성 시 fallback 차단 — 게스트가 host Character 잡는 race 방지.
        //    호출 측은 LocalPlayerReady 이벤트 대기.
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening) return null;

        // 3. 솔로 / Editor 단일 씬 fallback
        _localCharacter = UnityEngine.Object.FindFirstObjectByType<Character>();
        return _localCharacter;
    }
}
```

**효과**: `RewardController`, `VendingMachineController`, `CardDrawController` 등 LocalCharacter 사용처 일괄 fix. 솔로는 `IsListening=false` 라 영향 0.

### 2. `LostMemory/Assets/_Project/Scripts/Runtime/Shop/InventoryFullModal.cs` — 재검증 + self-heal

**위치 A**: `TryBind` (line 92~118)

**변경**: 기존 binding 이 현재 `LocalCharacter` 의 자식 인벤토리인지 검증. 다르면 unsubscribe + 재bind.

```csharp
private void TryBind()
{
    var localCharacter = LocalPlayerResolver.LocalCharacter;

    // 잘못된 binding 검증 — 기존 inventory 가 현재 LocalCharacter 의 자식 아니면 해제.
    if (inventory != null && localCharacter != null)
    {
        bool isLocalInventory = inventory.transform.IsChildOf(localCharacter.transform)
                             || inventory.GetComponentInParent<Character>() == localCharacter;
        if (!isLocalInventory)
        {
            Debug.Log($"[InventoryFullModal] Rebind — 이전 binding '{inventory.name}' 가 LocalCharacter 의 child 아님. 재bind.", this);
            inventory.OnTryAddRejected -= HandleRejected;
            inventory = null;
        }
    }

    if (inventory != null) return; // 이미 올바른 binding

    // 1) LocalCharacter 우선
    if (localCharacter != null)
    {
        inventory = localCharacter.GetComponentInChildren<PlayerRelicInventory>(includeInactive: true);
        if (inventory == null) inventory = localCharacter.GetComponentInParent<PlayerRelicInventory>();
    }

    // 2) fallback — 솔로일 때만 (LocalCharacter fix 후엔 NGO 환경에선 null)
    if (inventory == null)
    {
        inventory = FindAnyObjectByType<PlayerRelicInventory>();
    }

    if (inventory == null) return;

    inventory.OnTryAddRejected += HandleRejected;
    Debug.Log($"[InventoryFullModal] Bound to '{inventory.name}' (LocalCharacter='{(localCharacter != null ? localCharacter.name : "null")}').", this);
}
```

**위치 B**: `HandleLocalPlayerReady` (line 84) — 그대로 유지. TryBind 안의 검증 로직이 자동으로 self-heal.

---

## 변경 파일 요약

| 파일 | 변경 |
|---|---|
| `LocalPlayerResolver.cs` | `LocalCharacter` getter 의 fallback 을 NGO 활성 시 차단 — 한 곳 수정으로 RewardController 등 일괄 fix |
| `InventoryFullModal.cs` | `TryBind` 에 binding 검증 + self-heal 추가 — `LocalPlayerReady` 발화 시 잘못된 binding 자동 복구 |

---

## 재사용 utility (검증됨)

| 위치 | 용도 |
|---|---|
| `LocalPlayerResolver.LocalCharacter` | 로컬 player Character 단일 진입점 |
| `LocalPlayerResolver.LocalPlayerReady` event | Spawn race 대응 lazy 재시도 |
| `LocalPlayerResolver.TryGetRegisteredLocalPlayer` | fallback 없는 strict 조회 (이미 line 122) |
| `PlayerRelicInventory.OnTryAddRejected` | 인벤토리 가득 이벤트 |

---

## 검증 시나리오

### A. 정상 작동 확인 (멀티)
1. 빌드 → host + guest 2 instance 실행
2. 게스트 측에서 보상 픽업 16번 → 인벤토리 가득
3. 17번째 보상 픽업 시도
4. **게스트 화면에 교환 모달 표시** ← 핵심
5. Player.log grep:
   ```
   [InventoryFullModal] Bound to 'TestKhi_MinimalCharacter2D(Clone)' (LocalCharacter='TestKhi_MinimalCharacter2D(Clone)')
   [InventoryFullModal] HandleRejected: relic='...', reason='공간 부족'
   ```

### B. self-heal 검증
- Bootstrap 시점에 host character 가 먼저 spawn 됐을 때:
   ```
   [InventoryFullModal] Bound to '...' (LocalCharacter='...')
   [InventoryFullModal] Rebind — 이전 binding '...' 가 LocalCharacter 의 child 아님. 재bind.
   [InventoryFullModal] Bound to '...' (LocalCharacter='...')
   ```
- LocalPlayerReady 발화 후 자동 복구

### C. 호스트 회귀 확인
- 호스트도 동일 시나리오 → 모달 정상 표시
- LocalCharacter fix 영향 없음 (호스트의 Register 가 정상 발화 시점 정확)

### D. 솔로 회귀 확인
- 솔로 (NGO 미활성, `IsListening=false`) → fallback path 그대로 작동
- 기존 동작 회귀 0

### grep 명령
```bash
grep -E "InventoryFullModal|PlayerRelicInventory.*reject|LocalPlayerResolver" Player.log
```

### Player.log path
`C:\Users\SSAFY\AppData\LocalLow\DefaultCompany\LostMemory\Player.log`

---

## NGO 안정성 평가

| 항목 | 위험 |
|---|---|
| prefab GlobalObjectIdHash 변화 | **없음** (2개 .cs 만) |
| scene 변경 | 없음 |
| `LocalCharacter` fallback 차단 부작용 | 낮음 — NGO 활성 시점엔 항상 Register 흐름 보장 (KhiPlayerStateAggregator) |
| 솔로 회귀 | 0 — `IsListening=false` 라 기존 path 그대로 |
| 다른 controller 영향 | 양성 — RewardController/VendingMachine/CardDraw 의 host 잘못 잡기 race 자동 fix |

---

## 구현 순서 (Plan 승인 후)

1. **LocalPlayerResolver.cs** 의 `LocalCharacter` getter 에 NGO 가드 추가 (3분)
2. **InventoryFullModal.cs** 의 `TryBind` 에 검증 + self-heal 추가 (10분)
3. **빌드 → 멀티 진입 → 게스트 인벤토리 16개 채우기 → 17번째 픽업 → 모달 표시 확인** (5분)
4. Player.log grep 으로 binding sequence 검증

## 시연 후 follow-up

1. **`PlayerRelicInventory` NetworkBehaviour 화 검토** — 현재는 client-local. host/guest 인벤토리 sync 필요한 시나리오 (드롭/거래 등) 발생 시 NetworkList 도입
2. **다른 long-lived modal 패턴 audit** — `InventoryFullModal` 처럼 영구 살아있는 modal/controller 가 한 번 bind 후 재검증 안 하는 경우 동일 패턴 fix
3. **`LocalPlayerResolver.LocalCharacter` fallback 의 솔로/Editor 보존 검증** — Editor 단일 씬 테스트 시 NGO 미활성이라 fallback path 정상 작동 확인

# 멀티 — Weapon / Attack / MagicalGirl / Inventory 멀티 sync 일괄 수정

## Context

이전 작업(`multi_relic_player_exclude_and_reward_guest.md`) 검증 중 4가지 멀티 sync 결함 추가 발견:

1. **Guest 가 무기 교체 시 host 무기도 같이 바뀜.**
   `WeaponModeController` 가 NetworkBehaviour 아니고 IsOwner 가드도 없음. `PlayerMovementSync.DisableInputComponentsForNonOwner()` 의 비활성 리스트에도 누락 → guest 의 Q/마우스휠 입력이 host 측 guest-복제본 캐릭터의 WeaponModeController 까지 트리거.

2. **Guest 공격이 host 측 enemy 에 데미지 안 들어가고, host 화면에 guest 공격 visual 도 안 보임. Guest 화면에선 host 가 공격하는 것 처럼 보임.**
   `AttackBroadcast` 의 visual ClientRpc + `PlayerDamageRelay.RelayDamage` 의 ServerRpc 흐름이 어느 단계에서 끊기는지 미확정 — 진단 필요.

3. **MagicalGirl 이 guest 화면에 안 보임.**
   `MagicalGirlSpawner.cs:336` 가 `new GameObject(...)` 일반 Instantiate. NetworkObject 미부착, NGO Spawn 호출 없음.

4. **InventoryToggleController 가 guest 에서 `playerRelicInventory` null 에러.**
   SerializeField 로 host 측 PlayerRelicInventory 만 가리킴. 이미 `ShopController.cs:166-185` 의 `ResolvePreferringLocalPlayer<T>()` 패턴이 동일 문제 해결 사례.

목표: 4가지를 일괄 수정. 의사 결정(사용자 답변 반영):
- MagicalGirl → 완전 NGO Spawn 전환 (prefab 화 포함)
- Weapon mode → NetworkVariable + IsOwner 가드
- Attack sync → 진단 우선 (로그만 추가, fix 는 다음 턴)
- Inventory → LocalPlayerResolver / ResolvePreferringLocalPlayer 패턴 차용

---

## Phase A — Inventory wiring 수정 (작고 빠름)

### 핵심 파일
- `Assets/_Project/Scripts/Runtime/Shop/InventoryToggleController.cs`
- 재사용: `Assets/_Project/Scripts/Runtime/Networking/Player/LocalPlayerResolver.cs:109-113`
  (`GetComponentOnLocalPlayer<T>()` 헬퍼)
- 패턴 참조: `Assets/_Project/Scripts/Runtime/Shop/ShopController.cs:166-185`
  (`ResolvePreferringLocalPlayer<T>` 형태 — 더 robust)

### 변경

`InventoryToggleController.cs` 에 lazy resolve 추가:

```csharp
private void EnsurePlayerSideRefsResolved()
{
    if (playerRelicInventory == null)
        playerRelicInventory = LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerRelicInventory>();
    if (goldWallet == null)
        goldWallet = LocalPlayerResolver.GetComponentOnLocalPlayer<GoldWallet>();
    if (buildManager == null)
        buildManager = LocalPlayerResolver.GetComponentOnLocalPlayer<BuildManager>();
}
```

`Open()` 진입 시 `EnsurePlayerSideRefsResolved()` 호출. `panel` / `setEffectPanel` / `shopController` / `rewardController` 는 scene-placed UI 라 기존 wiring 유지 — player-side 만 동적 resolve.

### 유사 위험 파일 (같이 처리)
탐색 결과 동일 문제 가능성:
- `CardDrawController` (`playerRelicInventory`, `goldWallet` SerializeField)
- `VendingMachineController` (`playerRelicInventory`, `goldWallet` SerializeField)

→ 두 파일도 같은 패턴 적용. **본 plan 의 Phase A 에 포함.**

---

## Phase B — Weapon mode NetworkVariable + IsOwner 가드

### 핵심 파일
- `Assets/_Project/Scripts/Runtime/TestKhi/WeaponModeController.cs`
- `Assets/_Project/Scripts/Runtime/Networking/Player/PlayerMovementSync.cs:148-167` (`DisableInputComponentsForNonOwner`)

### 변경 전략

**옵션 1 (선호) — `WeaponModeController` 자체를 `NetworkBehaviour` 로 전환**:
- `NetworkVariable<WeaponMode> _syncedMode = new(WeaponMode.Sword, ..., NetworkVariableWritePermission.Owner)`
- `SetMode(mode)` / `CycleMode()` 진입에 `if (NetworkManager.Singleton.IsListening && !IsOwner) return;` 가드.
- `_syncedMode.OnValueChanged += (prev, next) => ApplyMode(next, fireEvent: true);`
- Awake/Start 에서 _currentMode 와 _syncedMode.Value 동기화 (NGO 미활성 시 fallback 으로 _currentMode 직접 사용).

이렇게 하면:
- guest 가 자기 무기 교체 → NetworkVariable 변경 → host 측 guest-복제본 캐릭터에서 OnValueChanged 발화 → ApplyMode 호출 → host 화면에 guest 무기 visual 정확.
- host 가 자기 무기 교체 → guest 측 host-복제본 캐릭터도 sync.

### NGO 미활성 회귀 위험
- 싱글플레이 / Editor 단일 씬 — `NetworkManager.Singleton == null` 또는 `!IsListening`. NetworkBehaviour 의 `IsOwner` 는 NGO 비활성 시 false 일 수 있음. Awake 단계에서 ApplyMode 가 안 돌면 무기 visual 깨짐.
- 해결: SetMode 의 가드를 `if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsOwner) return;` 로 — NGO 비활성 시 가드 skip.

### PlayerMovementSync 보강 (안전망)
`PlayerMovementSync.DisableInputComponentsForNonOwner()` 의 비활성 리스트에 `WeaponModeController` 도 추가. NetworkVariable sync 가 핵심 메커니즘이지만, 입력 자체를 비-owner 측에서 차단해서 race 막음.

```csharp
private void DisableInputComponentsForNonOwner()
{
    DisableIfPresent<KhiMeleeComboController>();
    DisableIfPresent<KhiParryController>();
    DisableIfPresent<KhiDashController>();
    DisableIfPresent<KhiFinisherLunge>();
    DisableIfPresent<WeaponModeController>();  // NEW
    // ...
}
```

---

## Phase C — MagicalGirl NGO Spawn 전환

### 핵심 파일
- `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlSpawner.cs` (`AddGirlByVisual`, line ~330-360)
- `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAI.cs`, `MagicalGirlFollower.cs`, `MagicalGirlHoverTooltip.cs`, `MagicalGirlAOE.cs`, `MagicalGirlProjectile.cs`
- 신규 prefab: `Assets/_Project/Prefabs/MagicalGirl/MagicalGirl_<Visual>.prefab` (각 visual 별로)
- `Assets/DefaultNetworkPrefabs.asset` — 등록 추가

### 작업 단계

#### 1. Prefab 화 (Unity Editor)
- 현재 코드가 런타임에 `new GameObject(...)` + `AddComponent<...>` 로 구성 → 이 구성을 prefab 으로 추출.
- 각 visual 별 (총 N개) prefab 생성, NetworkObject 부착. Animator + Sprite 등 visual 별 차이 그대로.
- DefaultNetworkPrefabs.asset 에 N개 entry 추가.

#### 2. Spawner 코드 수정

```csharp
[SerializeField] private GameObject[] _magicalGirlPrefabs; // visual idx → prefab

public void AddGirlByVisual(MagicalGirlVisual visual)
{
    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
        && !HostAuthority.IsHost) return;  // 멀티에선 호스트만 spawn 권한

    GameObject prefab = _magicalGirlPrefabs[(int)visual];
    GameObject go = Instantiate(prefab, position, Quaternion.identity);

    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
    {
        var netObj = go.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned) netObj.Spawn(destroyWithScene: true);
    }

    // 기존 setup 코드 (follower target / AI catalog 등) 유지
}
```

#### 3. 기존 동작 보존
- 싱글플레이 (`NetworkManager.Singleton.IsListening == false`) → 호스트 가드 skip + NGO Spawn skip → 기존 동작 그대로
- 멀티 → 호스트만 Spawn, NGO 가 모든 클라에 sync → guest 화면에도 보임

#### 4. 회귀 위험
- prefab 화 과정에서 visual asset (Sprite / Animator) 매핑 누락 시 spawn 후 비주얼 안 보일 수 있음. prefab 마다 한 번씩 Editor 에서 시각 확인 필요.
- AI catalog / follower target / aim 등 런타임 wiring 은 코드에 남겨두기 (prefab 에 baking 안 함).

### 작업량 추정
- prefab 생성: visual 종류 수 만큼 (코드에서 5~10개 예상) → Editor 작업 30분~1시간
- Spawner 코드 수정: 20분
- DefaultNetworkPrefabs 등록 + 테스트: 30분

---

## Phase D — Attack sync 진단 (로그만 추가, 동작 변경 X)

### 핵심 파일
- `Assets/_Project/Scripts/Runtime/Networking/Player/AttackBroadcast.cs`
- `Assets/_Project/Scripts/Runtime/Combat/PlayerDamageRelay.cs`
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs` (RunAttack 코루틴, hitbox.Sample 호출부)
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeHitbox.cs` (`Sample` 메소드)

### 변경

각 핵심 분기점에 진단 로그 1줄씩 (Owner / IsServer / NGO listening 컨텍스트 포함):

- `AttackBroadcast.OnAttackStartedOwner` 진입 — `RelayAttackEventServerRpc` 호출 직전 컨텍스트 로그
- `AttackBroadcast.BroadcastAttackEventClientRpc` 수신 — non-owner guard 통과 / skip 모두 로그
- `PlayerDamageRelay.RelayDamage` 진입 — IsServer 분기 + Damage 적용 / ServerRpc 호출 어느 경로인지 로그
- `PlayerDamageRelay.RequestDamageServerRpc` 수신 — target NetworkObject lookup 성공/실패 로그
- `KhiMeleeHitbox.Sample` — 발견한 hit count 로그 (특히 0 이면 guest 측 collider 와 host 측 enemy 위치 mismatch 의심)

### 검증 빌드 후 분석

Guest Player.log 로 끊김 지점 식별:
- `OnAttackStartedOwner` 발화 없음 → guest 입력 처리 실패 (KhiMeleeComboController disable)
- `BroadcastAttackEventClientRpc` host 측에 도착하지만 visual skip → AttackBroadcast 의 ClientRpc 라우팅 OK
- `Sample` 의 hit count = 0 → guest 측 hitbox 가 host enemy 와 collision 안 잡음 (좌표/scene 차이)
- `RequestDamageServerRpc` 호출되지만 host 측 target resolve 실패 → NetworkObjectId 매핑 문제

### 다음 턴 작업
진단 결과 따라 fix 방향 결정. 후보:
- KhiMeleeComboController disable 가드 제거 (host 측 guest 복제본도 RunAttack 코루틴 돌게)
- AttackBroadcast 에 hitbox 좌표/방향 같이 broadcast → host 가 권위로 hit detect
- PlayerDamageRelay 의 NetworkObjectId mapping 수정

---

## 작업 순서 권장

1. **Phase A 먼저** — 가장 짧고 위험 낮음. Inventory + CardDraw + Vending 일괄.
2. **Phase D 같이** — 로그만 추가. 빌드와 함께 진단 데이터 수집.
3. Phase A + D 빌드 → guest 인벤토리 열림 확인 + attack log 분석.
4. **Phase B** — Weapon NetworkVariable sync. NGO 비활성 회귀 주의해서 적용.
5. **Phase C** — MagicalGirl NGO 전환. Prefab 화 Editor 작업 포함.

각 phase 마다 빌드 1회 = 총 빌드 3~4회 예상.

---

## 검증 시나리오 (end-to-end)

빌드 후 host + guest:

1. **Inventory (Phase A)**:
   - guest 가 I 키 → 자기 인벤토리 패널 정상 열림 ✓
   - `[InventoryToggleController] panel 또는 playerRelicInventory 가 null` 에러 사라짐 ✓
   - host I 키 → host 자기 인벤토리 (각자 다른 인벤토리 표시) ✓

2. **Weapon Mode (Phase B)**:
   - guest 가 Q → 자기 캐릭터만 무기 교체 ✓
   - host 측 guest 복제본 캐릭터 visual 도 guest 모드 따라감 (NetworkVariable sync) ✓
   - host 가 Q → guest 측 host 복제본도 sync ✓
   - host 자기 무기는 guest Q 와 무관 ✓ (회귀 방지)

3. **MagicalGirl (Phase C)**:
   - host 가 미소녀 relic 획득 → host 화면 + guest 화면 양쪽에 미소녀 보임 ✓
   - guest 가 미소녀 relic 획득 → host 화면 + guest 화면 양쪽에 미소녀 보임 ✓
   - 미소녀가 enemy 만 추적 (이전 plan 의 player 제외 가드 결합) ✓
   - 싱글플레이에서 미소녀 정상 (회귀 방지) ✓

4. **Attack diagnosis (Phase D)**:
   - guest Player.log 에 진단 로그 출력
   - 끊김 지점 식별 → 다음 턴 plan 으로 이어짐

---

## 비-목표 (Out of Scope)

- Attack sync fix (Phase D 진단 결과 보고 결정)
- 다른 player-side 컴포넌트 (SetEffectApplicator 등) 의 wiring 점검 — Phase A 결과 보고 확장 여부 결정
- Boss / Enemy 측 자동 추적 — 이전 plan 에서 제외 명시됨
- Late-Join (F-3) 시 inventory / weapon 복원 — 별건

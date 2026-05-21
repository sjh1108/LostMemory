# Phase E — 멀티플레이어 동기화 잔여 버그 fix plan

## Context

Phase A–D fix (Inventory wiring lazy resolve / WeaponModeController NetworkVariable / MagicalGirl Host-only spawn 가드 / Attack diagnostic logs) 적용 후 host & guest Player.log 재취합 결과 다음 잔여 버그가 확인됨:

1. **공격 더블 입력** — owner 가 마우스 누르면 host & guest 양측에서 동시에 공격 코루틴 시작 (KhiMeleeCombo/Staff/Flame 모두 IsOwner gate 누락).
2. **host → guest 공격 시각 효과 무반응**, **guest → host 는 정상** (비대칭 sync). AttackBroadcast 의 `weaponData` 해석이 host 측 instance 에서 실패하는 timing race 추정.
3. **Staff 기본 발사체(KhiArrowProjectile)가 다른 player 를 hit** — friendly-fire 차단 미흡. 현재 `IsOwnedByAttacker` 는 attacker 와 동일 GameObject 만 skip → 다른 player 통과.
4. **Reward 판넬이 host 에게만 보임** — gold/memory 는 per-client 정상이지만 RewardPanelView 가 host 측 인스펙터 reference 만 가진 scene-placed single instance 의심.
5. **InventoryFullModal 미동작** — `[InventoryFullModal] PlayerRelicInventory not found — modal disabled`. `OnEnable` 의 즉시 `FindAnyObjectByType` 가 Player 생성 전에 실행되는 timing race. 5×5 grid 가득 차면 swap modal 이 나와야 함 (이전 구현됨).
6. **NetworkAnimator NullReferenceException** (BuildTransitionStateInfoList:412 / OnValidate:524, 4회 반복) — Player prefab/씬의 NetworkAnimator `m_Animator` 필드 미할당.

회귀 금지: **per-client inventory 표시는 정상** — 본 작업에서 건드리지 않음.

---

## 결정 (AskUserQuestion 결과 반영)

- **AttackBroadcast 구조 변경 없음** — 매 frame `ActiveStarted` 등 broadcast 유지. RPC overflow 원인은 비-owner 의 중복 발화 → IsOwner gate 만 추가하면 자연 해소.
- **Friendly-fire 차단 기준**: PvP 미상정. **target Health 의 GameObject 에 `PlayerHealthSync` 컴포넌트가 있으면 attacker 무관하게 skip** — 가장 단순 & 효율적인 "팀원 차단" 패턴.
- **MagicalGirl 분석 생략** — 사용자 확인 결과 미소녀는 문제 없음. 실제 버그는 Staff 기본공격 발사체.

---

## Fix 항목

### Fix 1 — 공격 입력 IsOwner gate (3개 컨트롤러)

**대상 파일:**
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs` (line 183~227, Update / RequestAttack)
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiStaffController.cs` (line 116~167, Update / TryCastBolt)
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiFlamethrowerController.cs` (line 86~111, Update)

**접근:** 각 컨트롤러를 `NetworkBehaviour` 로 변환하는 대신, **부착 GameObject 의 `NetworkObject.IsOwner` 를 cached field 로 lookup** 해서 Update 진입부에서 가드.

```csharp
// 예시 — KhiMeleeComboController
private NetworkObject _netObj;
private void Awake()
{
    _netObj = GetComponentInParent<NetworkObject>();
}
private void Update()
{
    if (_netObj != null && _netObj.IsSpawned && !_netObj.IsOwner) return;
    // 기존 입력 로직
}
```

**왜 NetworkBehaviour 변환 안 함:** 기존 컨트롤러는 ScriptableObject WeaponData / EventBus 의존이 깊어 NetworkBehaviour 상속 시 `OnNetworkSpawn` 타이밍 재배치 필요. cached `NetworkObject` lookup 으로 최소 침습.

**부가:** `Assets/_Project/Scripts/Runtime/Networking/Player/PlayerMovementSync.cs:160` 의 `DisableInputComponentsForNonOwner` 에 **`KhiStaffController`, `KhiFlamethrowerController` 추가** (안전망 이중 보강).

```csharp
DisableIfPresent<KhiStaffController>();
DisableIfPresent<KhiFlamethrowerController>();
```

**기대 효과:**
- 공격 더블 입력 해소.
- AttackBroadcast 의 ServerRpc 발화 빈도 정확히 owner 1명분 → Receive queue overflow 해소.

---

### Fix 2 — host → guest 공격 시각 비대칭

**대상 파일:** `Assets/_Project/Scripts/Runtime/Networking/Player/AttackBroadcast.cs`

**원인 분석:**
- `ResolveRefs()` 에서 `weaponData = comboController.WeaponData` 를 OnNetworkSpawn 시점에 1회 캐싱.
- 비-owner 측 `KhiMeleeComboController` 는 `PlayerMovementSync.DisableInputComponentsForNonOwner` 로 `enabled=false`. 그 상태에서 `Awake` 가 실행 안 됐다면 `WeaponData` getter 가 default null 반환 가능.
- `BroadcastAttackEventClientRpc` → `ResolveStep(comboStep)` → `weaponData == null` → `null` 반환 → 모든 시각 효과 skip.
- **비대칭 이유:** host 측 player 의 AttackBroadcast 는 인스펙터에 `weaponData` SerializeField 가 prefab default 로 잡혀있을 수 있고, guest 측 spawn 된 player 의 AttackBroadcast 는 NGO replication 후 inspector 값 잃을 수 있음 — 또는 그 반대로 인스펙터 fallback path 차이.

**Fix:**

(a) `AttackBroadcast.weaponData` 의 inspector 할당 누락 방지 — `ResolveRefs` 에서 comboController 가 null 이거나 enabled=false 라도 `comboController.WeaponData` 를 직접 읽고, 그래도 null 이면 **fallback 으로 `WeaponModeController.CurrentMode` 의 WeaponData 조회**:

```csharp
private void ResolveRefs()
{
    if (comboController == null) comboController = GetComponent<KhiMeleeComboController>();
    // ... 기존 ...
    if (weaponData == null)
    {
        // Phase E: enabled=false 인 comboController 의 WeaponData 도 명시적 read
        if (comboController != null) weaponData = comboController.WeaponData;
        // 그래도 null 이면 WeaponModeController 에서 fallback
        if (weaponData == null)
        {
            var modeCtrl = GetComponent<WeaponModeController>();
            if (modeCtrl != null) weaponData = modeCtrl.GetWeaponDataForCurrentMode();
        }
    }
}
```

(b) `BroadcastAttackEventClientRpc` 진입부에 **null guard 진단 로그** 강화 — `weaponData == null` 시 한 번만 경고.

(c) **prefab 인스펙터 점검** — `Player` prefab 의 AttackBroadcast 컴포넌트 `weaponData` field 가 직접 ScriptableObject reference 로 채워져 있는지 확인. 비어있으면 직접 할당 (이게 진짜 원인일 가능성 높음).

**기대 효과:** host → guest 측에서도 ClientRpc 수신 후 `step != null` 보장 → `KhiSlashAnimator.HandleAttackActiveStarted` 등 시각 효과 정상 호출.

---

### Fix 3 — KhiArrowProjectile friendly-fire 차단

**대상 파일:** `Assets/_Project/Scripts/Runtime/TestKhi/KhiArrowProjectile.cs` (line 169~184, OnTriggerEnter2D)

**현재 로직:**
```csharp
if (IsOwnedByAttacker(health, _attacker)) { /* skip */ }
// ↓ 다른 player 는 attacker 와 별개 GameObject 라 통과
health.Damage(...)
```

**Fix:** `IsOwnedByAttacker` 체크 직후, **target Health 의 GameObject 또는 부모 chain 에 `PlayerHealthSync` 컴포넌트가 있으면 skip**:

```csharp
// Phase E: PvP 미상정 — Player Health 는 모든 발사체 면역
if (health.GetComponentInParent<LostMemory.Networking.Player.PlayerHealthSync>() != null)
{
    if (verboseLog) Debug.Log($"[KhiArrowProjectile] skip player target {health.gameObject.name}", this);
    return; // OnTriggerEnter2D 종료 또는 continue
}
```

추가로 **homing 경로** (line 123 부근의 `IsOwnedByAttacker` 호출처) 에도 동일 가드 적용 — homing target 선택 시 다른 player 가 후보로 잡히면 안 됨.

**왜 PlayerHealthSync 컴포넌트 체크:** `Character.CharacterType == Player` 또는 `NetworkObject.IsPlayerObject` 와 등가이면서 NGO depending 없이 빠른 lookup. 다른 player NetworkObject 도 동일 컴포넌트 보유 → 자/타 player 모두 보호.

**참고:** `KhiMeleeHitbox.cs:215` 의 `IsOwnedByAttacker` 도 동일 결함 — Sample loop 내에서 다른 player Health 통과 가능. 동일한 PlayerHealthSync skip 가드 추가 권장.

**기대 효과:** Staff 기본 발사체 / 근접 hitbox 모두 팀원 통과.

---

### Fix 4 — Reward panel guest 측 미표시

**대상 파일:**
- `Assets/_Project/Scripts/Runtime/Stage/RewardController.cs` (line 186, 250)
- `Assets/_Project/Scripts/Runtime/Rewards/RewardPanelView.cs` (line 23, 77, 126)

**원인 분석:**
- `RewardController.HandleRoomClearedFromController` (line 129) 는 `RoomClearedBroadcastClientRpc` 통해 per-client 발화 → Show 자체는 모두 호출됨.
- 그러나 `RewardPanelView` 는 scene-placed single instance + inspector 의 `_inventory` field 가 host 측 PlayerRelicInventory 만 reference.
- guest 측에서는 같은 RewardPanelView 객체 (host scene 의 것) 를 보고 있거나, 또는 guest 의 RewardPanelView 가 inactive.

**Fix:**

(a) `RewardPanelView._inventory` 인스펙터 의존 제거 — `Show()` 진입 시 `LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerRelicInventory>()` 로 lazy resolve:

```csharp
private PlayerRelicInventory ResolveLocalInventory()
{
    if (_inventory != null) return _inventory; // 기존 inspector 할당 우선
    return LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerRelicInventory>();
}

public void Show(IEnumerable<RewardCard> cards)
{
    var inv = ResolveLocalInventory();
    if (inv == null) { Debug.LogWarning("..."); return; }
    // ... 기존 ...
}
```

(b) `OnCardSelected` (line 155) 의 `_inventory.TryAdd(selected)` 호출도 `ResolveLocalInventory()` 사용.

(c) **RewardController 측**: panel 인스턴스가 client 마다 자기 것을 갖도록 — 만약 scene-placed 가 1개뿐이면 `DontDestroyOnLoad` UI canvas 의 RewardPanelView 가 client 마다 각자 보이지만 `_inventory` 만 잘못 잡혀있을 가능성. (a) 만으로 충분할지 확인 필요. 추가로 `rewardPanelView` 의 GameObject 가 active 인지 OnNetworkSpawn 시점에 보장.

**기대 효과:** guest 측에서도 reward panel 표시 + 본인 inventory 에 add.

---

### Fix 5 — InventoryFullModal lazy resolve

**대상 파일:** `Assets/_Project/Scripts/Runtime/Shop/InventoryFullModal.cs` (line 60~75)

**원인:** `OnEnable` 의 `FindAnyObjectByType<PlayerRelicInventory>()` 가 Player 생성 전에 실행 → null → modal disable.

**Fix:**

(a) `OnEnable` 의 즉시 lookup 제거. 대신 **lazy bind 방식** — `OnEnable` 에서는 listener 등록 후보로 자기 자신을 `LocalPlayerResolver` 의 ready event 에 구독, Player 가 register 되면 그 시점에 PlayerRelicInventory subscribe:

```csharp
private void OnEnable()
{
    LocalPlayerResolver.OnLocalPlayerReady += HandleLocalPlayerReady;
    TryBind(); // 이미 ready 상태일 수 있음 — 시도
}

private void OnDisable()
{
    LocalPlayerResolver.OnLocalPlayerReady -= HandleLocalPlayerReady;
    if (_inventory != null) _inventory.OnTryAddRejected -= HandleRejected;
}

private void HandleLocalPlayerReady() => TryBind();

private void TryBind()
{
    if (_inventory != null) return;
    _inventory = LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerRelicInventory>();
    if (_inventory == null) return;
    _inventory.OnTryAddRejected += HandleRejected;
}
```

(b) **`LocalPlayerResolver` 에 `OnLocalPlayerReady` event 추가** — 현재는 register/unregister 만 있고 event 없음. `Register(stateAggregator)` (line 131) 호출 후 `OnLocalPlayerReady?.Invoke()` 발화. unregister 도 동일하게 `OnLocalPlayerCleared` 또는 동일 event 재발화.

```csharp
// LocalPlayerResolver.cs
public static event Action OnLocalPlayerReady;
public static void Register(KhiPlayerStateAggregator agg)
{
    _localAggregator = agg;
    OnLocalPlayerReady?.Invoke();
}
```

**기대 효과:** 5×5 grid 가득 차서 `TryAdd` reject 시 `OnTryAddRejected` 이벤트 → modal 정상 표시 → swap UI 동작.

**주의:** modal 의 5×5 swap UI 자체 구현은 이미 존재한다고 사용자 진술. 본 fix 는 wiring(bind) 만 복구.

---

### Fix 6 — NetworkAnimator NullReferenceException

**원인:** Player prefab/씬에 NetworkAnimator 컴포넌트는 있지만 `m_Animator` field 가 비어있음 (`{fileID: 0}`). 패키지 코드 (`com.unity.netcode.gameobjects/Runtime/Components/NetworkAnimator.cs:412, 524`) 의 `OnValidate` / `BuildTransitionStateInfoList` 에서 NRE.

**대상:** Unity 에디터 내 prefab 작업 — **코드 수정 아님**.

**확인/수정 절차:**
1. `Assets/_Project/Prefabs/` 또는 `Assets/_Project/Scenes/Town_solo_Copy.unity`, `Assets/_Project/Scenes/Dungeon*.unity` 등에서 Player GameObject 찾기.
2. Inspector → NetworkAnimator 컴포넌트 → Animator field 가 비어있으면 같은 GameObject 의 Animator 컴포넌트 drag 해서 할당.
3. Apply prefab (씬 placed instance 라면 Overrides → Apply All).
4. 에디터 재시작 후 OnValidate NRE 4회 발생 안 하는지 콘솔 확인.

**참조:** 사용자 메모리 [feedback_unity_scene_player_audit.md] — "Player" 키워드 grep 만으론 부족, prefab GUID + NetworkObject script GUID 까지 검증.

---

## 작업 순서 권장

1. **Fix 6 (NetworkAnimator NRE)** — 에디터 inspector 작업, 코드 변경 없음 → 빠르게 처리.
2. **Fix 1 (IsOwner gate)** — 가장 큰 root cause. 공격 더블 입력 + RPC overflow 동시 해소.
3. **Fix 2 (weaponData fallback)** — Fix 1 후에도 host→guest 비대칭 잔존 시 적용. 인스펙터 점검 우선.
4. **Fix 3 (PlayerHealthSync skip)** — 작은 변경, 영향 범위 명확.
5. **Fix 4 (Reward panel)** + **Fix 5 (InventoryFullModal)** — `LocalPlayerResolver` 의 ready event 추가가 공통 의존. 함께 작업.

---

## Critical Files to Modify

| 파일 | 변경 내용 |
|------|-----------|
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs` | Update 진입부 IsOwner gate |
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiStaffController.cs` | Update 진입부 IsOwner gate |
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiFlamethrowerController.cs` | Update 진입부 IsOwner gate |
| `Assets/_Project/Scripts/Runtime/Networking/Player/PlayerMovementSync.cs` | DisableInputComponentsForNonOwner 에 Staff/Flame 추가 |
| `Assets/_Project/Scripts/Runtime/Networking/Player/AttackBroadcast.cs` | weaponData fallback + null guard |
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiArrowProjectile.cs` | PlayerHealthSync skip (line 174 부근 + homing path) |
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeHitbox.cs` | PlayerHealthSync skip (line 94 부근) |
| `Assets/_Project/Scripts/Runtime/Rewards/RewardPanelView.cs` | LocalPlayer lazy resolve |
| `Assets/_Project/Scripts/Runtime/Shop/InventoryFullModal.cs` | OnEnable lazy bind via LocalPlayerResolver event |
| `Assets/_Project/Scripts/Runtime/Networking/Player/LocalPlayerResolver.cs` | `OnLocalPlayerReady` event 추가 |
| Player prefab / 씬 (에디터 작업) | NetworkAnimator.m_Animator 필드 할당 |

## Reuse 대상 (기존 utility)

- `LocalPlayerResolver.GetComponentOnLocalPlayer<T>()` — Fix 4/5 에서 활용.
- `PlayerHealthSync` 컴포넌트 — Fix 3 의 sentinel 로 사용 (별도 marker component 추가 불필요).
- `CombatTargetable.IsAuthoritativePlayer` (MagicalGirl 경로에서 검증됨) — 참조 패턴으로 PlayerHealthSync 체크 함수 작성 시 참고.
- `WeaponModeController.GetWeaponDataForCurrentMode()` (있다면) — Fix 2 의 fallback. 없으면 Mode → WeaponData 매핑 신규 추가.

---

## Verification

### A. 빌드 후 host + guest 2-client smoke test

1. **공격 더블 입력** (Fix 1)
   - host 가 좌클릭 → host log 만 `[KhiMeleeCombo] RequestAttack`. guest log 에 없어야 함.
   - guest 가 좌클릭 → 반대.
   - 합쳐서 RPC 호출 횟수: 좌클릭 1회 당 ServerRpc 1회 × 이벤트 3개 = 3회 (4명 시나리오면 ×4 가 아닌 ×1).

2. **공격 시각 sync** (Fix 2)
   - host 좌클릭 → guest 화면에서 host player 의 slash 애니메이션 + weapon swing 보임.
   - guest 좌클릭 → host 화면에서 동일.
   - `[AttackBroadcast] ClientRpc received event=...` 로그가 양측 모두 찍히고 `step != null` 처리.

3. **Staff 발사체 friendly-fire** (Fix 3)
   - host 가 staff 모드로 guest player 향해 발사 → guest HP 감소 없음.
   - guest → host 도 동일.
   - 로그 `[KhiArrowProjectile] skip player target ...` 출현.
   - 일반 몹에 대한 데미지는 정상 (회귀 없음).

4. **Reward panel** (Fix 4)
   - 보스 처치 → host & guest 양측 화면 모두 reward panel 표시.
   - 각자 카드 선택 → 본인 inventory 에만 add (`[PlayerRelicInventory] Added` 로그 host 와 guest 다른 item).

5. **InventoryFullModal** (Fix 5)
   - 인벤토리 16/25 칸 가득 채운 상태에서 새 아이템 획득 시도 → swap modal 표시.
   - 회귀 검증: **per-client inventory display 정상 유지** (사용자 요청).

6. **NetworkAnimator NRE** (Fix 6)
   - 에디터 재시작 시 콘솔에 NRE 4회 없음.

### B. Receive queue overflow 모니터링

- host log 의 `Receive queue is full, some packets could be dropped` 경고가 1분간 0회.

### C. 회귀 검증 체크리스트

- [ ] WeaponModeController 모드 전환이 양측 sync (Phase B 기능).
- [ ] MagicalGirl T 스킬 host-only spawn 유지 (Phase C 기능).
- [ ] PlayerHealthSync 자체 데미지 sync 동작 (Phase A 기능).
- [ ] **per-client inventory 표시** — 회귀 절대 금지 (사용자 명시).

---

## Open Items / Follow-up (이 plan 외)

- `KhiMeleeComboController` / `KhiStaffController` 의 `NetworkBehaviour` 본격 변환 (현재는 cached `NetworkObject` lookup 으로 최소 침습). 향후 입력 buffering / lag compensation 추가 시 정식 변환 필요.
- AttackBroadcast 의 `ActiveStarted` 매 frame broadcast 가 정말 필요한지 — 현재는 owner 1명분이라 무시할 만하지만 4인 시나리오에서 다시 검토.
- `LocalPlayerResolver.OnLocalPlayerReady` event 추가는 다른 UI 컴포넌트 (예: HealthBarBinder 등) 에서도 활용 가능 — 의존성 audit 후 확장.

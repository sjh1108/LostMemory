# CL-029: 다운·부활 상태 동기화 구현

소속 Epic: **Epic C. 멀티플레이 / 세션 네트워크**

선행: CL-014 (솔로 다운/부활 구현), CL-022 (NetworkObject 분기 진입점), CL-027 (방향·행동 상태 동기화), CL-028 (공격 판정·적 반응 동기화)

## Context

CL-014에서 솔로 기준 Down/Revive/Defeat 상태머신과 permits 토글 / Revive Zone / Debug Respawn까지 완성됐다([cl014_player_down_revive_implementation.md](../../client/docs/khi/cl014_player_down_revive_implementation.md)). 그러나 그 흐름은 **자기 인스턴스 안에서만** 진실 소스이고, 멀티 환경에서 다음이 깨진다:

- 한쪽 클라가 트랩에 맞아 다운돼도 상대 클라 화면에선 그대로 서 있음
- 상대가 다운된 줄 모르니 부활 상호작용이 의미 없음
- Down 타이머가 양쪽에서 별도로 흘러 부활/사망 시점 불일치
- 솔로 모드 enum (`KhiDownSoloBehavior`)이 멀티에서 의미 없는데도 진입점에서 평가됨

CL-022 권한 표는 본 영역을 **서버 권위**로 못박았다 ([cl022_player_network_prefab_plan.md:52](cl022_player_network_prefab_plan.md)):

> Down 상태 — 서버 권위. NetworkVariable<bool> IsDowned. 부활 상호작용은 Owner ServerRpc.

본 CL은 그 정책을 실제 코드로 구현한다. 결과:

- 서버(호스트)가 Down/Revive/Defeat **결정 단일 주체**
- 모든 클라이언트 인스턴스는 NetworkVariable 값 변화를 mirror해서 동일 상태 표시
- 부활 상호작용은 Owner의 ServerRpc로 서버가 판정·진행도 관리
- 멀티 모드에서 솔로 enum은 우회

비목표:
- 4인 다운/전멸 룰 — `docs/04_multiplayer.md:31` "전원 다운 시 패배"는 **상위 게임 진행 시스템**이 본 NetworkVariable을 폴링해 처리. 본 CL은 개인 단위 Down/Revive/Defeat까지.
- Down/Revive UI(타이머 바, 화면 오버레이) — CL-016 영역. 본 CL은 진실 소스 + 이벤트만.
- 보스/적 패턴이 다운된 플레이어를 무시할지 여부 — AI 영역
- 호스트 이주(Migration) — 호스트 이탈은 CL-025 정책상 세션 종료
- TDE `CharacterConditions.Down` 승격 — CL-015 미완 영역

## 현재 상태 (의존)

| 항목 | 상태 |
|---|---|
| `KhiDownController` 상태머신 | 솔로 OK ([KhiDownController.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs)) |
| `KhiDownController` 이벤트(`DownEntered`, `ReviveProgressChanged`, `ReviveCompleted`, `DefeatedByTimeout`, `DefeatedSolo`, `DebugRespawned`) | 솔로 OK |
| `KhiDownController.ForceRevive` / `ForceDefeat` / `DebugRespawn` 외부 진입점 | 솔로 OK |
| `KhiParryController.ForceIdle()` / `KhiHitStunController.ForceExit()` 정리 훅 | 솔로 OK |
| `TestKhiReviveZone` (BoxCollider2D + E 키) | 솔로 OK, 자기 자신 진입 가정 |
| `TestKhiSceneBootstrap` P 키 폴링(Defeated 비활성 GameObject 대응) | 솔로 OK |
| `KhiPlayerNetworkSync` (CL-027) NetworkVariable 패턴 | NGO 2.x 기준 정립 |
| `NetworkPlayerInitializer` Remote 분기(CL-022) | KhiPlayerNetworkSync.EnableRemoteOverride 호출 패턴 정립 |
| Health 데미지 서버 권위 / OnHit 발사 시점 | **CL-028에서 확정** — 본 CL은 그 결과에 의존 |

## 동기화 대상

| 트랙 | 데이터 | 빈도 | 메커니즘 | 권한 |
|---|---|---|---|---|
| 1. Down/Defeated 상태 | `KhiDownState` enum 3값 → `byte` | 전이 시점만 | `NetworkVariable<byte>` | 서버 |
| 2. Down 타이머 종료 시각 | `float NetworkTime` (서버 시각) | 1회 (Down 진입 시점에만) | `NetworkVariable<double>` | 서버 |
| 3. Revive 진행도 | `float` 0~1 (`holdDuration > 0`일 때만) | 진행 중 매 tick | `NetworkVariable<half>` | 서버 |
| 4. Defeat 사유 | `KhiDefeatReason` enum 2값 (Timeout/Solo) → `byte` | 전이 시점만 | `NetworkVariable<byte>` | 서버 |

**왜 NetworkTime 종료 시각**:
- Remote 측에서 매 프레임 `Time.time`을 동기화하면 노이즈가 큼
- 서버가 진입 시점에 `endTime = NetworkManager.ServerTime.Time + duration` 한 번만 발사
- 모든 클라가 같은 NetworkTime 도메인에서 `remaining = endTime - NetworkTime`로 동일 카운트다운 표시 (UI는 CL-016)

**왜 ClientRpc 미사용**:
- 늦게 합류한 클라가 현재 다운 상태를 알아야 함 → NetworkVariable 자동 spawn-sync
- DownEntered 같은 이벤트는 NetworkVariable.OnValueChanged 콜백이 대신 발사

**Defeat 사유를 별도 트랙으로 분리한 이유**:
- 솔로 즉사(`DefeatedSolo`)와 타임아웃 사망(`DefeatedByTimeout`)은 분석/연출에서 구분 필요
- enum 합쳐서 5값 byte로 직렬화도 가능하나, 가독성을 위해 별도 트랙 권장(작업자 판단으로 합쳐도 OK)

## 흐름 설계

### 트랙 A: 데미지로 인한 Down/Defeat 진입

```
[Server]  Health.Damage → CurrentHealth <= 0
[Server]  KhiDownController.HandleHealthHit (서버 인스턴스만)
[Server]  ResolveAllyContext()  // 멀티 모드: NetworkManager.ConnectedClients에 살아있는 다른 플레이어가 1명 이상이면 Down
                             // 솔로 모드(호스트 단독): 기존 enum 따름
[Server]  Down 결정 → EnterDown()
            ↓
[Server]  KhiPlayerNetworkSync.DownState.Value = (byte)Down
[Server]  KhiPlayerNetworkSync.DownTimerEnd.Value = ServerTime.Time + downDuration
            ↓
          NGO 자동 replicate
            ↓
[Owner & Remote OnValueChanged]
          KhiDownController.ApplyRemoteDown(duration)
            ↓
          기존 EnterDown 로직 일부(permits, ForceIdle, ForceExit, animator) 재사용
            ↓
          DownEntered?.Invoke(duration)  // UI/SFX 구독자가 자연스럽게 받음
```

### 트랙 B: Down 타이머 만료 (서버 주도)

```
[Server Update]  if NetworkManager.ServerTime.Time >= DownTimerEnd
                 → KhiDownController.EnterDefeatedByTimeout()
                 → KhiPlayerNetworkSync.DownState.Value = (byte)Defeated
                 → KhiPlayerNetworkSync.DefeatReason.Value = (byte)Timeout
                       ↓
[All]            OnValueChanged → ApplyRemoteDefeat(reason)
                       ↓
                 ExecuteDefeat 일부(permits 복원/Health.Kill 트리거) 재사용
```

핵심: 클라가 자기 시계로 만료 판정하지 않음. 서버가 단일 결정. 클라는 *표시*만.

### 트랙 C: 부활 상호작용

```
[Owner: 살아있는 동료]  TestKhiReviveZone OnTriggerStay (target = downed player)
                      E 키 입력 감지
                      → TestKhiReviveZone.RequestRevive()
                          ↓
[Owner → Server]      KhiPlayerNetworkSync.RequestReviveServerRpc(targetClientId)
                          ↓
[Server]              검증: target이 Down 상태인가? Owner와 거리 <= 트리거 반경 + grace?
                      검증 통과 → KhiDownController(target 서버 인스턴스).TryBeginRevive(reviverGameObject)
                          ↓
                      holdDuration == 0 → 즉시 CompleteRevive
                      holdDuration > 0  → 매 tick 진행도 갱신
                                          KhiPlayerNetworkSync.ReviveProgress.Value = current
                                          → 모든 클라 ProgressChanged 이벤트
                          ↓
                      CompleteRevive → ForceRevive → DownState=Normal, ReviveProgress=0
                          ↓
[All]                 OnValueChanged → ApplyRemoteRevive()
                                       → permits 복원 + Health.SetHealth + ReviveCompleted 이벤트
```

**Hold 진행 중 이탈 처리**:
- Owner가 Zone에서 벗어남 → ServerRpc(CancelReviveServerRpc) → 서버가 KhiDownController.CancelRevive
- 서버가 자체 검증(거리 매 tick 재확인)도 추가 권장 — 클라 신뢰만으로는 치팅/lag race 가능

### 트랙 D: 솔로 즉사 (호스트 단독 + ImmediateDefeat 모드)

```
[Server]  ResolveAllyContext() → 살아있는 동료 0명
[Server]  soloBehavior == ImmediateDefeat → 바로 EnterDefeatedSolo
[Server]  KhiPlayerNetworkSync.DownState.Value = Defeated
          KhiPlayerNetworkSync.DefeatReason.Value = Solo
              ↓ (NetworkVariable 합류 sync 위해 1회 set 후 0으로 안 되돌림 — 부활 시 Normal로 갱신)
[All]     ApplyRemoteDefeat(Solo)
```

호스트 혼자(singleton) 플레이는 NetworkManager.IsHost && ConnectedClientsCount==1.

## 작업 범위

### Step 1 — 데이터 형식 결정

`KhiDownState`: 현재 enum 3값(Normal/Down/Defeated). 0~2 연속 → byte 캐스팅 OK.

`KhiDefeatReason` 신규 enum:
```text
public enum KhiDefeatReason : byte {
    None = 0,
    Timeout = 1,
    Solo = 2,
}
```

`Assets/_Project/Scripts/Runtime/Networking/PlayerNetTypes.cs` 확장(CL-027에서 신설):
- `ToByte/FromByte(KhiDownState)`
- `ToByte/FromByte(KhiDefeatReason)`

### Step 2 — KhiPlayerNetworkSync 확장 (CL-027 자산)

`Assets/_Project/Scripts/Runtime/Networking/KhiPlayerNetworkSync.cs` 추가 NetworkVariable:

```text
[SerializeField] private KhiDownController downController;

public NetworkVariable<byte>   DownState     = new(0, Everyone, Server);
public NetworkVariable<double> DownTimerEnd  = new(0d, Everyone, Server);
public NetworkVariable<half>   ReviveProgress= new(default, Everyone, Server);
public NetworkVariable<byte>   DefeatReason  = new(0, Everyone, Server);
```

`OnNetworkSpawn`에 추가:

```text
DownState.OnValueChanged       += OnDownStateChanged;
DefeatReason.OnValueChanged    += OnDefeatReasonChanged;
ReviveProgress.OnValueChanged  += OnReviveProgressChanged;

// 합류 시 현재값 1회 강제 적용
ApplyRemoteDownState((KhiDownState)DownState.Value, DownTimerEnd.Value);
ApplyRemoteDefeatReason((KhiDefeatReason)DefeatReason.Value);
ApplyRemoteReviveProgress((float)ReviveProgress.Value);
```

서버 측만 동작하는 push:

```text
public override void OnNetworkSpawn() {
    if (IsServer) {
        downController.DownEntered    += SrvOnLocalDownEntered;
        downController.ReviveCompleted+= SrvOnLocalReviveCompleted;
        downController.DefeatedByTimeout += () => SrvSetDefeat(KhiDefeatReason.Timeout);
        downController.DefeatedSolo      += () => SrvSetDefeat(KhiDefeatReason.Solo);
        downController.ReviveProgressChanged += p => ReviveProgress.Value = (half)p;
    }
    // 모든 인스턴스(Owner 포함)가 OnValueChanged 구독
    ...
}

void SrvOnLocalDownEntered(float duration) {
    DownState.Value      = (byte)KhiDownState.Down;
    DownTimerEnd.Value   = NetworkManager.ServerTime.Time + duration;
    DefeatReason.Value   = (byte)KhiDefeatReason.None;
    ReviveProgress.Value = (half)0f;
}

void SrvSetDefeat(KhiDefeatReason r) {
    DownState.Value    = (byte)KhiDownState.Defeated;
    DefeatReason.Value = (byte)r;
}

void SrvOnLocalReviveCompleted(GameObject _) {
    DownState.Value      = (byte)KhiDownState.Normal;
    DefeatReason.Value   = (byte)KhiDefeatReason.None;
    ReviveProgress.Value = (half)0f;
}
```

### Step 3 — Revive ServerRpc

`KhiPlayerNetworkSync.cs` 메서드 추가:

```text
[ServerRpc(RequireOwnership = false)]
public void RequestReviveServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default) {
    var reviverClientId = rpcParams.Receive.SenderClientId;
    if (!ResolveTargetSync(targetClientId, out var targetSync)) return;
    if (!targetSync.downController.IsDown) return;
    if (!ValidateReviverProximity(reviverClientId, targetSync)) return;

    var reviverObject = NetworkManager.ConnectedClients[reviverClientId].PlayerObject?.gameObject;
    targetSync.downController.TryBeginRevive(reviverObject);
}

[ServerRpc(RequireOwnership = false)]
public void CancelReviveServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default) {
    if (!ResolveTargetSync(targetClientId, out var targetSync)) return;
    var reviverObject = NetworkManager.ConnectedClients[rpcParams.Receive.SenderClientId].PlayerObject?.gameObject;
    targetSync.downController.CancelRevive(reviverObject);
}
```

`ValidateReviverProximity`는 서버 측 두 NetworkObject의 Transform 거리 측정. 트리거 콜라이더 반경 + grace(0.5유닛 권장) 사용.

### Step 4 — KhiDownController에 Remote mirror APIs 추가

`Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs` 수정:

```text
public bool IsRemoteOverride { get; private set; }

public void EnableRemoteOverride() {
    IsRemoteOverride = true;
    // OnHit 자동 가로채기 비활성 (서버만 결정)
    if (health != null) health.OnHit -= HandleHealthHit;
}

public void ApplyRemoteDown(float duration) {
    if (_state == KhiDownState.Down) return;
    EnterDownInternal(duration); // 기존 EnterDown을 internal로 분리, 솔로 enum 평가 우회
}

public void ApplyRemoteDefeat(KhiDefeatReason reason) {
    if (_state == KhiDownState.Defeated) return;
    ExecuteDefeatInternal(reason); // 기존 ExecuteDefeat 분리. Health.Kill은 서버에서만 호출됨에 주의
}

public void ApplyRemoteRevive() {
    if (_state == KhiDownState.Normal) return;
    CompleteReviveInternal(reviver: null, healthOverride: -1f, isRemoteMirror: true);
}
```

**중요 분리 원칙**:
- 서버 인스턴스: 기존 `EnterDown`/`ExecuteDefeat`/`CompleteRevive` 그대로 (Health.Kill / Health.SetHealth 포함)
- 비서버(Owner/Remote) 인스턴스: `*Internal`은 permits·animator·이벤트만, Health 조작은 안 함 (Health는 별도 NetworkVariable로 동기화 — CL-028)

`isRemoteMirror=true` 분기에서 SetHealth/Kill 스킵.

### Step 5 — NetworkPlayerInitializer 보강

`NetworkPlayerInitializer.OnNetworkSpawn` (CL-022)에 추가:

```text
if (!IsServer) {
    GetComponent<KhiDownController>().EnableRemoteOverride();
}

if (!owner) {
    // CL-027 RemoteOverride에 더해
    GetComponent<TestKhiReviveZone>()?.SetEnabled(true); // 자기 다운 시 동료가 부활 가능
}
```

여기서 `EnableRemoteOverride`는 **Owner 본인 인스턴스에서도 호출**됨에 유의 — 다운 결정은 서버만, Owner도 자기 결정 안 함.

### Step 6 — TestKhiReviveZone 멀티 분기

`Assets/_Project/Scripts/Runtime/TestKhi/TestKhiReviveZone.cs` 수정:

```text
[SerializeField] private KhiPlayerNetworkSync ownerSync; // 부착된 Player의 Sync

void Update() {
    if (_occupant == null) return;
    if (!Input.GetKeyDown(interactKey)) return;

    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) {
        // 멀티 모드: Reviver(현재 Zone 진입자)의 NetworkObject가 자기 ServerRpc 호출
        var reviverNet = _occupant.GetComponentInParent<NetworkObject>();
        if (reviverNet == null || !reviverNet.IsOwner) return; // 내 캐릭터일 때만 입력 처리
        var targetClientId = ownerSync.OwnerClientId;
        reviverNet.GetComponent<KhiPlayerNetworkSync>()
                  .RequestReviveServerRpc(targetClientId);
    }
    else {
        // 솔로 모드 기존 흐름
        downController.TryBeginRevive(_occupant);
    }
}
```

`holdDuration > 0` 경로는 매 tick `RequestReviveServerRpc`를 보내지 말 것 — 시작 1회 + 이탈 시 Cancel 1회. 진행도는 서버가 NetworkVariable로 push.

Zone은 다운된 플레이어 자신의 자식 GameObject로 두는 게 자연. CL-014 Bootstrap 코드는 씬 단독 Zone을 생성하므로, **Zone 부착 위치를 플레이어 프리팹으로 이전**하거나 멀티 분기에서 동적 생성.

권장: **플레이어 프리팹에 Zone 자식으로 미리 부착** + Down 시 활성, Normal 시 비활성.

### Step 7 — TestKhiSceneBootstrap 멀티 분기

기존 P 키 Debug Respawn 폴링은 솔로 한정. 멀티에선:
- **Defeat 후 자동 부활 X** — 멀티는 세션 단위 로직(전원 다운 → 패배). P 키 폴링 멀티 모드에선 OFF.

```text
void Update() {
    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;
    // 솔로 P 키 기존 흐름
}
```

### Step 8 — 디버그 검증 도구

`Assets/_Project/Scripts/Runtime/Networking/PlayerNetSyncDebugOverlay.cs` (CL-027 선택 자산) 확장:

```text
[Player 0] Owner=true  State=Down    Timer=4.21s  Revive=0.00  Reason=None
[Player 1] Owner=false State=Normal  Timer=-      Revive=0.00  Reason=None
```

서버 측에 추가 라인: `[Server view] Decision=Down Reason=Lethal AllyCount=1`

### Step 9 — 검증 시나리오

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 호스트가 트랩으로 HP 0 도달 (멀티 2인) | 양쪽 화면에서 호스트 캐릭터 Down 시각, Down 타이머 동기 흐름 |
| 2 | 클라가 트랩으로 HP 0 도달 | 양쪽 화면에서 클라 캐릭터 Down, 클라 본인 입력 차단, 호스트도 클라 Down 인지 |
| 3 | Down 중인 동료 옆에서 E 누름 (`holdDuration=0`) | 양쪽 화면에서 즉시 Revive, HP MaxHealth×0.2 표시(CL-028 의존), 입력 재개 |
| 4 | Down 중 10초 미입력 | 양쪽 화면 동시 Defeated 전이, DefeatReason=Timeout |
| 5 | 호스트 단독(클라 미접속) HP 0 | ImmediateDefeat 모드 시 즉시 Defeated, Solo enum |
| 6 | 호스트와 클라 모두 다운 | 양쪽 모두 Down 표시(개별 NetworkVariable). 패배 판정은 본 CL 비범위 |
| 7 | 합류 시점에 호스트가 이미 Down 중 | 늦게 들어온 클라가 호스트 캐릭터를 Down 상태로 표시 + 잔여 타이머 일관 |
| 8 | `holdDuration=2.0` 설정 후 동료 부활 시도 | 양쪽 화면에서 진행도 0→1 동기화, 중간에 Zone 이탈 시 진행도 0으로 복귀 |
| 9 | Revive 진행 중 동료가 다른 동료 사망 → 부활 시도 끊김 | CancelReviveServerRpc 도달, 진행도 0 |
| 10 | 호스트 Down 중 클라가 호스트에 다가가 부활 | ServerRpc 거리 검증 통과, Revive 완료 |
| 11 | 클라가 호스트에서 5유닛 떨어진 곳에서 E 키(거리 검증 실패 케이스) | 서버가 ServerRpc 거부, 양쪽 상태 변화 0, 조작 무시 |
| 12 | UnityTransport Network Simulator 100ms + 5% loss | 1~10 시나리오 약간 지연되지만 결과적 동기 도달 |

## 비범위

- **데미지 적용 자체** — CL-028 (Health 서버 권위 ServerRpc)
- **전원 다운 → 패배** 게임 진행 처리 — 상위 Run 매니저
- **Down/Revive UI** — CL-016
- **Down 중 적의 행동 변화(추격 무시 등)** — AI 영역
- **호스트 이탈 중 다운된 캐릭터 처리** — CL-025 (세션 종료)
- **TDE `CharacterConditions.Down` 승격** — CL-015 영역
- **PlayerCombatReporter 글로벌 이벤트** — `Player.Down`/`Player.Revive`/`Player.Defeat` 키 (`docs/commonness/global-event-keys-and-hook-points.md`) 발행은 본 CL 후기 또는 별도 CL
- **재접속 부활** — `docs/04_multiplayer.md:40` MVP 미지원

## 사용자 결정 필요

| 항목 | 옵션 | 권장 |
|---|---|---|
| Down 타이머 권위 | 서버가 ServerTime 기반 / 각 클라 자기 시계 | **서버 ServerTime** (드리프트 없음) |
| Defeat 사유 직렬화 | 별도 NetworkVariable / DownState enum 5값 통합 | **별도 트랙** (가독성) |
| Revive Zone 부착 위치 | 씬 동적 생성(현재) / 플레이어 프리팹 자식 | **플레이어 프리팹 자식**, Down 상태에서만 활성 |
| Hold 진행도 직렬화 | half(2B) / float(4B) / byte 0~255(1B) | **half** (정밀도·대역폭 균형) |
| Revive 거리 검증 | Zone 트리거만 / 서버 측 거리 재검증 | **둘 다** (lag race 방지) |
| 솔로 enum 평가 | 서버 단독 플레이에서 사용 / 항상 멀티 룰 | **서버 단독일 때만** (현재 enum 그대로 유지) |
| Owner 본인 다운 체감 | 서버 통신 왕복 후 화면 반영 / 즉시 시각만 발사 후 검증 | **왕복 후** (권한 일관성 우선) |
| Health.Kill 호출 위치 | 모든 인스턴스 / 서버 단독 | **서버 단독** (Health도 서버 권위) |

## 위험 요소

1. **EnterDown permits 토글이 Remote 인스턴스에서도 동작해야 함** — Remote 인스턴스의 `dashController`/`meleeCombo`는 CL-022에서 비활성. 비활성 컴포넌트의 `PermitAbility`/`AbilityPermitted` 호출이 안전한지 확인 필요. 안전하지 않으면 Remote에선 permits 토글 스킵하고 animator/event만 처리. CL-027 RemoteOverride와 동일 결.
2. **Health.Damage가 Remote에서 호출되면 OnHit이 또 발사돼 이중 EnterDown** — 서버 단독으로 Damage 호출 보장 필요. CL-028 의존. 본 CL은 `EnableRemoteOverride()`에서 OnHit 구독 해제로 1차 방어.
3. **NetworkVariable 초기값 vs OnValueChanged 미발사** — Spawn 직후 값이 같으면 콜백 안 옴. `OnNetworkSpawn` 끝에 `ApplyRemoteDownState(...)` 강제 1회 호출 필수(Step 2 코드 반영).
4. **DownTimerEnd가 서버 시각** — 클라가 `Time.time`으로 카운트다운하면 NetworkTime 도메인 차이로 어긋남. 모든 UI 코드는 `NetworkManager.Singleton.ServerTime.Time`을 기준으로 `remaining` 계산.
5. **Defeat→Health.Kill 시점에 GameObject 비활성** — TDE `Health.Kill()`이 `DestroyOnDeath` 플래그로 GameObject SetActive(false). NetworkObject도 비활성 → NetworkVariable 변화가 더 이상 흐르지 않음. 멀티에선 `DestroyOnDeath=false`로 두고 시각만 사망 처리 권장. CL-014의 Debug Respawn 경로는 솔로 한정.
6. **TestKhiReviveZone Owner 판정** — Zone에 들어간 `_occupant`가 자기 캐릭터일 수도, 동료 캐릭터일 수도 있음. 본 CL의 부활 의미는 **동료 → 다운된 자기 자신**이 아닌 **다운된 동료 → 동료**. Zone OnTriggerEnter에서 occupant.GetComponent<NetworkObject>().IsOwner 검증 필수.
7. **Revive 진행 중 reviver 사망/다운** — 서버에서 매 tick `reviver.IsDown` 체크 + true면 자동 Cancel. KhiDownController.CancelRevive 호출.
8. **2 NetworkVariable 동시 변경 시 OnValueChanged 순서** — DownState와 DownTimerEnd 둘 다 set하면 OnValueChanged가 어느 것 먼저? NGO는 변수별 독립 발사. State가 Down으로 바뀌고 Timer가 0d 그대로면 1프레임 깜빡임. 해결: 항상 Timer 먼저 set 후 State set (코드 순서로 강제).
9. **half precision 0~1 범위** — half2(CL-027)와 별개로 단일 half 사용. NGO 직렬화 미지원 시 byte 0~255로 fallback.
10. **솔로 ImmediateDefeat에서 NetworkVariable 변경 ServerTime 0 미만** — 서버가 곧장 Defeated set. 합류 sync 정상 동작 검증.
11. **DebugRespawn 멀티 비활성으로 데모 중 사망 시 복구 없음** — 멀티 데모 중 모든 클라 Defeat 시 세션 재시작만이 복구. 본 CL 기준 OK이나 데모 운영 시 호스트가 한 번에 자기 세션 종료 → 재생성 흐름 권장.

## 변경/신규 파일

### 신규

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/KhiDefeatReason.cs` | enum 정의 (또는 PlayerNetTypes.cs에 합치기) |

### 변경

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/KhiPlayerNetworkSync.cs` | DownState/DownTimerEnd/ReviveProgress/DefeatReason NetworkVariable 4종 + 서버 push 핸들러 + RequestReviveServerRpc/CancelReviveServerRpc |
| `Assets/_Project/Scripts/Runtime/Networking/PlayerNetTypes.cs` | KhiDownState/KhiDefeatReason byte 헬퍼 |
| `Assets/_Project/Scripts/Runtime/Networking/NetworkPlayerInitializer.cs` | Server/Remote 분기로 KhiDownController.EnableRemoteOverride 호출 + ReviveZone 부착·활성 토글 |
| `Assets/_Project/Scripts/Runtime/Networking/PlayerNetSyncDebugOverlay.cs` | DownState/Timer/ReviveProgress 라인 추가 |
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs` | `IsRemoteOverride`, `EnableRemoteOverride`, `ApplyRemoteDown`, `ApplyRemoteDefeat`, `ApplyRemoteRevive` + 기존 `EnterDown`/`ExecuteDefeat`/`CompleteRevive` 내부 분리(`*Internal`, `isRemoteMirror` 플래그) |
| `Assets/_Project/Scripts/Runtime/TestKhi/TestKhiReviveZone.cs` | 멀티 분기: NetworkManager 활성 시 ServerRpc 경유, 솔로 시 기존 `TryBeginRevive` |
| `Assets/_Project/Scripts/Runtime/TestKhi/TestKhiSceneBootstrap.cs` | 멀티 모드에서 P 키 Debug Respawn 비활성, Revive Zone 동적 생성 분기 제거(프리팹 부착으로 이전) |
| `Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` | Revive Zone 자식 GameObject 부착(BoxCollider2D trigger + TestKhiReviveZone), 기본 비활성 |

### 미변경

- `KhiParryController.ForceIdle` / `KhiHitStunController.ForceExit` 정리 훅 — 본 CL 영향 없음
- TDE `Health` / `Character` 본체
- 적 AI / 트랩 데미지 코드
- 기타 게임 콘텐츠 / 룸 / 보상

## 검증 (E2E)

1. CL-021 씬 솔로 Play — `KhiDownSoloBehavior` 3가지 모드 모두 회귀 0 (R 키 / P 키 / 즉사 OK)
2. Multiplayer Play Mode 2 인스턴스 (Different Auth Profile) Host + Join
3. 검증 시나리오 1~10 모두 통과 (각 시나리오 양쪽 Editor에서 확인)
4. UnityTransport Network Simulator 100ms + 5% loss → 시나리오 12 통과
5. 합류 직전 호스트가 Down 상태 → 클라 합류 즉시 호스트 Down 시각 + 잔여 타이머 일관 (시나리오 7)
6. PlayerNetSyncDebugOverlay에서 양쪽 인스턴스의 DownState/Timer/Reason 일관 (lag < 100ms)
7. ServerRpc 거리 검증 실패 케이스(시나리오 11)에서 콘솔 경고 출력 + 상태 변화 없음
8. 5회 반복 stress 후 콘솔 에러 0
9. Defeat 후 GameObject가 비활성되지 않음 확인(`DestroyOnDeath` OFF 또는 멀티 분기)
10. Health 서버 권위(CL-028) 결합 후 회귀 — Down 진입 시 양쪽 HP 표시 0, Revive 후 양쪽 HP 표시 MaxHealth×0.2

## 관련 문서

- `docs/04_multiplayer.md:29-32` — 다운/전멸 규칙
- `docs/12_development_plan.md:232-244` — 호스트 권위 분기 원칙
- `docs/14_client_jira_story_backlog.md` — CL-029 정의
- `docs/khi/cl022_player_network_prefab_plan.md:52` — Down 권한 모델 표
- `docs/khi/cl027_direction_state_sync_plan.md` — KhiPlayerNetworkSync 패턴 + RemoteOverride
- `client/docs/khi/cl014_player_down_revive_implementation.md` — 솔로 다운/부활 솔리드 구현 기록
- `docs/commonness/global-event-keys-and-hook-points.md` — `Player.Down` / `Player.Revive` / `Player.Defeat` 글로벌 이벤트 키(승격은 후속)

## 다음 CL

- **CL-030**: 참가 실패·접속 오류 예외 흐름 — 본 CL의 Down/Defeat 흐름과 별개로, 참가 단계 자체에서의 데모 실패 방지.
- (후속) **PlayerCombatReporter 멀티 통합** — 본 CL의 NetworkVariable 변화를 글로벌 이벤트(`Player.Down`/`Player.Revive`/`Player.Defeat`)로 라이즈해서 룸 클리어/UI/스토리 진행 모듈이 통일된 hook으로 받게 함.

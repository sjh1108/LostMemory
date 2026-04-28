# CL-027: 플레이어 방향·행동 상태 동기화 구현

소속 Epic: **Epic C. 멀티플레이 / 세션 네트워크**

선행: CL-020~026 (특히 CL-022 NetworkObject/Initializer, CL-026 위치 동기화 polish)

## Context

CL-026에서 위치(Transform.position)가 부드럽게 동기화된다. 그러나 그것만으로는 Remote 캐릭터가 **마치 미끄러지는 인형처럼 보인다** — 어느 방향을 보고 있는지 알 수 없고, 걷는지 공격 중인지 대시 중인지 시각 단서가 없다.

본 CL은 그 시각 단서를 채운다. 동기화 대상 두 가지:

1. **방향 (Aim Direction)** — `KhiPlayerAim.CurrentDirection` (Vector2). 마우스 조준 기반 360° 회전. 칼이 향하는 방향, 공격 시각 회전, 좌/우 미러 모두 이 값에 의존.
2. **행동 상태 (KhiPlayerState)** — `KhiPlayerStateAggregator.CurrentState` enum 8종 (Idle/Move/Attack/Dash/Parry/Hurt/Down/Defeated). 애니메이션·VFX 트리거의 기반.

본 CL의 결과로 **Remote 캐릭터에서도** 자기 앞을 보고, 걷고/달리고/대시하는 시각이 재생되어야 한다. 단, **공격 판정 자체는 본 CL 비범위** — 공격 모션이 보이는 것까지만(데미지 적용은 CL-028).

비목표:
- 데미지 판정/적 반응 — CL-028
- 다운/부활 처리 — CL-029
- 패링 성공 시 적 경직 RPC — CL-028
- 보상·인벤토리 동기화 — Epic D
- 시각 효과 자체(슬래시 프레임 등) 신규 — CL-067 영역. 본 CL은 트리거만.

## 현재 상태 (의존)

| 항목 | 상태 |
|---|---|
| `KhiPlayerAim.CurrentDirection => Vector2` | 솔로 동작 (마우스 조준) |
| `KhiPlayerStateAggregator.CurrentState => KhiPlayerState` | 솔로 동작 (CL-015) |
| `StateChanged` event | 솔로 동작 |
| 위치 동기화 | CL-026 polish 완료 |
| IsOwner 분기 진입점 | CL-022 `NetworkPlayerInitializer` |
| Owner 측 시각 컨트롤러 (KhiSlashAnimator, KhiDashAfterimagePresenter, KhiParryFeedbackPresenter, KhiAttackVisualPresenter ×2 등) | 솔로 기준 작동, 멀티 트리거 미연결 |

CL-022 plan의 권한 모델 표:
- 시각 효과(슬래시·잔상·플래시): "모든 인스턴스 재생. ClientRpc로 트리거" ([cl022:53](cl022_player_network_prefab_plan.md))
- 콤보 상태: "Owner 결정 + 서버 검증" ([cl022:48](cl022_player_network_prefab_plan.md))

본 CL은 위 권한 모델을 **방향·상태에 한해** 실제 코드로 구현한다.

## 동기화 대상 분류

총 2개 트랙. 각각 동기화 메커니즘이 다름.

| 트랙 | 데이터 | 빈도 | 메커니즘 |
|---|---|---|---|
| 1. 방향 | Vector2 (정규화된 단위 벡터) | 매 tick | `NetworkVariable<half2>` (반정밀도) Owner 권한 |
| 2. 행동 상태 | `KhiPlayerState` enum (byte) | 상태 전이 시점만 | `NetworkVariable<byte>` Owner 권한 |

**왜 둘 다 NetworkVariable**:
- 방향: 매 tick 변경 가능 → NetworkVariable이 Threshold 기반 자동 전송 (NGO 내장 dirty 추적)
- 상태: 변경 빈도 낮음(보통 1초에 0~3회) → NetworkVariable의 OnValueChanged 콜백으로 Remote 측 시각 트리거 발사

**ClientRpc 미사용 이유**:
- 늦게 합류한 클라가 현재 상태를 모름 → NetworkVariable은 Spawn 시 자동 sync, RPC는 onTriggered 시점에만 발사
- 시각 효과 *시작* 트리거는 RPC가 자연스럽지만, 본 CL은 "현재 상태"가 진실 소스 → State 변화 자체를 Remote가 구독해 시각 재생

## 흐름 설계

### 트랙 1: 방향

```
[Owner Update]
KhiPlayerAim.CurrentDirection → KhiPlayerNetworkSync.AimDir(NetworkVariable<half2>)
                                  ↓
                              자동 replicate (NGO 30Hz)
                                  ↓
[Remote OnValueChanged]
AimDir 값 → 자기 KhiPlayerAim.SetRemoteDirection(value)
              ↓
KhiPlayerAim 내부의 RemoteOverride 모드 → 입력 무시하고 SetRemoteDirection 값 사용
              ↓
캐릭터 회전·미러·SlashSlot 위치 자동 갱신 (기존 솔로 코드 그대로)
```

### 트랙 2: 행동 상태

```
[Owner OnEnable] KhiPlayerStateAggregator.StateChanged += OnLocalStateChanged
[Owner OnLocalStateChanged] (prev, next) → KhiPlayerNetworkSync.PlayerState(NetworkVariable<byte>) = (byte)next
                                              ↓
                                          자동 replicate
                                              ↓
[Remote OnValueChanged]
prev_byte, next_byte → KhiPlayerStateAggregator.RaiseRemoteStateChange(prev, next)
                          ↓
                      기존 StateChanged 이벤트 그대로 발사
                          ↓
                      구독자(KhiSlashAnimator, KhiDashAfterimagePresenter 등)가 시각 재생
```

핵심: Remote 측에서도 `StateChanged` 이벤트가 발사되도록 `KhiPlayerStateAggregator`에 외부 인입 메서드를 추가한다. 시각 컨트롤러는 코드 변경 없음.

## 작업 범위

### Step 1 — 데이터 형식 결정

**방향**:
- Vector2 정규화 단위 벡터 (x,y) ∈ [-1, 1]
- half2 직렬화: 4 bytes (full precision의 절반)
- Threshold: 0.02 (약 ~1.1° 회전 단위)

**상태**:
- `KhiPlayerState` enum 8값 → `byte`
- 모든 enum 값을 한 번 byte 캐스팅 가능한지 검증 (KhiPlayerState 값이 0~7 연속이어야 함, CL-015 plan 기준 OK)

`Assets/_Project/Scripts/Runtime/Networking/PlayerNetTypes.cs` 신규:
```text
public static class KhiPlayerStateNet {
    public static byte ToByte(KhiPlayerState s) => (byte)s;
    public static KhiPlayerState FromByte(byte b) => (KhiPlayerState)b;
}
```

### Step 2 — KhiPlayerNetworkSync 신규 (NetworkBehaviour)

`Assets/_Project/Scripts/Runtime/Networking/KhiPlayerNetworkSync.cs` 신규:

```text
[RequireComponent(typeof(NetworkObject))]
public class KhiPlayerNetworkSync : NetworkBehaviour {
    [SerializeField] private KhiPlayerAim aim;
    [SerializeField] private KhiPlayerStateAggregator aggregator;

    // 방향
    public NetworkVariable<half2> AimDir = new NetworkVariable<half2>(
        default,
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Owner
    );

    // 상태
    public NetworkVariable<byte> PlayerState = new NetworkVariable<byte>(
        0,
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn() {
        if (IsOwner) {
            aggregator.StateChanged += OnLocalStateChanged;
        }
        else {
            AimDir.OnValueChanged += OnRemoteAimChanged;
            PlayerState.OnValueChanged += OnRemotePlayerStateChanged;
            // 합류 시점의 현재값으로 1회 강제 적용 (OnValueChanged는 변경 시만 발사)
            ApplyRemoteAim(AimDir.Value);
            ApplyRemoteState(PlayerState.Value);
        }
    }

    public override void OnNetworkDespawn() { /* 짝맞춤 unsubscribe */ }

    void Update() {
        if (!IsOwner) return;
        // 방향 push (Threshold 0.02)
        var dir = aim.CurrentDirection;
        var current = AimDir.Value;
        if (Vector2.Distance(new Vector2(current.x, current.y), dir) > 0.02f) {
            AimDir.Value = new half2(dir.x, dir.y);
        }
    }

    void OnLocalStateChanged(KhiPlayerState prev, KhiPlayerState next) {
        PlayerState.Value = (byte)next;
    }

    void OnRemoteAimChanged(half2 _, half2 next) => ApplyRemoteAim(next);
    void OnRemotePlayerStateChanged(byte _, byte next) => ApplyRemoteState(next);

    void ApplyRemoteAim(half2 v) => aim.SetRemoteDirection(new Vector2(v.x, v.y));
    void ApplyRemoteState(byte b) => aggregator.RaiseRemoteStateChange((KhiPlayerState)b);
}
```

> `half2`는 `Unity.Mathematics`의 타입 또는 NGO 호환 직렬화 타입. 정확한 타입은 작업 시점 패키지 확인. fallback으로 `FixedPointVec2`(short×2) 직렬화 가능.

### Step 3 — KhiPlayerAim에 RemoteOverride 추가

`Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerAim.cs` 수정:

```text
public bool IsRemoteOverride { get; private set; }
private Vector2 _remoteDirection;

public void SetRemoteDirection(Vector2 dir) {
    IsRemoteOverride = true;
    _remoteDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
    _lastDirection = _remoteDirection;
}

void Update() {
    if (IsRemoteOverride) {
        _lastDirection = _remoteDirection;
        return;
    }
    // 기존 솔로 로직 (마우스 조준)
}
```

`NetworkPlayerInitializer.OnNetworkSpawn` (CL-022)에서 IsOwner=false일 때 `aim.SetRemoteDirection(initial)` 또는 KhiPlayerNetworkSync의 ApplyRemoteAim이 처리.

### Step 4 — KhiPlayerStateAggregator에 외부 인입 추가

`Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerStateAggregator.cs` 수정:

```text
public bool IsRemoteOverride { get; private set; }

public void RaiseRemoteStateChange(KhiPlayerState next) {
    if (next == _currentState) return;
    var prev = _currentState;
    _previousState = prev;
    _currentState = next;
    _enteredAt = Time.time;
    StateChanged?.Invoke(prev, next);
}

public void EnableRemoteOverride() {
    IsRemoteOverride = true;
    enabled = false;  // Update 자동 집계 비활성, 외부 인입만 사용
}
```

Remote 인스턴스에서 `enabled=false`로 두면 매 프레임 자동 집계 안 함 → 외부에서 들어오는 RaiseRemoteStateChange만 진실 소스.

`NetworkPlayerInitializer.OnNetworkSpawn`에서 IsOwner=false면 `aggregator.EnableRemoteOverride()` 호출.

### Step 5 — NetworkPlayerInitializer 보강

`NetworkPlayerInitializer.cs` (CL-022) 추가 분기:

```text
if (!owner) {
    aim.SetRemoteDirection(Vector2.right);  // 초기값
    aggregator.EnableRemoteOverride();
}
```

KhiPlayerNetworkSync 컴포넌트도 프리팹에 부착되도록 등록(Step 6).

### Step 6 — TestKhi 프리팹에 KhiPlayerNetworkSync 부착

`TestKhi_MinimalCharacter2D.prefab` 편집:
- `KhiPlayerNetworkSync` 컴포넌트 추가
- `aim` 슬롯 = 같은 GameObject의 `KhiPlayerAim`
- `aggregator` 슬롯 = 같은 GameObject의 `KhiPlayerStateAggregator`

### Step 7 — 디버그 검증 도구

`Assets/_Project/Scripts/Runtime/Networking/PlayerNetSyncDebugOverlay.cs` (선택, CL-031 자료):

OnGUI로 각 NetworkPlayer의 IsOwner / AimDir / PlayerState 값 + 마지막 갱신 시각 표시. CL-021 NetworkSyncProbe와 같은 톤.

```text
[Player 0] Owner=true  Aim=(0.71, 0.71) State=Move  t=12.43
[Player 1] Owner=false Aim=(-1.0, 0.0) State=Attack t=12.41 (lag 0.02s)
```

### Step 8 — 검증 시나리오

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | Owner가 마우스로 360° 조준 | Remote 화면에서 캐릭터 회전 / 칼 방향 / SlashSlot 위치 동기 (육안 1프레임 이내) |
| 2 | Owner가 좌→우→좌 빠른 방향 전환 | Remote가 부드럽게 따라옴, 깜빡임/snap 없음 |
| 3 | Owner가 가만히 서 있다 이동 시작 (Idle→Move) | Remote에서 walk 애니메이션 시작 |
| 4 | Owner가 1타 공격 (Idle→Attack) | Remote에서 슬래시 시각 재생, 방향에 맞게 회전 |
| 5 | Owner 3타 콤보 | Remote에서 1→2→3 시각 모두 재생 (각 step 진입 시점에 트리거) |
| 6 | Owner 대시 (Move→Dash) | Remote에서 잔상 효과 재생 |
| 7 | Owner 패링 시도 (Idle→Parry) | Remote에서 패링 윈도우 시각 재생 |
| 8 | Owner 트랩 피격 (→Hurt) | Remote에서 hit stun 시각 (피격 플래시는 CL-028, 본 CL은 상태만) |
| 9 | 합류 시점에 Owner가 이미 공격 중 | 늦게 들어온 Remote 인스턴스가 현재 상태(Attack) 즉시 적용 |
| 10 | 1초간 30번 상태 전이 (스트레스) | 모든 전이 누락 없이 도착 (NetworkVariable 자동 dirty 추적 확인) |
| 11 | UnityTransport Network Simulator 100ms + 5% loss | 시나리오 1~6 약간 지연되지만 결과적으로 동기 도달 |

## 비범위

- **공격 판정·데미지 적용** — CL-028 (서버 권위 ServerRpc)
- **적 반응(피격 stagger)** — CL-028
- **다운·부활 동기화** — CL-029
- **피격 플래시 등 시각 효과 신규 작성** — CL-067
- **VFX 객체 풀링** — 본 CL은 시각 트리거만, 풀링은 기존 솔로 구조 그대로
- **음향(SFX) 동기화** — 시각 트리거에 묶여 자동 재생, 별도 RPC 없음

## 사용자 결정 필요

| 항목 | 옵션 | 권장 |
|---|---|---|
| 방향 직렬화 | half2 (4B) / short×2 fixed (4B) / Vector2 full (8B) | **half2** (대역폭·정밀도 균형) |
| 방향 push threshold | 0.01 / 0.02 / 0.05 | **0.02** (육안 인지 한계 근처) |
| KhiPlayerState 직렬화 | byte / int | **byte** (8값이라 충분) |
| Aggregator Remote override | enabled=false / 별도 플래그 | **enabled=false** (Update 비용 0) |
| 시각 트리거 누락 시 fallback | snap / 무시 | **snap** (NetworkVariable 합류 sync 포함) |
| Hurt/Down 본 CL 포함 여부 | 포함 / CL-029로 이관 | **상태 enum만 포함, 부활 RPC는 CL-029** |

## 위험 요소

1. **Aggregator Update 자동 집계 vs Remote override** — CL-015 설계가 매 프레임 컨트롤러를 폴링해 상태 결정. Remote에서 그 컨트롤러들도 비활성(CL-022) → 자동 집계 결과는 항상 Idle. enabled=false로 Update 자체를 막아 부정확 결과를 차단.
2. **State enum 추가 시 byte 호환 깨짐** — KhiPlayerState에 새 값이 9번째로 추가되면 byte 캐스팅은 OK이나 0~7 가정의 코드는 무관. 안전하나 enum 변경 시 본 plan 재검토.
3. **시각 컨트롤러가 Owner 가정으로 동작** — KhiSlashAnimator가 KhiMeleeComboController의 이벤트를 직접 구독한다면 Remote에선 그 컨트롤러가 비활성 → 이벤트 미발사 → 슬래시 미재생. 해결: 시각 컨트롤러를 StateChanged 이벤트(Aggregator 발) 구독으로 변경. **본 CL의 가장 큰 리스크** — 작업 전 KhiSlashAnimator의 구독 패턴 확인 필요.
4. **NetworkVariable 합류 sync 시점** — Spawn 직후 OnValueChanged 자동 호출 안 됨. OnNetworkSpawn에서 ApplyRemote* 강제 1회 호출 필요(Step 2 코드 반영).
5. **SlashSlot 위치 갱신 의존성** — KhiSlashAnimator는 KhiPlayerAim.CurrentDirection을 읽어 SlashSlot 회전 결정. Remote에서도 aim.CurrentDirection이 정확하면 자동. RemoteOverride가 _lastDirection을 갱신하면 OK.
6. **AimDir 정규화 누락** — Owner의 CurrentDirection이 (0,0) 또는 비정규일 수 있음. KhiPlayerAim.SetRemoteDirection에서 normalize.
7. **half2 NGO 직렬화 미지원** — NGO 버전에 따라 half 타입 자동 직렬화 안 될 수 있음. fallback: `INetworkSerializable` 직접 구현 또는 short×2 wrapper.
8. **합류 시 시각 1프레임 깜빡임** — Aim/State 강제 적용이 OnNetworkSpawn에서 한 번에 일어나면 KhiSlashAnimator가 아직 Awake 직후라 무시할 수 있음. coroutine으로 1프레임 지연 후 적용 검토.

## 변경/신규 파일

### 신규

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/KhiPlayerNetworkSync.cs` | NetworkVariable<AimDir>, NetworkVariable<PlayerState> + Owner push + Remote apply |
| `Assets/_Project/Scripts/Runtime/Networking/PlayerNetTypes.cs` (선택) | enum↔byte 헬퍼 / half2 직렬화 wrapper |
| `Assets/_Project/Scripts/Runtime/Networking/PlayerNetSyncDebugOverlay.cs` (선택) | OnGUI 디버그 |

### 변경

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerAim.cs` | `IsRemoteOverride`, `SetRemoteDirection(Vector2)` 추가 |
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerStateAggregator.cs` | `RaiseRemoteStateChange(KhiPlayerState)`, `EnableRemoteOverride()` 추가 |
| `Assets/_Project/Scripts/Runtime/Networking/NetworkPlayerInitializer.cs` (CL-022) | Remote 분기에 EnableRemoteOverride + 초기 방향 설정 |
| `Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` | KhiPlayerNetworkSync 컴포넌트 부착 |
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiSlashAnimator.cs` (조사 결과 따라) | StateChanged 구독 또는 컨트롤러 이벤트 구독 유지 — 작업 시 결정 |

### 미변경

- 4개 Khi 행동 컨트롤러 (Parry/HitStun/Dash/Down/Melee) 코드 본체 — Remote에서 비활성 상태로 둠
- TDE Character/CharacterMovement
- SessionService 등 CL-023~025 자산
- 게임 콘텐츠 코드/씬

## 검증 (E2E)

1. CL-021 씬 솔로 Play — 회귀 0
2. Multiplayer Play Mode 2 인스턴스 (Different Auth Profile) Host + Join
3. 검증 시나리오 1~10 모두 통과
4. Network Simulator 100ms + 5% loss → 시나리오 11 통과
5. 합류 직전 Owner가 공격 중인 상태 → Remote 합류 즉시 Attack 시각 재생 (시나리오 9)
6. PlayerNetSyncDebugOverlay에서 양쪽 Player의 Aim/State 값이 일관 (lag < 100ms)
7. 5회 반복 stress 후 콘솔 에러 0

## 관련 문서

- `docs/12_development_plan.md:232-244` — 호스트 권위 + Owner 권위 분기
- `docs/14_client_jira_story_backlog.md:56` — CL-027 정의
- `docs/khi/cl015_player_state_aggregator_implementation.md` — KhiPlayerStateAggregator 설계
- `docs/khi/cl022_player_network_prefab_plan.md:39-55, 92-119, 175` — 권한 모델 표 + Remote 분기 + 본 CL 인계
- `docs/khi/cl026_position_sync_polish_plan.md` — 위치 동기화 polish (본 CL은 그 위에 회전·상태)

## 다음 CL

- **CL-028**: 공격 판정 결과·적 반응 최소 동기화 — 본 CL의 "공격 모션이 보임"을 "공격이 적에게 데미지 입힘"으로 확장. ServerRpc + 적 Health NetworkVariable.

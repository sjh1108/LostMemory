# CL-022: 플레이어 네트워크 프리팹 구조 적용

소속 Epic: **Epic C. 멀티플레이 / 세션 네트워크**

선행: CL-020 (패키지), CL-021 (테스트 씬)

## Context

기존 솔로 플레이어 프리팹 `TestKhi_MinimalCharacter2D.prefab`에 `NetworkObject`를 부착하고, **TDE Character + 30+개 Khi 컨트롤러 + Rigidbody2D**가 멀티 환경에서 깨지지 않게 IsOwner/Remote 분기 패턴을 확립한다.

본 CL은 "프리팹 구조와 권한 분기 패턴"만 다룬다. 실제 이동값 동기화(CL-026), 공격 판정 동기화(CL-028), 다운/부활(CL-029)은 후속 CL에서 처리.

## 현재 상태 (탐색 결과)

### 플레이어 프리팹

`Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab`

부착 컴포넌트 카테고리:

| 카테고리 | 컴포넌트 |
|---|---|
| Unity 기본 | Rigidbody2D(Dynamic, GravityScale=0, Interpolate, Continuous), BoxCollider2D, SpriteRenderer, SortingGroup |
| TDE 기본 | Character, CharacterMovement, CharacterDash2D 파생, CharacterHandleWeapon, CharacterInventory, Health, 다수 Ability(Jump/Run/Pause/WallJump/TimeScale 등) |
| Khi 입력·전투 | KhiPlayerAim, KhiMeleeHitbox, KhiMeleeComboController, KhiDashController(:CharacterDash2D), KhiParryController, KhiHitStunController, KhiDownController, KhiFinisherLunge |
| Khi 시각 | KhiAttackVisualPresenter ×2, KhiDashAfterimagePresenter, KhiParryFeedbackPresenter, KhiCombatFeedbackBinder, KhiSlashAnimator, KhiWeaponPresenter |
| Khi 상태 | KhiPlayerStateAggregator |

### 입력·카메라·스폰

| 항목 | 현재 |
|---|---|
| 입력 | `TestKhiInputManager`(TDE InputManager 상속, DefaultExecutionOrder(-100), InputSystem 기반, ActionMap "TestKhi") |
| 카메라 | `KhiPlayerCamera` (독립 GameObject, DefaultExecutionOrder(220), Deadzone+SmoothDamp 추적) |
| 스폰 | `TestKhiSceneBootstrap` Awake에서 프리팹 직접 인스턴스화 + InputManager 생성 + 카메라 EnsureCamera |
| TDE 상속 | `KhiDashController` → `CharacterDash2D`. 그 외 대부분 MonoBehaviour + Character/Health 참조 |

## 권한 모델 (본 CL의 핵심 결정)

`docs/12_development_plan.md:232-235` 호스트 권위 원칙을 다음과 같이 적용한다:

| 데이터 | 권위 | 근거 |
|---|---|---|
| 위치·회전 (Transform) | **Owner Authoritative** | 입력 반응성. NGO ClientNetworkTransform 또는 OwnerAuthoritative 모드. |
| Rigidbody2D 시뮬레이션 | **Owner만 simulation 활성** | Remote는 NetworkTransform이 위치만 갱신, 물리 계산 X |
| 입력(TestKhiInputManager) | **Owner만 활성** | Remote 인스턴스는 입력 무시 |
| 카메라 추적 | **Owner만** | Remote 캐릭터로 카메라 따라가지 않음 |
| 콤보 상태(현재 step, 다음 입력 가능 여부) | **Owner 결정 + 서버 검증** | Owner가 콤보 상태머신 진행, 공격 판정만 서버에 보고 |
| 공격 데미지 판정 | **서버 권위** | `KhiMeleeHitbox` 충돌 검출은 Owner, 데미지 적용은 ServerRpc → 서버에서 적 Health 차감 |
| Health (받는 피해) | **서버 권위** | Health.MaximumHealth/CurrentHealth는 NetworkVariable. 회복/피해는 서버 RPC |
| 패링 성공 판정 | **Owner 결정 + 서버 통보** | 입력 타이밍은 Owner. 성공 시 ServerRpc로 보고 → 서버가 적 경직 처리 |
| 다운 상태 | **서버 권위** | NetworkVariable<bool> IsDowned. 부활 상호작용은 Owner ServerRpc |
| 시각 효과(슬래시·잔상·플래시) | **모든 인스턴스 재생** | ClientRpc로 트리거, 각자 시각 재생 |

본 CL은 위 정책을 **프리팹 구조에 반영하고 IsOwner 분기 진입점만 만든다**. 실제 RPC 구현은 후속 CL.

## 작업 범위

### Step 1 — NetworkObject 부착

`TestKhi_MinimalCharacter2D.prefab` 루트에 `NetworkObject` 추가.

설정:
- Always Replicate As Root: true
- Synchronize Transform: false (NetworkTransform이 별도 처리)
- Active Scene Synchronization: false

### Step 2 — NetworkTransform 결정 및 부착

옵션:
- (A) NGO 기본 `NetworkTransform` (서버 권위) — Owner가 위치 결정 못 함, RPC 우회 필요
- (B) NGO Samples의 `ClientNetworkTransform` (Owner 권위) — 별도 코드 가져오기 필요
- (C) NGO 2.x의 `NetworkTransform.AuthorityMode = Owner` — 패키지 버전 따라 옵션 노출

**권장: (C)** if NGO 2.x. 아니면 (B). 작업 시점 NGO 버전 확인 후 결정.

설정:
- Position/Rotation 동기화 ON, Scale OFF
- Interpolate ON
- Position Threshold 0.001, Rotation Threshold 1
- Authority Mode: Owner

### Step 3 — Rigidbody2D 동기화

NGO `NetworkRigidbody2D` 부착. 이 컴포넌트가 Remote 인스턴스에서 Body Type을 자동 Kinematic 처리하고 Owner만 시뮬레이션. 별도 코드 불필요.

대안(NetworkRigidbody2D 미제공 시): `NetworkPlayerInitializer` (신규)에서 OnNetworkSpawn 분기로 `rb.bodyType = IsOwner ? Dynamic : Kinematic`.

### Step 4 — IsOwner 분기 진입점 신설

`Assets/_Project/Scripts/Runtime/Networking/NetworkPlayerInitializer.cs` 신규 (NetworkBehaviour):

```csharp
public override void OnNetworkSpawn() {
    bool owner = IsOwner;

    // 입력 — Owner만
    GetComponentInParent<TestKhiInputManager>(true)?.SetEnabled(owner); // 매니저가 별도라면 PlayerID 매칭

    // 시각·상태 컨트롤러는 모두 활성 (시각·재생은 ClientRpc로 트리거됨)
    // 다만 입력 의존 컨트롤러는 Owner만:
    SetEnabled<KhiMeleeComboController>(owner);
    SetEnabled<KhiParryController>(owner);
    SetEnabled<KhiDashController>(owner);
    SetEnabled<KhiPlayerAim>(owner);
    SetEnabled<KhiFinisherLunge>(owner);

    // Health — 서버에서만 권위 처리 (실제 RPC는 후속 CL에서)
    // Health 컴포넌트 자체는 모두 활성, 단 데미지 인입은 서버만 받음

    // 카메라 — Owner만
    if (owner) {
        var cam = FindAnyObjectByType<KhiPlayerCamera>();
        cam?.SetTarget(transform);
    }

    // PlayerID 매핑
    GetComponent<MoreMountains.TopDownEngine.Character>().PlayerID = $"Player{OwnerClientId}";
}
```

`SetEnabled<T>` 헬퍼는 동일 GameObject의 Behaviour를 enabled 토글.

본 CL에선 위 진입점만 만들고 실제 RPC 호출/네트워크 변수는 비워둔다.

### Step 5 — TestKhiSceneBootstrap 멀티 분기

`TestKhiSceneBootstrap.Awake`에 다음 분기 추가:

```csharp
if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsHost)) {
    // 멀티 모드: NetworkManager가 PlayerPrefab을 자동 spawn하므로 인스턴스화 우회
    EnsureCamera(); // 카메라는 항상 보장
    return;
}
// 솔로 모드: 기존 흐름
```

NetworkManager가 PlayerPrefab을 자동 스폰하려면 NetworkManager.PlayerPrefab 슬롯에 `TestKhi_MinimalCharacter2D.prefab` 등록 + Network Prefabs List 등록.

### Step 6 — NetworkPrefabsList 등록

`Assets/_Project/Settings/NetworkPrefabsList.asset` 신규 (또는 기존):
- TestKhi_MinimalCharacter2D 등록
- NetworkSyncProbe (CL-021) 등록

NetworkManager에 NetworkPrefabsList 연결.

### Step 7 — InputManager 인스턴스 정책

기존: TestKhiSceneBootstrap이 InputManager 1개 생성, PlayerID="Player1".

멀티 변경: 각 클라이언트는 자기 InputManager 1개만 필요. PlayerID는 OwnerClientId 기반(`Player{OwnerClientId}`).

선택 옵션:
- (A) InputManager는 NetworkPlayer 인스턴스 안에 자식으로 둠 — Owner만 활성화
- (B) InputManager는 씬에 1개만 두고 Owner Player가 InputManager.PlayerID를 자기 ID로 갱신

**권장: (A)** — 인스턴스별 격리, 코드 단순.

### Step 8 — 검증 시나리오

CL-021 테스트 씬에서:

1. Multiplayer Play Mode 2 인스턴스
2. 인스턴스 1: Host → 자기 캐릭터 1 스폰, 입력·카메라 동작
3. 인스턴스 2: Client 접속 → 인스턴스 2에 자기 캐릭터, 인스턴스 1에도 인스턴스 2의 캐릭터(상대)가 보임
4. 인스턴스 1 입력 시 인스턴스 1의 캐릭터만 이동 (양쪽 인스턴스에서 위치 동기화 보임)
5. 인스턴스 2 입력 시 인스턴스 2의 캐릭터만 이동
6. 카메라: 각 인스턴스가 자기 캐릭터 추적
7. 콘솔 에러 0건

## 비범위

- 정확한 위치 동기화 보정 (CL-026)
- 공격 판정·적 반응 동기화 (CL-028)
- 다운/부활 동기화 (CL-029)
- 호스트 이탈 처리 (CL-025)
- 보상 인벤토리 동기화 — 본 CL은 캐릭터 자체 구조만

## 사용자 결정 필요

| 항목 | 옵션 | 권장 |
|---|---|---|
| NetworkTransform 권한 | NGO 2.x Owner mode / Samples ClientNetworkTransform / RPC 우회 | NGO 2.x Owner |
| NetworkRigidbody2D 사용 | NGO 자동 / 수동 BodyType 토글 | 자동 (있으면) |
| InputManager 배치 | NetworkPlayer 자식(A) / 씬 단독(B) | (A) |
| PlayerID 규칙 | `Player{OwnerClientId}` / `Player{1..4}` 매핑 테이블 | `Player{OwnerClientId}` |
| 사용 안 하는 TDE Ability(WallJump 등) 정리 | 제거 / 그대로 둠 | 그대로 둠 (비활성으로 두면 영향 없음, 본 CL 범위 밖) |

## 위험 요소

1. **Rigidbody2D 시뮬레이션 충돌** — Owner와 Remote 모두 Dynamic이면 위치가 양쪽에서 결정돼 jitter 발생. NetworkRigidbody2D 또는 수동 Kinematic 전환 필수.
2. **TDE CharacterMovement의 입력 폴링** — TDE Character는 InputManager에서 자동으로 입력 가져옴. Remote 인스턴스는 InputManager가 없거나 비활성화돼야 함. PlayerID 미스매치 시 입력이 안 와도 안전.
3. **DefaultExecutionOrder 충돌** — InputManager(-100), Camera(220). NetworkPlayerInitializer는 OnNetworkSpawn에서 한 번만 동작하므로 Order 영향 없음.
4. **Bootstrap이 솔로 모드 가정** — 멀티 모드에서 NetworkManager.PlayerPrefab이 비어 있으면 클라이언트가 캐릭터 없이 접속. 분기 누락 시 즉시 발견 가능.
5. **시각 컨트롤러 재생 트리거** — 본 CL에선 Owner가 자기 시각 재생을 직접 호출. 다른 클라이언트에 보이는 시각은 CL-027/028에서 ClientRpc로 추가. 본 CL 단계에선 다른 클라이언트는 상대 캐릭터의 공격 슬래시를 못 봄(이동만 보임) — 정상.
6. **KhiPlayerCamera 단일 인스턴스 가정** — 씬에 카메라 1개. 각 클라이언트 인스턴스에서 자기 카메라가 자기 캐릭터를 따라가면 OK.

## 변경/신규 파일

### 신규

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/NetworkPlayerInitializer.cs` | OnNetworkSpawn에서 IsOwner 분기 진입점 |
| `Assets/_Project/Settings/NetworkPrefabsList.asset` | NetworkObject 프리팹 목록 |

### 변경

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` | NetworkObject + NetworkTransform + (NetworkRigidbody2D) + NetworkPlayerInitializer 부착 |
| `Assets/_Project/Scripts/Runtime/TestKhi/TestKhiSceneBootstrap.cs` | 멀티 모드 분기 — NetworkManager 활성 시 인스턴스화 우회 |
| `Assets/Scenes/Test/CL021_NetworkTest_2P.unity` | NetworkManager.PlayerPrefab 슬롯에 TestKhi 프리팹 등록 |

### 미변경

- 30개 Khi 컨트롤러 .cs — 본 CL은 컨트롤러 코드 안 건드림. enabled 토글만.

## 검증 (E2E)

1. CL-021 테스트 씬에 PlayerPrefab 등록 후 단독 Play — 솔로 모드 그대로 동작 (회귀 0)
2. Multiplayer Play Mode 2 인스턴스, Host + Client → 양쪽에 2개 캐릭터 보임
3. 각 인스턴스에서 WASD 입력 시 자기 캐릭터만 이동
4. 카메라가 자기 캐릭터만 따라감
5. 한 쪽 disconnect 시 상대 캐릭터 정상 제거
6. 동일 시나리오 5회 반복 stability

## 관련 문서

- `docs/12_development_plan.md:232-244` — 호스트 권위 원칙
- `docs/12_development_plan.md:540-545` — Sessions/Relay 위주 스택
- `docs/14_client_jira_story_backlog.md` — CL-022 정의
- `cl021_network_test_scene_plan.md` — 테스트 씬 기반

## 다음 CL

- **CL-023**: 호스트 생성 및 Relay 참여 코드 발급 — 본 CL의 로컬 직결을 Relay 기반 원격 접속으로 확장.

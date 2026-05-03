# 네트워크 통합 규칙 (Phase B 준비)

> 대상 프로젝트: `client/LostMemory`
> 목적: Phase A(네트워크 레이어 셋업) 완료 이후, Phase B(플레이어/전투 동기화) 진입 전까지 *다른 작업과 병렬 진행 시* 충돌·재작업을 줄인다.
> 관련 문서:
> - `docs/04_multiplayer.md` — 멀티 규칙
> - `docs/12_development_plan.md` — 개발 계획
> - `docs/13_client_detailed_plan.md` — 클라 상세 계획
> - `client/docs/cl020_network_test_scene_setup_plan.md` — Phase A 절차서
> - `client/docs/commonness/topdown-engine-extension-and-original-protection.md` — TDE 확장 원칙
> - `client/docs/commonness/agent-unity-safety-rules.md` — Unity 파일 안전 규칙

## 1. 문서 목적

이 문서는 다음 두 종류 독자를 위한 *공유 규칙* 이다.

- **UI/콘텐츠 작업자**: Phase B 가 들어오기 전후로 어떤 코드를 *건드리지 말아야* 충돌이 안 나는지 알고 싶은 사람
- **네트워크 작업자(=Phase B 담당)**: 어떤 코드 수정이 다른 작업을 깨뜨릴 수 있는지 명확히 짚고 들어가야 하는 사람

## 2. 배경

### 2.1 합의된 멀티 구조

- 1~4인 중 **MVP는 2인**, **호스트 기반 진행**, **초대코드 흐름**
- 권위 모델: **호스트 권위** — 호스트가 전투 판정·룸 진행·보상 결정
- 일반 클라이언트는 입력 RPC 송신 + 위치는 클라 권위(`ClientNetworkTransform` 패턴)

### 2.2 Phase 구분 이유

원본 task list(12개)는 *위험도 기준* 으로 두 페이즈로 나뉘었다.

| Phase | 범위 | 다른 작업과의 충돌 위험 |
|---|---|---|
| A (완료) | NGO·Relay 셋업, 세션, 코드 발급, 예외 흐름, 로그 | 0 — 새 파일만 추가, 기존 코드 무수정 |
| B (예정) | 플레이어 NetworkObject, 이동/상태/전투/다운 동기화, 호스트 권위 게이트 전환 | 中 — 기존 플레이어/전투/Run 코드의 *공개 API* 와 닿음 |

Phase A 산출물은 모두 `Assets/_Project/Scripts/Runtime/Networking/` 아래 신규 파일이며, 기존 코드는 단 한 줄도 수정하지 않았다.

### 2.3 본 문서의 적용 시점

- **지금부터 Phase B 머지 직전까지**: 본 문서의 *시그니처 안정성 규칙* (3장) 은 즉시 효력
- **Phase B 진입 후**: 4·5장의 *로컬 플레이어 식별 / 프리팹 규칙* 도 활성

## 3. 시그니처 안정성 규칙 (가장 중요)

### 3.1 시그니처란

코드 외부에서 보는 **이름 + 매개변수 타입 + 반환 타입 + 이벤트 델리게이트 타입** 의 묶음.

```csharp
// 시그니처:
public event Action<KhiPlayerState, KhiPlayerState> StateChanged;
//             ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ 이 모양

public bool IsAuthority => true;
//     ^^^^ ^^^^^^^^^^^ 이 모양
```

다른 코드가 *이 모양에 의존* 하므로, 시그니처를 바꾸면 의존하는 모든 곳이 깨진다.

### 3.2 보호 대상 클래스

다음 클래스의 **public/protected 멤버 시그니처는 Phase B 머지 전까지 변경 금지**.

| 경로 | 클래스 | 보호 이유 |
|---|---|---|
| `_Project/Scripts/Runtime/TestKhi/KhiPlayerStateAggregator.cs` | `KhiPlayerStateAggregator` | Phase B 가 외부 sync 컴포넌트로 상태 관찰 + 동기화 |
| `_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs` | `KhiMeleeComboController` | Phase B 가 공격 이벤트 hook 해서 호스트로 RPC |
| `_Project/Scripts/Runtime/TestKhi/KhiMeleeHitbox.cs` | `KhiMeleeHitbox` | Phase B 가 hit 후보 라우팅 어댑터 부착 |
| `_Project/Scripts/Runtime/TestKhi/KhiDashController.cs` | `KhiDashController` | 행동 상태 동기화의 부분 |
| `_Project/Scripts/Runtime/TestKhi/KhiParryController.cs` | `KhiParryController` | 행동 상태 동기화의 부분 |
| `_Project/Scripts/Runtime/TestKhi/KhiDownController.cs` | `KhiDownController` | 다운/부활 동기화의 hook 지점 |
| `_Project/Scripts/Runtime/Combat/PlayerHealthStatApplier.cs` | `PlayerHealthStatApplier` | TopDown Engine `Health` 어댑터, Phase B 가 의존 |
| `_Project/Scripts/Runtime/Stage/RunManager.cs` | `RunManager` | `IsAuthority` 게이트 한 줄만 교체 예정, 다른 시그니처 보존 |
| `_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs` | `RoomEntryRuntimeController` | `IsAuthority` 게이트 동일 |
| `_Project/Scripts/Runtime/Stage/BossDefeatRoomClearController.cs` | `BossDefeatRoomClearController` | `IsAuthority` 게이트 동일 |
| `_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs` | `DungeonRunBootstrap` | `IsAuthority` 게이트 동일 |
| `_Project/Scripts/Runtime/Relics/IRelicEffectAuthority.cs` | `IRelicEffectAuthority` | Phase B 가 NetworkRelicEffectAuthority 로 구현체만 교체 |
| `_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs` | `RelicEffectRegistry` | 권위 주입 지점 한 곳만 변경 예정 |

> TopDown Engine 원본(`Assets/TopDownEngine/`) 은 어차피 직접 수정 금지(`topdown-engine-extension-and-original-protection.md` 참고).

### 3.3 DO / DON'T

#### ✅ 안전 — 시그니처 유지 + 내부 변경

```csharp
// 기존 시그니처 그대로
public event Action<KhiPlayerState, KhiPlayerState> StateChanged;

private void Update() {
    // 안의 로직 100줄 새로 짜도 OK
    // 새 조건 추가, 호출 시점 조정, 다 안전
}
```

#### ✅ 안전 — 새 멤버 추가

```csharp
// 기존 이벤트는 그대로
public event Action<KhiPlayerState, KhiPlayerState> StateChanged;

// 새 정보 필요하면 *새 이벤트* 추가
public event Action<KhiPlayerState, KhiPlayerState, float> StateChangedWithDuration;

// 새 메서드 추가도 OK
public bool TryGetCurrentStateInfo(out StateInfo info) { ... }
```

#### ❌ 위험 — 기존 시그니처 변경

```csharp
// ❌ 매개변수 추가
public event Action<KhiPlayerState, KhiPlayerState, float> StateChanged;

// ❌ 매개변수 줄임
public event Action<KhiPlayerState> StateChanged;

// ❌ 이름 변경
public event Action<KhiPlayerState, KhiPlayerState> StateUpdated;

// ❌ 반환 타입 변경
public KhiPlayerState IsAuthority => ...;  // bool 이었던 것

// ❌ 매개변수 타입 변경
public void StartRun(string stageName) { }  // 원래 매개변수 없었음
```

### 3.4 시그니처 *외* 자유롭게 바꿔도 되는 것

- `private` / `internal` 멤버
- 메서드 *내부 로직*
- 호출 시점, 조건, 순서
- `[SerializeField]` 필드의 *초기값* (인스펙터에서 조정 가능)
- 새 클래스, 새 컴포넌트, 새 이벤트, 새 메서드 *추가*

## 4. 로컬 플레이어 식별 규칙

### 4.1 문제

현재 UI/카메라/HUD 코드 중 일부는 *플레이어가 1명* 이라는 전제로 작성됨. 2인 모드 진입 시 "내 플레이어가 어느 거지?" 를 알 수 없으면 다음 문제 발생:

- 양쪽 화면에 같은 플레이어의 HUD 가 뜸
- 카메라가 잘못된 플레이어를 따라감
- 입력이 양쪽 플레이어에게 동시 적용

### 4.2 해결 — `LocalPlayerResolver` 패턴

Phase B 에서 다음 진입점이 도입된다 (예정).

```csharp
// 위치: _Project/Scripts/Runtime/Networking/Player/LocalPlayerResolver.cs (Phase B 신규)
public static class LocalPlayerResolver
{
    /// <summary>현재 클라이언트의 로컬 플레이어. 싱글 실행 중이거나 아직 미스폰이면 null.</summary>
    public static KhiPlayerStateAggregator LocalPlayer { get; }

    /// <summary>로컬 플레이어가 spawn 됐을 때 발화.</summary>
    public static event Action<KhiPlayerStateAggregator> LocalPlayerReady;
}
```

### 4.3 권장 사용 패턴

#### HUD / 카메라 추적 / 상태 표시

```csharp
// ❌ 옛 방식 (1인 전제)
private void Awake() {
    _player = FindObjectOfType<KhiPlayerStateAggregator>();
}

// ✅ 새 방식 (2인 호환)
private void OnEnable() {
    if (LocalPlayerResolver.LocalPlayer != null) {
        BindToPlayer(LocalPlayerResolver.LocalPlayer);
    } else {
        LocalPlayerResolver.LocalPlayerReady += BindToPlayer;
    }
}

private void OnDisable() {
    LocalPlayerResolver.LocalPlayerReady -= BindToPlayer;
}
```

### 4.4 임시 가드 (Phase B 진입 전)

`LocalPlayerResolver` 가 아직 없는 시점이라도, 새로 작성하는 UI 는 다음 가드만 미리 넣어두면 Phase B 합류가 깔끔해진다.

```csharp
// 현재는 항상 통과 (싱글 실행), Phase B 후 진짜 가드로 작동
private bool IsLocalPlayer(KhiPlayerStateAggregator player) {
    // TODO(Phase B): LocalPlayerResolver.LocalPlayer == player 로 교체
    return true;
}
```

## 5. 프리팹 수정 규칙

### 5.1 두 종류의 플레이어 프리팹

| 프리팹 | 용도 | 수정 규칙 |
|---|---|---|
| `Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` | 현재 *원본* (싱글 실행 기준) | 기존 자식 트랜스폼 경로·컴포넌트 *유지*. 시각·수치만 수정 |
| `TestKhi_Net_AD.prefab` (Phase B 신규) | 위 원본의 *복제본* + NetworkObject 추가 | Phase B 작업자 전용 |

### 5.2 자식 트랜스폼 경로 보존

UI 가 `transform.Find("HealthBar")` 같은 자식 경로 참조를 갖고 있을 수 있다.

- ✅ **루트에 컴포넌트 추가** — `Add Component` 로 새 스크립트/컴포넌트 부착
- ✅ **자식 *추가*** — 새 자식 오브젝트 추가는 안전
- ❌ **자식 *이름·계층 변경*** — 기존 자식 이름 변경, 부모 변경, 깊이 변경 = 외부 참조 다 깨짐
- ❌ **자식 *삭제*** — 다른 곳에서 참조 중일 수 있음

### 5.3 외부 에셋 원본 금지

`Assets/TopDownEngine/`, `Assets/CodeRespawn/` 의 프리팹·스크립트는 **직접 수정 금지**. 자세한 규칙은 `topdown-engine-extension-and-original-protection.md` 참고.

## 6. 새 UI 추가 체크리스트

UI 작업 시작 전 다음 체크.

### 6.1 안전한 UI 추가 (자유 진행 OK)

- [ ] 새 화면/패널 (상점, 인벤토리, 옵션 등)
- [ ] 새 Canvas, 새 위젯
- [ ] 시각·스타일 변경 (스프라이트, 컬러, 레이아웃, 폰트)
- [ ] 기존 이벤트 *구독* 만 하는 새 View/Presenter
- [ ] UI 내부 로직 변경 (View 표시 방식, 애니메이션, 트랜지션)

### 6.2 코디네이션 필요

- [ ] HUD/카메라가 플레이어 오브젝트를 *직접* 참조 → §4 의 LocalPlayerResolver 패턴 사용
- [ ] 새 UI 가 플레이어 prefab 의 자식 트랜스폼 *경로* 에 의존 → §5.2 자식 보존 규칙 확인

### 6.3 위험 — 진행 전 합의 필수

- [ ] §3.2 의 *보호 대상 클래스* 의 public 시그니처 변경 필요 → **합의 후 진행**
- [ ] 플레이어 prefab 의 자식 *이름·계층 변경* → **합의 후 진행**
- [ ] TopDown Engine 원본 수정 → **금지**

## 7. Phase B 가 건드릴 파일 미리보기

투명성을 위해 Phase B 진입 시 *수정될 기존 파일* 과 *신규 파일* 을 미리 공개.

### 7.1 기존 파일 수정 (각 1~3줄)

```text
_Project/Scripts/Runtime/Stage/RunManager.cs
  → IsAuthority 한 줄 교체 (=> true → => HostAuthority.IsHost)
_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs
  → IsAuthority 한 줄
_Project/Scripts/Runtime/Stage/BossDefeatRoomClearController.cs
  → IsAuthority 한 줄
_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs
  → IsAuthority 한 줄
_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs
  → 생성자/팩토리에서 권위 주입 교체
```

기존 *시그니처는 그대로* 유지된다. 위 변경은 본 문서의 §3 규칙을 따른다.

### 7.2 신규 파일

```text
_Project/Scripts/Runtime/Networking/Player/
  LocalPlayerResolver.cs
  PlayerMovementSync.cs
  KhiPlayerStateNetSync.cs

_Project/Scripts/Runtime/Networking/Combat/
  MeleeHitboxNetRouter.cs

_Project/Scripts/Runtime/Relics/
  NetworkRelicEffectAuthority.cs
```

### 7.3 신규 프리팹 / 씬

```text
Assets/_Project/Prefabs/Characters/TestKhi_Net_AD.prefab
  (TestKhi_MinimalCharacter2D.prefab 의 복제본 + NetworkObject)
```

원본 `TestKhi_MinimalCharacter2D.prefab` 은 *수정하지 않는다*.

### 7.4 영향 받지 않는 파일 (안심하고 작업 가능)

- `_Project/Scripts/Runtime/UI/` 전체
- `_Project/Scripts/Runtime/Save/` 전체
- `_Project/Scripts/Runtime/Shop/` 전체
- `_Project/Scripts/Runtime/Rewards/` 전체
- `_Project/Scripts/Runtime/Data/` 전체
- `_Project/Scenes/` 전체 (Test 씬 제외)

## 8. 자주 묻는 질문

### Q1. UI 에서 새 이벤트가 필요한데 `KhiPlayerStateAggregator` 의 기존 이벤트 매개변수 하나만 추가하면 편한데요?

**기존 이벤트는 그대로 두고 새 이벤트를 추가하세요.**

```csharp
// ❌ 위험
public event Action<KhiPlayerState, KhiPlayerState, float> StateChanged;  // 매개변수 추가

// ✅ 안전
public event Action<KhiPlayerState, KhiPlayerState> StateChanged;          // 그대로
public event Action<KhiPlayerState, KhiPlayerState, float> StateChangedWithDuration;  // 새로 추가
```

### Q2. 플레이어 prefab 에 새 컴포넌트(예: 시각 효과 컨트롤러) 추가해도 되나요?

**루트에 *추가* 만 하면 OK**. 자식 구조나 기존 컴포넌트의 시그니처는 건드리지 마세요.

### Q3. Phase B 시작 전에 임시로 2인 동작 흉내내야 하는 UI 작업이 있어요. 어떻게 가드해야?

§4.4 임시 가드 패턴을 사용하세요. `LocalPlayerResolver` 가 도입되면 그 한 줄만 교체.

### Q4. TopDown Engine 의 `Health` 컴포넌트를 직접 상속받아 새 클래스 만들어도 되나요?

`topdown-engine-extension-and-original-protection.md` 의 §3.3 참조. 상속·래퍼·어댑터 우선, 원본 직접 수정 금지.

### Q5. 새 UI 가 호스트인지 클라인지 알아야 표시가 다른 경우가 있는데?

Phase B 도입 후 `LostMemory.Networking.Common.HostAuthority.IsHost` 또는 `HostAuthority.IsNetworkSessionActive` 사용. Phase A 에서 이미 추가됨(`_Project/Scripts/Runtime/Networking/Common/HostAuthority.cs`).

```csharp
using LostMemory.Networking.Common;

if (HostAuthority.IsHost) {
    // 호스트 또는 싱글 실행
} else {
    // 일반 클라이언트
}
```

### Q6. 본 규칙을 어겼을 때 어떻게 발견되나요?

- 컴파일 에러 (시그니처 변경 시 의존 코드가 즉시 깨짐)
- Phase B 머지 시 conflict
- 런타임에 NullReferenceException (자식 트랜스폼 변경 시)

**머지 전에 본 문서 체크리스트(§6) 한 번 확인** 이 가장 빠른 예방.

## 9. 위반 시 대응

규칙을 어긴 변경을 머지하기 전에 발견했다면:

1. 변경 의도 확인 — 정말 시그니처 변경이 필요한가?
2. 대안 검토 — 새 멤버 추가로 해결 가능한가?
3. 불가피하면 — Phase B 작업자와 합의, 양쪽 코드 동시 갱신

머지 후에 발견됐다면 — 빠르게 alarm 띄우고 같이 fix forward. revert 보다 fix 가 보통 빠르다.

## 10. 본 문서 갱신 시점

- Phase B 완료 후 — 보호 대상 클래스 목록 업데이트 (NetworkObject 도입 후 어떤 클래스가 진짜 동기화 대상인지 확정)
- 새 도메인(Save/Talent/Memory 등) 의 멀티 도입 시 — 각 도메인별 보호 규칙 추가
- 호스트 권위 → 다른 모델로 변경되는 경우 — 전면 개정

## 11. 관련 코드 빠른 참조

```csharp
// 호스트 권위 체크 (Phase A 산출)
LostMemory.Networking.Common.HostAuthority.IsHost
LostMemory.Networking.Common.HostAuthority.IsNetworkSessionActive

// 세션 진입점 (Phase A 산출)
LostMemory.Networking.Session.RelaySessionHost.CreateAsync(maxPlayers)
LostMemory.Networking.Session.RelaySessionClient.JoinByCodeAsync(code)
LostMemory.Networking.Session.RelaySession.LeaveAsync()

// 네트워크 로그 단일 진입점 (Phase A 산출)
LostMemory.Networking.Common.NetLog.Info(...)
LostMemory.Networking.Common.NetLog.Warn(...)
LostMemory.Networking.Common.NetLog.Error(...)

// Phase B 도입 예정 (현재 미존재)
LostMemory.Networking.Player.LocalPlayerResolver.LocalPlayer
LostMemory.Networking.Player.LocalPlayerResolver.LocalPlayerReady
```

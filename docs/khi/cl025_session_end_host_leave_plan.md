# CL-025: 세션 종료·호스트 이탈 처리 구현

소속 Epic: **Epic C. 멀티플레이 / 세션 네트워크**

선행: CL-020 (패키지), CL-021 (테스트 씬), CL-022 (플레이어 프리팹), CL-023 (호스트 생성), CL-024 (코드 입력 참가)

## Context

CL-024까지 호스트가 만들고 클라가 합류하는 흐름이 완성됐다. 본 CL은 **그 세션이 끝나는 모든 경로**를 graceful 하게 처리한다.

`docs/04_multiplayer.md:34-37` MVP 정책: **호스트 이탈 시 세션 종료**. 호스트 마이그레이션 없음. 모든 클라는 메인 메뉴로 복귀하면 끝. 재접속 미지원(`docs/04_multiplayer.md:40-45`).

검증 목표:
- "방 나가기" 버튼으로 호스트가 자발적으로 종료 → 모든 클라가 즉시 알림 + 메인 메뉴 복귀
- 호스트가 앱을 강제 종료(Alt+F4) → 클라가 5초 내 disconnect 감지 + 메인 메뉴 복귀
- 클라가 자발적으로 나감 → 호스트는 계속 진행 가능
- 네트워크 단절(Wi-Fi off) → 클라가 timeout 후 메인 메뉴 복귀, 앱 크래시 X
- 모든 종료 경로에서 Sessions 서버 측 세션이 누수되지 않음 (수 분 내 auto-cleanup 포함)

본 CL은 **연결 종료 흐름과 UI 복귀**만. 다음 방·다음 던전 진행 중 종료 시 진행도 저장 처리는 비범위(CL-055/Epic G).

## 현재 상태 (가정 + 의존)

| 항목 | 상태 |
|---|---|
| `SessionService.LeaveSessionAsync()` | CL-023 plan에 시그니처만 정의됨, 본 CL에서 본체 구현 보강 |
| 클라 disconnect 감지 | CL-021 검증에서 "콘솔 로그만" 확인, graceful 처리는 본 CL |
| `ConnectMenu` 상태 표시 | CL-021/024 — Disconnected/Host/Client 텍스트만, "Leave" 버튼 없음 |
| `HostCreateView` "취소" 버튼 | CL-023 plan에 존재 — 본 CL에서 LeaveSessionAsync로 결선 |
| `JoinByCodeView` "취소" 버튼 | CL-024 plan에 존재 — 합류 진행 중 abort용. **합류 후** 나가기는 본 CL이 별도 처리 |
| `Session.Deleted` / NGO `OnClientDisconnectCallback` 콜백 결선 | 없음 — 본 CL 신규 |

## 종료 경로 분류

총 5종. 각각 트리거·감지·처리가 다름.

| # | 경로 | 트리거 | 감지 메커니즘 | 처리 |
|---|---|---|---|---|
| 1 | 호스트 자발 종료 | "방 나가기" 버튼 (호스트) | Local | `SessionService.LeaveSessionAsync()` → Sessions API에 Delete → NGO Shutdown → 메인 메뉴 |
| 2 | 클라 자발 종료 | "방 나가기" 버튼 (클라) | Local | `SessionService.LeaveSessionAsync()` → Sessions API에 Leave(호스트는 유지) → NGO Shutdown → 메인 메뉴 |
| 3 | 호스트 강제 종료 | 앱 닫기, 크래시, Alt+F4 | 클라 측: NGO `OnClientDisconnectCallback`(LocalClient) + Sessions `Session.Deleted` 이벤트 | 클라: 알림 표시 + 메인 메뉴 복귀. 호스트 측 Sessions 서버는 timeout으로 auto-cleanup |
| 4 | 클라 강제 종료 | 클라 앱 닫기, Alt+F4 | 호스트 측: NGO `OnClientDisconnectCallback`(other) | 호스트: 자기 진행 유지, Sessions 자동 슬롯 회수 |
| 5 | 네트워크 단절 | Wi-Fi off, 케이블 빠짐 | 양측: NGO `OnTransportFailure` + timeout | 클라/호스트 둘 다 메인 메뉴 복귀 (호스트는 세션도 정리 시도) |

## 채택 설계: NetworkSessionGuard 단일 진입점

5개 경로의 처리 분기가 흩어지면 일관성이 깨진다. 모든 disconnect 경로를 `NetworkSessionGuard` 단일 컴포넌트가 수신해 `DisconnectReason` enum으로 정규화하고 이벤트 1개로 발사 → UI 측은 reason만 보고 메시지·복귀 처리.

```
[Local Leave 버튼] ──────┐
[NGO OnClientDisconnect] ─┼─→ NetworkSessionGuard.RaiseDisconnected(reason)
[NGO OnTransportFailure]  │            │
[Sessions Session.Deleted]┘            ↓
                                  SessionDisconnected 이벤트
                                       ↓
                          ┌──── ConnectMenu/SessionEndView 구독 ────┐
                          ↓                                          ↓
                     "메인 메뉴 복귀"                          "호스트가 방을 나갔습니다"
```

## 흐름 설계

### 호스트 자발 종료 (경로 1)
```
[호스트 화면] "방 나가기" 버튼
    ↓
[확인 모달] "정말 나가시겠습니까? 모든 참가자가 메인 메뉴로 돌아갑니다"
    ↓ 예
SessionService.LeaveSessionAsync(asHost: true)
    ├─ 1. Sessions API: Session.DeleteAsync (방 자체를 닫음)
    ├─ 2. NetworkManager.Singleton.Shutdown()
    └─ 3. NetworkSessionGuard.RaiseDisconnected(LocalLeave)
        ↓
[메인 메뉴]
```

### 클라 자발 종료 (경로 2)
```
[클라 화면] "방 나가기" 버튼
    ↓
SessionService.LeaveSessionAsync(asHost: false)
    ├─ 1. Sessions API: Session.LeaveAsync (호스트 세션은 유지)
    ├─ 2. NetworkManager.Singleton.Shutdown()
    └─ 3. NetworkSessionGuard.RaiseDisconnected(LocalLeave)
        ↓
[메인 메뉴]
```

### 호스트 강제 종료 → 클라 (경로 3)
```
[호스트 측] 앱 종료
    ↓ (NGO 패킷 끊김 + Sessions heartbeat 끊김)
[클라 측] NGO OnClientDisconnectCallback(LocalClientId)
         OR Sessions Session.Deleted 이벤트
    ↓
NetworkSessionGuard.RaiseDisconnected(HostEnded)
    ↓
[알림 모달] "호스트가 방을 나갔습니다"
    ↓ 확인 (또는 3초 자동)
[메인 메뉴]
```

### 네트워크 단절 (경로 5)
```
[양측] NGO OnTransportFailure
       OR OnClientDisconnect with timeout
    ↓
NetworkSessionGuard.RaiseDisconnected(NetworkTimeout)
    ↓
[알림 모달] "네트워크 연결이 끊어졌습니다"
    ↓
[메인 메뉴]
호스트 측은 추가로 LeaveSessionAsync를 best-effort 호출 (실패 허용)
```

## 작업 범위

### Step 1 — DisconnectReason enum + SessionService 확장

`Assets/_Project/Scripts/Runtime/Networking/SessionService.cs` 확장:

```text
public enum DisconnectReason {
    LocalLeave,      // 자기가 나감 (Leave 버튼)
    HostEnded,       // 호스트가 종료/이탈
    NetworkTimeout,  // 네트워크 단절
    Kicked,          // 향후 확장 (현재 미사용)
    Unknown,
}

public partial class SessionService {
    public bool IsHost { get; private set; }   // CreateHostSessionAsync 성공 시 true, JoinByCodeAsync 성공 시 false
    public event Action<DisconnectReason, string> SessionDisconnected;

    public async Task LeaveSessionAsync();   // CL-023 시그니처에서 본체 구현 보강
}
```

`LeaveSessionAsync` 내부:
- IsHost == true: `ISession.DeleteAsync()` (Sessions가 자동으로 NGO Transport 정리)
- IsHost == false: `ISession.LeaveAsync()`
- 추가로 `NetworkManager.Singleton.Shutdown()` 명시 호출 (Sessions가 자동 처리 안 할 경우 가드)
- 마지막에 `SessionDisconnected?.Invoke(LocalLeave, "")` 발사
- 예외 발생해도 `SessionDisconnected` 만큼은 무조건 발사 (UI 복귀 보장)

### Step 2 — NetworkSessionGuard 신규

`Assets/_Project/Scripts/Runtime/Networking/NetworkSessionGuard.cs` 신규:

역할: 5개 disconnect 경로 모두 수신 → `SessionService.SessionDisconnected`로 정규화 발사.

구독 대상:
- `NetworkManager.Singleton.OnClientDisconnectCallback` — clientId가 LocalClientId면 본인이 disconnect됨을 의미
- `NetworkManager.Singleton.OnTransportFailure` — UTP 단절
- `ISession.Changed` / `ISession.RemovedFromSession` — Sessions가 호스트 종료를 알려줌 (패키지 버전 따라 이벤트명 다름, 작업자 IntelliSense 확인)
- `Application.quitting` — 자기 앱이 종료될 때 best-effort `LeaveSessionAsync` 호출 (await 불가하므로 fire-and-forget)

분류 규칙:
- LocalClientId가 disconnect: 자기가 의도적으로 Leave를 호출했으면 `LocalLeave`, 아니면 호스트(IsHost=false 측이) `HostEnded`, 호스트(IsHost=true 측이)가 받았다면 `NetworkTimeout`
- `OnTransportFailure`: 무조건 `NetworkTimeout`
- `Session.Deleted`: 클라가 받으면 `HostEnded`, 호스트가 받으면 (자기가 호출한 거라) 무시
- 위 모두 미해당: `Unknown` + raw 메시지 동봉

호스트 측 `Application.quitting`: best-effort `Session.DeleteAsync()`. 실패해도 Sessions 서버 timeout(수 분)으로 회수.

### Step 3 — UI Leave 버튼

수정 대상:
- `Assets/_Project/Scripts/Runtime/Networking/HostCreateView.cs` (CL-023): "취소" 버튼이 이미 존재 → onClick 결선을 본 CL에서 `SessionService.LeaveSessionAsync` 호출로 정식 결선. 합류 전(코드만 표시 상태)에서도 동일 처리.
- `Assets/_Project/Scripts/Runtime/Networking/JoinByCodeView.cs` (CL-024): "취소" 버튼은 **합류 진행 중**에만 동작. **합류 후** 나가기는 별도 UI 필요 (아래 신규).

신규: `Assets/_Project/Scripts/Runtime/Networking/InGameLeaveButton.cs` + 게임 씬 캔버스의 작은 "방 나가기" 버튼.
- 호스트/클라 공통 사용 (IsHost 자동 판별)
- 호스트가 누르면 확인 모달 → SessionService.LeaveSessionAsync(asHost=true)
- 클라가 누르면 확인 모달 없이 바로 LeaveSessionAsync(asHost=false)

### Step 4 — SessionEndView (알림 + 메인 메뉴 복귀)

`Assets/_Project/Scripts/Runtime/Networking/SessionEndView.cs` 신규 + `Assets/_Project/Prefabs/UI/SessionEndPanel.prefab`

역할: `SessionService.SessionDisconnected` 구독 → reason별 메시지 표시 → "확인" 또는 3초 자동 후 메인 메뉴 복귀.

reason별 메시지:
| DisconnectReason | 메시지 |
|---|---|
| `LocalLeave` | (메시지 없이 즉시 복귀) |
| `HostEnded` | "호스트가 방을 나갔습니다" |
| `NetworkTimeout` | "네트워크 연결이 끊어졌습니다" |
| `Kicked` | "방에서 추방되었습니다" |
| `Unknown` | "연결이 종료되었습니다 ({raw})" |

"메인 메뉴 복귀" = 현재 씬은 `CL021_NetworkTest_2P` 한 개뿐이므로 ConnectMenu 상태를 Disconnected로 리셋 + 그 외 패널 모두 비활성. 정식 메인 메뉴 씬은 CL-061 클라3.

### Step 5 — ConnectMenu 상태 머신 보강

`ConnectMenu.cs` 수정: 단순 4버튼 → 상태 머신:

```text
Disconnected: [Host 만들기] [코드로 참가] (디버그: Local Host/Server)
Connecting:   [연결 중...] [취소]
InRoom_Host:  ["코드 ABC123 표시"] [방 나가기]   (HostCreateView 흡수 또는 토글)
InRoom_Client:[연결됨, 호스트={pid}] [방 나가기]
EndedNotice:  ["호스트가 방을 나갔습니다"] [확인]   (SessionEndView 흡수 또는 토글)
```

상태 전이는 `SessionService` 이벤트 4종 구독:
- `SessionCreated` (CL-023) → `InRoom_Host`
- `SessionJoined` (CL-024) → `InRoom_Client`
- `JoinFailed` (CL-024) → `Disconnected` (인라인 메시지)
- `SessionDisconnected` (본 CL) → `EndedNotice` (LocalLeave면 즉시 Disconnected)

### Step 6 — 디버그 로그 (CL-031 자료)

```text
[Session] Leave start asHost={bool}
[Session] Leave OK reason={enum}
[Session] Leave FAILED ex={message}
[NGO] OnClientDisconnect clientId={id} isLocal={bool}
[NGO] OnTransportFailure
[Sessions] Session.Deleted
[Guard] Disconnected reason={enum} message={raw}
```

### Step 7 — E2E 검증 시나리오

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 호스트 "방 나가기" → 확인 → Yes | 호스트 1초 내 메인 메뉴, 클라는 "호스트가 방을 나갔습니다" 모달 후 메인 메뉴 |
| 2 | 호스트 "방 나가기" → 확인 → No | 그대로 호스트 상태 유지 |
| 3 | 클라 "방 나가기" | 클라 1초 내 메인 메뉴, 호스트는 정상 진행 (NetworkSyncProbe Tick 계속) |
| 4 | 호스트 앱 강제 종료 (Alt+F4) | 클라 5초 내 disconnect 감지 → "호스트가 방을 나갔습니다" → 메인 메뉴 |
| 5 | 클라 앱 강제 종료 | 호스트는 정상 진행, 콘솔에 OnClientDisconnect 로그 |
| 6 | 호스트 Wi-Fi 끄기 | 양측 NetworkTimeout 메시지 후 메인 메뉴 (10초 이내) |
| 7 | 클라 Wi-Fi 끄기 | 클라 NetworkTimeout 메시지 후 메인 메뉴, 호스트 진행 유지 |
| 8 | 합류 즉시 호스트가 종료 | 클라가 합류 직후라도 깔끔히 메인 메뉴 복귀, 좀비 캐릭터 X |
| 9 | 자발 종료 후 다시 방 만들기 | 새 코드 발급 정상, Sessions 서버에 잔재 X |
| 10 | 5회 방 생성/종료 반복 | 메모리 누수, 좀비 세션 X |

## 비범위

- **호스트 마이그레이션** — `docs/12_development_plan.md:75` 후순위
- **재접속/세션 유지** — `docs/04_multiplayer.md:40-45` MVP 미지원
- **진행도 저장 (다음 방·보스 직전 종료 등)** — CL-055 / Epic G
- **참여 코드 입력 UI 폴리시** — CL-060
- **참가 실패·접속 오류 예외 흐름** — CL-030 (본 CL은 합류 후 종료만)
- **정식 토스트/모달 UI 디자인** — CL-066
- **메인 메뉴 씬** — CL-061 (본 CL은 ConnectMenu Disconnected 상태로 흡수)
- **방에서 추방(Kick)** — enum 자리만 잡고 실제 흐름은 후순위

## 사용자 결정 필요

| 항목 | 옵션 | 권장 |
|---|---|---|
| 호스트 자발 종료 시 확인 모달 | 표시 / 생략 | **표시** (참가자에게 영향 큼, 실수 방지) |
| 클라 자발 종료 시 확인 모달 | 표시 / 생략 | **생략** (자기만 영향, 빠른 UX) |
| HostEnded 메시지 표시 시간 | 자동 닫기 / 확인 버튼 | **확인 버튼 + 5초 자동** (사용자가 읽었음을 보장하면서 정체 방지) |
| `Application.quitting` 시 LeaveAsync await | 동기 await / fire-and-forget | **fire-and-forget** (Unity는 quit에서 await 불가, Sessions timeout 백업) |
| disconnect 시 NetworkManager.Shutdown | 자동 / 수동 | **수동 명시** (Sessions가 자동 처리 안 하는 패키지 버전 가드) |
| Kick reason | enum 정의만 / 실제 RPC | **enum 정의만** (본 CL 비범위) |

## 위험 요소

1. **Sessions API 이벤트명 변동** — `Session.Deleted` / `Session.Changed` / `Session.RemovedFromSession` 등 이름이 패키지 버전마다 다름. 작업 전 1회 확인 + NGO `OnClientDisconnect` 만으로도 클라 측은 충분히 감지되므로 Sessions 이벤트는 **보조**.
2. **자발 vs 비자발 분기** — `OnClientDisconnect`는 두 경우 모두 발사. SessionService 내부에 `_intentionalLeave` 플래그를 두고 LeaveSessionAsync 시작 시 true, 콜백 처리 시 참조하여 LocalLeave vs HostEnded 분기.
3. **double Shutdown** — Sessions가 NGO 자동 정리 + LeaveSessionAsync에서 명시 Shutdown → 두 번 호출 시 NullRef 가능. `if (NetworkManager.Singleton.IsListening) Shutdown()` 가드.
4. **Application.quitting의 비동기 호출** — Unity는 quit 시점에 Task await 못함. fire-and-forget 호출 → 5초 timeout으로 best-effort. 실패해도 Sessions 서버 timeout 백업.
5. **클라 측 호스트 종료 감지 지연** — UTP 기본 timeout 30초까지 갈 수 있음. NetworkManager.NetworkConfig.ClientConnectionBufferTimeout / ConnectTimeoutMS 5초로 축소 검토.
6. **모달 중첩** — disconnect와 LeaveSessionAsync가 짧은 시간에 겹치면 모달 2개 표시. SessionEndView가 LocalLeave는 즉시 닫고 그 외만 표시.
7. **PlayerPrefab 강제 정리** — NGO는 disconnect 시 Owner 클라의 NetworkObject를 자동 destroy하지만, 카메라 등이 그 transform을 참조하고 있으면 NullRef 1프레임. KhiPlayerCamera가 owner 객체 destroy 감지하고 fallback 처리하는지 확인(CL-022 plan 참조).
8. **메모리 누수** — `SessionService.SessionDisconnected`, `NetworkSessionGuard`의 NGO 콜백 unsubscribe 누락 시 다음 세션에서 이중 수신. 모든 Subscribe는 OnEnable, Unsubscribe는 OnDisable 짝맞춤.

## 변경/신규 파일

### 신규

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/NetworkSessionGuard.cs` | 5개 경로 정규화 → `SessionDisconnected` 단일 이벤트 |
| `Assets/_Project/Scripts/Runtime/Networking/SessionEndView.cs` | reason별 메시지 + 메인 메뉴 복귀 UI |
| `Assets/_Project/Prefabs/UI/SessionEndPanel.prefab` | 위 View 프리팹 |
| `Assets/_Project/Scripts/Runtime/Networking/InGameLeaveButton.cs` | 인게임 작은 "방 나가기" 버튼 컨트롤러 |
| `Assets/_Project/Prefabs/UI/InGameLeavePanel.prefab` | 위 버튼 프리팹 |

### 변경

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/SessionService.cs` | `LeaveSessionAsync` 본체 구현 + `DisconnectReason` enum + `SessionDisconnected` 이벤트 + `IsHost` 노출 |
| `Assets/_Project/Scripts/Runtime/Networking/HostCreateView.cs` | "취소" onClick → `LeaveSessionAsync(asHost=true)` 정식 결선 |
| `Assets/_Project/Scripts/Runtime/Networking/ConnectMenu.cs` | 5개 상태 머신 + SessionService 4개 이벤트 구독 |
| `Assets/Scenes/Test/CL021_NetworkTest_2P.unity` | NetworkSessionGuard GameObject + SessionEndPanel/InGameLeavePanel 캔버스 추가 |

### 미변경

- `UnityServicesBootstrap.cs` (CL-023 그대로)
- `JoinByCodeView.cs` (CL-024 그대로 — 취소 버튼은 합류 진행 중 abort 전용, 합류 후는 InGameLeaveButton 사용)
- `TestKhi_MinimalCharacter2D.prefab` (CL-022 그대로)
- 게임 콘텐츠 코드/씬

## 검증 (E2E)

1. Multiplayer Play Mode 2 인스턴스 (Different Auth Profile) — 호스트 + 클라 합류
2. 시나리오 표 #1~#10 순서대로 수행, 각 케이스에서:
   - 메시지 텍스트가 reason과 일치
   - 메인 메뉴(=ConnectMenu Disconnected) 복귀까지 5초 이내
   - 콘솔 에러 0건
   - 직후 다시 방 생성/합류 가능 (좀비 세션 X)
3. Unity Cloud 대시보드 Sessions 탭에서 종료된 세션이 자동 cleanup 되는지 1회 확인 (수 분 단위)
4. 5회 반복 후 메모리 프로파일러 — NetworkObject/SessionService 인스턴스 0건 잔존

## 관련 문서

- `docs/04_multiplayer.md:34-37` — "호스트 이탈 시 세션 종료" MVP 정책
- `docs/04_multiplayer.md:40-45` — 재접속 미지원
- `docs/12_development_plan.md:215, 247` — 1단계 완료 기준 "세션 종료 흐름이 최소한 동작", "호스트 종료·참가 취소·코드 오류 같은 기본 예외 흐름이 확인된다"
- `docs/14_client_jira_story_backlog.md:54` — CL-025 정의
- `docs/khi/cl021_network_test_scene_plan.md:78-79, 87, 135` — disconnect 콘솔 로그 검증 + 본 CL 인계
- `docs/khi/cl022_player_network_prefab_plan.md:178, 227` — PlayerPrefab 자동 제거 + 본 CL 인계
- `docs/khi/cl023_host_create_relay_code_plan.md:143, 167` — Session 누수 방지 본 CL 영역
- `docs/khi/cl024_code_input_join_plan.md` — 합류 후 종료의 출발점

## 다음 CL

- **CL-026**: 2인 이동 위치 동기화 — 본 CL까지 완료되면 안정적인 세션 라이프사이클 위에서 본격적인 게임 동기화를 시작할 수 있다.

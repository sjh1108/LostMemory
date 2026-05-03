# CL-030: 참가 실패·접속 오류 예외 흐름 구현

소속 Epic: **Epic C. 멀티플레이 / 세션 네트워크**

선행: CL-020 (패키지·Services), CL-021 (테스트 씬), CL-022 (PlayerPrefab), CL-023 (호스트 생성), CL-024 (코드 입력 참가), CL-025 (세션 종료·호스트 이탈)

## Context

CL-023~025까지 호스트 생성 / 참가 / 종료가 happy path + 기본 에러 분류로 동작한다. 그러나 **데모 환경**(시연·발표·심사장)에서는 happy path 외의 모든 경로가 잠재 실패다:

- 발표 와이파이가 갑자기 끊김 → 합류 진행 중 멈춤 → 시연자가 어디까지 갔는지 모름
- 잘못된 코드 5번 입력 후 정상 코드 입력 → "마지막 시도가 진짜 실패였는지 백그라운드에 좀비 task인지" 판단 불가
- 합류 직후 PlayerPrefab 미스폰 → 화면은 빈 룸, 시연 종료
- Services 초기화 실패 후 부트 화면에서 동결
- 호스트 생성 중 "방 만들기" 버튼 더블클릭 → Sessions 2개 발급 → 코드 어느 게 진짜인지 혼란
- 합류 진행 중 취소 → 백그라운드에서 합류 완료 → 좀비 캐릭터

CL-030은 그 모든 "치명적 데모 실패" 경로를 닫는다. **신규 기능 추가가 아니라 기존 흐름의 안전망**이며, 다음을 보장:

- 모든 async 단계에 timeout이 있고, 시간 초과 시 사용자에게 분류된 메시지 + 다시 시도 가능 상태로 복귀
- 어느 단계에서 실패해도 메인 메뉴 = `ConnectMenu Disconnected` 상태로 1회 클릭 안에 복귀
- 어느 단계에서 취소해도 백그라운드 task가 정리되고 좀비 세션 0건
- 같은 버튼 빠른 더블클릭 / 진행 중 다른 버튼 클릭이 안전 (반-중복 가드)
- Services / NGO / Sessions 어느 layer에서 raw 예외가 터져도 UI 동결·앱 크래시 없음
- 5분 데모 동안 어떤 입력 시퀀스든 시연자가 "재시작 없이 다시 호스트/참가 가능"

비목표:
- **Auth 토큰 영속화·재발급 정책** — `docs/04_multiplayer.md:42-45` MVP는 매 부팅 새 익명, 재접속 미지원
- **호스트 마이그레이션** — `docs/12_development_plan.md:75` 후순위
- **정식 토스트/모달 UI 디자인** — CL-066 (본 CL은 인라인 + 모달 최소 형태로 충분)
- **Lobby/세션 목록 탐색** — `docs/12_development_plan.md:543`
- **방 추방(Kick)** — CL-025 enum 자리만, 본 CL 미동작
- **로그 수집/원격 진단** — 본 CL은 콘솔 로그 + UI 메시지까지

## 현재 상태 (의존)

| 항목 | 상태 |
|---|---|
| `JoinFailureReason` enum (`InvalidCode`/`SessionFull`/`NetworkError`/`AuthError`/`Unknown`) | CL-024 정의 완료 |
| `DisconnectReason` enum (`LocalLeave`/`HostEnded`/`NetworkTimeout`/`Kicked`/`Unknown`) | CL-025 정의 완료 |
| `SessionService.SessionCreated`/`SessionJoined`/`SessionFailed`/`JoinFailed`/`SessionDisconnected` 이벤트 | CL-023~025 정의 완료 |
| `SessionService.JoinByCodeAsync(string, CancellationToken)` | CL-024 시그니처 |
| `HostCreateView.cs` "취소" 버튼 → LeaveSessionAsync | CL-025에서 결선 |
| `JoinByCodeView.cs` "취소" 버튼 → CancellationToken | CL-024 |
| `NetworkSessionGuard` 5경로 disconnect 정규화 | CL-025 |
| `ConnectMenu` 5상태 머신 (Disconnected/Connecting/InRoom_Host/InRoom_Client/EndedNotice) | CL-025 |
| `HostCreateFailureReason` enum | **없음 — 본 CL 신규** |
| 단계별 timeout | **없음 — 본 CL 신규** |
| 반-중복(double-click) 가드 | **부분 — 본 CL 강화** |
| Pre-flight 인터넷 reachability | **없음 — 본 CL 신규(선택)** |

## 실패 분류 (Taxonomy)

CL-030이 다루는 실패는 다음 4축 × 6단계로 정리된다.

### 4축

| 축 | 의미 | 분류 enum |
|---|---|---|
| Host 생성 실패 | 호스트가 방 만드는 중 실패 | `HostCreateFailureReason` (신규) |
| Join 실패 | 클라가 코드로 참가 중 실패 | `JoinFailureReason` (CL-024) |
| 접속 오류 | 합류 후 게임 중 끊김 | `DisconnectReason` (CL-025) |
| 사용자 취소 | 어느 단계든 취소 | (각 enum의 별도 멤버 또는 silent path) |

### 6단계 (Pipeline phase)

| Phase | Host | Client | 본 CL의 timeout 권장 |
|---|---|---|---|
| 1. ServicesInit | UnityServices.InitializeAsync | 동일 | **5s** |
| 2. AuthSignIn | AuthenticationService.SignInAnonymously | 동일 | **5s** |
| 3. SessionAcquire | CreateSessionAsync | JoinByCodeAsync | **10s** |
| 4. TransportBind | (Sessions 자동 또는 NetworkManager.StartHost) | (자동 또는 StartClient) | **3s** |
| 5. NgoApproval | (해당 없음) | NGO 클라가 NetworkConfig.ConnectionApproval 통과 대기 | **10s** |
| 6. PlayerSpawned | NetworkManager가 PlayerPrefab 자동 spawn | 동일 | **5s** |

각 phase는 독립 timeout. 한 phase에서 실패하면 그 시점까지 만든 자원을 **역순 정리**(Phase 6 실패 → 5,4,3 정리) 후 사용자에게 "어느 단계에서 무엇이 실패했는지" 분류된 메시지 표시.

## 채택 설계: ConnectionPipeline 단일 진행 모델

기존 SessionService는 "기능 메서드 묶음"(create/join/leave)이고 각 메서드가 자기 try/catch로 enum 발사. 단계별 timeout / 부분 정리 / 취소가 산발적이다.

본 CL은 그 위에 **ConnectionPipeline** 한 층을 둬서 phase 6단계를 명시적으로 진행:

```
[버튼 클릭] HostPipeline.RunAsync(token) / JoinPipeline.RunAsync(code, token)
              ↓
            Phase 1 (timeout 5s) → 실패: SetResult(HostFailure.ServicesUnavailable) 종료
              ↓
            Phase 2 (timeout 5s) → 실패: HostFailure.AuthFailed
              ↓
            Phase 3 (timeout 10s) → 실패: HostFailure.{ServiceError|RateLimited|Unknown}
              ↓
            Phase 4 (timeout 3s) → 실패: HostFailure.TransportBind
              ↓
            Phase 5 (Host: skip / Client: timeout 10s) → 실패: JoinFailure.NgoApprovalTimeout
              ↓
            Phase 6 (timeout 5s) → 실패: HostFailure.PlayerSpawnTimeout
              ↓
            SetResult(Success)
```

각 phase는:
- 시작 직전 콘솔 로그 `[Pipeline] Phase X start`
- timeout 또는 예외 시 `[Pipeline] Phase X FAILED reason={enum} ms={elapsed}`
- 성공 시 `[Pipeline] Phase X OK ms={elapsed}`
- token cancel 감지 시 `[Pipeline] Phase X CANCELLED` + 부분 정리 후 즉시 종료

Pipeline은 **이벤트 발사 1회**(`PipelineCompleted` with success/failure/cancelled)로 결과를 알린다. UI는 phase별 진행도 표시 + 결과 메시지.

## 흐름 설계

### Host 생성 (정상 + 실패 분기)

```
[버튼 클릭]
  ├─ 가드: pipeline.IsRunning == true ? return (반-중복)
  ├─ 가드: NetworkManager.Singleton.IsListening ? "이미 연결돼 있습니다" + return
  └─ HostPipeline.Run(token)
       ├─ Phase 1: ServicesInit
       │    ├─ 이미 init됐으면 skip (idempotent)
       │    └─ 5s timeout
       ├─ Phase 2: AuthSignIn
       │    ├─ 이미 sign-in이면 skip
       │    └─ 5s timeout
       ├─ Phase 3: SessionAcquire (CreateSessionAsync)
       │    ├─ 10s timeout
       │    └─ 예외 → HostFailureReason 매핑
       ├─ Phase 4: TransportBind
       │    └─ Sessions 자동 결선 검증, 미결선 시 StartHost 명시 호출, 3s 안에 IsHost=true
       ├─ Phase 6: PlayerSpawned (Phase 5 skip)
       │    └─ 5s 안에 자기 클라이언트의 PlayerObject 존재 확인
       └─ Done
```

### Join (정상 + 실패 분기)

```
[코드 입력 + 참가 버튼]
  ├─ 가드: pipeline.IsRunning ? return
  ├─ 가드: code 영숫자 6자 검증 (CL-024 자체 검증, 본 CL은 그 위에 trim 추가)
  ├─ 선택 가드: pre-flight reachability 체크 (DNS / ping)
  └─ JoinPipeline.Run(code, token)
       ├─ Phase 1: ServicesInit  (5s)
       ├─ Phase 2: AuthSignIn   (5s)
       ├─ Phase 3: SessionAcquire (JoinByCodeAsync, 10s, 예외 → JoinFailureReason)
       ├─ Phase 4: TransportBind (3s)
       ├─ Phase 5: NgoApproval  (10s, NetworkManager.IsConnectedClient 도달)
       ├─ Phase 6: PlayerSpawned (5s)
       └─ Done
```

### 사용자 취소

```
[취소 버튼]
  └─ token.Cancel()
       ├─ 진행 중 phase 즉시 ThrowIfCancellationRequested
       ├─ 부분 정리:
       │   - Phase 3 이후 cancel: 본인이 만든/합류한 ISession을 LeaveAsync(asHost: created?)
       │   - Phase 4~6 cancel: NetworkManager.Shutdown()
       └─ PipelineCompleted(Cancelled, "")
```

### 합류 후 접속 오류 (CL-025와의 경계)

```
[게임 중 disconnect]
  └─ NetworkSessionGuard (CL-025) 처리
       └─ SessionDisconnected(reason)
       
본 CL이 추가하는 것:
  - reason=NetworkTimeout일 때 "재시도 / 메인 메뉴" 2버튼 모달 (CL-025는 메인 메뉴만)
  - "재시도" 클릭 → 직전 코드로 JoinPipeline 자동 재시작 (호스트 측은 "다시 방 만들기")
  - 재시도는 1회만, 또 실패하면 "메인 메뉴"만 노출
```

## 작업 범위

### Step 1 — HostCreateFailureReason enum 신규

`Assets/_Project/Scripts/Runtime/Networking/SessionService.cs` 확장:

```text
public enum HostCreateFailureReason {
    ServicesUnavailable,   // Phase 1 timeout/실패
    AuthFailed,            // Phase 2 실패
    SessionServiceError,   // Phase 3 일반 (대시보드 미활성, 권한 부족 등)
    SessionRateLimited,    // Phase 3 429
    NetworkError,          // 인터넷 단절 / Relay UDP 차단
    TransportBindFailed,   // Phase 4 실패
    PlayerSpawnTimeout,    // Phase 6 실패 (PlayerPrefab 미등록 등)
    Cancelled,
    Unknown,               // raw 메시지 동봉
}

public partial class SessionService {
    public event Action<HostCreateFailureReason, string> HostCreateFailed;
}
```

`JoinFailureReason`(CL-024)에도 phase 5/6 대응 추가 권장:
```text
NgoApprovalTimeout,    // Phase 5 timeout
PlayerSpawnTimeout,    // Phase 6 timeout
Cancelled,             // 명시 멤버
```
기존 5종 + 3종 = 8종.

### Step 2 — ConnectionPipeline 신규

`Assets/_Project/Scripts/Runtime/Networking/ConnectionPipeline.cs` 신규 (abstract):

```text
public enum PipelinePhase { Idle, ServicesInit, AuthSignIn, SessionAcquire, TransportBind, NgoApproval, PlayerSpawned, Done, Failed, Cancelled }
public enum PipelineResult { Success, Failed, Cancelled }

public abstract class ConnectionPipeline {
    public PipelinePhase CurrentPhase { get; private set; }
    public bool IsRunning => CurrentPhase != PipelinePhase.Idle && CurrentPhase < PipelinePhase.Done;
    public event Action<PipelinePhase> PhaseChanged;
    public event Action<PipelineResult, string> Completed;

    protected async Task RunInternalAsync(CancellationToken ct) {
        try {
            await StepAsync(PipelinePhase.ServicesInit, 5_000, RunServicesInit, ct);
            await StepAsync(PipelinePhase.AuthSignIn,  5_000, RunAuthSignIn,  ct);
            await StepAsync(PipelinePhase.SessionAcquire, 10_000, RunSessionAcquire, ct);
            await StepAsync(PipelinePhase.TransportBind, 3_000, RunTransportBind, ct);
            if (RequiresNgoApproval) await StepAsync(PipelinePhase.NgoApproval, 10_000, RunNgoApproval, ct);
            await StepAsync(PipelinePhase.PlayerSpawned, 5_000, RunPlayerSpawned, ct);
            SetResult(PipelineResult.Success, "");
        }
        catch (OperationCanceledException) {
            await CleanupAsync();
            SetResult(PipelineResult.Cancelled, "");
        }
        catch (TimeoutException te) {
            await CleanupAsync();
            SetResult(PipelineResult.Failed, MapTimeoutToReason(CurrentPhase));
        }
        catch (Exception ex) {
            await CleanupAsync();
            SetResult(PipelineResult.Failed, MapExceptionToReason(CurrentPhase, ex));
        }
    }

    protected abstract bool RequiresNgoApproval { get; }
    protected abstract Task RunServicesInit(CancellationToken ct);
    protected abstract Task RunAuthSignIn(CancellationToken ct);
    protected abstract Task RunSessionAcquire(CancellationToken ct);
    protected abstract Task RunTransportBind(CancellationToken ct);
    protected abstract Task RunNgoApproval(CancellationToken ct);
    protected abstract Task RunPlayerSpawned(CancellationToken ct);
    protected abstract Task CleanupAsync();
    protected abstract string MapTimeoutToReason(PipelinePhase phase);
    protected abstract string MapExceptionToReason(PipelinePhase phase, Exception ex);
}
```

`StepAsync`는 `Task.WhenAny(action, Task.Delay(timeout, ct))`로 timeout 강제. `MapTimeoutToReason`/`MapExceptionToReason`은 `HostCreateFailureReason` 또는 `JoinFailureReason`을 string으로 직렬화(예: `"NetworkError"`).

### Step 3 — HostPipeline / JoinPipeline 구현

`Assets/_Project/Scripts/Runtime/Networking/HostPipeline.cs` / `JoinPipeline.cs` 신규.

`HostPipeline.RequiresNgoApproval = false`. `JoinPipeline = true`.

`RunSessionAcquire`는 각자 `SessionService.CreateHostSessionAsync` / `JoinByCodeAsync` 호출. `RunTransportBind`는 `IsHost`/`IsClient` polling. `RunPlayerSpawned`는 `NetworkManager.SpawnManager.PlayerObject`(Owner 기준) 또는 `LocalClient.PlayerObject`이 null 아닌지 polling.

`CleanupAsync`는 phase 역순:
- Phase 4~6 진입 후 실패: `NetworkManager.Singleton.Shutdown()` + Sessions 측 Leave/Delete
- Phase 3 진입 후 실패: Sessions 측 Leave/Delete만
- Phase 1~2 실패: 정리할 자원 없음

### Step 4 — UI 반-중복 가드

`HostCreateView` / `JoinByCodeView` 양쪽:

```text
private bool _isPending;

public void OnSubmitClicked() {
    if (_isPending) return;
    if (NetworkManager.Singleton.IsListening) { ShowMessage("이미 연결돼 있습니다"); return; }
    _isPending = true;
    submitButton.interactable = false;
    pipeline.Completed += OnCompleted;
    pipeline.RunAsync(...);
}

void OnCompleted(PipelineResult r, string reason) {
    _isPending = false;
    submitButton.interactable = true;
    pipeline.Completed -= OnCompleted;
    // r/reason에 따라 UI 갱신
}
```

추가로 EventSystem의 `submit` 이벤트가 frame당 다중 발사되지 않도록 `Input.anyKeyDown` 체크는 `Update` 1회로 한정.

### Step 5 — 단계별 timeout UI 표시

`Assets/_Project/Prefabs/UI/PipelineProgressPanel.prefab` 신규 (또는 기존 ConnectMenu 확장):

진행 중 단계 표시:
```
[연결 중] Services 초기화...    (1/6)
[연결 중] 인증 중...             (2/6)
[연결 중] 세션 합류 중...        (3/6)
[연결 중] Transport 연결 중...   (4/6)
[연결 중] 호스트와 동기화 중...  (5/6)
[연결 중] 캐릭터 생성 중...      (6/6)
[취소]
```

PhaseChanged 이벤트 구독해 텍스트 갱신. 취소 버튼은 항상 활성. 한 phase가 길어지면 "예상보다 시간이 걸리고 있습니다" 보조 텍스트 (3s 이상 phase에 한해).

### Step 6 — 실패 메시지 매핑

본 CL이 정의하는 메시지 표 (CL-024/025의 표 확장):

`HostCreateFailureReason`:
| enum | 메시지 |
|---|---|
| `ServicesUnavailable` | "서비스 초기화에 실패했습니다 (네트워크 확인)" |
| `AuthFailed` | "인증에 실패했습니다 (다시 시도)" |
| `SessionServiceError` | "세션 서버에 접속할 수 없습니다 (잠시 후 다시 시도)" |
| `SessionRateLimited` | "요청이 너무 많습니다. 잠시 후 다시 시도해주세요" |
| `NetworkError` | "네트워크 연결을 확인해주세요" |
| `TransportBindFailed` | "Relay 연결에 실패했습니다 (방화벽/네트워크 확인)" |
| `PlayerSpawnTimeout` | "캐릭터 생성에 실패했습니다. 다시 시도해주세요" |
| `Cancelled` | (메시지 없이 닫기) |
| `Unknown` | "알 수 없는 오류 ({raw})" |

`JoinFailureReason` 추가분:
| enum | 메시지 |
|---|---|
| `NgoApprovalTimeout` | "호스트 응답이 없습니다. 코드를 다시 확인해주세요" |
| `PlayerSpawnTimeout` | "캐릭터 생성에 실패했습니다. 다시 시도해주세요" |
| `Cancelled` | (메시지 없이 닫기) |

`DisconnectReason`(CL-025)은 그대로 + "재시도" 버튼 추가 노출:

| enum | 메시지 | 버튼 |
|---|---|---|
| `NetworkTimeout` | "네트워크 연결이 끊어졌습니다" | [재시도] [메인 메뉴] |
| `HostEnded` | "호스트가 방을 나갔습니다" | [확인] |
| 기타 | (CL-025 그대로) | [확인] |

### Step 7 — 재시도 정책

`Assets/_Project/Scripts/Runtime/Networking/ReconnectController.cs` 신규:

- 최근 성공 호스트 코드 / 자기 호스트 옵션을 `_lastJoinCode` / `_wasHosting`에 캐시
- `NetworkTimeout` 발생 시 "재시도" 버튼 표시
- 클릭 시 1회만 자동 재실행 (호스트 = HostPipeline, 클라 = JoinPipeline with `_lastJoinCode`)
- 재시도 결과:
  - 성공 → 정상 게임 화면
  - 실패 → 인라인 메시지만, 더 이상 자동 재시도 안 함
- 사용자가 명시적으로 "메인 메뉴"를 누르면 캐시 클리어

전송 타임아웃 자동 재시도(transient retry)는 **사용자 가시 재시도 1회만**. 백그라운드 재시도 X (좀비 task 방지).

### Step 8 — Pre-flight 체크 (선택)

`Assets/_Project/Scripts/Runtime/Networking/PreflightCheck.cs` 신규 (선택):

`Application.internetReachability != NetworkReachability.NotReachable` 1회 검증.

`UnityWebRequest`로 Unity Cloud endpoint(또는 단순 `https://services.api.unity.com/`)에 HEAD 요청, 2s timeout, 200/204면 OK.

**미달 시**: 인라인 메시지 "네트워크 연결을 확인해주세요" + 버튼 비활성. 본 CL은 선택, 작업자 판단.

`Application.internetReachability` 단독 의존은 **신뢰 낮음**(Wi-Fi 잡혔지만 인터넷 안 되는 케이스 잡지 못함) → 위 endpoint 호출이 실질 판정.

### Step 9 — 디버그 로그 (CL-031 자료)

```text
[Pipeline] HOST start
[Pipeline] Phase ServicesInit start
[Pipeline] Phase ServicesInit OK ms=143
[Pipeline] Phase AuthSignIn OK ms=412
[Pipeline] Phase SessionAcquire OK ms=1837 sessionId={id} code={code}
[Pipeline] Phase TransportBind OK ms=87
[Pipeline] Phase PlayerSpawned OK ms=312
[Pipeline] HOST done
...
[Pipeline] JOIN start code={code}
[Pipeline] Phase SessionAcquire FAILED ex=SessionsNotFound reason=InvalidCode ms=1234
[Pipeline] JOIN failed reason=InvalidCode
[Pipeline] Cleanup phase=SessionAcquire (no-op)
```

각 phase 시작/종료/실패가 한 줄씩. CL-031에서 이 로그를 패턴화한다.

### Step 10 — E2E 검증 시나리오

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 정상 호스트 생성 | 6 phase 모두 OK 로그, 코드 표시, IsHost=true |
| 2 | 정상 클라 합류 | 6 phase OK, 게임 진입, NetworkSyncProbe 동기 |
| 3 | "방 만들기" 버튼 0.1s 간격 5번 클릭 | Pipeline 1개만 진행, 나머지 무시. Sessions 1개만 생성 |
| 4 | "참가" 버튼 더블클릭 | 동일. JoinPipeline 1개만 |
| 5 | 정상 합류 후 "방 만들기" 클릭 | 가드 메시지 "이미 연결돼 있습니다" |
| 6 | 5자 코드 입력 | submit 비활성, Pipeline 미시작 |
| 7 | 임의 6자 (없는 코드) | Phase 3 실패 → `InvalidCode` 메시지 |
| 8 | 와이파이 끄고 호스트 시도 | Phase 1 또는 3 timeout → `NetworkError` |
| 9 | 호스트 시도 중 와이파이 끄기 | 진행 중 phase timeout → 정리 후 메시지 |
| 10 | 합류 진행 중 취소 버튼 | Cleanup 실행, Sessions 측 잔재 0 |
| 11 | 합류 진행 중 취소 직후 다시 합류 | 정상 재진입 |
| 12 | NetworkManager.PlayerPrefab 슬롯 비워두고 합류 | Phase 6 timeout → `PlayerSpawnTimeout` |
| 13 | 합류 후 와이파이 끄기 | Phase 통과 후 `NetworkTimeout` 모달 + [재시도]/[메인 메뉴] |
| 14 | [재시도] 클릭 | JoinPipeline 자동 재실행, 1회만. 또 실패 시 메시지만 |
| 15 | UnityServices 대시보드 비활성 환경 | Phase 1 또는 3에서 401/403 → `SessionServiceError` (앱 크래시 X) |
| 16 | Sessions API rate limit 시뮬 (인위 빠른 반복) | `SessionRateLimited` 메시지 |
| 17 | 호스트 강제 Alt+F4 직후 클라 합류 | Phase 3에서 `InvalidCode` (Sessions 측 정리 시점에 따라 살짝 늦게) |
| 18 | 5분 데모 시뮬 (랜덤 입력 시퀀스) | 어떤 시점에도 앱 크래시/UI 동결 없음, 메인 메뉴 복귀 항상 가능 |

## 비범위

- **Auth 토큰 재발급/영속화** — MVP 미지원
- **호스트 마이그레이션** — 후순위
- **Lobby 검색·필터** — 후순위
- **정식 토스트/모달 디자인** — CL-066
- **로그 원격 수집** — 본 CL은 콘솔까지
- **Kick 흐름** — enum 자리만, 실제 RPC 후순위
- **재접속 정책** — `docs/04_multiplayer.md:42-45` 미지원
- **본격 NetworkConfig.ConnectionApproval 콜백 구현** — Phase 5 통과 검증만, 실제 approval 로직(비밀번호·버전 체크 등)은 별도

## 사용자 결정 필요

| 항목 | 옵션 | 권장 |
|---|---|---|
| Phase별 timeout 값 | 5/10s 권장값 / 더 짧게(2/5) / 더 길게(10/20) | **권장값** (데모 환경 ≥ 평균 RTT 200ms 고려) |
| Pre-flight 체크 | 미사용 / Application.internetReachability만 / Unity endpoint HEAD | **Unity endpoint HEAD** (실질 판정) |
| 자동 재시도 | 1회 가시 재시도 / 무한 retry / 0회 | **1회 가시 재시도** (좀비 task 방지) |
| 단계 진행도 표시 | 1/6, 2/6 텍스트 / 단순 "연결 중..." / 프로그레스 바 | **1/6 텍스트** (디버그 가치 + 사용자 안심) |
| 더블클릭 가드 | UI 버튼 disable / IsRunning 가드 / 둘 다 | **둘 다** (이중 안전) |
| ConnectionPipeline 구현 위치 | SessionService 내부 partial / 별도 클래스 | **별도 클래스** (책임 분리, 본 CL의 핵심) |
| 실패 후 ConnectMenu 복귀 | 메시지 자동 닫고 즉시 복귀 / 사용자 확인 후 | **확인 후** (메시지 읽혔음을 보장) |
| 호스트 측 NgoApproval phase | 사용/skip | **skip** (호스트는 자기에게 approval 안 함) |

## 위험 요소

1. **ConnectionPipeline 추상화 비용 vs 가치** — Pipeline 도입 시 기존 SessionService 메서드를 직접 호출하던 곳을 모두 Pipeline 경유로 바꿔야 함. CL-023~025의 작업이 마무리된 직후라 의존부 적음 — 비용 낮음. 그러나 Pipeline이 또 다른 추상화 층이라 디버그 시 trace 길어짐.
2. **Phase 정의가 Sessions API 내부 단계와 안 맞을 가능성** — Sessions API가 내부적으로 Auth/Relay/NGO를 묶어 처리하면 Phase 1~5가 외부에서 명시 분리 안 될 수 있음. 그 경우 Pipeline은 "외부 가시 단계"만(예: Phase 3+4 통합) 표현. 내부 세부는 SessionsAPI 예외 메시지 + raw 동봉으로 보존.
3. **Timeout 발생 시 자원 정리 race** — Phase 3 timeout인데 직후 1초 뒤 SessionsAPI 응답 도착 → 좀비 ISession. CleanupAsync에서 timeout 후 한 번 더 LeaveAsync best-effort 호출 + Sessions 서버 자동 cleanup 백업.
4. **OperationCanceledException 누락 처리** — 일부 Sessions API 메서드가 token 무시. cancel 후 응답 도착 시 부분 정리 필요. CleanupAsync가 이 케이스도 처리.
5. **재시도 후에도 같은 실패** — `_lastJoinCode`로 같은 코드 재시도하면 호스트가 종료된 케이스에선 또 InvalidCode. UX 상 "재시도" 1회 + 실패 시 자동 [메인 메뉴]만 노출.
6. **Pre-flight HEAD 요청 자체가 timeout** — pre-flight도 timeout 가드(2s) 필요. 실패 시 "체크 실패, 그래도 시도하시겠습니까?" 대신 그냥 진행 권장(과도한 friction).
7. **Phase 6 PlayerSpawned 검증 정확도** — `NetworkManager.LocalClient.PlayerObject != null`이 OnNetworkSpawn 콜백 직후라야 정확. polling 빈도 100ms 권장.
8. **NetworkConnection approval timeout 5s vs 10s** — NGO 기본 ConnectionApprovalTimeout이 10s. Phase 5 timeout을 그보다 길게 두면 NGO 자체 timeout이 먼저. 본 plan은 동일 10s — 동시 도착 가능. 안전하게 11s로 둘지 작업자 판단.
9. **Pipeline 인스턴스 재사용** — 한 번 끝난 Pipeline을 다시 RunAsync 호출 시 상태 잔재 가능. Pipeline은 매번 새 인스턴스 생성 또는 명시 Reset() 메서드.
10. **UI 진행도 phase 점프** — Phase 1~2가 빠르면 1/6, 2/6이 0.1s씩 깜빡임. 50ms 미만은 표시 생략 권장.
11. **테스트 환경 빠른 RTT vs 데모 환경 느린 RTT** — 로컬 Multiplayer Play Mode는 RTT < 1ms. 데모 외부망은 100ms+. timeout 값은 데모 RTT 기준으로 잡아야 함. 본 plan은 모두 외부망 기준.
12. **NetworkSessionGuard와 Pipeline 이벤트 중첩** — Pipeline 진행 중 OnClientDisconnect가 발사되면 Guard와 Pipeline 둘 다 처리. Pipeline IsRunning 상태에선 Guard가 발사 안 하도록 가드(Guard.Suppress(true) flag).

## 변경/신규 파일

### 신규

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/ConnectionPipeline.cs` | 6 phase + timeout + cleanup + cancel 베이스 |
| `Assets/_Project/Scripts/Runtime/Networking/HostPipeline.cs` | Host용 phase 구현 |
| `Assets/_Project/Scripts/Runtime/Networking/JoinPipeline.cs` | Client용 phase 구현 |
| `Assets/_Project/Scripts/Runtime/Networking/ReconnectController.cs` | 재시도 1회 + 캐시 |
| `Assets/_Project/Scripts/Runtime/Networking/PreflightCheck.cs` (선택) | 인터넷 reachability + Unity endpoint HEAD |
| `Assets/_Project/Prefabs/UI/PipelineProgressPanel.prefab` | phase 진행도 UI (또는 ConnectMenu 확장) |

### 변경

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/SessionService.cs` | `HostCreateFailureReason` enum + `HostCreateFailed` 이벤트 + `JoinFailureReason`에 NgoApprovalTimeout/PlayerSpawnTimeout/Cancelled 추가 |
| `Assets/_Project/Scripts/Runtime/Networking/HostCreateView.cs` | HostPipeline 결선 + `_isPending` 가드 + 진행도 표시 |
| `Assets/_Project/Scripts/Runtime/Networking/JoinByCodeView.cs` | JoinPipeline 결선 + `_isPending` 가드 + 진행도 표시 |
| `Assets/_Project/Scripts/Runtime/Networking/ConnectMenu.cs` | "이미 연결돼 있습니다" 가드 + Pipeline IsRunning 동안 다른 버튼 disable |
| `Assets/_Project/Scripts/Runtime/Networking/SessionEndView.cs` (CL-025) | `NetworkTimeout` 케이스에 [재시도] 버튼 추가 → ReconnectController 호출 |
| `Assets/_Project/Scripts/Runtime/Networking/NetworkSessionGuard.cs` (CL-025) | Pipeline IsRunning 동안 disconnect 콜백 suppress 옵션 |
| `Assets/Scenes/Test/CL021_NetworkTest_2P.unity` | PipelineProgressPanel 캔버스 추가 |

### 미변경

- `UnityServicesBootstrap.cs` (CL-023) — Pipeline 안에서 idempotent 호출
- `TestKhi_MinimalCharacter2D.prefab` (CL-022)
- 게임 콘텐츠 / 룸 / 보상

## 검증 (E2E)

1. Multiplayer Play Mode 2 인스턴스 (Different Auth Profile)
2. 시나리오 표 #1~#18 순서대로 수행
3. 각 시나리오에서:
   - 콘솔 에러 0건 (Warning은 의도적 케이스에서만)
   - UI 동결 0건 (5초 이상 응답 없음 = 실패)
   - 메인 메뉴 복귀 클릭 1회 안에 가능
   - 직후 정상 호스트/합류 가능 (좀비 X)
4. 5분 자유 입력 시퀀스 시뮬 (랜덤 클릭, Alt+F4, Wi-Fi 토글) — 앱 크래시 0
5. Unity Cloud 대시보드 Sessions 탭 — 검증 후 잔존 세션 0
6. 메모리 프로파일러 — Pipeline/SessionService 인스턴스 0 잔존

## 관련 문서

- `docs/04_multiplayer.md:18, 42-45` — 세션 흐름 + 재접속 미지원 (본 CL의 비범위 경계)
- `docs/12_development_plan.md:215, 247` — 1단계 완료 기준 "호스트 종료·참가 취소·코드 오류 같은 기본 예외 흐름이 확인된다" — **본 CL의 직접 근거**
- `docs/14_client_jira_story_backlog.md:58` — CL-030 정의
- `docs/khi/cl023_host_create_relay_code_plan.md:138, 167` — Session 누수 방지 + 본 CL 인계
- `docs/khi/cl024_code_input_join_plan.md:82-94, 154-156` — JoinFailureReason 5종 + 비범위 "정식 UI는 CL-030/066"
- `docs/khi/cl025_session_end_host_leave_plan.md:245` — 비범위 "참가 실패·접속 오류 예외 흐름 — CL-030"

## 다음 CL

- **CL-031**: 네트워크 핵심 로그 포인트 정리 — 본 CL이 만든 phase별 로그 + CL-023~025 로그를 패턴 일관성 있게 정리. 본 CL의 검증을 운영 환경에서도 추적 가능하게.

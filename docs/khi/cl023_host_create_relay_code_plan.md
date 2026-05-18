# CL-023: 호스트 생성 및 Relay 참여 코드 발급 구현

소속 Epic: **Epic C. 멀티플레이 / 세션 네트워크**

선행: CL-020 (패키지·Services 활성화), CL-021 (테스트 씬), CL-022 (플레이어 프리팹)

## Context

CL-021의 로컬 직결(127.0.0.1)을 **Relay 기반 원격 접속**으로 확장한다. 호스트가 다른 네트워크 상의 친구와 NAT을 우회해 접속할 수 있게, Unity Multiplayer Services의 **Sessions API**(Relay를 자동 wrap)를 사용해 세션을 만들고 6자리 참여코드를 발급한다.

목표는 다음을 검증:
- Unity Services 초기화 + 익명 로그인 흐름이 첫 진입 시 안정적
- 호스트가 "방 만들기" 버튼 한 번으로 Session 생성 + 참여코드 발급
- 발급된 코드가 UI에 표시되고, 후속 CL-024가 이 코드로 접속 가능

본 CL은 **호스트 측 흐름만**. 클라이언트 코드 입력 참가는 CL-024.

## 현재 상태 (가정)

| 항목 | 가정 |
|---|---|
| 패키지 | CL-020에서 `com.unity.services.multiplayer`, `com.unity.services.authentication`, `com.unity.services.core` 설치 |
| Services 대시보드 | Authentication, Multiplayer Services(Sessions/Relay) 활성화 완료 |
| 익명 로그인 | 첫 호출 시 `AuthenticationService` 익명 토큰 발급 가능 상태 |
| NetworkManager | CL-021 테스트 씬에 배치, PlayerPrefab=TestKhi 등록(CL-022) |
| ConnectMenu | CL-021 임시 UI 존재 (Host/Client/Server/Disconnect 버튼) |

## 채택 API: Sessions vs 직접 Relay

| 옵션 | 장점 | 단점 |
|---|---|---|
| (A) Sessions API (com.unity.services.multiplayer) | Relay+NGO Transport 자동 결선, 메타데이터(인원수·이름) 내장, 코드 발급/조회 1줄 | 패키지 의존, 디버그 시 추상화 한 단계 더 |
| (B) Relay 직접 (RelayService.CreateAllocation) | 저수준 제어, NGO Transport 수동 결선 | UnityTransport.SetRelayServerData 호출·hostConnectionData 직렬화 등 보일러플레이트 |

**채택: (A) Sessions API.** `docs/12_development_plan.md:540-545` "Sessions + Relay + Sessions/초대코드" 명시와 일치.

## 흐름 설계

```
[부트] UnityServices.InitializeAsync()
        ↓
[로그인] AuthenticationService.Instance.SignInAnonymouslyAsync()
        ↓ (PlayerId 발급)
[로비/타이틀 화면] "호스트 만들기" 버튼 클릭
        ↓
[Session 생성] MultiplayerService.CreateSessionAsync(options)
        ├─ options.MaxPlayers = 2 (MVP) / 4 (확장)
        ├─ options.IsPrivate = true (참여코드 전용)
        └─ options.WithRelayNetwork = true (Relay 자동 wrap)
        ↓ (ISession 반환, session.Code = "ABC123")
[NGO 시작] NetworkManager.Singleton.StartHost()  // Sessions가 자동 결선했으면 생략될 수도
        ↓
[UI 갱신] HostCreateView가 session.Code 표시 + 복사 버튼
        ↓
[대기] 클라이언트가 코드 입력 접속 (CL-024)
```

세부 API 시그니처는 작업 시점 패키지 버전에 따라 차이가 있어, plan 단계에선 **흐름과 책임 분리**만 확정하고 정확한 메서드명은 작업자가 IntelliSense로 확인.

## 작업 범위

### Step 1 — UnityServices 초기화 부트

`Assets/_Project/Scripts/Runtime/Networking/UnityServicesBootstrap.cs` 신규:

```
- async Task InitializeAsync()
  - UnityServices.InitializeAsync()
  - AuthenticationService.Instance.SignInAnonymouslyAsync()
  - 성공 시 PlayerId 캐시
- 성공/실패 이벤트 발행 (Action<bool, string>)
- 부트 씬에서 Awake에 InitializeAsync 호출
```

위치: 부트 씬에 GameObject "UnityServicesBootstrap" 1개. DontDestroyOnLoad.

### Step 2 — SessionService 래퍼

`Assets/_Project/Scripts/Runtime/Networking/SessionService.cs` 신규:

```
public class SessionService {
    public ISession CurrentSession { get; private set; }
    public string CurrentJoinCode => CurrentSession?.Code;
    public event Action<ISession> SessionCreated;
    public event Action<string> SessionFailed;

    public async Task<bool> CreateHostSessionAsync(int maxPlayers);
    public async Task LeaveSessionAsync();
}
```

내부에서 Sessions API 호출. 호스트 코드만 본 CL 범위. JoinByCodeAsync는 CL-024.

### Step 3 — HostCreateView UI

`Assets/_Project/Scripts/Runtime/Networking/HostCreateView.cs` 신규 + 프리팹.

UI 요소:
- "방 만들기" 버튼
- 진행 중 스피너/텍스트 ("연결 중...")
- 성공 시 표시:
  - 큰 텍스트로 코드 (예: `ABC123`)
  - "복사" 버튼 (`GUIUtility.systemCopyBuffer`)
  - "취소" 버튼 → SessionService.LeaveSessionAsync
- 실패 시 짧은 에러 메시지 (정식 메시지 UI는 CL-066)

CL-021 ConnectMenu의 "Host" 버튼이 본 View로 진입하도록 변경. ConnectMenu의 Host 버튼은 본 CL 이후 "Local Host" 디버그용으로만 유지하거나 제거.

### Step 4 — NetworkManager 결선

Sessions API가 NGO를 자동 결선하지 않을 경우 (패키지 버전 따라):
- Session 생성 후 `NetworkManager.Singleton.StartHost()` 호출
- 자동 결선되면 생략

**작업자 검증 포인트**: Session 생성 후 `NetworkManager.IsHost`가 true가 되는지. 안 되면 명시 호출.

### Step 5 — 디버그 로그

세션 라이프사이클 핵심 포인트에 로그:
- `[Session] Initialize OK, PlayerId={id}`
- `[Session] Created code={code} maxPlayers={n}`
- `[Session] Host started, NGO listening={bool}`
- `[Session] Leave OK`
- `[Session] Error {message}`

본 로그는 CL-031 "네트워크 핵심 로그 포인트 정리"의 1차 자료가 됨.

### Step 6 — 검증 시나리오

| 시나리오 | 기대 |
|---|---|
| 부트 씬 첫 로드 | UnityServices 초기화 + 익명 로그인 1초 내 |
| "방 만들기" 클릭 | 1~3초 내 코드 표시, NetworkManager.IsHost=true |
| 코드 복사 버튼 | 클립보드에 6자 코드 복사됨 |
| 호스트 상태에서 다른 머신/Multiplayer Play Mode 인스턴스가 같은 코드로 접속(CL-024 후 가능) | 정상 connect |
| 호스트 강제 종료 | 다음 부트 시 재실행 가능 (잔여 세션이 자동 정리되거나 일정 후 만료) |
| 인터넷 끊긴 상태에서 "방 만들기" | 정의된 에러 메시지, 앱 크래시 X |

## 비범위

- **코드 입력 참가** — CL-024
- **세션 종료/호스트 이탈 처리** — CL-025
- **참가 실패 예외 흐름의 정식 UI** — CL-030, CL-066
- **Lobby/세션 목록** — `docs/12:543` 후순위
- **계정 인증 게임 측** — Unity Authentication은 Relay용. 게임 계정(loginId+password)은 백엔드 별도 (CL-107 시프트분)
- **재접속 복구** — `docs/04_multiplayer.md:42-45` MVP 미지원

## 사용자 결정 필요

| 항목 | 옵션 | 권장 |
|---|---|---|
| MaxPlayers | 2 / 4 | **2** (MVP), 옵션으로 4 toggle 노출 |
| Session.IsPrivate | true / false | **true** (참여코드 전용, 검색 차단) |
| 코드 표시 형식 | 그대로 / 하이픈 삽입(ABC-123) | 그대로 (Unity 기본 형식 유지) |
| 익명 로그인 영속화 | 매 부팅 새 익명 / `PlayerAccountService` 영속 | 매 부팅 새 익명 (MVP 단순) |
| ConnectMenu의 "Local Host" 버튼 | 제거 / 디버그 모드에서만 유지 | 디버그 모드 유지 (개발 편의) |
| 자동 호스트 시작 | 버튼 클릭 / 플레이어 동의 후 / 자동 | 버튼 클릭 (사용자 의도 명시) |

## 위험 요소

1. **패키지 버전별 API 변경** — Sessions API는 com.unity.services.multiplayer의 1.x ~ 2.x 사이 시그니처가 자주 바뀜. 작업 전 README/Changelog 1회 확인 필요.
2. **Sessions ↔ NGO 자동 결선 여부** — 자동이면 NetworkManager.StartHost 명시 호출 불필요. 수동이면 호출 누락 시 호스트가 "Listening 안 함" 상태로 정지. 검증 필수.
3. **익명 로그인 토큰 캐싱** — 같은 머신에서 두 번째 부트에 토큰을 재사용. Multiplayer Play Mode 두 인스턴스가 같은 토큰을 쓰면 같은 PlayerId로 인식돼 Session 충돌. `AuthenticationService.ClearSessionToken()`을 두 번째 인스턴스에서 호출하거나, Multiplayer Play Mode의 "Different Auth Profile" 옵션 사용.
4. **Services 활성화 누락** — 대시보드에서 활성화 안 된 상태로 호출 시 401/403. 부트 시점 명확한 에러 로그.
5. **방화벽/네트워크 환경** — 최초 빌드 배포 시 일부 사내 네트워크에서 Relay UDP 포트 차단 가능. 검증 환경 외부망 1회 테스트 필수.
6. **Session 누수** — 호스트가 정상 종료 안 하면 서버 측 Session이 잠시 남음. Sessions 정책에 따라 일정 시간 후 자동 회수. 수동 정리 코드는 CL-025.

## 변경/신규 파일

### 신규

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/UnityServicesBootstrap.cs` | Initialize + 익명 로그인 |
| `Assets/_Project/Scripts/Runtime/Networking/SessionService.cs` | Session 생성/Leave 래퍼 |
| `Assets/_Project/Scripts/Runtime/Networking/HostCreateView.cs` | UI 컨트롤러 |
| `Assets/_Project/Prefabs/UI/HostCreatePanel.prefab` | UI 프리팹 |

### 변경

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/ConnectMenu.cs` | Host 버튼 → HostCreateView 호출로 교체 (Local Host 디버그 옵션 분리) |
| `Assets/Scenes/Test/CL021_NetworkTest_2P.unity` | UnityServicesBootstrap GameObject 추가, HostCreatePanel 캔버스 추가 |

### 미변경

- TestKhi 프리팹·게임 콘텐츠 — 본 CL은 세션 흐름만
- NetworkPlayerInitializer (CL-022) — 동일 분기 그대로 사용

## 검증 (E2E)

1. 빌드 후 인터넷 연결된 머신 2대(또는 Multiplayer Play Mode 2 인스턴스 + 다른 Auth Profile)에서 한 쪽 호스트 시작 → 코드 표시 확인
2. 코드 복사 → 작업자가 다른 인스턴스에서 메뉴 입력 (CL-024 미구현 상태에선 콘솔에서 직접 `JoinSessionByCodeAsync` 호출해 검증 가능)
3. 호스트가 Session 만든 직후 NetworkManager.IsHost=true 확인 (Inspector 또는 콘솔)
4. NGO 기본 동기화(CL-021의 NetworkSyncProbe)가 Relay 경유로도 동기화되는지 확인
5. 호스트 종료 → 다음 시도에서도 정상 호스트 가능

## 관련 문서

- `docs/04_multiplayer.md:18` — "호스트 생성 -> 코드 표시 -> 코드 입력 참가"
- `docs/12_development_plan.md:540-545` — Sessions+Relay 스택, Lobby 후순위
- `docs/13_client_detailed_plan.md:69` — 세션 흐름 기준
- `docs/14_client_jira_story_backlog.md` — CL-023 정의
- `docs/15_word_dict.md` — 세션 정의 (매칭룸 단위)

## 다음 CL

- **CL-024**: 코드 입력 기반 참가 기능 — 본 CL의 호스트 측에 대응하는 클라이언트 측 흐름. 코드 입력 UI + JoinSessionByCodeAsync.

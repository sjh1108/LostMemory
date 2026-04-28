# CL-024: 코드 입력 기반 참가 기능 구현

소속 Epic: **Epic C. 멀티플레이 / 세션 네트워크**

선행: CL-020 (패키지·Services 활성화), CL-021 (테스트 씬), CL-022 (플레이어 네트워크 프리팹), CL-023 (호스트 생성/Relay 코드)

## Context

CL-023에서 호스트가 Session 생성 + 6자 참여코드 발급까지 마쳤다. 본 CL은 그 코드의 **클라이언트 대응** — 코드를 입력하면 Session에 합류하고 NGO 클라이언트 모드로 진입하는 흐름을 구현한다.

목표는 다음을 검증:
- 참여자가 코드 입력 → `MultiplayerService.JoinSessionByCodeAsync` → Relay 자동 결선 → NGO Client 시작이 1~3초 안에 안정
- 합류 후 호스트 측 NetworkSyncProbe(CL-021) Tick 값이 클라이언트에 동기화
- 잘못된 코드/만석/네트워크 끊김/인증 오류가 사용자에게 분류된 메시지로 표시되고 앱 크래시 없음
- CL-022에서 등록된 PlayerPrefab이 합류 직후 호스트·클라 양쪽 화면에 자동 스폰

본 CL은 **호스트 1 + 클라 1** 2인 합류만. 3인 이상 합류·재접속·세션 목록 탐색은 비범위.

## 현재 상태 (가정 + 의존)

| 항목 | 상태 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/` 폴더 | CL-020 생성 가정 |
| `Packages/manifest.json` 멀티 패키지 | CL-020 설치 완료 가정 |
| `CL021_NetworkTest_2P.unity` 씬 | CL-021 작성 |
| `ConnectMenu.cs` 임시 UI | CL-021 작성 (Host/Client/Server/Disconnect 4버튼) |
| `TestKhi_MinimalCharacter2D.prefab` NetworkObject | CL-022 부착 + PlayerPrefab 등록 |
| `UnityServicesBootstrap.cs` | CL-023 작성 (익명 로그인까지) |
| `SessionService.cs` | CL-023 작성 (`CreateHostSessionAsync`까지) |
| `HostCreateView.cs` + `HostCreatePanel.prefab` | CL-023 작성 |

본 CL은 위 자산을 그대로 사용하며 Join 경로만 추가한다.

## 채택 API: Sessions JoinByCode

CL-023이 Sessions API(com.unity.services.multiplayer)를 채택했으므로 클라 측도 동일 패키지의 `JoinSessionByCodeAsync` 사용. Relay `JoinAllocation` 직접 호출은 비채택 — 추상화를 두 번 거치게 되고 Sessions가 NGO Transport를 자동 결선하지 않을 위험을 굳이 감수할 이유 없음.

> 정확한 메서드 시그니처는 작업 시점 패키지 버전에 따라 차이가 있으므로 작업자가 IntelliSense로 확인. plan은 흐름과 책임 분리만 확정.

## 흐름 설계

```
[로비/타이틀] "코드 입력으로 참가" 버튼
    ↓
[입력 화면] 6자 코드 입력 + "참가" 버튼
    ↓ (입력 검증: 길이/허용 문자)
[연결 중] 스피너 + "취소" 버튼
    ↓
[Session 합류] SessionService.JoinByCodeAsync(code)
    ├─ 성공: ISession 반환 → NetworkManager.IsClient=true 검증
    │       ↓
    │   [게임 진입] PlayerPrefab 자동 스폰(CL-022) → 호스트 화면에도 표시
    └─ 실패: 코드 정의된 에러 분류
        ├─ InvalidCode: "코드가 잘못되었거나 만료되었습니다"
        ├─ SessionFull: "방이 가득 찼습니다"
        ├─ NetworkError: "네트워크 연결을 확인하세요"
        ├─ AuthError:    "인증에 실패했습니다 (재시도)"
        └─ Unknown:      "알 수 없는 오류 (코드: {raw})"
            ↓
        [입력 화면 복귀] 인라인 메시지 (정식 토스트는 CL-066)
```

## 작업 범위

### Step 1 — SessionService 확장

`Assets/_Project/Scripts/Runtime/Networking/SessionService.cs` (CL-023 신규, 본 CL 확장):

```
public enum JoinFailureReason {
    InvalidCode,
    SessionFull,
    NetworkError,
    AuthError,
    Unknown,
}

public partial class SessionService {
    public event Action<ISession> SessionJoined;
    public event Action<JoinFailureReason, string> JoinFailed;

    public async Task<bool> JoinByCodeAsync(string code);
}
```

**예외 → enum 매핑 (작업자 참고)**:

| Sessions API 예외/리턴 코드 | enum |
|---|---|
| SessionException(SessionsServiceError.SessionNotFound) | `InvalidCode` |
| SessionException(SessionsServiceError.SessionFull) | `SessionFull` |
| RelayServiceException / 네트워크 타임아웃 | `NetworkError` |
| AuthenticationException / 401 / 403 | `AuthError` |
| 그 외 | `Unknown` (raw 메시지 동봉) |

> 정확한 enum 멤버명은 패키지 버전별 차이 가능. 작업 시점 IntelliSense로 매칭.

### Step 2 — JoinByCodeView UI

신규: `Assets/_Project/Scripts/Runtime/Networking/JoinByCodeView.cs` + `Assets/_Project/Prefabs/UI/JoinByCodePanel.prefab`

UI 요소:
- `TMP_InputField` (6자 제한, uppercase 자동 변환, 영숫자만 허용)
- "참가" 버튼 (입력 6자 충족 시만 활성화)
- "취소" 버튼 (요청 진행 중이면 토큰 취소)
- 진행 중 스피너/텍스트 ("연결 중...")
- 실패 시 짧은 인라인 메시지 (정식 토스트는 CL-066)

입력 정규화 (`onValueChanged` 콜백):
- 모든 lowercase → upper
- 공백/하이픈/점 제거
- 영숫자 외 문자 입력 차단
- 6자 도달 시 "참가" 버튼 활성

`JoinByCodeView`는 `SessionService.SessionJoined` / `JoinFailed` 이벤트 구독, 상태 머신:
```
Idle → Connecting → (Joined → 화면 닫기) | (Failed → Idle 복귀, 메시지 표시)
```

### Step 3 — ConnectMenu 결선 변경

수정: `Assets/_Project/Scripts/Runtime/Networking/ConnectMenu.cs` (CL-021 임시)

- 기존 `Client` 버튼 onClick → `JoinByCodeView` 활성화 + 입력 받기로 교체
- IP 직결 입력 필드는 디버그 모드 토글에서만 노출 (개발 편의)
- `Host`/`Server`/`Disconnect` 버튼은 유지 (디버그 검증용)

### Step 4 — NetworkManager 결선 검증

Sessions가 NGO Transport를 자동 결선하지 않을 경우 (패키지 버전 따라):
- `JoinByCodeAsync` 성공 후 `NetworkManager.Singleton.StartClient()` 명시 호출
- 자동 결선이면 생략

**작업자 검증 포인트**: Join 직후 `NetworkManager.Singleton.IsClient`가 true인지. 안 되면 명시 호출 필요. 자동/수동 분기를 SessionService 내부에서 한 곳으로 처리.

### Step 5 — 디버그 로그 (CL-031 자료)

```
[Session] JoinByCode start code={code}
[Session] JoinByCode OK sessionId={id} hostPlayerId={pid}
[Session] JoinByCode FAILED reason={enum} message={raw}
[Session] Client started, connected={bool}
```

UnityServicesBootstrap, SessionService(Create+Join), JoinByCodeView 4곳이 본 CL 이후 CL-031 로그 정리의 1차 자료가 된다.

### Step 6 — E2E 검증 시나리오

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 정상 코드 입력 | 1~3초 내 합류, NetworkSyncProbe Tick 호스트와 동기화 |
| 2 | 5자/7자 입력 | "참가" 버튼 비활성, RPC 미발사 |
| 3 | 임의 6자 (없는 코드) | `InvalidCode` 메시지, 입력 화면 복귀 |
| 4 | 호스트 종료 후 입력 | `InvalidCode` 또는 NotFound 메시지 |
| 5 | 네트워크 끊긴 상태 입력 | `NetworkError` 메시지, 앱 크래시 X |
| 6 | MaxPlayers=2 만석에서 3번째 참가 | `SessionFull` 메시지 |
| 7 | 합류 직후 PlayerPrefab 자동 스폰 | 호스트 화면에 클라 캐릭터 출현 (위치 동기화는 CL-026) |
| 8 | 같은 머신 2 인스턴스 (Different Auth Profile) | 정상 합류 |
| 9 | 합류 진행 중 "취소" 버튼 | 요청 취소, 입력 화면 즉시 복귀 |
| 10 | 합류 5회 반복 | 안정성 (메모리 누수, 좀비 세션 X) |

## 비범위

- **호스트 이탈 시 클라 graceful 처리** — CL-025
- **이동·방향·공격·다운 동기화** — CL-026~029
- **정식 토스트/모달 에러 UI** — CL-066
- **Lobby/세션 목록 탐색** — `docs/12_development_plan.md:543` 후순위
- **재접속** — `docs/04_multiplayer.md:42-45` MVP 미지원
- **참여 코드 입력 UI 폴리시(애니메이션·키패드 등)** — CL-060 (클라3)

## 사용자 결정 필요

| 항목 | 옵션 | 권장 |
|---|---|---|
| 코드 입력 형식 | 6자 한 칸 / 3-3 분할 / 자유 | **6자 한 칸** (Sessions 기본 형식) |
| 입력 자동 대문자화 | ON / OFF | **ON** (사용자 실수 방지) |
| 잘못된 코드 즉시 검증 | API 호출만 / 클라 1차 검증(영숫자) + API | **클라 1차 + API** |
| Auth Profile 분리 | 자동 / 수동 토글 | **Multiplayer Play Mode "Different Auth Profile" 자동** |
| 합류 성공 시 화면 전환 | 즉시 게임 씬 / 대기 화면 후 호스트 시작 신호 | **즉시 게임 씬** (MVP 단순) |
| 취소 버튼 동작 | 요청 abort + Idle / 요청 무시하고 결과 도착 시 자동 닫기 | **요청 abort** (CancellationToken) |

## 위험 요소

1. **Sessions API 시그니처 변동** — `com.unity.services.multiplayer` 1.x↔2.x 사이 메서드명/예외 타입이 바뀜. 작업 전 패키지 README/Changelog 1회 확인.
2. **Sessions ↔ NGO 자동 결선 여부** — Join 성공해도 `IsClient=false`면 명시 `StartClient()` 누락. 시나리오 #1 검증 필수.
3. **Auth Profile 충돌** — Multiplayer Play Mode 두 인스턴스가 같은 익명 토큰을 공유하면 Sessions가 같은 PlayerId로 인식해 합류 실패. "Different Auth Profile" 옵션 사용 또는 두 번째 인스턴스에서 `AuthenticationService.ClearSessionToken()`.
4. **Relay UDP 차단** — 사내망/방화벽 환경에서 `NetworkError` 빈발 가능. 외부망 1회 확인 + 메시지에 "방화벽/네트워크 확인" 안내.
5. **합류 후 PlayerPrefab 미스폰** — CL-022에서 `NetworkManager.NetworkPrefabs`에 등록 누락 시 합류만 되고 캐릭터 안 보임. 시나리오 #7이 가드.
6. **InputField IME 한글 입력** — 한글 IME 활성 상태에서 영숫자 강제 입력이 차단될 수 있음. `inputType=Standard` 강제 + `contentType=Alphanumeric` 설정. 빌드에서 한글 IME 토글 안 되면 OS별 추가 처리 필요 (MVP는 Windows 우선).
7. **6자 가정 깨짐** — Sessions가 발급하는 코드 길이가 패키지 버전에 따라 5/7자가 될 수도 있음. CL-023 결정 시점에 실측해 본 plan의 "6자 제한"을 확정.
8. **취소 토큰 누락** — 진행 중인 `JoinByCodeAsync`가 `CancellationToken`을 받지 않으면 사용자가 취소해도 백그라운드에서 합류가 완료돼 좀비 세션이 됨. SessionService API에 `CancellationToken` 파라미터 추가.

## 변경/신규 파일

### 신규

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/JoinByCodeView.cs` | UI 컨트롤러 |
| `Assets/_Project/Prefabs/UI/JoinByCodePanel.prefab` | UI 프리팹 |

### 변경

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/SessionService.cs` | `JoinByCodeAsync(string, CancellationToken)` + `JoinFailureReason` enum + `SessionJoined`/`JoinFailed` 이벤트 |
| `Assets/_Project/Scripts/Runtime/Networking/ConnectMenu.cs` | Client 버튼 → JoinByCodeView 호출 |
| `Assets/Scenes/Test/CL021_NetworkTest_2P.unity` | JoinByCodePanel 캔버스 추가 |

### 미변경

- `UnityServicesBootstrap.cs` (CL-023 그대로 — 부트 + 익명 로그인)
- `HostCreateView.cs` / `HostCreatePanel.prefab` (CL-023 그대로)
- `TestKhi_MinimalCharacter2D.prefab` (CL-022 그대로)
- 게임 콘텐츠 코드/씬

## 검증 (E2E)

1. 빌드 후 같은 머신 Multiplayer Play Mode 2 인스턴스 실행 (Different Auth Profile)
2. 인스턴스 1: HostCreateView로 호스트 시작 → 코드 표시
3. 인스턴스 2: JoinByCodeView 코드 입력 → 합류
4. NetworkSyncProbe Tick 양쪽 동기화 확인 (CL-021 텍스트 동일 값)
5. PlayerPrefab 두 캐릭터 모두 화면에 보임 (위치 동기화는 CL-026 영역)
6. 잘못된 코드 5회 시도 → 모두 `InvalidCode` 메시지, 앱 안정
7. 인스턴스 1 호스트 강제 종료 → 인스턴스 2 disconnect 콜백 (CL-025 미구현이면 콘솔 로그만 확인)
8. 합류 진행 중 취소 버튼 → 백그라운드 합류 abort, 좀비 세션 0건 확인 (Sessions 대시보드 또는 다음 시도 시 코드 재발급으로 검증)

## 관련 문서

- `docs/04_multiplayer.md:18` — "호스트 생성 → 코드 표시 → 코드 입력 참가"
- `docs/12_development_plan.md:38, 543` — Sessions+Relay+코드 흐름 확정, Lobby 후순위
- `docs/13_client_detailed_plan.md` — 세션 흐름 기준
- `docs/14_client_jira_story_backlog.md:53` — CL-024 정의
- `docs/15_word_dict.md` — 세션 정의 (매칭룸 단위)
- `docs/khi/cl021_network_test_scene_plan.md` — 테스트 씬 + 임시 ConnectMenu (본 CL이 정식 UI로 대체)
- `docs/khi/cl022_player_network_prefab_plan.md` — PlayerPrefab 등록 (본 CL 시나리오 #7 가드)
- `docs/khi/cl023_host_create_relay_code_plan.md` — 호스트 측 대응 (SessionService 본체)

## 다음 CL

- **CL-025**: 세션 종료·호스트 이탈 처리 — 본 CL의 합류 흐름이 끊겼을 때 클라이언트가 graceful 하게 메인 메뉴로 복귀하는 처리. CL-024의 검증 시나리오 #7이 CL-025 진입점.

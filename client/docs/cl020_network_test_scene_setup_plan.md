# CL-020 / CL-021 — 네트워크 테스트 씬 구성 절차

## 문서 목적

`docs/13_client_detailed_plan.md` Wave 1 항목 **CL-020 / CL-021** (네트워크 테스트 씬, NGO·Relay 패키지 셋업) 에 대응하는 *Unity Editor 작업 절차서*.

C# 코드(Phase A) 는 `Assets/_Project/Scripts/Runtime/Networking/` 아래에 이미 생성되어 있다. 본 문서는 그 코드를 활용하기 위한 *씬·컴포넌트 구성 단계* 만 다룬다.

> 본 문서의 작업은 `docs/commonness/agent-unity-safety-rules.md` 에 따라 *사람 작업자* 가 Unity Editor 에서 직접 수행한다. CLI/AI 가 `.unity`/`.prefab`/`.meta` 를 직접 생성/수정하지 않는다.

## 사전 조건

- Unity 6.3 (`6000.3.13f1`) 으로 프로젝트 열려 있음
- `LostMemory/Packages/manifest.json` 에 다음이 설치되어 있음 (이미 됨)
  - `com.unity.netcode.gameobjects: 2.11.1`
  - `com.unity.services.multiplayer: 2.2.1`
  - `com.unity.services.core: 1.16.0`
- **Unity Cloud 프로젝트 연결**:
  - `Edit → Project Settings → Services` 에서 Unity 계정 로그인
  - 신규 또는 기존 Unity Cloud 프로젝트 선택 (Project ID 가 `ProjectSettings/ProjectSettings.asset` 에 박힘)
  - 연결되어 있지 않으면 Phase A 4번(호스트 생성) 시 `ServicesInitFailed` 에러 발생함

## 1. 씬 생성

1. `Assets/_Project/Scenes/Test/` 폴더가 없으면 Project 창에서 생성
2. 새 씬 만들기 → 이름 `Test_Network_AD.unity` (작업자 이니셜 명시 — `agent-unity-safety-rules.md` 권장)
3. `Build Settings` (`File → Build Profiles → Scene List`) 에 추가. 부트 0번 / 로비 1번 다음 임의 인덱스에 배치

## 2. NetworkManager 오브젝트 구성

1. Hierarchy 우클릭 → `Create Empty` → 이름 `NetworkManager`
2. `Add Component` → **Network Manager** (Unity.Netcode)
3. 같은 오브젝트에 `Add Component` → **Unity Transport**
4. NetworkManager 의 `Network Transport` 슬롯에 위 Unity Transport 자동 할당 확인
5. **Unity Transport 인스펙터 — 중요**:
   - `Protocol Type` 을 **`Relay Unity Transport`** 로 변경 (혹은 빌드 시점에 SessionsAPI 가 자동 설정 — `WithRelayNetwork()` 가 구성 주입함)
   - 그 외 `Connection Data` 의 IP/Port 는 Sessions API 가 런타임에 덮어씀

## 3. SessionLifecycle 오브젝트

1. Hierarchy 우클릭 → `Create Empty` → 이름 `SessionLifecycle`
2. `Add Component` → **Lost Memory / Networking / Session Lifecycle** (검색 또는 메뉴)
3. 별도 인스펙터 설정 없음 (런타임에 NetworkManager.Singleton 자동 구독)

## 4. UI Canvas 구성

1. Hierarchy 우클릭 → `UI → Canvas` (Render Mode = `Screen Space - Overlay`)
2. EventSystem 자동 생성 확인
3. Canvas 자식으로 다음 UI 요소 배치 (TextMeshPro 권장 — 기존 UI 와 일관성):

```
Canvas
  StatusText                (TextMeshPro - Text)         — 상태 메시지
  JoinCodeDisplay           (TextMeshPro - Text)         — 호스트 시 코드 표시
  JoinCodeInput             (TextMeshPro - Input Field)  — 클라이언트 코드 입력
  HostButton                (UI - Button)                — Label "호스트"
  JoinButton                (UI - Button)                — Label "참가"
  LeaveButton               (UI - Button)                — Label "나가기"
```

## 5. RelayJoinCodeUI 부착

1. Canvas 에 `Add Component` → **Lost Memory / Networking / Relay Join Code UI**
2. 인스펙터에서 슬롯 연결:
   - `Status Text` ← Canvas/StatusText
   - `Join Code Display` ← Canvas/JoinCodeDisplay
   - `Join Code Input` ← Canvas/JoinCodeInput
   - `Host Button` ← Canvas/HostButton
   - `Join Button` ← Canvas/JoinButton
   - `Leave Button` ← Canvas/LeaveButton
   - `Max Players` = `2`

## 6. 검증

### 6.1 단일 인스턴스 동작 확인

- Play 모드 진입 → 상태 텍스트 "대기 중. 호스트 생성 또는 코드 입력 참가."
- 호스트 버튼 클릭 → 1~3초 후 `JoinCodeDisplay` 에 6자리 코드 표시, 상태 "호스트 활성. 코드: XXXXXX"
- 나가기 → 상태 "세션 종료"

### 6.2 2인 멀티 인스턴스 검증

1. `File → Build And Run` 으로 빌드 1회 (또는 ParrelSync/MPPM 설치 시 가상 인스턴스)
2. 인스턴스 A: 호스트 클릭 → 코드 메모
3. 인스턴스 B: 코드 입력 → 참가 클릭 → "참가 성공" 메시지
4. 인스턴스 A 강제 종료 → 인스턴스 B 가 "방장이 나가서 세션이 종료되었습니다." 메시지 표시
5. 콘솔의 `[Net]` 로그로 connect/disconnect/failure 추적

### 6.3 실패 케이스

- 잘못된 코드 입력 → "참여 코드를 찾을 수 없습니다." 메시지
- Cloud 프로젝트 미연결 상태로 호스트 시도 → "Unity 서비스 초기화에 실패했습니다." 메시지

## 7. 관련 파일

### Phase A C# 파일 (이미 생성됨)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Networking/
  Common/
    NetLog.cs                  - 네트워크 로그 단일 진입점
    HostAuthority.cs           - 호스트 권위 게이트 (IsHost)
  Session/
    RelaySession.cs            - 세션 공통 상태/초기화
    RelaySessionHost.cs        - 호스트 생성 + 코드 발급
    RelaySessionClient.cs      - 코드 입력 참가
    SessionLifecycle.cs        - 호스트 이탈 감지 + 정리
    SessionErrorPolicy.cs      - 에러 → 사용자 메시지
    RelayJoinCodeUI.cs         - 본 씬용 임시 UI 핸들러
```

### 후속 단계 (Phase B — UI 1차 안정화 후)

- 플레이어 NetworkObject 프리팹 — `_Project/Prefabs/Characters/TestKhi_Net_AD.prefab` (TestKhi_MinimalCharacter2D 복제)
- 위치/상태 동기화 컴포넌트 — `_Project/Scripts/Runtime/Networking/Player/`
- 공격 판정 라우터 — `_Project/Scripts/Runtime/Networking/Combat/`
- `RunManager.IsAuthority` 등 호스트 권위 분기 한 줄 교체

## 8. Open Question / 확인 필요

- Unity Cloud 프로젝트 ID 가 팀 공유 프로젝트로 연결되어 있는지 확인 필요 (개인 프로젝트로 연결되면 빌드 간 코드 공유 안 됨)
- Sessions API 호출 시 첫 회 익명 로그인 — 추후 `CL-105 ~ CL-108` 로그인 흐름 도입 시 익명 → 정식 로그인으로 교체 필요
- ParrelSync / MPPM (Multiplayer Play Mode) 도입은 빌드 1회 빌드 횟수를 줄이는 QoL — 본 작업 범위 외

## 9. 관련 문서

- `docs/04_multiplayer.md` — 멀티 규칙
- `docs/12_development_plan.md` — 개발 계획 (NGO + Relay 결정)
- `docs/13_client_detailed_plan.md` — 클라 상세 계획 (Wave 1·2 매핑)
- `client/docs/commonness/agent-unity-safety-rules.md` — 씬/프리팹 작업 안전 규칙
- `client/docs/commonness/scene-ownership-and-prefab-edit-rules.md` — 테스트 씬 명명 규칙

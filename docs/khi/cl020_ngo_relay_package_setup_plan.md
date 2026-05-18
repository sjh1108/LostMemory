# CL-020: NGO·Relay 패키지 세팅 및 버전 고정

소속 Epic: **Epic C. 멀티플레이 / 세션 네트워크**

## Context

CL-021 이후 모든 멀티 작업(테스트 씬·플레이어 프리팹·호스트 생성·참여코드·동기화)은 NGO/Transport/Multiplayer Services SDK 위에서 돌아간다. 따라서 첫 단추인 패키지 설치와 **정확한 버전 고정**이 후속 12개 CL의 공통 기반.

목표: 멀티 코드를 작성하지는 않되, 작성할 수 있는 환경을 완비하고 후일 회귀 방지를 위한 버전 고정 정책을 문서화한다.

## 현재 상태 (탐색 결과)

| 항목 | 상태 |
|---|---|
| cloudProjectId | `a15aff52-5b9b-4b49-9552-c43dc3a62c69` (organizationId: `gimhoein`) — 이미 연결 |
| 멀티 패키지 9종 | **모두 미설치** |
| `Unity.Netcode` / `Unity.Services.*` using | 코드 0건 |
| `Assets/_Project/Scripts/Runtime/Networking/` | 폴더 없음 |
| TDE 멀티 흔적 | 로컬 split-screen만 (Mirror/NGO 무관, 충돌 없음) |
| 멀티 전용 테스트 씬 | 없음 |
| `ProjectSettings/MultiplayerManager.asset` | 존재 (m_EnableMultiplayerRoles=0) — Multiplayer Center 자동 생성 추정 |
| `ProjectSettings/NetworkManager.asset` | 존재 (sendrate=15) — 어느 패키지가 만든 것인지 확인 필요 |

진행도 약 5% (클라우드 프로젝트만 연결).

## 설치 대상 패키지

`docs/12_development_plan.md:163, 543` 기준 — Lobby는 제외, Sessions(Multiplayer Services SDK)로 대체.

### 필수

| 패키지 | 역할 |
|---|---|
| `com.unity.netcode.gameobjects` | NGO 코어 — NetworkObject/NetworkBehaviour/RPC |
| `com.unity.transport` | UTP — NGO 전송 계층 (NGO 의존성) |
| `com.unity.services.core` | Unity Services 초기화 코어 |
| `com.unity.services.authentication` | Sessions·Relay 사용 시 필수, MVP 익명 또는 게스트 인증 토대 |
| `com.unity.services.multiplayer` | Multiplayer Services SDK — Sessions + Relay 통합 |
| `com.unity.multiplayer.playmode` | 에디터 2인 동시 테스트 도구 |

### 선택 (권장: 포함)

| 패키지 | 역할 | 권장 |
|---|---|---|
| `com.unity.multiplayer.tools` | 네트워크 디버그/시각화 (RuntimeNetStatsMonitor 등) | 포함 — CL-031 로그 포인트 정리 시 활용 |

### 제외 (이번 단계)

| 패키지 | 사유 |
|---|---|
| `com.unity.services.lobbies` | `docs/12_development_plan.md:543` 명시 후순위 (MVP는 코드 입력 참가만) |
| `com.unity.dedicated-server` | dedicated server 미사용 (호스트 기반) |

## 작업 단계

### Step 1 — 패키지 매니페스트 갱신

`LostMemory/Packages/manifest.json`에 위 7종 추가. 정확한 버전은 작업자가 Unity 6.3 (6000.3.13f1) Package Manager UI에서 "Verified for Unity 6" 또는 "Recommended" 라벨 안정판으로 확인 후 결정. 캐럿(`^`) 없이 `"X.Y.Z"` 정확한 핀.

### Step 2 — Unity Services 대시보드 활성화

Unity Cloud 대시보드(`cloud.unity.com`)에서 organization `gimhoein` 프로젝트의 다음 서비스 활성화:
- Authentication
- Relay
- Multiplayer Services (Sessions)

`ProjectSettings/Services/`에 자동 생성되는 설정 파일 git에 커밋. 단 API key/시크릿류는 절대 커밋 안 함.

### Step 3 — 부트 씬에 NetworkManager 임시 배치

`Assets/Scenes/SampleScene.unity` 또는 별도 부트 씬에 빈 GameObject "NetworkManager" 생성, `NetworkManager` + `UnityTransport` 컴포넌트 추가. 실제 활용은 CL-022. 이번엔 컴파일/Play 모드 진입 검증용.

### Step 4 — 폴더 구조 마련

빈 폴더만 생성:
- `Assets/_Project/Scripts/Runtime/Networking/` — 후속 CL이 채울 자리

### Step 5 — 버전 고정 정책 문서화

본 plan 파일 자체에 채택 버전을 표로 기록. 향후 버전 변경 시:
- 팀 합의 필수
- `Packages/packages-lock.json` 변경분 PR 별도 분리
- 변경 사유 plan에 한 줄 추가

### Step 6 — 검증

| 항목 | 통과 조건 |
|---|---|
| 콘솔 컴파일 에러 | 0개 |
| `Window > Multiplayer > Multiplayer Play Mode` 메뉴 | 노출 |
| `Edit > Project Settings > Services` 트리 | Authentication / Relay / Multiplayer Services 활성 표시 |
| 빈 씬 + NetworkManager + UnityTransport Play | 에러 0개 |
| `Packages/manifest.json` 명시 버전 | `Packages/packages-lock.json`과 일치 |

## 위험 요소

1. **`com.unity.services.multiplayer`의 의존성 그래프** — 이 패키지가 Relay·Sessions를 자동 포함하는지, 별도 추가가 필요한지는 Package Manager의 "Dependencies" 트리로 확인. 별도 추가가 필요하면 `com.unity.services.relay`도 명시.
2. **기존 `ProjectSettings/NetworkManager.asset`** — 이 asset이 NGO 설치 후 NetworkManager가 사용하는 그릇인지, 아니면 이전 흔적인지 확인. 충돌 시 삭제 후 재생성.
3. **TDE 호환** — TDE 자체는 NGO를 강제하지 않음. 단 후속 CL-022에서 우리 PlayerPrefab이 TDE Character + NetworkObject 합쳐질 때 컴포넌트 순서 검증 필요 (CL-022 plan에서 상세).
4. **인증 방식 충돌** — `docs/15_word_dict.md` 결정 사항: MVP는 `loginId+password+nickname` 기반 백엔드 인증. Unity Authentication은 Sessions/Relay에 필요한 토큰 발급 용도로만 사용 (게임 계정과는 별개). 두 인증을 병행하는 구조 명시 필요.

## 비범위

- 실제 호스트 생성·참여코드 발급 (CL-023)
- 코드 입력 참가 (CL-024)
- NetworkObject 등록·플레이어 프리팹 (CL-022)
- NetworkBehaviour 코드 작성 — 본 CL은 패키지/설정만
- Lobby 도입

## 사용자 결정 필요

| 항목 | 옵션 | 권장 |
|---|---|---|
| 패키지 정확 버전 | Unity 6.3 PackageManager UI 확인 후 결정 | 작업자에게 위임 |
| `com.unity.multiplayer.tools` 포함 | 포함 / 제외 | 포함 |
| `com.unity.services.lobbies` 포함 | 포함 / 제외 | 제외 (`docs/12:543` 기준) |
| Unity Authentication 모드 | 익명 / 게스트 / Custom Provider | 익명 (MVP) — 게임 계정과 별도 |
| `ProjectSettings/NetworkManager.asset` 처리 | 유지 / 삭제 후 재생성 | 패키지 설치 후 동작 확인하고 결정 |

## 변경 파일

| 파일 | 변경 내용 |
|---|---|
| `LostMemory/Packages/manifest.json` | 패키지 7종 추가 + 버전 핀 |
| `LostMemory/Packages/packages-lock.json` | 자동 갱신 (커밋) |
| `LostMemory/ProjectSettings/UnityServicesProjectSettings.json` | 자동 생성 (커밋) |
| `LostMemory/ProjectSettings/ServicesConfiguration.json` | 서비스 활성화 시 자동 생성 (커밋) |
| `LostMemory/ProjectSettings/NetworkManager.asset` | 패키지 설치 후 정상화 시 재기록 |
| `Assets/Scenes/<부트씬>` | NetworkManager 게임오브젝트 추가 (임시) |
| `Assets/_Project/Scripts/Runtime/Networking/.gitkeep` | 빈 폴더 마커 |

## 관련 문서

- `docs/12_development_plan.md:163` — NGO 스택 명시
- `docs/12_development_plan.md:543` — Lobby 후순위 결정
- `docs/13_client_detailed_plan.md:69` — 세션 흐름 기준
- `docs/14_client_jira_story_backlog.md` — CL-020 정의
- `docs/15_word_dict.md` — 인증/세션/런 용어 정리

## 다음 CL

- **CL-021**: 네트워크 테스트 씬 구성 — 본 CL의 NetworkManager·Transport를 실제 씬에 배치하고 호스트/클라이언트 모드 전환 테스트.

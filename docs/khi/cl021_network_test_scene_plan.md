# CL-021: 네트워크 테스트 씬 구성

소속 Epic: **Epic C. 멀티플레이 / 세션 네트워크**

선행: CL-020 (NGO·Relay 패키지 세팅)

## Context

CL-020에서 설치한 패키지 위에서, **NGO 기초 통신이 실제로 동작하는지** 확인할 최소 테스트 씬을 만든다. 본 씬은 후속 CL-022(플레이어 네트워크 프리팹), CL-023(호스트 생성/Relay 코드), CL-024(코드 입력 참가)의 기준 플레이그라운드가 된다.

목표는 게임 콘텐츠를 붙이지 않은 상태에서 다음을 검증:
- NetworkManager + UnityTransport가 부팅 가능
- Host/Client 모드 전환 가능
- 서버에서 변경한 NetworkVariable이 클라이언트에 동기화
- Multiplayer Play Mode 2 인스턴스로 같은 머신 내 호스트-클라이언트 접속

이 단계에서는 **Relay 미사용**. 로컬 Unity Transport(IP 127.0.0.1) 기준만 검증. Relay는 CL-023에서 도입.

## 현재 상태 (탐색 결과)

| 항목 | 상태 |
|---|---|
| 기존 테스트 씬 위치 | `Assets/Scenes/Test/` (BossDoorTest, CL037_MeleeEnemy_Manual 등 12개) |
| 씬 명명 규칙 | CL ID prefix 사용 사례 있음 (`CL037_MeleeEnemy_Manual.unity`) |
| `Assets/_Project/Scripts/Runtime/Networking/` | 없음 (CL-020에서 생성될 예정) |
| 부트 씬 | `Assets/Scenes/SampleScene.unity` 또는 `test_khi.unity` 추정 |

## 작업 범위

### 1. 테스트 씬 생성

경로: `Assets/Scenes/Test/CL021_NetworkTest_2P.unity`

구성:
- 카메라 + 평면 바닥 (회색 박스 머티리얼)
- 좌표 기준이 보이도록 격자/축 (선택)

### 2. NetworkManager 게임오브젝트

씬 루트에 `[NetworkManager]` GameObject 배치, 다음 컴포넌트:
- `NetworkManager` — `Run In Background` 체크
- `UnityTransport` — Connection Data: `127.0.0.1`, port `7777`, Listen Address `0.0.0.0`
- 추후 PlayerPrefab 슬롯은 비워둠 (CL-022에서 채움)

### 3. 임시 ConnectMenu (uGUI)

`Assets/_Project/Scripts/Runtime/Networking/ConnectMenu.cs` 신규:
- 화면 좌상단 캔버스에 Host / Client / Server / Disconnect 버튼 4개
- 각 버튼이 `NetworkManager.Singleton.StartHost()`/`StartClient()`/`StartServer()`/`Shutdown()` 호출
- 현재 상태 (Disconnected / Host / Client) 텍스트 표시

본 메뉴는 임시 — CL-024(코드 입력 참가)에서 정식 UI로 대체.

### 4. NetworkVariable 동기화 검증용 더미 객체

`Assets/_Project/Scripts/Runtime/Networking/NetworkSyncProbe.cs` 신규 (NetworkBehaviour):
- `NetworkVariable<int> Tick` (서버 권한)
- 서버에서 매 0.5초 Tick++
- TextMeshProUGUI 1개에 Tick 값 표시 (양쪽 인스턴스에서 같은 값이어야 함)

NetworkSyncProbe 프리팹:
- 경로: `Assets/_Project/Prefabs/Networking/NetworkSyncProbe.prefab`
- NetworkObject 컴포넌트 부착, NetworkManager의 Network Prefabs 리스트에 등록
- 씬에 **씬-기반 NetworkObject로** 배치 (씬에 미리 두고 IsSceneObject)

### 5. Multiplayer Play Mode 설정

`Window > Multiplayer > Multiplayer Play Mode` 패널:
- Player 2 활성화 (총 2 인스턴스)
- 각 인스턴스가 동일 씬을 자동 로드하는지 확인

### 6. 검증 시나리오

| 시나리오 | 기대 |
|---|---|
| 인스턴스 1에서 Host 시작 | NetworkSyncProbe Tick 증가 시작 |
| 인스턴스 2에서 Client 시작 (127.0.0.1:7777) | 즉시 접속, Tick 값 동기화 |
| 인스턴스 2에서 Disconnect | 인스턴스 2의 NetworkSyncProbe 정지, 인스턴스 1은 계속 |
| 인스턴스 1에서 Disconnect (Host 종료) | 인스턴스 2 자동 disconnect 확인 |
| 콘솔 에러 | 모든 시나리오에서 0건 |

## 비범위

- **Relay 연결** — CL-023에서 도입 (이번엔 127.0.0.1 직결)
- **참여코드** — CL-023/CL-024
- **플레이어 프리팹** — CL-022 (이번엔 PlayerPrefab 슬롯 빈 채로)
- **호스트 이탈 우아한 처리** — CL-025
- **이동/공격 동기화** — CL-026 이후
- **인증** — Unity Authentication 익명 로그인은 Relay 필요 시 CL-023에서

## 사용자 결정 필요

| 항목 | 옵션 | 권장 |
|---|---|---|
| 씬 파일명 | `CL021_NetworkTest_2P` / `Test_Network_2P` / 기타 | `CL021_NetworkTest_2P.unity` (cl037 패턴 일치) |
| ConnectMenu 위치 | OnGUI(IMGUI) 즉석 / uGUI Canvas | uGUI (시각 일관성) |
| NetworkSyncProbe 정보 표시 | 화면 텍스트 / 콘솔 로그만 | 화면 텍스트 (시각 검증 빠름) |
| Build Settings 추가 여부 | 추가 / 미추가 | 추가 (Multiplayer Play Mode가 자동 로드하려면 Build Settings에 등록 필요) |

## 위험 요소

1. **NetworkManager DontDestroyOnLoad 동작** — Multiplayer Play Mode에서 두 인스턴스가 같은 NetworkManager 인스턴스를 공유하지 않도록 주의 (각 프로세스 인스턴스가 독립).
2. **씬-기반 NetworkObject vs 동적 스폰** — 본 CL은 씬-기반으로 단순화. 추후 CL-022에서 동적 스폰으로 전환 시 NetworkPrefabsList 등록 필요.
3. **Build Settings 누락** — Multiplayer Play Mode 인스턴스가 Build Settings에 없는 씬은 자동 로드 못 함. 본 씬 추가 필수.
4. **포트 충돌** — 7777이 다른 프로세스에 점유돼 있으면 호스트 시작 실패. UnityTransport에 다른 포트(예 7778) 폴백 가능하게 만들지 결정 필요.

## 변경/신규 파일

### 신규

| 파일 | 내용 |
|---|---|
| `Assets/Scenes/Test/CL021_NetworkTest_2P.unity` | 테스트 씬 |
| `Assets/_Project/Scripts/Runtime/Networking/ConnectMenu.cs` | 임시 Host/Client UI |
| `Assets/_Project/Scripts/Runtime/Networking/NetworkSyncProbe.cs` | NetworkVariable 동기화 검증 NetworkBehaviour |
| `Assets/_Project/Prefabs/Networking/NetworkSyncProbe.prefab` | NetworkObject 부착 프리팹 |

### 변경

| 파일 | 내용 |
|---|---|
| `ProjectSettings/EditorBuildSettings.asset` | 신규 씬을 Scenes In Build에 추가 |
| `ProjectSettings/MultiplayerManager.asset` | Multiplayer Play Mode 인스턴스 수 2로 |

### 미변경

- `Packages/manifest.json` — CL-020에서 처리 끝
- 기존 게임 씬·플레이어 코드 — 본 CL은 격리된 검증

## 검증 (E2E)

1. 테스트 씬 단독 Play (싱글 모드) — 에러 0건
2. Host 시작 → Tick 증가 화면에 보임 → Disconnect → 정지
3. Multiplayer Play Mode 2 인스턴스, 한 쪽 Host / 다른 쪽 Client → Tick 동기화 확인
4. Host 강제 종료(앱 닫기) → Client 측 disconnect 콜백 발생 확인 (콘솔 로그)
5. 같은 씬에서 1번 시나리오 반복 5회 — 안정성

## 관련 문서

- `docs/12_development_plan.md:200-217` — 1단계 네트워크 테스트 씬 산출물 정의
- `docs/13_client_detailed_plan.md` — Wave 1 기반 세팅
- `docs/14_client_jira_story_backlog.md` — CL-021 정의

## 다음 CL

- **CL-022**: 플레이어 네트워크 프리팹 구조 적용 — 본 씬의 PlayerPrefab 슬롯 채우기 + TDE Character와 NetworkObject 통합 패턴 확정.

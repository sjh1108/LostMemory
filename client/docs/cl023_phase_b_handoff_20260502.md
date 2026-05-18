# Phase A·B 진행 상황 핸드오프 (2026-05-02)

## 문서 목적

작업 환경을 노트북에서 데스크톱으로 전환하는 시점의 *체크포인트 보고서*. 데스크톱에서 작업을 *어디서부터* 이어가야 하는지, *무엇이 검증됐고 무엇이 아직 남았는지* 명시한다.

본 문서를 읽으면 컨텍스트 0 인 상태에서도 작업 재개 가능하도록 작성.

## 환경 전환 사유

Phase B-2 검증부터는 **2인 인스턴스 동시 실행** (MPPM 또는 빌드 + Editor 동시) 이 필요하다. 노트북 환경에선:

- CPU/RAM/GPU 동시 점유로 인스턴스 응답성 저하
- 지터(딸깍거림) 가 환경 탓인지 동기화 코드 탓인지 *분리 분석 불가*
- 패키지 빌드 + 두 인스턴스 운영의 디스크/메모리 부담

→ 데스크톱 환경에서 검증 진행이 정확도/효율 모두 우수.

## 1. 완료된 작업

### 1.1 Phase A — 네트워크 레이어 셋업 (검증 통과 ✅)

원본 12개 task list 중 7개 완료.

| # | Task | 산출물 | 검증 |
|---|---|---|---|
| 1 | NGO·Relay 패키지 셋업 | `manifest.json` 확인 (이미 설치) | ✅ |
| 2 | 네트워크 테스트 씬 | `Test_Network_AD.unity` (사용자 생성) | ✅ |
| 4 | 호스트 + Relay 코드 발급 | `RelaySessionHost.cs` | ✅ 코드 `WQLPCJ` 발급 확인 |
| 5 | 코드 입력 참가 | `RelaySessionClient.cs` | ✅ 코드 `NTHFNG` 로 참가 성공 확인 |
| 6 | 세션 종료/이탈 처리 | `SessionLifecycle.cs` | (자명, 코드 검증) |
| 11 | 참가 실패/접속 오류 흐름 | `SessionErrorPolicy.cs` | (자명, 코드 검증) |
| 12 | 네트워크 핵심 로그 포인트 | `NetLog.cs` | ✅ Console 로그 출력 확인 |

**Phase A 핵심 산출물 위치:**
```text
LostMemory/Assets/_Project/Scripts/Runtime/Networking/
  Common/
    NetLog.cs
    HostAuthority.cs
  Session/
    RelaySession.cs
    RelaySessionHost.cs
    RelaySessionClient.cs
    SessionLifecycle.cs
    SessionErrorPolicy.cs
    RelayJoinCodeUI.cs

LostMemory/Assets/_Project/Scenes/Test/
  Test_Network_AD.unity                  (사용자 생성)
LostMemory/Assets/_Project/Prefabs/Characters/
  Stub_NetworkPlayer.prefab              (사용자 생성, Phase B-2 후 보관/삭제)
```

### 1.2 Phase B-1 — 호스트 권위 게이트 통일 (코드 완료, 싱글 검증 통과 ✅)

기존 `IsAuthority => true` 분산을 `HostAuthority.IsHost` 단일 정책으로 통일.

#### 수정된 파일

| 파일 | 변경 |
|---|---|
| `_Project/Scripts/Runtime/Stage/RunManager.cs:64` | `IsAuthority => HostAuthority.IsHost` |
| `_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs:62` | 동일 |
| `_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs:56` | 동일 |
| `_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs:40` | `_authority` 를 `NetworkRelicEffectAuthority` 로 교체 |

#### 신규 파일

```text
_Project/Scripts/Runtime/Relics/
  NetworkRelicEffectAuthority.cs
_Project/Scripts/Runtime/Networking/Player/
  LocalPlayerResolver.cs                 (Phase B-2/B-3 까지 활용 골격)
```

#### 검증 결과

- 컴파일 통과
- 싱글 플레이 흐름 *변경 전과 동일* 동작 확인 (HostAuthority.IsHost 가 NetworkManager 비활성 시 true 반환하는 fallback 정책 동작)

### 1.3 Phase B-2 — 플레이어 NetworkObject + 이동 동기화 (코드 완료, Editor 작업 + 검증 미수행 ⏳)

#### 코드 (완료)

```text
_Project/Scripts/Runtime/Networking/Player/
  PlayerMovementSync.cs
```

- `NetworkTransform` 상속 + `OnIsServerAuthoritative() => false` (owner 권위 위치 sync)
- 비-owner: `Character.CharacterType = AI` 로 전환 (TDE InputManager 입력 차단)
- owner: `LocalPlayerResolver.Register()` 호출

#### 절차서 (완료)

```text
client/docs/cl022_player_network_prefab_setup_plan.md
```

#### 미수행 (데스크톱에서 진행)

- TestKhi_Net_AD.prefab 생성 (TestKhi_MinimalCharacter2D 복제)
- NetworkObject + PlayerMovementSync 부착
- NetworkManager Player Prefab 슬롯 교체 (Stub → TestKhi_Net_AD)
- 2인 인스턴스 검증

### 1.4 보조 산출물 — 통합 규칙 / 절차서 (검토 진행)

```text
client/docs/commonness/
  networking-integration-rules.md        (초안 v0.1, Phase B 후 v1.0 갱신 예정)

client/docs/
  cl020_network_test_scene_setup_plan.md (Phase A 절차서)
  cl022_player_network_prefab_setup_plan.md (Phase B-2 절차서)
  cl023_phase_b_handoff_20260502.md      (본 문서)
```

## 2. 미완료 — 데스크톱에서 이어할 작업

### 2.1 즉시 시작점

**Phase B-2 의 Editor 작업** — `cl022_player_network_prefab_setup_plan.md` §1 (프리팹 복제) 부터 그대로 따라가면 됨. 절차서 자체가 사람 컨텍스트 0 가정으로 작성되어 있음.

### 2.2 Phase B 잔여 단계

| 단계 | 범위 | 상태 |
|---|---|---|
| **B-2** | Editor 작업 + 2인 검증 | ⏳ 시작점 |
| B-3 | KhiPlayerStateNetSync (행동 상태 동기화) | 코드 작성 예정 |
| B-4 | MeleeHitboxNetRouter (전투 호스트 라우팅) | 코드 작성 예정 |
| B-5 | 다운/부활 동기화 + RunManager Defeated 호스트 권위 | 코드 작성 예정 |

각 단계 시작 시 *해당 단계 코드 작성 → Unity Editor 작업 절차서 → 사용자 검증* 사이클 반복.

### 2.3 Post-Phase B

- `commonness/networking-integration-rules.md` 를 v1.0 으로 갱신 (실제 Phase B 결과 반영)
- 한글 폰트 정식 처리 (현재 임시)

## 3. 환경 셋업 (데스크톱)

### 3.1 Git

```bash
git pull              # 최신 변경 가져오기 (노트북에서 push 한 분)
git status            # 깨끗한지 확인
```

본 노트북에서 **반드시 push 하고 종료**. 안 그러면 데스크톱에서 작업 재개 불가.

### 3.2 Unity Hub

1. 데스크톱의 Unity Hub 에서 동일한 프로젝트 추가 (또는 폴더 직접 열기)
2. **Unity 6.3 (`6000.3.13f1`)** 설치돼있는지 확인. 없으면 Hub 에서 설치
3. 프로젝트 첫 오픈 시 패키지 import + 컴파일 자동 (5~10분 소요 가능)

### 3.3 Unity 계정 / Cloud Project

- Unity Hub 에서 **같은 Unity 계정** 로그인 (`gimhoein`)
- `Edit → Project Settings → Services` 로 가서 Project ID `a15aff52-5b9b-4b49-9552-c43dc3a62c69` 가 자동 인식되는지 확인
- 인식 안 되면 노트북과 동일하게 다시 연결

### 3.4 Multiplayer Play Mode (MPPM) 설치

데스크톱에서도 동일하게 MPPM 설치 필요.

1. `Window → Package Manager` → `Unity Registry` → 검색 `Multiplayer Play Mode` → Install
2. 설치 후 *Editor 재시작* 권장 (메뉴 갱신)
3. `Window → Multiplayer → Multiplayer Play Mode` 메뉴 확인

> 노트북에서 메뉴가 안 보이는 이슈 있었음. 데스크톱에서도 재시작 후에도 안 보이면 빌드 + Editor 동시 실행으로 대체 가능.

### 3.5 Build & Run 대비

MPPM 가 안 되면 빌드 방식. 데스크톱에서 첫 빌드 5~10분 걸릴 수 있음. `File → Build Profiles` 에 `Test_Network_AD` 씬이 등록돼있는지만 확인.

## 4. 알아야 할 컨텍스트 (Gotchas)

### 4.1 NetworkManager 의 Player Prefab 슬롯

- 현재 `Stub_NetworkPlayer.prefab` 가 들어있음 (Phase A 검증용)
- Phase B-2 Editor 작업 5단계에서 `TestKhi_Net_AD.prefab` 으로 교체
- 교체 안 하면 캐릭터가 화면에 안 보임 (Stub 은 빈 NetworkObject)

### 4.2 Stub_NetworkPlayer 의 운명

- B-2 통과 후엔 사용 안 함
- 보관 권장 (Phase A 회귀 테스트 시 임시 사용 가능)
- 삭제해도 무방

### 4.3 한글 폰트 임시 처리

- 사용자가 노트북에서 임시 처리함
- 데스크톱 환경 옮긴 후 Console 로그/UI 한글 표시 확인 필요
- 임시 상태 그대로 유지하거나, 정식 한글 TMP 폰트로 정리

### 4.4 Unity Cloud 무료 한도

- Relay/Multiplayer Service 는 무료 티어 한도 내에서 동작 중
- 검증 반복 시 호출량 증가 → 한도 초과 시 *방 생성에 실패* 메시지 발생 가능
- 대시보드 (`https://cloud.unity.com/`) 에서 사용량 확인

### 4.5 IsAuthority 게이트의 fallback

- `HostAuthority.IsHost` 는 NetworkManager 가 *없거나 listening 중이 아니면* true 반환
- → 싱글 플레이 흐름 (Town 등 NetworkManager 없는 씬) 은 변경 전과 100% 동일
- 멀티 환경에서만 호스트 권위 분기 활성

### 4.6 NetworkPrefabs 리스트 자동 등록

- Phase B-2 절차서 5-1 단계에서 확인
- Player Prefab 으로 지정하면 보통 자동 등록됨
- 자동 안 되면 수동으로 `+` 클릭

## 5. 검증 시작점 (데스크톱)

데스크톱에서 첫 작업은 다음 순서:

1. Git pull, Unity Editor 오픈, 패키지 import 대기
2. Console 빨간 에러 없는지 확인 (B-1 코드 변경분 정상 컴파일)
3. **`cl022_player_network_prefab_setup_plan.md` §1 부터 따라가기** ← 정확한 시작점
4. §7 검증 단계까지 통과되면 알림
5. Phase B-3 진입

## 6. 관련 파일 빠른 참조

### 핵심 코드 (Phase A + B-1 + B-2)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Networking/
  Common/
    HostAuthority.cs
    NetLog.cs
  Session/
    RelaySession.cs
    RelaySessionHost.cs
    RelaySessionClient.cs
    SessionLifecycle.cs
    SessionErrorPolicy.cs
    RelayJoinCodeUI.cs
  Player/
    LocalPlayerResolver.cs
    PlayerMovementSync.cs

LostMemory/Assets/_Project/Scripts/Runtime/Relics/
  NetworkRelicEffectAuthority.cs

# 수정된 기존 파일 (1~3줄 수정)
LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs
LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs
LostMemory/Assets/_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs
LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs
```

### 절차서 / 규칙 doc

```text
client/docs/
  cl020_network_test_scene_setup_plan.md      Phase A 절차
  cl022_player_network_prefab_setup_plan.md   Phase B-2 절차 ← 여기서 재개
  cl023_phase_b_handoff_20260502.md           본 문서

client/docs/commonness/
  networking-integration-rules.md             통합 규칙 (v0.1 초안)
```

### Unity 산출물

```text
LostMemory/Assets/_Project/Scenes/Test/Test_Network_AD.unity
LostMemory/Assets/_Project/Prefabs/Characters/Stub_NetworkPlayer.prefab
LostMemory/Packages/manifest.json    (변경 없음, NGO 2.11.1 + services.multiplayer 2.2.1 사전 설치)
```

## 7. 검증 결과 기록 (참고)

### Phase A 2인 검증 (노트북에서 통과)

- 호스트 측: `[Net][Session] Signed in. PlayerId=orQ2XRFJOY0tYKUBmnHWJUOSfTWJ`
- 호스트 측: `[Net][Host] Session created. JoinCode=WQLPCJ`
- 클라 측: `[Net][Session] Signed in. PlayerId=ZIM6uSKNr2Qt4o9FKFkMFjVfVcLV`
- 클라 측: `[Net][Client] Joining session by code=NTHFNG...`
- 클라 측: `[Net][Client] Joined session.`

→ Auth 분리 + 코드 발급 + 코드 입력 참가 흐름 모두 정상.

## 8. 관련 문서

- `docs/04_multiplayer.md` — 멀티 규칙 (호스트 권위 모델)
- `docs/12_development_plan.md` — 개발 계획 (NGO + Relay 결정)
- `docs/13_client_detailed_plan.md` — 클라 상세 계획 (Wave 1·2 매핑)
- `docs/14_client_jira_story_backlog.md` — Jira 백로그 (CL-020~031 = 본 작업 범위)

## 9. 다음 핸드오프 작성 시점

다음 환경 전환 또는 장기간 휴지 시점에 *동일 양식* 으로 신규 doc 작성 권장.

```text
client/docs/cl024_phase_b_handoff_<YYYYMMDD>.md
client/docs/cl025_phase_b_handoff_<YYYYMMDD>.md
```

본 문서는 *그 시점의 스냅샷* 이므로 갱신하지 않고 *새 문서* 로 누적.

# CL-020 ~ CL-025 + CL-031 — Phase A·B-1·B-2 통합 완료 (2026-05-03)

브랜치: `feat/S14P31C201-214/cl-ngo-relay-패키지-세팅-및-버전-고정`

## 문서 목적

본 브랜치에서 완료된 CL 들의 통합 완료 보고. PR 설명 + Jira 일괄 Done 처리의 근거 자료.

## Context — 스코프 확장 사실 명시

브랜치명 = `S14P31C201-214` = **CL-020 (NGO·Relay 패키지 세팅) 단일 티켓**.

그러나 네트워크 레이어는 *세션 생성 → 코드 발급 → 참가 → 플레이어 spawn → 위치 sync* 까지의 흐름이 한 묶음으로 검증돼야 의미가 있어, 단일 커밋([8d4d66f92](../../client) `phase A 진행 끝 B 진행 중`) 에 다음 7개 CL 의 코드·검증이 누적되었음.

- CL-020 패키지 세팅
- CL-021 테스트 씬 구성
- CL-022 PlayerPrefab 구조 + 호스트 권위 게이트 통일
- CL-023 호스트 생성·Relay 코드 발급
- CL-024 코드 입력 참가
- CL-025 세션 종료·호스트 이탈
- CL-031 핵심 로그 포인트 정리

**미포함 (별도 브랜치 예정)**:
- **CL-026** 위치 동기화 polish (TickRate/Threshold 튜닝 + Stage A/B/C 시뮬레이션 검증) — 본 브랜치는 *CL-022 수준의 "대충 따라옴"* 까지만 도달. cl026 plan §5 의 튜닝 결과 기록 표는 비어 있음
- CL-027 방향·행동 상태 동기화
- CL-028 공격 판정·적 반응 동기화
- CL-029 다운·부활 동기화
- CL-030 데모 안전망 (본 브랜치는 SessionErrorPolicy 의 happy path 외 *기본* 처리만)

향후 브랜치는 `1 CL = 1 브랜치 = 1 PR` 패턴 엄수.

---

## 1. CL-020 — NGO·Relay 패키지 세팅

`LostMemory/Packages/manifest.json` 사전 설치 확인. 본 브랜치에서 추가 변경 없음.

| 패키지 | 버전 |
|---|---|
| `com.unity.netcode.gameobjects` | 2.11.1 |
| `com.unity.services.multiplayer` | 2.2.1 |
| `com.unity.services.core` | 1.16.0 |

검증: 컴파일 통과 + 런타임 Sessions API 호출 성공 (CL-023 검증 시 확인).

## 2. CL-021 — 네트워크 테스트 씬

**산출물**: [`Assets/_Project/Scenes/Test/Test_Network_AD.unity`](../../client/LostMemory/Assets/_Project/Scenes/Test/Test_Network_AD.unity)

씬 구성:
- `NetworkManager` (Unity Transport, PlayerPrefab=`TestKhi_Net_AD`)
- `SessionLifecycle` 단일 GameObject
- `Canvas` + UI 6개 (StatusText, JoinCodeDisplay, JoinCodeInput, Host/Join/Leave Button)
- `Town Input Manager` ([`TestKhiInputManager`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiInputManager.cs)) — Town 씬에서 복사. 입력 + Camera follow 일체

검증: Phase A 2인 시나리오 통과 (호스트 코드 `WQLPCJ` 발급 → 클라 `NTHFNG` 참가 성공 로그 확인).

## 3. CL-022 — 플레이어 네트워크 프리팹 구조

### 3.1 PlayerPrefab — `TestKhi_Net_AD`

**산출물**: `Assets/_Project/Prefabs/Characters/TestKhi_Net_AD.prefab` (Unity Editor 작업으로 사람이 생성)

`TestKhi_MinimalCharacter2D.prefab` 복제 → 루트에 `NetworkObject` + `PlayerMovementSync` 부착.

### 3.2 [`PlayerMovementSync.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Networking/Player/PlayerMovementSync.cs)

**파일**: `Assets/_Project/Scripts/Runtime/Networking/Player/PlayerMovementSync.cs`

`NetworkTransform` 상속 + `OnIsServerAuthoritative() => false` (owner 권위 위치 sync).

| 분기 | 동작 |
|---|---|
| `IsOwner` | `LocalPlayerResolver.Register(stateAggregator)` 호출 |
| 비-owner | `Character.CharacterType = AI` 전환 (TDE InputManager 의 입력 차단) |
| 비-owner | 마우스/InputAction 의존 컴포넌트 5개 `enabled = false` (입력 격리) |

**비-owner 측에서 disable 되는 5개 컴포넌트**:
- [`KhiMeleeComboController`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs) — 좌클릭 공격, `Mouse.current` 직접 읽음
- [`KhiParryController`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs) — 패리, `Mouse.current` 직접 읽음
- [`KhiDashController`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDashController.cs) — 대시, `KhiPlayerAim` 사용
- [`KhiFinisherLunge`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiFinisherLunge.cs) — 피니셔, `KhiPlayerAim` 사용
- [`KhiWeaponPresenter`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiWeaponPresenter.cs) — 자식 오브젝트 (무기 visual), `GetComponentsInChildren` 으로 처리

> 트레이드오프: 비-owner 측에서 *상대의 공격/대시/패리 시각 효과 안 보임*, *상대 무기 회전 fallback (Vector2.right)*. 이건 의도된 미완성 — CL-027 (행동 상태 broadcast) 에서 owner 가 NetworkVariable 로 송신 → 비-owner 가 시각 효과만 reproduce 하는 형태로 보강 예정.

### 3.3 호스트 권위 게이트 통일 (Phase B-1)

기존 분산된 `IsAuthority => true` 를 [`HostAuthority.IsHost`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Networking/Common/HostAuthority.cs) 단일 정책으로 통일.

| 파일 | 변경 |
|---|---|
| [`RunManager.cs:64`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs:64) | `IsAuthority => HostAuthority.IsHost` |
| [`RoomEntryRuntimeController.cs:62`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs:62) | 동일 |
| [`DungeonRunBootstrap.cs:56`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs:56) | 동일 |
| [`RelicEffectRegistry.cs:40`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs:40) | `_authority` 를 [`NetworkRelicEffectAuthority`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Relics/NetworkRelicEffectAuthority.cs) 로 교체 |

`HostAuthority.IsHost` 는 `NetworkManager.Singleton` 이 null 이거나 listening 중이 아니면 *true 반환* (fallback). 결과: 싱글플레이 흐름 (NetworkManager 없는 Town 씬 등) 은 변경 전과 100% 동일 동작.

### 3.4 보조 — `LocalPlayerResolver`

**파일**: [`LocalPlayerResolver.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Networking/Player/LocalPlayerResolver.cs)

PlayerMovementSync 의 owner 진입 시 `KhiPlayerStateAggregator` 를 등록 → UI/카메라 등이 "내 플레이어" 식별 가능하게. CL-027 이후 단계에서 본격 활용 예정.

### 3.5 검증

- 컴파일 통과
- 단일 인스턴스: 호스트 클릭 시 캐릭터 spawn, WASD 이동, `[Net][Player] Local player spawned` 로그
- 2인 인스턴스: 양쪽 화면에 캐릭터 2개 보임
- WASD 입력 격리: 자기 캐릭터만 이동
- **마우스 입력 격리**: 자기 캐릭터의 무기/aim 만 자기 마우스 따라감 (3.2 의 비-owner 컴포넌트 disable 효과)
- 위치 sync: A 의 이동이 B 측 화면에 반영됨 (대충 따라옴 수준 — *CL-026 polish 미수행*)

## 4. CL-023 — 호스트 생성·Relay 코드 발급

**파일**: [`RelaySessionHost.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Networking/Session/RelaySessionHost.cs)

`Unity.Services.Multiplayer` Sessions API 의 `CreateSessionAsync` + Relay options 으로 호스트 생성, 6자리 join code 발급.

검증 로그:
```
[Net][Session] Signed in. PlayerId=orQ2XRFJOY0tYKUBmnHWJUOSfTWJ
[Net][Host] Session created. JoinCode=WQLPCJ
```

## 5. CL-024 — 코드 입력 참가

**파일**: [`RelaySessionClient.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Networking/Session/RelaySessionClient.cs)

`JoinSessionByCodeAsync` 로 코드 입력 참가. 실패 시 `JoinFailureReason` enum 으로 분류하여 UI 메시지 매핑.

검증 로그:
```
[Net][Session] Signed in. PlayerId=ZIM6uSKNr2Qt4o9FKFkMFjVfVcLV
[Net][Client] Joining session by code=NTHFNG...
[Net][Client] Joined session.
```

## 6. CL-025 — 세션 종료·호스트 이탈 처리

**파일**:
- [`SessionLifecycle.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Networking/Session/SessionLifecycle.cs) — NetworkManager 의 disconnect 이벤트 구독, 5경로 정규화
- [`SessionErrorPolicy.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Networking/Session/SessionErrorPolicy.cs) — 각 실패 사유 → 사용자 메시지 매핑

CL-030 의 데모 안전망 (단계별 timeout, 반-중복 가드, 재시도 흐름 등) 은 본 브랜치 미포함 — 별도 브랜치 예정.

## 7. CL-031 — 핵심 로그 포인트 정리

**파일**: [`NetLog.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Networking/Common/NetLog.cs)

`[Net][<Tag>]` 단일 prefix 정책. 모든 Phase A 코드가 NetLog 만 사용. 콘솔 grep 으로 네트워크 흐름 일괄 추적 가능.

---

## 알아야 할 한계 (의도된 미완성)

본 브랜치 통과 후에도 다음은 *동작하지 않음*. CL-027 이후에서 보강.

| 한계 | 사유 | 보강 시점 |
|---|---|---|
| 비-owner 측 무기 회전 = 우측 고정 (`Vector2.right` fallback) | 3.2 의 KhiWeaponPresenter disable | CL-027 (aim 방향 broadcast) |
| 비-owner 측에서 상대 공격/대시/패리 시각 효과 안 보임 | 3.2 의 액션 컨트롤러 5개 disable | CL-027/028 (행동 상태 broadcast → 비-owner 측 reproduce) |
| 상대가 공격해도 내 인스턴스에서 hit 판정 미발화 | 호스트 라우팅 미구현 | CL-028 (`MeleeHitboxNetRouter`) |
| 위치 동기화 jitter / snap 발생 가능 | NetworkTransform 기본 파라미터 (Threshold 0.001 등) 그대로 | **CL-026** (TickRate/Threshold 튜닝 + Stage A/B/C 검증) |
| 다운/부활 동기화 불일치 | 솔로 기준 상태머신 그대로 | CL-029 |
| 데모 안전망 (timeout/취소/반-중복) | 기본 에러 분류만 됨 | CL-030 |

---

## Jira 처리 권장

본 PR 머지 시 다음 CL 일괄 Done:

- ✅ CL-020 NGO·Relay 패키지 세팅
- ✅ CL-021 네트워크 테스트 씬 구성
- ✅ CL-022 플레이어 네트워크 프리팹 구조
- ✅ CL-023 호스트 생성 및 Relay 참여 코드 발급
- ✅ CL-024 코드 입력 기반 참가
- ✅ CL-025 세션 종료·호스트 이탈 처리
- ✅ CL-031 네트워크 핵심 로그 포인트 정리

각 CL 코멘트에 본 PR 링크 + 본 doc 경로 첨부 권장.

CL-026/027/028/029/030 은 In Progress 또는 To Do 유지.

---

## 다음 브랜치 가이드

권장 진입 순서:

1. **CL-026** 위치 동기화 polish — `feat/S14P31C201-XXX/cl-위치-동기화-polish` 브랜치
   - cl026 plan §5 Stage A/B/C 시나리오 수행
   - cl026 plan "튜닝 결과 기록" 표 채우기
2. **CL-027** 방향·행동 상태 동기화 — `KhiPlayerStateNetSync` (행동 상태 NetworkVariable broadcast). 이게 들어가야 비-owner 측 시각 효과 reproduce 가능
3. **CL-028** 공격 판정 호스트 라우팅 — `MeleeHitboxNetRouter`
4. **CL-029** 다운·부활 동기화 — [cl029 plan](cl029_down_revive_sync_plan.md) 참고
5. **CL-030** 데모 안전망 — [cl030 plan](cl030_join_connection_error_flow_plan.md) 참고

각 브랜치는 1 CL = 1 PR 패턴 엄수.

---

## 관련 문서

### 본 브랜치 산출 문서
- [`client/docs/cl020_network_test_scene_setup_plan.md`](../../client/docs/cl020_network_test_scene_setup_plan.md) — Phase A 절차서
- [`client/docs/cl022_player_network_prefab_setup_plan.md`](../../client/docs/cl022_player_network_prefab_setup_plan.md) — Phase B-2 절차서
- [`client/docs/cl023_phase_b_handoff_20260502.md`](../../client/docs/cl023_phase_b_handoff_20260502.md) — 노트북→데스크톱 환경 전환 시점 핸드오프
- [`client/docs/commonness/networking-integration-rules.md`](../../client/docs/commonness/networking-integration-rules.md) — 통합 규칙 v0.1

### Plan 문서 (`docs/khi/`)
- [cl020](cl020_ngo_relay_package_setup_plan.md), [cl021](cl021_network_test_scene_plan.md), [cl022](cl022_player_network_prefab_plan.md), [cl023](cl023_host_create_relay_code_plan.md), [cl024](cl024_code_input_join_plan.md), [cl025](cl025_session_end_host_leave_plan.md), [cl026](cl026_position_sync_polish_plan.md), [cl027](cl027_direction_state_sync_plan.md), [cl029](cl029_down_revive_sync_plan.md), [cl030](cl030_join_connection_error_flow_plan.md)

### 상위 문서
- `docs/04_multiplayer.md` — 멀티 규칙 (호스트 권위 모델)
- `docs/12_development_plan.md` — 개발 계획 (NGO + Relay 결정)
- `docs/13_client_detailed_plan.md` — 클라 상세 계획 (Wave 1·2 매핑)
- `docs/14_client_jira_story_backlog.md` — Jira 백로그

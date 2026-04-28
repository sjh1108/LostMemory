# CL-026: 2인 이동 위치 동기화 구현

소속 Epic: **Epic C. 멀티플레이 / 세션 네트워크**

선행: CL-020 (패키지), CL-021 (테스트 씬), CL-022 (NetworkObject/NetworkTransform 부착), CL-023 (호스트), CL-024 (코드 입력 참가), CL-025 (세션 종료)

## Context

CL-022에서 `NetworkObject` + `NetworkTransform` + `NetworkRigidbody2D`를 프리팹에 붙이고 IsOwner 분기 진입점까지 만들었다. 이 시점에서 "양쪽 인스턴스에 캐릭터가 보이고 대충 같은 자리로 따라온다"는 수준은 동작한다.

CL-022 plan의 **비범위 첫 줄**: "정확한 위치 동기화 보정 (CL-026)"([cl022_player_network_prefab_plan.md:175](cl022_player_network_prefab_plan.md)).

본 CL은 CL-022가 만든 구조를 **튜닝**해서 다음을 달성한다:
- 30~60ms RTT(국내) 환경에서 Remote 캐릭터가 부드럽게 따라옴 (jitter < 1프레임 시각 차이)
- 100~200ms RTT + 5% 패킷 손실 환경(국내~일본 Relay 등)에서도 텔레포트/스냅 백 없이 부드럽게 보정
- TDE `CharacterMovement` Rigidbody2D 이동 + `NetworkTransform` 동기화 사이에 **충돌·진동 없음**
- Owner 측에서 입력→이동→상대 화면 반영까지 체감 일관 (예측·보정 미사용 가정 — MVP)

본 CL의 산출물은 **튜닝된 파라미터 표 + 검증 절차**가 핵심. 코드 변경량은 적음.

비목표:
- 클라이언트 사이드 예측(Client-side prediction) / 서버 reconciliation — MVP는 Owner authoritative로 충분
- 방향·조준 동기화 — CL-027 (회전·`KhiPlayerAim`)
- 행동 상태(공격·대시·패링) 동기화 — CL-027/028
- 보간/외삽 알고리즘 자체 작성 — NGO `NetworkTransform` 내장 보간 사용
- 4인 동기화 — MVP는 2인

## 현재 상태 (가정 + 의존)

| 항목 | 상태 |
|---|---|
| `NetworkObject` 부착 | CL-022 완료 |
| `NetworkTransform` 부착 | CL-022 완료 (Authority=Owner, Pos Threshold=0.001, Rot Threshold=1) |
| `NetworkRigidbody2D` 부착 | CL-022 완료 (Remote는 자동 Kinematic) |
| `NetworkManager.NetworkConfig.TickRate` | 기본값(30) — CL-022에서 명시 변경 안 함 |
| Interpolation | NGO `NetworkTransform.Interpolate` ON (CL-022 Step 2) |
| 입력 → Rigidbody2D 이동 | TDE `CharacterMovement` (변경 X) |
| Network Simulator | NGO `UnityTransport`에 내장(시뮬레이션 패킷 손실/지연) — 본 CL에서 첫 사용 |

CL-022 기본 동작은 OK 가정. 본 CL은 "기본 동작 → polish"의 갭을 메운다.

## 채택 접근: NetworkTransform Owner Authority + 보간 튜닝

| 옵션 | 장점 | 단점 |
|---|---|---|
| (A) NetworkTransform Owner + Interpolate 튜닝 | 코드 0, NGO 내장 안정 | 예측 없음 → 본인 입력은 즉각, 상대는 약간 지연 (수용) |
| (B) Client-side prediction + reconciliation | RTT 200ms 환경에서도 즉각감 | 구현 복잡, 액션 게임 한정으로 ROI 낮음 (MVP 비목표) |
| (C) ServerRpc로 직접 좌표 보고 + 서버 보간 | 권한 깔끔 | 반응성 손해, NetworkTransform 무력화 의미 |

**채택: (A)**. CL-022에서 이미 결정된 Owner Authority + Interpolate를 그대로 쓰고, **TickRate / Threshold / Interpolate 파라미터만** 튜닝.

## 목표 파라미터 (튜닝 결과)

작업자는 아래 표를 시작점으로 두고, "검증 시나리오"에서 측정한 결과로 조정해 최종값 확정.

### NetworkManager 측

| 항목 | 시작값 | 비고 |
|---|---|---|
| `NetworkConfig.TickRate` | **30** | 30Hz = 1프레임당 2번 (60FPS 기준). 60으로 올리면 대역폭 2배 |
| `NetworkConfig.UseSnapshotDelta` | true (기본) | 변화량만 전송 |

### NetworkTransform 측 (TestKhi 프리팹)

| 항목 | 시작값 | 비고 |
|---|---|---|
| `AuthorityMode` | Owner (CL-022) | 변경 X |
| `Position Threshold` | **0.01** | CL-022의 0.001은 너무 민감 → 매 프레임 갱신 부담. 0.01(1cm) 단위로 완화 |
| `Rotation Threshold` | 1 (CL-022) | 변경 X |
| `Scale Threshold` | 0 | 동기화 OFF |
| `Use Half Float Precision` | true | 대역폭 절반, 정밀도 충분 |
| `Use Quaternion Synchronization` | false | 2D는 Z 회전만 — Quaternion 압축 불필요 |
| `Slerp Position` | false | 2D는 Lerp 충분 |
| `Interpolate` | true (CL-022) | 변경 X |

### UnityTransport Network Simulator (검증 전용 — 빌드에는 OFF)

| 항목 | 검증값 | 시나리오 |
|---|---|---|
| `Packet Delay (ms)` | 50 / 100 / 150 | RTT 30/60/120ms 시뮬 |
| `Packet Jitter (ms)` | 10 | 가변 지연 |
| `Packet Drop Rate (%)` | 0 / 1 / 5 | 패킷 손실 |

검증 후 빌드 시 모두 0으로 복귀.

## 작업 범위

### Step 1 — NetworkManager TickRate 명시 설정

`NetworkManager.NetworkConfig.TickRate = 30` 명시(기본값이지만 의도 표현).

CL-021 씬의 NetworkManager Inspector에서 직접 수정 또는 부트 시점 코드:
- `Assets/_Project/Scripts/Runtime/Networking/UnityServicesBootstrap.cs` (CL-023) Awake 직후
- 또는 NetworkManager 확장 스크립트 신규 (`NetworkConfigBinder.cs`) — 권장

### Step 2 — NetworkTransform Inspector 튜닝

`TestKhi_MinimalCharacter2D.prefab`에서 NetworkTransform 컴포넌트 값을 위 표대로 수정.

prefab 편집 모드 → Inspector 직접 변경 → Apply.

### Step 3 — TDE CharacterMovement vs NetworkTransform 흐름 검증

**문제 가능성**: TDE `CharacterMovement.Movement` (Update에서 Rigidbody2D 이동) + `NetworkRigidbody2D` (Owner는 Dynamic, Remote는 Kinematic) + `NetworkTransform` (위치 보간 적용).

**Remote 인스턴스에서**:
- CharacterMovement는 `KhiPlayerInputManager`로부터 입력을 못 받음(IsOwner=false에서 비활성, CL-022) → 이동 명령 0
- Rigidbody2D는 Kinematic → 물리 이동 0
- NetworkTransform이 Owner 측 위치를 매 tick 갱신 → 이게 유일한 위치 결정 경로

**검증 포인트**:
- Remote 측 CharacterMovement.enabled 가 false인지 (CL-022 NetworkPlayerInitializer 분기 확인)
- 만약 Remote에서 CharacterMovement가 활성이면 해당 컴포넌트가 매 프레임 Rigidbody2D에 zero velocity를 쓰지 않는지
- Animator의 walk 애니메이션은 Remote에서도 재생되어야 함 → CharacterMovement가 NetworkTransform 위치 변화를 감지해 Speed 파라미터를 갱신하거나, 별도 처리 필요(CL-027 영역). 본 CL에선 walk 애니메이션이 안 나와도 OK (위치만 부드러우면 통과).

### Step 4 — 카메라 흔들림 방지

`KhiPlayerCamera`는 Owner 캐릭터만 추적(CL-022 Step 4). Remote 캐릭터는 카메라 무관.

**검증**: Remote 캐릭터가 보간 중 미세 떨림이 있어도 카메라는 Owner 캐릭터를 따라가므로 본인 시점은 흔들림 없음. 본 CL에서 추가 작업 불필요.

### Step 5 — 검증 시나리오 (3 단계)

#### Stage A: 로컬 (Multiplayer Play Mode, 무지연)

| # | 시나리오 | 측정 | 기준 |
|---|---|---|---|
| A1 | 호스트가 가만히 서 있을 때 | 클라 화면의 호스트 캐릭터 떨림 | 떨림 없음, 위치 stable |
| A2 | 호스트가 좌우로 8자 이동 | 클라 화면의 호스트 캐릭터 궤적 | 궤적 곡선 부드러움, snap 없음 |
| A3 | 호스트가 빠르게 방향 전환 (좌→우→좌) | 시각 지연 | 100ms 이하 (육안 60FPS 기준 6프레임) |
| A4 | 클라가 동시에 이동 | 양쪽 화면의 두 캐릭터 위치 | 양쪽 인스턴스에서 같은 시각 위치가 ±0.1유닛 이내 |

#### Stage B: 시뮬레이션 지연 (Network Simulator 50ms / 0% loss)

| # | 시나리오 | 기준 |
|---|---|---|
| B1 | A1~A4 반복 | 떨림은 약간 증가 가능, snap 없음 |
| B2 | 호스트 30초 자유 이동 | Tick 손실 0, 좌표 누적 오차 0 |

#### Stage C: 시뮬레이션 지연 + 패킷 손실 (100ms delay + 5% loss)

| # | 시나리오 | 기준 |
|---|---|---|
| C1 | 호스트 자유 이동 | snap이 발생하더라도 0.5유닛 이내. 텔레포트(>2유닛) 0건 |
| C2 | 60초 연속 이동 | 콘솔 에러 0, 누적 좌표 표류 < 0.2유닛 |

각 단계에서 기준 미달이면 위 파라미터 표 조정 후 재측정. 변경 이력은 본 plan md "튜닝 결과 기록" 섹션(아래)에 누적.

## 튜닝 결과 기록 (작업 시 채움)

| 측정 일자 | 환경 | 변경값 | 결과 |
|---|---|---|---|
| YYYY-MM-DD | A1~A4 무지연 | (시작값) | 통과/실패 + 메모 |
| YYYY-MM-DD | B1~B2 50ms | (조정값) | ... |

작업자가 채움. PR 시점에 본 표가 채워져 있어야 완료.

## 비범위

- **방향·조준 동기화** — CL-027 (`KhiPlayerAim` Z 회전 또는 별도 NetworkVariable)
- **행동 상태 동기화 (Walk/Run 애니메이션 트리거)** — CL-027
- **공격 판정 동기화** — CL-028
- **다운/부활 동기화** — CL-029
- **클라이언트 사이드 예측** — MVP 비목표
- **4인 동기화** — `docs/04_multiplayer.md:9` 1~4인이지만 MVP는 2인 우선
- **NetworkRigidbody2D 미제공 시 fallback** — CL-022가 처리

## 사용자 결정 필요

| 항목 | 옵션 | 권장 |
|---|---|---|
| TickRate | 15 / 30 / 60 | **30** (대역폭·반응성 균형) |
| Position Threshold | 0.001 / 0.01 / 0.05 | **0.01** (1cm, jitter 억제) |
| Half Float Precision | ON / OFF | **ON** (대역폭 절감, 2D 정밀도 충분) |
| 본 CL 검증 환경 | 로컬만 / Network Simulator 포함 / 외부망 1회 | **3가지 모두** (Stage A/B/C) |
| 튜닝 결과 기록 위치 | 본 plan md 표 / 별도 결과 md | **본 plan md** (CL 단위 기록 일관) |

## 위험 요소

1. **TickRate=30이 60FPS 입력에 부족** — Owner 입력은 60FPS로 들어오는데 동기화는 30Hz → 상대 화면에 1프레임 늦게 반영. 액션 게임이면 50ms는 체감 가능. 60Hz로 올릴지는 Stage A 결과에 따라.
2. **Half Float 정밀도 손실** — 큰 좌표(>1000유닛)에서 정밀도 떨어짐. 던전 좌표가 작다면 무관(현재 단일 룸 ~10유닛 범위, 안전).
3. **Network Simulator 빌드 잔존** — 검증용 설정을 빌드에 포함하면 항상 지연. UnityTransport `useNetworkSimulator` 플래그를 `Application.isEditor` 또는 #if 가드로 막기.
4. **CharacterMovement Remote 활성** — CL-022에서 비활성 처리됐다고 가정했으나 NetworkPlayerInitializer 누락 시 Remote 캐릭터가 매 프레임 Rigidbody2D에 zero velocity → NetworkTransform 위치와 충돌(되돌림 jitter). Step 3 검증 필수.
5. **Animator Speed 파라미터 미갱신** — Remote 캐릭터의 walk 애니메이션이 안 재생됨. 본 CL에선 OK이나 CL-027에서 처리 필요. 본 CL 검증에서 미관 이슈로 보고하지 말 것.
6. **NetworkRigidbody2D 부재** — 패키지 버전에 따라 미제공. CL-022 plan의 fallback(수동 BodyType 토글) 적용됐는지 확인.
7. **앱-내 시간 동기화** — NGO는 Tick 기반이라 클라이언트 간 절대 시간은 약간 오차. 위치 동기화에는 무관(보간이 처리).
8. **카메라 deadzone vs 보간된 Remote 캐릭터** — Remote 캐릭터 떨림이 카메라에 안 들어감(Owner만 추적). 안전.

## 변경/신규 파일

### 신규

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/NetworkConfigBinder.cs` (선택) | 부트 시 TickRate 등 명시 설정. NetworkManager Inspector 직접 수정으로 대체 가능 |

### 변경

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` | NetworkTransform Position Threshold 0.001→0.01, Half Float ON 등 |
| `Assets/Scenes/Test/CL021_NetworkTest_2P.unity` | NetworkManager.NetworkConfig.TickRate 명시 (30) |

### 미변경

- TDE Character / CharacterMovement (변경 0)
- 30개 Khi 컨트롤러 (변경 0)
- SessionService 등 CL-023~025 자산
- 게임 콘텐츠 코드/씬

## 검증 (E2E)

1. CL-021 씬에 PlayerPrefab 등록된 상태에서 솔로 Play — 회귀 0 (위치 동기화 코드는 멀티 모드에서만 영향)
2. Multiplayer Play Mode 2 인스턴스, Different Auth Profile, Host + Join — 양쪽에 2캐릭터
3. Stage A 시나리오 4건 모두 통과
4. UnityTransport Network Simulator로 50ms 지연 적용 → Stage B 통과
5. 100ms + 5% loss → Stage C 통과
6. Network Simulator OFF 후 빌드 → 빌드에서도 동작 확인 (Editor only 검증이 아님을 보장)
7. 본 plan md "튜닝 결과 기록" 표에 모든 측정 결과 기재

## 관련 문서

- `docs/04_multiplayer.md:7-12` — 1~4인 호스트 기반
- `docs/12_development_plan.md:201, 215` — 1단계 산출물 "2인 더미 캐릭터 이동 동기화 테스트" + 완료 기준 "위치 동기화가 최소한 동작"
- `docs/12_development_plan.md:232-244` — 호스트 권위 원칙
- `docs/14_client_jira_story_backlog.md:55` — CL-026 정의
- `docs/khi/cl022_player_network_prefab_plan.md:43-82, 175` — NetworkTransform 부착 + 본 CL 인계
- NGO `NetworkTransform` 공식 문서 (작업자가 패키지 버전 확인 후 참조)

## 다음 CL

- **CL-027**: 플레이어 방향·행동 상태 동기화 — 본 CL의 위치에 더해 회전(Z축)과 행동 상태(Walk/Run/Attack/Dash 등) 동기화. Animator 파라미터 또는 NetworkVariable로 처리.

# Epic C 네트워크 작업 — 팀 분배 가이드 (2026-05-04)

## 문서 목적

CL-020~025 통합 머지 직후 시점. 남은 CL-026~030 을 팀에 분배하기 위한 **위임 가이드**.

새로 합류하는 사람이 본 doc 하나만 읽으면 *어디서부터 시작해야 하는지* 결정 가능하게 작성됨.

> 본 doc 은 *시점 스냅샷*. 작업 진척이 바뀌면 새 doc 으로 누적 (`network_team_task_assignment_<YYYYMMDD>.md`).

---

## 1. 한눈 진척도

| CL | 항목 | 상태 | 담당 |
|---|---|---|---|
| CL-020~025, 031 | 네트워크 레이어 + 플레이어 spawn + 입력 격리 | ✅ 완료 (PR 진행 중) | 김회인 |
| **CL-026** | **위치 동기화 polish (튜닝 + Stage A/B/C 검증)** | ❌ 미수행 | **미배정** |
| **CL-027** | **방향·행동 상태 동기화 (NetworkVariable)** | ❌ 미수행 | 김회인 (예정) |
| **CL-028** | **공격 판정 호스트 라우팅** | ❌ 미수행 | 김회인 (예정) |
| **CL-029** | **다운·부활 동기화** | ❌ 미수행 | 김회인 (예정) |
| **CL-030** | **데모 안전망 (timeout, 재시도 등)** | 🟡 기본만 | **미배정** |

본 시점 기준 통합 보고서: [cl020_to_025_phase_ab_completion_20260503.md](cl020_to_025_phase_ab_completion_20260503.md)

---

## 2. 권장 분배

**원칙: 1 CL = 1 브랜치 = 1 PR**

| 담당 | CL | 난이도 | 사유 |
|---|---|---|---|
| **친구 A** (서버 첫 task) | CL-026 | ⭐ | plan 문서가 step-by-step. NGO 깊게 몰라도 가능 |
| **친구 B** (있으면) | CL-030 | ⭐⭐ | timeout/재시도 — 일반 프로그래밍 + 약간 NGO. 격리됨 |
| **김회인** | CL-027 → CL-028 → CL-029 | ⭐⭐⭐ ~ ⭐⭐⭐⭐⭐ | 호스트 권위/RPC/상태 머신 — 핵심 모델, 일관성 위해 한 사람이 |

친구 A 가 CL-026 통과 후 NGO 패턴 익숙해지면 **CL-027 일부 분담** 가능.

---

## 3. 친구 A — CL-026 작업 가이드

### 3.1 시작 전 필수 정독 (총 30분)

순서대로:

1. **[cl020_to_025_phase_ab_completion_20260503.md](cl020_to_025_phase_ab_completion_20260503.md)** — 지금까지 뭐가 됐고 뭐가 안 됐는지. 한 번 훑기
2. **[cl023_phase_b_handoff_20260502.md](../../client/docs/cl023_phase_b_handoff_20260502.md)** — 환경 셋업 + Gotchas. *데스크톱 셋업 절차* 부분만 본인 환경에 맞게 수행
3. **[networking-integration-rules.md](../../client/docs/commonness/networking-integration-rules.md)** — 시그니처 안정성 규칙. *§3.2 보호 대상 클래스 표* 만큼은 외울 것 (이거 시그니처 바꾸면 충돌)
4. **[cl026_position_sync_polish_plan.md](cl026_position_sync_polish_plan.md)** — 본인이 할 작업의 plan. 끝까지 정독
5. **[PlayerMovementSync.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Networking/Player/PlayerMovementSync.cs)** — 본인이 만들 코드의 레퍼런스 패턴

### 3.2 환경 셋업 체크리스트

- [ ] develop 에서 새 브랜치 생성: `feat/S14P31C201-XXX/cl-위치-동기화-polish`
- [ ] Unity `6000.3.13f1` 설치 + 본 프로젝트 열기
- [ ] Unity Cloud 계정 `gimhoein` 로 로그인 — Project ID `a15aff52-5b9b-4b49-9552-c43dc3a62c69` 자동 인식 확인
- [ ] MPPM 설치 (`Window → Package Manager → Unity Registry → Multiplayer Play Mode`)
- [ ] `Test_Network_AD.unity` 열어서 Play → 호스트 클릭 → 6자리 코드 발급 확인 (이게 안 되면 환경 문제)

### 3.3 작업 범위 (점진적 확대)

**Day 1 — Stage A 까지만**:
- cl026 plan §4 Step 1~4 코드/설정 변경 (TickRate=30 명시, Position Threshold 0.01, Half Float ON 등)
- Stage A 시나리오 4개 (A1~A4) 통과 — 로컬 MPPM 무지연
- 결과를 cl026 plan "튜닝 결과 기록" 표에 채우기

이 시점에서 **PR 올려도 됨** — Stage A 만으로도 단독 가치 있음. PR 설명에 "Stage A 까지 완료, B/C 는 후속" 명시.

**Day 2~3 — Stage B/C**:
- UnityTransport Network Simulator 활용해서 50ms 지연 / 100ms+5%loss 환경 구성
- Stage B/C 시나리오 통과
- 통과 못하면 파라미터 재조정 → 결과 기록

### 3.4 PR 조건

- [ ] cl026 plan §5 Stage A 4개 시나리오 통과 로그/스크린샷
- [ ] cl026 plan "튜닝 결과 기록" 표 채워짐 (적어도 Stage A 행)
- [ ] Network Simulator 빌드 OFF 확인 (`UnityTransport.useNetworkSimulator = false`)
- [ ] PR 설명에: 어디까지 통과 / 어디 미수행

### 3.5 막힐 만한 곳 + 대처

| 증상 | 원인 후보 | 대처 |
|---|---|---|
| Remote 캐릭터 떨림 (jitter) | TDE CharacterMovement 가 Remote 측에서 zero velocity 매 프레임 적용 중 | cl026 plan §4 Step 3 검증 — `CharacterMovement.enabled` 가 비-owner 측에서 false 인지 확인. 아니면 PlayerMovementSync 에 disable 추가 |
| Stage A 통과인데 Stage B 에서 snap | 보간 부족 | NetworkTransform `Interpolate` ON 확인 / TickRate 60 으로 임시 올려서 비교 |
| MPPM 메뉴가 안 보임 | 패키지 설치 직후 Editor 재시작 안 함 | Editor 재시작 |
| 빌드 후 Cloud 연결 실패 | Cloud Project ID 빌드 시점 누락 | `Edit → Project Settings → Services` 재연결 후 재빌드 |

### 3.6 도움 요청 기준 (김회인 호출)

- 4시간 이상 같은 증상으로 막힘 → 핑
- 시그니처 안정성 보호 대상 클래스 (rules §3.2) 를 *수정해야 할 것 같음* → 반드시 핑 (다른 작업 깨짐)
- Unity Cloud 무료 한도 초과 메시지 → 핑

---

## 4. 친구 B — CL-030 작업 가이드 (있을 경우)

### 4.1 시작 전 필수 정독

1. [cl020_to_025_phase_ab_completion_20260503.md](cl020_to_025_phase_ab_completion_20260503.md)
2. [cl023_phase_b_handoff_20260502.md](../../client/docs/cl023_phase_b_handoff_20260502.md) — 환경 셋업
3. [networking-integration-rules.md](../../client/docs/commonness/networking-integration-rules.md)
4. **[cl030_join_connection_error_flow_plan.md](cl030_join_connection_error_flow_plan.md)** — 본인 작업
5. [SessionErrorPolicy.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Networking/Session/SessionErrorPolicy.cs), [SessionLifecycle.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Networking/Session/SessionLifecycle.cs) — 기존 에러 처리 코드

### 4.2 작업 범위

cl030 plan 의 *"실패 분류 (Taxonomy)"* 표 기준으로 단계별 timeout / 반-중복 가드 / 취소 흐름 추가. 본 CL 은 *기능 추가 아니라 안전망* 이므로 기존 happy path 동작 깨뜨리면 즉시 롤백.

### 4.3 PR 조건

cl030 plan 의 검증 시나리오 통과. 데모 5분 동안 *어떤 입력 시퀀스든* 시연자가 재시작 없이 다시 호스트/참가 가능.

### 4.4 막힐 만한 곳

- 단계별 timeout 추가 시 *기존 happy path 가 빨라서* timeout 안에 안 끝나는 경우 — timeout 값을 P95 + 여유 기준으로 잡기
- CancellationToken 전파 흐름 — async 함수 체인 끝까지 토큰 전달

---

## 5. 김회인 — CL-027 ~ CL-029 순차 진행

### 5.1 진입 순서

CL-027 → CL-028 → CL-029. 각 **별도 브랜치 + 별도 PR**.

### 5.2 의존 관계

```
CL-026 (위치 polish, 친구 A 진행)
   ↓ (독립)
CL-027 (방향·행동 sync) ← 김회인
   ↓
CL-028 (공격 판정 호스트 라우팅) ← 김회인
   ↓
CL-029 (다운/부활 sync) ← 김회인
```

CL-026 과 CL-027 은 **독립** — 동시 진행 가능. 단, CL-027 이 PlayerMovementSync 와 같은 prefab 의 자매 컴포넌트로 들어가므로 prefab 충돌 주의.

### 5.3 CL-027 시작점

행동 상태 broadcast 컴포넌트 (`KhiPlayerStateNetSync`) 신규 작성. PlayerMovementSync 와 *별도 GameObject 부착 가능* (cl022 plan 명시).

이게 들어가야:
- 비-owner 측에서 *상대의 공격/대시/패리 시각 효과 reproduce*
- PlayerMovementSync 의 `disableInputComponentsOnNonOwner` 가 disable 한 5개 컴포넌트의 *visual 만* 살아남게 보강

---

## 6. 충돌 회피 룰

여러 명이 같은 파일 만지면 머지 충돌. 다음 표로 사전 분리:

| 영역 | 담당 | 친구가 건드리면 |
|---|---|---|
| `Networking/Player/PlayerMovementSync.cs` | 김회인 (CL-027 추가 예정) | 친구 A 는 *읽기만*. 수정 금지 |
| `Networking/Player/KhiPlayerStateNetSync.cs` (신규) | 김회인 | — |
| `TestKhi_Net_AD.prefab` | 김회인 (CL-027 컴포넌트 추가) | 친구 A 가 NetworkTransform 인스펙터 값만 변경하는 건 OK. 새 컴포넌트 부착은 금지 |
| `Test_Network_AD.unity` | 친구 A (NetworkManager 인스펙터 튜닝) | 김회인은 *읽기만* |
| `NetworkConfigBinder.cs` (신규, CL-026) | 친구 A | — |
| `Networking/Session/*` | 친구 B (CL-030 timeout 보강) | 김회인은 *읽기만* |
| `_Project/Scripts/Runtime/TestKhi/Khi*Controller.cs` | 손대지 말 것 | 시그니처 보호 (rules §3.2) |

같은 prefab 을 두 명이 동시에 수정하면 *meta GUID 충돌* — 사전에 누가 언제 만질지 슬랙/MM 으로 합의 권장.

---

## 7. 슬랙/MM 채널에서 묻는 기준

**즉시 핑**:
- 시그니처 보호 대상 클래스 수정해야 할 듯
- Unity Cloud 한도 초과
- 머지 충돌 발생 시 (특히 prefab/씬)

**일일 stand-up 에서**:
- 진척 / 블로커 / 다음 단계
- "튜닝 결과 기록" 표 갱신 내역 (CL-026)

**4시간 룰**: 같은 증상으로 4시간 막히면 무조건 핑. 혼자 끙끙대지 말 것.

---

## 8. 본 doc 다음 갱신 시점

- 친구가 합류 결정되면 *담당 칸* 채우기
- CL-026 통과 시 → CL-027 시작 시점에 새 doc 작성 (`network_team_task_assignment_<YYYYMMDD>.md`)

---

## 9. 관련 문서 빠른 링크

### 본 시점 통합 보고
- [cl020_to_025_phase_ab_completion_20260503.md](cl020_to_025_phase_ab_completion_20260503.md)

### 환경/규칙
- [cl023_phase_b_handoff_20260502.md](../../client/docs/cl023_phase_b_handoff_20260502.md) — 환경 셋업
- [networking-integration-rules.md](../../client/docs/commonness/networking-integration-rules.md) — 시그니처 보호 규칙
- [agent-unity-safety-rules.md](../../client/docs/commonness/agent-unity-safety-rules.md) — Unity 파일 안전 규칙

### CL 별 plan
- [cl026_position_sync_polish_plan.md](cl026_position_sync_polish_plan.md)
- [cl027_direction_state_sync_plan.md](cl027_direction_state_sync_plan.md)
- [cl029_down_revive_sync_plan.md](cl029_down_revive_sync_plan.md)
- [cl030_join_connection_error_flow_plan.md](cl030_join_connection_error_flow_plan.md)

### 상위 정책
- `docs/04_multiplayer.md` — 멀티 규칙 (호스트 권위 모델)
- `docs/14_client_jira_story_backlog.md` — Jira 백로그

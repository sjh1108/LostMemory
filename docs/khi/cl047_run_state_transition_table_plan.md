# CL-047 런 상태 전이 표 정리 — 계획

소속 Epic: **Epic D. 런 진행 / 방·적·보스** (백로그 1단계 MVP)

## Context

다른 commonness docs 가 이미 *RunState / RunManager 존재*를 전제로 적혀 있으나, 실제 명세도 코드도 **비어있는 상태**:

- `docs/commonness/bootstrap-scene-and-initialization-flow.md` — RunManager 가 lobby→stage 전환에서 "런 상태"를 만든다고 적혀있음
- `docs/commonness/project-structure-and-namespace.md` — `RunState.cs` 파일 경로를 *예시*로 들고 있음

CL-047 = 그 빈 명세를 **표(설계 문서)로 채우는 작업**. 짝 티켓 **CL-048** 이 본 표 기반으로 RunStateMachine 코드를 만든다.

본 CL 의 산출물은 **markdown 1 개**. 코드 변경 없음.

## 결정 사항

1. **CL-047 단독** — CL-048 (코드 구현) 은 다음 티켓으로 분리
2. **솔로 우선 + 멀티 후크 미리 박기** — 표 자체에 `Authority` / `Sync?` 메타 컬럼 추가, 별도 *멀티 확장 후크* 섹션은 자리표시만. NGO 도입 시 *재작성 X, 확장만*

## 결과물

`docs/khi/cl047_run_state_transition_table_plan.md` (본 문서)

- 본 문서가 CL-047 의 산출물 (design doc).
- 짝 티켓 CL-048 작성자가 본 문서를 보고 RunStateMachine 코드를 도출.
- 표의 *완전한 행 채움* (모든 (From, 트리거) 조합 enumerate, 멀티 후크 자리표시 채움 등) 은 본 문서 안에서 진행. 아래 표는 *구조 예시*.

## 표 구조 (5 섹션)

### 섹션 1 — 런 상태 enum 정의

| 상태 | 의미 | Authority |
|---|---|---|
| `None` | 런 시작 전 / 메인 메뉴 | Local |
| `Initializing` | 시드 결정 + DA Build 진행 중 | Host |
| `InRun_Combat` | 일반 전투방 진행 중 | Host |
| `InRun_Bridge` | 통로 / 브릿지 모듈 진행 중 | Host |
| `InRun_Boss` | 보스방 진행 중 | Host |
| `RunCleared` | 보스 처치 → 런 성공 | Host |
| `RunFailed` | 모든 멤버 사망 등 실패 조건 | Host |
| `Resulting` | 결과 화면 표시 중 | Local |

> 상태 이름 / 분리 단위는 implementation 단계에서 확정. `InRun_Bridge` 가 *별도 상태로 가치 있는지* vs `InRun_Combat` 의 sub-mode 인지 검토 필요. RoomData.RoomType 와 정합 우선.

### 섹션 2 — 전이 표 (행 단위)

각 행 컬럼:

| From | To | 트리거 (관찰 가능 이벤트) | 효과 / 부수효과 | Authority | Sync? |
|---|---|---|---|---|---|

샘플 행 (실제 표는 모든 전이 망라):

| From | To | 트리거 | 효과 | Authority | Sync? |
|---|---|---|---|---|---|
| `None` | `Initializing` | 런 시작 버튼 (UI) | `DungeonRunBootstrap.BuildRun()` 호출 | Host | RPC |
| `Initializing` | `InRun_Combat` | DA build 완료 + 첫 module = combat | `OnSpawnedManagedObjects` → first room `BeginRoomEntry` | Host | RPC |
| `InRun_Combat` | `InRun_Combat` | 다음 방 module = combat 진입 | `RoomEntryZone.OnTriggerEnter2D` | Host | RPC |
| `InRun_*` | `InRun_Boss` | 다음 module 의 RoomData.RoomType = Boss | 동일 | Host | RPC |
| `InRun_Boss` | `RunCleared` | 보스방 RoomCleared 이벤트 | 결과 데이터 집계 시작 | Host | RPC |
| `InRun_*` | `RunFailed` | Player Defeated × 파티 전원 (멀티 시) | CL-014 부활 deadline 만료 시 | Host | RPC |
| `RunCleared` / `RunFailed` | `Resulting` | 일정 시간 후 또는 키 입력 | 결과 UI 활성 | Local | 없음 |
| `Resulting` | `None` | 결과 화면 닫기 (메인 메뉴) | scene 전환 | Local | 없음 |

> 완전한 행은 implementation 시 모든 *(상태×트리거)* 조합을 망라.

### 섹션 3 — 게이트 / 가드 / Invariant

표 본문 외 *부가 규칙*:

- `InRun_Boss → RunCleared`: FlowGraph End 노드의 boss_room module 이 *실제로 클리어* 되었을 때만 (조건 강화 = CL-049 영역)
- `InRun_* → RunFailed`: 솔로 = Player Defeated 즉시. 멀티 = 파티 전원 다운 + CL-014 부활 deadline 만료 (멀티 후크)
- `RunCleared` / `RunFailed` 도달 후 *재진입 불가* (transition lockout)
- `Initializing` 진입 후 *DA Build 실패* 시 → ? (예외 처리 정책 *명시적 미정* 으로 표기 → 후속 CL)

### 섹션 4 — 멀티 확장 후크 (자리표시)

다음 CL 에서 채울 *예약 슬롯*:

| 후보 상태 / 전이 | 의미 | 의존 CL |
|---|---|---|
| `WaitingForPartyReady` | 호스트가 모든 멤버 준비 신호 대기 | 네트워크 CL (CL-021~026 영역) |
| `MemberDown` (sub-state of InRun_*) | 한 멤버 다운, 나머지 진행 중 | CL-014 + 네트워크 CL |
| `PartyResynced` (transition gate) | 멤버 재접속 후 상태 보정 | 네트워크 CL |
| host migration 시 상태 인계 | host disconnect → new host promote | 네트워크 CL (장기) |

본 CL 은 이름과 1줄 의미만. 세부 명세 X.

### 섹션 5 — 후속 CL / 의존 CL 매핑

| CL | 본 표와의 관계 |
|---|---|
| **CL-048** (본 표의 짝) | RunStateMachine 클래스 + 상태 enum 코드화 |
| CL-014 (다운/부활) | `InRun_* → RunFailed` 전이 트리거 |
| CL-049 (보스방 진입 조건) | `InRun_Combat → InRun_Boss` 전이의 가드 강화 |
| CL-105 (DA·방 연동, 완료) | `Initializing → InRun_*` 전이의 *원인* (DungeonRunBootstrap) |
| CL-106 (유물 효과) | `InRun_*` 진입 시 RelicInventory 라이프사이클 hook |
| 네트워크 CL (CL-021~026 + 후속) | Authority/Sync 컬럼 *값 채우기* + 멀티 확장 후크 *세부 명세* |

## 핵심 파일 (작성 시 참고)

### 도메인 정보 출처
- `docs/14_client_jira_story_backlog.md:81` — CL-047/048 백로그 정의
- `docs/commonness/bootstrap-scene-and-initialization-flow.md` — RunManager 가정 라인
- `docs/commonness/project-structure-and-namespace.md:213` — RunState.cs 위치 예시

### 코드 (전이 트리거의 *실제 발생 지점*)
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs` — `BuildRun()`, `OnSpawnedManagedObjects` (Initializing → InRun 트리거)
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs` — `BeginRoomEntry`, RoomCleared (InRun 내부 전이)
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryZone.cs` — OnTriggerEnter2D (다음 방 진입 트리거)
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/AllEnemiesDefeatedTracker.cs` — 클리어 판정

### 패턴 참고 (이름 / 구조 스타일)
- `BossRoomDoorState` enum (Locked/UnlockedIdle/WaitingForParty/EntryConfirmed/Transitioning) — 이름 패턴 참고
- `KhiPlayerState` enum + `KhiPlayerStateAggregator` (CL-015) — aggregator 패턴 참고
- 위 두 파일 정확 위치는 작성 시 grep — `grep -r "enum BossRoomDoorState\|enum KhiPlayerState"`

## 작성 순서

1. 빈 markdown 파일 생성 + 5 섹션 헤더 골격
2. 섹션 1 (상태 enum) 행 채우기 — *우선 솔로 기준* 으로
3. 섹션 2 (전이 표) 모든 *(From, 트리거)* 조합 enumerate → To 와 효과 채우기
4. 섹션 3 (가드/invariant) 작성 — 표 본문에 안 들어가는 *부가 규칙*
5. 섹션 4 (멀티 후크) 자리표시 — 1줄 의미만
6. 섹션 5 (후속 CL 매핑) 검토 — 누락 의존 없는지
7. 자기 검증 (아래 *검증* 섹션 체크리스트)
8. (옵션) 다른 commonness docs 에 cross-link 추가 검토 — 별도 작업 / commit

## 검증 (표의 self-consistency)

작성된 표가 *충분한지* 다음 체크리스트로 점검:

- [ ] 모든 상태에 *진입 전이* 최소 하나 정의 (orphan state 없음)
- [ ] 모든 상태에 *이탈 전이* 정의 (`None` 제외)
- [ ] 모든 전이의 *트리거* 가 *관찰 가능 이벤트* (단순 "조건" 이 아닌 메서드 호출 / 콜백 / 입력)
- [ ] 모든 전이의 *효과* 가 *관찰 가능 부수효과* (다음 메서드 호출 / 이벤트 발행 / 데이터 변경)
- [ ] `Authority` 컬럼 모든 행에 값 있음 (Host / Local / Shared 중 하나)
- [ ] `Sync?` 컬럼 모든 행에 값 있음 (RPC / NetworkVariable / 없음 중 하나)
- [ ] 후속 CL 매핑이 backlog 와 일치 (CL-014, CL-048, CL-049, CL-105, CL-106 중 누락 없음)
- [ ] 멀티 확장 후크 섹션의 후보 상태들이 *솔로 표를 깨지 않음* (메인 표에 *추가만* 가능한 모양)

추가 검증 (선택):
- 짝 티켓 CL-048 작성자가 본 표만 보고 *RunStateMachine.cs* 구조를 도출할 수 있는지
- CL-014 / CL-049 작업자가 본 표에서 *자기 영역* 을 명확히 식별할 수 있는지

## 위험 / 결정 보류

1. **상태 분리 입도** — `InRun_Combat` / `InRun_Bridge` / `InRun_Boss` 분리가 적절한지 vs `InRun` + `currentRoomType` 으로 단일화하는 게 더 단순한지. 본 plan 은 분리안 추천. CL-048 구현 시 단일화로 변경 가능 (호환 깨지지 않음).
2. **Initializing 실패 처리** — DA Build 실패 시 전이 정책 미정. 본 표에 *명시적 미정* 으로 표기, 후속 CL 또는 본 표 v1.1 에서 채움.
3. **Resulting → None** vs **Resulting → Initializing** — "다시하기" 버튼이 메인메뉴 거치는지 / 바로 새 런 시작인지. UI 명세 영역 의존, 본 표에서는 *Resulting → None* 만 명세 + "다시하기 후속 검토" 메모.
4. **호스트 권위 가정** — 본 표는 *호스트 권위* 모델 가정. 메시 / P2P / 서버 권위 등 다른 모델 도입 시 Authority 컬럼 값 재해석 필요. 현재 NGO 가 호스트 권위 기본이라 안전.
5. **이름 컨벤션** — `RunState_None` / `RunState.None` / `None` 등 표기 통일. CL-048 시 enum 이름 확정 → 본 표 retrospective 정합.

---

## v1.1 Retrospective (CL-048 구현 시 결정)

본 spec v1 (분리안: `InRun_Combat / InRun_Bridge / InRun_Boss`) 작성 후, CL-048 ([cl048_run_state_machine_minimal_plan.md](cl048_run_state_machine_minimal_plan.md)) 구현 단계에서 다음 결정이 spec 에 영향.

### 1. 상태 단일화 — 위험 1번 해결

`InRun_Combat / InRun_Bridge / InRun_Boss` 분리 → **`InRun` + sub-info 로 단일화** 결정.

근거:
- `StageRoomType` enum 에 `Bridge` 가 존재하지 않음 (실제 값: `Unknown / Combat / Shop / Event / Boss`)
- 즉 Bridge 는 *데이터 모델에 없는 개념* — spec 의 방 종류 표기에 부정합
- 메인 상태 분리 시 곱집합 폭발 위험 (멀티 시 `InRun_Combat_MemberDown` 등)
- 외부 트리거 기반 storage 패턴에 단일화가 정합 (CL-048 머신 패턴 결정과 일관)

### 2. CL-048 확정 enum (6개)

```csharp
public enum RunState {
    None, Initializing, InRun, RunCleared, RunFailed, Resulting,
}
```

방 종류 정보 필요 시 `RoomClearedPayload.Data.RoomType` (`StageRoomType`) 으로 query.

### 3. v1 표와의 관계

본 spec 의 표 자체는 *디자인 의도* 기록으로서 v1 그대로 유지 (재작성 X). CL-048 구현 결정은 본 retrospective 섹션이 권위 있는 reference. 후속 CL 작업자는 본 retrospective 부터 보고 *단일 InRun 모델* 로 작업.

전이 표(섹션 2)의 `InRun_*` 행들은 v1.1 에서 다음으로 매핑:
- `InRun_Combat → InRun_Combat` → `InRun → InRun` (RoomEntered 시 sub-info 만 변경, 메인 상태 유지)
- `InRun_* → InRun_Boss` → `InRun → InRun` (sub-info 변경)
- `InRun_Boss → RunCleared` → `InRun → RunCleared` (가드: `currentRoomType == Boss`)
- `InRun_* → RunFailed` → `InRun → RunFailed`

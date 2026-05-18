# CL-015 플레이어 상태머신 최소 구조 정리 구현 기록 (진행 중)

작성일: 2026-04-23

대상 씬: `LostMemory/Assets/Scenes/test_khi.unity`

**상태**: 🟡 **부분 완료** — 프리팹/씬 누락으로 인한 Parry 미진입 이슈는 `KhiParryController`를 플레이어 프리팹 인스턴스에 부착하고 HitStun/Down/Aggregator의 `parryController` 참조를 연결해 해소. 체크 #7 통과. 체크 #8(실패 후딜 감쇠 피해)과 보너스(성공 0피해 + 0.12s 무적)은 오버레이 외 시각 피드백 부재로 확인 보류.

## 목적

CL-011~014로 각 컨트롤러(Parry/HitStun/Dash/Down/MeleeCombo)가 독립적으로
permits 캐시/복원하며 잘 작동 중이지만, **"지금 플레이어가 어떤 상태인가?"**
쿼리할 단일 진실 소스가 없었다. 후속 CL(CL-016 UI, CL-020대 네트워크,
PlayerCombatReporter)에서 상태 쿼리가 필요해지므로 이를 한 곳에서 제공하는
읽기 전용 집계기를 추가한다.

티켓 이름의 "최소 구조 정리" 원칙대로 기존 컨트롤러는 한 줄도 건드리지 않고,
신규 컴포넌트 3개만 추가하는 (a) 읽기 전용 집계 스코프로 진행.

## 설계 기준

- 기존 4개 Khi 컨트롤러 **무수정** (회귀 리스크 0)
- 행동 변경 0 (순수 구조 추가)
- 새 enum `KhiPlayerState` (Idle/Move/Attack/Dash/Parry/Hurt/Down/Defeated) 독립 정의
- TDE `CharacterConditions` enum 원본 안 건드림. Normal/Dead만 TDE 값 그대로 읽고,
  Down/Hurt는 Khi 컨트롤러에서 파생
- Parry Cooldown, HitStun PostHitIFrame은 입력 가능 시간이므로 Parry/Hurt 로
  집계하지 않고 Idle/Move 로 폴백
- OnGUI 디버그 오버레이는 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 가드로 릴리스 빌드 배제

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 접근 방식 | 읽기 전용 집계 (상태 보관 X, 매 프레임 재계산) | 기존 컨트롤러 무변경 |
| sub-state 처리 | Parry Cooldown, HitStun PostHitIFrame은 Parry/Hurt 아님 | 입력 가능한 시간은 "제어 불가" 아님 |
| Aggregator 실행 순서 | `[DefaultExecutionOrder(200)]` | 모든 Khi 컨트롤러(50~70) 이후 |
| Aggregator 배치 | 플레이어 루트 프리팹 | 다른 컨트롤러와 형제 |
| Overlay 배치 | 씬 레벨 오브젝트 | Defeated 시 플레이어 GameObject 비활성 대응 |
| Overlay 토글 키 | F3 | 기존 P/R 키와 충돌 없음 |
| Build 가드 | `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` | 릴리스 빌드 배제 |
| History depth | 10개 transition ring buffer | 빠른 전이 여유 |

## 추가/수정 파일

신규 파일 (3개):

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerState.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerStateAggregator.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerStateDebugOverlay.cs
```

수정 파일: **없음** (기존 컨트롤러 0바이트 변경)

문서:

```text
client/docs/khi/cl015_player_state_aggregator_implementation.md
```

Editor 수작업 (유저가 처리):

- `TestKhi_MinimalCharacter2D.prefab`에 `KhiPlayerStateAggregator` 컴포넌트 부착
- 씬에 빈 GameObject 생성 → `KhiPlayerStateDebugOverlay` 컴포넌트 부착

## 컴포넌트 구조

### KhiPlayerState enum

```text
Idle, Move, Attack, Dash, Parry, Hurt, Down, Defeated
```

단독 파일, 네임스페이스 `LostMemory.TestKhi`.

### KhiPlayerStateAggregator

매 프레임 4개 컨트롤러 + `Character.MovementState/ConditionState`를 쿼리해
현재 `KhiPlayerState`를 결정. 상태 자체는 보관하지 않고 쿼리 결과만 노출.

**우선순위 해결 규칙** (위에서 아래로 첫 match 채택):

| 순위 | 조건 | 결과 |
|---|---|---|
| 1 | `downController.IsDefeated` 또는 `ConditionState == Dead` | Defeated |
| 2 | `downController.IsDown` | Down |
| 3 | `hitStunController.IsStunned` (HitStun만, PostHitIFrame 제외) | Hurt |
| 4 | `parryController.IsParryWindowActive \|\| IsParryRecovering` (Cooldown 제외) | Parry |
| 5 | `dashController.IsDashing` | Dash |
| 6 | `meleeCombo.IsAttacking \|\| IsInAttackRecovery` | Attack |
| 7 | `MovementState.CurrentState` ∈ {Walking, Running, Jumping, Falling, Crawling, Crouching} | Move |
| 8 | 그 외 | Idle |

**공개 API**:

```text
KhiPlayerState CurrentState, PreviousState
float          TimeInCurrentState
event Action<KhiPlayerState, KhiPlayerState> StateChanged   // (prev, next)

float ParryRemaining, HurtRemaining, DownRemaining
KhiParryState RawParryState
KhiHitStunState RawHitStunState
KhiDownState RawDownState
bool RawIsAttacking, RawIsInAttackRecovery, RawIsDashing
CharacterStates.MovementStates RawMovementState
CharacterStates.CharacterConditions RawConditionState
```

### KhiPlayerStateDebugOverlay

- OnGUI 기반 화면 오버레이 (좌상단 260×240 박스)
- `[DefaultExecutionOrder(300)]`
- `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 전체 클래스 가드
- Aggregator를 씬에서 자동 탐색 (`FindFirstObjectByType` with `FindObjectsInactive.Include`)
- F3 키로 표시 토글, 기본 ON
- `StateChanged` 구독해 최근 10개 전이를 history 링 버퍼로 저장
- 구성: 현재 State + 서브스테이트 타이머 + Prev + TimeInCurrentState + Raw 값들 + History

## 주요 설계 결정

### 1. 왜 읽기 전용 집계인가

현재 체감 버그 없음 + "최소 구조 정리" 티켓 이름 → 리스크 있는 과격한 리팩터
(permits 제거, 상태머신 중심 재작성)는 YAGNI. UI/네트워크가 상태 쿼리를
필요로 할 때 단일 진실 소스만 있으면 충분하므로 읽기 전용 집계가 최소 비용 최대 효용.

### 2. TDE 재사용 vs 신규 enum

TDE `CharacterConditions.Stunned`를 `Hurt`에 매핑하는 것은 의미론적으로 어색
(Stunned는 일시 마비, Hurt는 피격 반응). `Down`은 TDE에 아예 없음.

해결: `Normal`과 `Dead`는 TDE 값 그대로 읽기만 하고, `Down`/`Hurt`는 프로젝트 전용
`KhiPlayerState` enum에서 별도 값으로 정의. 하이브리드 접근.

### 3. Parry Cooldown / HitStun PostHitIFrame 폴백

Parry Cooldown (0.45s)과 HitStun PostHitIFrame (0.15s)은 **입력 가능한 시간**.
플레이어가 이미 움직이고 공격도 하는데 UI가 "Parry" / "Hurt"를 표시하면
체감이 과장됨. 따라서:

- `Parry` 상태 = ParryWindow + FailureRecovery 만 (permits가 입력 차단하는 구간)
- `Hurt` 상태 = HitStun 만 (permits 차단)
- Cooldown / PostHitIFrame 에서는 `Idle` 또는 `Move`로 폴백
- 단 `Raw:` 섹션에는 실제 sub-state 이름이 그대로 표시되어 디버그 가능

이 "불일치"가 오버레이에서 시각적으로 확인 가능 → 설계 검증의 핵심 포인트.

### 4. OnGUI 선택 이유

`_Project/Scripts` 전체에 기존 OnGUI 오버레이 선례 없음 (빈 캔버스).

Canvas/UGUI 대비 OnGUI 장점:
- GameObject 없이 컴포넌트 하나로 완결 (씬 셋업 최소화)
- `#if` 가드로 릴리스 빌드에서 완전 제거 용이
- 디버그 전용이므로 UGUI의 배치/레이아웃 비용 불필요

단점: 성능 (디버그 한정이므로 수용), Text Mesh Pro 미사용 (폴리시 없음)

정식 UI (CL-016)는 Canvas/TMP로 따로 구현 예정.

### 5. Overlay가 씬 레벨에 있는 이유

`Health.Kill()`이 플레이어 GameObject를 비활성화 → 플레이어에 붙은 컴포넌트
Update/OnGUI 중단. CL-014에서 P키 폴링을 Bootstrap으로 옮긴 것과 같은 이유.

Overlay는 Defeated 상태일 때도 "No player" 메시지를 띄워야 하므로 씬 레벨 유지.
Aggregator는 플레이어 레벨 (다른 Khi 컨트롤러와 형제).

### 6. history 10개 ring buffer

HitStun → PostHitIFrame → Idle 같은 빠른 전이 체인에서 5개로는 금세 밀림.
10개면 평균적으로 1~2초 분량이라 검증에 충분.

## 검증 결과

### 자동 검증

```text
dotnet build LostMemory/Assembly-CSharp.csproj --no-restore
→ 기존 2개 deprecation 경고만, 신규 에러/경고 0
```

```text
git diff --check
→ 공백 이슈 없음
```

csproj(gitignore)에 신규 파일 3개 수동 추가. Unity 재실행 시 자동 재생성.

### 수동 검증 (플레이 테스트)

완료:

- [x] #1 가만히 서 있기 → `Idle` 표시, `t_in` 타이머 증가
- [x] #2 WASD 이동 → `Move`, Raw Cond=Normal, Move=Walking
- [x] #3 Space 대시 → `Dash`, Raw Dash=true
- [x] #4 대시 종료 → Idle/Move 복귀
- [x] #5 좌클릭 1타 → `Attack` 진입, 스윙+recovery 동안 유지
- [x] #6 3타 콤보 → 콤보 내내 `Attack`
- [x] #9 **트랩 피격 (일반) → `Hurt` (0.12s) → PostHitIFrame 중 `Idle/Move`** (Hurt 아님 ← 설계 의도대로 폴백 확인됨)
- [x] #10 치명 피격 → `Down`, 타이머 10초 카운트다운 Raw 표시
- [x] #11 R 키 → Down → Idle 전이, history 기록
- [x] #12 10초 미입력 → `Defeated` 표시 (Kill 직전 마지막 프레임)
- [x] #13 P 키 → 플레이어 재활성, Aggregator 재탐색되며 `Idle` 복귀
- [x] #14 F3 → 오버레이 표시 토글 정상
- [x] #15 오버레이 History에 최근 10개 전이가 시간 순으로 표시됨

추가 완료 (프리팹 부착 후 재검증):

- [x] #7 **우클릭 (빈 공간) 시 `Parry` 표시 → Cooldown은 `Idle/Move`** (폴백 확인)

보류 (시각 피드백 없어 판정 어려움, 후속 CL에서 재확인):

- [ ] #8 **패링 실패 (trap 맞기) → `Parry` (FailureRecovery) → Cooldown 중 Idle/Move**
      (감쇠 피해 20% 적용 여부 Health 수치 추적 또는 KhiParryFeedbackPresenter 연결 필요)
- [ ] 보너스: **패링 성공 시 `State=Parry` 1프레임 → Cooldown 폴백 + Health 변화 없음**

미완 (MVP 불필요):

- [ ] #16 릴리스 빌드 모드로 전환 시 Overlay 코드가 컴파일 제외되는지 — 실제 빌드 할 때 검증

### 실측 로그 샘플 (#9 성공 케이스)

```text
State : Hurt (0.12s)
Raw:
  HitStun : HitStun         ← 일치

[0.12s 경과]
State : Move                ← 이동 가능해졌음
Raw:
  HitStun : PostHitIFrame   ← 내부는 아직 IFrame (불일치 ← 설계 확인)

History:
  Idle -> Hurt @ 8.000
  Hurt -> Move @ 8.120      ← 0.12s 뒤 Hurt 탈출, PostHitIFrame은 Hurt 아님 증거
```

## 해결된 이슈 기록

### Parry 입력이 Parry 상태 진입으로 이어지지 않던 문제 (해소)

**원인**: 코드 회귀 아님. CL-012 시점에 `KhiParryController` 컴포넌트가 플레이어 프리팹 인스턴스에
부착되지 않은 채 커밋됨. 당시 수동 검증은 로컬에 부착된 상태에서 통과 후 씬 저장 누락.

**증거**: GUID `e91b5caa1ec5f7a428fe2e32f2b348d2`가 `test_khi.unity`와
`TestKhi_MinimalCharacter2D.prefab` 전부에서 0회 검색됨. 씬의 HitStun/Down/Aggregator 세 컴포넌트의
`parryController` 직렬화 필드도 모두 `fileID: 0` (null).

**조치**: Unity Editor에서 플레이어에 `KhiParryController` 부착, 4곳의 참조 연결 후 씬 저장.

## 남은 후속 작업

- `KhiParryFeedbackPresenter` 부착/연결 상태 점검. 현재 LineRenderer ring/플래시가 보이지 않아
  #8 감쇠 피해와 보너스 검증이 시각적으로 어려움. CL-018(패링 성공 전용 피드백) 혹은 별도 소규모
  티켓에서 처리.

## 후속 CL 연결

- **CL-016 카메라/UI**: `KhiPlayerState` enum + `aggregator.StateChanged` 이벤트 +
  sub-state remaining 값들을 정식 UI가 구독. 상태 아이콘, Parry 창 링, Down 타이머
  바, Hurt 플래시 연동.
- **CL-017 히트스톱/VFX**: 대부분 개별 컨트롤러 이벤트(HitStunStarted 등)로 이미
  연결 가능. Aggregator는 보조적 참조.
- **CL-020대 네트워크**: `KhiPlayerState` enum이 서버→클라 replicate 대상. MVP enum
  값 8개는 `byte` 직렬화 충분. `StateChanged` 발사 시점에 네트워크 브로드캐스트 가능.
- **PlayerCombatReporter**: 현재 각 컨트롤러의 개별 이벤트를 구독하도록 설계.
  Aggregator는 선택적 단일 진실 소스 역할. 둘 다 공존 가능.

## 관련 문서

- 구현 계획 (로컬): `C:\Users\AD\.claude\plans\humming-swinging-hickey.md`
- 직전 CL: `client/docs/khi/cl014_player_down_revive_implementation.md`
- 참조 CL-012 (파링 버그 원인 탐색용): `client/docs/khi/cl012_timing_parry_implementation.md`
- 클라1 마스터 플랜: `client/docs/khi/client1_tasks_master_plan.md` (CL-015 정의 L77)
- 전투 시스템 기획: `docs/03_combat_system.md`

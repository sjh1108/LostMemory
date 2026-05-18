# CL-014 플레이어 다운·부활 기본 흐름 구현 기록

작성일: 2026-04-23

대상 씬: `LostMemory/Assets/Scenes/test_khi.unity`

## 목적

CL-014의 목표는 플레이어 HP가 0에 도달했을 때 TDE의 기본 `Kill()` 경로로
바로 사망 처리하지 않고, 협동 플레이 기획에 맞게 `Down` 상태로 전이한 뒤
아군 상호작용 또는 타임아웃으로 Revive/Defeat를 결정하는 기반을 마련하는 것이다.

이번 구현은 솔로 MVP 씬에서 Down/Revive/Defeat 경로를 전부 시각 검증할 수
있게 하되, 네트워크 동기화와 UI는 후속 CL로 미룬다.

## 설계 기준

- HP 0 진입 시점에 `Kill()` 가로채기. TDE `Health.Damage()` 내부에서
  `OnHit` 발사 → `Kill()` 체크 순서이므로, `OnHit` 핸들러에서 HP를 양수로
  되돌리면 Kill이 자동 차단된다.
- Down 중에는 이동·공격·대시·패링 모두 차단 + 추가 피해 무적.
- Down 타이머 기본 10s, 만료 시 `Health.Kill()` 명시 호출로 Defeat.
- Revive 성공 시 `MaxHealth × 0.2` (최소 1)로 복귀 + 짧은 post-revive i-frame.
- Solo 처리: 기획 스펙은 "즉시 패배"지만 MVP 솔로 씬에서 Down 상태 자체를
  검증해야 하므로 Inspector enum 토글로 3가지 모드 제공.
- TDE `Health`, `CharacterMovement` 등 원본은 수정하지 않는다.
- CL-012 Parry, CL-013 HitStun과 permits 캐시/복원 패턴을 일관되게 재사용하되,
  충돌 race는 새 `ForceIdle` / `ForceExit` 훅으로 해결.
- 네트워크 동기화, UI, MMFeedbacks 연출, TDE `CharacterConditions.Down` 승격은
  범위 밖.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| Kill 가로채기 | `OnHit`에서 `health.SetHealth(downHealthFloor=1)` | Health.Damage 순서상 OnHit 뒤 Kill 체크 → 양수로 되돌리면 Kill 스킵 |
| Down 지속 시간 | 10s | 기획 원안 (`docs/03_combat_system.md` L53) |
| Revive HP | `Mathf.Max(1, MaxHealth × 0.2)` | 비율 기반. 캐릭터 성장에 자연스럽게 따라감 |
| Revive 인터랙션 | Tap 기본 (`holdDuration=0`) + SerializeField 파라미터 | CL-016 UI 진행도 훅 준비 (`holdDuration>0`이면 Zone이 진행도 트래킹) |
| Debug Revive 키 | `R` (Down 상태, `DownWithDebugRevive` 모드에서만) | 솔로 씬 테스트 편의 |
| Debug Respawn 키 | `P` (씬 레벨 Bootstrap에서 폴링) | Defeated 시 GameObject 비활성화 → 컨트롤러 Update 불가 → Bootstrap이 대신 폴링 |
| Solo 처리 | enum `KhiDownSoloBehavior { ImmediateDefeat, DownWithDebugRevive, DownNoRevive }` | MVP 기본 `DownWithDebugRevive`. 프로덕션은 `ImmediateDefeat` 전환 |
| Parry 충돌 처리 | `KhiParryController.ForceIdle()` 신설 + Down 진입 시 호출 | Parry FailureRecovery 중 lethal damage 시 permits 캐시 race 방지 |
| HitStun 충돌 처리 | `KhiHitStunController.ForceExit()` 신설 + Down 진입 시 호출 | HitStun 진행 중 치명타가 들어오면 HitStun을 정리하고 Down이 permits 소유 |
| HitStun 선행조건 추가 | `downController == null || !downController.IsDown && !downController.IsDefeated` | Down/Defeated 상태에선 HitStun 진입 금지 |
| Defeat 처리 | `Health.Kill()` 명시 호출 | TDE 기본 사망 플로우 (ConditionState=Dead, DeathMMFeedbacks, 비활성화) 재활용 |
| 실행 순서 | `KhiDownController [DefaultExecutionOrder(70)]` | HitStun(65) 이후 평가. OnHit 체인에서 HitStun이 선 실행되며 `CurrentHealth ≤ 0` 체크로 스킵되고 Down이 마지막 결정권 |

## 추가/수정된 파일

신규 파일:

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiReviveZone.cs
```

수정 파일:

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiHitStunController.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiSceneBootstrap.cs
```

문서:

```text
client/docs/khi/cl014_player_down_revive_implementation.md
```

Editor 수작업 (유저가 처리):

- `TestKhi_MinimalCharacter2D.prefab`에 `KhiDownController` 컴포넌트 부착
- (선택) `TestKhiSceneBootstrap` 인스펙터에서 `Create Revive Zone On Awake` 토글

## 컴포넌트 구조

### KhiDownController

- 역할: Down/Revive/Defeat 상태머신, Health.OnHit 가로채기, permits 토글, Kill 명시 호출, Debug Revive 훅.
- 상태: `Normal`, `Down`, `Defeated`.
- `[DefaultExecutionOrder(70)]` — HitStun(65) 다음 차례.
- Health.OnHit 구독 엔트리: 선행 조건 통과 시 Kill 차단 후 Down 진입.

전이:

```text
Normal → Down         : lethal OnHit + ResolveAllyContext() true
Normal → Defeated     : lethal OnHit + ResolveAllyContext() false (솔로 즉사 모드)
Down   → Normal       : ForceRevive (R 키 / Zone / TryBeginRevive)
Down   → Defeated     : 타이머 만료 또는 ForceDefeat
```

진입 선행 조건 (HandleHealthHit):

1. `_state == Normal`
2. `health != null`
3. `health.CurrentHealth <= 0` (치명타만, HitStun과 역할 분리)
4. `ResolveAllyContext()` — solo behavior + ally 탐지 (MVP는 enum만)

공개 API:

```text
KhiDownState CurrentState
bool IsDown
bool IsDefeated
float DownTimeRemaining / DownTimeElapsed / DownDuration / ReviveInteractionHoldDuration

event Action<float>      DownEntered            // duration
event Action<float>      DownTimerTicked        // remaining (0.1s throttle)
event Action<GameObject> ReviveStarted
event Action<float>      ReviveProgressChanged
event Action<GameObject> ReviveCompleted
event Action             DefeatedByTimeout
event Action             DefeatedSolo
event Action             DebugRespawned

bool TryBeginRevive(GameObject reviver)
void CancelRevive(GameObject reviver)
void ForceRevive(float healthOverride = -1f)
void ForceDefeat()
void DebugRespawn()
```

Down 진입 시 permits 토글 순서:

```text
1. parryController.ForceIdle()     // parry race 선제 정리
2. hitStun.ForceExit()              // stun 진행 중이면 종료
3. CachePermitsIfNeeded()
4. ApplyBlockingPermits()
   - handleWeapon.AbilityPermitted = false
   - dashController.PermitAbility(false)
   - meleeCombo.ExternalBlock = true
   - characterMovement.MovementForbidden = true
5. meleeCombo.AbortCurrentAttack()  // 진행 중 스윙 중단
6. health.DamageDisabled()          // Down 중 추가 피해 차단
7. Animator null-safe "Down" 트리거
8. _state = Down; _downEnterTime = Time.time
9. DownEntered?.Invoke(downDuration)
```

Revive/Defeat 경로는 각각 permits 복원 → HP 세팅/Kill 순서.

### TestKhiReviveZone

- 목적: MVP 솔로 씬에서 Down 플레이어 근접 + E 키 입력으로 Revive 경로 시각 검증.
- BoxCollider2D (trigger) + `_occupant` 추적 + Update에서 E 키 폴링.
- `holdDuration == 0` → 탭 즉시 Revive, `> 0` → 해당 시간만큼 누르면 진행도 누적 후 완료.
- 이벤트: `ReviveProgressChanged(float 0~1)`, `ReviveTriggered(KhiDownController)`.
- 솔로 씬에서 자기 자신이 Zone에 들어와 E 누르는 구조라 실전 의미는 약함.
  후속 CL에서 아군 기반으로 교체.

### KhiParryController (수정)

- 추가: `public void ForceIdle()` — Parry 상태를 즉시 Idle로 복귀시키고 permits 복원.
- Idempotent (이미 Idle이면 RestorePermits만 호출 후 return).
- 목적: CL-014 Down 진입 시 Parry가 permits를 쥐고 있는 상태(`FailureRecovery` 등)
  에서 race 없이 정리.

### KhiHitStunController (수정)

- 추가 필드: `[SerializeField] KhiDownController downController;` + Awake auto-resolve.
- `CanEnterHitStun()` 선행 조건 #5 추가: `downController.IsDown || downController.IsDefeated`
  면 HitStun 진입 차단.
- 추가 메서드: `public void ForceExit()` — HitStun을 Idle로 강제 복귀, permits 복원.
  Idempotent. Down 진입 시 KhiDownController가 호출.

### TestKhiSceneBootstrap (수정)

- 추가 SerializeField:
  - `createReviveZoneOnAwake`, `reviveZonePosition`, `reviveZoneSize`, `reviveZoneHoldDuration`
  - `enableDebugRespawnKey`, `debugRespawnKey = KeyCode.P`
- 추가 메서드:
  - `EnsureReviveZone()` — `EnsureDamageTrap()` 패턴 복제. BoxCollider2D(trigger) +
    `TestKhiReviveZone` 컴포넌트 런타임 생성. `SerializedFieldSet` 리플렉션 헬퍼로
    `holdDuration` 주입.
  - `Update()` — `Application.isPlaying && enableDebugRespawnKey` 시 P 키 감지.
    `FindObjectsByType<KhiDownController>(FindObjectsInactive.Include, ...)`로
    비활성 GameObject 포함 전체 탐색, `IsDefeated == true` 인 컨트롤러에 `DebugRespawn()` 호출.

## 주요 설계 결정

### 1. Kill 가로채기를 OnHit에서 수행

TDE `Health.Damage()` 는 순서상:

```text
SetHealth(CurrentHealth - damage)
OnHit?.Invoke()
...
if (CurrentHealth <= 0) { CurrentHealth = 0; Kill(); }
```

`OnHit` 핸들러에서 `health.SetHealth(1)` 로 되돌리면 마지막 조건문이 실패해
`Kill()` 이 자동으로 호출되지 않는다. 피해 소스(`TestKhiDamageTrap` 등) 수정 없이,
`Health` 원본 수정 없이, 단일 진실 소스로 가로채기가 가능하다.

참고: HitStun과 Down 모두 `OnHit` 을 구독하지만 실행 순서가 다르다.

- HitStun(65): 먼저 실행. `CurrentHealth > 0` 조건으로 스킵 (lethal은 건드리지 않음).
- Down(70): 나중 실행. `CurrentHealth <= 0` 조건으로 진입.

역할이 HP 양수/0 기준으로 깔끔하게 분리된다.

### 2. Solo 처리를 3-mode enum으로

기획 스펙은 "솔로 즉시 패배" 이지만, MVP 솔로 씬에서 Down 상태 자체(이동 차단,
타이머, 시각 마커, permits 토글)를 검증해야 한다. 프로덕션 로직에만 맞추면
솔로 씬에선 Down 경로를 절대 밟지 못한다.

```text
ImmediateDefeat      : 기획 스펙. 프로덕션 동작.
DownWithDebugRevive  : MVP 기본. R 키로 혼자 부활 가능.
DownNoRevive         : 실전 시뮬. Down 진입하지만 R 안 먹힘. 타임아웃 Defeat.
```

Inspector 토글만으로 세 가지 경로 전부 테스트 가능. CL-015 또는 네트워크 CL에서
ally 탐지 로직이 붙으면 `ResolveAllyContext()` 내부만 교체하면 됨.

### 3. P 키 폴링을 Bootstrap으로 이관 (구현 중 발견)

초기 설계에선 `KhiDownController.Update()` 안에서 P 키를 감지하려 했지만,
`Health.Kill()` 이 `DestroyOnDeath` 플래그로 GameObject를 비활성화하면
MonoBehaviour `Update()` 가 호출되지 않는다.

해결: 씬 레벨 `TestKhiSceneBootstrap` (항상 활성)에서 P 키를 폴링하고,
`FindObjectsByType<...>(FindObjectsInactive.Include, ...)` 로 비활성 GameObject
포함 전체 탐색 후 `IsDefeated == true` 인 컨트롤러에 `DebugRespawn()` 호출.
`DebugRespawn()` 첫 줄에서 `gameObject.SetActive(true)` 로 재활성화 → `Health.Revive()`
→ `SetHealth(Max)` 순서.

### 4. ForceIdle / ForceExit 외부 정리 훅

Permits 캐시/복원 패턴은 각 컨트롤러가 자기 permits를 소유한다고 전제한다.
여러 컨트롤러가 동시에 permits를 잡으면 복원 순서에 따라 잘못된 값으로 덮어씌워진다.

예: Parry `FailureRecovery` 중(permits locked) lethal damage 진입 시, Down이 현재
permits(locked=false)를 그대로 캐시하면 Revive 시 "locked=false"로 복원 — 버그.

해결:
- Down 진입 직전 `parryController.ForceIdle()` → Parry가 자기 캐시 복원 후 Idle 복귀
- Down 진입 직전 `hitStun.ForceExit()` → HitStun 정리 (stun 중에 치명타 들어온 경우 대비)
- 이후 Down이 깨끗한 "normal" permits 상태를 캐시

`ForceIdle`/`ForceExit` 모두 idempotent. Idle 상태에서 호출되면 `RestorePermits()` 만
실행하고 return (안전망).

### 5. TestKhiReviveZone의 역할 제한

솔로 씬에서 Zone은 "자기 자신이 Zone에 들어와 E 누르는" 구조라 기능적 의미는
약하다. 그러나 트리거 영역 + 상호작용 입력 + 진행도 이벤트 파이프라인을
선구현해 두면 CL-016 UI 연결과 후속 네트워크 CL의 아군 기반 구현 시 API
형태가 이미 고정되어 재작업이 줄어든다.

`holdDuration > 0` 경로는 실측되지 않았지만(MVP 기본 0), 구조는 이미 잡혀 있어
CL-016에서 UI 진행도 바 붙일 때 `ReviveProgressChanged` 이벤트만 구독하면 된다.

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

참고: 로컬 `Assembly-CSharp.csproj` 에 신규 파일 2개(`KhiDownController.cs`,
`TestKhiReviveZone.cs`)를 수동 추가한 뒤 빌드. Unity 재실행 시 자동 재생성되므로
저장소 영향 없음.

### 수동 검증 (플레이 테스트)

완료 (`DownWithDebugRevive` 기본 모드):

- [x] 트랩 위에서 HP 0 도달 → Down 진입, 캐릭터 정지
- [x] Down 중 WASD / 좌클릭 / Space / 우클릭 모두 무시됨
- [x] Down 중 트랩 위에 계속 있어도 HP 변화 없음 (Invulnerable)
- [x] Down 중 R 키 → `MaxHealth × 0.2` 로 복귀, 입력 재개, post-revive i-frame 동안 추가 피해 없음
- [x] 10s 미입력 → `DefeatedByTimeout` 로그 + Kill 연쇄 → DeathMMFeedbacks 재생
- [x] Defeated 상태에서 P 키 → 플레이어 재활성 + HP Max
- [x] `soloBehavior = ImmediateDefeat` 전환 후 HP 0 도달 → Down 건너뛰고 즉시 Kill

미완 (MVP 불필요, 후속 CL / 콘텐츠 붙을 때 회귀 검증):

- [ ] HitStun 진행 중 치명타 → HitStun 스킵 + Down 정상 진입 (타이밍 맞추기 까다로움, 코드 리뷰로 대체)
- [ ] Parry `FailureRecovery` 중 치명타 → `ForceIdle` 후 Down 진입, 복귀 시 permits 정상 (타이밍 까다로움)
- [ ] `TestKhiReviveZone` 활성화 시 Zone + E 키 경로 (솔로 씬 의미 제한, 후속 CL에서 아군 기반 재검증)
- [ ] `DownTimerTicked` 이벤트 로그 실측 (CL-016 UI 붙을 때 자연 검증)

## 후속 CL 연결

- **CL-015 상태머신 정리**: TDE `CharacterStates.CharacterConditions` 에 `Down` 추가 →
  `_character.ConditionState.ChangeState(Down)` 으로 승격. 현재 permits 조합을
  condition-state 기반으로 통합 가능. `KhiDashController.HandleInput` 의 `Normal` 체크도
  함께 정비.
- **CL-016 카메라 / UI**: `DownTimerTicked`, `ReviveProgressChanged`, `DefeatedBy*`
  구독 → 타이머 바, 화면 오버레이, Revive 진행도 UI.
- **CL-017 히트스톱 / VFX**: `DownEntered`, `ReviveCompleted` 구독 → MMFeedbacks,
  카메라 흔들림, 파티클.
- **CL-020대 네트워크**: `ResolveAllyContext()` 를 `Physics2D.OverlapCircleNonAlloc` +
  Player 레이어 기반 또는 NetworkedPlayers 컬렉션으로 교체, `TryBeginRevive/ForceRevive`
  를 RPC 래핑, 서버 권위로 Down/Revive 결정.
- **PlayerCombatReporter**: `DownEntered`, `ReviveCompleted`, `DefeatedBy*` 를
  `Player.Down`, `Player.Revive`, `Player.Defeat` 글로벌 이벤트로 승격
  (`docs/commonness/global-event-keys-and-hook-points.md`).

## 관련 문서

- 구현 계획 (로컬): `C:\Users\AD\.claude\plans\humming-swinging-hickey.md`
- 직전 CL: `client/docs/khi/cl013_player_hit_stun_implementation.md`
- 전투 시스템 기획: `docs/03_combat_system.md` (다운/부활 L50-54)
- 멀티플레이 기획: `docs/04_multiplayer.md` (다운/전멸 규칙 L29-32)
- 클라1 마스터 플랜: `client/docs/khi/client1_tasks_master_plan.md` (CL-014 정의 L76)

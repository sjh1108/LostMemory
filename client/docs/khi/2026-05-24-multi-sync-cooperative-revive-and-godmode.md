# 2026-05-24 멀티플레이 sync 마무리 + 협력 부활 UI + 시연 디버그

> 시연 직전 작업 일지. 부활/디버그/UI 관련 .cs 만 변경 (prefab 0 수정). NGO join 안정성 우선.
> Plan 문서 참조: `2026-05-24-guest-revive-serverrpc-plan.md` (시작 의도)

## 작업 범위 한 줄 요약

1. **게스트→호스트 협력 부활** ServerRpc 위임 + 거리 재검증 (호스트 권위 ForceRevive)
2. **mirror 측 KhiDownController._downEnterTime sync** — mirror가 자체 timer로 즉시 Defeated 가던 버그
3. **부활 애니메이션 mirror sync** — `_syncedDownState` Down→Normal 시 Revive trigger + Rebind fallback
4. **협력 부활 차징바 UI** (`KhiCoopReviveBarView`) — 동적 spawn, prefab 0 수정, host/guest 양쪽 sync
5. **F6 God Mode 토글** — 호스트/게스트 누가 누르든 양쪽 무적 (게임 흐름 테스트용)
6. **F5 자살 키 (이전 작업) + 무적 호환** — Invulnerable 임시 해제 후 Damage → 복원
7. **`RenaBossSpellCombatController.cs` + `Town_Preview.unity` merge conflict** 해결

## 변경 파일

| 파일 | 종류 | 핵심 |
|---|---|---|
| `Networking/Player/PlayerMovementSync.cs` | 수정 | `[ServerRpc] RequestCooperativeReviveServerRpc` + `SubmitCoopReviveProgressServerRpc` |
| `Networking/Player/PlayerHealthSync.cs` | 수정 | `[ClientRpc] BroadcastCoopReviveProgressClientRpc` + `ApplyDownStateFromNetwork` 호출 + Revive 애니 sync + F6 GodMode |
| `TestKhi/KhiDownController.cs` | 수정 | host/guest 분기 ForceRevive, `_downEnterTime` mirror set, 차징바 auto-spawn, F5 자살 키 |
| `TestKhi/KhiCoopReviveBarView.cs` | **신규** | procedural sprite 차징바 (KhiStaffChargeBarView 패턴) |
| `TestKhi/KhiParryHealth.cs` | 수정 | F6 GodMode 진입 가드 (TDE Invulnerable 우회 안전망) |

Prefab/scene 수정 0. asset 0 (procedural sprite). NGO `GlobalObjectIdHash` 변화 0.

---

## 1. 게스트→호스트 협력 부활 (server-authoritative)

### 문제
NGO server-authoritative 한계로 게스트가 R hold 부활 차징 완료해도 `target.ForceRevive()` 직접 호출이 게스트 화면만 반영. host 의 `PlayerHealthSync._syncedHealth` NetworkVariable 까지 도달 못 함.

### 해결 — PlayerMovementSync.cs
```csharp
[ServerRpc]
public void RequestCooperativeReviveServerRpc(ulong targetNetObjId)
```
호스트 측 6단계 검증:
1. `SpawnedObjects.TryGetValue` 로 target 해결
2. `targetNo.IsPlayerObject` 확인
3. self 부활 차단 (`NetworkObjectId == targetNetObjId`)
4. 거리 재검증 (client 1.5m × 2 = 3.0m tolerance, anti-cheat + lag)
5. `KhiDownController.IsDown` 확인
6. host 권위로 `dc.ForceRevive()` → NetworkVariable sync → 모든 client OK

### KhiDownController.cs 완료 분기
```csharp
if (nm != null && nm.IsServer)
{
    target.ForceRevive();  // host 직접
}
else
{
    reviverSync.RequestCooperativeReviveServerRpc(targetNo.NetworkObjectId);  // 게스트 위임
}
```

---

## 2. mirror 측 `_downEnterTime` sync (자체 timer 즉시 Defeated 버그)

### 증상
호스트 F5 자살 → host Down → mirror도 Down state sync → **mirror가 1~3초 만에 자체적으로 Defeated 로 전환**. 게스트가 R hold 시작하기 전에 이미 `IsDown=false` → target=NULL.

### 근본 원인
`ApplyDownStateFromNetwork(KhiDownState.Down)` 가 `_state` 만 set, **`_downEnterTime` 은 default 0**. 그 후 `TickDown()` 이:
```csharp
remaining = downDuration - (Time.time - _downEnterTime) = 30 - Time.time
```
Time.time 이 27 같으면 remaining=3 → 3초 뒤 `EnterDefeatedByTimeout` → mirror 즉사.

### 해결 — KhiDownController.ApplyDownStateFromNetwork
```csharp
if (newState == KhiDownState.Down)
{
    _downEnterTime = Time.time;
    _nextTickEventTime = Time.time;
}
```
host의 정확한 down 시점은 RTT 만큼 어긋나지만 30초 timeout 안에선 무시 가능.

---

## 3. 부활 애니메이션 mirror sync + Animator stuck fallback

### 증상
호스트 부활 → 게스트 화면 호스트 mirror가 Animator "Down" state 에서 안 빠져나옴. `_state=Normal` 은 sync 됐는데 시각만 stuck.

### 원인
- 호스트 본인 측: `KhiDownController.CompleteRevive` 가 `SetTrigger("Revive")` + `EnsureNotStuckInDownAnimationCoroutine` (0.3초 후 Rebind) fallback 자체 처리
- mirror 측: `_syncedDownState` Down→Normal OnValueChanged 핸들러 `ClearDownVisualsOnClient` 가 **컴포넌트 reenable만 하고 Animator trigger 안 쏨**

### 해결 — PlayerHealthSync.ClearDownVisualsOnClient
1. `ResetTrigger("Down")` + `ResetTrigger("Death")` + `SetTrigger("Revive")` 발동
2. `EnsureMirrorRevivedNotStuckCoroutine` 추가 — 0.4초 후 여전히 Down/Dead state면 `Rebind` + `Play(0)` 강제 (Animator transition 누락 fallback)
3. Collider2D / Renderer reenable (Defeated 잔재 정리)
4. `reviveAnimatorTrigger` SerializeField 추가 ("Revive")

---

## 4. 협력 부활 차징바 UI (`KhiCoopReviveBarView`)

### 설계
- **procedural sprite** (흰 사각형 64×8px) — asset 추가 0
- KhiStaffChargeBarView 패턴 답습
- KhiDownController.Awake() 가 자식 GameObject 동적 생성 + view attach → **prefab 수정 0**

### 데이터 흐름 (host/guest 모두 sync)
```
살리는 사람 KhiDownController.TickCooperativeReviveSearch
  ├─ target.ReviveProgressChanged.Invoke(ratio)   ← 자기 화면 mirror 즉시 fill
  └─ throttle 0.1s 또는 Δ≥0.05 시 broadcast:
        ├─ host:  target.PlayerHealthSync.BroadcastCoopReviveProgressClientRpc(ratio)
        └─ guest: reviverSync.SubmitCoopReviveProgressServerRpc(targetNoId, ratio)
                  → host가 target.PlayerHealthSync.BroadcastCoopReviveProgressClientRpc(ratio)
              ↓ 모든 client 받음
        target.KhiDownController.ApplyCoopReviveProgressFromNetwork(ratio)
              ↓
        _coopReviveBar.SetProgress(ratio)
```

### Inspector 노출 (KhiDownController)
- `autoSpawnCoopReviveBar` (bool, true)
- `coopReviveBarLocalOffset` (Vector3) — 머리 위 offset
- `coopReviveBarWidth` / `coopReviveBarHeight` — scale (sprite raw 1m × 0.125m 기준)
- `coopReviveBarBackgroundColor` / `coopReviveBarFillColor` / `coopReviveBarReadyColor`
- `coopReviveBarSortingLayer` (string) — 빈 문자열 + autoFollow=true 면 캐릭터 메인 SR layer 따라감
- `coopReviveBarSortingOrder` (int) — autoFollow 시 offset, 아니면 절대값
- `coopReviveBarAutoFollowCharacterSortingLayer` (bool)

### 핵심 버그 fix
1. **bg/fill localPosition mismatch** — pivot (0, 0.5) sprite 라 둘 다 `-barWidth*0.5` 로 시작점 align 필요. fill 이 bar 영역 바깥에 그려지던 버그
2. **_fillBaseScale 미초기화** — AddComponent 가 Awake 즉시 호출하는데 sprite 필드 wireup 전이라 `_fillBaseScale = Vector3.zero`. `EnsureInitialized()` factory 메서드로 명시 호출
3. **라이브 튜닝** — KhiDownController.LateUpdate 가 매 프레임 SetColors push (OnValidate 신뢰성 문제 우회)

### KhiCoopReviveBarView 외부 API
```csharp
public void SetProgress(float ratio01)       // 0=hide, 0~1=fill
public void SetColors(Color bg, Color fill, Color ready)
public void SetSorting(string layerName, int backgroundOrder)
public void EnsureInitialized()              // 동적 생성 후 명시 호출
public static KhiCoopReviveBarView CreateOnTransform(Transform parent, Vector3 localOffset, float width, float height)
```

---

## 5. F6 God Mode (게임 흐름 테스트용)

### 설계
- `PlayerHealthSync.GodModeActive` (public static bool)
- F6 누르면 토글 — 누구든 (host/guest) 누를 수 있고 server 측 모든 player.Invulnerable=true
- 매 프레임 `LateUpdate` (server only) 에서 enforce — `CompleteRevive` 가 `Invulnerable=false` set 해도 다음 프레임 복원
- `KhiParryHealth.Damage` 진입 가드 — `GodModeActive` 시 base.Damage 호출 자체 skip (TDE Invulnerable 우회 경로 차단 안전망)

### 흐름
```
호스트 F6  → 직접 ToggleGodModeForAllOnServer
게스트 F6  → RequestToggleGodModeServerRpc → host 가 동일 처리
       ↓
GodModeActive = !GodModeActive
       ↓
LateUpdate (server) 매 프레임 모든 _serverInstances.health.Invulnerable=true
       ↓
KhiParryHealth.Damage 진입 시 return (데미지 path 전체 skip)
```

### F5 자살 호환
`KhiDownController.IsForceDownPressed` 안에서 임시 해제:
```csharp
bool wasInvulnerable = health.Invulnerable;
if (wasInvulnerable) health.Invulnerable = false;
health.Damage(99999f, gameObject, 0f, 0f, Vector3.zero);
if (wasInvulnerable) health.Invulnerable = true;
```

### Release build 자동 제외
모든 디버그 키 코드는 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 가드. release 빌드 자동 stripping.

---

## 6. F5 자살 디버그 키 (이전 작업 보완)

- `KhiDownController.debugForceDownKey = KeyCode.F5` SerializeField
- New Input System + legacy Input fallback (`IsForceDownPressed()`)
- `health.Damage(99999f, ...)` 로 호출 → production 흐름 (Health.OnHit → HandleHealthHit → EnterDown) 거쳐 mirror 까지 sync 정상
- Editor / Dev build only

---

## 7. Merge conflict 해결 — `feat/...241` ← `preview`

### `RenaBossSpellCombatController.cs`
- **충돌 위치 1** (line 1103~1217)
  - HEAD: IceSweep row warning sync 함수 3개 추가 (`SpawnVisualOnlyIceSweepRowWarnings`, `RunVisualOnlyIceSweepRowWarnings`, `BroadcastIceSweepRowWarningsIfHost`)
  - preview: `CreateIceSweepWarningRenderer` 헬퍼 추출
  - **resolution**: 4개 함수 모두 유지 (의도 직교)

- **충돌 위치 2** (line 1258~1270)
  - HEAD: 단순 alpha 계산 + `GetSyncedTime()`
  - preview: 5개 시각 효과 변수 (`pulse`, `fastPulse`, `finalFlash`, `railExpand`, `railFade`) + `Time.time`
  - **resolution**: preview 의 풍부한 시각 효과 변수 유지 + `Time.time` → `GetSyncedTime()` 교체 → 시각 + sync 양쪽 다 살림

### `Town_Preview.unity`
- HEAD 측 변경 0, preview 측만 PrefabInstance modifications 추가:
  - `portraitSprite`
  - `iceSweepAreaSize.x = 28`, `.y = 16`
  - `iceSweepAreaCenterAnchor` (보스방 중앙 anchor)
  - `roomController` (clear)
- **resolution**: preview 그대로 — 잃는 것 0

---

## 검증 시나리오

### 기본 (Host → Guest)
1. Host + Guest 멀티 진입
2. Guest 피격 Down
3. Host R hold 2초 → 게스트 머리 위 차징바 차오름
4. 확인: Host 화면 / Guest 화면 모두 부활 + Animator 정상

### 신규 (Guest → Host) — 핵심
1. Host F5 자살 → 양쪽 화면 Host Down
2. Guest R hold 2초 (1.5m 내)
3. 확인:
   - Guest 콘솔: `Coop revive 완료 (guest) — ServerRpc 발사`
   - Host 콘솔: `Coop revive 승인` + `ForceRevive`
   - 양쪽 화면 Host 부활 (NetworkVariable sync)
   - **Animator stuck fallback** 작동 확인 (Rebind 로그)

### 차징바
- R hold 시 target 머리 위 시안색 fill 좌→우 차오름
- 완료 직전 (≥0.99) 민트색 펄스
- R release / 거리 이탈 시 즉시 hide
- 양쪽 화면에서 동일하게 보임

### F6 GodMode
- F6 토글 → 콘솔 `God Mode ON applied to N player(s)`
- 적에 맞아도 HitStun / Down 진입 안 함
- F5 자살은 여전히 작동 (임시 해제)
- 다시 F6 → OFF → 정상 데미지

### 자기 부활 차단 (anti-self-revive)
- Down 상태에서 R 눌러도 `TickCooperativeReviveSearch` 가 Normal 에서만 작동 → 무시
- host 측 ServerRpc 도 `NetworkObjectId == targetNetObjId` 검사로 거부

---

## 시연 후 follow-up

1. **진단 로그 정리** — `[CoopRevive] rHeld=true ...` 등 verbose 가드 또는 제거
2. **mirror 측 KhiDownController.Update 의 TickDown owner-only 가드** — 현재는 mirror 도 자체 Tick 하는데, `_downEnterTime` sync 로 우회. owner-only 로 제한하면 더 깔끔
3. **F6 무적 ON 시 UI 표시** — 콘솔 로그만으론 시연 중 상태 인지 어려움. OnGUI 우상단 "GOD MODE" 텍스트
4. **`coopReviveBarLocalOffset` Inspector 튜닝** — 캐릭터 sprite 마다 적절한 머리 위 위치 (Y 0.5~2.0)
5. **`Hero_Animator` 에 Revive transition 정식 wireup** — 현재는 mirror 측 Rebind fallback 의존. 부활 모션 자연스럽게 보이려면 Down → Idle transition (Revive trigger) 추가
6. **차징 효과음 / 시각 이펙트** — 차징 중 SFX + ready 도달 SFX
7. **PlayerHealthSync.LateUpdate 의 GodMode enforce static 호출 중복** — 인스턴스마다 호출되지만 static 이라 idempotent. 깔끔하게 별도 manager 로 분리 가능

---

## NGO 안정성 평가

| 항목 | 위험 |
|---|---|
| prefab GlobalObjectIdHash 변화 | **없음** (코드만, 동적 생성) |
| NetworkBehaviour 메서드 추가 | 낮음 (기존 RPC 추가 사례와 동일 패턴) |
| 컴파일 깨질 위험 | 낮음 (메서드 단일 추가) |
| 회귀 (호스트 부활 깨짐) | 낮음 (host 분기 그대로 `target.ForceRevive()`) |
| join 깨질 위험 | **매우 낮음** — 76개 prefab 일괄 변경 없음, .cs 5개 + scene 1개 (외부 anchor) |

---

## 핵심 인사이트

1. **NGO server-authoritative 우회 패턴** — game-level state 변경은 무조건 host 측 ForceRevive / 검증 → NetworkVariable sync. 게스트 직접 호출 = 자기 화면만 영향.
2. **Mirror 측 자체 timer 위험** — sync 받은 state 에 대해 timer 계산 시 RTT / 기준점 차이 주의. `_downEnterTime` 같은 시점 의존 값은 sync 진입 시 명시 set 필요.
3. **Animator transition 누락 대비** — `SetTrigger` 만 의존하지 말고 `Rebind` fallback. 멀티 측에선 양쪽 다 호출되어도 idempotent.
4. **procedural 동적 생성 패턴** — prefab 수정 위험 회피. NGO join 안정성 극대화. asset 추가 0 가능 (코드 한 줄로 sprite 생성).
5. **시연 디버그 키 격리** — `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 가드 + static frame guard 로 중복 입력 방지.

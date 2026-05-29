# 2026-05-24 — 협력 부활 (Cooperative Revive) 구현 계획

## Context

현재 R 키 부활 시스템은 **시연용 임시**:
- IsOwner 가드 제거 후 host R 한 번으로 모든 Down player 부활
- 자기 자신도 R 누르면 부활 가능

**사용자가 원하는 최종 동작**:
1. 자기 자신은 R 로 부활 **불가**
2. **살아있는 팀원**이 죽은 팀원 **근처(1.5m)** 로 가서
3. R 키를 **2초 차징** 하면
4. 죽은 팀원 부활
5. 차징 중 거리 벗어나거나 R 떼면 **취소**

Diablo / Vermintide 표준 협력 부활 패턴.

## 현재 상태

| 항목 | 현재 |
|---|---|
| 자기 R 부활 | 작동 (자기 측만, host sync 불완전) |
| host R = 모든 player 부활 | 작동 (IsOwner 가드 제거) |
| 협력 부활 (근처 hold) | 미구현 |

## 변경 방향

**`KhiDownController` 통합 (작업 구조 A)** — 단일 파일 변경, Unity editor 작업 0.

### 동작 변경

| Player state | R 키 처리 |
|---|---|
| `Normal` | 매 frame 근처 Down player 검색. 발견 + R hold → 차징 진행. 2초 완료 시 ForceRevive 호출. |
| `Down` | **R 무효** (자기 자신 부활 차단 — 사용자 요구) |
| `Defeated` | 무효 (이미 영구 사망) |

### 차징 파라미터 (SerializeField, 인스펙터 조정 가능)

- `cooperativeReviveDistance = 1.5f` (m)
- `cooperativeReviveChargeSeconds = 2.0f`
- `cooperativeReviveDetectionLayerMask = "Player"` (또는 자동 resolve)

### 차징 취소 조건

1. R 키 뗌 (release)
2. 살리는 player ↔ Down player 거리 > 1.5m
3. Down player 가 Defeated 진입 (시간 초과)

## 변경 파일

### 1. `Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs`

#### Update / TickDown 의 R 키 처리 변경

**기존**: `TickDown` 안에서 R 누름 시 자기 ForceRevive
**변경**: 두 분기

1. **Normal 상태 (자기 살아있음)** — 새 메서드 `TickCooperativeReviveSearch()`:
   - `Physics2D.OverlapCircleNonAlloc` 으로 1.5m 내 Down player 검색
   - 발견 시 그 player 의 `KhiDownController` reference 저장
   - R hold 감지 → 차징 진행 (`_coopReviveProgress += Time.deltaTime`)
   - 매 frame 거리 재검증 — 벗어나면 취소
   - R release 또는 거리 이탈 시 `_coopReviveProgress = 0`, target reference 해제
   - 차징 ≥ 2.0s 도달 → target 의 `ForceRevive(reviveHealthFraction × MaxHP)` 호출
   - target 의 `KhiDownController.TryBeginRevive(this.gameObject)` 사용 (기존 API)

2. **Down 상태** — R 키 처리 제거 (자기 부활 차단)

#### 새 필드/메서드

```csharp
[Header("Cooperative Revive")]
[SerializeField] private float cooperativeReviveDistance = 1.5f;
[SerializeField] private float cooperativeReviveChargeSeconds = 2.0f;
[SerializeField] private LayerMask cooperativeReviveLayerMask;

private float _coopReviveProgress;
private KhiDownController _coopReviveTarget;
private static readonly Collider2D[] _coopReviveBuffer = new Collider2D[8];

private void TickCooperativeReviveSearch(bool rHeld)
{
    // 1. 근처 Down player 검색
    // 2. target 갱신 (가장 가까운 거)
    // 3. rHeld 면 progress 누적, 거리 검증
    // 4. progress >= duration 시 ForceRevive
}
```

### 2. `Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs` — R 키 hold 감지

기존 `IsRevivePressed()` 가 `wasPressedThisFrame` (한 번 누름) 만 감지. 새 메서드 `IsReviveHeld()` 추가:

```csharp
private bool IsReviveHeld()
{
#if ENABLE_INPUT_SYSTEM
    var kb = Keyboard.current;
    if (kb != null && kb.rKey.isPressed) return true;
#endif
    try { if (Input.GetKey(debugReviveKey)) return true; }
    catch { }
    return false;
}
```

기존 `IsRevivePressed()` 는 그대로 두되 호출 안 함 (자기 부활 차단).

## 재사용 utility

| 위치 | 용도 |
|---|---|
| `KhiDownController.TryBeginRevive(GameObject reviver)` | 부활 시작 API (기존) |
| `KhiDownController.ForceRevive(float healthOverride)` | 즉시 부활 (기존) |
| `Physics2D.OverlapCircleNonAlloc` (KhiArrowProjectile homing 패턴) | 근처 player 검색 |
| `LocalPlayerResolver` | 자기 player 식별 (참고) |
| `KhiStaffChargeBarView` | progress bar UI 패턴 (시연 후 적용) |

## 멀티 환경 sync 전략 (1차: 시연용)

### Server-authoritative 한계 활용

- **Host 가 부활시키면**: host 측 `ForceRevive` → `Health.SetHealth` → PlayerHealthSync NetworkVariable write → 모든 client sync ✓
- **게스트가 게스트를 부활시키면**: 게스트 측 `ForceRevive` 자기 측만 효과. host NetworkVariable 갱신 안 됨 → 다른 client 화면 sync 불완전 (이전 본인 R 부활과 동일 한계)

→ 시연 직전엔 **"host 가 부활시키는 게 가장 안정"** 안내. 추가 작업 없이 동작.

### 2차 (시연 후) — ServerRpc 위임

`PlayerMovementSync` 또는 `KhiDownController` 에 `RequestCooperativeReviveServerRpc(ulong targetNetObjId)` 추가:
- 게스트 측 charge 완료 → ServerRpc 발사 → host 가 권위로 target 의 ForceRevive 호출
- NetworkVariable sync 정상 → 모든 client 화면 부활

⚠️ 이전 RPC 추가가 NGO join 깨뜨린 적 있음. **시연 후 안전한 시점에 진행**.

## UI (시연 후 작업)

`KhiStaffChargeBarView` 패턴으로 살리는 player 머리 위 progress bar:
- backgroundSprite (회색)
- fillSprite (초록색, progress 0~1 따라 scale.x lerp)
- KhiDownController 의 `ReviveProgressChanged` 이벤트 hookup

시연 1차에선 UI 없음. 키 입력만으로 작동. 시연 후 추가.

## 검증 시나리오

### 기본 동작
1. Host + Guest 멀티 진입
2. Guest 가 적에게 맞아 Down (HP 0)
3. **Host 가 Guest 옆 1.5m 거리로 이동**
4. **Host R 키 누르고 있기 (2초)**
5. 확인:
   - [ ] Host 자기 화면: Guest mirror 부활 (애니메이션 + HP 복구)
   - [ ] Guest 자기 화면: 자기 player 부활 (host NetworkVariable sync 로)

### 거리 검증
1. Host R hold 시작 → 차징 진행 중
2. **1초 후 host 가 멀리 이동** (1.5m 초과)
3. 확인:
   - [ ] 차징 즉시 취소 (progress = 0)
   - [ ] 다시 가까이 와서 R hold 하면 처음부터 다시 차징

### 자기 자신 부활 차단
1. Host 가 Down 상태
2. **Host R 누름**
3. 확인:
   - [ ] 부활 안 됨 (자기 자신 부활 차단)
   - [ ] 다른 player 가 가까이 와서 R hold 해야 부활

### 콘솔 로그 확인
- `[KhiDownController] R 키 감지 — state=Normal ...` (살리는 측)
- `[KhiDownController] Cooperative revive started target=<player>` (차징 시작)
- `[KhiDown] Revive completed by <reviver>` (부활 완료)

## Unity Editor 작업

**없음** — 코드 변경만으로 동작. `cooperativeReviveDistance` / `cooperativeReviveChargeSeconds` / `cooperativeReviveLayerMask` SerializeField 가 player prefab 의 KhiDownController 컴포넌트에 자동 추가됨. 인스펙터에서 값 조정 가능.

`cooperativeReviveLayerMask` 가 비어있으면 자동 fallback — `Player` layer name 으로 검색 (또는 모든 KhiDownController GameObject 검색).

## 한계 / Follow-up

### 1차 (이번 구현)
- Host 가 부활시키면 정상 sync
- 게스트 → 게스트 부활은 sync 불완전

### 2차 (시연 후)
- ServerRpc 위임 — 모든 케이스 정확 sync
- Progress bar UI (KhiStaffChargeBarView 패턴)
- 차징 효과음 / 시각 이펙트 (TestKhiReviveZone 의 sound 패턴 활용)

## 위험 요소

- **새 RPC 안 추가** — NGO join 깨질 위험 0
- 코드 단일 파일 변경 (KhiDownController.cs) — 다른 시스템 영향 없음
- `Physics2D.OverlapCircleNonAlloc` 매 frame 호출 — Normal 상태 모든 player 에서 작동. 4인이면 frame 당 4회. 부담 미미.
- `_coopReviveBuffer` static 사용 — race condition? 단일 frame 내 순차 호출이라 무해.

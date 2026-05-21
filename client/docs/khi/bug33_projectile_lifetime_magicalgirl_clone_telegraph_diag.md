# 3건 묶음 — 투사체 lifetime / 마법소녀 검정 박스 / 몬스터 장판 진단 추가

## Context

플레이테스트에서 확인된 3개 이슈:

1. **투사체 안 사라짐 (멀티만)**: host 화면에서 host 본인 투사체는 정상 사라지지만 *게스트 화면에서 host 투사체는 maxLifetime 까지 비행* → 게스트에 "안 사라짐" 으로 보임. 역도 성립. 원인: `KhiArrowProjectile.SetVisualOnly` 가 collider 끄고 `OnTriggerEnter2D` 가 `_visualOnly` 무조건 return → 적과 충돌 자체 안 됨 → maxLifetime 까지 비행.

2. **마법소녀 visual clone 검정 박스 (host 화면)**: 게스트 측 마법소녀 본체는 정상이나 host 화면의 visual clone 이 *검정 사각형* 으로 표시. 우리가 만든 `SpawnVisualOnlyClone` 이 AI 본체의 `ApplyVisualAppearance` 와 *동일하지 않음*: catalog 성공 시 `Color.white` 미설정 + `transform.localScale` 미통일 → 다른 시각.

3. **몬스터 장판 여전히 안 보임**: APPLY 메뉴로 16개 prefab 에 `MonsterAttackBroadcast` yaml 부착 확인 (검증 완료). 모든 코드 path 정상 — broadcast 호출, IsServer 가드, ClientRpc, `AttackTelegraph2DView.Show`, `Foreground` sorting layer 존재. 그런데도 안 보임 → ClientRpc 도착 여부 또는 NetworkObject spawn race 의 진단 추가 필요.

목표: 한 사이클에 3개 — 투사체 + 마법소녀 *픽스*, 몬스터 장판 *진단 로그* (다음 플레이에서 root cause 확정).

---

## 작업 1 — 투사체 lifetime 픽스 (Option A: collider 살리고 damage 만 skip)

### Root cause

`Runtime/TestKhi/KhiArrowProjectile.cs:104~117 SetVisualOnly`:
```csharp
if (col != null) col.enabled = false;  // ← 콜라이더 끔
```

`OnTriggerEnter2D:163~167`:
```csharp
if (_visualOnly) return;  // ← 무조건 통과
```

= clone 은 적과 *물리 충돌 자체 안 됨* → `OnTriggerEnter2D` 미발화 → `PlayImpactAndDestroy` 호출 안 됨 → maxLifetime 까지 비행.

owner 측 진짜 projectile 은 정상 충돌 → 즉시 `Destroy(go, impactHoldDuration)` → 사용자 눈에 "정상 사라짐".

### 픽스

**파일**: `Runtime/TestKhi/KhiArrowProjectile.cs`

```csharp
// 1) SetVisualOnly — collider 끄지 말 것. homing 만 0.
public void SetVisualOnly(bool visualOnly) {
    _visualOnly = visualOnly;
    if (!visualOnly) return;
    // (제거) col.enabled = false;
    homingTurnRateDegPerSec = 0f;
    homingDetectionRadius = 0f;
}

// 2) OnTriggerEnter2D — visual-only 면 layer/Health 검사까지 통과 후 damage 만 skip + PlayImpactAndDestroy 정상 호출.
private void OnTriggerEnter2D(Collider2D other) {
    if (!_launched) return;
    // (제거) if (_visualOnly) return;

    int layer = other.gameObject.layer;
    string lname = LayerMask.LayerToName(layer);
    if (((1 << layer) & targetLayers.value) == 0) return;

    Health health = other.GetComponentInParent<Health>();
    if (health == null) return;
    if (_attacker != null && IsOwnedByAttacker(health, _attacker)) return;
    if (IsPlayerTarget(health)) return;
    if (!health.CanTakeDamageThisFrame()) return;

    // Bug #33 — visual-only clone 은 damage 호출 skip (owner 측 진짜 projectile 만 데미지 권위).
    if (!_visualOnly) {
        health.Damage(_damage, _attacker, targetFlickerDuration, targetInvincibilityDuration, _direction);
        if (logProjectileEvents) Debug.Log($"[Projectile {name}] hit {other.name}(L:{lname}) → damage {_damage} APPLIED");
    } else {
        if (logProjectileEvents) Debug.Log($"[Projectile {name}] hit {other.name}(L:{lname}) → visual-only skip damage, destroy only");
    }

    if (destroyOnHit) {
        PlayImpactAndDestroy();  // 양쪽 동일 시각 destroy.
    }
}
```

결과: clone 도 적과 충돌 시 즉시 PlayImpactAndDestroy → owner 와 같은 시점에 사라짐. damage 는 owner 측만 처리 → double-hit 없음.

### KhiMeteor — 픽스 불필요

자체 시간 기반 `Sequence` 코루틴이 destroy 책임. `_visualOnly` 는 `ApplyDamage` 만 skip → 시각 일치 자동 보장. 변경 없음.

---

## 작업 2 — 마법소녀 visual clone 검정 박스 픽스

### Root cause

`Runtime/MagicalGirl/MagicalGirlSpawner.cs:401~458 SpawnVisualOnlyClone` 가 AI 본체의 `Awake + ApplyVisualAppearance` 와 *다음 3가지가 다름*:

| 항목 | AI 본체 (정상) | clone 코드 (검정 박스) |
|---|---|---|
| catalog 성공 시 `sr.color` | `Color.white` 명시 | *미설정* (Unity 기본값 = 흰색이지만 race 가능) |
| transform.localScale | 항상 `0.4f` (Awake 에서) | catalog 성공 시 *미설정* (= 1.0f, 4배 큼) |
| catalog null 진단 | (없음) | (없음 — 사용자가 어디서 깨졌는지 모름) |

추가로 `MagicalGirlVisualPalette.Get(visual)` 의 모든 색은 *밝은 RGB* (Blackhole 도 노란색). 즉 *fallback path 자체* 가 검정 아님 → 픽스는 catalog 접근 + clone-AI 시각 일치.

### 픽스

**파일**: `Runtime/MagicalGirl/MagicalGirlSpawner.cs`

```csharp
public void SpawnVisualOnlyClone(MagicalGirlVisual visual) {
    if (visual == MagicalGirlVisual.Default) return;

    var go = new GameObject($"MagicalGirl_{visual}_VisualClone");
    Transform anchorT = anchor != null ? anchor : transform;
    go.transform.position = anchorT.position;
    go.transform.localScale = new Vector3(0.4f, 0.4f, 1f);  // AI 본체와 통일 (catalog 성공 / fallback 무관)

    var sr = go.AddComponent<SpriteRenderer>();

    // Bug #34 진단 — catalog wiring 확인 (host-side spawner 의 prefab inspector 할당 검증).
    if (attackCatalog == null) {
        Debug.LogWarning($"[DiagMagicalGirl-Clone] attackCatalog NULL — host-side prefab 의 inspector catalog 할당 확인 필요. visual={visual}", this);
    }

    Sprite sprite = null;
    if (attackCatalog != null && attackCatalog.TryGet(visual, out var entry)) {
        sprite = entry.sprite;
        if (sprite == null) {
            Debug.LogWarning($"[DiagMagicalGirl-Clone] catalog entry.sprite NULL. visual={visual}", this);
        }
    }

    if (sprite != null) {
        sr.sprite = sprite;
        sr.color = Color.white;  // ← AI 본체와 통일. sprite 자체 색상 사용.
    } else {
        // fallback — AI.Awake 와 동일 (whiteTexture + tint).
        sr.sprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: Texture2D.whiteTexture.width);
        sr.color = MagicalGirlVisualPalette.Get(visual);
    }
    sr.sortingLayerName = "Foreground";
    sr.sortingOrder = 100;

    var follower = go.AddComponent<MagicalGirlFollower>();
    Vector2 off = GetFormationOffset(visual);
    follower.Init(anchorT, playerAim, off);
    follower.SmoothTime = followSmoothTime;
    follower.BobAmplitude = followBobAmplitude;
    follower.BobSpeed = followBobSpeed;

    if (_logSpawn) Debug.Log($"[MagicalGirl] visual-only clone spawned visual={visual} sprite={(sprite != null ? "OK" : "FALLBACK")}");
}
```

### 변경 핵심

1. **scale 통일** — `0.4f` 를 모든 분기에서 적용 (catalog 성공 / fallback 무관). AI Awake 와 동일.
2. **catalog 성공 시 `Color.white` 명시** — AI 의 ApplyVisualAppearance:120 와 동일.
3. **진단 로그 2개** — catalog null / entry.sprite null. 다음 플레이에서 *호스트 측 spawner 의 catalog wiring 누락* 확정.

만약 catalog 자체가 host-side spawner 에 null 이면 → prefab inspector 검사 후 사용자 측 wiring 작업 필요.

---

## 작업 3 — 몬스터 장판 미표시 진단 로그 추가

### 진단 결과

- prefab yaml 부착 ✅ (16개 모두 MonsterAttackBroadcast 정상)
- 코드 path 모두 정상 (호출 → IsServer 가드 → ClientRpc → Show → "Foreground" layer 존재)
- 그러나 게스트 화면에 *안 보임* → ClientRpc 손실 또는 NetworkObject.IsSpawned race 가 가장 유력

### 픽스 (진단 로그만 추가, 다음 플레이 후 root cause 확정 픽스)

#### 1. `Runtime/Combat/Telegraph/MonsterAttackBroadcast.cs`

이미 `BroadcastTelegraph` 시점 + `ClientRpc` 도착 시점에 verboseLog 가드된 로그 있음. 단 verboseLog 가 true 인지 사용자 prefab 인스펙터 확인. *true 가 기본값* 이라 별도 변경 불필요.

추가 진단: ClientRpc 도착 시 *현재 클라의 IsServer / IsClient / NetworkObjectId* 명시:

```csharp
[ClientRpc]
private void BroadcastTelegraphClientRpc(...) {
    var nm = NetworkManager.Singleton;
    Debug.Log($"[DiagTelegraph-RpcRecv] mob={name} shape={shape} pos={worldPosition} " +
              $"IsHost={IsHost} IsServer={IsServer} IsClient={IsClient} " +
              $"localId={(nm != null ? nm.LocalClientId : 0)} netObjId={NetworkObjectId} 도착", this);
    // (이미 있는 verboseLog 라인은 유지 or 통합)
    SpawnVisualClone(...);
}
```

#### 2. `Runtime/Combat/Telegraph/AttackTelegraph2DView.cs`

`Show(request)` 진입 시 로그 1줄:

```csharp
public void Show(AttackTelegraphRequest2D request) {
    EnsurePreviewRenderer();
    _activeRequest = request;
    _remainingDuration = request.Duration;
    _visible = true;
    ApplyRequest();
    _previewObject.SetActive(true);

    // [DiagTelegraph-Show] sprite + sorting + duration 가시화.
    int sortLayerId = _previewRenderer != null ? _previewRenderer.sortingLayerID : 0;
    int sortOrder = _previewRenderer != null ? _previewRenderer.sortingOrder : 0;
    Debug.Log($"[DiagTelegraph-Show] shape={request.Shape} center={request.Center} size={request.Size} duration={request.Duration:F2} " +
              $"sortingLayerId={sortLayerId} sortingOrder={sortOrder} previewActive={(_previewObject != null && _previewObject.activeInHierarchy)}", this);
}
```

#### 3. 결과 매트릭스 (다음 플레이 콘솔 분석)

| 콘솔 패턴 | 결론 |
|---|---|
| host: `[DiagTelegraph-Spawn]` ✓, guest: `[DiagTelegraph-RpcRecv]` ✓, `[DiagTelegraph-Show]` ✓, 그러나 시각 안 보임 | sortingLayer 또는 카메라 culling — 추가 조사 |
| host: `[DiagTelegraph-Spawn]` ✓, guest: `[DiagTelegraph-RpcRecv]` 없음 | **ClientRpc 손실** — NGO schema mismatch 또는 enemy NetworkObject 비-spawn |
| host: `[DiagTelegraph-Spawn]` 없음 | **broadcast 호출 자체 안 됨** — controller 의 monsterAttackBroadcast ref 자동 resolve 실패 또는 IsServer 가드 |
| host/guest 모두 `[DiagTelegraph-RpcRecv]` ✓, `[DiagTelegraph-Show]` 없음 | clone 객체 생성은 됐는데 view.Show 호출 race |

각 케이스별 후속 픽스는 다음 사이클.

---

## 변경 파일 표

| 파일 | 변경 |
|---|---|
| `Runtime/TestKhi/KhiArrowProjectile.cs` | `SetVisualOnly` 에서 collider.enabled 끄지 않음. `OnTriggerEnter2D` 에서 `_visualOnly` 인 경우 *damage 만 skip*, PlayImpactAndDestroy 정상 호출 |
| `Runtime/MagicalGirl/MagicalGirlSpawner.cs` | `SpawnVisualOnlyClone` 의 scale 통일 (0.4f 모든 분기). catalog 성공 시 `Color.white` 명시. catalog null + entry.sprite null 진단 로그 2개 |
| `Runtime/Combat/Telegraph/MonsterAttackBroadcast.cs` | `BroadcastTelegraphClientRpc` 의 진단 로그 보강 (`IsHost/IsServer/IsClient/netObjId` 포함) — verboseLog 가드는 유지 |
| `Runtime/Combat/Telegraph/AttackTelegraph2DView.cs` | `Show()` 진입 시 `[DiagTelegraph-Show]` 로그 — sortingLayerId / sortingOrder / 활성 상태 가시화 |

---

## 검증 (다음 플레이 1회)

### 작업 1 — 투사체
1. 호스트 + 게스트 던전 진입
2. 호스트가 스태프 Bolt / Fireball / 활 화살 발사 → 적과 충돌
3. **게스트 화면**: host 투사체가 적과 충돌 시 *즉시 사라져야* (이전엔 maxLifetime 까지 비행)
4. 호스트 화면도 동일 — host 본인 투사체 정상 동작 (회귀 없음)
5. 게스트가 동일 발사 → 호스트 화면에서도 즉시 사라짐

### 작업 2 — 마법소녀
1. **게스트가** 마법소녀 유물 획득
2. **호스트 화면**: 게스트 캐릭터 옆에 마법소녀 sprite (검정 박스 아니어야 함)
3. 게스트 콘솔: `[DiagMagicalGirl-Clone] attackCatalog NULL ...` 가 *없으면* → catalog wiring 정상, sprite 표시 OK
4. 만약 위 경고가 *떴다면* → host 측 prefab 의 spawner inspector catalog 할당 필요 (사용자 Editor 작업)

### 작업 3 — 몬스터 장판
1. SkeletonMage 또는 Bertha 가 area attack 발동
2. **호스트 + 게스트 콘솔 모두** 확인:
   - `[DiagTelegraph-Spawn] mob=... IsServer=True` (host 측)
   - `[DiagTelegraph-RpcRecv] mob=... IsHost=False/True ...` (각 클라)
   - `[DiagTelegraph-Show] shape=... sortingLayerId=... sortingOrder=...`
3. 위 결과 매트릭스 표 참조해 다음 사이클 픽스 방향 결정

---

## 회귀 확인

- 솔로 모드 (NM 비활성): `_visualOnly = false` 기본값 → 기존 OnTriggerEnter2D 동작 그대로. KhiMeteor 도 변경 없음.
- 단검 슬래시: 변경 무관 (AttackBroadcast 의 슬래시 path 그대로).
- 호스트 본인 투사체: `SetVisualOnly(true)` 호출 안 됨 → 변경 무관.

---

## 한계 / 다음 사이클

- **마법소녀 catalog wiring**: 진단 결과 `attackCatalog NULL` 이면 prefab inspector 작업 필요 (코드 외).
- **몬스터 장판 root cause**: 진단 로그 기반 다음 사이클 픽스 — ClientRpc 손실 vs sorting layer 적용 race vs NetworkObject spawn race.
- **투사체 시각 부정확**: clone 이 직선 (homing 0) 이라 owner 의 homing 결과와 미세 발산 — 적과 정확히 같은 위치에서 안 터질 수 있음. 시각 충분히 가까우면 acceptable, 부정확 보이면 다음 사이클에 owner→broadcast destroy ID tracking 추가.
- 보류: 마법소녀 공격 시각 sync, 진단 로그 일괄 제거, Dungeon_1F_2R / 1F_4R asset 픽스 (사용자 Editor 작업 대기).

---

## 결정점 (사이클 종료 후)

- 작업 1 — 투사체 destroy 시각 일치 확인. 호밍 발산이 눈에 띄게 크면 추가 픽스.
- 작업 2 — 검정 박스 사라지면 OK. catalog NULL 경고 떴으면 사용자 prefab 작업 후 재검증.
- 작업 3 — 콘솔 매트릭스 기반 root cause 확정 → 다음 사이클 정공 픽스.

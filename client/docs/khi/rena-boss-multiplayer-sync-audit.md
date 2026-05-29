# RenaRoot 보스 멀티플레이어 동기화 감사 + 게스트 데미지 구현 Plan

> **Plan 위치 주의**: 사용자 메모리 규칙 `feedback_plan_location` 에 따라 `client/docs/khi/` 에 정본 저장. ExitPlanMode UI 표시 용도로 시스템 지정 경로(`~/.claude/plans/`) 에도 동일 사본 작성.

## Context

`LostMemory/Assets/_Project/Prefabs/Enemies/Boss/RenaRoot.prefab` 의 멀티플레이 동기화 상태 점검 요청. 보스의 HP, 위치, 방향, 사망 같은 핵심 상태는 `Unity Netcode for GameObjects` 기반으로 잘 동기화되는지, 보스가 쏘는 투사체(Fireball / Inferno / IceSweep / ThunderStrike / Thunderbolt) 가 게스트 클라이언트에 보이고 데미지가 일관되게 처리되는지 확인하기 위함. 출시 전 멀티플레이 회귀 방지가 목표.

본 문서는 **(1) 진단 리포트 + (2) "게스트 공격 → 보스 HP 반영" 구현 plan** 의 혼합 문서. 그 외 항목(투사체 시각화 등)은 진단만 정리하고 구현은 추후 결정.

## RenaBossProjectile 개요 (참고)

자체 제작 경량 투사체 컴포넌트. `MonoBehaviour` 단독, NetworkObject 없음. `new GameObject()` + `AddComponent` 로 런타임 빌드되어 `Configure(...)` 호출로 파라미터 주입. 직선/호밍 이동 + `Physics2D.OverlapCircle` 콜리전 + `Health.Damage()` 직접 호출 + sprite 3단계(Casted/Loop/Hit) 애니메이션. 5종 패턴 모두 이 컴포넌트를 재구성해서 사용.

---

## 사용 중인 네트워킹 스택

- **Unity Netcode for GameObjects (NGO)**
- NetworkManager: [LostMemory/Assets/_Project/Prefabs/Network/NetworkManager.prefab](LostMemory/Assets/_Project/Prefabs/Network/NetworkManager.prefab)
- Transport: `LostMemoryRelayTransport` (Relay: k14c201.p.ssafy.io:7777)
- Server-authoritative 정책 (Host = Server)

---

## RenaRoot.prefab 네트워크 컴포넌트

루트에 다음이 부착 (탐색 결과):

| 컴포넌트 | 역할 |
|---|---|
| `NetworkObject` | NGO 식별/스폰. Ownership=Server |
| `NetworkTransform` | 위치/회전 server-authoritative sync |
| `NetworkAnimator` (Visual 자식) | 애니메이션 파라미터 sync |
| `MonsterHealthSync` | HP + MaxHealth sync, 사망 ClientRpc |
| `MonsterNetSync` | 게스트 측 AI/Combat 컴포넌트 비활성 + facing sync |
| `MonsterAttackBroadcast` | 망치 등 공격 시각효과 broadcast |

씬 배치(예: `2F_BossTest.unity`) 기반이라 `DefaultNetworkPrefabs.asset` 등록은 불필요.

---

## 동기화 항목별 상태

### ✅ HP 동기화 — OK (server-authoritative)

[MonsterHealthSync.cs:43-99](LostMemory/Assets/_Project/Scripts/Runtime/Networking/Monster/MonsterHealthSync.cs:43)

- `_syncedHealth` + `_syncedMaxHealth` (`NetworkVariableWritePermission.Server`)
- 서버: 매 프레임 `Health.CurrentHealth` 변경 감지 → NetworkVariable write (minDelta=0.01)
- 게스트: `Health.DamageDisabled()` 호출로 로컬 데미지 차단 + `OnValueChanged` 로 `health.SetHealth(value)` 적용
- MaxHealth 도 sync — Runtime scaling (EnemyDataRuntimeAdapter) 에 대응 (Bug #36 해결)
- 사망: 서버에서 `current ≤ 0` 감지 → `TriggerDeathVisualsClientRpc()` 발화 → `deathDespawnDelay=2s` 후 `NetworkObject.Despawn(destroy:true)`
- 게스트 측 사망 시각: Animator `SetTrigger("Death")` + Collider2D 모두 disable

### ✅ 위치/회전 — OK

`NetworkTransform` 사용. 서버 권위. 추가 검토 불필요.

### ✅ 방향 (facing) — OK

[MonsterNetSync.cs:52-114](LostMemory/Assets/_Project/Scripts/Runtime/Networking/Monster/MonsterNetSync.cs:52)

- `_isFacingRight` NetworkVariable
- 게스트 측 `CharacterOrientation2D` 가 disabled 상태라, CharacterModel localScale.x 또는 fallback SpriteRenderer.flipX 를 직접 조작 (`ApplyFacing`)

### ✅ 애니메이션 트리거 — OK (NetworkAnimator)

Visual 자식의 NetworkAnimator 가 fastCast / HeavyCast / Death 등 파라미터를 sync. 미세한 레이턴시는 있음.

### ❌ 투사체 시각화 — 게스트에 안 보임 (CRITICAL)

[RenaBossProjectile.cs:10](LostMemory/Assets/_Project/Scripts/Runtime/Enemies/Boss/Rena/RenaBossProjectile.cs:10), [RenaBossSpellCombatController.cs:1911-2011](LostMemory/Assets/_Project/Scripts/Runtime/Enemies/Boss/Rena/RenaBossSpellCombatController.cs:1911)

- `RenaBossProjectile` = `MonoBehaviour` (NetworkBehaviour 아님), NetworkObject 없음
- `SpawnProjectile()` 은 `new GameObject(objectName)` + `AddComponent<RenaBossProjectile>()` 로 **동적 생성** → 어차피 NetworkPrefabsList 등록 불가능
- 데미지 판정은 `Physics2D.OverlapCircle` 기반 → `Health.Damage()` 직접 호출 (서버에서만 의미 있음)
- **결과**:
  - 서버 사이드 SpawnProjectile 호출이 게스트에서도 일어나면 → 게스트도 시각은 보임. 단, 데미지/콜리전은 본인 화면에서만 의미 (서버 판정만 반영됨). 게스트가 본인의 Health.DamageDisabled() 덕분에 게스트 본인 캐릭터 데미지는 무시되지만, 보스 HP 에는 영향 없음 (`IsOwnedByAttacker` 체크와 무관하게 보스 collider 와 충돌 안 함).
  - 만약 게스트 측에서 `SpellCombatController` 가 비활성 → 투사체 자체가 안 만들어져 화면에 안 보임 → **게스트는 보스 공격을 회피할 시각 단서 없음**
- 어느 케이스인지 다음 항목(↓) 에서 확인 필요

### ⚠️ RenaBossSpellCombatController 게스트 측 활성 여부 — 검증 필요

[RenaBossSpellCombatController.cs:12](LostMemory/Assets/_Project/Scripts/Runtime/Enemies/Boss/Rena/RenaBossSpellCombatController.cs:12) — `class RenaBossSpellCombatController : MonoBehaviour` (TDE `AIAction` / `AIDecision` 자손 **아님**)

[MonsterNetSync.cs:27-33](LostMemory/Assets/_Project/Scripts/Runtime/Networking/Monster/MonsterNetSync.cs:27) `additionalDisableComponentNames` 기본값:
```
"CharacterMovement", "CharacterOrientation2D", "CharacterHandleWeapon", "AIBrain"
```
→ `RenaBossSpellCombatController` / `RenaBossEncounterController` / `RenaBossWanderController` **모두 명시적 disable 대상이 아님**.

**가능한 두 시나리오**:
1. **게스트 측에서도 SpellCombatController 가 enable 인 채로 Update() 돌면서 투사체 생성** → 게스트는 시각적으로 투사체를 봄 (좋음). 단, 자기만의 로컬 투사체라 위치/타이밍이 서버와 약간 다를 수 있음 (배치 RNG, deltaTime drift). 더 위험한 건 게스트 본인의 투사체가 본인 Player 콜라이더와 충돌해 로컬 Health.Damage 호출 시도 → `DamageDisabled()` 차단 덕분에 게스트 본인 Player HP 는 안 깎임. 보스 콜라이더와 충돌 가능성도 있는데 보스 owner 매칭 (`IsOwnedByAttacker`) 으로 자기 자신은 무시.
2. **RenaBossEncounterController / WanderController / AIBrain 흐름이 SpellCombatController 를 트리거** → AIBrain 이 disable 되니 트리거가 안 와서 게스트 측 SpellCombatController 가 사실상 idle → **투사체 자체 미생성 → 게스트 화면 보이지 않음**.

→ **실제 동작 확인 필요**. (Encounter/Wander Controller 가 어떻게 SpellCombatController.Cast*() 를 호출하는지 코드 추적, 또는 2P 실측)

### ❌ 게스트 공격이 보스 HP에 반영 안 됨 → **본 plan 의 구현 대상**

[MonsterHealthSync.cs:19-21](LostMemory/Assets/_Project/Scripts/Runtime/Networking/Monster/MonsterHealthSync.cs:19) 코드 주석에 명시된 known limitation:

> 한계 (발표 후 follow-up):
> 게스트가 공격해도 데미지 0 (DamageDisabled 차단). 게스트 데미지도 적용하려면 player hitbox →
> ServerRpc → host Damage 패턴이 별도로 필요.

→ 이 항목은 **반드시 구현** (사용자 결정). 아래 "구현 Plan" 섹션 참고.

---

## 핵심 파일 (수정 검토 시)

```
LostMemory/Assets/_Project/Prefabs/Enemies/Boss/RenaRoot.prefab
LostMemory/Assets/_Project/Scripts/Runtime/Networking/Monster/MonsterHealthSync.cs
LostMemory/Assets/_Project/Scripts/Runtime/Networking/Monster/MonsterNetSync.cs
LostMemory/Assets/_Project/Scripts/Runtime/Enemies/Boss/Rena/RenaBossSpellCombatController.cs
LostMemory/Assets/_Project/Scripts/Runtime/Enemies/Boss/Rena/RenaBossProjectile.cs
LostMemory/Assets/_Project/Scripts/Runtime/Enemies/Boss/Rena/RenaBossEncounterController.cs
LostMemory/Assets/_Project/Scripts/Runtime/Enemies/Boss/Rena/RenaBossWanderController.cs
LostMemory/Assets/_Project/Prefabs/Network/NetworkManager.prefab
LostMemory/Assets/DefaultNetworkPrefabs.asset
LostMemory/Assets/_Project/Scenes/Dungeon/2F_BossTest.unity
```

---

## Verification (실측 체크리스트)

이 audit 결론을 실제 2P 빌드로 확인하는 방법:

1. **Host + Client 동시 접속** (에디터 Host + 빌드 Client 또는 Relay 통한 2 인스턴스)
2. **호스트 시점**:
   - RenaRoot 등장 후 Fireball / Inferno / IceSweep / ThunderStrike / Thunderbolt 각 패턴 발동되는지
   - HP 깎이고 죽는 흐름 정상
3. **게스트 시점** (CRITICAL):
   - [ ] 보스 위치/방향이 호스트와 같이 움직이는가
   - [ ] 보스 HP 바가 호스트와 동일하게 줄어드는가
   - [ ] **Fireball / Inferno 투사체가 화면에 보이는가** (Scenario 1 vs 2 판별)
   - [ ] IceSweep / ThunderStrike / Thunderbolt 시각 효과가 보이는가
   - [ ] 게스트가 보스 콜라이더와 겹쳐도 본인 HP 가 안 깎이는가? (DamageDisabled 검증 — Player 측에는 적용 안 됨에 주의. 실제로는 PlayerHealthSync 등 별도 처리 확인 필요)
   - [ ] 게스트가 보스를 때려도 보스 HP 변동 없음 확인 (known limitation)
   - [ ] 보스 사망 시 게스트 화면에서도 사라지는가 (Despawn sync)
4. **로그 확인**:
   - `MonsterHealthSync.verboseLog` true 로 켜고 게스트에서 OnValueChanged 로그 확인
   - `MonsterNetSync.verboseLog` true 로 켜고 게스트에서 "Non-server — AI/Combat 비활성" 로그가 RenaRoot 에도 찍히는지

5. **코드 추가 추적** (실측 전에 가능):
   - `RenaBossEncounterController.cs` 에서 `_spellCombatController.XXX()` 호출 패턴 grep
   - 그 호출이 AIBrain / AIAction chain 에 의존하는지, 자체 `Update()` 로 도는지 확인
   - 게스트에서 `SpellCombatController.Update()` 가 도는지 결정

---

## 구현 Plan — 게스트 공격 → 보스 HP 반영 (MUST)

### 좋은 소식: 인프라 이미 존재

이미 같은 server-relay 패턴이 두 곳에 구현되어 있음 → **추가 인프라 작성 불필요. 단지 RenaRoot 까지 흐름이 닿는지 확인 + 빈 곳만 연결**.

- [PlayerDamageRelay.cs](LostMemory/Assets/_Project/Scripts/Runtime/Networking/Player/PlayerDamageRelay.cs)
  - `TryDamage(Health target, float damage, ...)` 진입점
  - 호스트면 즉시 `target.Damage(...)` 호출
  - 게스트면 `target.GetComponent<NetworkObject>().NetworkObjectId` 를 추출해 `RequestDamageServerRpc(targetNetworkObjectId, damage, ...)` 발화 → 호스트가 NetworkObject 찾아서 `Health.Damage()` 호출
- [AttackBroadcast.cs:279-294](LostMemory/Assets/_Project/Scripts/Runtime/Networking/Player/AttackBroadcast.cs:279) `RelayProjectileDamageServerRpc`
  - 게스트 owner 의 projectile (화살/스태프) 이 적 충돌 시 사용

### 구현 단계

#### Step 1 — RenaRoot 가 정상적인 NetworkObject Health target 인지 확인 (검증만)
- RenaRoot 의 `Health` 컴포넌트가 RenaRoot 루트(=NetworkObject 가 붙은 GameObject) 에 있는지 확인 (PlayerDamageRelay 는 `GetComponentInParent<NetworkObject>` 또는 동등 패턴으로 NetworkObjectId 추출)
- `Health` 가 자식에 있으면 NetworkObject 까지의 parent 탐색이 되는지 확인 (PlayerDamageRelay 코드의 추출 방식 점검: `target.GetComponentInParent<NetworkObject>()`)

#### Step 2 — 플레이어 공격 진입점들이 PlayerDamageRelay 를 거치는지 점검
플레이어가 보스에게 데미지를 가할 수 있는 경로 전수 점검:
- `AnimationEventMeleeWeapon` (검 차지 등)
- `KhiMeleeHitbox` (Khi 캐릭터 근접)
- `KhiArrowProjectile` (활)
- `KhiMeteor` (메테오)
- `KhiDaggerTeleportController` (단검 텔레포트)
- 기타 `health\.Damage` 호출 grep 결과

각 경로에서 **직접 `target.Damage(...)` 를 호출하는 곳이 있다면 → `PlayerDamageRelay.TryDamage(...)` 로 교체**. 이미 relay 거치는 곳은 무수정.

#### Step 3 — MonsterHealthSync 의 `DamageDisabled()` 와 충돌 없는지 확인
- 게스트 측 RenaRoot 의 `Health.DamageDisabled()` 가 호출됨 → 게스트 로컬 `Health.Damage` 무시
- PlayerDamageRelay 는 게스트에서 `Health.Damage` 를 직접 호출하지 않고 ServerRpc 로 호스트에 위임 → 호스트의 RenaRoot Health 는 `DamageDisabled()` 호출 안 됨 (서버니까) → 정상 데미지 적용됨
- 호스트 사이드 RenaRoot.CurrentHealth 가 깎이면 → 기존 `MonsterHealthSync` 가 자동으로 `_syncedHealth.Value` 업데이트 → 게스트도 동기화 → 추가 작업 불필요

#### Step 4 — owner 어트리뷰션 (Optional)
- `Health.Damage(damage, owner, ...)` 의 owner 인자가 ServerRpc 경로에서 누락되지 않는지 확인 (점수/통계 처리에 사용될 수 있음)
- 필요하면 ServerRpc 파라미터에 `ulong attackerClientId` 추가 (혹은 `ServerRpcParams.Receive.SenderClientId` 사용)

### 핵심 수정 파일 (예상)

```
LostMemory/Assets/_Project/Scripts/Runtime/Networking/Player/PlayerDamageRelay.cs     (재사용 — 수정 없음 또는 owner 어트리뷰션 추가)
LostMemory/Assets/_Project/Scripts/Runtime/Combat/AnimationEventMeleeWeapon.cs        (점검 → 필요시 Relay 경유로 변경)
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeHitbox.cs                  (점검 → 필요시 변경)
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiArrowProjectile.cs              (점검 — 이미 AttackBroadcast 경유일 가능성 큼)
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeteor.cs                       (점검)
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDaggerTeleportController.cs     (점검)
LostMemory/Assets/_Project/Prefabs/Enemies/Boss/RenaRoot.prefab                       (NetworkObject/Health 배치 검증만)
```

### Verification (게스트 데미지 구현)

1. Host + Guest 2P 빌드
2. **호스트 시점**: 게스트 플레이어가 보스 때릴 때 보스 HP 깎이는 로그 (`MonsterHealthSync.verboseLog=true` → "SERVER write 깎인 값" 로그) 확인
3. **게스트 시점**: 본인이 때리는 액션에 보스 HP 바가 즉시(혹은 RTT 후) 줄어듦
4. 호스트 단독으로도 회귀 없음 (기존 호스트 처치 흐름 깨지지 않음)
5. 보스 사망 시 양측 모두 정상 (사망 ClientRpc + Despawn)

---

## Priority Summary (수정 의사결정용)

| 순위 | 항목 | 영향 | 본 plan 처리 |
|---|---|---|---|
| **P0 (MUST)** | **게스트 공격이 보스 HP 에 반영** | 게스트가 보스 처치 기여 못함 | **본 plan 으로 구현 (위 "구현 Plan" 섹션)** |
| P1 | 투사체 게스트 시각화 (Scenario 2 인 경우) | 게스트가 보스 공격 회피 어려움 | 진단만, 실측 후 별도 plan |
| P2 | SpellCombatController 게스트 동작 결정 (Scenario 1 의 drift) | 화면 투사체 위치 서버와 약간 다름 | 진단만, 실측 후 결정 |
| P3 | NetworkAnimator AuthorityMode (현재 Owner) | 미세 레이턴시 | 보류 |

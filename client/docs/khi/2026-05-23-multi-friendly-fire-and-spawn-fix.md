# 2026-05-23 — 멀티 친아군 차단 + 방 스폰 위치 + 보스 진입 권위 fix

## Context

시연 임박 4인 코옵 멀티 빌드에서 다음 3가지 멀티 버그를 확인:

1. **3인 이상에서 팀원 데미지** — 다른 player 가 공격하면 체력은 닳지만 히트 VFX 가 안 보이는 비대칭 증상. 2인 환경에선 발생 안 함.
2. **1F-3R / 1F-4R / 2F-1R~4R 적이 잘못된 위치에 소환됨** — 1F-1R, 1F-2R 까지만 정상.
3. **보스 룸 진입 트리거 권위 결함** — 게스트가 먼저 trigger 영역 밟으면 host 측 보스 활성화가 누락될 위험.

Phase E ([multiplayer_fixes_phase_E.md](multiplayer_fixes_phase_E.md)) 에서 `KhiMeleeHitbox` + `KhiArrowProjectile` 두 경로에만 `PlayerHealthSync` 가드를 넣었음. 4인 환경에선 가드가 닿지 않은 다른 데미지 경로가 다수 새고 있었음.

---

## 1. 친아군 차단 — 12경로 + 통합 헬퍼

### Root cause

- **Sentinel 부재**: 표준 PlayerPrefab `TestKhi_MinimalCharacter2D.prefab` (GUID `4d290d1fc0f84526999c421c61df5d8f`) 에 `PlayerHealthSync` 컴포넌트가 부착되어 있지 않음. `PlayerHealthSync` GUID 검색 결과 `Test_shm_nickname.prefab` 에만 존재 → Phase E 가드가 그동안 실제 player 에서 작동 안 했음.
- **서버 중계 우회**: `PlayerDamageRelay.RequestDamageServerRpc` 와 `AttackBroadcast.RelayProjectileDamageServerRpc` 가 서버 측에서 재검증 없이 `health.Damage()` 호출. 게스트가 ServerRpc 보내면 host 가 친아군 가드 없이 데미지 적용. 이게 **"이펙트는 안 나는데 체력만 닳음" 증상의 정체** — VFX 트리거(`TargetHit` 이벤트)는 호출자 측 로컬 가드에서 걸러지는데 데미지 RPC 만 서버에서 통과됨.
- **분산된 가드**: 무기/스킬마다 친아군 판정이 제각각. `IsAuthoritativePlayer` (Character.CharacterType + IsPlayerObject), `IsOwnedByAttacker` (GameObject 비교), 일부는 가드 자체 부재.

### Fix 전략

통합 헬퍼 `CombatTargetable.IsFriendlyPlayer(Health)` 신규. 3개 sentinel 을 OR 로 묶어 prefab 부착 누락에 강건:

1. `PlayerHealthSync` 컴포넌트
2. `PlayerMovementSync` 컴포넌트 (실제 player prefab 에 더 광범위하게 부착됨)
3. `NetworkObject.IsPlayerObject` (NGO SpawnAsPlayerObject 로 spawn 된 player)

4인 환경 자동 확장 — N명 무관. host 측에서 AI 변환된 게스트 player 도 IsPlayerObject 로 잡힘.

### 변경 파일

| 파일 | 변경 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Combat/CombatTargetable.cs` | `IsFriendlyPlayer(Health/Collider2D)` 신규 + `CanBeAutoTargetedEnemy` 에 안전벨트로 IsFriendlyPlayer 추가 |
| `Assets/_Project/Scripts/Runtime/Networking/Player/PlayerDamageRelay.cs` | `RelayDamage` 진입부 + `RequestDamageServerRpc` 서버 측 양방향 가드 |
| `Assets/_Project/Scripts/Runtime/Networking/Player/AttackBroadcast.cs` | `RelayProjectileDamageServerRpc` 서버 측 가드 |
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiMeteor.cs` | OverlapCircle 결과 처리 시 친아군 skip |
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiFlameZone.cs` | 매 tick OverlapBox 결과 처리 시 친아군 skip |
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiParryDamageOnTouch.cs` | `ApplyReducedDamage` 진입부 가드 |
| `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAI.cs` | fallback 직접 데미지 분기 가드 |
| `Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs` | `FindNearbyEnemies` 친아군 skip + `ApplyChain` 루프 안전벨트 |
| `Assets/_Project/Scripts/Runtime/Tarot/TarotCards.cs` | `DeathCard.Activate` 적 enumeration 가드 |

### 자동 차단된 경로 (CombatTargetable 강화로)

`CanBeAutoTargetedEnemy` 호출 경로는 별도 패치 없이 자동 차단:
- `MagicalGirlProjectile.OnTriggerEnter2D`
- `MagicalGirlAOE.DoTick`
- `MagicalGirlFusion.FireLaser` (Pattern A)
- `MagicalGirlFusion.FireGlobalAOE` (Pattern B)

### 스킵한 경로

- `TestKhiDamageTrap` — enemy → player 트랩일 가능성이 커서 친아군 가드 적용 시 회귀 위험. instigator 가 누군지 확정 후 별도 처리.

### 진단 로그

ServerRpc 진입 시 `[PlayerDamageRelay] FriendlyFire blocked at ServerRpc — target=... requester=...` / `[AttackBroadcast] FriendlyFire blocked at DamageRequest — target=...` 로그 출현. 시연/회귀 검증 시 어느 경로가 발화했는지 추적 가능.

---

## 2. 방 스폰 위치 버그

### Root cause

`Assets/_Project/Scripts/Runtime/Stage/Data/RoomEncounterAnchor.cs:11` 의 `cachedSpawnPoints` 가 `[SerializeField]` 로 **prefab 레벨로 직렬화**됨. 동일 layout prefab 을 여러 방으로 spawn 시 첫 2개 인스턴스는 우연히 fileID 가 맞지만 3번째부터 stale fileID 가 **다른 방의 SpawnPoint** 를 가리켜 적이 엉뚱한 위치에 소환.

기존 `Awake` 는 `cachedSpawnPoints.Length == 0` 일 때만 refresh — prefab 에 부분적으로 직렬화된 stale 배열이 있으면 조건 미충족.

### Fix

```csharp
private void Awake()
{
    // prefab 직렬화된 cachedSpawnPoints 무시. 런타임엔 항상 자기 자식 chain 에서 fresh 재계산.
    RefreshSpawnPoints();
}
```

보스 방도 같은 `EnemyEncounterSpawner.Begin()` → `RoomEncounterAnchor.GetSpawnPoints()` 흐름 사용하므로 함께 해결.

### 변경 파일

- `Assets/_Project/Scripts/Runtime/Stage/Data/RoomEncounterAnchor.cs`

---

## 3. 보스 룸 진입 권위 가드

### Root cause

`BossCurrentPositionStartTrigger` 는 MonoBehaviour 로 모든 클라에서 `OnTriggerEnter2D` 발화. 게스트가 먼저 trigger 영역에 들어가면 게스트 측에서만 `BeginEncounter` 호출 → host 측 `RenaBossEncounterController.SetCombatActive` 가 누락. 게스트가 trigger 영역 빠져나가면 host 가 영영 trigger 못 밟을 위험.

`RoomEntryRuntimeController.BeginRoomEntry` 는 `if (!IsAuthority) return` 가드가 있는데 보스 트리거엔 없었음.

### Fix

`OnTriggerEnter2D` 진입부에 `NetworkManager.IsServer` 가드. 게스트 측 trigger 발화는 무시. host 측에서도 게스트 player 위치가 `PlayerMovementSync` 로 동기화되므로 host 측 자기 OnTriggerEnter2D 가 별도로 발화 — 자연스럽게 권위 일관.

### 변경 파일

- `Assets/_Project/Scripts/Runtime/Stage/BossCurrentPositionStartTrigger.cs`

---

## P0 후속 — BossStartTrigger 그룹 텔레포트 (한 명 진입 → 모두 이동)

### Context

Town_Preview.unity 의 `BossStartTrigger` 가 여러 player 환경에서 "모두 같이 이동" 의도대로 작동 안 함.

### Root cause

[PlayerMovementSync.cs:48](LostMemory/Assets/_Project/Scripts/Runtime/Networking/Player/PlayerMovementSync.cs): `OnIsServerAuthoritative() => false` — **owner-authoritative** NetworkTransform. host 가 게스트 player 의 `transform.position` 을 직접 옮겨도 다음 frame 에 owner 가 자기 위치로 덮어씀. `BossCurrentPositionStartTrigger.GatherPlayers` 가 host 측에서 `TeleportCharacter` 호출해도 host 자기 캐릭터만 이동.

추가: 우리 직전 P0 fix 의 `IsServer` 가드 때문에 게스트가 먼저 trigger 밟으면 host 측 발화가 영영 안 됨 (host 가 영역 안 들어오면 dead-lock).

### Fix 전략

**ServerRpc 위임 + ClientRpc 그룹 텔레포트** — NGO 표준 패턴. Player prefab 에 이미 부착된 `PlayerMovementSync` (NetworkTransform 상속 NetworkBehaviour) 에 메서드 2개 추가 → Unity editor 작업 불필요.

### 흐름

1. 게스트가 trigger 밟음 → `OnTriggerEnter2D` 가 자기 owner player 의 `PlayerMovementSync.RequestBossStartServerRpc()` 호출
2. host 측 ServerRpc 수신 → 씬에서 매칭하는 `BossCurrentPositionStartTrigger` 인스턴스 찾아 `StartBossAtCurrentPositions(initiator)` 호출
3. host 의 `GatherPlayers` 가 각 player 의 `PlayerMovementSync.TeleportPlayerClientRpc(targetPos, freeze)` 호출
4. 각 owner client 가 ClientRpc 수신 → `IsOwner` 인 경우만 자기 player 텔레포트 (`MovePosition`)
5. owner-auth NetworkTransform 이 정상 sync → 다른 클라들도 새 위치 봄
6. host 가 BeginEncounter (intro / 보스 활성)

### 변경 파일

| 파일 | 변경 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Networking/Player/PlayerMovementSync.cs` | `RequestBossStartServerRpc()`, `TeleportPlayerClientRpc(Vector3, bool)` 추가 |
| `Assets/_Project/Scripts/Runtime/Stage/BossCurrentPositionStartTrigger.cs` | `OnTriggerEnter2D` 게스트 분기 (P0 IsServer 가드를 ServerRpc 위임으로 대체), `GatherPlayers` 멀티 분기 (ClientRpc 텔레포트) |

### 효과

- 한 명(누구든) trigger 진입 → 모든 player 가 `bossEntryPoint` 또는 `gatherPoint` 의 정해진 spacing 위치로 그룹 텔레포트
- 게스트 dead-lock (host 가 trigger 안 밟는 경우) 해소
- 4인까지 자동 확장
- 솔로 환경 회귀 없음 (`!networkActive` 분기로 기존 직접 텔레포트 유지)

### Reward panel 떠있을 때 처리

이동 직전 reward panel 이 활성이면 보상 선택 후 텔레포트 — 보상 누락 방지.

**2층 방어**:
1. **Trigger 발화 차단** (`OnTriggerEnter2D` 진입부) — 자기 측 reward panel `activeInHierarchy=true` 면 즉시 return. 자기가 trigger 발화 자체 안 함.
2. **ClientRpc 텔레포트 wait** (`TeleportPlayerClientRpc` 안) — 다른 player 가 trigger 밟아 host 가 broadcast 보내도, 자기 측 reward panel 닫힐 때까지 coroutine 으로 polling. 닫힌 뒤 자기 측 텔레포트.

각자 자기 측 RewardPanelView 인스턴스의 `gameObject.activeInHierarchy` 로 판정 — `RewardController` 가 이미 같은 패턴 사용 중 (`rewardPanelView.gameObject.activeSelf`). 별도 API 추가 없음.

### 한계 (시연 후 follow-up)

- 씬에 `BossCurrentPositionStartTrigger` 인스턴스가 여러 개면 host 가 첫 번째 인스턴스만 사용. 현 시연 씬은 1개라 OK. 여러 개 필요 시 trigger 식별자 (NetworkObjectId 또는 string ID) 인자 추가 필요.
- Reward panel 처리 중인 player 가 늦게 합류하므로 boss intro 의 초반부를 못 볼 수 있음. 시연 영향 미미 (reward 처리는 보통 짧음). 완벽한 sync 가 필요하면 ready-up 패턴(옵션 B)로 보강.

---

## P0 후속 — Rena 보스 게스트 측 invisible fix (Intro visibility broadcast)

### Context

호스트 화면엔 Rena 보스 정상 표시되는데 **게스트 화면에서만 보스가 안 보이는** 증상.

### Root cause

`BossIntroSequenceController` 가 일반 MonoBehaviour. 모든 client 의 `OnEnable` 에서 `sequenceData.hideBossBeforeEntry == true` 면 Visual GameObject hide. 그런데:

- **호스트**: BossStartTrigger 발화 → BeginIntro → RunIntroSequence → entry 시점 `SetIntroVisibility(true)` → 자기 Visual reveal ✓
- **게스트**: 우리 P0 fix 의 ServerRpc 위임 패턴 때문에 게스트 측 `BeginIntro` 호출 안 됨 → `RunIntroSequence` 안 돔 → 게스트 측 Visual 영원히 hide ✗

추가로 `RenaBossPrototypeBuilder.cs:702` 가 `hideBossBeforeEntry = true` 를 강제 직렬화 — Inspector 에서 false 로 바꿔도 빌더 재실행 시 덮어씀.

### Fix 전략

ServerRpc 위임 + ClientRpc 텔레포트 패턴(P0 후속 BossStartTrigger 그룹 텔레포트)과 동일 패턴. **Intro visibility 도 host → ClientRpc broadcast** 로 모든 client 동기화.

OnEnable/OnDisable 의 hide 는 그대로 모든 client 가 자체 실행 (broadcast 불필요). **reveal 시점만 host broadcast**.

### 변경 파일

| 파일 | 변경 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Stage/BossIntroSequenceController.cs` | `SetIntroVisibility` 내부 → `ApplyIntroVisibilityLocal` + `BroadcastIntroVisibilityIfHost`. `ApplyRemoteIntroVisibility(bool)` public 메서드 추가 (ClientRpc 수신 측 진입점). |
| `Assets/_Project/Scripts/Runtime/Networking/Player/PlayerMovementSync.cs` | `BroadcastBossIntroVisibilityClientRpc(bool)` 추가 — 모든 client 가 자기 측 BossIntroSequenceController 찾아 visibility 적용 |

### 흐름

```
1. Scene load → 모든 client OnEnable → 자체 hide (host broadcast 불필요 — 모든 client 동시)
2. 게스트 trigger 진입 → PlayerMovementSync.RequestBossStartServerRpc (기존 P0)
3. host 측 BossCurrentPositionStartTrigger.StartBossAtCurrentPositions → BeginIntro
4. host RunIntroSequence entry → SetIntroVisibility(true)
   ├─ ApplyIntroVisibilityLocal(true) → host 자기 Visual.SetActive(true)
   └─ BroadcastIntroVisibilityIfHost(true) → ClientRpc 발사
       └─ 모든 client.PlayerMovementSync.BroadcastBossIntroVisibilityClientRpc
           └─ 게스트 측 ApplyRemoteIntroVisibility(true) → 게스트 Visual.SetActive(true)
5. 게스트 화면 보스 정상 표시
6. Intro 완료 → BeginEncounter → 보스 정상 작동
```

### 효과

- 등장 연출 유지 (`hideBossBeforeEntry = true` 그대로) — 보스 entry animation/motion 정상
- 게스트도 host 와 동일 시점(RTT 50~150ms)에 보스 등장
- 빌더 재실행 시에도 fix 유지 — broadcast 패턴이 ScriptableObject 설정과 무관
- ClientRpc 는 owner 무관 모든 client 수신 → PlayerMovementSync 인스턴스 1개에서 발사하면 host + 모든 guest 가 받음

### Unity editor 작업 — 없음

- `BossIntroSequenceController` 는 RenaRoot.prefab 에 이미 부착
- `PlayerMovementSync` 는 모든 player prefab 에 이미 부착
- 메서드 추가만으로 자동 작동
- `RenaBossIntroSequenceData.hideBossBeforeEntry = 1` 유지 (등장 연출 살아있음)

### 한계 (시연 후 follow-up)

- 다른 보스(Bertha) 의 BossIntroSequenceController 도 동일 동작 — Bertha 멀티 sync 도 자동 적용됨
- player freeze (`shouldLockPlayers`) 는 게스트 측에서 자기 측 RunIntroSequence 안 도므로 자기 측 player 안 freeze. 등장 시점에 게스트 player 가 움직일 수 있음. 시연 영향 미미.
- 완벽한 sync 가 필요하면 `BeginIntro` 자체를 ClientRpc broadcast 로 확장 — 모든 client 가 자기 측 RunIntroSequence 진행. 시간 더 필요.

---

## P0 후속 — Rena 보스 투사체 게스트 visual sync

### Context

보스 visual fix 후에도 **Rena 보스의 투사체(Fireball/Inferno)가 게스트 화면엔 안 보임**. host 화면엔 정상.

### Root cause

`RenaBossSpellCombatController.SpawnProjectile` 가 `new GameObject` + `AddComponent` 로 동적 생성. **NetworkObject.Spawn 호출 없고 ClientRpc broadcast 도 없음** → host 측 local GameObject 만 존재.

데미지는 host 권위로 게스트 player Health 에 RPC 적용 → 체력 감소는 도달, **투사체 sprite 만 빠짐**.

비교:
- 일반 적 (SkeletonMage 등): NetworkObject.Spawn 으로 정식 sync ✓
- Khi 활 (KhiArrow): ClientRpc visual-only clone broadcast ✓
- Rena 보스: ❌ 둘 다 없음

### Fix 전략

KhiArrow 의 visual-only clone 패턴 적용 — host 가 SpawnProjectile 직후 ClientRpc 발사 → 게스트가 자기 측 visual-only GameObject 생성.

### 변경 파일

| 파일 | 변경 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Enemies/Boss/Rena/RenaBossProjectile.cs` | `_visualOnly` 필드 + `SetVisualOnly` setter. Update 의 ProcessHits/ProcessObstacleCollision 에 `!_visualOnly` 가드. 이동/애니메이션/lifetime 만료는 진행. |
| `Assets/_Project/Scripts/Runtime/Enemies/Boss/Rena/RenaBossSpellCombatController.cs` | `CastKind` enum public 화. SpawnProjectile 끝에 `BroadcastVisualSpawnIfHost` 호출. 신규 `SpawnVisualOnlyProjectile(int kindIndex, Vector3, Vector2)` public — 게스트 측 ClientRpc 진입점. Fireball/Inferno 지원. |
| `Assets/_Project/Scripts/Runtime/Networking/Player/PlayerMovementSync.cs` | `BroadcastBossProjectileSpawnClientRpc(int, Vector3, Vector2)` — IsHost return 가드 + 게스트가 SpellCombatController 찾아 SpawnVisualOnlyProjectile 호출. |

### 흐름

```
1. host SpawnProjectile (Fireball/Inferno) → 자기 측 GameObject + RenaBossProjectile 생성
2. host 측에서 데미지 권위 처리 (기존)
3. ⭐ 직후 BroadcastVisualSpawnIfHost → PlayerMovementSync.BroadcastBossProjectileSpawnClientRpc 발사
4. 게스트 client 수신 → 자기 측 RenaBossSpellCombatController 인스턴스 lookup → SpawnVisualOnlyProjectile
5. 게스트 측 visual-only GameObject 생성 + RenaBossProjectile.SetVisualOnly(true)
6. 게스트 측 RenaBossProjectile.Update — 이동/애니메이션 진행, hit 처리 skip
7. lifetime 만료 시 자기 측 destroy
```

### 한계 (시연 후 follow-up)

- **지원 범위**: Fireball + Inferno 만. Thunderbolt / ThunderStrike / IceSweep 은 다른 spawn 시스템(area / pillar / arc) → 동일 패턴으로 별도 broadcast 필요.
- **Homing**: visual-only clone 은 homing 무시 (host 측 player Transform reference 가 게스트 측엔 다른 instance) → 직선 이동만. 시각 약간 다를 수 있지만 시연 OK.
- **Hit 시각 동기화**: host 가 destroyOnHit 으로 일찍 destroy 돼도 게스트 visual-only 는 lifetime 만료까지 화면에 남음. destroyOnHit=false 인 보스 투사체 위주라 시연 영향 미미.

### 회고 — 왜 이전에 안 했나

이번 layer 가 드러나기 전:
1. 원거리 audit 시점에 "Rena Instantiate" 까지 봤지만 "NetworkObject.Spawn 없음 → 게스트에 안 만들어짐" 결론까지 안 갔음
2. Priority 가 telegraph → 보스 visible 순서. 투사체 visual 은 noise 에 가려져 있다 보스 visible fix 후 드러남
3. 체크리스트의 "host-only 동적 GameObject 생성" 항목 미적용

시연 후 audit 1회 — 모든 보스/적 prefab 의 `Instantiate` + `new GameObject` grep 으로 NGO sync 누락 일괄 점검 권장.

---

## P1 적용 — Rena 보스 attack-cue broadcast (단기 fix)

### Context

`RenaBossSpellCombatController.Update` 가 phase (`_isPhaseTwo`) / 쿨다운 (`_nextFireballAllowedAt` 등) 을 모두 **호스트 로컬 변수**로 관리. 게스트는 host 의 ClientRpc/시각 변경만 보는데, **보스에는 telegraph broadcast 가 없어서** 호스트의 투사체 NetworkObject.Spawn 완료까지 cue 가 전달 안 됨 → 게스트 화면에서 공격이 늦게 보임.

### 일반 적 vs 보스 — 패턴 차이

| | 공격 결정 권위 | Telegraph sync |
|---|---|---|
| **일반 적** (Orc/Skeleton/Bat 등) | host 로컬 (AIBrain) | ✓ `MonsterAttackBroadcast.BroadcastTelegraph` ClientRpc 즉시 발화 |
| **Bertha 보스** | host 로컬 | ✓ `BerthaAreaAttackController` 가 broadcast 호출 |
| **Rena 보스** (전) | host 로컬 | ❌ broadcast 호출 부재 → 게스트 cue 누락 |

**보스 prefab 자체엔 이미 `MonsterAttackBroadcast` 컴포넌트가 부착됨** (RenaRoot.prefab 등) — 에디터 작업 불필요, 코드에서 호출만 추가.

### 적용 내용

`Assets/_Project/Scripts/Runtime/Enemies/Boss/Rena/RenaBossSpellCombatController.cs`:

1. `MonsterAttackBroadcast` cached resolve (`ResolveAttackBroadcast`)
2. `StartCast(kind)` 진입부에서 `BroadcastCastTelegraph(kind, duration, releaseDelay)` 호출
3. cast 5종 모두 종류별 telegraph 발화:
   - **Fireball** → Circle, 1.6×1.6, 주황
   - **Inferno** → Circle, 2.2×2.2, 진주황
   - **IceSweep** → Box, `iceSweepAreaSize` 전체, 시안 (영향 영역 정확히 표시)
   - **ThunderStrike** → Circle, 2.2×2.2, 노랑
   - **Thunderbolt** → Box, 길이/너비 비율, 노랑

`warning duration = release delay` 로 설정 → 실제 공격 발사 시점까지 게스트도 telegraph 보임 → RTT + cast wind-up 흡수.

### 효과

- 게스트가 보스 cast 시작 즉시(RTT 만) telegraph 봄. 투사체 NetworkObject 도착까지 자연스러운 wind-up.
- Bertha 보스 / 일반 적과 동일한 패턴 — 검증된 코드 재사용.
- `BroadcastTelegraph` 내부에 `!IsServer` 가드 있어 게스트 측에서 컨트롤러가 enabled 인 경우라도 안전.

### 변경 파일

- `Assets/_Project/Scripts/Runtime/Enemies/Boss/Rena/RenaBossSpellCombatController.cs`

---

## Follow-up (이 세션 외 — 시연 후 권장)

### P1 잔여: 보스 phase/cooldown NetworkVariable sync

본 세션의 broadcast 는 cue 전달 — phase 전환 / cooldown 자체는 여전히 호스트 로컬. 게스트가 보는 보스 행동 자체는 ClientRpc/NetworkTransform 동기화 시각만으로 추적.

근본 해결: `_isPhaseTwo`, `_nextFireballAllowedAt` 등을 NetworkVariable 로 sync. 게스트도 자체 cooldown timer 진행 가능 → 더 부드러운 sync.

비용 1~2일, 회귀 위험 중간. 시연 후 검토.

### P1 잔여: Bertha 보스 audit

Bertha 의 `BerthaAreaAttackController` 는 broadcast 적용됨. `BerthaDashAttackController`, `BerthaBossSpellCombatController` 등 다른 컨트롤러도 동일 점검 필요. 사용자가 Bertha 시연 시 동일 지연 보고하면 같은 패턴으로 처리.

### P2: RoomClearedBroadcast 4인 신뢰성

4인 네트워크 부하 시 `NetworkManager.IsListening` false 또는 RPC 큐 지연 가능성. retry/fallback 도입 검토.

### P2: MonsterHealthSync despawn 타이밍

`deathDespawnDelay` 후 server 만 Despawn — 4인에서 게스트 간 시각 종료 타이밍 jitter 가능. 시연 영향 미미.

### 무기 시스템 리팩터링

지금은 NO 권장. 시연 후, 다음 룰만 강제:
- **데미지 적용은 `PlayerDamageRelay` 단일 entrypoint 강제** — 직접 `health.Damage()` 호출 금지. 친아군/오너십/네트워크 가드를 한 곳에 모음.
- 무기별 컨트롤러는 그대로 두고 마지막 "데미지 적용" 단계만 통일.

전면 재설계(SO weapon registry, IAttack 인터페이스) 는 ROI 낮음.

---

## Critical Files Modified

- `Assets/_Project/Scripts/Runtime/Combat/CombatTargetable.cs`
- `Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs`
- `Assets/_Project/Scripts/Runtime/Networking/Player/PlayerDamageRelay.cs`
- `Assets/_Project/Scripts/Runtime/Networking/Player/AttackBroadcast.cs`
- `Assets/_Project/Scripts/Runtime/Stage/Data/RoomEncounterAnchor.cs`
- `Assets/_Project/Scripts/Runtime/Stage/BossCurrentPositionStartTrigger.cs`
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiMeteor.cs`
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiFlameZone.cs`
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiParryDamageOnTouch.cs`
- `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAI.cs`
- `Assets/_Project/Scripts/Runtime/Tarot/TarotCards.cs`

## Reuse 대상 (기존 utility)

- `CombatTargetable.CanBeAutoTargetedEnemy()` — MagicalGirl 계열이 이미 사용. 본 세션에서 안전벨트 강화.
- `PlayerDamageRelay.RelayDamage()` — 친아군 가드의 이상적 단일 entrypoint. 향후 모든 player 데미지 경로가 이걸로 모이도록 유도.
- `PlayerHealthSync` / `PlayerMovementSync` — IsFriendlyPlayer sentinel.

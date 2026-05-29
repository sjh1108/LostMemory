# 2026-05-23 — Unity 에디터 적용 / 검증 가이드

본 세션 코드 변경 ([2026-05-23-multi-friendly-fire-and-spawn-fix.md](2026-05-23-multi-friendly-fire-and-spawn-fix.md)) 의 검증 절차. Unity 에디터에서 빌드/실행 시 따라할 체크리스트.

---

## 0. 사전 점검 — 컴파일

에디터 재실행 또는 스크립트 변경 감지 후 콘솔에서:

- [ ] 컴파일 에러 0건
- [ ] `using LostMemory.Combat;` 누락 경고 없음 (이번에 추가된 파일 9곳)
- [ ] `using LostMemory.Networking.Player;` 누락 경고 없음 (`CombatTargetable.cs` 추가됨)

에러 발생 시 가장 흔한 원인:
- asmdef reference 누락 — `LostMemory.Combat` asmdef 가 `LostMemory.Networking.Player` 를 reference 하는지 확인. 신규로 `PlayerMovementSync` 도 참조하기 시작했음.

---

## 1. PlayerHealthSync prefab 부착 점검 (선택 — 안전벨트 작동 중이므로 시연 필수 아님)

본 세션의 `IsFriendlyPlayer` 헬퍼는 3개 sentinel(PlayerHealthSync / PlayerMovementSync / IsPlayerObject) OR 로 강건하지만, 명시성 측면에서 PlayerHealthSync 도 부착해두면 더 안전.

### 점검 대상 prefab

- `Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` (멀티 표준 PlayerPrefab, GUID `4d290d1fc0f84526999c421c61df5d8f`)
- `Assets/_Project/Prefabs/Characters/TestKhi_Net_AD.prefab`

### 절차

1. 위 prefab 을 Project 창에서 열기.
2. Inspector 에서 `PlayerHealthSync` 컴포넌트 존재 여부 확인.
3. 없으면 Add Component → `PlayerHealthSync` 추가.
4. Inspector 에서 `health` 필드에 같은 GameObject 의 `Health` 컴포넌트 drag-assign.
5. 씬 인스턴스에 override 가 있으면 Apply to Prefab.

PlayerHealthSync 부착이 없어도 `PlayerMovementSync` 가 부착되어 있으면 IsFriendlyPlayer 가 잡으므로 기능은 동작. 그래도 인스펙터에 명시되면 명확함.

---

## 2. 호스트 + 게스트 멀티 빌드 smoke test

### 2-1. 컴파일 에러 없이 빌드 성공

빌드 후 실행 환경 2-4개 띄우기 (에디터 1개 + 빌드 인스턴스 N개).

### 2-2. 친아군 차단 (P0 — 시연 필수)

다음 모든 조합을 시도해 **팀원 데미지 0** 확인:

| 공격자 → 대상 | Khi 근접 | Khi 활 | Khi 메테오 | Khi 화염장판 | MagicalGirl 투사체 | MagicalGirl AOE |
|---|---|---|---|---|---|---|
| Host → Guest1 | ✅ 차단 | ✅ | ✅ | ✅ | ✅ | ✅ |
| Guest1 → Host | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Guest1 → Guest2 (3인) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Guest1 → Guest3 (4인) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

각 조합에서:
- [ ] 대상 player 의 HP 가 줄지 않음
- [ ] 히트 VFX 도 발생 안 함 (가드가 데미지 전에 차단되므로 자연스럽게 시각도 막힘)
- [ ] 콘솔에 `FriendlyFire blocked` 로그 1회 이상 출현
  - `[PlayerDamageRelay] FriendlyFire blocked at RelayDamage — target=...` — 호출자 측 차단
  - `[PlayerDamageRelay] FriendlyFire blocked at ServerRpc — target=... requester=...` — ServerRpc 우회 시도 차단 (이게 출현하면 root cause 가 실제로 잡힌 신호)
  - `[AttackBroadcast] FriendlyFire blocked at DamageRequest — target=...`

### 2-3. 일반 적 / 보스 데미지 정상 (회귀 검증)

- [ ] 모든 player 가 일반 몹에게 정상 데미지
- [ ] 보스 데미지 정상
- [ ] 보스 → player 데미지 정상 (`IsFriendlyPlayer` sentinel 은 player 에만 부착되므로 enemy → player 방향은 영향 없어야 함)
- [ ] 콘솔에 `FriendlyFire blocked` 가 enemy/보스 대상으로 오발사 안 됨

---

## 3. 방 스폰 위치 (P0 — 시연 필수)

### 3-1. 일반 방

각 방 진입 시 적이 **방 안의 SpawnPoint Transform 위치**에 정확히 소환되는지:

- [ ] 1F-1R: 정상
- [ ] 1F-2R: 정상
- [ ] **1F-3R: 정상** (이전 버그 발생 지점)
- [ ] **1F-4R: 정상** (이전 버그 발생 지점)
- [ ] **2F-1R ~ 2F-4R: 정상** (이전 버그 발생 지점)

이상한 위치(방 밖, 다른 방 좌표, world origin 등) 에 소환되면 fix 미작동 신호.

### 3-2. 보스 방

- [ ] 보스가 방 중앙 또는 인스펙터 지정 위치에 정상 소환
- [ ] 보스 방 안에 같이 소환되는 zako (있다면) 정상 위치

---

## 4. 보스 룸 진입 권위 (P0 — 시연 필수)

### 4-1. 게스트 먼저 진입 시나리오

1. Host + Guest1 빌드 실행, 같은 던전 진행.
2. **Guest1 이 host 보다 먼저** 보스 trigger 영역에 진입.
3. 확인:
   - [ ] Guest1 화면에서 보스 인카운터 즉시 시작되지 않음 (host 가 밟을 때까지 대기)
   - [ ] Host 가 trigger 영역 진입 시 그 시점에 양쪽 모두 보스 시작
   - [ ] 보스 BGM / intro / 공격이 host + guest 양측에서 정상 시작

### 4-2. 호스트 먼저 진입 (회귀)

- [ ] Host 가 먼저 trigger 밟으면 기존과 동일하게 즉시 시작 (회귀 없음)

---

## 5. 회귀 검증 체크리스트

- [ ] 솔로 플레이 데미지 정상 (`NetworkManager.IsListening == false` 분기 작동)
- [ ] Phase E 의 검증 항목 회귀 없음 — `multiplayer_fixes_phase_E.md` 의 verification 섹션 재확인
- [ ] WeaponModeController 모드 전환 양측 sync (Phase B)
- [ ] MagicalGirl T 스킬 host-only spawn (Phase C)
- [ ] PlayerHealthSync 데미지 sync 정상 (Phase A)
- [ ] per-client inventory 표시 (사용자 명시 회귀 금지)

---

## 6. 트러블슈팅

### 컴파일 에러: `The type or namespace name 'PlayerMovementSync' could not be found`

`CombatTargetable.cs` 가 `LostMemory.Networking.Player` 를 reference 하는데 asmdef 의존이 빠진 경우. asmdef 파일을 열어 `references` 에 `LostMemory.Networking.Player` (또는 통합 asmdef 이면 자동) 추가.

### 친아군 차단이 작동 안 함 (HP 가 닳음)

1. 콘솔에서 `FriendlyFire blocked` 로그가 한 번이라도 찍히는지 확인 — 안 찍히면 가드 자체가 호출 안 됨.
2. 해당 player prefab 의 root 에 다음 중 하나라도 부착됐는지 확인:
   - `PlayerHealthSync`
   - `PlayerMovementSync`
   - `NetworkObject` 가 `SpawnAsPlayerObject` 로 spawn 됨 (NetworkManager 설정 확인)
3. 세 sentinel 모두 부재면 IsFriendlyPlayer 가 false 반환 → 가드 통과. 적어도 하나는 반드시 있어야 함.
4. 어떤 데미지 경로에서 새는지 모르면 `PlayerDamageRelay.verboseLog` true 로 두고 콘솔에서 `Damage ... -> <대상이름>` 로그 추적.

### 1F-3R 적이 여전히 이상한 위치

1. 해당 방의 layout prefab 인스펙터에서 `RoomEncounterAnchor` 컴포넌트의 `cachedSpawnPoints` 가 비어 있어도 됨 — Awake 에서 자동 refresh.
2. 자식에 `RoomEncounterSpawnPoint` 컴포넌트가 실제로 부착된 자식 GameObject 가 있는지 확인. 없으면 적이 spawn 될 위치가 없음.
3. Context Menu → `Refresh Spawn Points` 수동 실행 후 인스펙터에서 결과 확인.

### 보스 룸 진입 안 됨

1. Host 측 콘솔에 `[BossCurrentPositionStartTrigger] Starting boss ...` 로그 출현 여부.
2. 출현 안 하면 host 측 player Collider 가 trigger 영역과 실제로 겹치는지 확인 (게스트 player 위치 sync 가 정상인지).
3. `debugLogging = true` 로 두면 더 자세한 로그.

---

## 6.5. BossStartTrigger 그룹 텔레포트 (P0 후속 — 이번 세션 적용됨)

### 6.5-1. 검증 시나리오

#### 시나리오 A: 게스트가 먼저 trigger 진입

1. Host + Guest1 (가능하면 Guest2 까지) Town_Preview.unity 로 입장.
2. **Guest1 이 host 보다 먼저** Town 내 BossStartTrigger 영역 진입.
3. 확인:
   - [ ] **즉시** host + guest 양쪽 화면에서 모든 player 가 boss entry point 로 그룹 텔레포트
   - [ ] 텔레포트 후 host 가 boss intro / 보스 인카운터 정상 시작
   - [ ] 게스트 측 텔레포트가 자연스러움 (튕김 / 다시 돌아감 없음)

#### 시나리오 B: Host 가 먼저 진입 (회귀)

- [ ] host 가 먼저 밟아도 동일하게 모든 player 그룹 텔레포트
- [ ] 기존 솔로 시나리오 회귀 없음

### 6.5-2. 콘솔 로그

- Host 콘솔: `[BossCurrentPositionStartTrigger] Gathered N player(s) before boss intro.` 출현
- `[Player] RequestBossStartServerRpc — scene 에 BossCurrentPositionStartTrigger 없음.` 경고 안 나야 정상

### 6.5-3. 트러블슈팅

- **게스트 trigger 밟아도 무반응**: Host 콘솔에 `Gathered ... player(s)` 안 나오면 ServerRpc 도달 실패 — NetworkManager.IsListening 확인.
- **텔레포트 후 게스트가 원래 위치로 돌아옴**: owner-auth 위반 — `TeleportPlayerClientRpc` 안 `IsOwner` 가드 확인.
- **씬에 BossStartTrigger 가 여러 개**: 현재 host 는 첫 번째 인스턴스만 사용. 시연 후 trigger 식별자 인자 추가 필요.

### 6.5-4. Unity editor 작업

**없음** — `PlayerMovementSync` 가 이미 모든 player prefab 에 부착되어 있어 메서드 추가만으로 동작.

### 6.5-5. Reward panel 이동 지연 검증

#### 시나리오

1. Host + Guest1 같은 던전 진행.
2. 클리어 보상으로 reward panel 이 양쪽에 표시되도록 유도.
3. **Guest1 이 reward panel 안 받은 상태**에서 Host 가 먼저 BossStartTrigger 영역 진입.

확인:
- [ ] Guest1 화면: reward panel 표시 유지. Boss 영역으로 텔레포트 안 됨.
- [ ] Guest1 이 reward 카드 선택 후 panel 자동 닫힘 → 그 시점에 즉시 Boss 영역으로 텔레포트.
- [ ] Host 화면: Guest1 늦게 합류해도 정상 (Host 가 먼저 Boss intro 시작).

#### 반대 시나리오

- Reward panel 떠있는 state 에서 자기가 직접 trigger 밟으면 → 발화 자체 차단. 콘솔: `[BossCurrentPositionStartTrigger] Reward panel 활성 상태 — boss trigger 발화 보류.` 출현.
- Reward 카드 선택 → panel 닫힘 → 다시 trigger 영역 들어가면 정상 발화.

#### 회귀 검증

- [ ] Reward panel 닫힌 상태에서 trigger 밟으면 즉시 그룹 텔레포트 (지연 없음)
- [ ] 솔로 플레이 reward panel 정상 동작 — 카드 선택 후 자연스럽게 다음 진행

---

## 6.6. Rena 보스 게스트 측 invisible fix (P0 후속 — 이번 세션 적용됨)

### 6.6-1. 검증 시나리오

1. Host + Guest1 보스 방 입장 (BossStartTrigger 영역 진입)
2. 확인:
   - [ ] **호스트 화면**: 보스 등장 연출 정상 (entry animation, drop motion)
   - [ ] **게스트 화면**: 호스트와 거의 동일 시점에 보스 등장 (RTT 만큼 늦음, 보통 50~150ms)
   - [ ] 보스 sprite 정상 표시 (host + guest 둘 다)
   - [ ] 보스 공격 시 양측 화면에서 보스 visible 유지

### 6.6-2. 회귀 검증

- [ ] 호스트 단독(솔로) 플레이 시 등장 연출 정상 — `BroadcastIntroVisibilityIfHost` 가 `NetworkManager.IsListening` false 면 return
- [ ] Bertha 보스 등장 정상 (같은 BossIntroSequenceController 사용)
- [ ] 빌더 재실행(`Lost Memory/Enemies/Build Rena Boss Prototype`) 후에도 정상 — broadcast 패턴이 ScriptableObject `hideBossBeforeEntry` 값과 무관

### 6.6-3. 트러블슈팅

- **게스트 화면 여전히 안 보임**: 콘솔에 `[Netcode] [RenaRoot][NetworkAnimator][isActiveAndEnabled: False]` 경고 spawn 시점만 1회 출현하면 OK. intro reveal 후엔 활성. 게스트 콘솔에서 `BroadcastBossIntroVisibilityClientRpc` 호출 stack trace 확인.
- **호스트도 안 보임**: ServerRpc 위임 자체가 실패 — host 콘솔 `[BossCurrentPositionStartTrigger] Starting boss ...` 로그 확인.

### 6.6-4. Unity editor 작업 — 없음

- BossIntroSequenceController 는 RenaRoot.prefab 에 이미 부착
- PlayerMovementSync 는 모든 player prefab 에 이미 부착
- RenaBossIntroSequenceData.hideBossBeforeEntry 토글 변경 불필요 (등장 연출 유지)

### 6.6-5. RenaBossPrototypeBuilder 관련 주의

빌더 (`Lost Memory/Enemies/Build Rena Boss Prototype`) 가 `hideBossBeforeEntry = true` 를 강제 직렬화하지만, 본 fix 는 그 설정과 무관하게 멀티 sync 보장. 빌더 코드 line 702 수정 불필요.

---

## 7. Rena 보스 attack-cue telegraph (P1 — 이번 세션 적용됨)

### 7-1. 검증 절차

1. Host + Guest1 보스 방 진입.
2. **게스트 화면에서** 각 cast 시작 시 telegraph 시각이 즉시 보이는지 확인:

| 공격 | Telegraph 모양 | 색 | 위치 |
|---|---|---|---|
| Fireball | 원형 | 주황 | 보스 위치 |
| Inferno | 원형 (큼) | 진주황 | 보스 위치 |
| IceSweep | 사각 (큼) | 시안 | iceSweep 영역 중앙 |
| ThunderStrike | 원형 | 노랑 | 보스 위치 |
| Thunderbolt | 사각 (길쭉) | 노랑 | 보스 위치 |

3. 각 telegraph 가 cast release 시점에 자연스럽게 hide → 실제 공격 시각으로 전환.
4. host/guest 양측에서 telegraph 동시 표시.

### 7-2. 회귀 검증

- [ ] 호스트 단독(솔로) 플레이 시 보스 telegraph 가 **중복** 표시되지 않음 (host 측은 자기 enemy controller 의 원본 visual + ClientRpc clone 분기 처리)
  - `MonsterAttackBroadcast` 의 `IsHost return` 분기로 차단됨 — 호스트 측 ClientRpc 도착 시 SpawnVisualClone skip.
- [ ] Bertha 보스 telegraph 회귀 없음 (이미 broadcast 적용된 시스템 동일)
- [ ] 솔로 (NetworkManager 비활성) 환경에서 telegraph 정상 표시 (`BroadcastTelegraph` 가 networkActive=false 일 때 즉시 return — solo 는 controller 의 원본 telegraph view 가 처리)

### 7-3. 트러블슈팅

**게스트 화면에 telegraph 안 보임**:
- 콘솔에 `[DiagTelegraph-Spawn]` (host) 와 `[DiagTelegraph-RpcRecv]` (guest) 양쪽 로그 모두 출현하는지 확인.
- Host 측만 출현하면 ClientRpc 전송은 됐는데 게스트가 못 받는 것 — NetworkObject sync 문제.
- Guest 측 로그도 출현하는데 시각이 안 뜨면 `MonsterAttackBroadcast.telegraphVisualPrefab` 또는 runtime 자동 생성 visual 에 sortingLayer 문제 가능. 다른 일반 적 telegraph 와 비교.

**Telegraph 모양이 어색하거나 위치가 안 맞음**:
- `RenaBossSpellCombatController.BroadcastCastTelegraph` 의 switch case 에서 각 cast 종류별 size/color/worldPos 조정.
- IceSweep 의 경우 `ResolveIceSweepAreaCenter()` 값이 정확한지 확인.

---

## 8. 시연 직전 권장 추가 검증

- [ ] **(P1) 보스 공격이 게스트에서 늦게 보이는 정도 — telegraph 적용 후 재측정** — host 좌측 / guest 우측 동시 화면녹화. telegraph cue 가 RTT (보통 50~150ms) 만 늦게 보이면 OK. release 시점에 투사체가 자연스럽게 나타나면 시연 안정.
- [ ] **(P2) 4인 RPC 부하** — 4인 동시 공격/스킬 난사 시 콘솔에 `Receive queue is full` 경고 빈도. 1분간 0회 목표.
- [ ] **(권장) Editor PlayMode 가 아닌 빌드 인스턴스로 검증** — 일부 NGO timing 은 빌드/에디터 차이 있음.

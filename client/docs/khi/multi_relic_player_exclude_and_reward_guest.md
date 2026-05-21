# 멀티 — Player 자동공격 제외 + Guest Reward 패널 표시 수정

## Context

멀티플레이 테스트 중 두 가지 문제 확인됨:

1. **호스트의 미소녀(MagicalGirl)/relic 자동공격이 게스트 player 를 공격함.**
   사용자 직접 목격: host 화면에서 자기 미소녀가 guest 캐릭터를 추적/타격.
   원인: `Character.CharacterType == AI` 가드는 존재하지만, `PlayerMovementSync.convertNonOwnerToAi=true` (B-2) 로 인해 **host 측에서 guest 캐릭터의 CharacterType 이 AI 로 변환됨** → 가드 무력화.
   동일 결함 가능성은 host↔guest 양방향 (단, 실제 본 케이스는 host→guest 만).

2. **방 클리어 시 guest 화면에 보상 패널이 전혀 안 뜸.**
   A-3 (RoomClearedBroadcastClientRpc) 는 정상 구현됐고, host 측은 패널 정상 표시. guest 만 패널 자체 미표시 → `RewardController.ShowReward()` 도달 실패 또는 `ResolveRewardPanelView()` 실패 의심.

목표: (1) MagicalGirl + chain/wind on-hit 효과에서 player(host & guest 모두) 확실히 제외, (2) guest 가 보상 패널을 정상으로 띄우고 클릭으로 inventory 적용까지 완료, (3) reward 클릭 후 inventory sync 가 의도대로 per-client 로 작동하는지 검증.

---

## Phase A — Player 제외 가드 강화

### 핵심 파일
- `Assets/_Project/Scripts/Runtime/Combat/CombatTargetable.cs` — 헬퍼 추가
- `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAI.cs` (line 150-179, `FindClosestEnemy`)
- `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAOE.cs` (line ~114-116)
- `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlProjectile.cs` (line ~84-87)
- `Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs` (line 317 `ApplyWindBlade`, line 490 `ApplyChain`)

### 변경 사항

1. **CombatTargetable.cs 에 static 헬퍼 추가**:
   ```csharp
   public static bool IsAuthoritativePlayer(GameObject go)
   {
       if (go == null) return false;
       // CharacterType 검사 (싱글 환경 / host 자기 캐릭터)
       Character ch = go.GetComponentInParent<Character>();
       if (ch != null && ch.CharacterType == Character.CharacterTypes.Player) return true;
       // NetworkObject.IsPlayerObject 검사 (멀티 환경에서 host 측 게스트 캐릭터)
       var no = go.GetComponentInParent<Unity.Netcode.NetworkObject>();
       if (no != null && no.IsPlayerObject) return true;
       return false;
   }
   ```
   - 첫 번째 가드는 기존 동작 보존 (싱글플레이 / host 자기 PlayerObject).
   - 두 번째 가드는 멀티 핵심: B-2 의 AI 변환을 회피하기 위해 NGO 의 `IsPlayerObject` 로 player 식별.

2. **5개 callsite 일괄 교체**: 기존의 `CharacterType != AI` 또는 `CharacterType == Player` skip 가드를 `CombatTargetable.IsAuthoritativePlayer(go) → skip` 호출로 통일.
   - 기존 `CharacterType` 체크는 **유지**해도 무방 (헬퍼 안에 포함됨). 헬퍼만 추가 호출.
   - 안전망 — `NetworkObject` null 인 싱글환경에선 `CharacterType` 만으로 fall back.

### 영향 범위 — 회귀 위험 평가

- 싱글플레이: `NetworkObject` 없음 → 두 번째 가드 자동 skip → 기존 `CharacterType` 가드만 작동 → 동작 동일.
- 보스 / 적 AI 의 player 추적: `CombatTargetable.IsAuthoritativePlayer` 는 player 자기편 공격 시스템에만 추가됨. 보스 추적 로직은 **이 helper 호출하지 않음** → 영향 없음.

---

## Phase B — Guest Reward 패널 표시 진단/수정

### 핵심 파일
- `Assets/_Project/Scripts/Runtime/Stage/RewardController.cs`
  - `HandleRoomClearedFromController` (line ~126)
  - `ShowReward` (line ~176-223)
  - `ResolveRewardPanelView` (line ~338-361)
  - `ResolveRefs`
  - 진단용: `[SerializeField] private bool logRewardFlow = true;` 강제 ON
- `Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs:466-483` (참조만, 변경 안 함)

### 1단계 — 진단 빌드 (수정 전)

1. `RewardController.logRewardFlow = true` 강제 ON.
2. 추가 디버그 로그 임시 삽입 (커밋 안 함):
   - `HandleRoomClearedFromController` 진입 직후 — `IsServer/IsClient/IsHost` 함께 로깅
   - `ShowReward` 진입 직후 — `rewardPanelView != null`, `playerRelicInventory != null`, `LocalPlayerResolver.LocalCharacter != null` 로깅
   - `ResolveRewardPanelView` 의 lazy resolve 결과 로깅
3. 빌드 → host + guest join → 방 클리어
4. **Guest Player.log** 의 `[Reward]` 또는 `[RewardController]` 라인 추출 → 어디서 멈추는지 식별:
   - `HandleRoomClearedFromController` 까지 안 옴 → RoomClearedBroadcast 이벤트 미발화 (A-3 ClientRpc 회귀)
   - 진입했지만 `RoomType != Combat` 또는 `HasSpawnedEnemies == false` 로 early return → 게스트 측 RoomEntryRuntimeController 상태 sync 누락
   - `ShowReward` 진입했지만 `rewardPanelView == null` → 패널 resolve 실패
   - `ShowReward` 진입했지만 `playerRelicInventory == null` → inventory resolve 실패
   - 모두 통과했는데 `Show()` 호출 안 됨 → RewardPanelView GameObject 자체가 inactive 또는 destroy

### 2단계 — 원인별 수정 (1단계 결과 따라 분기)

후보별 픽스:

- **panelView 또는 inventory null** → `ResolveRewardPanelView` / `ResolveRefs` 강화:
  - `FindFirstObjectByType<RewardPanelView>(FindObjectsInactive.Include)` 로 inactive 도 검색
  - `LocalPlayerResolver.LocalCharacter` 기준 같은 씬의 인스턴스 우선
  - DontDestroyOnLoad 컨텍스트 검사
- **RoomType / HasSpawnedEnemies 미동기화** → `RoomEntryRuntimeController` 의 spawned-count 가 guest 에게 broadcast 되는 값(`spawnedEnemyCount`) 으로 재계산하도록 변경. ClientRpc 인자 활용.
- **이벤트 핸들러 등록 실패** → `RewardController.OnEnable` 에서 `RoomEntryRuntimeController` 인스턴스 lazy resolve 시점 race 확인. `Update` 에서 재시도 또는 NGO 의 OnNetworkSpawn 패턴 차용.

### 3단계 — 영구 로그 정리

- 진단 로그 중 유의미한 것만 유지 (state log 위주)
- `logRewardFlow` 는 default false 로 되돌리되 옵션은 남김

---

## Phase C — Reward 클릭 → Inventory 적용 검증 (조건부 수정)

### 사전 조사 결과 (변경 안 해도 될 가능성 높음)

- `RewardPanelView.OnCardSelected` (line 150) → `_inventory.TryAdd()` 직접 호출
- `PlayerRelicInventory.TryAdd` (line 217) → 로컬 `_ownedRelics` / `_grid` 수정
- **per-client 독립 처리, ServerRpc 없음** — F-1 의 골드/파편 패턴과 동일
- 4인 개별 보상 정책 (사용자 초기 요구) 과 일치 → **의도된 동작**

### 검증 항목 (Phase B 끝나고 guest 패널 정상 표시되면 수행)

1. Guest 가 패널의 한 카드 클릭
2. **확인**: guest 측 `PlayerRelicInventory._ownedRelics` 에 추가됨
   - 인벤토리 UI 새로고침 됨
   - SetEffectApplicator / BuildManager 가 새 relic 효과 적용
3. **확인**: host 측 inventory 는 영향 없음 (개별 보상 정책)
4. **확인**: host 도 본인 패널에서 별도 선택 가능, 자기 인벤토리에 추가

### 수정이 필요한 경우 (검증 실패 시)

- guest 클릭 후 inventory 추가가 안 되면 — `RewardPanelView` 가 host 의 `PlayerRelicInventory` 를 참조하는지 확인 (LocalPlayerResolver 로 local player inventory 만 잡도록 수정)
- guest 클릭이 host inventory 에도 영향 주면 (의도와 다름) — inventory 인스턴스가 local-only 인지 NetworkBehaviour 인지 확인. 만약 NGO Sync 객체면 분리 필요.

수정 위치 후보:
- `RewardPanelView` 의 `_inventory` 필드 resolve 로직
- `RewardController.ResolveRefs` 에서 `playerRelicInventory` 잡는 부분

---

## 작업 순서 권장

1. Phase A 먼저 (작고 위험 낮음, 즉시 효과)
2. Phase B 진단 빌드 → log 분석 → 수정
3. Phase B 완료 후 Phase C 검증 → 필요 시 수정

각 phase 마다 빌드/테스트 사이클 1회씩 = 총 빌드 3회 예상.

---

## 검증 시나리오 (end-to-end)

빌드 후 host + MPPM guest (또는 빌드 2개) 시나리오:

1. Title → 로그인/우회 → Town_solo_Copy → 자기 캐릭터 보임
2. MultiLobby portal → Test_MultiLobby_Copy → host 가 Host 클릭, guest 가 Join
3. 두 명 모두 LoadScene trigger 진입 → Dungeon_1F_1R 자동 전환
4. 방 enemy 처치 후 EntryZone 진입 → 보상 패널 표시
5. **체크리스트**:
   - [ ] host 미소녀가 enemy 만 추적 (guest 캐릭터 무시) — Phase A
   - [ ] guest 캐릭터의 ChainOnHit / WindAOE 효과도 host 캐릭터 무시 — Phase A
   - [ ] 방 클리어 시 host 화면에 reward 패널 표시 ✓ (이미 작동)
   - [ ] **방 클리어 시 guest 화면에도 reward 패널 표시 — Phase B 핵심**
   - [ ] guest 가 카드 클릭 시 guest 자기 인벤토리에 relic 추가 — Phase C
   - [ ] host 카드 선택은 guest inventory 에 영향 없음 (per-client 보상) — Phase C
6. Player.log 양쪽 확인:
   - 새 가드 작동 로그 (선택적으로 debug log 남기면)
   - `[RewardController] ShowReward(...)` host & guest 양쪽에 찍힘
   - 회귀 에러 없음

---

## 비-목표 (Out of Scope)

- 보스(Bertha / Rena) 의 자동 추적은 enemy → player 가 정상 동작 → 가드 적용 안 함
- 상점(Shop) 의 multiplayer 가드는 A-2 에서 완료 — 이번 작업 범위 X
- Late-Join (F-3) 재접속 시 보상 누락 처리는 별건 — 이번 작업 X
- NetworkAnimator OnValidate NRE — Editor only 노이즈, 런타임 무관 → 이번 작업 X

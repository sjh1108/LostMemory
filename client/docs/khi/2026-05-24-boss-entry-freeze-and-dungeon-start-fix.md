# 2026-05-24 보스방 진입 게스트 멈춤 fix + Dungeon_1F_2R 시작 방 fix + F12 비상탈출 복구

> 시연 직전 작업 일지. 같은 날 이전 작업 (협력 부활 / GodMode / merge conflict) 은 `2026-05-24-multi-sync-cooperative-revive-and-godmode.md` 참조.

## 작업 범위 한 줄 요약

1. **보스방 진입 후 게스트 영구 멈춤** — `UnfreezePlayerClientRpc` 의 `IsOwner` 가드가 mirror 측 unfreeze 차단, ClientRpc race 시 복구 path 0
2. **F12 비상 탈출 안 먹음** — `EmergencyTimeScaleRecovery.Init()` 의 자동 등록 어트리뷰트가 주석 처리되어 F12 polling driver 자체가 안 생성됨
3. **Dungeon_1F_2R 시작 방 잘못됨** — `Map_1F_2R_2_NWE_Hall` 로 spawn (의도: `Map_1F_2R_1_E_EP`)
4. **진단 로그 인프라** — `[BossEntry]` / `[Freeze]` / `[Unfreeze]` / `[BossIntro]` / `[EmergencyRecovery]` prefix 로 Player.log grep 친화

## 변경 파일 (5개, prefab 0, scene 0)

| 파일 | 종류 | 핵심 |
|---|---|---|
| `Stage/EmergencyTimeScaleRecovery.cs` | 수정 | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` **주석 해제** + Driver heartbeat / F12 pressed 로그 |
| `Networking/Player/PlayerMovementSync.cs` | 수정 | `UnfreezePlayerClientRpc` 의 **`IsOwner` 가드 제거** + `[Unfreeze]` `[Freeze][Teleport]` `[BossEntry]` 로그 |
| `Stage/BossIntroSequenceController.cs` | 수정 | `[BossIntro] step=...` 로그 + **0.5s 재송신** + **5s watchdog** + Freeze/Unfreeze count 로그 |
| `Stage/BossCurrentPositionStartTrigger.cs` | 수정 | `[BossEntry] OnTriggerEnter2D` / `Gathered N player(s)` 로그 |
| `Stage/DungeonRunBootstrap.cs` | 수정 | `OnSpawnedManagedObjects` 를 **2-pass** 로 — Pass 1 `_EP` suffix 우선, Pass 2 fallback (기존 동작) |

---

## 1. 보스방 진입 후 게스트 영구 멈춤

### 증상
- 게스트가 Rena 보스방 trigger 진입 시 텔레포트 후 **영구 freeze**
- 호스트는 정상
- 강제 종료 외 복구 불가

### 근본 원인
`PlayerMovementSync.UnfreezePlayerClientRpc` 의 `IsOwner` 가드:
```csharp
[ClientRpc]
public void UnfreezePlayerClientRpc()
{
    if (!IsOwner) return;   // ← 가드
    ...
}
```

흐름:
1. 게스트 trigger 진입 → `RequestBossStartServerRpc`
2. 호스트 `BossCurrentPositionStartTrigger.GatherPlayers` → `TeleportPlayerClientRpc(pos, freezeAfter=true)` 모든 player → 게스트 freeze
3. 호스트 `BossIntroSequenceController.RunIntroSequence` → 인트로 진행
4. intro 완료 → `UnfreezeCachedPlayers` → `BroadcastUnfreezeIfHost` → 모든 `UnfreezePlayerClientRpc`
5. **실패**: ClientRpc 도달 못 함 (NetworkObject IsSpawned race) 또는 `IsOwner` 체크에 mirror 측 차단 → 영구 freeze

### 해결 — 3중 안전망
| 안전망 | 효과 |
|---|---|
| **IsOwner 가드 제거** | mirror 인스턴스도 unfreeze 호출 — `Character.UnFreeze()` 는 Frozen state 체크 후 처리라 idempotent 안전 |
| **0.5s 재송신** | `ResendUnfreezeAfterDelayCoroutine` — ClientRpc race 자동 복구 |
| **5s watchdog** | `UnfreezeWatchdogCoroutine` — 5초 후 여전히 Frozen 인 Player 강제 UnFreeze + ClientRpc 재 broadcast |

### 검증 (Player.log 양상, 실제 빌드 결과)
```
[BossEntry] OnTriggerEnter2D by 'TestKhi_MinimalCharacter2D(Clone)' isPlayer=True started=False
[BossEntry] Gathered 2 player(s), freezeAfter=True, networkActive=True — broadcast 완료.
[Freeze][Teleport] recv netId=1 owner=True freeze=True ...
[Freeze][Teleport] recv netId=4 owner=False freeze=True ...
[BossIntro] step=Start t=98.50 cachedPlayers=2
[Freeze] FreezeCachedPlayers — 2/2 frozen (host side).
[BossIntro] step=CompleteIntro t=100.58 shouldUnlock=True
[Unfreeze] UnfreezeCachedPlayers — 2/2 host-side UnFreeze.
[Unfreeze] recv netId=1 owner=True ... condition=Normal       ← 정상 unfreeze
[Unfreeze] recv netId=4 owner=False ... condition=Normal      ← ★ IsOwner=False 인 mirror 도 unfreeze (이전 가드 차단됐던 path)
[Unfreeze] BroadcastUnfreezeIfHost — sent to 2/2.
[BossIntro] step=End t=100.58
[Unfreeze] Resend after 0.50s — 재송신.
[Unfreeze][WATCHDOG] All players unfrozen after 5.0s. OK.
```

핵심 증거 — `netId=4 owner=False ... condition=Normal` 줄. 이전 빌드엔 `IsOwner` 가드에 막혀 호출 안 됐던 path. fix 후엔 정상 호출 → 멈춤 0회 재현.

---

## 2. F12 비상 탈출 복구

### 증상
F12 키를 눌러도 아무 반응 없음. 게스트 멈춤 발생 시 강제 종료 외 탈출 불가.

### 근본 원인
`EmergencyTimeScaleRecovery.Init()` 의 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 어트리뷰트가 **주석 처리** (line 31):
```csharp
// 시연 안정성 — NGO join 흐름 충돌 가능성으로 자동 등록 비활성. 필요 시 다시 활성화.
// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
private static void Init() { ... }
```

→ `EmergencyRecoveryDriver` MonoBehaviour 자동 생성 안 됨 → F12 polling Update 자체 안 일어남.

### 해결
**주석 1줄 해제**. NGO 충돌 위험 평가 결과:
- `AfterSceneLoad` 는 `NetworkManager.Awake` 이후 호출
- `Init()` 는 GameObject 1개 생성 + Update 폴링만, `NetworkManager.Singleton` 접근 0
- 위험 매우 낮음

추가 보완:
- Driver 에 5초마다 heartbeat 로그 (`[EmergencyRecovery] Driver heartbeat`) — driver 살아있는지 grep 으로 확인 가능
- F12 발화 시 명시적 로그
- New Input System + legacy Input 둘 다 가드 (`try/catch`)

### 검증
Player.log:
```
[EmergencyRecovery] Initialized — F12 강제 복구 + 씬 전환 자동 복구 활성.
[EmergencyRecovery] Driver Start — F12 polling 활성.
[EmergencyRecovery] ForceRestore 완료 (reason=scene-loaded) — players unfrozen + timeScale=1
[EmergencyRecovery] Driver heartbeat t=5.0 — F12 polling alive
[EmergencyRecovery] Driver heartbeat t=10.0 — F12 polling alive
... (계속)
```

씬 전환 시 자동 ForceRestore 작동 확인. F12 누를 경우 `[EmergencyRecovery] F12 pressed — ForceRestore 호출.` 추가됨.

---

## 3. Dungeon_1F_2R 시작 방 fix

### 증상
- `Dungeon_1F_2R.unity` 진입 시 player 가 `Map_1F_2R_2_NWE_Hall` (Hall 방) 에 spawn
- 의도: `Map_1F_2R_1_E_EP` (Entry Point, `_EP` suffix 마커)
- **다른 던전 (1F_1R, 1F_3R) 은 정상** → 코드 자체는 OK, 1F_2R 만 데이터 setup 차이

### 근본 원인
`DungeonRunBootstrap.OnSpawnedManagedObjects` (line 158~) 의 기존 로직:
```csharp
// 첫 번째 RoomEntryRuntimeController 가진 module 을 시작 방으로 무조건 선택
for (int i = 0; i < spawnedManagedObjects.Length; i++)
{
    var inst = spawnedManagedObjects[i];
    var controller = inst.GetComponentInChildren<RoomEntryRuntimeController>(...);
    if (controller != null) { WarpPlayer + BeginRoomEntry → return; }
}
```

`spawnedManagedObjects` 순서 (DA Dungeon Architect graph + scene hierarchy 기반):
| index | module | position |
|---|---|---|
| [0] | `Map_1F_2R_2_NWE_Hall` (Hall) | (34, -6) |
| [1] | `Map_1F_2R_1_E_EP` (Entry Point) | (0, 0) |

→ `[0]` 이 hall 이라 player 가 hall 로 warp. **다른 던전은 우연히 [0] 이 entry point 라서 정상 작동했던 것**.

Scene 분석:
- `_2_NWE_Hall` 은 scene line 2234 (먼저 정의), `RoomData_Combat_1F_2R_2.asset` wire ✓
- `_1_E_EP` 는 scene line 8957 (나중 정의), RoomData wire 미식별

### 해결 — 2-pass 선택 로직
```csharp
// Pass 1 — "_EP" suffix 우선 (Entry Point 명명 규칙).
for (int i = 0; i < spawnedManagedObjects.Length; i++)
{
    var inst = spawnedManagedObjects[i];
    if (inst == null) continue;
    var ctrl = inst.GetComponentInChildren<RoomEntryRuntimeController>(true);
    if (ctrl == null) continue;
    if (inst.name.IndexOf("_EP", StringComparison.OrdinalIgnoreCase) >= 0)
    {
        startModule = inst; startController = ctrl;
        Debug.Log($"[DungeonRunBootstrap] Entry Point module selected (Pass 1, _EP suffix): '{inst.name}'.");
        break;
    }
}

// Pass 2 — fallback (기존 동작).
if (startModule == null) { /* 기존 첫 controller 선택 */ }

// 이후 WarpPlayer + BeginRoomEntry (공통).
```

### 다른 던전 영향 — 0 (회귀 안 함)
- `_EP` suffix 있으면 Pass 1 매치 → 그게 시작 방 (의도와 일치)
- `_EP` suffix 없으면 Pass 2 fallback → 기존 동작 그대로

### 핵심 인사이트
**명명 규칙이 코드의 단서가 됨**. `_EP` suffix 는 단순 라벨이 아니라 "이게 entry point" 라고 코드에 알려주는 마커. 새 던전 만들 때 entry point 방 이름에 `_EP` 만 붙이면 자동으로 시작 방 인식.

---

## 진단 로그 prefix 규약

| Prefix | 의미 | 발화 위치 |
|---|---|---|
| `[BossEntry]` | trigger 진입 / ServerRpc 흐름 | `BossCurrentPositionStartTrigger`, `PlayerMovementSync` |
| `[Freeze]` | freeze 적용 (Teleport, FreezeCachedPlayers) | `PlayerMovementSync`, `BossIntroSequenceController` |
| `[Unfreeze]` | unfreeze 송신/수신, count, broadcast | `PlayerMovementSync`, `BossIntroSequenceController` |
| `[Unfreeze][WATCHDOG]` | 5초 watchdog 발동 | `BossIntroSequenceController` |
| `[BossIntro]` | intro 시퀀스 step | `BossIntroSequenceController` |
| `[EmergencyRecovery]` | F12 / ForceRestore / Driver | `EmergencyTimeScaleRecovery` |
| `[DungeonRunBootstrap]` | 시작 방 선택 / Warp | `DungeonRunBootstrap` |

### grep 명령 모음
```bash
# 모든 진단 로그
grep -E "\[BossEntry\]|\[Freeze\]|\[Unfreeze\]|\[BossIntro\]|\[EmergencyRecovery\]|\[DungeonRunBootstrap\]" Player.log

# 게스트 멈춤 진단
grep -E "\[Unfreeze\]|\[Freeze\]\[Teleport\]" Player.log

# 시작 방 결정 흐름
grep -E "\[Spike\]|Entry Point module|Fallback module|Warped player" Player.log
```

### Player.log 위치 (Standalone build)
`C:\Users\SSAFY\AppData\LocalLow\DefaultCompany\LostMemory\Player.log`

---

## NGO 안정성 평가

| 항목 | 위험 |
|---|---|
| prefab GlobalObjectIdHash 변화 | **없음** (5개 .cs 만) |
| scene 변경 | 없음 |
| F12 driver auto-spawn (NGO 충돌 우려) | 낮음 — `Init()` 은 NM 참조 0, AfterSceneLoad 는 NM.Awake 이후 |
| IsOwner 가드 제거 부작용 | 낮음 — `Character.UnFreeze` 는 Frozen state 만 체크 후 처리 (idempotent) |
| 0.5s 재송신 / 5s watchdog 트래픽 | 미미 (성공 시 ClientRpc 1번, 실패 시 2~3번) |
| 다른 던전 회귀 (EP suffix fix) | 0 — fallback 유지 |

---

## 시연 시나리오 검증 (실제 빌드 결과)

### 보스방 진입 (1F_2R → Rena 보스방)
- 호스트 / 게스트 양쪽 정상 텔레포트
- 인트로 시퀀스 정상 진행 (98.50 → 100.58, 약 2초)
- intro 완료 후 양쪽 모두 정상 unfreeze
- watchdog 5초 검사 통과 — `All players unfrozen. OK`

### F12 비상탈출
- Driver heartbeat 정상 출력
- 씬 전환 시 자동 ForceRestore 작동
- F12 폴링 활성

### Dungeon_1F_2R 시작
- player 가 `(0, 0)` 부근 (`_1_E_EP` anchor) 에 spawn ✓
- `[DungeonRunBootstrap] Entry Point module selected (Pass 1, _EP suffix)` 로그 확인

---

## 시연 후 follow-up

1. **`disableInputComponentsOnNonOwner` 우회 검증** — PlayerMovementSync OnNetworkSpawn 의 non-owner 처리가 게스트 자기 character 에선 영향 0인지 확인
2. **`convertNonOwnerToAi` 부작용** — host 측 게스트 mirror 의 CharacterType=AI 가 unfreeze 흐름 영향 가능성
3. **`EmergencyTimeScaleRecovery` NGO 충돌 검증** — 일주일 운영 후 문제 없으면 영구 활성화 확정
4. **`unfreezeResendDelaySeconds` / `unfreezeWatchdogSeconds` SerializeField 노출** — 인스펙터 튜닝
5. **`RoomData.RoomType (Start/Combat/Boss)` 활용** — `_EP` suffix 보다 더 견고한 식별. RoomData asset 자체에 type 플래그
6. **`_EP` 명명 규칙 문서화** — 던전 디자이너 가이드에 명시
7. **`mvpRoomDataSequence` Editor 검증 도구** — wire 누락 자동 감지 Editor script

---

## 핵심 인사이트

1. **NGO ClientRpc 의 IsOwner 가드는 idempotent 작업엔 제거하는 게 견고**. Owner-only 제약은 server-authoritative 작업에서만 의미 있음. UnFreeze 같은 visual state 복원은 모든 client 호출 안전.
2. **race condition 은 3중 안전망으로**. 즉시 호출 + delay 재송신 + watchdog. 각각 다른 실패 mode 대응.
3. **자동 등록 어트리뷰트 주석 처리는 잠재적 영구 비활성**. NGO 충돌 우려로 주석한 후 다시 활성화 못 하면 기능 자체가 죽음. 주기적으로 재검토 필요.
4. **명명 규칙을 코드가 인식하는 단서로**. `_EP` suffix 같은 디자이너 친화 마커는 코드가 직접 읽어서 의미 부여 가능. RoomData.RoomType enum 보다 가볍지만 효과적.
5. **진단 로그 prefix 일관성**. grep 친화적 prefix (`[BossEntry]` 등) 가 빌드 환경에서 사용자 채팅 안 거치고 직접 분석 가능하게 함.

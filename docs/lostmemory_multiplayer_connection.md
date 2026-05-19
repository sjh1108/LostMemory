# LostMemory 멀티 네트워크 연동 가이드 (클라이언트 팀 인계)

> 작성: 손홍민 (백엔드/멀티 sync)
> 대상 흐름: 회원가입/로그인 → 마을(솔로/로비) → 던전 → 사망/결과창 → 마을 복귀
> 핵심: server-authoritative health/death sync + 사망 시 결과창 양쪽 표시 + 마을 복귀 시 player NetworkObject **fresh 재 spawn**

작성자가 못 보는 부분 (인스펙터 wireup / 정확한 prefab 경로 / 인증 UI 등) 은 `[클라 확인]` 마커로 표시. 클라 팀이 직접 채우거나 검증 부탁드립니다.

---

## 0. 전체 사용자 흐름

```
[Title 씬]
   │ (회원가입 또는 로그인 — 백엔드 /api/auth/signup, /api/auth/login)
   │ JWT 발급 → 클라가 보관
   ▼
[솔로 진입] Town_solo                    [멀티 진입] Test_MultiLobby
   │                                       │ (호스트: 세션 생성, 게스트: join)
   │                                       │ 양쪽 player 가 Tag=Respawn 위치에 spawn
   │                                       │
   │ (NGO 미시작)                          │ (NGO 활성, 세션 유지)
   │                                       │
   │ 던전 trigger                          │ HostOnlyLoadSceneTrigger (호스트가 trigger 통과)
   ▼                                       ▼
   ┌───────────────────────────────────────┐
   │              Mob_Sync_Test (던전)             │
   │  - 몬스터 공격 / 피격 / Down / Defeated       │
   │  - 모두 사망 시 5초 후 결과창 양쪽 동시 표시  │
   └───────────────────────────────────────┘
        │ 마을로 버튼
        ▼
   ┌───────────────────────────────────────┐
   │      RunManager.ReturnToTown 분기            │
   │  - 멀티 + lobbySceneName 지정 → 정공법       │
   │      (PlayerObject Despawn + 새 씬에서       │
   │       OnLoadEventCompleted 콜백으로 재 spawn) │
   │  - 솔로 또는 lobby 미지정 → 기존 town 흐름   │
   └───────────────────────────────────────┘
        │
        ▼
   [솔로] Town 또는 Town_solo            [멀티] Test_MultiLobby (세션 유지)
                                          → 다시 던전 진입 가능
```

---

## 1. 핵심 스크립트 위치

### 멀티/사망/sync 관련
| 스크립트 | 경로 | 역할 |
|---|---|---|
| `PlayerHealthSync` | `Assets/_Project/Scripts/Runtime/Networking/Player/PlayerHealthSync.cs` | server-authoritative health sync + 사망/Down/Defeated state sync (NetworkVariable) + 결과창 RPC + **마을 복귀 시 fresh 재 spawn (`RespawnPlayersAfterSceneLoad`)** |
| `PlayerMovementSync` | `Assets/_Project/Scripts/Runtime/Networking/Player/PlayerMovementSync.cs` | NetworkTransform owner-authoritative + spawn point align (Tag="Respawn") |
| `MonsterHealthSync` | `Assets/_Project/Scripts/Runtime/Networking/Monster/MonsterHealthSync.cs` | server-authoritative monster health sync |
| `KhiDownController` | `Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs` | Down/Defeated state machine + 부활/Defeat 처리 |
| `RunManager` | `Assets/_Project/Scripts/Runtime/Stage/RunManager.cs` | 런 상태 머신 + 결과창 표시 + 마을/로비 복귀 분기 |
| `RunResultPanelView` | `Assets/_Project/Scripts/Runtime/UI/RunResultPanelView.cs` | 결과창 UI (마을로/다시시작 버튼) |
| `SessionTownReturnHandler` | `Assets/_Project/Scripts/Runtime/Networking/Session/SessionTownReturnHandler.cs` | 세션 disconnect/failed 시 Town_solo 복귀 핸들러 (멀티 정상 종료와 무관) |
| `HostOnlyLoadSceneTrigger` | `Assets/_Project/Scripts/Runtime/Networking/Session/HostOnlyLoadSceneTrigger.cs` | 호스트가 trigger 진입 시 던전 씬 LoadScene |
| `RelaySession` | `Assets/_Project/Scripts/Runtime/Networking/Session/RelaySession.cs` | 세션 생성/입장/이탈/실패 이벤트 |
| `TownSpawnRouter` | `Assets/_Project/Scripts/Runtime/SceneFlow/TownSpawnRouter.cs` | Town 복귀 spawn 라우팅 (솔로 경로) |

### 인증/세션 관련
`[클라 확인]` — 회원가입/로그인 UI 와 JWT 클라이언트 처리 스크립트 경로. 백엔드 API 는 아래 4-A 참조.

---

## 2. 플레이어 프리팹 wireup

### 부착 컴포넌트 (필수)

- `NetworkObject` (NGO)
  - **DestroyWithScene**: false (마을 → 던전 진행 시 player 유지. 마을 복귀 시는 정공법으로 명시적 Despawn + 재 spawn)
- `Health` (TopDownEngine)
- `Character` (TopDownEngine)
- `CharacterMovement` (TopDownEngine)
- `Animator` (자체 또는 자식 — 자식이면 `Health.TargetAnimator` 에 명시 wireup)
- `PlayerHealthSync` ★
- `PlayerMovementSync` ★
- `KhiDownController` ★
- `KhiAnimatorMovementBinder` (자식 model GameObject)
- `KhiSpriteFlipBinder` (자식 model GameObject)
- 기타 Khi*Controller 들 (MeleeCombo, Parry, Dash, FinisherLunge 등)

### Inspector 설정 — `PlayerHealthSync`

| 필드 | 값 | 비고 |
|---|---|---|
| `Health` | self Health 컴포넌트 | 자동 resolve (Awake) 되지만 명시 권장 |
| `Min Delta` | 0.01 | health sync 임계값 |
| `Sync Hit` | true | 피격 SFX/시각 효과 sync |
| `Hit Animator Trigger` | `Hit` | Animator trigger 이름 |
| `Sync Down` | true | Down state sync |
| `Down Animator Trigger` | `Down` | Animator trigger 이름 (Hero_Animator 기준) |
| `Sync Death` | true | 사망 sync |
| `Death Animator Trigger` | `Down` | 사용자 환경의 사망 transition trigger |
| `Input Component Names To Disable On Death` | `KhiMeleeComboController`, `KhiParryController`, `KhiDashController`, `KhiFinisherLunge` | 사망 시 disable 할 입력 컴포넌트 (string match) |
| `Death Overlay Object` | (선택) "You Died" UI GameObject | 비워두면 OnGUI fallback 으로 표시 |
| `Death Overlay Fallback Text` | `You Died` | OnGUI fallback 시 표시 텍스트 |
| `Verbose Log` | 디버깅 시 true | 콘솔에 sync 흐름 출력 |
| `Defeat Hide Delay Seconds` | 5 | Defeated 후 visual hide 까지 대기 (KhiDownController.defeatObjectDisableDelay 와 일치) |
| `Run Failed Ui Delay Seconds` | 5 | 팀 전멸 후 결과창 표시까지 대기 (RunManager.failureResultingDelaySeconds 와 일치) |

### Inspector 설정 — `Health` (TDE)

- `TargetAnimator` → Hero_Animator (player root 또는 자식의 정확한 Animator). **이 필드가 비어있으면 PlayerHealthSync 가 GetComponentInChildren 으로 fallback — 잘못된 Animator 잡을 수 있음**.

### Inspector 설정 — `KhiDownController`

- `Animator` → Hero_Animator (Health.TargetAnimator 와 동일)
- `Solo Behavior` → `DownWithDebugRevive` (default)
- `Defeat Object Disable Delay` → 5
- `Log State Transitions` → 디버깅 시 true

### Animator (Hero_Animator) 요구사항

- `Down` state (이름 정확히) — `Down` trigger 받아 transition
- `Dead` state (이름 정확히) — KhiDownController 가 `animator.Play("Dead")` 명시 호출

---

## 3. NetworkManager wireup

### 부착 위치
`[클라 확인]` — Bootstrap 씬 또는 각 씬마다. NGO singleton 이라 DontDestroyOnLoad 일 수도.

### Inspector 설정 — `NetworkManager.NetworkConfig`

| 필드 | 값 | 비고 |
|---|---|---|
| **`Player Prefab`** | player prefab drag&drop | **필수** — `PlayerHealthSync.RespawnPlayersAfterSceneLoad` 가 이 값을 사용. 비어있으면 마을 복귀 시 player 없음 |
| `Enable Scene Management` | **true** | NGO LoadScene 으로 호스트 → 모든 client sync |
| `Auto Spawn Player Prefab Client Side` | true (NGO default) | client 접속 시 자동 spawn |

### Transport
`[클라 확인]` — `LostMemoryRelayTransport` 가 NetworkManager 옆에 부착 (`Assets/Scripts/Multiplayer/LostMemoryRelayTransport.cs`).

---

## 4. 씬별 작업

### A. `Title` (회원가입/로그인)
**경로**: `[클라 확인]` — Test_Title 또는 Title.unity

| 항목 | 작업 |
|---|---|
| **로그인 UI** | `[클라 확인]` — ID/Password 입력 + 로그인 버튼 |
| **회원가입 UI** | `[클라 확인]` — ID/Password/Nickname 입력 + 회원가입 버튼 |
| 백엔드 API 호출 | `POST https://k14c201.p.ssafy.io/api/auth/signup` / `/api/auth/login` |
| JWT 저장 | 응답의 `accessToken` 을 클라 메모리 또는 PlayerPrefs |
| 다음 씬 분기 | 솔로 모드 선택 → `Town_solo` / 멀티 모드 선택 → `Test_MultiLobby` |
| NetworkManager | **NGO 미시작** (Title 단계는 솔로) |
| Build Settings 등록 | **필수** (Scenes In Build 의 첫 번째 또는 두 번째) |

#### 백엔드 API endpoint (참고)

| Method | Path | Request | Response |
|---|---|---|---|
| POST | `/api/auth/signup` | `{ loginId, password, nickname }` | `{ userId, accessToken, refreshToken }` |
| POST | `/api/auth/login` | `{ loginId, password }` | `{ userId, accessToken, refreshToken }` |
| GET | `/api/users/me` | (Bearer JWT) | `{ userId, nickname, ... }` |

이후 모든 API 호출은 `Authorization: Bearer <accessToken>` 헤더 필요.

### B. `Test_MultiLobby` (멀티 로비)
**경로**: `Assets/_Project/Scenes/Test/Multi_Test/Test_MultiLobby.unity`

| 항목 | 작업 |
|---|---|
| **PlayerSpawnPoint** | 빈 GameObject + Tag=`Respawn` + position `(0, 0, 0)` |
| NetworkManager | `[클라 확인]` — 씬에 배치 또는 DontDestroyOnLoad 인스턴스 사용 |
| 호스트/게스트 UI | `[클라 확인]` — 세션 생성 / Join 버튼 |
| `HostOnlyLoadSceneTrigger` | 던전 진입용 trigger 영역 (Collider2D + isTrigger=true) + `Scene Name`=`Mob_Sync_Test` |
| `SessionTownReturnHandler` | (선택) disconnect 시 Town_solo 복귀용 |
| Build Settings 등록 | **필수** |

### C. `Town_solo` (솔로 마을)
**경로**: `Assets/_Project/Scenes/Test/Multi_Test/Town_solo.unity`

| 항목 | 작업 |
|---|---|
| 솔로 시작 위치 | (기존 wireup 유지) |
| NetworkManager | **NGO 미시작** |
| 던전 진입 UI | `[클라 확인]` — 던전 입장 버튼 또는 trigger |
| Build Settings 등록 | **필수** |

### D. `Mob_Sync_Test` (던전)
**경로**: `Assets/_Project/Scenes/Test/Multi_Test/Mob_Sync_Test.unity`

| 항목 | 작업 |
|---|---|
| **PlayerSpawnPoint** | Tag=`Respawn` + 던전 시작 위치 |
| `RunManager` | 씬에 배치 — 아래 표 wireup |
| **결과창** | `Test_ResultPanel` prefab drag&drop. 기존 인스턴스 있으면 비활성/삭제 |
| Monster prefab | NetworkObject + MonsterHealthSync 부착 (Test_Orc 등) |
| Build Settings 등록 | **필수** |

#### `RunManager` 인스펙터 (Mob_Sync_Test 의 RunManager)

| 필드 | 값 |
|---|---|
| `Town Scene Name` | `Town` (기본값 유지 — 솔로 던전 사망 시 복귀 씬) |
| `Town Scene Path` | (기존 유지) |
| **`Lobby Scene Name`** ★신규 | `Test_MultiLobby` |
| **`Lobby Scene Path`** ★신규 | `Assets/_Project/Scenes/Test/Multi_Test/Test_MultiLobby.unity` |
| `Restart Dungeon Scene Name` | (기존 유지) |
| `Failure Resulting Delay Seconds` | 5 |
| `Total Stage Count` | (시연 stage 수) |
| `Run Result Panel View` | active scene 의 인스턴스 자동 resolve (비워둬도 OK) |

> **분기 로직**: 멀티 세션 활성 + `Lobby Scene Name` 비어있지 않으면 → lobby 로 복귀 + player NetworkObject 재 spawn. 그 외 → 기존 town 흐름 (솔로 동작 보존).

---

## 5. 몬스터 wireup

### 부착 컴포넌트
- `NetworkObject`
- `Health` (TDE)
- `MonsterHealthSync`
- 기타 monster AI / animation 컴포넌트

`[클라 확인]` — 몬스터 prefab 정확한 경로 (Test_Orc 등).

### 동작
- server 가 health 권위 — 게스트는 자체 damage 무시
- monster 사망 시 server 측에서 destroy → NGO sync 로 client 자동 처리

---

## 6. UI wireup

### `Test_ResultPanel` (`Assets/_Project/Prefabs/Test/Test_ResultPanel.prefab`)

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| `RunResultPanelView` | `Restart Button` | 다시시작 버튼 |
| | `Lobby Button` | 마을로 버튼 |
| | (스탯 텍스트 필드들) | KillCount/BossKillCount/PlayTime 등 |

- `RunResultPanelView.OnLobby` / `OnRestart` 는 **C# event — 인스펙터 wireup 불가**. `RunManager.BindRunResultPanelView` 가 자동 subscribe.
- 마을로 클릭 → `RunManager.ReturnToTown` → 멀티/솔로 분기

### 사망 UI (You Died)
- `PlayerHealthSync.deathOverlayObject` 에 비활성 GameObject wireup. 사망 시 자동 활성, 마을 복귀 시 자동 비활성.
- wireup 안 하면 OnGUI fallback 으로 화면 중앙 텍스트 표시.

### HUD UI
`[클라 확인]` — HP/MP/Stage 등 HUD 는 발표 후 follow-up. 미구현.

---

## 7. Build Settings 필수 등록 씬

순서 (Scenes In Build):
```
0. Title 또는 Test_Title                                         [클라 확인 경로]
1. Assets/_Project/Scenes/Test/Multi_Test/Town_solo.unity
2. Assets/_Project/Scenes/Test/Multi_Test/Test_MultiLobby.unity
3. Assets/_Project/Scenes/Test/Multi_Test/Mob_Sync_Test.unity
```

`[클라 확인]` — Bootstrap 씬 또는 NetworkManager 가 DontDestroyOnLoad 처리되는 별도 씬이 있으면 0번 또는 그 이전에 등록.

---

## 8. 테스트 계정

`[클라 확인]` — 정확한 ID/Password 는 클라 팀 보유. 메모리 기준으로 `testuser1 ~ testuser4` 존재 (각 user_id 1~4, memory_shards 300, talent total_point 5 시드 적용 가정 — 시드 SQL 은 `server/docs/sql/seed_demo_users.sql` 참조).

| 계정 | 용도 |
|---|---|
| testuser1 | 솔로 시연 (호스트 단독) 또는 멀티 호스트 |
| testuser2 | 멀티 게스트 1 |
| testuser3 | 멀티 게스트 2 (4인 시연) |
| testuser4 | 멀티 게스트 3 (4인 시연) |

비밀번호: `[클라 확인]` — 기존 시연 시 사용한 password.

---

## 9. 테스트 시나리오

### 시나리오 A — 전체 흐름 (회원가입 → 던전 종료)
1. Build Settings 첫 번째 씬 = `Title`
2. Editor Play → Title 씬 진입
3. **회원가입** (testuser 미생성 시) — `POST /api/auth/signup` 호출 → JWT 발급
4. 또는 **로그인** — `POST /api/auth/login` 호출 → JWT 발급
5. (메인 메뉴) 솔로/멀티 선택
6. 분기:
   - **솔로 분기**: Town_solo 진입 → 던전 trigger → Mob_Sync_Test → 사망 → 결과창 → 마을로 → Town 복귀
   - **멀티 분기**: Test_MultiLobby 진입 (호스트 세션 생성) → 다른 client join → 던전 trigger → Mob_Sync_Test → 사망 → 결과창 → 마을로 → Test_MultiLobby 복귀 (세션 유지)

### 시나리오 B — 멀티 핵심 검증
1. **Editor 1 (호스트)**: Title → testuser1 로그인 → Test_MultiLobby → 세션 생성
2. **Editor 2 또는 Build (게스트)**: Title → testuser2 로그인 → Test_MultiLobby → join (호스트 세션 코드 또는 자동 매칭)
3. 양쪽 player 가 Test_MultiLobby 의 PlayerSpawnPoint 위치 spawn
4. 호스트가 던전 진입 trigger 통과 → HostOnlyLoadSceneTrigger 가 `Mob_Sync_Test` 로 LoadScene → 양쪽 던전 진입
5. **몬스터 공격 테스트**: 몬스터 공격 → 양쪽 화면 동일 HP
6. **사망 테스트**: health 1 도달 → KhiDownController.EnterDown → 양쪽 화면 Down 모션
7. Down timer 10초 만료 → Defeated → 5초 후 양쪽 시체 hide
8. 두 player 모두 Defeated → 5초 후 양쪽 결과창 동시 표시
9. 호스트가 **마을로** 버튼 클릭
10. **기대**:
    - 콘솔: `Despawn old PlayerObject clientId=...` × 2 → `OnLoadEventCompleted scene=Test_MultiLobby` → `Respawned PlayerObject for clientId=...` × 2
    - 양쪽 모두 Test_MultiLobby 진입
    - **fresh player** — 사망 모션 X, Health full, 이동/공격 정상
    - NGO 세션 유지 (Disconnect 안 됨)
11. (회귀) 다시 던전 trigger 통과 → 정상 진입

### 시나리오 C — 부분 검증 항목

| 목적 | 행동 |
|---|---|
| **체력 1 도달 시 양쪽 Down 모션** | 한 player 만 사망까지 데미지 → 양쪽 화면 Down state 확인 |
| **시체 5초 후 hide 양쪽 동기화** | Defeated 진입 후 5초 카운트 |
| **You Died 텍스트 시체와 함께 사라짐** | 사망 후 5초 후 텍스트 사라지는지 |
| **마지막 사망 player 의 호스트 화면 Down 모션** | 두 player 시간차 사망 → 마지막 사망의 호스트 측 화면 Down 보이는지 |
| **결과창 양쪽 동시 표시** | 호스트/게스트 5초 ± frame 단위 동시 |
| **마을 복귀 후 fresh** | 콘솔의 `Respawned` 로그 확인 + 이동/공격 정상 |
| **마을 복귀 후 재 던전 진입** | 세션 유지 검증 |
| **솔로 회귀 (기존 동작 보존)** | Town_solo 시작 → 던전 → 사망 → 마을로 → Town 복귀 (멀티 정공법 미적용 검증) |

---

## 10. 핵심 코드 흐름 요약

### 인증 → 마을 진입
```
[Title 씬]
  ├─ 로그인 UI → POST /api/auth/login → JWT
  └─ 솔로/멀티 선택
       ├─ 솔로 → SceneManager.LoadScene("Town_solo")
       └─ 멀티 → SceneManager.LoadScene("Test_MultiLobby")
            └─ Test_MultiLobby 진입 시 NGO StartHost / StartClient 호출
```

### 던전 진입
```
HostOnlyLoadSceneTrigger.OnTriggerEnter2D (호스트만)
  └─ networkManager.SceneManager.LoadScene("Mob_Sync_Test")
       → NGO sync 로 모든 client 던전 진입
```

### 사망 → 결과창
```
KhiDownController.EnterDown (Health.OnHit 가로채기) → _state = Down
PlayerHealthSync.Update (server)
  ├─ _syncedHealth NetworkVariable write
  └─ _syncedDownState NetworkVariable write
NetworkVariable.OnValueChanged (모든 client) → ApplyDown/DefeatedVisualsOnClient
KhiDownController.DefeatedByTimeout (10초)
  └─ PlayerHealthSync.HandleDefeatedServer → HidePlayerVisualWithDelayClientRpc(5s) → ApplyHidePlayerVisual
PlayerHealthSync.Update — AnyPlayerAlive=false 감지
  └─ RunManager.HandlePlayerDefeatedDirect (RunFailed 전이)
  └─ BroadcastRunFailedUiClientRpc(5s) → 모든 client → RunManager.ShowResultingUI
```

### 마을 복귀 (멀티)
```
RunResultPanelView.OnLobby (버튼) → RunManager.ReturnToTown
  ├─ networkSessionActive && lobbySceneName 지정 → useLobbyScene=true
  ├─ PlayerHealthSync.RespawnPlayersAfterSceneLoad
  │    ├─ NetworkManager.SceneManager.OnLoadEventCompleted 콜백 등록
  │    └─ 모든 client.PlayerObject.Despawn(destroy=true)
  ├─ networkManager.SceneManager.LoadScene(Test_MultiLobby)
  └─ Destroy(gameObject) (RunManager singleton)
NGO LoadScene 완료
NetworkSceneManager.OnLoadEventCompleted (server)
  └─ 각 clientId 마다 Instantiate(PlayerPrefab) + SpawnAsPlayerObject(clientId, destroyWithScene:false)
PlayerMovementSync.OnNetworkSpawn (IsOwner)
  └─ AlignToSpawnPoint — Tag="Respawn" GameObject 위치
```

---

## 11. 알려진 미해결 / Follow-up

- **솔로 던전 사망 시 fresh respawn 미적용** — 솔로는 NGO 미시작이라 정공법 적용 X. 미봉책 (Health.Revive + Animator.Rebind 등) 유지. 솔로 사망 시연이 포함되면 검증 필요.
- **HUD UI** — HP/MP/Stage HUD 미구현. 발표 후 follow-up.
- **회원가입 시점에 자동 시드 데이터 적용** — testuser 의 memory_shards/talent 초기값은 `server/docs/sql/seed_demo_users.sql` 로 DB 직접 적용. 시연 직전 reset 필요 시 `server/docs/sql/reset_demo.sql` 사용.

---

## 12. 변경 파일 목록 (커밋 인지용)

| 파일 | 변경 종류 |
|---|---|
| `PlayerHealthSync.cs` | 광범위 — NetworkVariable<int> _syncedDownState + 다수 ClientRpc + RespawnPlayersAfterSceneLoad + BroadcastResetDeathStateForAll + LateUpdate Animator 강제 + ApplyLocalDeathReset 등 |
| `RunManager.cs` | `lobbySceneName`/`lobbyScenePath` 추가 + `ReturnToTown` 분기 + `_resultingUiShown` idempotent guard + `ShowResultingUI`/`HandlePlayerDefeatedDirect` public 화 |
| (테스트 시드) `Test_ResultPanel.prefab` | 결과창 복제본 — Mob_Sync_Test 던전 전용 |

---

## 13. 디버깅 팁

- **콘솔 로그 추적**: `PlayerHealthSync.Verbose Log` ON → 모든 sync 흐름 출력
- **마을 복귀 시 player 안 보임** → NetworkManager 의 `Player Prefab` 등록 확인 (콘솔에 `NetworkManager.NetworkConfig.PlayerPrefab is null` warning)
- **호스트 화면 게스트 stand** → Animator state 이름 "Down" 확인 + `Health.TargetAnimator` 가 Hero_Animator 인지
- **이동키 안 먹음** → fresh 재 spawn 정상 작동했는지 (`Respawned PlayerObject` 콘솔 로그) 확인. 미작동 시 PlayerPrefab 등록 확인
- **회원가입/로그인 실패** → 백엔드 EC2 상태 확인 (`https://k14c201.p.ssafy.io/api/auth/login` 응답)
- **세션 join 실패** → Relay 포트 (7777 UDP) inbound + 호스트 세션 ID 정확성

---

## 문의

본 인계 사항 / 인스펙터 wireup 검증 시 콘솔 로그 + 시나리오 재현 단계 같이 공유 부탁드립니다.

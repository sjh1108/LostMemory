# 멀티플레이 세션 구조 설계 — 클라이언트 팀 논의 안건

> 작성: 2026-05-13
> 발표: D-8 (2026-05-21)
> 작성자: 손홍민 (백엔드)
> 대상: 클라이언트 팀
> 컨텍스트: 분신 (캐릭터 복제) 버그 임시 fix 후 후속 설계 결정

---

## 1. 현 작업 내용 전체 요약

1. **NetworkTransport 교체** — `UnityTransport (UGS)` → 자체 `LostMemoryRelayTransport` (Netty UDP relay, EC2 호스팅) 로 전환
2. **사용한 캐릭터 프리팹** — `Assets/_Project/Prefabs/Characters/Test_shm.prefab` (TestKhi 재클론 + NetworkObject + PlayerMovementSync 추가본)
3. **사용한 유니티 씬** — `Test_Title.unity`, `Test_Town.unity` (정식 Title / Town 씬은 미수정)
4. **Test_Town 의 scene-placed Player 제거 + NetworkManager 의 PlayerPrefab (Test_shm) 만 사용** → 멀티 환경에서 다수 캐릭터 자산 정상 spawn 동작 확인
5. **빌드 후 배포 서버 (EC2) 의 자체 Relay 회귀 검증 성공** — 호스트 / 게스트 1대1 세션 생성·진입·이탈 사이클 통과

---

## 2. Test_Town 의 Player 프리팹 제거 사유

### 문제 — 동시 존재로 인한 캐릭터 복제

씬 (`Test_Town.unity`) 에 배치된 Player 프리팹 + `NetworkManager.NetworkConfig.PlayerPrefab` 에 등록된 Player 프리팹이 **세션 진입 시 동시 spawn** 됨. 결과로 캐릭터가 2개 보이고, 컴포넌트별로 기능이 둘로 쪼개져 일관되지 않는 동작 발생.

### 관찰된 일관성 깨짐 증상

| 기능 | scene Player (본체) | NGO PlayerPrefab spawn (분신) |
|---|---|---|
| HP UI 갱신 | X (UI 미변동) | O (HP 바에 반영) |
| 실제 HP 깎임 | O (피격 시 감소) | O (피격 시 감소) |
| 피격 판정 | O (collider 적중 가능) | O (collider 적중 가능) |
| 사망 판정 → 게임 종료 | O (Down 만료 → Defeated 트리거) | X (죽어도 종료 안 됨) |
| 타격 (공격) 판정 | O | 부분적 (확실치 않음) |

→ 둘 중 한쪽으로 통합해야 의미 있는 게임플레이 가능. NGO 가 PlayerPrefab 의 자동 spawn 을 강제하므로 (멀티 동작 위해 필요) scene 측을 제거하는 방향으로 임시 fix.

### 임시 fix 의 한계
Test_Town 만 처리한 상태. 던전 씬 / 정식 Town 씬은 동일 패턴 반복 필요 → 일괄 적용 전에 설계 결정 (논의점 3) 이 선행돼야 함.

---

## 3. 논의점

### 3.1 Canvas 내부 오브젝트와의 reference 충돌 가능성

기존 맵 유니티 파일에서 Player 프리팹을 제거하고 NetworkManager 의 PlayerPrefab 만 쓰는 구조로 갈 때, **Canvas 산하 UI 오브젝트들이 scene Player 를 직접 참조하도록 wiring 됐을 경우** 모두 깨지는 우려.

#### 우려 본질
- Unity 의 prefab serialization 제약: **prefab 은 scene 오브젝트를 직접 참조 못 함**
- 결과로 다음 두 reference 방향이 다 깨질 수 있음:
  - `scene Canvas → scene Player` (UI 가 SerializeField 로 Player 드래그)
  - `scene Player → scene Canvas` (Player 의 어떤 컴포넌트가 HP 바 / 인벤토리 패널 등 드래그)
- 깨지면 마을 UI 전체를 service locator / event 패턴으로 재배선해야 할 수 있음

#### 현재 관찰 — 부분적 양호 신호
- Test_Town 의 분신 fix 후 콘솔 로그상 모든 wiring 이 `OK` (catalog / playerStat / inventory / playerAim / health / container 등) — 단 이건 **Player 내부 reference** 만 보여줌
- **Canvas 측 wiring 은 로그 미노출** — 시각 확인 / 별도 audit 필요
- `LocalPlayerResolver` 가 이미 존재 (`PlayerMovementSync.OnNetworkSpawn` 에서 owner 인 경우 `Register` 호출) — 클라 팀이 service locator 패턴을 사전 도입한 흔적

#### 작업량 시나리오
| 케이스 | fix 부담 |
|---|---|
| Canvas UI 가 모두 `LocalPlayerResolver` / Event 패턴 | 0. 자동 동작 |
| 일부 UI 만 ad-hoc Inspector wiring | 1~2h. 해당 컴포넌트만 마이그레이션 |
| 다수 UI 가 ad-hoc 패턴 + 일관성 없음 | 4~8h+. 위험 큼 |

#### 클라 팀 검증 요청
- Test_Town 의 Canvas 하위 UI 컴포넌트들이 player 를 어떻게 찾는지 점검:
  - (a) Inspector SerializeField 로 scene Player 직접 참조 → 깨짐 위험
  - (b) `FindObjectOfType` / `GameObject.Find` 등 런타임 검색 → 정상 동작
  - (c) `LocalPlayerResolver.Register/Resolve` → 정상 동작
  - (d) Event / Observer 패턴 → 정상 동작
- Play 모드에서 Test_Town 의 UI (HP / 인벤토리 / 통화 표시 등) 가 NGO spawn 한 Test_shm(Clone) 과 정상 동기화되는지 시각 확인
- 결과에 따라 fix 시간 산정 → 논의점 3.2, 3.3 의 선택지가 좌우됨

#### (보조) 저장 기능 — 별도 카테고리, 위험 낮음
- `MemoryShardWallet` 은 별도 singleton + DontDestroyOnLoad + 로컬 파일 (`MemoryMetaService.Load/Save`) 패턴. scene Player 와 무관.
- 다른 wallet (`GoldWallet`, 재능 포인트 wallet, `RelicInventory` 등) 도 같은 패턴인지는 grep 으로 5분에 확인 가능. 다른 패턴이면 그것만 singleton 분리.

---

### 3.2 멀티 대기 위치 — 별개 세션룸 vs 호스트 마을

호스트가 세션을 만든 직후 호스트 + 게스트가 만나는 공간 구성 방식.

#### 선택지 A — 별개의 세션룸 (Lobby 씬)
호스트가 세션 생성 → Lobby 씬 진입. 게스트도 코드 입력 후 Lobby 로.

**장점**
- 개발 편함. Test_Town 패턴 (scene Player 없음, NGO 가 PlayerPrefab spawn) 을 그대로 적용
- 솔로 / 멀티 코드 패스 분리 명확. 마을은 솔로 전용, Lobby 는 멀티 전용
- 호스트/게스트 데이터 격리가 자연스러움 (각자 자기 캐릭터 spawn, UI 도 자기 캐릭터 바인딩)
- 마을 UI 의 Canvas 충돌 우려 (논의점 3.1) 가 마을에 국한 — Lobby 는 신규 씬이라 처음부터 깔끔히 짜면 됨

**단점**
- 마을의 일부 기능 (기억의 파편 프레임, 무기 해금 등) 을 세션룸에서도 쓰려면 약식 오브젝트 / UI 가 Lobby 에 따로 필요
- 호스트가 세션을 만든 경우에만 입장 가능한 입구 조건 / UX 필요

#### 선택지 B — 호스트 마을에 게스트 초대
호스트가 세션 생성 후 본인 마을에 머무름. 게스트가 NGO 로 호스트의 마을 씬에 진입.

**장점**
- 별도 씬 만들 필요 없음. 마을의 모든 기능 그대로 사용 가능
- 게임 컨셉상 "친구 마을에 놀러간다" 같은 몰입감 가능

**단점**
- 호스트 / 게스트의 존재 방식을 달리 해야 함:
  - 호스트는 scene-placed Player (또는 NGO host spawn)
  - 게스트는 NGO 가 spawn 한 분신만
- **데이터 격리 사고 위험 큼** — 게스트가 마을 기능 (상점 / 인벤토리 / 기억 패널 등) 을 열었는데 호스트의 진행상황이 표시되는 대참사 가능
- 마을의 모든 UI 컴포넌트가 "본인 데이터만" 표시하도록 audit + 수정 필요. 작업량 + 위험 큼

#### 백엔드 권장 — 선택지 A
D-8 일정 + 데이터 격리 위험 + 마을 UI audit 부담 종합 시 선택지 A 안전. 선택지 B 의 "친구 마을 방문" 컨셉이 발표 메시지의 핵심이면 재고.

---

### 3.3 던전에서의 Player 프리팹 관리 방식

논의점 3.1, 3.2 의 결과에 따라 던전 처리도 결정. 던전은 솔로 / 멀티 둘 다 지원해야 하는 게 차이점.

#### 옵션 a — 솔로 / 멀티 통합 (항상 NGO)
던전 진입 시 항상 NGO `StartHost()` 호출. 솔로 = 게스트 0명의 호스트. PlayerPrefab spawn 한 캐릭터로만 플레이.

**장점**
- 분기 코드 0. 솔로 동작 = 멀티 동작 보장
- 던전 씬에서 scene Player 일괄 제거 가능. 분신 버그 패턴 적용 가능
- 4인 코옵 검증 (sprint plan 5/14) 도 같은 path 그대로 작동

**단점**
- 솔로 시작 시에도 backend 에 `createSession` 호출 (~50ms) + `sessions` row 1 추가
- backend 오프라인 시 솔로 플레이 불가 (현재 정책상 어차피 백엔드 SoT 라 큰 단점 아님)

#### 옵션 b — 솔로 / 멀티 분기
솔로 던전은 scene-placed Player 그대로. 멀티 던전만 NGO PlayerPrefab spawn.

**장점**
- 솔로 백엔드 의존성 없음 (오프라인 가능)
- 마을 → 솔로 던전 흐름이 NGO 없이 단순

**단점**
- 솔로 / 멀티 코드 패스 2개 유지. spawn 위치 / 카메라 / UI / 입력 등 두 경로의 동작이 어긋날 위험
- 던전 씬에서 scene Player 보존 / 제거 분기 필요
- 솔로 → 멀티 / 멀티 → 솔로 fallback 시 분기 추가

#### 백엔드 권장 — 옵션 a (통합)
NGO 단일 코드 패스로 솔로 / 멀티 / 4인 모두 동작. 솔로 backend overhead 무시 가능. 발표 안정성 + 향후 확장성 모두 유리.

#### 의존성
**논의점 3.1 (Canvas 충돌) 결과가 던전에도 영향**. 던전 UI 의 Canvas wiring 패턴도 같이 audit 필요. ad-hoc Inspector wiring 이 던전 씬에 다수면 옵션 a 의 작업량 증가.

---

## 4. 그 외 문제

### 4.1 PK (Player Kill) / Friendly Fire 방지

협동 게임에서 PK 는 허용될 수 없음. 분신 버그 fix 와는 별개의 문제로, **아군 오브젝트를 구분하는 메커니즘이 게임 전체에 요구됨**.

#### 현재 관찰
- 분신 fix 전 검증 시 PK 가 가능했고 (분신을 자기가 죽일 수 있음, 피격 판정이 분신에 있어서 본체 HP 감소), 본인 분신을 자기가 죽이는 자해도 가능했음
- 이건 분신 버그 자체의 부작용이기도 하지만, 본질적으로 **TopDownEngine 의 Health / Weapon 시스템이 PvE 진영 분리를 적용하고 있지 않음** (#139 검증에서 발견된 known issue 와 동일)

#### 가능한 해결 방향
1. **Unity Layer 기반 필터링**
   - Player Layer 와 Enemy Layer 를 분리
   - Weapon 의 `TargetLayerMask` 를 Enemy Layer 만 포함하도록 설정
   - 모든 무기 / 데미지 컴포넌트의 LayerMask 일괄 점검 필요
2. **TopDownEngine `Health.OwnerSide` / `DamageOnTouchOwnerSide` 활용**
   - Player 끼리는 같은 OwnerSide 로 묶고, DamageOnTouch / MeleeWeapon 의 `IgnoreSameSide` 활성화
   - TDE 내장 기능이라 비교적 작업량 적음
3. **데미지 발생 시점에 호출자 / 피격자 비교**
   - DamageReceiver 에서 attacker 가 같은 Player team 이면 무시
   - 가장 유연하나 모든 데미지 경로 점검 필요

#### 작업 위치
- 본 문제는 분신 fix 와 무관하게 멀티 협동 출시 전제 조건. 멀티 fix 마무리 후 별도 작업으로 분리 권장
- 클라 작업 영역. 백엔드는 무관

---

## 5. 백엔드 측 권장안 정리

| 항목 | 권장 |
|---|---|
| Canvas 충돌 audit (논의 3.1) | Test_Town 의 Canvas UI 패턴 점검 + Play 모드 시각 확인. ad-hoc Inspector wiring 발견 시 LocalPlayerResolver 로 마이그레이션 |
| 멀티 대기 위치 (논의 3.2) | **선택지 A — 별개 세션룸 (Lobby 씬)** |
| 던전 처리 (논의 3.3) | **옵션 a — 항상 NGO, 솔로 = 1인 세션** |
| PK 방지 (4.1) | 분신 fix 마무리 후 별도 작업. TDE `OwnerSide` 기반이 작업량 적을 듯 |

### 권장 게임 흐름

```
Player A (호스트):
  Title → Login → Town (솔로) → [멀티 시작] → Lobby (NGO 세션 시작) → Dungeon

Player B (게스트):
  Title → Login → [코드 입력] → Lobby (NGO 세션 join) → Dungeon

솔로 플레이:
  Title → Login → Town (솔로) → [솔로 던전] → Dungeon (NGO 1인 세션, 분기 없음)
```

### 씬별 캐릭터 처리

| 씬 | scene-placed Player | NGO PlayerPrefab spawn | 데이터 source |
|---|---|---|---|
| Town (솔로 전용) | 있음 (현행 유지) | 없음 (NGO 미진입) | 백엔드 + DontDestroyOnLoad 싱글톤 |
| Lobby (멀티 전용) | 없음 | 있음 | 동일 (싱글톤 그대로 따라옴) |
| Dungeon (솔로/멀티 통합) | 없음 | 있음 | 동일 |

---

## 6. 클라 팀에 요청드리는 액션

### 1. Canvas UI reference audit (1~3h, 최우선)
- Test_Town 의 Canvas 하위 UI 오브젝트들이 player 를 어떻게 찾는지 점검 (Inspector SerializeField / Find / LocalPlayerResolver / Event 중 어느 패턴)
- ad-hoc Inspector wiring 발견 시:
  - 어떤 UI 컴포넌트인지 리스트
  - LocalPlayerResolver 로 마이그레이션 비용 산정
- Play 모드에서 Test_Town 의 UI (HP / 인벤토리 / 통화 표시 등) 가 NGO spawn Test_shm(Clone) 와 정상 동기화되는지 시각 확인
- (보조) wallet 류 (`GoldWallet`, 재능 포인트 wallet, `RelicInventory`) singleton/backend 패턴 grep 검증

### 2. 멀티 대기 위치 결정 (논의 3.2)
- 권장안 (선택지 A) 동의 / 반대 의견
- 선택지 B 가 필요한 이유가 있으면 (게임 컨셉 등) 회신

### 3. 던전 처리 결정 (논의 3.3)
- 권장안 (옵션 a 항상 NGO) 동의 / 반대 의견
- 던전 씬의 scene-placed Player / TestKhi 인스턴스 제거 시점 / 작업자 결정
- 향후 PlayerPrefab 을 어떤 캐릭터로 할지 (Test_shm 유지? TestKhi 에 직접 NetworkObject 부착? 정식 캐릭터?)

### 4. Lobby 씬 설계 (선택지 A 채택 시)
- Lobby 씬 만들 사람 / 일정
- Lobby 에 필요한 약식 기능 (기억의 파편 패널 / 무기 해금 등) 범위 결정
- 호스트 [Start Dungeon] 버튼 + 게스트 Ready 버튼 UX

### 5. PK 방지 (4.1) 작업자 / 시점 결정
- 분신 fix 마무리 후 작업 권장
- TDE `OwnerSide` 기반이 작업량 적을 듯 — 검증 필요

---

## 7. 회신 기한

가능하면 **5/14 (수) 중**으로 위 5개 액션에 대한 회신 부탁드립니다.

발표 D-7 시점에 던전까지 분신 fix 마무리하려면 5/15~5/19 4일이 필요합니다.

질의 / 추가 정보 필요 시 손홍민 (백엔드) 또는 본 문서 작성자에게 알려주세요.

---

## 8. 참고 — 관련 작업 진행 상황

- Test_Town 의 분신 fix 검증 완료 (`[fix] Test_shm 재클론 + Test_Town 분신 제거` 커밋)
- Test_shm.prefab = TestKhi 재클론 + NetworkObject + PlayerMovementSync (임시 형태)
- backend Sessions API + Relay UDP 통신 1대1 검증 통과 (k14c201.p.ssafy.io:7777)
- 본 문서의 결정이 끝나면 후속 PR: Lobby 씬 생성 / Dungeon NGO 통합 / Canvas UI audit fix / PK 방지

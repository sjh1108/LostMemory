# 멀티 세션 구조 설계 — 클라 팀 회신 초안

> 회신 대상: 손홍민 (백엔드)
> 원문: `lostmemory_multi_session_design_discussion.md` (2026-05-13 작성)
> 회신 기한: 2026-05-14
> 발표: D-7 (2026-05-21)
> 작성: 김회인

---

## Context

백엔드 측에서 분신 (캐릭터 복제) 임시 fix 후 멀티 세션 구조에 대한 5개 결정 회신을 요청. 본 문서는 코드 베이스 실측을 기반으로 한 클라 팀 회신 초안. 코드 검증 결과 백엔드의 일부 우려 (Canvas wiring, PK 방지 방향) 가 실제 코드와 다르게 추정돼 있어 정정해서 회신.

---

## 1. 5개 결정 요약 — 클라 팀 회신

| # | 항목 | 백엔드 권장 | 클라 회신 | 비고 |
|---|---|---|---|---|
| 1 | Canvas UI audit (3.1) | LocalPlayerResolver 마이그레이션 | ✅ **동의 — 작업량 큼 (3-6h)** | 던전 8개 씬에 SerializeField wiring 다수 발견 |
| 2 | 멀티 대기 위치 (3.2) | A: 별개 Lobby 씬 | ✅ **A 동의** | 발표 D-7 + 데이터 격리 위험 |
| 3 | 던전 처리 (3.3) | a: 항상 NGO | ✅ **a 동의 — 단 작업량 큼 (5-8h)** | 던전 8개 씬에 scene-placed TestKhi (NetworkObject 없음) 박혀있음 |
| 4 | Lobby 설계 (3.4) | — | 후술 (§4) | 약식 기능 범위 합의 필요 |
| 5 | PK 방지 (4.1) | TDE `OwnerSide` | ⚠️ **방향 정정 — 분신 fix 후 재현 테스트 우선** | Layer 셋업·LayerMask 이미 정확 |

---

## 2. Canvas UI audit (3.1) 회신

### audit 의 본질 정리

백엔드 §3.1 의 우려 = "scene Player 를 제거하면 Canvas UI 의 player 참조 wiring 이 깨진다". audit 대상 = **scene Player 가 제거될 씬**.

### A + a 채택 시 scene Player 처리

| 씬 | scene Player 현황 | audit 대상? |
|---|---|---|
| Town (솔로 전용) | 유지 | ❌ 제거 안 함 |
| Lobby (신규) | 없음 | ❌ 신규 씬 |
| **Dungeon (8개 씬)** | **TestKhi_MinimalCharacter2D 박혀있음 (NetworkObject 미부착)** | ✅ **여기가 핵심 대상** |

해당 씬: `Dungeon.unity`, `Dungeon_1F_1R.unity`, `Dungeon_1F_2R.unity`, `Dungeon_1F_3R.unity`, `Dungeon_1F_4R.unity`, `Dungeon_1F_Boss.unity`, `Dungeon_1F_Shop.unity`, `Dungeon_1F_Shop_testkhi.unity` — TestKhi prefab GUID `4d290d1fc0f84526999c421c61df5d8f` 검출.

### Test_Town 실측 (참고 — 분신 fix 검증 환경)

UI Presenter 다수가 LocalPlayerResolver / Event / Singleton 패턴 사용:
- `CurrencyHUDView` (Event), `TownCurrencyHUDPresenter` (Singleton), `PlayerHUDPresenter` (Resolver+fallback)

### Dungeon_1F_1R 실측 (진짜 검증 대상)

씬 안에 TestKhi 캐릭터의 컴포넌트들이 **stripped MonoBehaviour** 로 다수 등장 → 씬의 다른 오브젝트 (Canvas UI 등) 가 SerializeField wiring 한 증거 (§3.1 (a) 패턴):

| 참조되는 캐릭터 컴포넌트 | 클래스 |
|---|---|
| 라인 9871 | `LostMemory.Relics.BuildManager` |
| 라인 9883 | `LostMemory.TestKhi.KhiWeaponPresenter` |
| 라인 9895 | `LostMemory.TestKhi.KhiParryController` |
| 라인 9907 | `LostMemory.TestKhi.KhiMeleeComboController` |
| 라인 9919 | `LostMemory.TestKhi.KhiPlayerAim` |
| 라인 10536 | `LostMemory.Relics.PlayerRelicInventory` |
| 라인 10548 | `LostMemory.TestKhi.KhiPlayerStateAggregator` |
| 라인 10672 등 | + 추가 다수 |

→ 백엔드 §3.1 "다수 UI 가 ad-hoc 패턴, 4-8h+, 위험 큼" **시나리오 적중**.

### 회신 결론

- scene-placed TestKhi 제거 + Canvas / 씬 SerializeField wiring 마이그레이션 필요
- 마이그레이션 방향: LocalPlayerResolver 의 `Register/Resolve` 또는 `LocalPlayerReady` 이벤트 패턴으로 통일
- 단, 일부 컴포넌트 (BuildManager, PlayerRelicInventory) 는 Player 자체가 아닌 Player 의 컴포넌트를 wiring 하므로 마이그레이션 패턴 설계 필요 — LocalPlayerResolver 에 `T GetComponentOnLocalPlayer<T>()` 같은 확장 API 가 깔끔
- **작업량: 3-6h** (Canvas audit 1-2h + 마이그레이션 2-4h)

---

## 3. 멀티 대기 위치 (3.2) 회신

**선택: A (별개 Lobby 씬)** 동의.

이유:
- 발표 D-7 일정상 마을 데이터 격리 audit 부담을 감수할 시간 없음
- 마을 / Lobby 코드 패스 분리가 솔로 / 멀티 흐름을 명확히 함
- B 의 "친구 마을 방문" 컨셉은 발표 후 v2 로 보류

비고:
- 솔로 플레이 흐름은 `Title → Town → Dungeon (NGO 1인 세션)` 으로 유지 (마을 자체엔 NGO 미진입)
- 멀티 흐름은 `Title → Town → [멀티 시작] → Lobby → Dungeon`

---

## 4. 던전 처리 (3.3) 회신

**선택: a (항상 NGO, 솔로 = 1인 세션)** 동의 — 단 작업량이 ~0 이 아님.

### 현 상태 실측
- 8개 던전 씬 전부에 **scene-placed `TestKhi_MinimalCharacter2D`** 박혀있음 (`Dungeon_1F_1R.unity` 라인 9805~9858 PrefabInstance)
- 해당 프리팹에 **NetworkObject 미부착** (NGO script GUID `d5a57f767e5e46a458fc5d3c628d0cbb` grep no-match)
- 멀티 던전 진입 시 scene-placed TestKhi + NGO spawn Test_shm 동시 존재 → **Test_Town 과 동일한 분신 버그 재현됨**

### 적용 방안
- 8개 던전 씬에서 scene-placed TestKhi 인스턴스 제거
- Town → Dungeon 전환부에 `StartHost()` 진입 지점 마련 (솔로 = 1인 세션)
- PlayerPrefab (Test_shm 또는 정식 캐릭터) 만 NGO 가 spawn 하도록 통일
- Canvas / 씬 wiring 마이그레이션 (§2 참조)

### 작업량 추정: 5-8h
- 8개 씬에서 캐릭터 인스턴스 제거 + 위치/카메라 anchor 조정: 2-3h
- StartHost 진입부 코드: 1-2h
- 회귀 검증 (8개 씬 각각 진입 / 8개 씬 솔로 / 1F_Boss 멀티): 2-3h

### 비고
- 옵션 b (솔로/멀티 분기) 채택해도 결국 멀티 던전엔 scene Player 제거 작업 필요 → 옵션 a 가 여전히 합리적
- 4인 코옵 검증 (sprint 5/14) 도 같은 path

---

## 5. Lobby 씬 설계 (회신 §4)

### 권장 범위 — 발표 최소 셋업

| 요소 | 포함 | 비고 |
|---|---|---|
| 캐릭터 spawn 영역 (NGO PlayerPrefab) | ✅ | Test_Town 패턴 그대로 |
| [Start Dungeon] 버튼 (호스트 only) | ✅ | NetworkManager 호스트 권한 체크 |
| 게스트 [Ready] 토글 | ✅ | 간단한 NetworkVariable bool |
| 세션 코드 표시 | ✅ | RelaySessionLifecycle 에서 가져옴 |
| 기억의 파편 패널 | ❌ | 마을에서 사용 후 Lobby 진입 |
| 무기 해금 / 재능 패널 | ❌ | 동일 |
| 캐릭터 미리보기 | ✅ (가능하면) | 다른 플레이어 캐릭터 시각 확인용 |

### 작업자 / 일정 (제안)
- Lobby 씬 생성 + Start/Ready UX: 4-6h, 김회인 담당
- 5/15 ~ 5/16 작업

---

## 6. PK 방지 (4.1) 회신 — 방향 정정

### 코드 실측 결과 — Layer 기반 필터링은 이미 정상

| 항목 | 설정 |
|---|---|
| Layer 정의 (`TagManager.asset`) | Player = 10, Enemies = 13 |
| `Test_shm.prefab` Layer | 10 (Player) |
| `TestKhi_MinimalCharacter2D.prefab` Layer | 10 (Player) |
| Enemy 프리팹 Layer | 13 (Enemies) |
| Player 무기 `TestKhi_Sword` `TargetLayerMask` | 8448 = Obstacles(8) + Enemies(13) → **Player Layer 미포함** |
| Enemy 무기 `OrcMeleeWeapon` `TargetLayerMask` | 1024 = Player(10) |
| `CanDamageOwner` | 0 (소유자 자해 방지) |

→ "enemy 팀이 실수해서 LayerMask 가 전체 적용" 이라는 추정은 **사실과 다름**. Layer 셋업과 LayerMask 모두 분리돼 있음.

### 분신 fix 전 PK 가능했던 원인 추정

scene-placed Player + NGO spawn 분신 두 캐릭터가 동시 존재 → 그 사이 collider / Health 공유 동작이 비정상 데미지 경로를 만들었을 가능성. **분신 자체가 사라지면 PK 도 자연 해소될 가능성**.

### 회신 결론

1. **던전 분신 fix 가 먼저** — 던전 8개 씬에서 분신 제거하고 NGO spawn 만 남기는 작업 (§4) 이 끝나야 PK 가 분신과 무관한 문제인지 깨끗하게 검증 가능
2. 던전 fix 후 2인 세션에서 PK 재현 테스트
3. 재현 안 됨 → **추가 작업 0**. Layer 셋업으로 충분
4. 재현됨 → 어떤 데미지 경로가 Layer 체크 누락인지 핀포인트 후 그 경로만 수정

작업 시점: 던전 분신 fix 완료 후. 작업자: 클라 (백엔드 무관).

---

## 6.5 Editor 단일 씬 테스트 워크플로우 보존 (중요)

### 우려
백엔드 권장대로 던전 8개 씬에서 scene-placed TestKhi 를 모두 제거하면, **Unity Editor 에서 Dungeon_1F_*.unity 를 단독으로 Play 했을 때 캐릭터가 없어서 테스트 불가**. 일상 개발 / 회귀 검증 워크플로우 중단됨.

### 해결 — 조건부 scene-placed 캐릭터 패턴

#### 신규 컴포넌트 `EditorTestCharacterMarker`
```csharp
public class EditorTestCharacterMarker : MonoBehaviour
{
    void Awake()
    {
        // NGO 가 켜져있다 = 정상 멀티 흐름 (Lobby → Dungeon)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            Destroy(gameObject);
        // NGO 비활성 = Editor 단일 씬 직접 실행 → 그대로 보존
    }
}
```

→ 8개 던전 씬의 scene-placed TestKhi 에 이 컴포넌트만 부착. 작업 30분.

#### `LocalPlayerResolver` 에 fallback 통합
```csharp
public static Character LocalPlayer
{
    get
    {
        if (_localPlayer == null)
            _localPlayer = Object.FindFirstObjectByType<Character>();
        return _localPlayer;
    }
}
```
Editor 단일 씬 테스트 시 LocalPlayerResolver 가 비어있어도 씬에서 직접 찾음. 기존 `PlayerHUDPresenter` 의 fallback 패턴을 LocalPlayerResolver 본체로 끌어올리는 것. 작업 15분.

### 작동 매트릭스

| 시나리오 | NGO | scene-placed TestKhi | UI wiring | 결과 |
|---|---|---|---|---|
| Editor 단일 씬 Play (Dungeon_1F_3R 등) | 비활성 | 살아남 | LocalPlayerResolver fallback 으로 scene 캐릭터 잡음 | ✓ 테스트 가능 |
| 정상 흐름 (Title → Lobby → Dungeon) | 활성 | 자동 Destroy | NGO spawn 캐릭터를 PlayerMovementSync.OnNetworkSpawn 에서 Register | ✓ 정상 |
| 빌드된 게임 멀티 세션 | 활성 | 자동 Destroy | 동일 | ✓ 정상 |

### (옵션) Editor 씬 부트스트랩
Dungeon 환경 (PlayerRunState, GoldWallet, Relic 초기값, 적 컨테이너 활성화 등) 을 자동 초기화하는 `EditorSceneBootstrap` 컴포넌트 추가 시 단일 씬 테스트가 정상 던전 상태로 시작됨. 1-2h.

### 추가 작업 비용
- `EditorTestCharacterMarker`: 30분
- `LocalPlayerResolver` fallback: 15분
- (옵션) `EditorSceneBootstrap`: 1-2h
- **합계 1-3h** 추가 → 마이그레이션 후에도 Dungeon 씬 단독 Play 워크플로우 그대로 유지

---

## 7. 추가 사이드 이슈

### 7.1 PlayerPrefab 캐릭터

`Test_shm` 은 TestKhi 재클론 임시본. 발표 전 정식 캐릭터 프리팹에 NetworkObject + PlayerMovementSync 부착해서 교체 권장. 캐릭터 선택 UI 가 있다면 캐릭터별 PlayerPrefab variant 필요.

**제안**: 던전 NGO 통합 작업 (§4) 과 같은 PR 에서 교체. 일정: 1-2h 추가.

### 7.2 NetworkManager prefab 파일명 트레일링 스페이스

`Assets/_Project/Prefabs/Network/NetworkManager .prefab` — 이름 끝에 스페이스. 경로 매칭 / Git 이슈 우려. 별도 PR 로 rename + 참조 GUID 검증 권장 (15분).

---

## 8. 후속 작업 일정 — 클라 측

발표 D-7 까지 5/15 ~ 5/19 4일 활용.

| Day | 작업 | 추정 |
|---|---|---|
| 5/15 (목) | **던전 Canvas / 씬 wiring audit** (8개 씬 × 컴포넌트별 참조 매핑) + LocalPlayerResolver 마이그레이션 패턴 설계 + Lobby 씬 기본 셋업 | 6-8h |
| 5/16 (금) | **던전 8개 씬에 `EditorTestCharacterMarker` 부착 (scene-placed TestKhi 조건부 보존)** + Canvas wiring 마이그레이션 (Canvas / Boss UI / Minimap / BuildManager / Relic wiring) + `LocalPlayerResolver` fallback 통합 | 6-8h |
| 5/17 (토) | Dungeon NGO 진입부 (`StartHost` Town → Dungeon 전환) + Lobby UX (Start/Ready 버튼 + 세션 코드 표시) + PlayerPrefab 정식 캐릭터 교체 검토 | 5-7h |
| 5/18 (일) | 4인 코옵 회귀 검증 (Lobby → Dungeon 8개 씬 각각) + PK 재현 테스트 → 필요 시 fix + NetworkManager prefab rename | 3-5h |
| 5/19 (월) | 발표 데모 시나리오 리허설 + 잔여 버그 fix 버퍼 | — |

**총 20-29h** (이전 추정 13-22h 대비 약 +7h, 워크플로우 보존 +1h 포함). D-7 일정상 PK 재현 시 추가 fix 가 크면 위험.

### 일정 위험 대비
- Canvas wiring 마이그레이션 작업량이 audit 후 8h 초과로 추정되면 **PlayerPrefab 정식 캐릭터 교체** 와 **NetworkManager prefab rename** 은 발표 후로 보류
- PK 가 던전 분신 fix 후에도 재현되면 발표용은 **무기 LayerMask 확인 + 임시 비활성화** 로 처리하고 정공법은 발표 후

---

## 9. 백엔드에 추가 요청

- `LostMemoryRelayTransport` 의 4인 동시 접속 stress 테스트 결과 공유 (1대1 검증은 완료, 2-4인 검증 여부 확인 필요)
- 솔로 = 1인 세션 NGO 진입 시 backend `sessions` row 생성 + 자동 종료 (게스트 0 + 호스트 이탈) 정책 동작 확인
- 세션 코드 발급 / 검증 API 명세 (Lobby UI 의 코드 표시 / 입력 연결)

---

## 10. 검증 방법 (후속 작업 완료 후)

- Lobby: 호스트 / 게스트 1대1 세션 생성·진입·이탈 사이클 (현행 Test_Town 패턴 그대로)
- **던전 8개 씬 각각 회귀**: 솔로 진입 / 멀티 진입 시 캐릭터 분신 없음 + Canvas (HP, BuildManager UI, Boss HP, Minimap, Relic 패널, KhiWeapon/Parry/Combo 상태 표시) 정상 동작
- **Editor 단일 씬 테스트 회귀**: 던전 8개 씬을 Editor 에서 단독 Play 했을 때 scene-placed TestKhi 보존 + UI 동작 + 적 사냥 가능 확인
- 4인 코옵: Lobby → Dungeon 진입 시 4명 spawn + UI 격리 + HP / 인벤토리 격리
- PK 재테스트: 던전 분신 fix 후 2인 세션에서 서로 무기 휘둘러 데미지 없음 확인
- 마을 회귀: 마을 데이터 (MemoryShardWallet, PlayerWallet) Lobby / Dungeon 진입·이탈에도 유지

---

## 변경 대상 파일 (구현 단계 참고)

### 신규
- `LostMemory/Assets/_Project/Scenes/Lobby/Lobby.unity`
- `LostMemory/Assets/_Project/Scripts/Runtime/Networking/Lobby/LobbyController.cs` (Start / Ready 로직)
- `LocalPlayerResolver` 확장 — `T GetComponentOnLocalPlayer<T>()` API + Editor 단일 씬 테스트용 `FindFirstObjectByType<Character>()` fallback 통합
- `LostMemory/Assets/_Project/Scripts/Runtime/Networking/Player/EditorTestCharacterMarker.cs` — NGO 비활성 시 scene-placed 캐릭터 보존, 활성 시 자동 Destroy
- (옵션) `EditorSceneBootstrap.cs` — Editor 단일 씬 테스트용 던전 환경 초기화

### 던전 8개 씬 수정 (scene-placed TestKhi 에 `EditorTestCharacterMarker` 부착 + spawn 위치 anchor)
- `LostMemory/Assets/_Project/Scenes/Dungeon/Dungeon.unity`
- `Dungeon_1F_1R.unity` ~ `Dungeon_1F_4R.unity`
- `Dungeon_1F_Boss.unity`
- `Dungeon_1F_Shop.unity`
- `Dungeon_1F_Shop_testkhi.unity`

### Canvas / 씬 wiring 마이그레이션 (SerializeField → LocalPlayerResolver 경유)
- Boss / Combat UI 컴포넌트들 (TestKhi 의 `KhiWeaponPresenter`, `KhiParryController`, `KhiMeleeComboController`, `KhiPlayerAim`, `KhiPlayerStateAggregator` 를 SerializeField 참조하는 측)
- `LostMemory.Relics.BuildManager`, `LostMemory.Relics.PlayerRelicInventory` 참조 측

### 기타 수정
- Town → Dungeon 전환 코드 (`StartHost` 진입부)
- (조건부) `PlayerDamageReceiver.cs` (던전 분신 fix 후에도 PK 재현 시 Layer 체크 보강)
- rename: `NetworkManager .prefab` → `NetworkManager.prefab` (트레일링 스페이스 제거)

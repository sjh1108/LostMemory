# 2026-05-25 세션 — 멀티 환경 fix 모음

> 작성일: 2026-05-25
> 브랜치: `mulit_play_fix`
> 마지막 commit: `732ae6151b multi bug 수정 및 연결`
> 모든 plan 파일 출처: `~/.claude/plans/4-snazzy-canyon.md` (시간순으로 덮어쓴 단일 파일)

이 문서는 시연 직전 멀티플레이 안정화 세션 동안 적용된 7개 fix 를 모았다. 각 fix 는 독립 plan 으로 진행됐고, 모두 동일 커밋에 들어갔다.

---

## 1. InventoryFullModal — 씬 전환 시 modal 소멸 (DontDestroyOnLoad 추가)

**증상**: 호스트도 게스트도 던전에서 인벤토리 가득 차도 모달 안 뜸. Player.log 에 `[InventoryFullModal]` 로그 0개.

**근본 원인**: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 의 Bootstrap 은 **앱 시작 시 1회**만 호출. 타이틀/로비 씬에서 modal GameObject 생성 → 던전 LoadScene 시 modal **소멸** → Bootstrap 재호출 안 됨 → 던전에 modal 없음.

**Fix** (`InventoryFullModal.cs`):
- `Bootstrap` 에 `SceneManager.sceneLoaded` 구독 추가 — 매 씬 로드마다 modal 존재 확인 + 없으면 재생성
- `OnEnable` 에 `DontDestroyOnLoad(gameObject)` 추가 — 한 번 만들어진 modal 영속화

**검증**: Player.log 에 `[InventoryFullModal] Bound to '...(Clone)'` + `[InventoryFullModal] HandleRejected` 시퀀스 나옴. 기존 self-heal 로직 (이전 fix `2026-05-24-inventory-full-modal-multi-fix.md`) 과 결합 — 씬 전환 후에도 새 LocalPlayer inventory 로 재bind.

---

## 2. InventoryFullModal — 한글 폰트 깨짐 fix

**증상**: 모달 텍스트가 □□□□ 로 표시 (TMP LiberationSans 에 한글 글리프 없음).

**Fix** (`InventoryFullModal.cs` `AddText`):
- 씬의 다른 TMP_Text 컴포넌트에서 폰트 빌려옴 (Galmuri9 등)
- `BorrowSceneFont()` helper — LiberationSans 이름 가드로 잘못 빌려오기 방지
- 검증된 패턴 — `TooltipView.BorrowSceneFont` 동일

```csharp
private static TMP_FontAsset BorrowSceneFont()
{
    TMP_Text[] existing = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
    foreach (TMP_Text txt in existing)
    {
        if (txt != null && txt.font != null && txt.font.name != "LiberationSans SDF")
            return txt.font;
    }
    return null;
}
```

---

## 3. 인벤토리 그리드 4×4 vs 5×5 불일치 fix

**증상**: 인벤토리 UI 는 4×4 (16칸) 표시인데, 1×1 아이템 17개 받아도 reject 안 떠서 모달 미발화.

**근본 원인** — Prefab Inspector override:

| Prefab | `_baseMaxSlots` | `_maxCols` | `_maxRows` |
|---|---|---|---|
| `TestKhi_MinimalCharacter2D.prefab` (현 사용) | **25 → 16** | **5 → 4** | **5 → 4** |
| `TestKhi_Net_AD.prefab` (미사용, 이미 4×4) | 16 | 4 | 4 |

코드 SerializeField 기본값은 4×4 였지만 player prefab 이 inspector 에서 5×5 로 override.

**Fix**: `TestKhi_MinimalCharacter2D.prefab` line 1106-1108 의 3개 값을 4×4 / 16 으로 정정. `ShopUIBuilder.cs:572` 의 UI `constraintCount = 4` 하드코딩과 일치.

**파일**: `Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` (현재 워킹 디렉토리에 미커밋 상태)

---

## 4. Build Settings — Test_MultiLobby_Copy 등록

**증상**: 호스트 생성 클릭 시 NGO `NetworkSceneManager.LoadScene` 에서 `Scene 'Test_MultiLobby_Copy' couldn't be loaded — not in build settings`.

**근본 원인**: 새 멀티 로비 씬 `Assets/_Project/Scenes/multi/Test_MultiLobby_Copy.unity` 가 Build Settings 의 Scenes In Build 리스트에 미등록. 기존엔 `Test_MultiLobby_Copy_old.unity` (다른 경로) 만 등록되어 있었음.

**Fix** (`ProjectSettings/EditorBuildSettings.asset`): 신규 entry 추가
```yaml
- enabled: 1
  path: Assets/_Project/Scenes/multi/Test_MultiLobby_Copy.unity
  guid: eed1e46d1db24524bba35189a72993aa
```

`Test_MultiLobby_Copy_old.unity` 와 이름 다르므로 NGO `LoadScene("Test_MultiLobby_Copy")` 검색 충돌 없음.

---

## 5. Test_MultiLobby_Copy 씬 — 재능/기억 NPC + Panel 추가

**증상**: 멀티 로비 씬에 진입한 후 재능/기억 패널이 열리지 않음. 마을(Town.unity) 에서는 정상.

**근본 원인**: 멀티 로비 씬은 마을에서 가져올 때 NPC interactable + panel prefab 인스턴스를 누락. `MemoryNpcInteractable` GameObject 는 있었지만 `_unlockPanel: fileID: 0` (null).

**Fix** (`Assets/_Project/Scenes/multi/Test_MultiLobby_Copy.unity`):

| 추가/연결 | 위치 |
|---|---|
| `TalentPanel` prefab 인스턴스 | Canvas 자식 |
| `MemoryUnlockPanel` prefab 인스턴스 | Canvas 자식 |
| `KhiInteractionPrompt` 의 `OnActivate` → `TalentPanelView.Toggle()` | ButtonInteract NPC |
| `MemoryNpcInteractable._unlockPanel` ← 새 MemoryUnlockPanel | 기존 NPC |

**활용 prefab**:
- `Assets/_Project/Prefabs/UI/TalentPanel.prefab`
- `Assets/_Project/Prefabs/UI/MemoryUnlockPanel.prefab`

**NPC 동작**: F/E 키로 패널 토글. `_requirePlayerInRange = true` 라 NPC 콜라이더 안에서만 작동.

---

## 6. TalentStartupApplier — LocalPlayer 측 container 정확 적용

**증상 (audit 결과 발견)**: 던전 진입 시 재능 stat 이 호스트/게스트 중 잘못된 player container 에 적용될 위험.

**근본 원인**:
- `TalentStartupApplier` 는 dungeon scene 의 별도 GameObject
- `_container` SerializeField 는 scene-placed Player 의 container 에 inspector wire 됨
- **NGO 활성 시 `EditorTestCharacterMarker` 가 scene-placed Player 를 즉시 destroy** → `_container` Unity pseudo-null → `ResolveRefsIfNeeded` fallback 의 `FindAnyObjectByType<PlayerStatModifierContainer>` 가 owner 비구분 race

**Fix** (`TalentStartupApplier.cs`):
- `ResolveRefsIfNeeded` 의 `_container` / `_relicInventory` 를 `LocalPlayerResolver.GetComponentOnLocalPlayer<T>()` 로 변경
- `IsLocalPlayerComponent` helper 추가 (잘못된 binding self-heal)
- `OnEnable`/`OnDisable` 에 `LocalPlayerReady` 구독 + `HandleLocalPlayerReady` 메서드 추가 (게스트 spawn 지연 대응)

**검증된 utility 재사용**: `LocalPlayerResolver.GetComponentOnLocalPlayer<T>()` — InventoryFullModal fix 에서 검증.

---

## 7. TalentPanelView — 마을 즉시 적용 시 LocalPlayer 측 container 사용

**증상 (audit 결과 발견)**: 마을/로비에서 재능 투자 직후 stat 즉시 적용 path 가 같은 race 위험.

**근본 원인**: `TalentPanelView.ApplyStatsImmediately()` line 153:
```csharp
PlayerStatModifierContainer container =
    FindAnyObjectByType<PlayerStatModifierContainer>(FindObjectsInactive.Include);
```
`TalentStartupApplier` fix 와 동일 패턴인데 미적용 → 마을 HUD 가 잘못된 player stat 표시 가능.

**Fix** (`TalentPanelView.cs` line 151-179):
```csharp
PlayerStatModifierContainer container =
    LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerStatModifierContainer>()
    ?? FindAnyObjectByType<PlayerStatModifierContainer>(FindObjectsInactive.Include);
```
LocalPlayer 우선, 솔로/Editor 단일 씬은 fallback.

추가: using 문에 `LostMemory.Networking.Player` 추가.

---

## 멀티 환경 데이터 분리 audit 결과 (참고)

| 시스템 | 분리 상태 |
|---|---|
| `TalentSaveService` (PlayerPrefs) | ✓ 머신 독립 |
| `MemoryMetaService` (PlayerPrefs) | ✓ 머신 독립 |
| `MemoryUnlockPanelView` | ✓ player-side 미접근 |
| `MemoryShardWallet` Singleton | ✓ MPPM/빌드 각 Unity 프로세스 별로 분리 |
| `TalentStartupApplier` | ✓ fix #6 이후 LocalPlayer 기반 |
| `TalentPanelView.ApplyStatsImmediately` | ✓ fix #7 이후 LocalPlayer 기반 |

**MPPM 한계**: 같은 PC 에서 MPPM 가상 인스턴스가 동일 product name 으로 `HKCU\Software\DefaultCompany\LostMemory` registry 공유 → PlayerPrefs 가 공유될 수 있음. 단 panel 열 때마다 `TalentSyncService.PullAsync` 가 user ID 별 서버 데이터로 덮어쓰므로 실 사용에선 분리. **실제 시연 / 별도 PC 배포 시 정상 작동**.

---

## 변경 파일 요약

### 커밋된 변경 (`732ae6151b multi bug 수정 및 연결`)

| 파일 | fix |
|---|---|
| `Scripts/Runtime/Shop/InventoryFullModal.cs` | #1, #2 |
| `Scripts/Runtime/Talents/TalentStartupApplier.cs` | #6 |
| `Scripts/Runtime/Talents/TalentPanelView.cs` | #7 |
| `Scenes/multi/Test_MultiLobby_Copy.unity` | #5 (+ #4 가능) |
| `ProjectSettings/EditorBuildSettings.asset` | #4 |
| `DefaultNetworkPrefabs.asset` | 기타 멀티 prefab 등록 |
| `Map/Modules/**/*.prefab` | 기타 멀티 prefab 정리 |

### 미커밋 변경 (working tree)

| 파일 | fix |
|---|---|
| `Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` | **#3** (4×4 grid) |
| `Map/Modules/1F_BOSS.prefab` | 시연 보정 |
| `Map/Modules/Module_Bridge 1.prefab` | 시연 보정 |
| `Map/Modules/Shop/ShopRoom_Sample.prefab` | 시연 보정 |
| `Art/Fonts/malgun SDF.asset` | 폰트 통합 |

---

## 재사용된 검증 utility (참고)

| 위치 | 용도 |
|---|---|
| `LocalPlayerResolver.GetComponentOnLocalPlayer<T>()` (line 115) | LocalPlayer 측 컴포넌트 단일 진입점 — fix #1, #6, #7 모두 사용 |
| `LocalPlayerResolver.LocalPlayerReady` event (line 122) | Spawn race 대응 lazy 재시도 |
| `LocalPlayerResolver.LocalCharacter` NGO 가드 | NGO 활성 시 `FindFirstObjectByType<Character>()` fallback 차단 (기존 fix) |
| `TooltipView.BorrowSceneFont` 패턴 | 한글 TMP 폰트 동적 빌림 |

---

## 검증 체크리스트 (시연 직전)

- [ ] 호스트로 던전 진입 → F8 16개 픽업 → 17번째 reject + 모달 표시 (fix #1, #2, #3)
- [ ] 멀티 로비 진입 → F 키 → 재능 패널 / 기억 패널 정상 (fix #4, #5)
- [ ] 마을에서 재능 1포인트 투자 → HP 즉시 증가 (fix #7)
- [ ] 던전 진입 직후 `[TalentStartupApplier] LocalPlayerReady 수신 → refs 재조회` 로그 (fix #6)
- [ ] 게스트 측 검증 (가능 시) — 각자 재능 stat 독립 적용

## 후속 follow-up (시연 후 권장)

1. NPC GameObject 들 prefab 화 — 마을/멀티로비 wiring 중복 제거
2. UI panel auto-spawn 패턴 통일 — `RuntimeInitializeOnLoadMethod + sceneLoaded` 헬퍼 추출
3. `PlayerStatModifierContainer` 등 player-side 컴포넌트 조회 패턴 단일 진입점화 (현재 LocalPlayerResolver.GetComponentOnLocalPlayer 산발 사용)
4. 다른 멀티 씬 (`Multi_Test/*`) 동일 누락 audit

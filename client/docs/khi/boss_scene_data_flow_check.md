# Dungeon_1F_Boss 씬 데이터 흐름 누락 점검 & 보강 계획

## Context

사용자는 `Dungeon_1F_1R ~ 4R` 씬의 데이터 흐름(인벤토리, 재능, 세트 효과, 보상)이 정상 작동함을 확인. 같은 흐름이 `Dungeon_1F_Boss` 씬에서도 유지되는지 확인 필요.

씬 YAML 의 GUID 추적과 핵심 클래스의 수명주기(`DontDestroyOnLoad` 여부) 분석으로, **prefab 안에 들어있어서 자동으로 따라오는 컴포넌트** vs **씬에 직접 배치돼야 하는 GameObject** 를 구분함.

---

## 1. 수명주기 / 소속 분류

### A. DontDestroyOnLoad — 1R 에서 만들어지면 Boss 까지 자동 따라옴

| 클래스 | 경로 |
|---|---|
| `RunManager` | [RunManager.cs:111](LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs) |
| `StageRouteManager` | [StageRouteManager.cs:84](LostMemory/Assets/_Project/Scripts/Runtime/Stage/StageRouteManager.cs) |
| `PlayerRunState` | [PlayerRunState.cs:42](LostMemory/Assets/_Project/Scripts/Runtime/Player/PlayerRunState.cs) |
| `PlayerWallet` | [PlayerWallet.cs:46](LostMemory/Assets/_Project/Scripts/Runtime/Player/PlayerWallet.cs) |
| `MemoryShardWallet` | [MemoryShardWallet.cs:67](LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemoryShardWallet.cs) |
| `Stage1BgmController` | [Stage1BgmController.cs:36](LostMemory/Assets/_Project/Scripts/Runtime/Stage/Stage1BgmController.cs) |

### B. Player prefab(`TestKhi_MinimalCharacter2D.prefab`) 내부 컴포넌트 — prefab 인스턴스만 있으면 자동 포함

| 컴포넌트 | prefab 안 카운트 |
|---|---|
| `BuildManager` | 1 |
| `SetEffectApplicator` | 1 |
| `PlayerRelicInventory` | 1 |

Boss 씬의 player prefab(`4d290d1f…`) 인스턴스 카운트 = 12 (1R 과 동일) → **3개 모두 Boss 씬에 이미 존재함**. 처음 “BuildManager 가 Boss 에 없다” 진단은 stripped MonoBehaviour entry 만 보고 내린 오진이었음.

### C. 씬에 직접 배치돼야 하는 외부 prefab / scene-direct GameObject — **Boss 씬에서 진짜 누락**

| 항목 | 형태 | 1R prefab GUID / fileID | Boss 카운트 |
|---|---|---|---|
| `TalentStartupApplier` | 외부 prefab 인스턴스 (`87528d94…`) | `Assets/_Project/Prefabs/System/TalentStartupApplier.prefab` | **0** |
| `InventoryPanel` (UI) | 외부 prefab 인스턴스 (`60cdd546…`) | `Assets/_Project/Prefabs/UI/InventoryPanel.prefab` | **0** |
| `InventoryToggleController` | scene-direct GameObject | 1R `&695301356` | **0** |

---

## 2. 1R 씬에서의 정확한 위치 (Boss 에 복제할 대상)

### 2-1. `InventoryToggleController` (scene-direct GameObject)
- 1R `&695301356`, m_Name=`InventoryToggleController`, Layer=5 (UI)
- 부모: **`Canvas`** (1R `&1992835322`, 씬 루트의 UI Canvas) — 자식 9개 중 마지막
- SerializeField wiring (1R 기준):
  - `panel` → InventoryPanel prefab instance (1R `&1617425738`)
  - `playerRelicInventory` → player prefab 안의 PlayerRelicInventory (1R `&2006321053`, stripped)
  - `goldWallet` → 1R `&384745938`
  - `rewardController` → 1R `&384745936`
  - `shopController` → 비어있음(null)
  - `buildManager`, `setEffectPanel` → 비어있음(코드에 새로 추가된 SerializeField — 1R 도 wiring 미완성)
- 토글 키: KeyCode.I (105)

### 2-2. `InventoryPanel` (UI prefab instance)
- 1R PrefabInstance `&127387768`, prefab GUID `60cdd546cf781cb46971530d64c4e8a7`
- 부모: 같은 **`Canvas`** (1R `&1992835322`) — 자식 9개 중 첫 번째
- → 결국 “Canvas 의 자식으로 `InventoryPanel` prefab + `InventoryToggleController` GameObject 가 짝으로 있는 구조”

### 2-3. `TalentStartupApplier` (외부 prefab instance)
- 1R PrefabInstance, prefab GUID `87528d944c1db91408e8f1aaaec33328`
- `m_TransformParent: {fileID: 0}` → **씬 루트** (Canvas 가 아닌 최상위)
- Prefab override (wiring 필요):
  - `_bootstrap` → 1R `&122087497`
  - `_container` → 1R `&2113634227`
  - `_relicInventory` → player prefab 안의 PlayerRelicInventory (1R `&2006321053`)
  - 위치: (-4.12619, -0.83916, 0)

### 2-4. Player prefab 내부 컴포넌트들의 1R prefab override
- 1R 의 player prefab instance 는 `BuildManager`(`&1683277796 stripped`), `PlayerRelicInventory`(`&2006321053 stripped`) 에 인스턴스 단위 override 가 있음.
- 이 override 가 무엇인지(예: `_setDatabase` 의 SO 배열, scene-direct GoldWallet 참조 같은 것)는 별도 확인 필요. Boss 의 player prefab instance 에 동일 override 가 들어가있는지는 아직 비교 안 함 — **혹시 Boss instance 가 override 없는 “bare” 상태라면 SerializeField 가 비어 NullRef 가능**.

---

## 3. Boss 씬에 추가할 작업

### 사전 정보 — 1R wiring 의 정체

1R 의 SerializeField 가 가리키는 4개 컴포넌트는 모두 **stripped MonoBehaviour** (prefab 인스턴스 안의 컴포넌트). 클래스 GUID 로 역추적:

| 1R fileID | 클래스 | Boss 씬에 이미 존재? |
|---|---|---|
| `&384745936` | `RewardController` ([RewardController.cs](LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs)) | ✅ |
| `&384745938` | `GoldWallet` ([GoldWallet.cs](LostMemory/Assets/_Project/Scripts/Runtime/Stage/GoldWallet.cs)) | ✅ |
| `&122087497` | `DungeonRunBootstrap` ([DungeonRunBootstrap.cs](LostMemory/Assets/_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs)) | ✅ |
| `&2113634227` | `PlayerStatModifierContainer` ([PlayerStatModifierContainer.cs](LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerStatModifierContainer.cs)) | player prefab 내부일 가능성 ⇒ ✅ |

→ wiring 대상이 모두 Boss 씬에도 이미 있음. 그대로 드래그하면 됨.

### Editor 작업 순서 (코드 변경 없음, 씬 데이터만)

#### 1단계: 1R 에서 복사

1. `Dungeon_1F_1R.unity` 열기.
2. Hierarchy 에서 **Canvas 펼치기** → 자식 중 다음 두 개를 Ctrl+클릭으로 같이 선택:
   - `InventoryPanel` (prefab instance — 파란색 아이콘)
   - `InventoryToggleController` (일반 GameObject)
3. Ctrl+C.

#### 2단계: Boss 에 붙여넣기 (Canvas 하위)

4. `Dungeon_1F_Boss.unity` 열기.
5. Hierarchy 에서 Boss 씬의 **`Canvas` 선택** → Ctrl+V.
   - 둘 다 Canvas 자식으로 추가됨.

#### 3단계: TalentStartupApplier 별도 복사

6. 다시 `Dungeon_1F_1R.unity` 로 전환.
7. Hierarchy 의 **씬 루트(최상위) 에서 `TalentStartupApplier`** 선택 → Ctrl+C.
8. `Dungeon_1F_Boss.unity` 로 전환 후 Hierarchy 의 **빈 공간 클릭** (선택 해제) → Ctrl+V.
   - 씬 루트에 붙어야 함. 만약 Canvas 자식으로 들어갔다면 드래그로 루트로 빼기.

#### 4단계: SerializeField 재바인딩 (Boss 씬, 핵심)

씬 간 참조는 Unity 가 자동 복원 못 하므로 빨간 표시(Missing) 나 None 으로 나옴. 다음 표대로 Boss 씬의 Hierarchy 에서 드래그해 채움.

##### `InventoryToggleController` (Boss > Canvas > InventoryToggleController)
| 필드 | 무엇을 드래그? |
|---|---|
| `Panel` | 방금 붙여넣은 `Canvas > InventoryPanel` |
| `Player Relic Inventory` | Boss 씬의 player prefab 인스턴스(예: `TestKhi_MinimalCharacter2D`) 의 `PlayerRelicInventory` 컴포넌트 |
| `Gold Wallet` | Boss 씬의 `GoldWallet` 컴포넌트가 붙은 GameObject (player prefab 안에 있을 가능성 큼 — 인스펙터 검색창에 `GoldWallet` 입력하면 빨리 찾음) |
| `Set Effect Panel` | None 그대로 둠 (1R 도 비어있음, 새 작업) |
| `Build Manager` | None 그대로 둠 (1R 도 비어있음, 새 작업) |
| `Shop Controller` | None 그대로 둠 (1R 도 비어있음 — Shop 씬에서만 wiring) |
| `Reward Controller` | Boss 씬의 `RewardController` 컴포넌트가 붙은 GameObject |

##### `TalentStartupApplier` (Boss 씬 루트)
| 필드 | 무엇을 드래그? |
|---|---|
| `_bootstrap` | Boss 씬의 `DungeonRunBootstrap` 컴포넌트가 붙은 GameObject |
| `_container` | Boss 씬의 `PlayerStatModifierContainer` 컴포넌트 (player prefab 안에 있을 가능성 큼) |
| `_relicInventory` | Boss 씬의 player prefab 안의 `PlayerRelicInventory` 컴포넌트 (위와 동일) |

#### 5단계: 저장 & Play 검증

9. Ctrl+S 로 Boss 씬 저장.
10. RunManager 가 Boss 씬을 직접 로드하는 디버그 진입 또는 1R → … → Boss 정상 진입.
11. Console 확인 사항:
    - ✅ `[BuildManager] OnEnable — inventory 구독 시작 … ownedCount=N`
    - ✅ `[InventoryToggleController]` null 에러 **없음**
    - ✅ I 키 누르면 인벤토리 패널 뜸
    - ✅ 재능 보너스 (스탯/이속/공속 등) 가 보스전에서 적용됨

---

## 4. 보상 흐름 (의도 확인 — 변경 없음)

`RewardController` 는 Boss 씬에도 존재. 다만 [RewardController.cs:133-137](LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs) 에서 `RoomType != Combat` 은 명시적으로 스킵, [RunManager.cs:621-655](LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs) 는 Boss 클리어 시 포탈만 열고 보상 호출 안 함 — **의도된 설계**. 보스 클리어 보상이 필요하면 별도 작업으로 분리.

---

## 5. 수정 대상 파일

- 씬 데이터만 변경: [Dungeon_1F_Boss.unity](LostMemory/Assets/_Project/Scenes/Dungeon/Dungeon_1F_Boss.unity)
- 참조 기준: [Dungeon_1F_1R.unity](LostMemory/Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity) (2R/3R/4R 도 동일)
- 코드 변경 없음.

---

## 6. Verification

1. **재능** — 재능 선택 후 보스 진입 시 재능 보너스(데미지/이속 등) 적용 여부 확인.
2. **인벤토리 UI** — Boss 씬에서 I 키 → 인벤토리 패널 뜨는지. 보스 인트로(`BossSceneAutoStarter`, `BossIntroSequenceController`) 와 입력 충돌 점검.
3. **세트 효과** — Boss 진입 직후 Console 에 `[BuildManager] OnEnable` + 잠시 후 `tier ... → ...` 로그. `BuildManager._logTierChanges = true` 임시 설정 권장.
4. **회귀** — 1R~4R 흐름 변화 없는지 (수정 대상은 Boss 씬뿐).

# 씬 매니저 prefab 화 + catalog 정리 — 계획

> **티켓 번호 미정** — Jira 신설 후 `cl<번호>_*_plan.md` 로 rename 권장.
> Epic Q (1차 MVP 통합 데모 흐름) 의 후속 / 또는 별도 *유지보수 epic* 으로 분류 가능.

## Context

CL-115 (`feat/S14P31C201-354/cl-115-...`) 작업 중 두 가지 wiring 회귀 발견:

1. **`MVP1_testkhi.unity` 의 RunManager.goldWallet 슬롯이 null** — Combat 클리어 시 +50 hook silent fail
2. **`MVP1_testkhi.unity` 의 ShopController.inventoryPanel 슬롯이 null** — Shop 열림 시 인벤토리 패널 안 같이 뜸

두 회귀 모두 *매니저 GameObject 들이 prefab 화 안 돼있어서* 씬마다 매번 *수동 wiring* 이 필요한 데서 비롯됨. 다른 팀원이 시연 씬 / 테스트 씬 만들 때마다 동일 회귀 위험.

본 ticket = **재사용 가능 시스템 prefab + 사용 매뉴얼** 정비.

### 책임 분리

- **김회인 (본인)**: 모든 작업 (시스템 prefab + 컴포넌트 + 코드 + 문서)
- **노소연 (UI 영역)**: 본 ticket 의 *UI prefab* (InventoryPanel, ShopPanel, RewardPanel) 은 *이미 prefab 화 됨* (CL-194/CL-342). 본 ticket 은 *그것을 묶는 시스템 prefab* 만 신규 — 노소연 영역 미침범

## 목표

1. **재사용 가능한 시스템 prefab 4 종 신설** — 다른 씬에 끌어다 놓으면 즉시 동작
2. **각 prefab 의 사용 설명** — Inspector 에서 보이는 `PrefabReadme` 컴포넌트 + git markdown catalog
3. **MVP1_testkhi.unity 가 prefab 인스턴스 사용** — 회귀 발생 시 *prefab override* 하나만 고치면 모든 씬에 반영

## 결정 사항

### 1. `PrefabReadme.cs` 컴포넌트 신설

위치: `client/LostMemory/Assets/_Project/Scripts/Runtime/Meta/PrefabReadme.cs`

```csharp
[DisallowMultipleComponent]
[AddComponentMenu("Lost Memory/Meta/Prefab Readme")]
public sealed class PrefabReadme : MonoBehaviour
{
    [Header("Prefab 설명")]
    [TextArea(3, 10), SerializeField] private string description;
    [Header("사용법")]
    [TextArea(2, 10), SerializeField] private string usage;
    [Header("의존성 (이 prefab 이 동작하려면 씬에 함께 있어야 할 것들)")]
    [TextArea(2, 10), SerializeField] private string dependencies;
}
```

각 prefab root 에 부착 → Inspector 에서 *prefab 더블클릭 시 즉시 설명 표시*. 런타임 영향 0 (필드만 가짐).

### 2. 시스템 prefab 4 종 신설

| Prefab | 묶이는 컴포넌트 | 신설 위치 |
|---|---|---|
| **RunSystems.prefab** | RunManager + GoldWallet + PlayerRelicInventory + PlayerConsumableInventory + RunStateMachine | `_Project/Prefabs/Systems/RunSystems.prefab` |
| **RewardSystem.prefab** | RewardController + RewardPanelView (CL-110 prefab 인스턴스) | `_Project/Prefabs/Systems/RewardSystem.prefab` |
| **ShopSystem.prefab** | ShopController + ShopPanelView (CL-194 prefab 인스턴스) + InventoryPanelView (CL-342 prefab 인스턴스) + InventoryToggleController | `_Project/Prefabs/Systems/ShopSystem.prefab` |
| **(선택) PlayerHud.prefab** | 후속에 노소연이 GoldHUD 만들면 그것 묶음 | 후속 ticket |

> *UI 부분* (RewardPanelView / ShopPanelView / InventoryPanelView) 은 *이미 prefab 화 된 노소연 산출물* — 본 ticket 은 *시스템 prefab 안에 nested instance* 로 wiring 만 함. 노소연 prefab 자체엔 변경 X.

### 3. 각 prefab 의 PrefabReadme 내용 (예시)

#### RunSystems
- **description**: 런 한 회 동안의 *상태 / 골드 / 인벤토리* 관리 매니저들 묶음. 씬당 1 개.
- **usage**: 새 던전 씬 만들 때 Hierarchy 에 끌어다 놓으면 됨. RunManager 의 wiring 은 자동 (prefab 안에서 다 연결됨).
- **dependencies**: 같은 씬에 RewardSystem + ShopSystem 도 있어야 보상 / 상점 흐름 정상.

#### RewardSystem
- **description**: Combat 방 클리어 시 보상 카드 3 택 흐름.
- **usage**: 던전 씬에 끌어다 놓고 RunManager 의 rewardController slot 에 wire. RoomEntryRuntimeController 들이 자동으로 RewardController 에 구독됨 (RunManager.HandleDungeonBuilt).
- **dependencies**: RunSystems / Player (Khi*Controller 들 — input 차단용).

#### ShopSystem
- **description**: Shop 방 진입 시 NPC F 키 → 상점 패널 + 인벤토리 동시 표시. ESC 일괄 닫기.
- **usage**: 던전 씬에 끌어다 놓음. ShopRoom_Sample.prefab 의 NPC 가 자동으로 ShopController 호출.
- **dependencies**: RunSystems (GoldWallet / PlayerRelicInventory) / Player (input 차단).

### 4. `docs/khi/prefab_catalog.md` 신설

전체 prefab 목록 + 의존 그래프 + 사용 가이드:

```markdown
# Lost Memory — Prefab Catalog

## 시스템 prefab (본 ticket 산출물)
- RunSystems — Run 한 회 매니저 묶음
- RewardSystem — 보상 카드 흐름
- ShopSystem — 상점 흐름

## 방 module prefab (CL-112 / CL-113)
- CombatRoom_Sample_Small
- ShopRoom_Sample
- (Boss 후속)

## UI prefab (노소연 영역)
- RewardPanel
- ShopPanel
- InventoryPanel
- ShortcutBar
- TooltipPanel

## 의존 그래프
[mermaid 또는 ASCII 다이어그램]
```

### 5. `MVP1_testkhi.unity` 마이그레이션

기존 GameObject 들을 *지우고* prefab 인스턴스로 교체:
- 기존 RunManager + 부속 → RunSystems.prefab 인스턴스
- 기존 RewardController + 부속 → RewardSystem.prefab 인스턴스
- 기존 ShopController + 부속 → ShopSystem.prefab 인스턴스

씬 wiring (예: RoomEntryRuntimeController → RunManager 구독) 은 *런타임에 자동 연결* (FindObjectsOfType 기반) 이라 prefab 화 후에도 정상 동작 예상.

## 핵심 파일

### 신설
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Meta/PrefabReadme.cs`
- `client/LostMemory/Assets/_Project/Prefabs/Systems/RunSystems.prefab`
- `client/LostMemory/Assets/_Project/Prefabs/Systems/RewardSystem.prefab`
- `client/LostMemory/Assets/_Project/Prefabs/Systems/ShopSystem.prefab`
- `docs/khi/prefab_catalog.md`

### 수정
- `client/LostMemory/Assets/Scenes/MVP1/MVP1_testkhi.unity` — prefab 인스턴스 사용
- `client/LostMemory/Assets/Scenes/MVP1/MVP1.unity` (필요 시) — 동일 마이그레이션
- `client/LostMemory/Assets/Scenes/test_khi.unity` (선택) — 테스트 씬에도 적용

## 검증 방법

### 정적
- 컴파일 에러 0
- prefab 4 개 모두 *missing reference 없음*
- `PrefabReadme` 의 description / usage / dependencies 모두 채워짐

### 런타임 (MVP1_testkhi.unity)
- CL-115 의 검증 시나리오 1~6 *그대로 통과* (회귀 X)
- 추가: **새 빈 씬에서 prefab 4 개만 끌어놓고 시연 흐름 동작 확인**
  - Combat 방 클리어 → 보상 카드 → 골드 +50
  - Shop 진입 → F → 상점 + 인벤 표시
  - I 키 단독 / Shop 가드 / ESC 일괄 닫기
  - → 모두 정상이면 prefab 자체 완결성 검증 ✅

### 문서
- `prefab_catalog.md` 의 의존 그래프가 실제 prefab 의존 관계와 일치
- 각 PrefabReadme 의 dependencies 섹션이 catalog md 와 일치

## 위험 / 확인 포인트

1. **씬 → prefab 마이그레이션 시 reference 손실** — Unity 가 GameObject 를 prefab 화하면 *기존 reference 가 끊어질* 수 있음. *unpack → 새 prefab 으로 만들기* 가 안전. test_khi 씬에서 먼저 prototype 권장
2. **RunStateMachine** — 단독 컴포넌트인지 RunManager 의 nested 인지 확인 필요. nested 면 RunSystems 안에 자동 포함, 단독이면 별도 GameObject 추가
3. **PlayerRelicInventory.consumableInventory wiring** — CL-115 의 단축키바 라우팅 fix (제외됨) 와 직접 연관. 본 ticket 진행 시 *그 fix 도 같이 검토* 권장 (PrefabReadme 에 명시)
4. **노소연의 UI prefab nested instance 사용 시** — UI prefab override 는 nested 단계가 깊어지면 헷갈림. 시스템 prefab 은 *UI prefab 인스턴스* 만 *참조* 만 하고 *override 는 안 하는* 정책 권장

## 작업 순서 (예상 3~4 시간)

1. **30 분** — `PrefabReadme.cs` 신설 + Inspector 시각 확인
2. **60 분** — RunSystems.prefab 신설 + 각 매니저 wiring + PrefabReadme 작성
3. **60 분** — RewardSystem / ShopSystem prefab 신설 + 동일 작업
4. **30 분** — MVP1_testkhi.unity 마이그레이션 + 검증 시나리오 1~6 회귀 확인
5. **30 분** — 새 빈 씬에서 prefab 4 개로 minimum 시연 흐름 검증
6. **30 분** — `prefab_catalog.md` 작성 + 의존 그래프 그리기

## 후속 (본 ticket 외)

- **PlayerSystems.prefab** — TestKhi 캐릭터 + 부속 컴포넌트 묶음 (별도 ticket — 캐릭터 영역)
- **CL-119 (시연용 시드 고정)** — 시연 dedicated 씬에 본 prefab 들 사용
- **CL-112 (DA layout)** — DA 도입 시에도 본 prefab 들 그대로 재사용 (DA 가 module spawn 만 대신함)
- **PlayerRelicInventory ↔ PlayerConsumableInventory 라우팅 fix** (CL-115 에서 제외) — 본 ticket 의 RunSystems prefab wiring 시 자연스럽게 fix 가능

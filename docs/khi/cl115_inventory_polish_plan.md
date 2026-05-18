# CL-115 — 골드 통화 최소 + 인벤토리 연동 정식화 (Phase 1) — 계획

## Context

CL-113 ([cl113_shop_integration_completion.md](cl113_shop_integration_completion.md)) 머지 후 후속 ticket. epic 정의 = *"골드 통화 최소 버전 (전투 보상 → 상점 사용)"*.

골드 통화 *흐름 자체* (GoldWallet + Combat +50 hook + Shop 사용) 는 이미 CL-113 에서 동작. 본 CL Phase 1 의 실질 작업은 **인벤토리 연동 정식화** + **개발자 trace 보강**.

### 책임 분리

- **김회인 (본인 = 연동 코드)**: 본 CL 의 모든 작업
- **노소연 (UI / asset)**: 본 CL 미포함 — 정식 골드 HUD prefab / 보상 +50 토스트 prefab / 디자인 / 애니메이션 / 사운드 등은 *후속 UI ticket*

### scope 결정 사유

후보 5 개 중 3 개 채택:
- ✅ **B** (보상 +50 의도 로그) — 1 줄
- ✅ **C** (Shop↔InventoryToggle desync 방지)
- ✅ **D** (ESC 키 일괄 닫기)
- ❌ **A** (골드 HUD) — 정식 디자인 = 노소연 영역. placeholder 도 안 만듦. 골드 trace 는 GoldWallet `logChanges = true` default 로 이미 console 출력
- ❌ **E** (네임스페이스 분리 `LostMemory.Shop` → `LostMemory.UI.Inventory`) — cosmetic refactor + 노소연이 만든 파일 (`InventoryPanelView` 등) 의 namespace 변경은 침범 risk. 동작 영향 X 이므로 보류

---

## 작업 항목

### B. Combat 보상 +50 의도 로그 (1 줄)

**문제**: GoldWallet 자동 로그 (`[GoldWallet] Add(+50) -> Current=N`) 는 *언제 / 얼마* 는 알려주지만 *왜* 는 안 알려줌. 진단 시 "Combat hook 자체가 안 불렸나? GoldWallet 안에서 막혔나?" 분리 어려움.

**수정**: [`RunManager.cs:305-308`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs:305) 의 +50 hook 옆에 의도 로그 1 줄.

```csharp
if (payload.Data.RoomType == StageRoomType.Combat && goldWallet != null)
{
    Debug.Log("[RunManager] Combat clear reward gold +50.");
    goldWallet.Add(50);
}
```

→ Console 출력:
```
[RunManager] Combat clear reward gold +50.
[GoldWallet] Add(+50) -> Current=50
```

### C. Shop ↔ InventoryToggle desync 방지

**문제**: 현재 [`InventoryToggleController.Update()`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/InventoryToggleController.cs:42) 가 `ShopController.IsOpen` 검사 X. Shop 열림 중 I 키 누르면 ShopController 가 연 InventoryPanel 을 InventoryToggleController 가 강제로 Close → ShopController 는 IsOpen=true 인데 InventoryPanel 만 사라진 *시각적 desync*.

**수정**:
- `InventoryToggleController.cs` 에 `[SerializeField] ShopController shopController` 슬롯 (null 허용 — 단독 씬 호환)
- `Update()` 에 Shop 우선 가드 — Shop 열림 중에는 I / ESC 입력 *완전 무시* (ShopController 가 자체 Close 책임)

### D. ESC 키 일괄 닫기

**문제**: 현재 ESC 키 핸들링 0 개. 시연 중 패널 닫는 방법이 *F* (Shop 토글) / *I* (Inventory 토글) 만 — 직관성 낮음.

**수정**:
- **`ShopController.cs`**: 신규 `Update()` — Shop 열림 중 ESC → `Close()` (인벤토리도 같이 닫힘)
- **`InventoryToggleController.cs`**: 기존 `Update()` 에서 (C 가드 통과 후) Inventory 단독 열림 시 ESC → `Close()`

---

## 핵심 파일

### 수정
- [InventoryToggleController.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/InventoryToggleController.cs) — ShopController 슬롯 + Shop 가드 + ESC 핸들러
- [ShopController.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs) — 신규 Update + ESC 핸들러
- [RunManager.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs) — Debug.Log 1 줄 (305 라인 부근)

### 씬 wiring (Unity Editor — 코드 후 1 회)
- `client/LostMemory/Assets/Scenes/MVP1/MVP1_testkhi.unity` 의 InventoryToggleController GameObject 의 Inspector → `shopController` 슬롯 wire

### 신규
- 없음

---

## 검증 방법

### 정적
- 컴파일 에러 0

### 런타임 (Unity Editor playmode, MVP1_testkhi.unity)

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | Run 시작 → Combat_1 클리어 | Console: `[RunManager] Combat clear reward gold +50.` + `[GoldWallet] Add(+50) -> Current=50` |
| 2 | I 키 (Shop 닫힘 상태) | InventoryPanel 토글 |
| 3 | I 키 → ESC | InventoryPanel 닫힘 |
| 4 | Shop F 키 | ShopPanel + InventoryPanel 동시 활성 (CL-113 회귀 X) |
| 5 | Shop 열림 + **I 키** | **무시** — 두 패널 그대로 (desync 방지) |
| 6 | Shop 열림 + **ESC** | 두 패널 동시 닫힘. Console: `[ShopController] ESC → Close.` |
| 7 | Run 종료 | `[GoldWallet] ResetToInitial -> Current=0` (CL-113 회귀 X) |

### 회귀
CL-113 의 검증 12 항목 중 1~10 (통과) 본 변경 후에도 그대로 통과 — 특히 Shop 열림 / 닫힘 / 구매 / 출구 / 다음 방.

---

## 후속 (본 CL 외)

| 항목 | 담당 | ticket |
|---|---|---|
| 정식 골드 HUD prefab (디자인 / 폰트 / 위치) | 노소연 | 후속 UI ticket |
| 정식 보상 +50 토스트 UI (디자인 / 애니메이션 / 사운드) | 노소연 | 후속 UI ticket |
| 네임스페이스 분리 (E) | 김회인 | (선택) 추후 코드 정리 ticket |
| 정식 골드 시스템 (메타 누적 / 영구 / 마을 환전) | 김회인 | CL-115 Phase 2 또는 별도 |
| `ShopController.SetCombatInputsBlocked` ↔ `RewardController.SetCombatInputsBlocked` 공통 추출 | 김회인 | 별도 리팩토링 |

---

## 위험 / 확인 포인트

1. **`InventoryToggleController.shopController` null 허용** — 단독 인벤토리 테스트 씬 호환. 가드 코드 = `shopController != null && shopController.IsOpen`
2. **`ShopController.Update()` 신규** — 기존 lifecycle (OnEnable / OnDisable / Open / Close) 와 충돌 X. Update 는 IsOpen 가드만 하니 안전
3. **ESC 키 충돌** — exploration 결과 *현재 KeyCode.Escape 사용처 0 개*. 충돌 risk X

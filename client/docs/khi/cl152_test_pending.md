# CL-152 검증 — 보류 (다른 환경에서 진행)

작성일: 2026-05-06
다음 진행 시 본 문서 따라 검증.

---

## 컨텍스트

CL-152 코드 구현 완료. 검증 도중 **CL-146 에서 누락된 버그 발견 + 수정**.

수정된 파일들 (commit 전 검증 필요):
- `Runtime/Relics/PlayerRelicInventory.cs` — `OnTryAddRejected` 이벤트 추가
- `Runtime/UI/ToastNotifier.cs` — 신규 placeholder
- `Runtime/UI/ToastNotifierBridge.cs` — 신규 hook 컴포넌트
- `Editor/Rewards/RewardPoolPopulateMenu.cs` — 신규 Editor 메뉴 3개
- `Runtime/Rewards/RewardPanelView.cs` — **버그 fix** (OnCardSelected 호출 순서)

---

## 검증 1: 보상 후 못 움직임 버그 fix (HIGHEST priority)

### 원인 (해소 완료, 회귀 검증 필요)

`RewardPanelView.OnCardSelected` 의 호출 순서 문제. `RewardSelected.Invoke` 가 `gameObject.SetActive(false)` 보다 먼저 호출되어 `RewardController.HandleRewardSelected` 가 "패널 살아있음" 으로 잘못 판단 → timeScale 복구 X → Player 영구 정지.

CL-146 에서 행운 5스택 다중 픽만 검증, picksAllowed=1 (single pick) 케이스 누락.

### 수정 내용 (RewardPanelView.cs)

```csharp
// before:
RewardSelected?.Invoke(selected);
if (_picksMade >= _picksAllowed) gameObject.SetActive(false);

// after (CL-152 fix):
bool willClose = _picksMade >= _picksAllowed;
if (willClose) gameObject.SetActive(false);
else DisableSelectedCard(selected);
RewardSelected?.Invoke(selected);   // ← 마지막
```

### 검증 시나리오

#### 시나리오 1-A: Single pick (picksAllowed=1) ★ 핵심 ★
1. Play Mode → 행운 0스택 빌드 (clear 빌드)
2. 방 클리어 → 보상 패널 3장
3. 1장 선택
4. **확인**:
   - 패널 즉시 닫힘
   - Console: `Reward picked: ... — 다중 픽 진행 중` 메시지 **안 뜸**
   - 대신: `Reward selected: ... Restoring timeScale + aim` 로그
   - Player 정상 이동 가능
   - Player 공격 가능
   - timeScale 1.0 복구

#### 시나리오 1-B: 다중 픽 (행운 5스택, picksAllowed=2) — 회귀 검증
1. 행운 5스택 빌드 (행운 RelicData 5개)
2. 방 클리어 → 보상 패널 5장 + 2픽
3. **1번째 픽**:
   - 카드 1장 비활성화
   - 패널 유지
   - Console: `Pick 1/2 — ... 선택됨, 추가 선택 대기` + `Reward picked: ... — 다중 픽 진행 중`
   - timeScale 0 유지 (Player 못 움직임 — 의도)
4. **2번째 픽**:
   - 패널 닫힘
   - Console: `Reward selected: ... Restoring timeScale + aim`
   - Player 정상 이동/공격
   - timeScale 1.0 복구

---

## 검증 2: CL-152 본 검증 (RewardPool 등록 + 토스트)

### 시나리오 2-A: RewardPool 일괄 등록
1. Unity Editor 메뉴: `LostMemory > Relics > Populate RewardPool (Dry Run)`
2. Console: `[CL-152] [DRY RUN] 기존 19개 / 추가 예정 N개 / 풀 검색 95개` + 추가 예정 목록
3. 임시 SO (`RelicData_TEST_*`) 있다면 폴더 정리 후 다시 Dry Run
4. `LostMemory > Relics > Populate RewardPool` (Apply)
5. Console: `[CL-152] [APPLIED] ...`
6. `LostMemory > Relics > Audit RewardPool Distribution`
7. Console: 등급별 분포 출력
   ```
   [CL-152] RewardPool 분포 (총 95):
     Legendary: 5
     Unique: 5
     Rare: 19
     Common: 46
     Consumable: ?
   ```

### 시나리오 2-B: 보상 흐름 정상 (95개 풀)
1. Play Mode → 방 클리어
2. 보상 패널 3장 표시
3. 95개 풀에서 다양한 유물 등장 확인 (이전 19개와 다른 유물)
4. 1장 선택 → 인벤토리 추가 정상

### 시나리오 2-C: ToastNotifierBridge wiring + 검증

#### Wiring
1. Hierarchy 의 Stage 또는 UI Canvas 하위에 신규 GameObject `ToastNotifierBridge` 생성
2. `Add Component > Lost Memory > UI > Toast Notifier Bridge`
3. `Inventory` 슬롯에 Player 의 `PlayerRelicInventory` 드래그

#### 인벤토리 가득 참 토스트
1. Play Mode → PlayerRelicInventory 디버그 배열에 5×5 가득 채우는 유물 등록 + Add
2. 보상 패널에서 추가 카드 선택 시도 → TryAdd false
3. Console: `[TOAST] 인벤토리 추가 실패: {name} (공간 부족)`

#### 사이즈 초과 토스트
1. 임시 (6,1) 사이즈 SO + Lock 체크 (덮어쓰기 방지) 만들기
2. Debug Add 시도 → TryAdd false
3. Console: `[TOAST] 인벤토리 추가 실패: {name} (사이즈 초과)`

---

## 검증 3: 회귀 (Phase 3/4 시스템)

### 시나리오 3-A: CL-146 행운 hook
- 행운 1스택 → LuckPoints 가중치 (체감 어려움 — 통계 검증)
- 행운 3스택 → 인벤토리 슬롯 +1 콘솔 로그
- 행운 5스택 → 다중 픽 (검증 1-B 와 동일)
- 행운 7스택 → forceLegendary

### 시나리오 3-B: CL-147 타로
- 타로 1+ 스택 → 평타 누적 → PROC! 카드 발화

### 시나리오 3-C: CL-150 사이즈 (95 SO 사이즈 매핑)
- Editor: `LostMemory > Relics > Apply Rarity-based Sizes (Dry Run)` → 변경 예정 0개 확인 (이미 일치)

### 시나리오 3-D: CL-151 자동 배치 + Sort
- 새 유물 획득 시 그리드 자동 배치 정상
- ContextMenu Sort 동작

---

## Toast 이름 결정 (선택)

현재 명칭 `Toast` 는 모바일/웹 UI 표준 용어. 한국어로 익숙치 않으면 변경 가능.

**옵션**:
- `Toast` — 업계 표준 (Android Toast / 웹 Toast notification)
- `NotificationBanner` — 직관적, 길이 길음
- `AlertMessage` — 단순
- `SnackBar` — Material Design 표준
- `MessagePopup` — 한글 친화

**변경 시 영향**:
- `Runtime/UI/ToastNotifier.cs` → `XxxNotifier.cs` 또는 `XxxMessage.cs`
- `Runtime/UI/ToastNotifierBridge.cs` → `XxxNotifierBridge.cs`
- `[AddComponentMenu("Lost Memory/UI/Toast Notifier Bridge")]` → 새 이름

본 CL placeholder 라 이름 유연성 있음. 정식 UI ticket 시점에 최종 결정해도 OK.

---

## 검증 후 다음 단계

1. **검증 통과 → cl152_implementation.md 작성** (Claude 에 요청)
2. **Git commit 분리 (CL-150 패턴)**:
   - commit 1: 코드 (PlayerRelicInventory.cs + 신규 4 파일 + RewardPanelView.cs 버그 fix)
   - commit 2: RewardPool.asset 변경분 (`CL-152: populate reward pool 95 items`)
3. **사용자 작업**: ToastNotifierBridge GameObject 생성 + wiring (Editor)

---

## 발견된 이슈 (commit 전 정리)

### 발견 1: RewardPanelView.OnCardSelected 호출 순서 버그 (CL-146 누락)
**상태**: 코드 fix 완료, 검증 1-A / 1-B 회귀 통과 시 마무리.

### 발견 2: ShopPanelView.TryBuy 의 TryAdd false 흐름 (잠재적)
**상태**: 미검증. 본 CL 토스트 hook (PlayerRelicInventory.OnTryAddRejected) 으로 자동 토스트 발화는 작동. 단 ShopPanelView 의 골드 차감 / OnItemPurchased 발화 순서 검증 필요. 노소연 코드라 침범 시 별도 ticket.

---

## 참고: 신규 Editor 메뉴 위치

| 메뉴 | path |
|---|---|
| Populate (Dry Run) | LostMemory > Relics > Populate RewardPool (Dry Run) |
| Populate (Apply) | LostMemory > Relics > Populate RewardPool |
| Audit | LostMemory > Relics > Audit RewardPool Distribution |
| (CL-150) Apply Sizes (Dry Run) | LostMemory > Relics > Apply Rarity-based Sizes (Dry Run) |
| (CL-150) Apply Sizes (Apply) | LostMemory > Relics > Apply Rarity-based Sizes |
| (CL-151) Print Grid | LostMemory > Relics > Debug — Print Inventory Grid |
| (CL-141) Validate items.csv | LostMemory > Relics > Validate items.csv (Dry Run) |
| (CL-141) Generate from CSV | LostMemory > Relics > Generate RelicData from CSV |

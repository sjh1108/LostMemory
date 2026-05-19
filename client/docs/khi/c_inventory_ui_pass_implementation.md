# C. 인벤토리·아이템 UI 그룹 — 구현 노트

## Context

플레이어가 인벤토리 슬롯/아이템 정보를 시각적으로 판단하기 어렵다는 피드백을 바탕으로 6개 작업을 한 PR 안에서 커밋 단위로 분리해 진행. 데이터 asset(78개 RelicData) 변경은 최소화하고, 기존 시스템(RelicTag/Rarity/EffectEntry/BuildManager 등)을 재사용.

핵심 발견:
- 등급/태그/효과 enum 및 라벨 매퍼는 이미 갖춰져 있음 → 새 enum 추가 불필요
- `PlayerRelicInventory.OnTryAddRejected` 이벤트가 이미 발화됨 → 풀 슬롯 모달은 이 이벤트만 구독
- `TooltipView`/`BuildManager`/`SetEffectPanelView` 도 이미 존재 → 보강 위주
- 인벤토리 그리드 prefab은 16칸(Slot_0~Slot_15)인데 `PlayerRelicInventory` 직렬화 값이 25칸 → **정합성 깨진 상태**

---

## 변경 사항 (7개 커밋 권장)

### Commit 0 — 인벤토리 용량 25 → 16 정합성 복원
UI prefab(16칸)과 데이터 모델 정합성 회복. 다른 작업의 기반.

- `Relics/PlayerRelicInventory.cs` — `_baseMaxSlots: 25→16`, `_maxCols/_maxRows: 5→4`
- `Prefabs/Characters/TestKhi_Net_AD.prefab` — 직렬화 값 25/5/5 → 16/4/4
- `Prefabs/Test/Test_shm.prefab` — 동일
- `Prefabs/Test/Test_shm_nickname.prefab` — 동일
- `Prefabs/UI/InventoryPanel.prefab` — 이미 16칸이라 변경 없음

### Commit 1 — 슬롯 등급별 테두리
- **신규** `Relics/RelicRarityColors.cs` — 등급-색상 매퍼(`Bright` / `Slot` / `SlotHover` / `Text`). Common(흰)/Rare(파)/Unique(보)/Legendary(주황) + Empty/Consumable
- `Shop/InventorySlotView.cs` — `SetRelic` / `Clear` / `Awake` / `OnPointerEnter` 가 `RelicRarityColors`로 슬롯 배경색 적용. 호버 시 등급색의 1.6배 밝기
- `Shop/TooltipView.cs` — 기존 `GetRarityColor` 삭제하고 `RelicRarityColors.Text` 위임

### Commit 2 — 툴팁 정보 보강
- **신규** `Relics/RelicEffectLabels.cs` — `RelicEffectType` 한국어 라벨 + 수치 포맷터(`Format(EffectEntry)`). %/flat 자동 구분
- `Shop/TooltipView.cs` — 이름/등급/태그(Primary+Secondary)/효과 수치 리스트/세트 진행도 표시. `BuildManager` lazy resolve로 활성 카운트·다음 티어 정보 조회
- 화면 클램프는 기존 로직 유지

### Commit 3 — 인벤토리 풀 시 모달 (교환 포함)
- **신규** `Shop/InventoryFullModal.cs` — `PlayerRelicInventory.OnTryAddRejected("공간 부족")` 구독. UI는 런타임 자동 생성 (별도 prefab 불필요)
  - Step 1: 판매 / 파괴 / 골드 변환 / 교환 / 줍지 않음 — 등급별 골드 변환량 인스펙터 설정
  - Step 2 (교환): 인벤토리 그리드 표시 → 빠질 아이템 선택
  - Step 3: 빠진 아이템에 대해 다시 판매/파괴/골드변환/줍지않음
  - 판매는 `ShopController.IsOpen` 시에만 활성
- `UI/ToastNotifierBridge.cs` — "공간 부족" 사유는 모달이 담당하므로 토스트 스킵

### Commit 4 — 활성 세트 요약 라인
- `Shop/InventoryPanelView.cs` — 옵셔널 `_setSummaryText` 필드 + `BuildManager.OnSetTierChanged` 구독. "활성 세트: [미소녀 T1], [얼음 T0]" 형식. 필드 비워두면 동작 안 함(하위호환)

### Commit 5 — 태그 라벨 풀네임화
- `Relics/RelicTagLabels.cs` — 데이터 asset/enum 변경 없이 라벨 매퍼만 수정
  - 공속 → 공격 속도
  - 쿨감 → 쿨타임 감소
  - 일반뎀 → 공격력
  - 불 → 화염
  - 체력 → 최대 체력
  - 범위 → 공격 범위

### Commit 6 — 툴팁 자동 부트스트랩 + 세트 호버 툴팁
씬에 `TooltipPanel` 인스턴스가 없는 플레이 씬에서 툴팁이 안 뜨는 버그 수정 + 세트 패널 호버 기능 추가.

- `Shop/TooltipView.cs`:
  - `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` 부트스트랩 — 씬 로드 후 `Instance == null` 이면 자동 생성
  - `EnsureInstance()` 정적 메서드 — Canvas(ScreenSpaceOverlay 우선) 자식으로 GameObject 자동 생성
  - `EnsureUI()` — 인스펙터 wiring 없으면 코드로 UI 구조 자동 빌드 (Image bg + VerticalLayoutGroup + ContentSizeFitter + 3개 TMP_Text)
  - `BorrowSceneFont()` — 씬의 다른 TMP_Text 폰트 복사 (한글 표시 보장)
  - `ShowSet(BuildSetData, count, activeTier, screenPos)` — 세트 전용 표시 메서드. 티어별 효과 리스트(✓ 활성 / ▶ 다음 / 회색 미발동)
- `Shop/InventorySlotView.cs` — 호버 시 `TooltipView.EnsureInstance()` 안전망 호출
- `Shop/SetEffectRowView.cs` — `IPointerEnter/Move/Exit` 구현 + Bind 시 set/count/activeTier 캐싱 → 호버 시 `TooltipView.ShowSet` 호출. `_backgroundImage.raycastTarget` Awake 강제 ON
- `Prefabs/UI/TooltipPanel.prefab` — root Image + 3개 TMP_Text 의 `m_RaycastTarget: 1 → 0` (큰 툴팁에서 마우스 깜빡임 방지)

---

## 핵심 헬퍼 (재사용 가능)

| 헬퍼 | 위치 | 용도 |
|---|---|---|
| `RelicRarityColors.Slot/SlotHover/Bright/Text` | `Relics/` | 등급 색상 단일 진실. 슬롯/툴팁/모달 모두 공유 |
| `RelicEffectLabels.Format(EffectEntry)` | `Relics/` | 효과 한 줄 표시 ("공격 속도 +5%"). %/flat 자동 |
| `RelicEffectLabels.LabelFor(RelicEffectType)` | `Relics/` | 라벨만 (BuildSetData 미설명 fallback) |
| `RelicTagLabels.ToKorean(RelicTag)` | `Relics/` | 기존 매퍼 — 라벨만 풀네임으로 갱신 |
| `TooltipView.EnsureInstance()` | `Shop/` | 어디서든 호출 가능한 lazy 부트스트랩 |
| `TooltipView.ShowSet(set, count, tier, pos)` | `Shop/` | 세트 정보 툴팁 (티어 리스트 색상 강조) |

---

## 데이터·Prefab 영향

- **데이터 asset 변경**: 없음 (78개 RelicData 모두 무변경)
- **enum 변경**: 없음 (`RelicTag`/`RelicRarity`/`RelicEffectType` 모두 그대로)
- **Prefab 변경 4개**:
  - 3개 캐릭터 prefab — `_baseMaxSlots`/`_maxCols`/`_maxRows` 16/4/4
  - `TooltipPanel.prefab` — raycastTarget 4개 모두 OFF

---

## Unity Editor 후속 작업 (선택)

코드만으로 모든 기능 동작. 아래는 시각 다듬기용 옵션:

1. **인벤토리 패널 세트 요약 라인**: `InventoryPanel.prefab` 상/하단에 TMP_Text 추가하고 `InventoryPanelView._setSummaryText` 에 드래그. 비워두면 동작 안 함
2. **풀 모달 시각 디자인**: 현재 런타임 자동 생성 (검은 오버레이 + 회색 패널). prefab 기반으로 바꾸려면 `InventoryFullModal` 에 SerializedField 추가 후 prefab 작업
3. **TooltipPanel prefab**: 기존 인스펙터 wiring 유지. 런타임 자동 부트스트랩이 fallback 으로만 동작

---

## 검증 절차

각 커밋 후 단계 확인:

0. **Commit 0**: Play → 인벤토리 열기 → 4×4 16칸 보임. 17번째 아이템 픽업 시도 → `OnTryAddRejected` 발화 확인
1. **Commit 1**: 4가지 등급 아이템 각각 슬롯에 채워 색상 구분. 마우스 호버 시 같은 색의 밝기 1.6배
2. **Commit 2**: 효과 2개 이상 아이템에 호버 → 이름/등급/태그/효과 수치 라인/세트 진행도(예: "미소녀 1/2") 표시
3. **Commit 3**: 인벤토리 16칸 채운 상태에서 새 아이템 픽업 → 모달 표시. 5개 옵션 동작:
   - 판매(상점에서만), 파괴, 골드 변환(+G 토스트), 교환(슬롯 선택 → 2단계 모달), 줍지 않음
4. **Commit 4**: `_setSummaryText` wiring 후 미소녀 1개 보유 → "활성 세트: [미소녀 T0]" 표시
5. **Commit 5**: 모든 태그가 풀네임으로 (인벤토리/툴팁/세트 패널 일괄). git diff로 78개 asset 무변경 확인
6. **Commit 6**: 새 씬에서도 툴팁 뜸. 우측 세트 패널 행에 호버 → 티어별 효과 리스트 표시

---

## Out of Scope

- 새 카테고리 enum 추가 (Knight/Mage 등) — 기존 16개 enum 재사용
- 별도 도감/스포일러 패널 — 세트 정보는 툴팁 + 요약 라인으로 충분
- ItemData asset 78개 일괄 변경 — 라벨 매퍼/UI만 손댐
- 풀 모달의 prefab 기반 시각 디자인 — 런타임 자동 생성으로 우선 동작

---

## 알려진 개선 여지 (후속 ticket 후보)

- 인벤토리 풀 모달 prefab 화 + 모달 시각 디자인 일관화
- 골드 변환 비율을 `RelicEconomyConfig` SO 같은 단일 출처로 통합 (현재 `InventoryFullModal` 인스펙터 필드)
- `TooltipView` 의 자동 부트스트랩 폰트 — `BorrowSceneFont` 가 실패하는 씬에서 한글 깨질 가능성. `TMP_Settings.fallbackFontAssets`에 한글 폰트 등록 권장
- `SetEffectPanelView` 의 행 자체 prefab(`SetEffectRowView`)에 `_backgroundImage.raycastTarget` 인스펙터 ON 확인 (현재 Awake 강제이지만 prefab 단계에서 보장하면 안전)

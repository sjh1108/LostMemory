# CL-148 — 인벤토리 5×5 그리드 + 빌드 진척 표시 UI

## Context

Epic S Phase 4 (UI)의 첫 ticket. 빌드 시스템의 시각화.

**기존 인프라 발견 (재활용)**:
- ✅ `Shop/InventoryPanelView.cs` — 상점용 인벤토리 패널 (슬롯 그리드, 스왑, 버리기)
- ✅ `Shop/InventorySlotView.cs` — 개별 슬롯 (드래그/드롭)
- ✅ `RewardPanelView` 패턴 — Show/Refresh 라이프사이클

**본 CL 책임**:
1. **인게임 HUD 인벤토리** (상시 표시) — 5×5 그리드
2. **빌드 진척 표시** — 16세트 현재 카운트 + 활성 티어
3. **상점 인벤토리(기존)와의 관계 정리** — 재사용 vs 별도

→ B-Lite 1 사이즈 시스템 (CL-150) 과 자동 배치 (CL-151) 는 후속. 본 CL은 **단순 1×1 슬롯 가정**.

---

## 결정사항

### 1. 상점 InventoryPanelView vs HUD 인벤토리

기존 InventoryPanelView는 **상점 전용** (gold 표시 + 드래그 스왑). HUD에는 별도 더 단순한 뷰 필요.

**옵션**:
- (a) 기존 InventoryPanelView를 HUD에서도 재사용 (gold 영역 숨김)
- (b) **별도 HUD 인벤토리 뷰 신설** ⭐
- (c) 둘 통합 (mode flag로 분기)

**채택: (b)**.
- HUD는 항상 표시 (상점은 일시 표시)
- HUD는 읽기 전용 (드래그·버리기 X), 상점은 편집 가능
- 책임 분리 깔끔

**관계**:
- HUD: `InventoryHudView` 신규 (본 CL)
- 상점: `Shop/InventoryPanelView` 기존 유지

### 2. HUD 인벤토리 표시 정책

**옵션**:
- (a) 항상 표시 (전투 중에도)
- (b) Tab 키 토글
- (c) Pause 시 표시
- (d) **항상 표시 + 컴팩트 모드** ⭐

**채택: (d)**.
- 빌드 빠르게 확인 가능 (로그라이크 메타)
- 컴팩트 사이즈 (작은 슬롯)
- 상세는 Tab 키로 확장 (선택)

**위치**: 화면 우측 상단 (또는 좌측 — 디자이너 결정)

### 3. 빌드 진척 표시 형식

**옵션**:
- (a) 텍스트 리스트: "행운: 2/7" "치명타: 4/6"
- (b) 막대 그래프: 각 세트 진행률 바
- (c) 아이콘 + 카운트: 16개 세트 아이콘 + 숫자
- (d) **하이브리드** (활성 세트만 강조 + 모든 세트 작게) ⭐

**채택: (d)**.
- 활성된 세트 (티어 ≥ 1): 큰 아이콘 + 카운트 + 티어 강조
- 비활성 세트: 작은 회색 아이콘
- 한눈에 빌드 정체성 파악

**레이아웃 예시**:
```
┌─────────────────────────┐
│ 인벤토리 [HUD] (5×5)    │
│ ▣▣▣▣▣                 │
│ ▣▣▣  ▣                  │
│ ▣ ▣                      │
│ ▣                        │
│                          │
├─────────────────────────┤
│ 활성 빌드 (3개)          │
│ 🔥 불 4스택 [티어3]      │
│ ⚔️ 치명타 6스택 [티어3]  │
│ 🍀 행운 5스택 [티어3]    │
│                          │
│ 비활성: 체력(2) 공속(1)  │
│        방어(0) 회피(0)   │
└─────────────────────────┘
```

### 4. 슬롯 디자인

**기본**:
- 64×64 픽셀 슬롯 (B-Lite 1 미적용 단순 1×1)
- 슬롯 배경 (회색 박스)
- 아이템 아이콘 (Sprite)
- 등급별 테두리 색 (일반 회색 / 레어 파랑 / 유니크 보라 / 전설 금색)
- 빈 슬롯은 어두운 회색

**HUD 컴팩트 모드**: 32×32 픽셀로 축소.

### 5. BuildManager 이벤트 구독

`InventoryHudView`가 BuildManager의 OnSetTierChanged 구독 → UI 갱신.

```csharp
public class InventoryHudView : MonoBehaviour
{
    [SerializeField] private PlayerRelicInventory inventory;
    [SerializeField] private BuildManager buildManager;
    [SerializeField] private InventorySlotView[] slots;       // 25개
    [SerializeField] private BuildSetIconView[] setIcons;     // 16개

    private void OnEnable()
    {
        inventory.OnRelicAcquired += HandleInventoryChanged;
        inventory.OnRelicRemoved += HandleInventoryChanged;
        buildManager.OnSetTierChanged += HandleTierChanged;
    }

    private void HandleInventoryChanged(RelicData _) => RefreshSlots();
    private void HandleTierChanged(RelicTag tag, int oldTier, int newTier) => RefreshSetIcon(tag);

    private void RefreshSlots() { /* slots 갱신 */ }
    private void RefreshSetIcon(RelicTag tag) { /* 해당 세트 아이콘 갱신 */ }
}
```

### 6. BuildSetIconView (빌드 세트별 아이콘)

```csharp
public class BuildSetIconView : MonoBehaviour
{
    [SerializeField] private RelicTag setTag;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private TextMeshProUGUI tierText;
    [SerializeField] private GameObject activeFrame;

    public void Refresh(int count, int activeTier)
    {
        countText.text = count.ToString();
        tierText.text = activeTier >= 0 ? $"T{activeTier + 1}" : "";
        activeFrame.SetActive(activeTier >= 0);
    }
}
```

**아이콘**: 각 세트의 placeholder 아이콘 (16개)
- 임시: 색깔 동그라미 + 한글 1글자 ("불", "치", "행" 등)
- 정식 아이콘은 후속 ticket

### 7. Tab 키 토글 (선택, 확장 모드)

기본 HUD는 컴팩트. Tab 누르면 확장 모드 (더 큰 슬롯 + 상세 정보):

**MVP**: Tab 토글 X. 컴팩트 HUD만. 후속 ticket에서 확장.

### 8. 상점 인벤토리와 동기화

상점에서 아이템 스왑/버리기 → PlayerRelicInventory 변경 → OnRelicAcquired/Removed 발화 → HUD도 자동 갱신.

→ **자동 동기화** (이벤트 기반). 별도 작업 X.

### 9. MaxSlots 처리 (CL-146 행운 3스택)

CL-146에서 PlayerRelicInventory에 MaxSlots 도입.

InventoryHudView는 `inventory.MaxSlots`만큼 슬롯 활성화.

```csharp
private void RefreshSlots()
{
    int maxSlots = inventory.MaxSlots;
    for (int i = 0; i < slots.Length; i++)
    {
        bool active = i < maxSlots;
        slots[i].gameObject.SetActive(active);
        if (active && i < inventory.OwnedRelics.Count)
            slots[i].SetRelic(inventory.OwnedRelics[i]);
        else
            slots[i].Clear();
    }
}
```

기본 25개 슬롯 prefab. MaxSlots 변경 시 활성/비활성만.

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/UI/InventoryHudView.cs` | HUD 인벤토리 컨트롤러 |
| `Assets/_Project/Scripts/Runtime/UI/BuildSetIconView.cs` | 세트별 진척 아이콘 |
| `Assets/_Project/Prefabs/UI/InventoryHud.prefab` | HUD 인벤토리 prefab |
| `Assets/_Project/Prefabs/UI/InventorySlot_HUD.prefab` | 컴팩트 슬롯 prefab |
| `Assets/_Project/Prefabs/UI/BuildSetIcon.prefab` | 세트 아이콘 prefab |

### 재활용 (수정 X)

| 경로 | 사용 방법 |
|---|---|
| `Shop/InventorySlotView.cs` | HUD 슬롯 prefab에 부착 (또는 단순화한 별도 컴포넌트) |
| `BuildManager.cs` (CL-139) | OnSetTierChanged 구독 |
| `PlayerRelicInventory.cs` (CL-110) | OnRelicAcquired/Removed 구독 |

### 임시 아트

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Art/UI/SetIcons/` | 16개 세트 placeholder 아이콘 |

→ 정식 아이콘은 후속 ticket. 본 CL은 단색 동그라미 + 한글 1글자.

---

## 구현 단계

### 1단계: BuildSetIconView 컴포넌트 (45분)

1. `BuildSetIconView.cs` 작성
2. Prefab 작성 (Image + Text 두 개)
3. 16개 세트별 아이콘 색상 매핑:

```csharp
public static class BuildSetVisuals
{
    public static readonly Dictionary<RelicTag, (Color color, string label)> Map = new()
    {
        { RelicTag.AttackPower,  (Color.red,    "공") },
        { RelicTag.AttackSpeed,  (Color.yellow, "속") },
        { RelicTag.Critical,     (Color.orange, "치") },
        { RelicTag.MaxHealth,    (Color.green,  "체") },
        { RelicTag.Defense,      (Color.gray,   "방") },
        { RelicTag.Dodge,        (Color.cyan,   "피") },
        { RelicTag.Range,        (Color.white,  "범") },
        { RelicTag.Cooldown,     (Color.blue,   "쿨") },
        { RelicTag.Fire,         (Color.red,    "🔥") },
        { RelicTag.Ice,          (Color.cyan,   "❄") },
        { RelicTag.Lightning,    (Color.yellow, "⚡") },
        { RelicTag.Wind,         (Color.green,  "🌪") },
        { RelicTag.MagicalGirl,  (Color.magenta,"♥") },
        { RelicTag.Luck,         (Color.yellow, "🍀") },
        { RelicTag.Greed,        (Color.gold,   "💰") },
        { RelicTag.Tarot,        (Color.purple, "🔮") },
    };
}
```

(이모지 사용 시 폰트 호환 확인 필요. 안 되면 한글 1글자 또는 Unity Sprite 사용.)

### 2단계: InventoryHudView 컴포넌트 (1시간)

1. `InventoryHudView.cs` 작성
2. SerializeField:
   - PlayerRelicInventory inventory
   - BuildManager buildManager
   - InventorySlotView[] slots (25개)
   - BuildSetIconView[] setIcons (16개)
3. OnEnable / OnDisable 이벤트 구독
4. RefreshAll / RefreshSlots / RefreshSetIcon 메서드

### 3단계: InventoryHud.prefab 작성 (1시간)

1. Canvas (Screen Space - Overlay)
2. 우측 상단 패널 (RectTransform)
3. 5×5 GridLayoutGroup (32px 슬롯)
4. 16개 세트 아이콘 영역 (FlowLayout 또는 Vertical)
5. InventoryHudView 컴포넌트 부착

### 4단계: 배치 + Connection (30분)

1. 게임 씬에 InventoryHud prefab 배치
2. Player GameObject의 PlayerRelicInventory와 BuildManager 참조 연결
3. InventorySlot_HUD prefab을 25개 자동 생성 (또는 prefab 안에 미리 25개)
4. BuildSetIcon prefab을 16개 자동 생성

### 5단계: 16개 BuildSetData 색상·라벨 매핑 (15분)

`BuildSetVisuals` 정적 매핑 활용해서 각 BuildSetIconView 초기화.

### 6단계: 검증 (30분)

```
시나리오 1: 빈 인벤토리
- HUD 슬롯 25개 빈 상태 (회색)
- 빌드 진척 16개 세트 0/0 (비활성)

시나리오 2: 아이템 추가
- 분홍 리본 [미소녀+범위] 추가
- 슬롯 1번에 분홍 아이콘 표시
- 미소녀 카운트 1, 범위 카운트 1
- 둘 다 티어 1 활성 (강조)

시나리오 3: 세트 트리거
- 미소녀 5스택 도달
- HUD에 미소녀 활성 강조 (T5)
- (CL-145 합체 발동도 함께 검증)

시나리오 4: 세트 해제
- 아이템 제거 → 카운트 감소 → 티어 변화 시 HUD 갱신

시나리오 5: 행운 슬롯 +1
- 행운 3스택 도달 → MaxSlots 26 → 슬롯 26번 활성화
```

---

## 위험 / 결정 미정

### 위험
1. **이모지 폰트 호환**: TextMeshPro에 이모지 폰트 없으면 깨짐. → 한글 1글자 또는 Sprite 활용.
2. **성능**: 매 BuildManager 이벤트마다 RefreshSetIcon. 한 번에 여러 세트 변화 시 다중 호출. → Dirty flag로 Frame당 한 번만 갱신 (선택).
3. **HUD 위치 가림**: 우측 상단 보스 체력바 등과 겹침? → Canvas SortingOrder 또는 위치 조정.
4. **MaxSlots 변경 시 슬롯 재배치**: GridLayoutGroup 자동. 안 되면 수동 RebuildLayout.
5. **Shop InventoryPanelView와 동기화**: 상점에서 스왑 시 HUD도 갱신? → OnRelicAcquired 이벤트 기반 자동 갱신 OK.

### 결정 미정
- [ ] HUD 위치 (우측 상단 / 좌측 하단 / 중앙 하단) — **우측 상단** 추천
- [ ] 슬롯 크기 (24px / **32px** / 48px) — **32px**
- [ ] 세트 아이콘 (이모지 / 한글 / 단색) — **한글 1글자 + 색** (안전)
- [ ] Tab 토글 확장 모드 — MVP 제외 (별도 ticket)
- [ ] 비활성 세트도 표시? — **YES, 회색 작게** (빌드 가능성 인지)

---

## 후속 ticket 영향

| Ticket | CL-148과의 관계 |
|---|---|
| **CL-149 (툴팁)** | 슬롯 hover 시 아이템 상세 표시. 본 CL의 InventorySlotView에 hover 이벤트 추가 |
| **CL-150 (사이즈 시스템)** | ItemData에 size field 추가. 본 CL의 슬롯 그리드가 다양 사이즈 점유 처리 (CL-151과 함께) |
| **CL-151 (자동 배치)** | 본 CL의 단순 1×1 그리드 → Top-left first fit 알고리즘으로 확장 |
| **CL-152 (보상/상점 통합)** | HUD가 상점/보상 시스템과 동시 작동 검증 |
| **별도 ticket: 정식 세트 아이콘** | placeholder 한글/색 → 정식 디자인 |
| **별도 ticket: HUD 폴리싱** | 위치 조정, 애니메이션, 등급 효과 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (BuildSetIconView) | 45분 |
| 2단계 (InventoryHudView) | 1시간 |
| 3단계 (Prefab 작성) | 1시간 |
| 4단계 (배치 + Connection) | 30분 |
| 5단계 (색상·라벨 매핑) | 15분 |
| 6단계 (검증) | 30분 |
| **합계** | **약 4시간** (티켓 점수 3점에 부합) |

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | HUD 위치 | 우상단 / 좌상단 / 좌하단 / 우하단 | **우상단** |
| 2 | 슬롯 크기 | 24px / 32px / 48px | **32px** |
| 3 | 세트 아이콘 표현 | 이모지 / 한글+색 / 단색 | **한글+색** |
| 4 | Tab 확장 모드 | 본 CL / **별도 ticket** | **별도** |
| 5 | 비활성 세트 표시? | YES / NO | **YES** (회색) |

전부 추천대로면 **우상단 + 32px + 한글+색 + 별도Tab + 비활성표시**.

---

## Phase 4 진행률 (CL-148 후)

| Ticket | Plan |
|---|---|
| **CL-148 인벤토리 + 진척 UI** | ✅ ← 방금 |
| CL-149 아이템 툴팁 | ⏳ |
| CL-150 사이즈 시스템 | ⏳ |
| CL-151 자동 배치 | ⏳ |

**Phase 4: 1/4**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-149 툴팁 | 2점 | 가장 가벼움. 본 CL의 슬롯 hover 활용 |
| B | CL-150 사이즈 시스템 | 2점 | ItemData size field + 등급별 매핑 |
| C | CL-151 자동 배치 | 4점 | Top-left first fit 알고리즘 (Mabinogi-style 핵심) |

추천: **B → C 순서** (사이즈 정의 후 자동 배치 자연스러움). 또는 **A** (툴팁이 직접적 가치).

뭐로 갈까요?

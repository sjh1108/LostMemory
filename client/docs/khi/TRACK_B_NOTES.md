# Track B — UI / 인벤토리 / 보스 콘텐츠 작업 노트

> 작업 범위: UI, 맵, 씬, 인벤토리, 보스 콘텐츠 (Track B 원본 15개 항목)
> 작업 기간: 2026-05-20 세션
> 플랜 파일: `C:\Users\SSAFY\.claude\plans\track-b-replicated-boot.md`

---

## 📊 진행 현황 요약

| 분류 | 개수 | 항목 |
|---|---|---|
| ✅ 검증 완료 | **5** | #1 인벤토리 풀 / #6 다시 보지 않기 / #8 목걸이 / #12 게임오버 / #15 미니맵 |
| 🟢 코드 완료 · 검증 대기 | **3** | #4 미소녀 호버 / #9 전기 미소녀 / 미소녀 학습 보조 (툴팁·필터·자산 swap) |
| 🟡 재진단 필요 | **1** | #7 마을 재능 HP |
| ⛔ 보류 | **4** | #5 기억 조각 / #11 상점 범위 / #13 보스 텔포 / #14 보스방 크기 |
| 사용자 직접 처리 | **1** | #10 몬스터 스폰 거리 |
| 사전 완료 | **2** | #2 인벤토리 툴팁 / #3 아이템 UI 테두리 |
| **총** | **15+α** | |

---

## ✅ 검증 완료 (5개)

### #1 인벤토리 풀 모달 미오픈

| 항목 | 내용 |
|---|---|
| **증상 (Before)** | 인벤토리가 4×4 가득 찬 상태에서 새 유물 픽업 시 "공간 부족" 모달 자체가 안 떴음. 사용자가 유물을 골라 버리거나 교환할 UI가 없어 게임 진행 불가 상태. |
| **원인** | `InventoryFullModal` 컴포넌트가 씬 어디에도 부착되어 있지 않아 `PlayerRelicInventory.OnTryAddRejected` 이벤트가 발화돼도 구독자가 없었음. |
| **수정 파일** | `Scripts/Runtime/Shop/InventoryFullModal.cs` |
| **수정 내용** | ① `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 로 씬 로드 시마다 `InventoryFullModal_Auto` GameObject 자동 생성<br>② `OnEnable` 에서 `inventory == null` 이면 `FindAnyObjectByType<PlayerRelicInventory>()` 로 자동 탐색<br>③ `HandleRejected` 진입 + 구독 시점 디버그 로그 추가 |
| **검증 결과** | 사용자: "잘됨". 콘솔에 `[InventoryFullModal] Subscribed to OnTryAddRejected on 'Player'.` 확인됨. 모달 정상 표시. |
| **사이드 효과** | 마을 씬 Hierarchy에 `InventoryFullModal_Auto` GameObject가 매 진입마다 생성됨 (정상 동작) |

### #6 다시 보지 않기 동작 불가 (Town + Dungeon)

| 항목 | 내용 |
|---|---|
| **증상 (Before)** | 튜토리얼 패널에서 "다시 보지 않기" 체크 후 닫아도 재진입 시 패널이 또 표시됨. |
| **원인** | 인스펙터 `_alwaysShow` 디버그 토글이 true로 켜져 있어 PlayerPrefs 값을 무시하고 항상 표시. 사용자가 이 사실을 모르고 있었음. |
| **수정 파일** | `Scripts/Runtime/Town/TutorialPanelView.cs`<br>`Scripts/Runtime/Town/TownTutorialController.cs`<br>`Scripts/Runtime/Stage/DungeonTutorialController.cs` |
| **수정 내용** | ① TutorialPanelView Awake에서 Toggle 자동 자식 탐색 + 없으면 좌하단 자동 생성<br>② TownTutorialController/DungeonTutorialController의 HandleClose에 `dontShowAgain=True/False key='...'` 진단 로그 추가 |
| **검증 결과** | 사용자: "완료". 콘솔 로그로 토글 인식 정상 확인 (`dontShowAgain=True`) → Always Show 디버그 토글 해제 안내로 해결. |
| **추가 사항** | 코드는 prefab에 Toggle UI 없을 때도 자동 생성하므로 wiring 누락 사고 방지 |

### #8 잔상의 목걸이 표기/실효과 불일치

| 항목 | 내용 |
|---|---|
| **증상 (Before)** | 인벤토리 툴팁 태그에 `[회피] [공속]` 표시되는데 실제 효과는 쿨다운 감소(Cooldown). 사용자가 빌드 결정에 혼란. |
| **원인** | RelicData 자산의 `_tagSecondary`가 1(AttackSpeed/공속)로 잘못 설정. 설명 텍스트도 회피 관련으로 무관함. |
| **수정 파일** | `Assets/_Project/ScriptableObjects/Relics/RelicData_잔상의목걸이.asset` |
| **수정 내용** | `_tagSecondary: 1 (공속) → 6 (쿨감)`<br>`_effectDescription`: "대시 후 짧은 시간 회피 성능 강화" → "대시 사용 시 스킬 쿨다운 감소" |
| **검증 결과** | 사용자: "완료". InventoryTestWindow에 자산이 안 보이는 별도 이슈도 발견되어 같이 해결 (검색 경로 확장). |
| **추가 사항** | InventoryTestWindow 검색 폴더를 `Generated`에서 부모 `Relics`로 확장 → 잔상의목걸이 외 17개 자산도 같이 표시됨 |

### #12 게임오버 오버레이 사이즈

| 항목 | 내용 |
|---|---|
| **증상 (Before)** | 게임오버 결산 패널이 화면을 일부만 덮거나 위치/크기가 어긋남. 다양한 해상도에서 일관성 없음. |
| **원인** | `RunResultPanel.prefab`의 RectTransform이 부모 Canvas 사이즈에 따라 인스턴스화 시점에 변동 가능. anchor 설정 누락 또는 leftover offset. |
| **수정 파일** | `Scripts/Runtime/UI/RunResultPanelView.cs` |
| **수정 내용** | `Show()` 메서드에서 RectTransform을 강제로 화면 전체 stretch 적용:<br>`anchorMin = (0,0)`, `anchorMax = (1,1)`, `offsetMin = offsetMax = (0,0)`, `localScale = (1,1,1)` |
| **검증 결과** | 사용자: "잘됨". 추가로 무지개 보더 이슈 발견 → Image Type=Sliced + Source Image border 미정의 시각화임을 안내 (코드 수정 아닌 인스펙터 설정 변경 권장). |
| **추가 권장** | RunResultPanel의 Image 컴포넌트 — Type을 Simple로 변경하거나 컴포넌트 제거 |

### #15 미니맵 스테이지/라운드 표기

| 항목 | 내용 |
|---|---|
| **증상 (Before)** | 미니맵에 현재 스테이지/방 정보가 표시되지 않아 진행 상황 파악 불가. |
| **원인** | MinimapHUD에 라벨 시스템 자체가 없었음. |
| **수정 파일** | `Scripts/Runtime/UI/Minimap/MinimapHUD.cs`<br>`Scripts/Runtime/Stage/StageRouteManager.cs`<br>`Prefabs/UI/Minimap/MinimapRig.prefab` |
| **수정 내용** | ① MinimapHUD에 `[SerializeField] TMP_Text stageLabel` + `RefreshStageLabel()` 추가<br>② StageRouteManager에 `RouteNodeCount` 프로퍼티 노출<br>③ MinimapRig.prefab에 StageLabel TMP_Text 자식 추가 (fileID `8000000000000000001~004`) + 인스펙터 wiring<br>④ 표시 형태: `Stage 1-1`, `Stage 1-2`, ... (RunManager.CurrentStageNumber + StageRouteManager.CurrentNodeIndex+1) |
| **검증 결과** | 사용자: "잘됨". 방 이동마다 라벨 정상 갱신 확인. |
| **변경 이력** | 초기에 자동 생성 코드 시도 → 사용자가 성능상 prefab 직접 편집 선호 → prefab YAML 편집으로 변경 |

---

## 🟢 코드 완료 · 게임 검증 대기 (3개)

### #4 미소녀 prefab 호버 툴팁 (숨은 능력)

| 항목 | 내용 |
|---|---|
| **목표** | 5종 미소녀 spawn 후 sprite에 마우스 호버 시 visual별 숨은 능력 설명 툴팁 표시. |
| **수정 파일** | 신규: `Scripts/Runtime/MagicalGirl/MagicalGirlHoverTooltip.cs`<br>`Scripts/Runtime/MagicalGirl/MagicalGirlSpawner.cs`<br>`Scripts/Runtime/Shop/TooltipView.cs` |
| **수정 내용** | ① 신규 컴포넌트 `MagicalGirlHoverTooltip` 추가 — OnMouseEnter/Exit에서 TooltipView 호출<br>② `MagicalGirlSpawner.AddGirlByVisual`에서 spawn 시 자동 부착 (`CircleCollider2D` trigger r=0.6 + `MagicalGirlHoverTooltip`)<br>③ `TooltipView.ShowMagicalGirl(visual, screenPos)` 메서드 추가 — 기존 인벤토리 툴팁 재사용 |
| **검증 방법** | Lightning 또는 Fire 유물 획득 → 미소녀 spawn 확인 → sprite에 마우스 호버 → 우상단에 "○○의 미소녀 / 숨은 능력 ..." 표시 |
| **주의 사항** | **메인 카메라에 `Physics2DRaycaster` 컴포넌트 필요**. 없으면 OnMouseEnter 이벤트 미작동. 안 뜨면 카메라 인스펙터에서 추가. |
| **5종 미소녀 표기** | Fire="불의 미소녀" / Ice="얼음의 미소녀" / Star="별의 미소녀" / Blackhole="어둠의 미소녀" / Arrow="빛의 미소녀" |

### #9 전기 미소녀 이미지 누락 보강

| 항목 | 내용 |
|---|---|
| **목표** | Arrow visual (전기/빛) 미소녀가 흰색 placeholder 대신 정상 sprite로 표시되는지 확인 + 누락 시 명확한 원인 추적. |
| **수정 파일** | `Scripts/Runtime/MagicalGirl/MagicalGirlAI.cs` |
| **수정 내용** | `ApplyVisualAppearance()`의 단계별 진단 워닝 추가:<br>① `catalog 미할당` — Spawner의 attackCatalog 필드 wiring 누락<br>② `entry 없음` — catalog asset에 해당 visual entry 없음<br>③ `entry.sprite null` — entry는 있는데 sprite 슬롯 비어있음 |
| **검증 방법** | 전기 안경 유물 획득 → Arrow 미소녀 spawn → sprite 확인. 흰색 placeholder면 콘솔 워닝 확인 |
| **현재 상태** | catalog asset의 visual=5 (Arrow) sprite는 이미 wired됨 (`fileID: 892058060`). sprite 슬라이스 자체가 잘못됐을 가능성 있음 — 인스펙터에서 girl.png Sprite Editor 확인 필요 |

### 🌟 미소녀 학습 보조 (추가 작업 — 직관성 개선)

기존 듀얼 태그 시스템(현재 시스템 유지)에서 학습 비용을 낮추는 3가지 작업.

#### 1. 유물 툴팁에 소환 미소녀 표시
| 항목 | 내용 |
|---|---|
| **수정 파일** | `Scripts/Runtime/Shop/TooltipView.cs` |
| **수정 내용** | `BuildBodyText`에서 `AppendMagicalGirlInfo()` 호출. 듀얼 태그 [미소녀]+[속성] 검사 후 Effects의 Summon/Elemental/Enhanced 보유 시 한 줄 추가:<br>`★ {visual 이름} {소환/강화}` (노란색 #FFD966) |
| **예시 출력** | 분홍 리본 호버 → `★ 불의 미소녀 소환`<br>합체 부적 호버 → `★ 불의 미소녀 강화` |

#### 2. InventoryTestWindow 미소녀 필터 토글
| 항목 | 내용 |
|---|---|
| **수정 파일** | `Scripts/Editor/InventoryTest/InventoryTestWindow.cs`<br>`Scripts/Editor/InventoryTest/Resources/InventoryTestWindow.uxml` |
| **수정 내용** | ① uxml에 ToolbarToggle `MagicalGirlOnlyToggle` 추가<br>② 코드에서 `_magicalGirlOnly` 플래그 + `IsMagicalGirlSpawnRelic()` 헬퍼<br>③ 토글 ON 시 미소녀 소환 유물 14개만 트리에 표시 (소모품 자동 숨김) |
| **사용 방법** | LostMemory → Inventory Test Window → 상단 toolbar `[ ] 미소녀만` 토글 |

#### 3. 자산 일관성 swap (분홍 리본 ↔ 별 모양 단추)
| 항목 | 내용 |
|---|---|
| **수정 파일** | `Assets/_Project/ScriptableObjects/Relics/Generated/RelicData_분홍 리본.asset`<br>`Assets/_Project/ScriptableObjects/Relics/Generated/RelicData_별 모양 단추.asset` |
| **변경 전 (어색)** | 분홍 리본 (Range → Star 미소녀)<br>별 모양 단추 (Fire → Fire 미소녀) ⚠️ 이름과 효과 불일치 |
| **변경 후 (자연스러움)** | 분홍 리본: `_tagSecondary 13 → 7 (Fire)` → **불의 미소녀** 소환<br>별 모양 단추: `_tagSecondary 7 → 13 (Range)` + description "화염 공격" → "별 공격" → **별의 미소녀** elemental 공격 |
| **밸런스 영향** | 효과 타입(Summon/Elemental)/stat 보너스는 그대로 — 어떤 visual이 spawn되는지만 swap. 게임 밸런스 영향 거의 0. |

#### 검증 미완 합계
- 미소녀 호버 동작 (Physics2DRaycaster 확인 포함)
- 전기 미소녀 sprite 표시 (콘솔 워닝 확인)
- 5종 미소녀별 툴팁 표기 (각각 호버해서 텍스트 확인)
- InventoryTest 필터 동작
- swap 후 분홍 리본→불, 별 모양 단추→별 정상 동작

---

## 🟡 재진단 필요 (1개)

### #7 마을 조각 체력 증가 HUD 반영 — **여전히 미반영**

| 항목 | 내용 |
|---|---|
| **증상** | 마을에서 재능 트리에 최대 체력 포인트 투자 후 [저장] 클릭해도 HP 바가 갱신되지 않음. (사용자 직접 확인) |
| **시도 1 — HUD 폴링** | `Scripts/Runtime/UI/PlayerHUDPresenter.cs` Update에 MaxHealth 변화 폴링 추가. 어떤 경로로 변경되든 HP 바 갱신되도록. |
| **시도 2 — HealthApplier 강화** | `Scripts/Runtime/Combat/PlayerHealthStatApplier.cs` — base 값을 현재 mul로 역산 캡처(이중 적용 방지) + health/container 자동 탐색 + OnEnable 직후 즉시 동기화. |
| **시도 3 — 저장 이벤트** | `Scripts/Runtime/Talents/TalentSaveService.cs`에 `static event Action Saved` 추가. `Save()` 직후 발화. |
| **시도 4 — TalentStartupApplier 구독** | TalentSaveService.Saved 구독 → Apply 자동 호출. 공통 source identifier 도입 (중복 누적 방지). |
| **시도 5 — TalentPanelView 직접 적용** | `Scripts/Runtime/Talents/TalentPanelView.cs`의 `OnSave`가 `PlayerStatModifierContainer`를 FindAnyObjectByType으로 찾아 직접 stat 갱신. `RemoveBySource(TalentStartupApplier.Source)` 후 재등록. |
| **현재 상태** | 5단계 모두 적용했으나 사용자 환경에서 HP 바가 여전히 100/100 유지. 사용자가 "넘어가자" 결정. |
| **다음 진단** | 사용자가 마을에서 재능 [저장] 후 콘솔 로그 캡처:<br>① `[TalentSaveService] 저장 완료` — 저장 자체는 동작하는지<br>② `[TalentPanelView] 즉시 적용 — MaxHealth=+N%` — 새로 추가한 코드 호출되는지 ← **이게 안 뜨면 OnSave 핸들러 자체 문제**<br>③ `[PlayerHealthStatApplier] MaxHealth 100 → 110` — container 갱신 받았는지 |
| **추정 원인** | (a) PlayerStatModifierContainer가 마을 씬에 존재하지 않음, (b) PlayerHealthStatApplier가 마을 player에 부착 안 됨, (c) 콘솔 로그가 잘려서 사용자가 못 봤거나 시점이 안 맞음 |

---

## ⛔ 사용자 보류 (4개)

### #5 기억 시스템 마지막 조각 선택 UI

| 항목 | 내용 |
|---|---|
| **목표 (원안)** | MemoryPieceConfirmDialog가 마지막 조각 선택 시 동작 점검 / UI 보강 |
| **보류 이유** | 사용자: "이거 나도 이해 안가 일단 넘어가". 증상이 모호하여 작업 범위 정의 불가. |
| **재개 조건** | 사용자가 구체적인 시나리오 + 기대 동작을 명세할 때 |
| **관련 파일** | `Scripts/Runtime/Memory/UI/MemoryPieceConfirmDialog.cs:68-100` |

### #11 상점 상호작용 범위 / 키 표시 범위

| 항목 | 내용 |
|---|---|
| **목표 (원안)** | 상점 NPC 근접 시 'F키 누르세요' 안내 UI 표시 범위와 실제 F 입력 처리 범위 분리 (안내가 멀리서 뜨고, 실제 작동은 가까울 때만) |
| **보류 이유** | 사용자: "이거는 넘겨". 우선순위 낮음. |
| **재개 시 작업량** | 약 30분<br>① `ShopNpcInteractable`에 outer/inner Collider2D 분리<br>② OnTrigger Enter/Exit에서 outer는 prompt, inner는 F입력 활성화 |
| **관련 파일** | `Scripts/Runtime/Shop/ShopNpcInteractable.cs` |

### #13 보스 처치 후 텔포 위치 어긋남

| 항목 | 내용 |
|---|---|
| **목표 (원안)** | 보스 클리어 포탈 진입 후 도착 위치가 어긋나는 문제 수정 |
| **보류 이유** | 사용자: "이거 넘겨". 정확한 어긋남 위치(보스방 텔포 / 다음 스테이지 / 마을 복귀 중 어디)가 불명확. |
| **재개 시 진단** | 어디서 어긋나는지 사용자 시연 또는 로그 캡처 필요<br>가능한 원인:<br>(a) `BossRoomEntryPoint.arrivalPoints` 배열이 비어있어 self transform fallback<br>(b) `participantIndex` 인덱스 오프<br>(c) 다음 스테이지 spawn point 미설정 |
| **관련 파일** | `Scripts/Runtime/Stage/BossRoomLocalTransitionDriver.cs:248-282`<br>`Scripts/Runtime/Stage/BossRoomEntryPoint.cs:24-53`<br>`Scripts/Runtime/Stage/RunManager.cs:963` (AdvanceToNextStage) |

### #14 보스방 캐릭터 크기 큼

| 항목 | 내용 |
|---|---|
| **목표 (원안)** | 보스방에서 캐릭터가 상대적으로 너무 크게 느껴짐 — 카메라 줌아웃 또는 캐릭터 스케일 다운 |
| **보류 이유** | 사용자: "이거는 나중에 알아봄". 디자인 결정 필요 (카메라 vs 캐릭터 스케일). |
| **재개 시 작업량** | 약 15분<br>① `BossRoomLocalTransitionDriver`의 보스방 진입 시점에 `KhiPlayerCamera.orthographicSize` 일시 증가 (예: 5 → 7)<br>② 보스 처치 후 원복<br>대안: 캐릭터 prefab Transform.localScale 줌인/아웃 |
| **관련 파일** | `Scripts/Runtime/TestKhi/KhiPlayerCamera.cs`<br>`Scripts/Runtime/Stage/BossRoomLocalTransitionDriver.cs` |

---

## 🟢 사용자 직접 처리 (1개)

### #10 몬스터 멀리 스폰 거리
| 항목 | 내용 |
|---|---|
| **상태** | 사용자가 직접 처리 완료 — 코드 변경 없음 |

---

## 📝 사전 완료 (2개 — 본 세션 이전)

- **#2 인벤토리 아이템 툴팁** — TooltipView 기반 (본 세션에서 미소녀 정보 추가로 확장됨)
- **#3 아이템 UI 테두리** — InventorySlot prefab 단계에서 사전 완료

---

## 🔍 검증 체크리스트

게임 한 판 돌리며 확인:

- [ ] **#4 미소녀 호버** — Lightning/Fire 유물 획득 → 미소녀 sprite에 마우스 호버 → 우상단 툴팁 표시
- [ ] **#7 마을 재능 HP** — 마을에서 재능 [저장] → 콘솔 `[TalentPanelView] 즉시 적용 — MaxHealth=+N%` + HP 바 갱신
- [ ] **#9 전기 미소녀** — Arrow 미소녀 spawn → sprite 확인 (흰색 placeholder X)
- [ ] **미소녀 툴팁** — 분홍 리본 호버: `★ 불의 미소녀 소환`. 별 모양 단추 호버: `★ 별의 미소녀 소환`.
- [ ] **InventoryTest 필터** — 미소녀만 토글 ON → 14개 표시

---

## 🛠 추후 작업 추천

### 우선순위 높음
1. **#7 마을 HP 재진단** — 다음 게임에서 콘솔 로그 캡처 (위 진단 가이드 참조)

### 우선순위 중간
2. **보류 항목 재개** — #11(30분) → #14(15분) → #13(진단 필요) 순으로 처리 권장
3. **#5 기억 조각** — 사용자가 시나리오 명세 후 진행

### 우선순위 낮음
4. **자동화** — 마을 씬에 TalentStartupApplier 자동 spawn (#7 근본 해결)
5. **검증 미완 항목 일괄 테스트**

---

## 🔑 진단 로그 키워드 (디버깅 가이드)

문제 발생 시 콘솔에서 다음 prefix 검색:

| Prefix | 시스템 | 확인 포인트 |
|---|---|---|
| `[InventoryFullModal]` | 인벤토리 풀 모달 | 구독 + reason 매칭 |
| `[TownTutorialController]` / `[DungeonTutorialController]` | 튜토리얼 | HandleClose의 dontShowAgain |
| `[TutorialPanelView]` | 튜토리얼 UI | Toggle 자동 생성 여부 |
| `[TalentSaveService]` | 재능 저장 | "저장 완료" 발화 |
| `[TalentPanelView]` | 재능 패널 | "즉시 적용 — MaxHealth=..." |
| `[TalentStartupApplier]` | 재능 적용 | "ApplyTalentStats", "ApplyMemoryBoosts" |
| `[PlayerHealthStatApplier]` | HP 동기화 | "MaxHealth A → B" |
| `[MagicalGirlAI]` | 미소녀 visual | sprite 미적용 단계별 워닝 |
| `[MinimapHUD]` | 미니맵 라벨 | "StageLabel → 'Stage X-Y'" |
| `[MemoryPieceUnlockService]` | 기억 조각 | "StatBoost 기록" |
| `[RunManager]` | 런 흐름 | "Build stage X/Y" |
| `[StageRouteManager]` | 씬 라우팅 | "Loading route node X" |

---

## 📦 변경 파일 인덱스

### Scripts/Runtime (17개 파일)
- `Combat/PlayerHealthStatApplier.cs` — base 역산, fallback resolve, 진단 로그
- `Memory/MemoryPieceUnlockService.cs` — StatBoost 기록 로그
- `MagicalGirl/MagicalGirlAI.cs` — sprite 미적용 진단 로그
- `MagicalGirl/MagicalGirlHoverTooltip.cs` — **신규** (호버 컴포넌트 + 텍스트 매핑)
- `MagicalGirl/MagicalGirlSpawner.cs` — Collider + 호버 컴포넌트 자동 부착
- `Shop/InventoryFullModal.cs` — 자동 spawn + 자동 fallback + 진단 로그
- `Shop/TooltipView.cs` — ShowMagicalGirl, AppendMagicalGirlInfo
- `Stage/DungeonTutorialController.cs` — HandleClose 로그
- `Stage/StageRouteManager.cs` — RouteNodeCount 노출
- `Talents/TalentSaveService.cs` — Saved 이벤트
- `Talents/TalentStartupApplier.cs` — 공통 Source, Saved 구독, 진단 로그
- `Talents/TalentPanelView.cs` — OnSave 즉시 stat 적용
- `Town/TownTutorialController.cs` — HandleClose 로그
- `Town/TutorialPanelView.cs` — Toggle 자동 생성
- `UI/Minimap/MinimapHUD.cs` — StageLabel + Stage X-Y 라벨
- `UI/PlayerHUDPresenter.cs` — MaxHealth 폴링
- `UI/RunResultPanelView.cs` — RectTransform 강제 stretch

### Scripts/Editor (2개 파일)
- `InventoryTest/InventoryTestWindow.cs` — 검색 경로 확장, 미소녀 필터
- `InventoryTest/Resources/InventoryTestWindow.uxml` — 토글 UI

### ScriptableObjects (3개 자산)
- `Relics/RelicData_잔상의목걸이.asset` — 태그/설명 정정
- `Relics/Generated/RelicData_분홍 리본.asset` — secondary 13→7
- `Relics/Generated/RelicData_별 모양 단추.asset` — secondary 7→13, 설명

### Prefabs (1개)
- `UI/Minimap/MinimapRig.prefab` — StageLabel TMP_Text 자식 추가

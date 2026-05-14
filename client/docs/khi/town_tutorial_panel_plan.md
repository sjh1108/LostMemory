# 마을 튜토리얼 패널 플랜

## Context

마을(`Town.unity`)에 처음 들어온 플레이어는 **세 가지 핵심 시스템 — 기억 퍼즐, 재능, 던전 입구** 가 마을 어디에 있는지 모른다. 현재 안내 UI 가 없어 NPC/포탈 위치를 시행착오로 찾아야 한다. 빈 껍데기 [Tutorial.prefab](../../LostMemory/Assets/_Project/Prefabs/UI/Tutorial.prefab) 이 이미 만들어져 있어 그 자리를 쓴다.

**목표**: 첫 마을 진입 시 단계별 페이지 패널이 자동으로 떠서 3가지 시스템을 차례로 설명하고, 페이지마다 월드 마커가 해당 시설(NPC/포탈) 위에 표시되어 위치를 직관적으로 알려줌. "다시 보지 않기" 체크 후 닫으면 PlayerPrefs 에 기록되어 다음부터 안 뜸.

## 결정 요약

| 항목 | 결정 |
| --- | --- |
| 트리거 | 마을 첫 진입 시 자동 표시 (`PlayerPrefs.GetInt("TownTutorialSeen", 0) == 0`) |
| 레이아웃 | 단계별 페이지 (Prev / Next / Close + 페이지 인디케이터 "1 / 3") + "다시 보지 않기" 체크박스 |
| 위치 안내 | 월드 마커 prefab 1개를 페이지 전환마다 좌표 이동시킴 (NPC 머리 위 화살표) |
| 페이지 데이터 | TutorialController 인스펙터 배열 (SO 도입 안 함 — YAGNI) |
| Prefab 재사용 | 기존 `Tutorial.prefab` 의 60x60 빈 RectTransform 을 패널 레이아웃으로 통째 교체 |

## 페이지 데이터 (3페이지)

| # | 제목 | 본문 (요지) | 마커 위치 |
| --- | --- | --- | --- |
| 1 | 기억의 집 | "던전에서 모은 *파편* 으로 *기억 조각* 을 해금. 집 앞 NPC에게 F" | `MemoryHouse_Placeholder` = (-6, 2) → 마커 (-6, 3.5) |
| 2 | 재능 NPC | "포인트로 5종 재능 (치명/공속/방어/이동/체력) 강화. **Save 필수**. F로 대화" | `NpcHouse_Placeholder` = (5, -2) → 마커 (5, -0.5) |
| 3 | 던전 입구 | "던전 포탈에 다가가서 E. 즉시 입장 (확인창 없음)" | `Portal_ToDungeon` = (0, 5) → 마커 (0, 6.5) |

(좌표 출처: [TownSceneBuilder.cs:334](../../LostMemory/Assets/_Project/Scripts/Editor/Stage/TownSceneBuilder.cs:334), `TownTalentSceneInstaller.cs:21`)

## 변경 / 생성 파일

### 신규 스크립트 (2개)

**`LostMemory/Assets/_Project/Scripts/Runtime/Town/TutorialPanelView.cs`**
- 인스펙터 필드: `_titleText`, `_bodyText`, `_pageIndicatorText`, `_prevButton`, `_nextButton`, `_closeButton`, `_dontShowAgainToggle`
- API:
  - `Show(int pageCount)` → 첫 페이지로 초기화, 패널 활성화
  - `BindPage(int index, int total, string title, string body)` → 텍스트 갱신, 인디케이터 갱신, Prev/Next 활성/비활성
  - `event Action OnNextClicked / OnPrevClicked / OnCloseClicked`
  - `bool DontShowAgain => _dontShowAgainToggle.isOn`

**`LostMemory/Assets/_Project/Scripts/Runtime/Town/TownTutorialController.cs`**
- 인스펙터 필드:
  ```csharp
  [Serializable] private struct Page {
      public string Title;
      [TextArea(2,5)] public string Body;
      public Vector3 WorldMarkerPosition;
  }
  [SerializeField] private Page[] _pages;     // 3개 인스펙터에서 직접 입력
  [SerializeField] private TutorialPanelView _panel;
  [SerializeField] private GameObject _worldMarkerPrefab;
  [SerializeField] private string _seenKey = "TownTutorialSeen";
  ```
- `Start()`:
  - `PlayerPrefs.GetInt(_seenKey, 0) != 0` → 패널 즉시 비활성화 후 종료
  - 아니면 `_panel.Show(_pages.Length)` + 마커 Instantiate + 페이지 0 바인딩
- `OnNextClicked` 핸들러: 인덱스++, 마지막 페이지면 다음 누를 때 닫기로 변환 ("다음" → "완료" 라벨 변경은 BindPage 에서)
- `OnCloseClicked`: 마커 Destroy, 패널 비활성화, `DontShowAgain` 이면 PlayerPrefs 저장

### 신규 Prefab (1개) + 기존 prefab 채움

**`LostMemory/Assets/_Project/Prefabs/UI/Tutorial.prefab`** (기존 빈 껍데기 통째 교체)
- 루트 RectTransform: Anchor `Middle Center`, Size 480x320 (현재 60x60 → 변경)
- Image (배경, 노소연 톤 `(0.17, 0.17, 0.17, 1)`)
- 자식:
  - `Title` (TMP, 18pt, Center)
  - `Body` (TMP, 14pt, Left, Word Wrap)
  - `PageIndicator` (TMP, 12pt, Center 우상단, "1 / 3")
  - `PrevButton` / `NextButton` / `CloseButton` (UI Button + TMP 라벨)
  - `DontShowAgainToggle` (UI Toggle + 라벨 "다시 보지 않기")
  - `TutorialPanelView` 컴포넌트 부착, 위 6개 자식 wiring
- 폰트: `24e7867240b94b4458d8a28b7e2e0a93` (노소연 통일)

**`LostMemory/Assets/_Project/Prefabs/World/TutorialWorldMarker.prefab`** (신규)
- 빈 GameObject + SpriteRenderer (화살표 스프라이트, 아래 방향)
- 단순 bobbing 애니메이션 (위아래 0.3 진폭, 1초 주기) — `MonoBehaviour` 인라인 또는 Animator
- Sorting Layer: 다른 NPC 스프라이트보다 위
- (스프라이트는 임시로 `Knob` 같은 Unity 기본 또는 화살표 PNG 한 장 — 디자이너가 나중에 교체)

### 씬 수정 (Town.unity)

1. `TownTopRightHUD` 가 든 Canvas 아래로 `Tutorial.prefab` 인스턴스 배치 (또는 새 Canvas)
2. 빈 GameObject `TownTutorialController` 추가, `TownTutorialController` 컴포넌트 부착:
   - `_panel` ← Tutorial 인스턴스의 `TutorialPanelView`
   - `_worldMarkerPrefab` ← `TutorialWorldMarker.prefab`
   - `_pages` 배열에 위 표 3개 직접 입력 (제목/본문/Vector3)

## 활용 가능한 기존 자산

- `TownTopRightHUD.prefab` 의 ESC 패널 토글 패턴 (`GameObject.SetActive` + 자식 패널 wiring) — 동일 패턴
- 노소연 UI 디자인 토큰 (Panel 배경 `(0.17, 0.17, 0.17, 1)`, 폰트 GUID `24e7867240b94b4458d8a28b7e2e0a93`, 골드 악센트 `(0.96, 0.77, 0.26, 1)`, 본문 14pt, 제목 18pt) — 직전 분석 결과 그대로 적용
- TMP_Text / Image / Button / Toggle Unity 표준 컴포넌트만 사용

## 검증

1. **첫 진입 자동**: `PlayerPrefs.DeleteKey("TownTutorialSeen")` 또는 `PlayerPrefs.DeleteAll()` 실행 → Town 씬 Play → 패널 자동 표시 확인
2. **페이지 진행**: Next 두 번 → 본문/마커 위치가 기억집 → NPC 집 → 던전 포탈 순으로 이동
3. **Prev / 인디케이터**: Prev 동작, "1 / 3 → 2 / 3 → 3 / 3" 갱신, 1페이지면 Prev 비활성, 마지막이면 Next 가 "완료" 로
4. **닫기 + 안 보기**: 체크 안 하고 닫음 → 다시 Play 시 또 뜸. 체크하고 닫음 → 다시 Play 시 안 뜸.
5. **PlayerPrefs 키**: `Edit → Project Settings → ... PlayerPrefs` 또는 디버그 메뉴로 `TownTutorialSeen` 값 확인
6. **마커 위치**: 페이지 1 활성 시 마커가 (-6, 3.5) 즈음 (기억의 집 위) 표시 — Scene 뷰에서 시각 확인

## 범위 외 (다음 작업 후보)

- 게시판(`NoticeBoard_Placeholder`) 으로 튜토리얼 다시 열기 — 사용자가 자동만 선택했으므로 보류
- 페이지별 일러스트/아이콘
- 마커에 NPC 이름 라벨 함께 표시
- "이전에 본 페이지로 다이렉트" 기능
- 다국어
- ScriptableObject 기반 페이지 데이터 (페이지 늘어나면 도입)

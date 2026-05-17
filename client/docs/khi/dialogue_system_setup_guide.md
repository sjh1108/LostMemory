# 대화창(Dialogue) 시스템 - Unity Editor 셋업 가이드

코드(Scripts)는 모두 작성 완료. 이 문서는 Unity Editor 에서 **수동으로** 해야 할 작업 (prefab, SO asset, scene 배치) 단계별 안내.

관련 plan: `~/.claude/plans/zesty-wobbling-origami.md`

---

## 0. 생성된 파일 목록

### Scripts (`Assets/_Project/Scripts/Runtime/Dialogue/`)
- `DialogueLine.cs` — POCO (id, groupId, order, speaker, text, portraitId, isPlayer)
- `DialogueGroup.cs` — 순서 정렬된 라인 묶음
- `DialogueDatabase.cs` — ScriptableObject, CSV 파싱
- `DialogueSkin.cs` — ScriptableObject, 시각 디자인 묶음
- `PortraitCatalog.cs` — ScriptableObject, portraitId → Sprite 매핑
- `DialoguePanelView.cs` — MonoBehaviour, UI 표현 + 타이프라이터
- `DialogueController.cs` — MonoBehaviour, 대화 시작/종료/IBossIntroDialoguePlayer 구현
- `DialogueTrigger.cs` — MonoBehaviour, 범용 트리거(OnTriggerEnter/OnInteract/Manual)

### Data
- `Assets/_Project/Data/Dialogues/dialogues.csv` — 샘플 라인 (boss_intro, shopkeeper_greeting, tutorial_intro)

---

## 1. ScriptableObject Asset 생성 (Editor 메뉴)

Project 창에서 `Assets/_Project/ScriptableObjects/Dialogue/` 폴더 생성 후, 우클릭 메뉴로 다음 asset 생성:

### 1-1. DialogueDatabase asset
- Create → LostMemory → Dialogue → Dialogue Database
- 이름: `DialogueDatabase.asset`
- Inspector → **Csv Asset** 슬롯에 `Assets/_Project/Data/Dialogues/dialogues.csv` 드래그
- (`logOnBuild` 체크 권장 — 초기에 어느 그룹이 로드됐는지 콘솔에서 확인 가능)

### 1-2. PortraitCatalog asset
- Create → LostMemory → Dialogue → Portrait Catalog
- 이름: `PortraitCatalog.asset`
- Inspector → **Entries** 리스트에 (id, sprite) 추가. CSV 에 등장하는 portraitId 와 매칭:
  - `khi_neutral` → Khi 평상시 일러스트
  - `khi_alert` → Khi 경계 일러스트
  - `bertha_smirk` → Bertha 비웃음
  - `bertha_laugh` → Bertha 웃음
  - `shop_smile` → 상점 NPC 미소
- **FallbackSprite**: 매칭 실패 시 표시될 placeholder (회색 박스 권장, 없으면 null)
- ⚠️ 일러스트 PNG 가 아직 없으면 회색 단색 sprite 1개 만들어서 모든 entry 에 임시 할당 → 후에 실제 일러스트 들어오면 교체

### 1-3. DialogueSkin asset (Default)
- Create → LostMemory → Dialogue → Dialogue Skin
- 이름: `DefaultDialogueSkin.asset`
- Inspector 값:
  - `BackgroundSprite`: 패널 배경 (없으면 비워두면 단색만)
  - `BackgroundColor`: (0, 0, 0, 0.85) 검정 반투명 — 기본값으로 OK
  - `SpeakerPortraitAlpha`: 1.0
  - `IdlePortraitAlpha`: **0.4** (사용자 결정)
  - `CharactersPerSecond`: 30
  - `ContinueIndicatorBlinkInterval`: 0.5

(선택) `BossDialogueSkin.asset`, `ShopDialogueSkin.asset` 등 분기 skin 추가 생성 후 DialogueTrigger 의 `SkinOverride` 슬롯에 할당.

---

## 2. DialoguePanel.prefab 생성

위치: `Assets/_Project/Prefabs/UI/DialoguePanel.prefab`

### 2-1. Canvas 준비
- Scene 에 기존 UI Canvas 가 있는지 확인 (`PlayerHUD` 같은 거)
- 없으면 GameObject → UI → Canvas 로 새로 만들기 (Render Mode: Screen Space - Overlay 또는 Camera)
- Canvas 자식으로 다음 구조 생성:

### 2-2. 자식 GameObject 구조

```
DialoguePanel (GameObject)
├── DialogueController (script)        ← DialogueController.cs 부착
├── DialoguePanelView (script)         ← DialoguePanelView.cs 부착 — 같은 GameObject 에 둬도 됨
└── PanelRoot (GameObject)             ← View 의 PanelRoot 슬롯
    ├── Background (Image, RectTransform anchor: bottom-stretch, height: ~250)
    ├── LeftPortrait (Image, anchor: bottom-left, 200x300 정도)
    ├── RightPortrait (Image, anchor: bottom-right, 200x300 정도)
    ├── NameBox (Image, anchor: bottom-left, 위쪽 살짝 띄움)
    │   └── NameText (TMP_Text)
    ├── TextBox (Image, anchor: bottom-stretch, Background 위 중앙)
    │   └── DialogueText (TMP_Text — 한글 폰트 SDF)
    └── ContinueIndicator (Image, anchor: bottom-right, TextBox 끝쪽 작은 ▼)
```

**참고 비율** (1920×1080 기준):
- Background: 화면 폭 전체, 높이 250~300
- LeftPortrait: 좌측 끝에서 안쪽으로 약간(40px), bottom 정렬, 300×450
- RightPortrait: 우측 끝, 같은 크기 (이미지 flip 또는 좌우대칭 원본)
- NameBox: TextBox 위 살짝 좌측 (200×50)
- TextBox: 좌우 Portrait 사이 중앙, 폭 ~1000, 높이 200
- ContinueIndicator: TextBox 우하단 (30×30)

### 2-3. DialoguePanelView Inspector wiring
- `PanelRoot`: 위 PanelRoot GameObject
- `BackgroundImage`: Background
- `NamePlateImage`: NameBox 의 Image
- `TextBoxImage`: TextBox 의 Image
- `ContinueIndicatorImage`: ContinueIndicator
- `LeftPortraitImage`: LeftPortrait
- `RightPortraitImage`: RightPortrait
- `NameText`: NameText (TMP_Text)
- `DialogueText`: DialogueText (TMP_Text)

### 2-4. DialogueController Inspector wiring
- `PanelView`: 같은 GameObject (또는 자식) 의 DialoguePanelView 컴포넌트
- `Database`: `DialogueDatabase.asset`
- `PortraitCatalog`: `PortraitCatalog.asset`
- `DefaultSkin`: `DefaultDialogueSkin.asset`
- `AdvanceKey`: Space (기본)
- `UseMouseLeftClick`: true (기본)

### 2-5. Prefab 저장
- DialoguePanel GameObject 를 `Assets/_Project/Prefabs/UI/DialoguePanel.prefab` 으로 드래그하여 prefab 화

---

## 3. Scene 배치

### 3-1. 게임 Scene 에 prefab 배치
- 사용할 Scene (예: `Game.unity` 또는 첫 보스방 scene) 열기
- DialoguePanel.prefab 을 Canvas 자식으로 드래그
- 시작 시 패널은 자동으로 비활성됨 (DialoguePanelView.Awake → HidePanel)

### 3-2. NPC 또는 트리거 영역에 DialogueTrigger 부착
**예 1: 상점 NPC F 키 대화**
- ShopNpc GameObject 선택 → Add Component → Lost Memory → Dialogue → Dialogue Trigger
- `DialogueGroupId`: `shopkeeper_greeting`
- `TriggerMode`: OnInteract
- `OneShot`: false (매번 발화)
- `PromptObject`: (선택) 기존 "F 누르세요" placeholder 가 있으면 할당
- Collider2D (IsTrigger=true) + 플레이어 tag 가 "Player" 인지 확인

**예 2: 보스방 입장 자동 발화**
- 방법 A — `BossIntroSequenceData` 에 cue 등록:
  - 보스방의 BossIntroSequenceData asset 열기
  - `PlayDialogue`: true
  - `DialogueCueIds`: `["boss_intro"]` 추가
  - BossIntroSequenceController 의 `DialoguePlayerOwner` 슬롯에 DialoguePanel GameObject 할당 (DialogueController 가 IBossIntroDialoguePlayer 구현)
- 방법 B — DialogueTrigger 직접 사용:
  - 보스방 입구에 빈 GameObject + BoxCollider2D(IsTrigger) + DialogueTrigger 부착
  - `DialogueGroupId`: `boss_intro`, `TriggerMode`: OnTriggerEnter, `OneShot`: true

**예 3: 튜토리얼 대화 (Manual)**
- 시작 cutscene 스크립트에서 `DialogueController.Instance.Show("tutorial_intro")` 호출

---

## 4. 검증 체크리스트

1. ☐ Play 모드 진입 시 콘솔에 `[DialogueDatabase] Built — N lines, M groups` 로그 확인
2. ☐ 샵 NPC 에 접근 → "F 누르세요" 표시 → F 키 → 하단 패널 등장
3. ☐ 좌측에 Khi(플레이어) portrait, 우측에 NPC portrait 표시. 화자만 알파 1.0, 반대편 0.4
4. ☐ 텍스트 한 글자씩 차오름 (charactersPerSecond=30)
5. ☐ 타이핑 중 마우스 좌클릭 → 즉시 완성
6. ☐ 완성 후 ▼ 깜빡임 → 다시 클릭/Space → 다음 라인
7. ☐ 마지막 라인 종료 후 클릭 → 패널 사라짐
8. ☐ 보스방 입장 시 `boss_intro_1` ~ `_5` 5라인 차례로 표시
9. ☐ OneShot 트리거: 한 번 발동 후 다시 진입해도 발화 안 됨
10. ☐ `DialogueSkin` SO 교체 (BossDialogueSkin) → 색/sprite 변경 즉시 반영

---

## 5. 사용자가 추가로 줘야 할 자산

### 필수
- **포트레이트 PNG** — `khi_neutral`, `khi_alert`, `bertha_smirk`, `bertha_laugh`, `shop_smile` 등 (권장 512×768, 투명 배경)
- 없으면 회색 placeholder 로 동작은 가능

### 선택 (디자인 완성도)
- 패널 9-slice sprite (Background, NameBox, TextBox 테두리)
- ContinueIndicator ▼ sprite
- BossDialogueSkin 용 어두운 배경 sprite

### 한글 폰트 (TMP_Text)
- 기존 프로젝트에서 쓰는 TMP SDF 폰트 그대로 사용
- 한글 동적 atlas (Dynamic SDF) 또는 사전 빌드된 SDF asset

---

## 6. 다음 단계 (이번 plan 범위 밖)

- 분기 대화 (choices) — CSV 에 `choices`, `next` 컬럼 추가
- 카메라 줌·연출 (cutscene)
- 음성/SFX 컷별 재생
- 대화 스킵 / 자동 진행 / 로그 보기 UI
- 다국어 (CSV 에 ko/en 컬럼)

# 대화창(Dialogue) 시스템 — 현재 상태

메이플 스타일 단일 portrait 하단 대화창. CSV 외부 콘텐츠 + Scene 단일 controller + `IBossIntroDialoguePlayer` 자동 연동.

모든 asset (Scripts/SO/Prefab/Scene) YAML 직접 작성으로 생성. Unity Editor 수동 셋업 단계는 없음.

---

## 1. 파일 인벤토리

### Scripts (`Assets/_Project/Scripts/Runtime/Dialogue/`)
| 파일 | 역할 |
|------|------|
| `DialogueLine.cs` | POCO 데이터 라인 |
| `DialogueGroup.cs` | 순서 정렬된 라인 묶음 |
| `DialogueDatabase.cs` | CSV 파싱 SO. **`OnEnable` 에서 캐시 리셋** (stale state 방지) |
| `PortraitCatalog.cs` | portraitId → Sprite 매핑 SO |
| `DialogueSkin.cs` | sprite 슬롯 SO (색·blink 필드는 dead) |
| `DialoguePanelView.cs` | UI 표현. **단일 portrait**. `ApplySkin` 은 sprite 만 override |
| `DialogueController.cs` | Scene 단일. `IBossIntroDialoguePlayer` 구현 |
| `DialogueTrigger.cs` | 범용 트리거 (`OnTriggerEnter` / `OnInteract` / `Manual`) |
| `DialogueTestStarter.cs` | 테스트 씬용 자동 시작 (Start 후 0.2s, **R 키** 재발화) |

### Data
- `_Project/Data/Dialogues/dialogues.csv` — 샘플 9 라인 (boss_intro 5 / shopkeeper_greeting 2 / tutorial_intro 2)

### ScriptableObject Assets (`_Project/ScriptableObjects/Dialogue/`)
- `DialogueDatabase.asset` — csvAsset wired
- `PortraitCatalog.asset` — entries 비어있음, fallbackSprite wired
- `DefaultDialogueSkin.asset` — **모든 sprite null** (단색 블록 모드)

### Prefab
- `_Project/Prefabs/UI/DialoguePanel.prefab` — 단일 portrait 구조

### Test Scene
- `_Project/Scenes/Test/Dialogue_test.unity` — Canvas + EventSystem + DialoguePanel 인스턴스 + TestStarter

---

## 2. 아키텍처 결정사항

| 항목 | 결정 | 이유 |
|------|------|------|
| Portrait 개수 | **단일** (LeftPortrait) | 사용자 결정. RightPortrait 코드/prefab 모두 제거 |
| Portrait 프레임 | **없음** | freestanding 스타일 |
| 시각 디자인 | **단색 블록** (sprite 없음) | 사용자 결정 (옵션 3) |
| 색상 source of truth | **Prefab Inspector** | edit/Play 색 일치. Skin override 제거 |
| ContinueIndicator | **고정 표시** (깜빡임 X) | 사용자 결정. 코드에서 blink 로직 제거 |
| 그룹화 방식 | **prefix 기반** (`boss_intro_*`) | CSV id 끝 `_숫자` 토큰 |
| Controller 수명 | Scene 단일, **DontDestroyOnLoad X** | Scene 별 별도 |
| Reload 안정성 | `DialogueDatabase.OnEnable` 에서 `_built=false` reset | Unity Domain Reload 후 stale state 영구 고장 방지 |
| Input | 레거시 `Input.GetKeyDown/MouseButtonDown` | Project 의 Active Input Handler = Both |

---

## 3. Prefab 구조

```
DialoguePanel  (DialogueController + DialoguePanelView)
└─ PanelRoot  (Awake 에서 SetActive(false), 대화 시작 시 켜짐)
   ├─ Background       (흰색, 하단 360px stretch band)
   ├─ LeftPortrait     (현재 화자 sprite, 360×500, 좌하단)
   ├─ NameBox          (파랑, 240×70, portrait 아래)
   │   └─ NameText     (흰색, TMP)
   └─ TextBox          (투명, 우측 stretch 영역)
       ├─ DialogueText (흰색, TMP, top-left align, word wrap)
       └─ ContinueIndicator (초록, 100×50, 우하단)
```

색 / 위치 / 크기 모두 Inspector 에서 자유롭게 수정 가능.

---

## 4. CSV 포맷

```csv
id,speaker,text,portraitId,isPlayer
boss_intro_1,Khi,"(기분 탓인가? 저 멀리 보이는 게... 어쩐지 마음에 걸려)",khi_neutral,true
boss_intro_2,Bertha,"드디어 왔구나, 잃어버린 자여.",bertha_smirk,false
```

| 컬럼 | 의미 |
|------|------|
| `id` | `<groupId>_<순서>` 형식. 끝의 `_숫자` 가 순서번호 |
| `speaker` | NameBox 에 표시될 이름 |
| `text` | 본문. 따옴표로 감싸 콤마/줄바꿈 허용 (RFC 4180) |
| `portraitId` | PortraitCatalog 조회 키. 못 찾으면 fallbackSprite |
| `isPlayer` | true/false. **현재 단일 portrait 라 시각 차이 없음** (CSV 호환 유지용) |

순서번호는 1부터 연속 권장. 비연속이면 경고 로그만 (동작은 함).

---

## 5. 외부 통합 API

### 직접 호출
```csharp
// 한 그룹 재생
DialogueController.Instance.Show("boss_intro");

// 완료 콜백
DialogueController.Instance.Show("shopkeeper_greeting", onGroupFinished: () =>
{
    Debug.Log("대화 끝");
});

// 여러 그룹 순차 (보스 인트로 cue 배열 등)
DialogueController.Instance.ShowSequence(new[] { "boss_intro", "boss_threat" }, onAllFinished);

// 강제 종료
DialogueController.Instance.Hide();
```

### 이벤트
```csharp
DialogueController.Instance.OnDialogueEnd += (groupId) => { ... };
```

### Trigger 컴포넌트
- NPC GameObject 또는 트리거 영역에 `DialogueTrigger` 부착
- `triggerMode`: `OnTriggerEnter` / `OnInteract` (F 키) / `Manual`
- `dialogueGroupId`: CSV 의 groupId
- `oneShot`: 한 번만 발화 후 비활성
- `skinOverride`: 다른 Skin SO 로 임시 교체

### 보스 입장 자동 연동
`BossIntroSequenceData.DialogueCueIds` 에 그룹 ID 추가 → `BossIntroSequenceController` 가 `IBossIntroDialoguePlayer.Play()` 호출 → `DialogueController` 가 cue 순차 재생.

별도 wiring 불필요 — `DialogueController` 가 이미 `IBossIntroDialoguePlayer` 구현.

---

## 6. 테스트

### Dialogue_test.unity
1. Scene 열고 Play
2. **0.2초 후 boss_intro 자동 시작**
3. **클릭** 또는 **Space** → 다음 라인 (또는 타이프라이터 즉시 완성)
4. 마지막 라인 후 클릭 → 패널 닫힘
5. **R 키** → 처음부터 재발화

### 다른 그룹 테스트
`DialogueTestStarter` 컴포넌트의 `dialogueGroupId` 슬롯을 `shopkeeper_greeting` 또는 `tutorial_intro` 로 변경.

---

## 7. 커스터마이즈 (Inspector 만)

### 색상
- 패널 배경: `DialoguePanel.prefab` → `Background` → Image color
- 이름판: `NameBox` → Image color
- Next 표시: `ContinueIndicator` → Image color
- 텍스트: `NameText` / `DialogueText` → TMP color

### 위치/크기
각 child 의 RectTransform 조정.

### Sprite 추가 (단색에서 메이플 sprite UI 로 업그레이드)
1. 각 Image 의 Sprite 슬롯에 drag (예: `Assets/2D Pixel Quest Vol.3 - The UI-GUI/...`)
2. 9-slice frame 이면 Image Type = `Sliced` 로 변경
3. Sprite 자체에 9-slice border 가 설정돼 있어야 늘어남 없음 (Sprite Editor → Border)

### Portrait sprite
`PortraitCatalog.asset` → Entries 리스트에 `(id, sprite)` 추가:
- `khi_neutral`, `khi_alert` → 플레이어 일러스트
- `bertha_smirk`, `bertha_laugh` → 보스 일러스트
- `shop_smile` → 상점 NPC

매칭 안 되면 `fallbackSprite` 사용 + warning 로그 1회.

### Skin 변형 (보스용 / 상점용)
1. Project 우클릭 → Create → LostMemory → Dialogue → Dialogue Skin
2. 새 SO 의 sprite 슬롯만 채움 (색은 무시됨)
3. `DialogueTrigger.skinOverride` 에 할당하면 그 트리거 발화 시 자동 적용

---

## 8. 알려진 dead 필드

`DialogueSkin.asset` 의 아래 필드는 **코드에서 더 이상 읽지 않음**:

- `backgroundColor` — Background prefab Image color 사용
- `speakerNameColor` — NameText prefab TMP color 사용
- `dialogueTextColor` — DialogueText prefab TMP color 사용
- `continueIndicatorBlinkInterval` — blink 로직 자체 제거됨

Inspector 에 보이긴 하지만 동작 영향 없음. 추후 cleanup 필요 시 `DialogueSkin.cs` 에서 필드 삭제.

---

## 9. 디버깅 / 문제 해결

### "groupId 'boss_intro' 를 DB 에서 찾을 수 없음"
- `DialogueDatabase.asset` → Inspector 에서 `Csv Asset` 슬롯에 `dialogues.csv` 가 wired 됐는지 확인
- Console 위쪽에 `[DialogueDatabase] csvAsset 가 null` 메시지 있는지
- `[DialogueDatabase] Built — N lines, M groups` 로그의 N/M 가 0이 아닌지
- 그래도 안 되면 `DialogueDatabase.asset` 우클릭 → Reimport

### R 키 안 먹힘
- Game View 에 포커스 줘야 `Input.GetKeyDown` 가 받음. Scene/Hierarchy 창 클릭 시 입력 사라짐

### Edit 색과 Play 색이 다름
- `ApplySkin` 의 색 override 는 이미 제거됨. prefab 색이 그대로 Play 에 반영되어야 함
- 다르면 Skin asset 에 sprite 가 wired 돼있어서 prefab Image 의 sprite 를 덮어쓰는 경우 — Skin 의 sprite 슬롯 비우거나 prefab 의 sprite 직접 교체

### portrait 가 안 보임
- `PortraitCatalog.fallbackSprite` 가 null 이면 화자 image 가 disable (`image.enabled = sprite != null`)
- 최소 fallbackSprite 1개라도 wired 필요

---

## 10. 히스토리 (변경 이력)

| 단계 | 내용 |
|------|------|
| 초기 | 9개 SerializeField, 좌·우 portrait, blink ContinueIndicator |
| 단순화 1 | RightPortrait 코드/prefab 제거 → 단일 portrait |
| 시각 시도 | TravelBook/NoteBook → Pixel Quest UI 팩 → 사용자 결정으로 단색 블록 회귀 |
| 코드 정리 | `ApplySkin` 색 override 제거 (edit/Play 색 일치) |
| 코드 정리 | ContinueIndicator blink 로직 제거 |
| 안정성 | `DialogueDatabase.OnEnable` 에서 캐시 리셋 (stale state 방지) |
| 버그 수정 | Build() 의 `_built=true` 영구고장 버그 (실패 시 false 유지) |

---

## 11. 향후 작업 (이번 구현 범위 밖)

- 분기 대화 (choices) — CSV 에 `choices` + `next` 컬럼
- 카메라 줌·연출 (cutscene)
- 음성/SFX 컷별 재생
- 대화 스킵 / 자동 진행 / 로그 보기 UI
- 다국어 (CSV 에 ko/en 컬럼)
- 보스/상점용 sprite 디자인 입혀서 시각 업그레이드
- `DialogueSkin` 의 dead 필드 cleanup

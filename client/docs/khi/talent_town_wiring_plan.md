# Talent 시스템 Town 연결 + 동작 검증 계획

## Context
프로젝트에 재능(Talent) 시스템이 이미 ~80% 구현되어 있음:
- 데이터(SO), 모델, 계산, UI, 저장(PlayerPrefs), 런 시작 시 stat 적용까지 코드 완성.
- 그러나 **Town 씬에 TalentPanel이 실제로 배치/연결되어 있는지 확인되지 않음**.
- 사용자가 요청한 "Town에서 재능 정하고 1-1, 1-2... 던전에서 적용 + 영구 유지"는 **이론상 이미 동작**해야 함.

이번 작업의 목적은 **새 시스템 구현이 아니라, 기존 시스템을 Town 씬에 wiring 하고 End-to-End 동작 검증**이다. 재능 포인트 수원(Memory Shards 등)은 이번 티켓 범위에서 제외 — `_debugTotalPoints = 10` 유지.

## 핵심 제약
- **노소연 작업 코드 변경 금지** (가정): `Assets/_Project/Scripts/Runtime/Memory/**` 전체 — 읽기만, 수정 X.
- 변경 가능 영역: `Assets/_Project/Scripts/Runtime/Talents/**`, Town 씬, Talent prefab, 인터랙션 컴포넌트.
- 공용 씬 수정(`Town.unity`) 안전 규칙: 자식 GameObject 추가만, 기존 GameObject 직렬화 필드는 건드리지 않음.

## 결정 사항 (사용자 선택)
- **UI 형태**: 기존 `TalentPanel.prefab` 그대로 (라디얼/표/슬라이더 등 디자인은 이미 있음).
- **포인트 수원**: 이번 작업 범위 X. `_debugTotalPoints = 10` 하드코딩 유지.
- **저장 방식**: 기존 PlayerPrefs 그대로. JSON 통일은 별도 티켓.
- **활성화**: Town 씬에서만, NPC 상호작용으로 열기. 우리가 방금 만든 `KhiInteractionPrompt` 활용 (이미 잘 동작 확인됨).

## 아키텍처 (이미 존재, 손볼 곳)

```
Town 씬
  TalentNPC (GameObject)
    ├ Collider2D (Trigger)         ← 새로 추가 (없을 시)
    ├ KhiInteractionPrompt          ← 새로 추가
    │   OnActivate UnityEvent → TalentPanelView.Open()
    └ PromptVisuals (자식)            ← 캐릭터 가까이 가면 "E 재능"
  Canvas
    └ TalentPanel (TalentPanel.prefab 인스턴스)   ← 새로 인스턴스화

이후 흐름 (이미 코드 존재):
  TalentPanelView.Open() ─▶ TalentSaveService.Load() ─▶ TalentModel
  플레이어 분배/저장 ─▶ TalentSaveService.Save() (PlayerPrefs)
  Dungeon 진입 ─▶ DungeonRunBootstrap.BuildRun() ─▶ DungeonBuilt 이벤트
                ─▶ TalentStartupApplier.Apply() ─▶ TalentCalculator → RunStartStats
                ─▶ PlayerStatModifierContainer.AddPermanent(...) ─▶ Stat 영향
```

## 작업 단계

### 1. 사전 확인 (코드 수정 없이 검증)
- `Assets/_Project/Scenes/Town/Town.unity` 열기 → Hierarchy 점검
  - TalentPanel이 이미 배치되어 있는지
  - TalentNpcInteractable 같은 NPC GameObject가 있는지
- 결과에 따라 분기:
  - **이미 다 있고 동작함** → 검증만 (3번 단계로 직행)
  - **prefab만 있고 wiring 안 됨** → 2번 단계로
  - **전혀 없음** → 2번 + 추가 작업

### 2. Town 씬 wiring (Unity 작업, ~30분)
2-1. **TalentPanel 인스턴스화**
- Hierarchy의 메인 Canvas(또는 Overlay Canvas) 아래에 `TalentPanel.prefab` 드래그
- 초기 상태 비활성 (`GameObject.SetActive(false)`)

2-2. **TalentNPC GameObject 생성**
- 마을 적당한 위치에 빈 GameObject 생성 (이름: `TalentNPC`)
- 자식으로 SpriteRenderer (NPC 모양 sprite) 또는 이미 있는 NPC sprite 활용
- 컴포넌트 부착:
  - `BoxCollider2D` (Is Trigger ✓)
  - `KhiInteractionPrompt` (방금 만든 거)
- `KhiInteractionPrompt` 설정:
  - Prompt Visuals: 자식 `PromptVisuals` (이전에 만든 디자인 재사용)
  - Description Text: "재능"
  - Activation Key: E
  - On Activate UnityEvent:
    - 타깃: 위에서 만든 TalentPanel 인스턴스
    - 메서드: `TalentPanelView.Open()` (또는 `GameObject.SetActive(true)`)

2-3. **저장**
- Ctrl+S
- 외부 에셋 미터치 ✓ / 메모리 코드 미터치 ✓

### 3. End-to-End 검증 (PlayMode, ~20분)
**시나리오 1**: Town에서 재능 분배
1. Town 씬 진입
2. 캐릭터로 TalentNPC 접근 → "E 재능" 프롬프트 표시
3. E 키 → TalentPanel 열림
4. 남은 포인트 10개를 5종에 분배 (예: 공속 +5, 방어 +5)
5. 저장 버튼 → Console에 `[TalentSaveService] Saved` 같은 로그 확인
6. 패널 닫기 → Town 자유 이동

**시나리오 2**: 던전 진입 시 stat 적용
1. Town에서 던전 입구로 이동 → 1-1 진입
2. Console에 `[TalentStartupApplier] Apply` 같은 로그 확인
3. PlayerStatModifierContainer 내부 상태 점검 (Inspector 또는 디버그):
   - 분배한 재능에 해당하는 StatId 가 modifier로 등록되었는지
4. 게임플레이 — 분배한 stat이 실제로 작용하는지 (공속 빨라짐 등)

**시나리오 3**: 영구 저장 검증
1. Unity 종료 → 재실행
2. Town 진입 → TalentPanel 다시 열기
3. 이전에 분배한 값이 그대로 복원되는지 확인

**시나리오 4**: 씬 전환에도 적용
1. 던전 1-1 진입 (stat 적용 확인) → 1-2로 이동 → stat 그대로 유지되는지

### 4. 문제 발생 시 진단 가이드
- **패널이 안 열림**: KhiInteractionPrompt 의 OnActivate UnityEvent 연결 확인
- **저장은 됐는데 분배 안 됨**: TalentStartupApplier 가 씬에 있는지, DungeonBuilt 이벤트 구독 OK 인지
- **재실행 후 값 사라짐**: PlayerPrefs 키 충돌 가능 → TalentSaveService 의 키 이름 확인 (`Talent_<TalentType>`)

## 핵심 파일

### 수정 가능 영역 (이번 작업)
- `Assets/_Project/Scenes/Town/Town.unity` (씬 자식 GameObject 추가만)
- `Assets/_Project/Prefabs/UI/TalentPanel.prefab` (필요 시 디자인 미세 조정, 가능하면 미터치)
- `Assets/_Project/Scripts/Runtime/Talents/**` (필요 시. 동작 이상하면 수정)

### 읽기 전용 (노소연 코드 가정)
- `Assets/_Project/Scripts/Runtime/Memory/**` 전체

### 참고 (절대 미수정)
- `Assets/_Project/Scripts/Runtime/Player/PlayerStatModifierContainer.cs`
- `Assets/_Project/Scripts/Runtime/Stage/RunManager.cs`, `DungeonRunBootstrap.cs`

## 위험/주의

| 위험 | 대응 |
|---|---|
| Town 씬은 공용 씬 — 다른 사람 작업 덮어쓸 위험 | 자식 GameObject 추가만 / HP·Map 등 기존 UI 위치 미변경 / 작업 전 git status로 충돌 확인 |
| 노소연 작업 영역 침범 | Memory*.cs 는 읽기만. Memory 관련 데이터 변경 필요하면 사용자에게 보고 후 보류 |
| TalentStartupApplier 가 씬에 부착 안 되어 있을 수도 | 검증 시 DungeonRunBootstrap GameObject 또는 RunManager 에서 해당 컴포넌트 확인. 누락 시 별도 GameObject로 추가 |
| _debugTotalPoints 하드코딩 → 매 런마다 10 으로 초기화? | 코드 확인 필요 — 분배값은 저장되지만 "총 포인트" 자체가 매번 10으로 reset 되면 분배 의미 없음. 검증 단계에서 봐야 함 |

## 후속 (이번 작업 X)
- 재능 포인트 수원 결정 (Memory Piece 보상으로 추정) — 별도 티켓
- PlayerPrefs → JSON 마이그레이션 — 별도 티켓
- TalentPanel 디자인 개선 / Presenter 분리 — 별도 티켓

## 작업량 추정
- 사전 확인: 10분
- Unity wiring: 30분
- 검증 (4개 시나리오): 20분
- **총 약 1시간**

코드 작성은 거의 없음 — 대부분 Unity 에디터 작업 + 검증.

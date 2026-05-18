# cl231 — 작업 우선순위 / 난이도 정리

## Context
사용자가 제시한 7개 작업에 대해, 코드베이스 탐색을 통해 각 작업의 **난이도**(코드 변경 범위, 디버깅 복잡도)와 **우선순위**(사용자 경험에 미치는 영향, 다른 작업과의 의존 관계)를 정리한다. 이 문서는 작업 자체를 진행하기 전에 어떤 순서로 처리할지 결정하기 위한 참고용이다.

---

## 작업별 분석

### 1. 무스 비-사망 상태에서 한 번씩 검어짐
- **난이도: 중**
- **우선순위: 중-높음** (게임 플레이 중 자주 눈에 띄는 버그)
- **원인 추정**
  - `EnemyStatusEffect.cs:232-241` (LateUpdate)에서 Burn tint(`_burnTintColor`)를 매 프레임 SpriteRenderer.color 로 강제 적용
  - Death 콜백(`EnemyDeathAnimationLock.cs:153-176`)에 색상 복원 로직이 없음 → 죽지 않은 상태에서도 Burn 상태 visual 이 살아 있는 동안 강제 어둡게 보일 수 있음
  - `OnHitEffectRegistry` 의 도트 데미지/색상 유지도 의심
- **작업 포인트**
  - `EnemyStatusEffect.LateUpdate()` 의 색상 강제 적용 시점/조건 점검 (Burn 종료 시 원래 색 복원 보장)
  - Hit flash 와 Burn tint 의 충돌 가능성 검증
- **재현 조건 파악이 어렵다는 점이 가장 큰 난관** — 먼저 어떤 상태(피격 직후? Burn 중? 특정 무기 맞을 때?)에서 검어지는지 사용자 관찰 정보가 더 필요

---

### 2. 인벤토리 UI 가 정지 버튼에 붙어 있음
- **난이도: 낮**
- **우선순위: 높음** (UX 직접 저해)
- **현재 구조**
  - `TopRightHUDView.cs:64,118-120` → Pause 버튼 클릭 시 `PausePanelView` 표시
  - `InventoryToggleController.cs:76-86` → `I` 키 입력으로 토글
  - 두 시스템은 본래 독립적이지만, prefab/리스너 어딘가에서 잘못 연결된 것으로 보임
- **작업 포인트**
  - `PausePanel.prefab` 의 Pause 버튼 OnClick 이벤트 → 인벤토리 토글이 잘못 바인딩되어 있는지 확인
  - 또는 인벤토리 패널 부모가 PausePanel 의 자식 GameObject 로 들어가 있는지 씬/prefab 구조 확인
- 코드 자체 수정이 아니라 prefab/씬 인스펙터 정리 작업일 가능성이 크다

---

### 3. 시작 시 씬 위/아래가 보임 (해상도 대응 필요)
- **난이도: 중**
- **우선순위: 높음** (첫인상에 큰 영향, 모든 해상도 대응 필요)
- **현재 구조**
  - `KhiPlayerCamera.cs` 는 deadzone + SmoothDamp 추적만 담당. orthographic size 동적 조정 없음
  - PixelPerfectCamera 미사용 → 16:9 외 비율에서 위아래 여백 발생
- **작업 포인트**
  - 카메라에 종횡비 보정 로직 추가: 화면 비율 대비 디자인 기준 비율(16:9)이면 size 유지, 그보다 세로가 길면 가로 기준으로 size 키움
  - 공식: `targetSize = baseHorizontalHalfWidth / cam.aspect`
  - Awake 와 해상도 변경 감지(Update 에서 Screen.width/height 변화 감지) 양쪽 처리
- 새 컴포넌트 1개로 처리 가능 (예: `CameraAspectFitter`)

---

### 4. 마을 재능 UI 점검
- **난이도: 미정** (사용자가 직접 켜서 확인 후 정리하기로 함)
- **우선순위: 보류**
- **현재 상태**
  - `TalentPanelView`, `TownTalentSceneInstaller`, `TalentNpcInteractable` 모두 구현 완료. ESC 키 닫기, `TalentSaveService` 저장까지 견고함
  - `Assets/_Project/Prefabs/UI/TalentPanel.prefab` 존재
- **다음 단계** — 사용자가 게임 내에서 직접 확인 후 발견된 이슈를 별도 항목으로 분리 예정. 현재 plan 에서는 작업 큐에서 일단 빼고, 확인 결과가 나오면 다시 추가

---

### 5. 인트로 씬 연결 추가
- **난이도: 중**
- **우선순위: 높음** (게임 흐름 자체 변경)
- **확정된 흐름: Title → Phase0_Intro → Town**, **처음 시작 시 기준**
  - "처음 시작 시" 의 정확한 의미(① 게임 첫 실행 1회만 / ② Title 에서 시작할 때마다)는 구현 단계에서 다시 확인. 일반적으로는 ①로 해석하여 PlayerPrefs 또는 세션 플래그로 한 번 본 유저는 스킵
- **현재 구조**
  - `Phase0IntroSequenceController` — 인트로 시퀀스 실행 + `IntroCompleted` 이벤트
  - `TitleSceneController` — 로그인 후 `SceneManager.LoadScene(townSceneName)` 으로 **바로 Town 으로 이동**
  - 인트로 씬(`Phase0_Intro.unity`)이 현재 흐름에서 빠져 있음
- **작업 포인트**
  - `TitleSceneController` 의 로그인 완료 콜백 분기: "처음" 조건이면 `Phase0_Intro` 로, 아니면 `Town` 으로 이동
  - `Phase0_Intro` 씬에 진입 시 `Phase0IntroSequenceController.BeginIntro()` 자동 실행, `IntroCompleted` 이벤트에서 `Town` 으로 전환하는 컨트롤러 추가
  - "이미 본 상태" 저장 위치 결정: PlayerPrefs(로컬) vs 서버 세션 플래그
- **6번(인트로 스킵)과 함께 진행하는 게 효율적**

---

### 6. 인트로 스킵 (P 꾹 누르면 넘어감)
- **난이도: 낮**
- **우선순위: 중** (5번에 자연스럽게 따라옴)
- **작업 포인트**
  - `Phase0IntroSequenceController` 에 hold-to-skip 로직 추가:
    - `Update()` 에서 `Input.GetKey(KeyCode.P)` 가 지속된 시간 누적
    - 임계값(예: 1.0초) 초과 시 코루틴 중단 + `CompleteIntro()` 호출
  - 시각 피드백(원형 게이지 등) 있으면 더 좋지만 MVP 는 키 입력만으로 충분
- 단일 컴포넌트 수정으로 완결

---

### 7. 무기 선택 UI (사용자가 명시적으로 "나중에")
- **난이도: 높**
- **우선순위: 낮음** (사용자 미룸)
- **현재 구조**
  - `WeaponSlotSwitcher` — Q 키 순환만 있고 UI 없음
  - 참고할 패턴: `TalentPanelView`, `InventoryPanelView`
- **작업 포인트**
  - 새 패널 prefab + ViewModel + 선택 시 `WeaponSlotSwitcher` 에 적용하는 어댑터
  - 무기 데이터 SO(`WeaponDataSO`) 와 어떻게 연동할지 디자인 필요
- 다른 작업이 어느 정도 마무리된 뒤 진행 권장

---

## 추천 작업 순서

빠른 승리(low-hanging fruit) → 게임 흐름 정비 → 디버깅성 작업 → 신규 UI 순:

| # | 작업 | 난이도 | 우선순위 | 비고 |
|---|------|--------|----------|------|
| 1 | 인벤토리/정지버튼 분리 (#2) | 낮 | 높음 | prefab/인스펙터 작업, 가장 빠른 정리 |
| 2 | 카메라 해상도 보정 (#3) | 중 | 높음 | 단일 컴포넌트 추가, 첫인상 개선 |
| 3 | 인트로 씬 연결 (#5) + 스킵 (#6) | 중 | 높음 | Title→Intro→Town, 처음 시작 시 기준 |
| 4 | 무스 검어짐 버그 (#1) | 중 | 중-높음 | 재현 조건 파악이 핵심 |
| 5 | 무기 선택 UI (#7) | 높 | 낮음 | 사용자 명시 후순위 |
| — | 마을 재능 UI 점검 (#4) | 미정 | 보류 | 사용자가 직접 본 뒤 별도 항목으로 분리 |

---

## 작업 시작 시 추가 확인이 필요한 항목

1. **#5 "처음 시작 시" 의 정확한 정의** — 게임 첫 실행 1회 한정인지, Title 진입할 때마다인지. 그리고 "이미 봤음" 플래그를 PlayerPrefs(로컬) vs 서버 세션 중 어디에 저장할지
2. **#1 무스 검어짐** — 재현 상황(피격 직후 / Burn 중 / 특정 무기 / 사망 직전 등) 단서 있으면 디버깅 시간 단축
3. **#4 재능 UI** — 사용자가 직접 확인 후 발견된 이슈가 있으면 별도 plan 항목 추가

---

## Verification

작업을 본격 진행하기 전 사용자가 위 우선순위/순서에 동의하면 각 항목별로 별도 plan 파일(cl232+ 등)을 만들어 세부 진행.

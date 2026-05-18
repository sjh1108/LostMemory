# Phase 0 인트로 — SCN-01 슬라이스

`로스트메모리_스크립트_Phase0_인트로_v2.docx`의 Phase 0 인트로 첫 슬라이스(SCN-01) 구현 문서.

---

## 1. 무엇을 만들었나

암전에서 시작해 잔잔한 피아노 BGM이 페이드인되고, 일러스트 8장이 화면에 깜빡이는 동안 한국어 내레이션이 타이핑 효과로 출력되는 **인트로 시퀀스**.

### 시퀀스 흐름 (시간축)

```
T=0.0s   ┃ 화면 완전 암전, UI 없음
         ┃ BGM 페이드인 시작 (0 → 0.15, 2.5초간)
         │
T=2.0s   ┃ ── initialBlackHold 끝 ──
         ┃ Backdrop(일러스트 루프) 페이드인 시작 (alpha 0 → 1, 1초간)
         ┃ Backdrop 8장 sprite swap 루프 시작 (6 fps)
         ┃ 텍스트 타이핑 시작 동시 진행
         │
         ┃ "어느 날 마물이 마을을 침략해왔다."         (타이핑, 18 cps)
T=~3.7s  ┃ ── delayAfter 0.4s ──
         ┃ "나는 마을을 지키기 위해 싸웠다. 싸우고, 또 싸웠다."
T=~6.5s  ┃ ── delayAfter 1.0s ──
         ┃ "죽고, 다시 살아났다."                     (타이핑)
         ┃ "죽고, 다시 살아났다."                     (즉시 출력 — 루프 감각)
T=~9.5s  ┃ ── delayAfter 1.5s ──
         ┃ IntroCompleted 이벤트 발사
```

전체 약 9~10초. 각 값은 ScriptableObject에서 자유롭게 조정 가능.

---

## 2. 아키텍처

`Assets/_Project/Scripts/Runtime/Stage/BossIntroSequenceController.cs`의 패턴을 그대로 미러링했다. **Coroutine + ScriptableObject** 조합.

### 컴포넌트 협력 관계

```
┌───────────────────────────────────────────────────────────────┐
│ Phase0IntroSequenceController  (시퀀스 본체, MonoBehaviour)    │
│                                                                │
│  RunIntroSequence() — IEnumerator                              │
│    ├─ PlayBgmWithFadeIn        ←→  AudioSource (BGM)          │
│    ├─ WaitForSecondsRealtime(initialBlackHold)                │
│    ├─ StartBackdropParallel    ──→ Phase0NarrationBackdrop    │
│    │     ├─ FadeIn(CanvasGroup)                                │
│    │     └─ StartLoop(Sprite[], fps)                           │
│    └─ for each Line                                            │
│          ├─ delayBefore                                        │
│          ├─ typewriter.AppendTyped / AppendInstant             │
│          │     └─ Phase0NarrationTypewriter  ←→ TMP_Text       │
│          │                                    ←→ AudioSource(SFX)│
│          └─ delayAfter                                         │
└───────────────────────────────────────────────────────────────┘
              ↑ 모든 타이밍/텍스트/이미지는 SO에서 읽음
┌───────────────────────────────────────────────────────────────┐
│ Phase0IntroSequenceData (ScriptableObject)                     │
│  Lines / BGM / SFX / Backdrop / Visuals 필드                   │
└───────────────────────────────────────────────────────────────┘
```

### 왜 Coroutine인가 (Timeline 비채택 사유)

1. 같은 패턴이 `BossIntroSequenceController`에 이미 있어서 미러링이 자연스러움
2. 팀 전체가 코루틴 스타일 — UniTask 0건, Timeline `.playable` 0건
3. 이 씬은 텍스트+포즈+볼륨 페이드가 전부라 Timeline의 트랙 편집 강점이 살지 않음
4. 일러스트별 sprite swap도 SO 데이터 + 코루틴 한 줄이면 끝나서 Timeline보다 단순

Timeline 검토 시점은 SCN-05(카메라 블러 + 캐릭터 등장 + 대사창 전환처럼 동시 트랙 多)에서.

---

## 3. 파일 목록

### 런타임 (5개)
```
LostMemory/Assets/_Project/Scripts/Runtime/Intro/Phase0/
├── Phase0IntroLine.cs                    Serializable struct (줄별 데이터)
├── Phase0IntroSequenceData.cs            ScriptableObject (인트로 전체 데이터)
├── Phase0IntroSequenceController.cs      MonoBehaviour (시퀀스 코루틴)
├── Phase0NarrationTypewriter.cs          MonoBehaviour (TMP 타이핑 + 글자 SFX)
├── Phase0NarrationBackdrop.cs            MonoBehaviour (Image swap + CanvasGroup fade)
└── IPhase0SceneVisualPlayer.cs           확장 인터페이스 (SCN-05 컷씬용 자리)
```

### 에디터 (1개)
```
LostMemory/Assets/_Project/Scripts/Editor/Intro/
└── Phase0IntroSetupMenu.cs               메뉴: Lost Memory > Intro > Setup ...
```

### 데이터 (1개 — 메뉴 실행 시 자동 생성)
```
LostMemory/Assets/_Project/Data/Intro/
└── Phase0IntroSequenceData_SCN01.asset
```

### 씬
```
LostMemory/Assets/_Project/Scenes/intro/
└── Phase0_Intro.unity                    (사용자 생성, 메뉴가 객체들 자동 추가)
```

### 사용 자산
```
LostMemory/Assets/_Project/Art/intro/
├── intro1_1.png ~ intro1_8.png           Backdrop 8프레임 일러스트
LostMemory/Assets/_Project/Art/Fonts/
└── malgun SDF.asset                      한국어 TMP 폰트
```

---

## 4. ScriptableObject 필드 가이드

`Phase0IntroSequenceData_SCN01.asset` Inspector. 모든 튜닝의 단일 출처.

### Flow
| 필드 | 의미 | 기본값 |
|---|---|---|
| Initial Black Hold | 첫 타이핑 직전까지 암전 유지 시간(초) | `2.0` |
| Default Chars Per Second | 타이핑 기본 속도. 라인별 값이 0이면 이걸 사용 | `18` |
| Lines | 줄 배열 (아래 표 참고) | 4개 |

### Lines[n]
| 필드 | 의미 |
|---|---|
| Text | 출력 문자열 (한국어 그대로 OK) |
| Delay Before | 이 줄 시작 전 포즈(초) |
| Chars Per Second | 이 줄만의 속도. `0`이면 Default 사용 |
| Instant Reveal | ✓면 타이핑 없이 즉시 전체 출력 |
| Clear Before Line | ✓면 누적 텍스트 지우고 시작 |
| Delay After | 이 줄 끝난 후 포즈(초) |

### BGM
| 필드 | 의미 |
|---|---|
| Bgm Clip | BGM 파일 |
| Bgm Fade In Duration | 페이드인 시간(초) |
| Bgm Target Volume | 최종 볼륨 (0~1, 권장 0.1~0.2) |
| Bgm Start Delay | 시퀀스 시작 후 N초 뒤 페이드인 시작 |

### Typing SFX
| 필드 | 의미 |
|---|---|
| Type Clip | 타이핑 클릭 SFX |
| Type Volume | 0~1 |
| Type Pitch Jitter | 글자마다 피치 ±N (0.05 = ±5%) — 단조로움 방지 |
| Play Every N Characters | 몇 글자에 1번 재생. 시끄러우면 ↑ |

### Backdrop (배경 일러스트 루프)
| 필드 | 의미 |
|---|---|
| Backdrop Frames | Sprite 배열 (intro1_1~8 자동 채움) |
| Backdrop Fps | 초당 프레임 수. 낮을수록 잔잔 |
| Backdrop Fade In Duration | 검정에서 일러스트로 페이드인 시간 |
| Backdrop Fade In Delay | 암전 끝나고 N초 뒤 페이드인 시작 |

### Visuals (확장 자리, 현재 미사용)
| 필드 | 의미 |
|---|---|
| Scene Visual Id | 빈 문자열이면 무시. SCN-05 같은 컷씬에서 활성 예정 |

---

## 5. Editor 셋업 메뉴

씬과 SO와 모든 참조를 한 번에 만들어주는 자동화.

### `Lost Memory > Intro > Setup Phase0 Intro Scene`
- `Phase0IntroSequenceData_SCN01.asset` 신규 생성(없을 때만) + 기본값 채움
- `intro1_1~8.png`를 자동으로 **Sprite로 reimport** (TextureImporter 설정 변경)
- SO의 `backdropFrames`가 비어있으면 8장 자동 할당
- 현재 씬에 다음 GameObject 자동 생성:
  - Main Camera 검정 배경으로 변경
  - `Phase0_Canvas` (Screen Space Overlay, 1920×1080 Scale)
    - `BlackBackground` (Image, 검정)
    - `IntroBackdrop` (Image + CanvasGroup, 일러스트 swap용)
    - `NarrationText` (TextMeshProUGUI, malgun SDF, 중앙 정렬)
  - `Phase0IntroRoot`
    - AudioSource (BGM용)
    - `SfxSource` 자식 + AudioSource (타이핑 SFX용)
    - `Phase0NarrationTypewriter` 컴포넌트
    - `Phase0IntroSequenceController` 컴포넌트
- 모든 Inspector 참조 자동 연결 (reflection)
- 씬 자동 저장

재실행 안전 — `Phase0_Canvas`/`Phase0IntroRoot`는 매번 제거 후 재생성하고, SO는 보존(값 안 덮어씀).

### `Lost Memory > Intro > Refresh Backdrop Sprites`
이미지 파일을 교체했을 때 SO의 `backdropFrames`를 8장으로 강제 갱신. 다른 필드는 건드리지 않음.

---

## 6. 사용자가 자주 만질 곳

| 하고 싶은 것 | 어디서 |
|---|---|
| 대사 문장 변경 | SO > Lines > [n] > Text |
| 전체 타이핑 속도 | SO > Default Chars Per Second |
| 특정 줄만 속도 다르게 | SO > Lines > [n] > Chars Per Second |
| 줄 사이 간격 | SO > 해당 Line의 Delay After (또는 다음 줄 Delay Before) |
| 첫 암전 더 길게 | SO > Initial Black Hold |
| 이미지 깜빡임 속도 | SO > Backdrop Fps |
| 이미지 페이드인 시간 | SO > Backdrop Fade In Duration |
| 이미지가 텍스트보다 늦게 등장 | SO > Backdrop Fade In Delay (예: 0.5s) |
| BGM 볼륨 조절 | SO > Bgm Target Volume |
| 타이핑 SFX 시끄러움 | SO > Type Volume ↓ 또는 Play Every N Characters ↑ |
| 폰트 크기/색 | Hierarchy > NarrationText > TextMeshProUGUI |
| 텍스트 박스 위치/크기 | Hierarchy > NarrationText > RectTransform |
| 이미지 fit 방식 | Hierarchy > IntroBackdrop > Image > Preserve Aspect 토글 |

### 라이브 튜닝 (Play 모드 중)
1. ▶ Play
2. Project에서 SO 선택
3. Inspector 값 드래그/수정 → 즉시 반영
4. **Play 끝나면 값 원복됨** (Unity 표준)
5. 영구 저장 원하면 좋은 값 메모 후 Play 끝나고 다시 입력

---

## 7. 확장 가이드 (SCN-02 이후 작업 시)

### SCN-02 추가 시
1. `Phase0IntroSequenceData_SCN02.asset` 신규 생성 (Create > LostMemory > Intro)
2. SCN-02용 이미지를 `Assets/_Project/Art/intro/scn02/`에 넣고 SO에 할당
3. SCN-02 진입은 다음 중 하나로:
   - `Phase0IntroSequenceController.sequenceData` 교체 후 `BeginIntro()` 호출
   - 별도 컨트롤러로 분리하고 SCN-01의 `IntroCompleted` 이벤트로 트리거

### "SCN별 일러스트가 각각 프레임 애니메이션" 요구 대응
현재 `Phase0NarrationBackdrop`는 sprite 배열 1세트만 처리. SCN마다 다른 배열을 쓰려면:
- 옵션 A (가장 단순): SCN마다 별도 SO + 별도 시퀀스 컨트롤러
- 옵션 B: SO에 `BackdropSegment[]` 추가하여 한 시퀀스 내에서 sprite 세트 교체. 예: `{ startAtLine: 2, sprites: [...], fps: 8 }`
- 옵션 C: `IPhase0SceneVisualPlayer` 인터페이스를 구현한 별도 컴포넌트로 컷씬 단위 비주얼 처리

지금은 옵션 A로 가는 게 가장 가볍다. 시퀀스가 5개라 SO 5개로 충분.

### 화면 셰이크 / 페이드아웃 / 캐릭터 등장 (SCN-03~05)
- 셰이크: MoreMountains MMFeedbacks의 `MMF_CameraShake` 재활용
- 페이드아웃: `Phase0NarrationBackdrop.FadeOut` 이미 구현됨, Controller에서 호출만 추가
- 캐릭터 등장: 별도 컴포넌트(`Phase0CharacterReveal`) 신설 또는 Sprite 추가 슬롯
- 대사창 UI 전환(SCN-05): 별도 다이얼로그 시스템 호출

### 다국어
현재 한국어 SO에 하드코딩. 다국어 필요 시:
- 옵션 1: SO를 언어별로 복제 (`...SCN01_ko.asset`, `...SCN01_en.asset`)
- 옵션 2: Localization Package 도입 후 `Lines[n].textKey`로 변경

---

## 8. 트러블슈팅

| 증상 | 원인 | 해결 |
|---|---|---|
| 한글이 □□□로 깨짐 | malgun SDF가 정적 atlas로 굽혀짐 (한글 글리프 없음) | malgun SDF.asset Inspector에서 Atlas Population Mode를 Dynamic OS로 변경, 또는 Font Asset Creator로 Unicode AC00-D7A3 범위 다시 굽기 |
| Setup 메뉴가 안 보임 | 컴파일 실패 | Console 에러 확인 후 수정 |
| 이미지가 안 뜸 | png가 Texture2D로 import됨 | Setup 메뉴를 다시 실행 (자동 Sprite reimport) 또는 Refresh Backdrop Sprites 메뉴 |
| 이미지가 SO에 안 들어감 | 기존 SO에 이미 backdropFrames 비어있음 / Sprite import 실패 | Refresh Backdrop Sprites 메뉴 강제 실행 |
| 타이핑 SFX 안 들림 | SO Type Clip 미할당 / SfxSource Mute / 마스터 볼륨 0 | SO 필드 + AudioSource 확인 |
| BGM 안 들림 | SO Bgm Clip 미할당 / Bgm Target Volume 0 | SO 필드 확인 |
| Play 중 SO 값 바꿔도 반영 안 됨 | 일부 값은 코루틴 시작 시점에 캐싱됨 (예: BGM duration) | 다음 사이클에서 반영되거나 ▶ 재시작 |
| Setup 다시 누르면 SO 값이 원복될까 걱정 | 기존 SO는 재사용, 값 안 덮어씀 (단 backdropFrames가 비어있으면 채움) | 안심하고 재실행 가능 |

---

## 9. 관련 파일 (참조용)

- 디자인 원본: `로스트메모리_스크립트_Phase0_인트로_v2.docx`
- 패턴 참조: `LostMemory/Assets/_Project/Scripts/Runtime/Stage/BossIntroSequenceController.cs`
- 오디오 시스템 참조: `LostMemory/Assets/_Project/Scripts/Runtime/Stage/Stage1BgmController.cs`
- 플랜 원본: `C:\Users\SSAFY\.claude\plans\c-users-ssafy-downloads-phase0-v2-docx-majestic-dove.md`

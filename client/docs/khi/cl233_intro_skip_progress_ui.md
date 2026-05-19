# cl233 — Phase0 인트로 P 꾹 누름 스킵 진행도 UI

## Context
Phase0_Intro 씬에서 `P` 키 1초 hold 시 인트로가 스킵되는 기능은 이미 `Phase0IntroSequenceController` 에 들어가 있지만, **사용자에게 hold 진행도가 시각적으로 보이지 않음**. 사용자가 키를 얼마나 더 눌러야 하는지, 그 키가 있다는 사실 자체도 모를 수 있음. 우상단에 원형 게이지 + 안내 라벨을 추가해 다음 흐름을 만든다:

1. 인트로 시작 후 잠시(promptDelay) 대기
2. 안내 프롬프트 짧게 등장 ("[P] 꾹 눌러 스킵")
3. 안내 사라지고 평소엔 숨김
4. P 키 누르면 라벨 + 게이지 함께 등장 + 게이지 차오름
5. 1초 hold 완료 → Skip → UI 자동 숨김

---

## 변경 대상

### 1. `Phase0IntroSequenceController.cs` — 진행도 외부 노출
경로: `Assets/_Project/Scripts/Runtime/Intro/Phase0/Phase0IntroSequenceController.cs`

추가 (private 필드는 그대로, 비율만 노출):
```csharp
/// <summary>스킵 키 hold 진행도 (0~1). _isRunning=false 면 0.</summary>
public float SkipProgress => _isRunning
    ? Mathf.Clamp01(_skipHoldTime / Mathf.Max(skipHoldDuration, 0.0001f))
    : 0f;

public KeyCode SkipKey => skipKey;
```

기존 `IntroStarted` / `IntroCompleted` event 가 이미 있어 그대로 사용.

### 2. `Phase0SkipProgressView.cs` — 새 컴포넌트
경로: `Assets/_Project/Scripts/Runtime/Intro/Phase0/Phase0SkipProgressView.cs`

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Intro.Phase0
{
    [DisallowMultipleComponent]
    public sealed class Phase0SkipProgressView : MonoBehaviour
    {
        [SerializeField] private Phase0IntroSequenceController controller;
        [SerializeField] private Image fillImage;            // type=Filled, FillMethod=Radial360
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text label;             // "[P] 꾹 눌러 스킵"

        [Header("Prompt Window (인트로 시작 후 안내 표시)")]
        [SerializeField] private float promptDelay = 3f;     // 시작 후 N초 뒤 등장
        [SerializeField] private float promptDuration = 2f;  // 표시 지속 시간

        [Header("Fade")]
        [SerializeField] private float fadeSpeed = 6f;       // alpha 변화 속도

        private float _introElapsed = -1f;  // -1 = 아직 시작 전
        private bool _bound;

        private void OnEnable()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (fillImage != null) fillImage.fillAmount = 0f;
            BindController();
        }

        private void OnDisable() => UnbindController();

        private void BindController()
        {
            if (controller == null || _bound) return;
            controller.IntroStarted += HandleIntroStarted;
            controller.IntroCompleted += HandleIntroCompleted;
            _bound = true;
        }

        private void UnbindController()
        {
            if (controller == null || !_bound) return;
            controller.IntroStarted -= HandleIntroStarted;
            controller.IntroCompleted -= HandleIntroCompleted;
            _bound = false;
        }

        private void HandleIntroStarted() => _introElapsed = 0f;
        private void HandleIntroCompleted() => _introElapsed = -1f;

        private void Update()
        {
            if (controller == null) return;

            float targetAlpha = 0f;
            float fill = 0f;

            if (_introElapsed >= 0f)
            {
                _introElapsed += Time.unscaledDeltaTime;
                float progress = controller.SkipProgress;

                if (progress > 0f)
                {
                    targetAlpha = 1f;
                    fill = progress;
                }
                else if (_introElapsed >= promptDelay
                      && _introElapsed < promptDelay + promptDuration)
                {
                    targetAlpha = 1f;
                }
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(
                    canvasGroup.alpha, targetAlpha,
                    fadeSpeed * Time.unscaledDeltaTime);
            }
            if (fillImage != null) fillImage.fillAmount = fill;
        }
    }
}
```

`Time.unscaledDeltaTime` 사용 — 인트로가 timeScale 영향 받지 않도록 (다른 인트로 코드도 동일 패턴).

### 3. `Phase0_Intro.unity` — 우상단 UI GameObject 추가
경로: `Assets/_Project/Scenes/intro/Phase0_Intro.unity`

부모: `Phase0_Canvas` (fileID 1895166185) 의 자식으로 추가.

구조 (Editor 작업):
```
SkipProgressIndicator   [RectTransform, CanvasGroup, Phase0SkipProgressView]
  ├─ FillRing           [Image: type=Filled, FillMethod=Radial360, FillOrigin=Top]
  └─ Label              [TMP_Text: "[P] 꾹 눌러 스킵"]
```

RectTransform (`SkipProgressIndicator`):
- AnchorMin/Max: `(1, 1)`
- Pivot: `(1, 1)`
- AnchoredPosition: `(-40, -40)`
- SizeDelta: `(96, 96)` (라벨 포함 영역)

`FillRing` (96×96 원):
- 색: 흰색 alpha 0.9, 배경 링은 동일 위치에 별도 Image(unfilled, alpha 0.25)로 깔아두면 더 보기 좋음 (선택)

`Label`:
- 텍스트 `"[P] 꾹 눌러 스킵"` (또는 영문 `"Hold [P] to Skip"`)
- 위치: 링 아래 또는 옆. 폰트 작게 (12~14pt). 색 흰색 alpha 0.9
- CanvasGroup 한 번에 alpha 조절되므로 별도 처리 불필요

**Phase0SkipProgressView Inspector 필드 연결**:
- `controller` → 씬의 `Phase0IntroRoot` 의 `Phase0IntroSequenceController`
- `fillImage` → `FillRing`
- `canvasGroup` → 자신 GameObject 의 `CanvasGroup`
- `label` → `Label` (선택, 현재 코드는 label 사용 안 함 — 향후 동적 텍스트 시 사용)

> 씬 YAML 직접 편집은 fileID 충돌 위험이라 사용자가 Editor에서 GameObject 생성 + 컴포넌트 연결. prefab 화하면 재사용/유지보수 쉬움.

---

## 동작 흐름 (확인)

| 시점 | 라벨 | 게이지 | alpha |
|------|------|--------|-------|
| 인트로 시작 직후 | 숨김 | - | 0 |
| +3.0초 (promptDelay) | "[P] 꾹 눌러 스킵" | - | 페이드인 → 1 |
| +5.0초 (prompt 종료) | 숨김 | - | 페이드아웃 → 0 |
| P 누르기 시작 | 보임 | 차오름 | 페이드인 → 1 |
| P 떼면 | 숨김 | 0 으로 리셋 | 페이드아웃 → 0 |
| 1초 hold 완료 → Skip | 숨김 (IntroCompleted) | 0 | 페이드아웃 → 0 |

---

## Verification

1. Editor에서 Phase0_Intro 씬 실행
2. **시작 3초 후** 우상단에 "[P] 꾹 눌러 스킵" 라벨 페이드인, 2초 후 페이드아웃 ← 안내 1회
3. 그 외 시간엔 우상단 비어있음
4. P 누르기 시작 → 라벨 + 원형 게이지 페이드인, 게이지 시계방향으로 차오름
5. 중간에 P 떼면 게이지 0 리셋 + 페이드아웃
6. 1초 끝까지 holding → Skip 발동, 씬 Town 전환, UI 자동 숨김
7. 인트로 끝까지 본 케이스 → IntroCompleted 후에도 UI 숨김 상태 유지

---

## 작업 순서

1. `Phase0IntroSequenceController.cs` 에 `SkipProgress` / `SkipKey` getter 추가
2. `Phase0SkipProgressView.cs` 신규 작성
3. Unity Editor에서 Phase0_Intro 씬 열어 우상단 GameObject 트리 추가 + 컴포넌트 연결
4. 빌드 또는 Editor Play로 동작 검증

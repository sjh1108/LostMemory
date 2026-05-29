# 마우스 커서 커스터마이즈 — F5 토글 + Town_Preview 전용 커서

> 작성일: 2026-05-25
> 출처: 시연 중 마우스 커서 제어 두 가지 기능 통합 정리.

## Context

시연 중 두 가지 커서 제어 필요:
1. **F5 한 키 토글** — 시연 중 임의 시점에 마우스 커서 숨김 / 표시 (스크린샷, 깔끔한 시연 화면용)
2. **Town_Preview 전용 커서** — 마을 미리보기 씬에서만 다른 커서 텍스처 (`tool_sword_b.png`) 사용

두 기능을 별도 컴포넌트로 분리 (책임 분리 + scene 단위 적용 차별화).

## 변경 파일 요약

| 파일 | 변경 | 위험 |
|---|---|---|
| `Assets/_Project/Scripts/Runtime/TestKhi/CursorToggleDebug.cs` (신규) | F5 누름 토글 컴포넌트. RuntimeInitializeOnLoadMethod 자동 spawn + DontDestroyOnLoad + Singleton | scene/prefab 0 |
| `Assets/_Project/Scripts/Runtime/TestKhi/TownPreviewCursorSetter.cs` (신규) | scene 진입 시 커서 텍스처 교체 (OnEnable/OnDisable). scene 한 곳에 빈 GameObject + 이 컴포넌트 attach 후 텍스처 drag | scene 1개 변경 (Town_Preview) |
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs` | F5 양보 — `_forceDownDebugEnabled = false` const 가드 추가. 폴링 자체 비활성 (prefab 인스펙터 override 무관) | prefab 0 |

코드 .cs 3 파일 + scene 1개 (Editor 작업).

## 1. CursorToggleDebug (F5 토글)

### 책임
- F5 누름 → `Cursor.visible` 토글 + `Cursor.lockState` 같이 토글 (Editor windowed 모드에서도 보장)
- 게임 전역 작동 — `DontDestroyOnLoad` 로 scene 전환 후에도 살아있음
- Singleton 보장 — 중복 인스턴스 차단 (이전 버그 — 두 인스턴스가 같은 F5 잡아 즉시 토글 복귀)

### 동작
| 액션 | 결과 |
|---|---|
| F5 (첫 누름) | `visible=false` + `lockState=Confined` (게임 창 안 가둠 + 안 보임) |
| F5 (다시) | `visible=true` + `lockState=None` |

### 입력 폴링
- New Input System (`Keyboard.current.f5Key.wasPressedThisFrame`) 우선
- Legacy `Input.GetKeyDown(KeyCode.F5)` fallback
- KhiDownController.IsForceDownPressed 와 동일 패턴

### Singleton 보장 (중복 방지)
```csharp
private static CursorToggleDebug _instance;

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
private static void Bootstrap()
{
    if (_instance != null) return;
    if (FindAnyObjectByType<CursorToggleDebug>(FindObjectsInactive.Include) != null) return;
    // ... new GameObject + DontDestroyOnLoad
}

private void Awake()
{
    if (_instance != null && _instance != this)
    {
        Destroy(gameObject);
        return;
    }
    _instance = this;
}
```

## 2. TownPreviewCursorSetter (scene 진입 시 커서 교체)

### 책임
- scene 의 빈 GameObject 에 attach
- OnEnable → `Cursor.SetCursor(texture, hotspot, mode)`
- OnDisable → `Cursor.SetCursor(null, Vector2.zero, Auto)` (default 복귀)
- OnValidate → Inspector 에서 hotspot/texture 바꾸면 Play 중 즉시 재적용

### SerializeField
- `cursorTexture` (Texture2D) — Town_Preview 에선 `tool_sword_b.png`
- `hotspot` (Vector2) — 실제 클릭 포인트 픽셀 좌표
- `cursorMode` (Auto / ForceSoftware) — default Auto (hardware cursor)
- `logOnApply` (bool) — 진단 로그

### 사용 가이드 (Editor 작업)
1. 적용할 scene 열기 (예: `Town_Preview.unity`)
2. Hierarchy 우클릭 → Create Empty (이름: `[TownPreviewCursorSetter]`)
3. Add Component → `Town Preview Cursor Setter`
4. `cursorTexture` 슬롯에 텍스처 drag (예: `Assets/_Project/Art/mouse/tool_sword_b.png`)
5. `hotspot` 조정 (검 끝이 클릭 포인트면 검 끝 픽셀 좌표)
6. Scene 저장 (Ctrl+S)

### 텍스처 import 권장 설정
| 항목 | 권장 값 | 이유 |
|---|---|---|
| Texture Type | Cursor (또는 Default) | Sprite 도 작동하지만 sub-optimal |
| Read/Write Enabled | ON | `CursorMode.ForceSoftware` 사용 시 필수 |
| Compression | None | 선명도 |
| Max Size | 32 또는 64 | OS 한계 (Windows 최대 32~64) |

## 3. KhiDownController F5 양보

기존 F5 기능 (자기 강제 Down) 을 CursorToggleDebug 에 양보.

```csharp
// KhiDownController.cs
private const bool _forceDownDebugEnabled = false;

private void Update()
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    if (_forceDownDebugEnabled && IsForceDownPressed())
    {
        // ... 자기 강제 Down (현재 비활성)
    }
#endif
    // ...
}
```

**가드 방식**:
- SerializeField `debugForceDownKey` 그대로 둠 (default `KeyCode.F5`)
- `const bool _forceDownDebugEnabled = false` 로 폴링 자체 차단
- 재활성화 필요 시 `true` 로 변경 + `debugForceDownKey` 다른 키로 (F11 등)

**prefab override 무관**: 인스펙터에서 F5 로 설정돼 있어도 const 가드가 우선.

## 두 컴포넌트 상호작용

| 상황 | 결과 |
|---|---|
| Town_Preview 진입 | 커서 텍스처 = `tool_sword_b` (TownPreviewCursorSetter) |
| F5 누름 (visible=false) | 텍스처는 그대로지만 안 보임 (Cursor.visible 별개) |
| F5 다시 (visible=true) | 텍스처 그대로 보임 (SetCursor 유지) |
| Town_Preview 빠짐 | 커서 default 복귀 (TownPreviewCursorSetter.OnDisable) |
| F5 누름 (default 상태) | OS default 커서 visible 토글 |

## 검증 시나리오

### A. CursorToggleDebug F5 토글
1. Editor Play
2. Game 창 위로 마우스 이동
3. F5 누름 → 콘솔: `[CursorToggleDebug] F5 토글 — Cursor.visible=False lockState=Confined`
4. 커서 사라짐 ✓
5. F5 다시 → `visible=True lockState=None` + 커서 복귀

### B. Town_Preview 커서 텍스처 적용
1. Town_Preview scene 진입
2. 콘솔: `[TownPreviewCursorSetter] 커서 적용 — texture='tool_sword_b' hotspot=(X, Y) mode=Auto`
3. 화면에서 마우스 = `tool_sword_b` 텍스처 ✓
4. 다른 scene 으로 이동 → 콘솔: `[TownPreviewCursorSetter] 커서 default 복귀`

### C. Singleton 검증 (이전 버그 회귀 방지)
1. Editor Play
2. F5 **한 번** 누름
3. 콘솔에 토글 로그 **1줄만** (이전엔 2줄 → 즉시 visible=True 로 복귀 버그)
4. 만약 `[CursorToggleDebug] 중복 인스턴스 감지 — destroy` warning 떠도 OK (Awake 가드 작동)

### D. KhiDownController F5 양보 회귀
1. Editor Play → 호스트 캐릭터 활성 상태
2. F5 누름 → 커서 토글만 발화, **호스트 캐릭터 안 죽음** ✓ (이전엔 자기 강제 Down 했음)

## NGO / 안정성 평가

| 항목 | 위험 |
|---|---|
| prefab 변경 | 0 |
| scene 변경 | 1개 (Town_Preview 에 GameObject 1개 추가) |
| NGO join risk | 0 (Town_Preview 는 NGO 없는 미리보기 씬) |
| 다른 시스템 영향 | 0 (F5 단독 사용 확인, KhiDownController 만 썼고 비활성) |
| 솔로/멀티 둘 다 작동 | ✓ (NGO 무관) |

## 시연 후 follow-up (선택)

1. **F5 외 다른 키 후보 정리** — F5~F12 모두 점령 상태. 함수키 외 (Shift+M 등) 조합 키로 분산 검토.
2. **TownPreviewCursorSetter 의 scene 적용 자동화** — 다른 scene 에도 커서 교체 필요하면 ScriptableObject + sceneLoaded 패턴으로 자동 적용.
3. **Cursor.SetCursor Hardware vs Software 비교** — Hardware (Auto) 가 빠르지만 일부 효과 제한. 시연 후 성능/시각 비교.
4. **F5 양보된 KhiDownController.debugForceDownKey 의 향후 키 재할당** — 재활성화 필요 시 F1~F4 중 비점령 키 사용.

## 이상한 점 (능동 지적)

- **Hotspot 의 중요성**: 검 텍스처면 사용자가 "검 끝" 으로 클릭하려는 의도일 가능성. hotspot 안 맞추면 손가락 ↔ 화면 클릭 위치 어긋남. 시연 직전 인게임 클릭 정밀도 확인 권장.
- **OS Cursor 크기 한계**: Windows 는 보통 32x32 또는 일부 64x64 까지. tool_sword_b.png 가 그보다 크면 잘리거나 흐려짐.
- **다른 scene 의 잔재 카메라/AudioListener**: 같은 패턴이 다른 dungeon scene 에도 있을 수 있음 — 별도 audit (`grep -c "^AudioListener:" *.unity`) 권장.

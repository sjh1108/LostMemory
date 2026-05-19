# cl234 — Track A 구현 (전투 / 입력 / 스탯 / HUD)

## Context

게임 베타 피드백 32개 중 **Track A — 플레이어·전투·스탯·상단 HUD** 영역의 12개 항목을 일괄 처리한 작업 기록. plan 은 `cl234_track_a_combat_input_stats_plan.md` 참고. Track B (UI/맵/씬/인벤토리/보스) 와 파일 영역 분리되어 병렬 처리 가능했음.

작업 진행 중 발견한 인접 문제 (인벤토리 UI raycast, 사운드 자동 적용, 머지 충돌, 다른 패널 입력 차단) 도 함께 처리.

---

## Track A 항목별 처리 결과

### A-1. 보상창 떴을 때 공격 클릭이 카드 클릭으로 새어 들어감 ✅

**파일**: `Scripts/Runtime/TestKhi/KhiMeleeComboController.cs:479`

`WasAttackPressedThisFrame()` 진입부에 `EventSystem.IsPointerOverGameObject()` + `UIInputBlocker.IsBlocked` 이중 가드 추가. `RewardPanelView` 의 기존 1초 selection delay 는 그대로 유지 (UI 자체 보호용).

### A-2. UI 창 열렸을 때 클릭하면 공격됨 ✅

A-1과 동일한 가드로 동시 해결.

#### A-2-α. 우클릭/Space 등 마우스가 UI 위에 없을 때도 키보드 입력 차단 ✅

**신규 컴포넌트 2개**:
- `Scripts/Runtime/UI/UIInputBlocker.cs` — 정적 카운터 (`Acquire`/`Release`/`IsBlocked`/`Reset`)
- `Scripts/Runtime/UI/UIInputBlockerSource.cs` — MonoBehaviour, OnEnable/OnDisable 자동 hook

**입력 핸들러 가드 추가** (4개):
- `KhiMeleeComboController.WasAttackPressedThisFrame()` — 좌클릭/게임패드 X
- `KhiParryController.WasParryPressedThisFrame()` — 우클릭/parry action
- `KhiDashController.HandleInput()` — Space 대시 (키보드라 EventSystem 으로는 차단 안 됨)
- `KhiDaggerTeleportController.Update()` — 우클릭 단검 텔레포트
- `KhiBowController.Update()` — 활 좌/우클릭

**5개 UI 패널 prefab 에 `UIInputBlockerSource` 컴포넌트 부착**:
- `InventoryPanel.prefab` (fileID 1234567900003)
- `PausePanel.prefab` (fileID 1234568000001)
- `RewardPanel.prefab` (fileID 1234568000002)
- `ShopPanel.prefab` (fileID 1234568000003)
- `TalentPanel.prefab` (fileID 1234568000004)

**백업**: `InventoryToggleController.Open/Close` 에 `UIInputBlocker.Acquire/Release` 직접 호출 추가 — prefab YAML 변경이 Unity 에 미반영된 환경 안전망.

### A-3. 공격 도중 대쉬/방어 캔슬 불가 ✅

코드 인프라 (`KhiDashController.allowDashDuringAttack`, `KhiParryController.allowParryDuringAttack`) 이미 존재. 사용자가 캐릭터 prefab Inspector 에서 두 토글 ON 완료.

### A-4. 기본 공격 + 활 좌클릭 홀드 자동 공격 ✅

**파일**: `KhiMeleeComboController.cs:479`, `KhiBowController.cs:84`

두 컨트롤러 모두 `mouse.leftButton.wasPressedThisFrame` → `isPressed` 로 변경. 기존 cooldown (`_comboExpiresAt` / `_nextSingleShotAllowedAt`) 으로 발사 빈도 자체 제한 → 자동 연사. 사용자가 누르고 있는 동안 공속에 맞춰 연속 공격.

### A-5. 아이템 "최대체력 +10%" 잘못 적용 ✅

A-8 합성 공식 변경으로 자동 해결됨 (아래 A-8 참조).

### A-6 / A-7. 재능 방어 + 방어템 데미지 0 케이스 ✅

**파일**: `Scripts/Runtime/Combat/PlayerDamageReceiver.cs:81-110`

기존: `refund = Mathf.Min(defense, damageReceived)` — 방어력 ≥ 데미지면 모두 환불, 데미지 0 발생.

신규: **로그 기반 다이미니싱 공식** (팀원 김민재 제안):

```
피해감소율(%) = 102.5 × Log10(1 + 0.025 × 방어력)
최종 피해     = round(max(raw × (1 - rate), 1))
```

C# 구현 (`ComputeDefenseFinalDamage()` static helper):

```csharp
float rate = Mathf.Clamp(102.5f * Mathf.Log10(1f + 0.025f * defense) / 100f, 0f, 0.99f);
float reduced = rawDamage * (1f - rate);
return Mathf.Max(Mathf.RoundToInt(reduced), 1);
```

| 방어력 | 감소율 | 10 데미지 → 받는 피해 |
|--------|--------|---------------------|
| 0 | 0% | 10 |
| 20 | 18.04% | 8 |
| 100 | 55.77% | 4 |
| 400 | ~100% (clamp 99%) | 1 |

최소 1 데미지 floor 로 무피해 케이스 완전 제거. 매 피격마다 상세 로그 출력 (`[DamageReceiver] HIT — raw=X | def=Y | rate=Z% | final=N`).

### A-8. 재능 체력 1100 폭주 fix ✅

**원인 진단**:
- `TalentData_MaxHealth.IncreasePerPoint = 1` → 10포인트 = +10 stats.MaxHealth
- `TalentStartupApplier:134` 가 `AddPermanent(StatId.MaxHealth, 10, this)` 호출
- `PlayerStatModifierContainer.GetTotalMultiplier(MaxHealth)` = `1 + 10 = 11` (multiplier 처리)
- `PlayerHealthStatApplier` 가 `_baseMaxHealth (100) × 11 = 1100` 폭주

**Fix — flat 트랙 분리**:

1. `StatId.cs` 에 `MaxHealthFlat` 열거값 추가
2. `TalentStartupApplier:134` — `StatId.MaxHealth` → `StatId.MaxHealthFlat`
3. `PlayerHealthStatApplier.HandleChanged()` 합성 공식 변경:

```csharp
float flatBonus = container.GetTotalFlat(StatId.MaxHealthFlat);
float mul       = container.GetTotalMultiplier(StatId.MaxHealth);
float newMax    = (_baseMaxHealth + flatBonus) * mul;
```

유물의 % multiplier (`StatId.MaxHealth`) 와 재능의 flat 가산 (`StatId.MaxHealthFlat`) 이 독립 트랙. 100 + 10 (재능 flat) + 10% (유물) = 121 정상 결과.

A-5 (최대체력 +10% 잘못 적용) 도 이 합성 공식으로 자연스럽게 해결 — 이전엔 재능 flat 이 multiplier 합산에 끼어들어 모든 % 효과가 폭주.

### A-9. 재능 UI 최댓값 표기 ✅

코드는 이미 정상 (`TalentRowView.Refresh()` 에서 `$"{invested}/{_maxLevel}"` 표시 + `TalentPanelView.Open()` 에서 자동 호출). 사용자가 prefab 의 `_valueText` 바인딩 점검 완료.

### A-10. 피격 화면 선혈 vignette ⏸️ **패스 (사용자 결정)**

코드는 완성되어 있음:
- `Scripts/Runtime/UI/HitVignetteOverlay.cs` — 신규 컴포넌트 (Pulse fade in/hold/fade out)
- `KhiCombatFeedbackBinder.HandleHitStun()` — `hitVignette?.Pulse()` 호출 추가

추후 활성화하려면:
1. HUD Canvas 자식으로 풀스크린 Image 추가 (빨간 가장자리 그라데이션 스프라이트)
2. `HitVignetteOverlay` 컴포넌트 부착
3. 캐릭터 prefab 의 `KhiCombatFeedbackBinder._hitVignette` 필드에 드래그 연결

### A-11. 타격 소리 옵션 ✅ (사용자 자체 처리)

본 plan 작업 범위 밖.

### A-12. 던전 중 플레이 시간 HUD ✅

**RunManager 게터 추가** (`Scripts/Runtime/Stage/RunManager.cs:108`):

```csharp
public float RunStartedAt   => runStartedAt;
public float ElapsedRunTime => InRun ? Mathf.Max(0f, Time.time - runStartedAt) : 0f;
```

**신규 컴포넌트**: `Scripts/Runtime/UI/RunTimerHudPresenter.cs`
- Update() 폴링 → MM:SS 포맷팅 → TMP_Text 갱신
- `_keepActiveWhenIdle` / `_showCentiseconds` 옵션
- InRun 아닐 때 빈 문자열 (마을·타이틀)

**미니맵 prefab 부착** (`MinimapRig.prefab`):
- `smallMap` 자식 `BottomInfo` 컨테이너 (anchor: bottom stretch)
- `BottomInfo/TimerBackground` (검은 반투명 박스, RGBA 0,0,0,0.6, 90×26)
- `BottomInfo/RunTimer` (TMP_Text "00:00", 18pt 흰색, Center) + `RunTimerHudPresenter`

B-15 (미니맵 스테이지/라운드 표기) 와 형제 노드로 공존 — 같은 `BottomInfo` 부모 공유.

---

## 인접 문제 — 함께 처리

### 인벤토리 UI raycast 실패 ✅

**원인**: `InventoryPanel.prefab` 에 자체 Canvas / GraphicRaycaster 없음 → `EventSystem.IsPointerOverGameObject()` hit-test 실패.

**Fix**: prefab 루트에 추가:
- Canvas (Override Sorting ON, sortingOrder=100)
- GraphicRaycaster

### 사운드 자동 적용 — ESC 눌러야 적용되던 문제 ✅

**진단 (코드 분석 결과)**:
1. `SettingsPanelView` 가 자체 PlayerPrefs 키 (`Settings_BGMVolume`/`Settings_SFXVolume`) 사용
2. PlayerPrefs → MM 적용은 `SettingsPanelView.Awake()` 한 곳뿐 — 옵션 메뉴 GameObject 활성화 시에만
3. 게임 시작 시엔 옵션 메뉴 비활성 → 적용 안 됨
4. 추가로 Unity AudioMixer.SetFloat 의 알려진 타이밍 버그 — 부팅 직후 SetFloat 호출이 무시되는 경우 있음

**Fix — 3중 안전망**:

#### (1) `MMSoundManagerSettings.asset` 변경
- `AutoSave: 0 → 1` — 옵션 슬라이더 변경 시 MM 가 자체 PlayerPrefs 에도 자동 저장
- AutoLoad=1 유지 (Start 1회 호출. MM 의 합리적 설계 유지)

#### (2) 신규 `SettingsAutoApplier.cs`
- `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 자동 부트스트랩
- DontDestroyOnLoad 싱글톤
- `ApplyWhenMMReady()` 코루틴 — MM 준비 (300프레임 wait) 후 PlayerPrefs → SetTrackVolume
- `PollAndReapply()` 코루틴 — 매 **0.5초** 강제 재적용. Unity AudioMixer 타이밍 버그 우회 + 늦게 활성화된 AudioSource 가 mixer 값 가져갈 때 우리 값 보장
- 같은 값이면 사실상 no-op (CPU 부담 거의 0)
- `SceneManager.sceneLoaded` 구독 → 매 씬 전환마다 재시도

#### (3) `SettingsPanelView.cs` 보강
- `OnEnable()` 추가 — 매 옵션 패널 활성화 시 PlayerPrefs 값 재적용
- `ReapplyWhenMMReady()` 코루틴 — Awake 시점 MM 미준비 시 5초 wait + 재시도
- `OnBGMChanged`/`OnSFXChanged` 에 `PlayerPrefs.Save()` 즉시 호출 — 갑작스러운 종료에도 저장 보장

**왜 단순히 AutoLoad 끄지 않았나** (사용자 질문에 대한 답):

MM 의 AutoLoad path 는 `MMSoundManager.Start()` 에서 **1회만** 호출 — 우리 값을 *반복* 덮어쓰는 게 아님. MM 코드 주석:

```
This is done on Start and not Awake because of a bug in Unity's AudioMixer API
```

즉 진짜 문제는 MM 가 우리 값을 덮어쓰는 게 아니라 **Unity AudioMixer.SetFloat 자체의 타이밍 버그**. 폴링이 효과적인 이유는 두 시스템 충돌 해결이 아니라 이 Unity 버그 우회 + AudioSource 늦은 활성화 케이스 안전망. AutoLoad 단순히 끄면 Unity 버그가 그대로 남음.

### 머지 충돌 정리 ✅

`develop` → `feat/S14P31C201-606/cl-232-버그-수정-2-차` 머지 충돌 2개 해결:

#### `RunManager.cs` 2개소
- 1000~1056행 (`HandlePlayerStateChanged`/`HandlePlayerDefeatedDirect`): HEAD 의 헬퍼 추출 구조 (`CheckPartyDefeatNow()`) 유지. develop 의 멀티 대응 코드는 HEAD 가 이미 상위 호환으로 흡수. `HandlePlayerDefeatedDirect` public 시그니처 유지 (PlayerHealthSync.cs:547 외부 호출).
- 1103~1112행 (`ShowResultingUI`): 양쪽 다 살리기 — `_resultingUiShown` idempotent 가드 + `Time.timeScale = 1f` 복구. 둘 다 서로 다른 안전장치.

#### `DefaultNetworkPrefabs.asset`
양쪽 NetworkPrefab 6개 (HEAD 5 + develop 1) 모두 보존.

---

## 변경 파일 목록

### 신규 (.cs)
- `Scripts/Runtime/UI/UIInputBlocker.cs` — 정적 카운터
- `Scripts/Runtime/UI/UIInputBlockerSource.cs` — 자동 Acquire/Release
- `Scripts/Runtime/UI/HitVignetteOverlay.cs` — A-10 (비활성)
- `Scripts/Runtime/UI/RunTimerHudPresenter.cs` — A-12 HUD 타이머
- `Scripts/Runtime/Audio/SettingsAutoApplier.cs` — 사운드 자동 적용

### 수정 (.cs)
- `Scripts/Runtime/Combat/StatId.cs` — `MaxHealthFlat` 추가
- `Scripts/Runtime/Combat/PlayerHealthStatApplier.cs` — `(base+flat)×mul` 합성
- `Scripts/Runtime/Combat/PlayerDamageReceiver.cs` — 로그 방어 공식 + 상세 로그
- `Scripts/Runtime/Talents/TalentStartupApplier.cs` — MaxHealth → MaxHealthFlat
- `Scripts/Runtime/TestKhi/KhiMeleeComboController.cs` — 가드 + 홀드
- `Scripts/Runtime/TestKhi/KhiBowController.cs` — 가드 + 홀드
- `Scripts/Runtime/TestKhi/KhiParryController.cs` — 가드
- `Scripts/Runtime/TestKhi/KhiDashController.cs` — 가드
- `Scripts/Runtime/TestKhi/KhiDaggerTeleportController.cs` — 가드
- `Scripts/Runtime/TestKhi/KhiCombatFeedbackBinder.cs` — A-10 hook
- `Scripts/Runtime/Stage/RunManager.cs` — 게터 추가 + 머지 충돌 해결
- `Scripts/Runtime/Shop/InventoryToggleController.cs` — UIInputBlocker 백업
- `Scripts/Runtime/UI/SettingsPanelView.cs` — OnEnable 추가 + 재시도 코루틴

### 수정 (.prefab / .asset)
- `_Project/Prefabs/UI/InventoryPanel.prefab` — Canvas + GraphicRaycaster + UIInputBlockerSource
- `_Project/Prefabs/UI/PausePanel.prefab` — UIInputBlockerSource
- `_Project/Prefabs/UI/RewardPanel.prefab` — UIInputBlockerSource
- `_Project/Prefabs/UI/ShopPanel.prefab` — UIInputBlockerSource
- `_Project/Prefabs/UI/TalentPanel.prefab` — UIInputBlockerSource
- `_Project/Prefabs/UI/Minimap/MinimapRig.prefab` — BottomInfo/RunTimer/TimerBackground
- `_Project/Prefabs/Weapons/Bow/KhiArrow.prefab` — targetLayers Enemies only (m_Bits 4294967295 → 8192)
- `TopDownEngine/.../MMSoundManagerSettings.asset` — AutoSave: 0 → 1
- `_Project/DefaultNetworkPrefabs.asset` — 머지 충돌 해결

---

## Unity Editor 후속 작업

### 사용자 직접 완료한 항목 ✅
- A-3: `TestKhi_Net_AD.prefab` / `TestKhi_MinimalCharacter2D.prefab` 의 `KhiDashController.allowDashDuringAttack` + `KhiParryController.allowParryDuringAttack` 토글 ON
- A-9: `TalentPanel.prefab` 각 row 의 `TalentRowView._valueText` 바인딩 점검

### 선택 작업
- A-10 활성화하려면: HUD Canvas 풀스크린 Image + HitVignetteOverlay 부착 + KhiCombatFeedbackBinder 연결

---

## 검증

| 시나리오 | 기대 결과 |
|---------|----------|
| 인벤토리/상점/보상창/일시정지/재능 패널 열고 좌클릭 | 공격 안 됨 |
| 패널 열고 우클릭 | 패리/텔레포트 안 됨 |
| 패널 열고 Space | 대시 안 됨 |
| 좌클릭 홀드 (검·활) | 공속에 맞춰 자동 연사 |
| 콤보 1~3타 중 Shift/Space (캔슬 토글 ON) | 즉시 대시 전이 |
| 적 공격 10 데미지, 방어력 20 | 8 받음 (`[DamageReceiver] HIT — raw=10 \| def=20 \| final=8`) |
| 재능 체력 10포인트 | 마을·던전 모두 +10 (110), 1100 아님 |
| 옵션에서 BGM 0.2 저장 → 게임 재시작 | 옵션 메뉴 안 열어도 즉시 0.2 로 재생 |
| 던전 진입 | 미니맵 아래 타이머 `00:00` 부터 카운트업, 검은 박스 배경 |

---

## 위험 / 메모

- **방어 공식 곡선**: 방어력 400 ≈ 99% 감소 캡. 게임 후반 빌드에서 방어력 400 넘기는 경로가 가능한지 디자인 단계 재검토.
- **고정 피해도 방어 적용**: 사용자 명시 공식 그대로. RPG 일반 관습과 다르므로 QA 시 의도 재확인.
- **A-4 홀드 공격 + A-3 캔슬 결합**: 홀드 중 dash 캔슬 후 마우스 누른 상태면 자동 재발사. MVP 정책 — 거슬리면 release 감지 플래그 추가 필요.
- **사운드 폴링 빈도**: 매 0.5초 `mixer.SetFloat` 호출. CPU 부담 사실상 0 이지만 향후 더 빈도 줄이고 싶다면 1~2초로 조정 가능.
- **`StatModifier` 로그 형식 표시 버그**: `AddPermanent Defense +1000.0%` 출력은 `magnitude:+0.0%` 포맷이 flat 정수 10 을 1000% 로 표시하는 단순 표기 문제. 실제 동작 정상 (`GetTotalFlat(Defense)` 가 10 반환).

---

## git 작업 (사용자가 직접)

머지 결과 commit 필요:

```bash
git add client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs
git add client/LostMemory/Assets/DefaultNetworkPrefabs.asset
# Track A 작업물 전체
git add client/LostMemory/Assets/_Project/Scripts/...
git add client/LostMemory/Assets/_Project/Prefabs/...
git commit
```

---

## 진행 상태 요약

| 분류 | 개수 |
|------|------|
| 코드/Inspector 완료 | 11 (A-1~A-9, A-11, A-12) |
| 패스 (사용자 결정) | 1 (A-10) |
| 인접 문제 해결 | 4 (인벤토리 raycast / 사운드 자동 / 머지 충돌 / 다른 패널 UIInputBlocker) |
| 검증 완료 | 사용자 Play 모드 확인 |

Track A 코드 작업 종료 상태. 남은 건 git commit 만.

# cl234 — Track A: 전투/입력/스탯/HUD 일괄 처리

## Context

게임 베타 피드백 32개 중 **Track A (플레이어·전투·스탯·상단 HUD)** 영역의 12개 항목을 한 트랙(한 작업자/한 브랜치)으로 묶어 일괄 처리한다. Track B (UI·맵·씬·인벤토리·보스 콘텐츠)와는 파일 영역이 분리되어 있어 머지 충돌 없이 병렬 가능. B 트랙 일부는 이미 완료, B-2/B-3는 별도로 해결됨.

**Track A 영역**: `Scripts/Runtime/TestKhi/`, `Scripts/Runtime/Combat/`, `Scripts/Runtime/Player/`, `Scripts/Runtime/Talents/`, `Scripts/Runtime/Audio/`, `Scripts/Runtime/Stage/RunManager.cs` (read-only로 시간 게터만), 인게임 HUD 일부.

---

## 항목별 수정 계획

### A-1. 보상창 떴을 때 공격 클릭이 카드 클릭으로 새어 들어감
- **수정 파일**: `Scripts/Runtime/TestKhi/KhiMeleeComboController.cs:479-489` (`WasAttackPressedThisFrame`)
- **수정 내용**: 메서드 진입부에 `EventSystem.IsPointerOverGameObject()` 가드 추가.
  ```csharp
  if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
      return false;
  ```
- **부수 효과**: A-2 도 같은 가드 한 줄로 동시 해결됨 (보상창·UI 패널 둘 다 UI 레이캐스트로 잡힘).
- **기존 자산 활용**: `RewardPanelView.cs:26-27`의 1초 selection delay는 그대로 둠 — UI 자체 보호용으로 의미 있음.

### A-2. UI 창 열렸을 때 클릭하면 공격됨
- A-1과 동일 한 줄로 해결.

### A-3. 공격 도중 대쉬/방어 캔슬 불가
- **수정 위치**: Inspector 토글만 ON.
  - `KhiDashController` 의 `allowDashDuringAttack` 필드 (코드: `KhiDashController.cs:128-146` 의 `ShouldBlockDash()` 가 참조)
  - `KhiParryController.cs:53` 의 `allowParryDuringAttack` 필드
- **변경**: 두 필드 모두 `true`. 캐릭터 prefab(`TestKhi_Net_AD.prefab` 또는 `TestKhi_MinimalCharacter2D.prefab`) 에서 직접 변경.
- **검증**: 콤보 중 dash·parry 키 입력 시 즉시 캔슬 → 전이되는지 확인.

### A-4. 기본 공격 홀드 자동 공격
- **수정 파일**: `Scripts/Runtime/TestKhi/KhiMeleeComboController.cs:479-489` (`WasAttackPressedThisFrame`)
- **수정 내용**:
  - `mouse.leftButton.wasPressedThisFrame` → `mouse.leftButton.isPressed` (홀드 감지)
  - 단, 매 프레임 발사되면 안 되므로 콤보/공속 쿨다운에 맞춰 자동 트리거. 이미 존재하는 attack interval/cooldown 변수와 결합.
  - 1회 호출(콤보 시작)과 홀드 갱신(다음 공격 트리거)을 분리해서 처리.
- **주의**: A-1 가드와 충돌하지 않도록 — UI 위에서 홀드 중이어도 차단되도록 순서 유지.

### A-5. 아이템 "최대체력 +10%" 가 +10/현재체력+10% 로 잘못 적용
- **확인 위치**: `Scripts/Runtime/Combat/PlayerHealthStatApplier.cs:41-42` (기본 multiplier 적용)
- **수정 대상**: `Scripts/Runtime/Relics/RelicEffectRegistry.cs` 의 `MaxHealthPercent` 분기 — `EffectEntry.Magnitude` 가 % (0.1) 로 들어왔을 때 multiplier에 정확히 곱해지는지, 정수 가산 경로로 새지 않는지 검증.
- **현재 추정 버그**: % 효과를 flat 정수처럼 `MaxHealth += Magnitude` 로 적용 + 현재체력에는 `current *= (1 + Magnitude)` 로 잘못 곱한 경로.
- **수정 내용**: % 효과는 `multiplier *= (1 + magnitude)` 형태로만 적용하고, 현재체력은 비율 유지(`SetHealth(MaximumHealth * ratio)`)로 통일.

### A-6. 재능 방어력 20 빌드에서 초반 무피해 (데미지 0)
- **수정 파일**: `Scripts/Runtime/Combat/PlayerDamageReceiver.cs:83-93`
- **현재 버그**: 방어력 → 데미지 차감 시 음수 결과를 clamp 안 함. 방어력 ≥ 데미지면 환불(refund) 계산이 잘못되어 데미지 0이 됨.
- **확정 공식** (팀원 김민재 제안 — 로그 기반 다이미니싱):
  ```
  치명타 적용 피해 = 공격력 × (1 + 치명타피해%)         // 치명타 발생 시
  방어 전 피해      = 치명타 적용 피해 + 고정 피해         // 고정 피해는 방어 적용 전 합산
  피해감소율        = 102.5 × Log10(1 + 0.025 × 방어력)   // %
  최종 피해         = RoundToInt(max(방어 전 피해 × (1 - 피해감소율/100), 1))
  ```
- **C# 구현 스케치** (`PlayerDamageReceiver` 또는 별도 `DamageFormula` static 헬퍼):
  ```csharp
  public static int ComputeFinal(int rawDamage, int defense)
  {
      if (defense < 0) defense = 0;
      float rate = 102.5f * Mathf.Log10(1f + 0.025f * defense);
      rate = Mathf.Clamp(rate / 100f, 0f, 0.99f);  // 방어 400+ 폭주 방지
      float reduced = rawDamage * (1f - rate);
      return Mathf.Max(Mathf.RoundToInt(reduced), 1);
  }
  ```
- **참고 곡선**:
  - 방어 0 → 0% / 10 → ~10% / 20 → ~18% / 40 → ~31% / 100 → ~56% / 200 → ~80% / 400 → ~100% (clamp 99%)
- **위험**:
  - 방어력 400 이상은 모두 99% 감소로 묶임. 게임 후반에 방어력 400 넘기는 빌드가 가능하면 캡 또는 곡선 재조정 필요. **현재 디자인 캡 미정 → 일단 99% clamp 로 안전 처리.**
  - "고정 피해도 방어 감소를 받는다" 는 RPG 일반 관습과 다름. 사용자 명시 공식이라 그대로 따르되, QA 단계에서 의도 재확인.
- **검증**: 방어력 20, 적 공격 10 → 8 받음. 방어력 100 → 4. 방어력 400 → 1.

### A-7. 방어 +10 시작 + 방어템 데미지 0
- A-6 로그 공식 적용으로 동시 해결. 방어력 +10 시작 + 방어템 누적이라도 로그 곡선에서는 누적 방어력 30~50대 → ~25~37% 감소만 됨. 0 데미지 케이스 완전 제거.

### A-8. 재능 체력 10 포인트 마을 미적용 / 던전 진입 시 1100
- **수정 파일**: `Scripts/Runtime/Talents/TalentStartupApplier.cs:125-135`
- **현재 흐름**: `TalentStartupApplier.Apply()` 가 `DungeonBuilt` 이벤트에서만 호출됨 → 마을에서는 스탯 컨테이너에 반영 안 됨 → 던전 진입 시 처음 적용되며 1100 표시.
- **수정 내용**: 마을 진입 시점에도 `Apply()` 호출되도록 fix. 후보:
  - `Town*Installer` 또는 `Phase` 진입 콜백에서 `TalentStartupApplier.Apply()` 호출
  - 또는 `TalentSaveService` 변경 이벤트에 즉시 Apply 구독
- **부수 점검**: 마을 HUD (`TownTopRightHUDView` 또는 유사) 가 스탯 컨테이너를 폴링하는지 이벤트 구독인지 확인 — 둘 다 갱신되도록.

### A-9. 재능 UI 최댓값 표기
- **수정 파일**: `Scripts/Runtime/Talents/TalentRowView.cs:36-41`
- **현재**: `$"{invested}/{_maxLevel}"` 포맷팅 코드 존재. 하지만 `Refresh()` 호출 시점이 누락되어 처음 열거나 재진입 시 빈 값.
- **수정 내용**: `TalentPanelView.Open()` 에 `foreach (row in _rows) row.Refresh();` 추가. 또는 `OnEnable` 에서 자동 갱신.

### A-10. 피격 시 화면 테두리 선혈 효과 (신규)
- **신규 컴포넌트**: `Scripts/Runtime/UI/HitVignetteOverlay.cs` (가칭)
- **표시 방식**: Screen Space - Overlay Canvas 위 풀스크린 Image — 빨간 가장자리 그라데이션 스프라이트, 알파 페이드 인/아웃 (예: 0.3초 페이드 인, 0.5초 페이드 아웃).
- **트리거**: `PlayerDamageReceiver` 의 OnDamaged 이벤트 또는 기존 `KhiCombatFeedbackBinder.cs:110-115` 의 HitStun 핸들러에 한 줄 추가.
- **자원**: 가장자리 빨간 그라데이션 스프라이트 1장 (Assets/_Project/Art/UI/ 위치, 사용자가 별도로 제공하거나 임시로 단색 vignette 생성).
- **MVP**: 첫 단계는 단순 풀스크린 Image 알파 페이드. 셰이더/포스트프로세싱은 보류.

### A-11. 타격 소리 조절 옵션
- **상태**: ✅ **이미 처리됨** (사용자 확인). 본 plan 작업 범위 밖.

### A-12. 던전 플레이 중 플레이 시간 HUD 표시
- **표시 위치**: **미니맵 아래** (사용자 확정 — 시선 동선 우선). B-15 (미니맵 스테이지/라운드 표기) 와 같은 미니맵 prefab 안에 들어가지만 **서로 다른 GameObject** 이므로 prefab 라인 충돌은 사실상 없음.
- **수정 파일**:
  - `Scripts/Runtime/Stage/RunManager.cs:1084-1094` — `runStartedAt` 게터 public 추가 (`public float RunStartedAt => runStartedAt;`).
  - 신규: `Scripts/Runtime/UI/RunTimerHudPresenter.cs` — `Update()` 에서 `RunManager.Instance.RunStartedAt` 폴링, `MM:SS` 포맷팅으로 TMP 텍스트 갱신.
  - 미니맵 prefab (`Assets/_Project/Prefabs/UI/MinimapPanel.prefab` 또는 유사) 안 아래쪽에 TMP_Text + `RunTimerHudPresenter` 컴포넌트 부착.
- **기존 자산**: `RunResultPanelView.cs:49` 의 시간 포맷 함수 재사용 권장 (`FormatTime` 또는 유사). 없으면 신규 헬퍼에 인라인 `TimeSpan` 포맷.
- **B-15 와의 조율**: 작업 시작 시 미니맵 prefab 어떤 자식 노드 추가하는지 작업자 간 짧게 합의 (예: 타이머는 `MinimapPanel/BottomInfo/Timer`, 스테이지/라운드는 `MinimapPanel/BottomInfo/StageRound` 처럼 부모는 같이 쓰고 형제 GameObject 로 분리).

---

## 작업 순서 (제안)

1. **빠른 승리 (인스펙터 토글·한 줄 가드)** — A-1·A-2·A-3·A-9
2. **로직 버그 수정** — A-5·A-6·A-7 (Combat/Stats 한 파일군에서 일괄, 로그 공식 도입)
3. **재능 적용 시점 수정** — A-8
4. **입력 방식 변경** — A-4 (홀드)
5. **신규 컴포넌트** — A-10 (선혈)·A-12 (HUD 타이머)
6. ~~A-11~~ — 이미 처리됨, 작업 없음

---

## 실작업 진행 결과 (cl234)

| # | 항목 | 결과 | 변경 위치 |
|---|------|------|----------|
| A-1 | 보상창 공격 클릭 차단 | ✅ 코드 처리 | `KhiMeleeComboController.cs:WasAttackPressedThisFrame()` EventSystem 가드 추가 |
| A-2 | UI 위 클릭 공격 차단 | ✅ 코드 처리 | A-1과 같은 메서드 한 줄로 동시 해결 |
| A-3 | 공격 캔슬 토글 | 🟡 prefab Inspector | KhiDashController.allowDashDuringAttack / KhiParryController.allowParryDuringAttack 토글 ON (Unity Editor 작업) |
| A-4 | 홀드 자동 공격 | ✅ 코드 처리 | 같은 메서드에서 `wasPressedThisFrame` → `isPressed` 변경. RequestAttack 내부 가드가 공속 제어 |
| A-5 | 최대체력 +10% 적용 버그 | ✅ A-8과 동시 해결 | PlayerHealthStatApplier 합성 공식 `(base + flat) × mul` 로 변경 — 재능 폭주 제거되면서 자연스럽게 정상화 |
| A-6 | 방어 20 데미지 0 | ✅ 코드 처리 | `PlayerDamageReceiver.ComputeDefenseFinalDamage()` 신규 — 로그 공식 + 최소 1 floor |
| A-7 | 방어 +10 + 방어템 0 | ✅ A-6 공식으로 동시 해결 | — |
| A-8 | 재능 체력 1100 표기 | ✅ 코드 처리 | `StatId.MaxHealthFlat` 추가 + `TalentStartupApplier` 가 MaxHealth → MaxHealthFlat 으로 전환 + `PlayerHealthStatApplier` flat 합산 |
| A-9 | 재능 최댓값 표기 | 🟡 prefab 점검 | 코드는 `TalentRowView.Refresh()` 에서 `{invested}/{_maxLevel}` 이미 표시. prefab `_valueText` 바인딩만 확인 |
| A-10 | 피격 선혈 vignette | ✅ 신규 컴포넌트 | `Scripts/Runtime/UI/HitVignetteOverlay.cs` 신규 + `KhiCombatFeedbackBinder.HandleHitStun()` 에 `Pulse()` 호출 |
| A-11 | 타격 소리 조절 | — | 사용자가 이미 처리. 작업 없음 |
| A-12 | 던전 중 플레이 시간 HUD | ✅ 신규 컴포넌트 | `RunManager.RunStartedAt`/`ElapsedRunTime` public 게터 + `Scripts/Runtime/UI/RunTimerHudPresenter.cs` 신규 |

### Unity Editor 후속 작업 (prefab 와이어링)

코드는 모두 들어갔지만 다음은 Unity Editor 에서 직접 해야 함:

1. **A-3** — 캐릭터 prefab (`TestKhi_Net_AD.prefab` 또는 `TestKhi_MinimalCharacter2D.prefab`) 의 `KhiDashController` / `KhiParryController` 컴포넌트에서 토글 ON.
2. **A-9** — `TalentPanel.prefab` 안 각 row 의 `_valueText` 가 비어있는지 확인. 비어있으면 TMP_Text 드래그 연결.
3. **A-10** — 게임 HUD Canvas (Screen Space Overlay) 위에 풀스크린 Image GameObject 추가 → `HitVignetteOverlay` 컴포넌트 부착 → 캐릭터 prefab 의 `KhiCombatFeedbackBinder._hitVignette` 필드에 연결. Image 스프라이트는 빨간 가장자리 그라데이션(또는 임시 단색).
4. **A-12** — 미니맵 패널 prefab 안 아래쪽에 TMP_Text + `RunTimerHudPresenter` 컴포넌트 부착. B-15 (스테이지/라운드 표기) 와 형제 노드로 공존.

### 주의

- **PlayerHealthStatApplier 합성 공식 변경**으로 인해 **유물 % 효과의 체감이 다소 달라질 수 있음**. 이전: `base × (1 + flat_polluted)` (재능이 multiplier 폭주). 새: `(base + flat) × mul`. 정상화됐지만 밸런스 QA 권장.
- **`_lastAppliedMul` → `_lastAppliedMax`** 로 변수명 변경. `_lastAppliedMax = -1f` 초기값으로 첫 호출 강제 적용.
- **A-4 (홀드 공격)** + **A-3 (공격 캔슬 토글 ON)** 결합 시: 홀드 중 dash 캔슬 후 마우스 누른 상태면 자동으로 다음 공격 발사됨. 이게 거슬리면 release 감지 플래그 추가 작업 필요 — MVP 단계에선 일단 그대로.
- **방어 공식 곡선**: 방어력 400 ≈ 99% 감소 캡. 후반 빌드에서 방어력 400 넘기는 경로가 있는지 디자인 단계에서 재검토.

---

## 검증

- **A-1/A-2**: 인벤토리·상점·보상창 등 UI 패널 위에서 좌클릭 → 공격 모션 안 나옴. UI 닫은 후 빈 화면 클릭 → 공격 정상.
- **A-3**: 콤보 1·2·3타 중 각 시점에서 dash/parry 입력 → 즉시 캔슬 전이.
- **A-4**: 마우스 좌클릭 홀드 → 공속에 맞춰 자동 연사. UI 위 홀드 시 차단.
- **A-5**: "최대체력 +10%" 아이템 획득 → 최대체력 정확히 10% 증가 (예: 100 → 110), 현재체력 비율 유지 (예: 80% → 88).
- **A-6/A-7**: 방어력 20 + 10 데미지 → 8 받음 (~18% 감소). 방어력 100 → 4 받음. 방어력 400+ → 1 받음. 0 데미지 케이스 완전 제거.
- **A-8**: 재능 체력 10포인트 찍음 → 마을 HUD에서 즉시 1100 표시 + 던전 진입 시 동일.
- **A-9**: 재능 패널 열면 각 row 에 `X/MAX` 표시.
- **A-10**: 적 공격 받으면 화면 가장자리 빨간 vignette 0.3초 페이드 인 → 0.5초 페이드 아웃.
- **A-11**: 이미 처리됨 — 본 plan에서 작업 없음.
- **A-12**: 던전 진입 직후 미니맵 아래에 `00:00` → 시간 흐름에 따라 갱신. B-15 (스테이지/라운드) 표기와 같은 영역에서 자식 노드로 공존.

---

## 위험 / 메모

- **A-4 (홀드 공격)** 와 **A-3 (공격 캔슬)** 가 결합되면 흐름이 미묘해짐 — "홀드 중 dash 캔슬 후 다시 홀드 중이면 자동 공격 재개?" 정책 필요. **MVP 정책**: 캔슬 후 마우스 떼고 다시 누르기 전까지는 자동 재개 안 함. 추후 사용자 확인.
- **A-8 마을 적용 타이밍** — TalentSaveService 변경 이벤트에 구독하면 매 포인트 투자마다 Apply 호출되어 비싸질 수 있음. 디바운스 또는 패널 닫을 때만 Apply 호출하는 방식 검토.
- **A-10 신규 컴포넌트** — 셰이더 없이 UI Image vignette 만으로도 충분한 시각 효과 가능. URP 셰이더 / Post-Processing Volume 도입은 보류.
- **B 트랙과의 경계** — A-12 (던전 HUD 시간) 가 B-15 (미니맵 스테이지/라운드 표기) 와 같은 미니맵 prefab 안에 들어감. 사용자 결정: 서로 다른 GameObject 자식 노드로 공존 → prefab 라인 충돌 사실상 없음. 작업자 간 자식 노드 이름만 사전 합의.
- **A-6 방어 공식 곡선** — 방어력 400 ≈ 99% 감소 캡. 게임 후반에 방어력이 400 넘는 빌드가 가능한지 디자인 단계에서 재검토 필요. 현재 plan은 `Clamp(_, 0, 0.99)` 로 안전 처리.
- **A-6 "고정 피해도 방어 적용"** — 일반 RPG 관습과 다름. 사용자 명시 공식에 따라 그대로 구현하되, QA 시 게임플레이 의도와 부합하는지 재확인.

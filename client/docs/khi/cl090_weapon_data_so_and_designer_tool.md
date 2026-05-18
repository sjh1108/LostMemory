# CL-090 WeaponData SO + Save & Auto-revert 안전망 (MVP critical)

작성일: 2026-04-25
업데이트: 2026-04-27 (Plan refinement + Auto-revert 검증 완료)
상태: 🟢 **완료** — Save / Auto-revert 안전망 모두 검증, MVP 폴리시 진입 가능
시간 견적: 5~6h. 실 사용 ~3h.

---

## 📌 Plan 변경 메모 (2026-04-27)

**원래 plan**:
- CL-090: WeaponData SO + Save (안전성 절반)
- CL-103: Custom Inspector + Scene Gizmo + Auto-revert

**작업 중 발견**: Auto-revert 와 Save 는 한 세트인 **안전성 메커니즘**이고, Custom Inspector / Scene Gizmo 는 별개 **UX 폴리시** 카테고리. plan 단계의 카테고리 분류 실수.

**Plan refinement 결정**:
- **CL-090**: Save **+ Auto-revert** (안전성 한 묶음)
- **CL-103**: Custom Inspector + Scene Gizmo (UX 폴리시 한 묶음)

**정당화**: Save 만 있고 Auto-revert 없으면 "사고로 변경된 값도 영구 저장" 위험 — Save 의 안전성 절반밖에 못 함. 둘이 같은 ticket 에 있어야 안전성 메커니즘이 완성됨.

작업 총량 변동 없음 (Auto-revert 는 어차피 만들 거, ticket 경계만 재배치). Scope creep 아닌 **plan refinement**.

---

## 🚀 이어서 할 때 — 5분 안에 컨텍스트 복원

### 이미 끝난 것

✅ `WeaponData.cs` + `AttackStepData` 클래스 정의 (신규)
✅ `KhiMeleeTypes.cs` 정리 (`KhiMeleeAttackStep`/`KhiDirectionalHitbox` 제거)
✅ `KhiMeleeComboController.cs` 리팩터 (WeaponData 참조)
✅ `KhiSlashAnimator.cs` 리팩터 (frames/tint/autoMirror SO 에서 가져옴)
✅ `KhiMeleeHitbox.cs` 시그니처 변경
✅ `KhiAttackVisualPresenter.cs` 시그니처 변경
✅ `KhiFinisherLunge.cs`, `KhiCombatFeedbackBinder.cs`, `KhiWeaponPresenter.cs` 시그니처 변경
✅ **Step 2** Sword_Default.asset 생성 + 데이터 입력 (사용자 완료)
✅ **Step 3e** Prefab WeaponData 슬롯 연결 (사용자 완료)
✅ **Step 5 부분** 라이브 튠 + Save Current Values 동작 확인

### 남은 것 — 모두 완료 ✅

1. ✅ Step 6 — Auto-revert Editor 스크립트 (`WeaponDataPlayModeIsolator.cs`) 동작 검증
2. ✅ Step 5 라이브 튠 + Save Current Values 보존 검증
3. (선택) 회귀 검증 — 1/2/3타 콤보, Finisher Lunge, Combat Feedback 정상 동작
4. (권장) git commit baseline 떠두기

본 ticket 의 핵심 산출물 (WeaponData SO + Save + Auto-revert 안전망) 모두 동작.

### 검증 시 발견된 Unity 6 구현 노트

- `EditorApplication.playModeStateChanged` 의 `ExitingEditMode` 이벤트가 도메인 리로드 타이밍 때문에 옛 도메인에서 발화 후 unload 되어 핸들러가 효과 없음.
- `[InitializeOnEnterPlayMode]` attribute backstop 으로 해결. 새 도메인에서 Play 진입 시 무조건 호출되어 Snapshot 보장.
- `EnteredEditMode` (Play 종료 후) 는 정상 발화 → Restore 작동.

### 막힘 신호

- Unity 컴파일 에러 발생: `using LostMemory.Data;` 누락 가능성. Console 메시지 보고 해당 파일 추가.
- Awake 에서 `[KhiMeleeComboController] WeaponData 가 할당되지 않음` 에러: Step 3e 의 prefab 드래그 누락. prefab 다시 확인.

---

## 본 ticket 의 위치

이 ticket은 **MVP 전투 밸런스/느낌 폴리시를 가능하게 만드는 enabler** 입니다. 풀 디자이너 친화 도구가 아니라 **lean 버전**:
- WeaponData SO 자료 구조 확립 + 데이터 마이그레이션
- 라이브 튠 (Play 중 SO 변경 즉시 반영 + ContextMenu "Save Current Values" 영구 저장)

추가 폴리시 (Custom Inspector 정밀 그룹핑, Scene Gizmo, 다른 카테고리 확장)는 별도 ticket으로 분리:
- **CL-103**: 디자이너 툴 폴리시 (Custom Inspector + Scene Gizmo)
- **CL-104**: 데이터 카테고리 확장 (EnemyData, CharacterData)

## 목적

CL-067 작업 중 두 가지 한계 발견:

1. **데미지 영역과 시각 영역이 별개로 baked** — `KhiMeleeAttackStep.Baseline.Offset` 과 `SlashSlot.localPosition` 이 따로 튜닝. 1타·3타에서 위치 어긋남.
2. **밸런스 수치가 Inspector 여기저기 분산** — `KhiMeleeComboController.attackSteps`, `KhiSlashAnimator.combo*Frames/Tint`, prefab 별 hitbox 등. 밸런스 폴리시 iteration이 느림.

→ 무기 단위 ScriptableObject로 통합하고, 라이브 튠 가능하게 만들어 **MVP 폴리시 단계의 iteration 속도 확보**.

## 결정 사항 (CL-067 논의에서 합의)

### Phase 3 사용자 확인 요약

| 항목 | 결정 | 비고 |
|---|---|---|
| 공통 콤보 수치 (baseDamage 등) | **WeaponData 에 포함** | 무기마다 다를 수 있음. 향후 망치/활 추가 시 자연 분기 |
| SlashSlot transform 처리 | **옵션 B (prefab 사전 배치 유지)** | 시각 편집 워크플로우 보존. SO 는 frames/tint/콤보 데이터만. 헷갈림 완화는 책임 분리 주석 |
| Live tune 저장 방식 | **ContextMenu 우클릭 버튼** | "Save Current Values". Custom Editor 불필요 |
| `slashRigCenterY` 위치 | **KhiSlashAnimator 잔존** | 캐릭터 가슴 높이, 무기와 무관. CL-104 CharacterData 도입 시 이전 |
| WeaponData null 처리 | **에러 + disable** | 안전 우선, silent 통과 X |

---

## 코드 작업 결과 (완료)

### 신규 파일

#### `client/LostMemory/Assets/_Project/Scripts/Runtime/Data/WeaponData.cs`

`WeaponData` ScriptableObject + `AttackStepData` Serializable 클래스 정의.

**WeaponData 필드** (private SerializeField + getter, TalentData 패턴 일치):
- Identity: `_displayName`
- Combo Common: `_baseDamage`, `_comboInputWindow`, `_minimumChainInputDelay`, `_inputBufferDuration`
- Visual Common: `_autoMirrorOnLeftAim`, `_frameInterval`
- Combo Steps: `_steps` (AttackStepData[])

**AttackStepData 필드** (public, [Serializable] class):
- Identity: `label`, `comboStep`, `damageMultiplier`
- Timing: `startupDuration`, `activeDuration`, `recoveryDuration`, `animatorTrigger`
- Hitbox: `hitboxOffset`, `hitboxSize`
- Visual: `slashFrames`, `slashTint`

**ContextMenu**:
- `[ContextMenu("Save Current Values")]` — Play 중 변경값 영구 저장. `EditorUtility.SetDirty()` + `AssetDatabase.SaveAssetIfDirty()`.

**namespace**: `LostMemory.Data`
**CreateAssetMenu**: `LostMemory/Combat/WeaponData`

**책임 분리 주석 명시**: 클래스 헤더와 `slashFrames` Tooltip 에 "**위치 데이터는 prefab 의 SlashSlot transform 에서 결정. 본 SO 는 무엇/언제만 다룸**" 명시.

### 수정된 파일

#### `KhiMeleeTypes.cs`
- `KhiMeleeAttackStep` 클래스 + `KhiDirectionalHitbox` 구조체 **제거**
- `KhiAttackRequest` 만 잔존 (런타임 struct, SO 와 독립)

#### `KhiMeleeComboController.cs`
- `[SerializeField] private WeaponData weaponData;` 추가
- 제거: `baseDamage`, `comboInputWindow`, `minimumChainInputDelay`, `inputBufferDuration`, `attackSteps`, `EnsureDefaultSteps()`, `CreateStep1/2/3()`
- `Awake()` 에 weaponData null 검증 + 에러 로깅 + `enabled = false`
- 이벤트 시그니처: `KhiMeleeAttackStep` → `AttackStepData`
- `step.ComboStep` → `step.comboStep`, `step.DamageMultiplier` → `step.damageMultiplier` 등 (camelCase)
- `weaponData.BaseDamage * step.damageMultiplier` 등 SO 참조
- `public WeaponData WeaponData` getter 추가 (KhiSlashAnimator 가 fallback 으로 사용)

#### `KhiSlashAnimator.cs`
- `[SerializeField] private WeaponData weaponData;` 추가 (없으면 `comboController.WeaponData` fallback)
- 제거: `combo1/2/3Frames/Tint`, `autoMirrorOnLeftAim`, `frameInterval`, `DefaultSlotLocalPositions/Rotations/Scales` 상수, `EditorResetSlotPositions` ContextMenu
- 유지: `slashRigCenterY` (캐릭터 속성), `slashSortingOrder/Layer`, `EditorSetupSlashRig` ContextMenu (rig+slot 자동 생성 fallback)
- frames/tint/autoMirror/frameInterval 모두 `weaponData.Steps[i].slashFrames` / `weaponData.AutoMirrorOnLeftAim` 등으로 SO 에서 가져옴
- 슬롯 자동 생성 시 default transform 사용 (디자이너가 prefab 에서 직접 편집)

#### `KhiMeleeHitbox.cs`
- `Sample()` 시그니처 `KhiMeleeAttackStep step` → `AttackStepData step`
- `step.Baseline.Offset/Size` → `step.hitboxOffset/hitboxSize`

#### `KhiAttackVisualPresenter.cs`, `KhiFinisherLunge.cs`, `KhiCombatFeedbackBinder.cs`, `KhiWeaponPresenter.cs`
- 이벤트 핸들러 시그니처 모두 `KhiMeleeAttackStep` → `AttackStepData` 변경
- `step.ComboStep` → `step.comboStep`, `step.AnimatorTrigger` → `step.animatorTrigger` 등 camelCase 통일
- `using LostMemory.Data;` 추가

### 검증 (전체 코드)

- `KhiMeleeAttackStep`, `KhiDirectionalHitbox`, `attackSteps`, `EnsureDefaultSteps`, `CreateStep[123]` 잔존 참조 0개
- `step.ComboStep`, `step.DamageMultiplier`, `step.StartupDuration`, `step.ActiveDuration`, `step.RecoveryDuration`, `step.AnimatorTrigger` (대문자 시작) 잔존 참조 0개

---

## 📋 사용자 수동 작업 가이드

### Step 2 — `Sword_Default.asset` 생성 + 데이터 입력 (~30분)

**Unity 에디터에서**:

#### 1. 폴더 생성

Project 창에서:
- `Assets/_Project/ScriptableObjects/` 폴더 안으로 이동
- 우클릭 → Create → Folder → 이름 `Weapons`

#### 2. 자산 생성

- `Assets/_Project/ScriptableObjects/Weapons/` 폴더에서 우클릭
- **Create → LostMemory → Combat → WeaponData** 클릭
- 이름: `Sword_Default`

#### 3. Inspector 에서 값 입력

(현재 코드 하드코딩 + prefab 값 그대로 — 마이그레이션 단순 복사)

##### Combo Common
| 필드 | 값 |
|---|---|
| Base Damage | `10` |
| Combo Input Window | `0.6` |
| Minimum Chain Input Delay | `0.12` |
| Input Buffer Duration | `0.25` |

##### Visual Common
| 필드 | 값 |
|---|---|
| Auto Mirror On Left Aim | `true` (체크박스 ON) |
| Frame Interval | `0.04` |

##### Combo Steps (Size = 3)

**Steps[0] — 1타**
| 필드 | 값 |
|---|---|
| label | `1타: 빠른 사선` |
| comboStep | `1` |
| damageMultiplier | `1.0` |
| startupDuration | `0.1` |
| activeDuration | `0.1` |
| recoveryDuration | `0.15` |
| animatorTrigger | `Attack_1` |
| hitboxOffset | `(1.05, -0.2)` |
| hitboxSize | `(1.7, 1.1)` |
| slashFrames | 기존 prefab 의 `KhiSlashAnimator.combo1Frames` 4개 그대로 드래그 |
| slashTint | `(255, 255, 255, 255)` 흰색 |

**Steps[1] — 2타**
| 필드 | 값 |
|---|---|
| label | `2타: 반대 사선` |
| comboStep | `2` |
| damageMultiplier | `1.1` |
| startupDuration | `0.12` |
| activeDuration | `0.12` |
| recoveryDuration | `0.2` |
| animatorTrigger | `Attack_2` |
| hitboxOffset | `(1.05, 0.2)` |
| hitboxSize | `(1.8, 1.2)` |
| slashFrames | 기존 `combo2Frames` 5개 드래그 |
| slashTint | 흰색 |

**Steps[2] — 3타**
| 필드 | 값 |
|---|---|
| label | `3타: 마무리` |
| comboStep | `3` |
| damageMultiplier | `1.5` |
| startupDuration | `0.2` |
| activeDuration | `0.2` |
| recoveryDuration | `0.3` |
| animatorTrigger | `Attack_3` |
| hitboxOffset | `(1.25, 0)` |
| hitboxSize | `(2.5, 1.8)` |
| slashFrames | 기존 `combo3Frames` 9개 드래그 |
| slashTint | 흰색 |

#### 팁

- 기존 prefab 의 `combo1/2/3Frames` 배열 값을 미리 메모해두고 드래그 (또는 prefab 두 개 인스펙터 동시에 띄우고 복사)
- 자산 생성 후 `Ctrl+S` 로 씬/프로젝트 저장

---

### Step 3e — Prefab 에 SO 드래그 (~5분)

**Unity 에디터에서**:

#### 1. Prefab 열기

Project 창에서 `Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` 더블클릭 (또는 씬에서 인스턴스 선택)

#### 2. KhiMeleeComboController 컴포넌트

Inspector 에서 새로 추가된 **Weapon Data** 슬롯에 `Sword_Default.asset` 드래그.

(만약 기존 `Attack Steps` 빈 배열이 보이면 무시 — Unity 가 다음 prefab 저장 시 자동 정리)

#### 3. KhiSlashAnimator 컴포넌트

Inspector 의 **Weapon Data** 슬롯에도 동일하게 `Sword_Default.asset` 드래그.

(또는 비워둬도 OK — Awake 에서 comboController 통해 자동 fallback)

#### 4. 저장

`Ctrl+S` 로 prefab 저장 (또는 prefab 모드 종료 시 "Save changes" 다이얼로그에서 Save).

---

### Step 5 — Play 모드 검증 (~10분)

#### 기본 동작 검증

- [ ] `test_khi.unity` 씬에서 Play 진입
- [ ] 1/2/3타 콤보 정상 동작 (Right aim 부터 시작)
- [ ] 4방향 cardinal (Right/Up/Left/Down) 모두 정상
- [ ] 대각선 aim 정상 (45° 단위 sweep)
- [ ] 슬래시 sprite 표시 + 색상 정상

#### 라이브 튠 검증

Play 중에:

- [ ] `Sword_Default` 자산 선택 → Inspector 열기
- [ ] `steps[0].damageMultiplier` 1 → 2 변경 → 다음 1타 attack 시 데미지 2배 확인
- [ ] `steps[2].slashTint` 빨간색으로 변경 → 3타 슬래시 빨갛게 보임 확인
- [ ] `steps[0].hitboxSize` (1.7, 1.1) → (3, 2) 변경 → 빨간 디버그 박스 즉시 커지는 거 확인
- [ ] `Sword_Default` 자산 우클릭 → **"Save Current Values"** 클릭
- [ ] Console 에 `[WeaponData] Saved: Sword_Default` 메시지 확인

#### 영구 저장 검증

- [ ] Play 정지
- [ ] `Sword_Default` 자산 다시 선택 → 변경값 보존됐는지 확인 (위에서 Save Current Values 한 값들)

#### 에러 케이스 검증

- [ ] 빈 prefab 임시 생성 (또는 기존 prefab 인스턴스의 WeaponData 슬롯 비우기)
- [ ] Play 진입 → Console 에 `[KhiMeleeComboController] WeaponData 가 할당되지 않음` 에러 확인
- [ ] 공격 키 눌러도 attack 발동 안 됨 확인

#### 회귀 검증 (CL-067 결과물 보존 확인)

- [ ] 슬래시 위치/회전이 이전 (CL-067 마무리 시점) 과 동일하게 보임
- [ ] hemisphere mirror 동작 (Left aim 에서 flipX) 정상
- [ ] `slashRigCenterY = 0.6` 유지 — 회전 중심 흔들림 없음
- [ ] Finisher Lunge (3타 active 시 lunge) 정상
- [ ] Combat Feedback (히트스톱, 카메라 쉐이크, Finisher 강조) 정상

---

## Step 6 — Auto-revert 안전망 (Plan refinement 추가)

**목적**: Play 중 SO 변경이 자동 영구 저장되는 Unity 기본 동작 → 사고로 변경된 값까지 보존되는 위험. Auto-revert 도입으로 **"Save 누른 값만 보존, 나머지는 Play 종료 시 자동 복원"** 워크플로우 확보.

이 작업으로 **Save (commit) + Auto-revert (cancel)** 안전성 메커니즘이 한 세트로 완성됨.

### 작업 항목 (1.5~2h, 3h hard timer)

#### 6a. Editor 스크립트 작성 (~1h)

**파일**: `Assets/_Project/Scripts/Editor/WeaponDataPlayModeIsolator.cs`

**기능**:
- `[InitializeOnLoad]` 로 Unity 시작 시 등록
- `EditorApplication.playModeStateChanged` 구독
- Play 시작 시 (`ExitingEditMode`): 모든 `WeaponData` 자산을 `EditorJsonUtility.ToJson` 으로 snapshot
- Play 종료 시 (`EnteredEditMode`): committed 안 된 자산은 `EditorJsonUtility.FromJsonOverwrite` 로 snapshot 복원 + `EditorUtility.SetDirty` + `AssetDatabase.SaveAssetIfDirty`
- `MarkCommitted(WeaponData)` public 메서드: snapshot 을 현재 값으로 overwrite (Save 시점이 새 baseline)

#### 6b. WeaponData.cs 의 SaveCurrentValues 확장 (~10분)

`SaveCurrentValues` ContextMenu 안에서 `WeaponDataPlayModeIsolator.MarkCommitted(this)` 호출 추가. Play 모드 외에서는 무해 (committed 가 다음 Play 시작 시 reset).

#### 6c. 검증 (~30분)

- [ ] Play 시작 → snapshot 자동 캡처 (백그라운드, UI 변화 없음)
- [ ] Play 중 `damageMultiplier` 1 → 5 변경 → 즉시 게임 반영
- [ ] Save Current Values 안 누르고 Play 정지
- [ ] `Sword_Default` 자산 다시 선택 → `damageMultiplier = 1` 로 자동 복원 확인 ✓
- [ ] Play 다시 시작 → `damageMultiplier` 1 → 3 변경 → Save Current Values 클릭 → Play 정지
- [ ] 자산 재선택 → `damageMultiplier = 3` 로 보존 확인 ✓
- [ ] Save 후에도 추가로 5 로 변경 → Play 정지 시 3 (Save 시점 baseline) 으로 복원 확인 ✓

### 시간 초과 방어

3h 안에 동작 안 하면 → 현 상태 commit 하고 별도 ticket (CL-105 등) 으로 옮김. CL-090 본 ticket 은 Save + 라이브 튠까지 만으로 닫음.

---

## 시간 합계 (refinement 후)

- Step 1 (코드): ~25분 (✅ 완료)
- Step 3 (코드 wiring): ~30분 (✅ 완료)
- Step 2 (수동 데이터 입력): ~30분 (✅ 완료)
- Step 3e (prefab 연결): ~5분 (✅ 완료)
- Step 5 부분 (라이브 튠): ~5분 (✅ 부분 완료, Auto-revert 검증 잔여)
- **Step 6 (Auto-revert): ~1.5~2h (진행 중)**

**합계 견적: ~3~3.5h**, plan 의 5~6h 안 충분히 들어옴.

---

## 후속 작업 (별도 ticket으로 분리)

본 ticket 완료 후, 다음 작업들이 자연 후속. **본 ticket 안에 넣지 말고 별도로** 진행:

### CL-103: 디자이너 UX 폴리시 (재정의됨)

**카테고리**: UX 폴리시 (Auto-revert 는 본 ticket 흡수로 빠짐).

- Custom Inspector (콤보 1/2/3 접힘 그룹, Tooltip 풍부, Validation 경고)
- Scene Gizmo (hitbox 빨간 박스, slash 영역 파란 박스 시각화)
- (선택) Scene 핸들 드래그로 hitbox 직접 편집
- (선택) "Sync Slot Positions" ContextMenu — 본 ticket 의 옵션 B 헷갈림 완화책 #2
- 시간 견적: 3~4h (Auto-revert 가 빠져 살짝 줄어듦)
- 시작 신호: MVP 끝난 후, 또는 디자이너/밸런스 담당자 합류 시점
- 포트폴리오 가치 ★★★★★

### CL-104: 데이터 카테고리 확장
- `EnemyData` SO 도입 (Orc Rider 등 — CL-039/040 연계)
- `CharacterData` SO 도입 (Khi 체력/이동속도/대시 등 — `slashRigCenterY` 도 이전)
- 기존 prefab/scripts 마이그레이션
- 시간 견적: 카테고리당 2~3h
- 시작 신호: 두 번째 무기 추가, 또는 적 종류 늘어남, 또는 캐릭터 밸런스 본격 작업
- 본 ticket의 WeaponData 패턴이 검증된 후 확장

### 까먹지 않게 — 미리 ticket 발급은 안 함

CL-103/CL-104는 ticket 번호만 예약. 실제 ticket 발급은 시작 시점에. 까먹 방지를 위해:
- 본 doc 의 "후속 작업" 섹션 (← 여기) — CL-090 닫을 때 자연스럽게 보임
- `client1_tasks_master_plan.md` 의 후순위 메모 — 다음 ticket 정할 때 보임

---

## 알려진 트레이드오프 / 주의

### 1. SO 와 prefab transform 분리 (옵션 B 선택의 결과)

- 디자이너가 hitbox 변경 시 SlashSlot transform 도 함께 수동 조정 필요
- 책임 분리 주석으로 안내 (보완책 #1 적용됨 — `WeaponData.cs` 클래스 헤더 + `slashFrames` Tooltip)
- 향후 거슬리면 보완책 #2 (CL-103 의 Sync ContextMenu) 추가

### 2. 이벤트 시그니처 변경

`AttackStarted`, `AttackActiveStarted`, `AttackActiveEnded`, `TargetHit`, `FinisherHit` 모두 `KhiMeleeAttackStep` → `AttackStepData` 로 시그니처 바뀜. 구독자 모두 영향:
- ✅ KhiSlashAnimator
- ✅ KhiAttackVisualPresenter
- ✅ KhiCombatFeedbackBinder
- ✅ KhiFinisherLunge
- ✅ KhiWeaponPresenter

향후 새 구독자 추가 시 `AttackStepData` 시그니처 사용해야 함.

### 3. Live tune Unity 동작

- Play 중 SO 변경은 Unity 가 자동 즉시 반영 (SerializedObject 메커니즘)
- ContextMenu "Save Current Values" 는 영구 저장만 담당
- 만약 즉시 반영 안 되면 KhiMeleeComboController 의 캐싱 의심 — 현재 매 GetStep 시 SO 참조하므로 캐싱 없음, 즉시 반영 보장

### 4. WeaponData null fallback 정책

- WeaponData 가 비면 `enabled = false` + Console error
- `EnsureDefaultSteps()` 폴백 제거 — silent 통과 차단
- 디버깅 시 누락 즉시 발견 가능

---

## 관련 문서

- 직전 ticket: [cl067_combat_visuals_and_effects.md](cl067_combat_visuals_and_effects.md)
- 마스터 플랜: [client1_tasks_master_plan.md](client1_tasks_master_plan.md)
- 참고 패턴: [cl039_cl040_charge_enemy_plan.md](cl039_cl040_charge_enemy_plan.md) (ChargeHitboxAnchor 분리 원칙)
- 콤보 시스템: [cl009_sword_combo_implementation_plan.md](cl009_sword_combo_implementation_plan.md)
- 기존 SO 패턴 참조: `Assets/_Project/Scripts/Runtime/Data/TalentData.cs`

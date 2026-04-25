# CL-090 WeaponData SO + 라이브 튠 (MVP critical)

작성일: 2026-04-25
상태: 🟢 작업 예정 (CL-067 닫기 후 즉시 시작)
시간 견적: **5~6시간** (4일 MVP 안 완료 목표)

## 본 ticket 의 위치

이 ticket은 **MVP 전투 밸런스/느낌 폴리시를 가능하게 만드는 enabler** 입니다. 풀 디자이너 친화 도구가 아니라 **lean 버전**:
- WeaponData SO 자료 구조 확립 + 데이터 마이그레이션
- 라이브 튠 (Play 중 SO 변경 즉시 반영 + 자동 저장)

추가 폴리시 (Custom Inspector 정밀 그룹핑, Scene Gizmo, 다른 카테고리 확장)는 별도 ticket으로 분리:
- **CL-103**: 디자이너 툴 폴리시 (Custom Inspector + Scene Gizmo)
- **CL-104**: 데이터 카테고리 확장 (EnemyData, CharacterData)

## 목적

CL-067 작업 중 두 가지 한계 발견:

1. **데미지 영역과 시각 영역이 별개로 baked** — `KhiMeleeAttackStep.Baseline.Offset` 과 `SlashSlot.localPosition` 이 따로 튜닝. 1타·3타에서 위치 어긋남.
2. **밸런스 수치가 Inspector 여기저기 분산** — `KhiMeleeComboController.attackSteps`, `KhiSlashAnimator.combo*Frames/Tint`, prefab 별 hitbox 등. 밸런스 폴리시 iteration이 느림.

→ 무기 단위 ScriptableObject로 통합하고, 라이브 튠 가능하게 만들어 **MVP 폴리시 단계의 iteration 속도 확보**.

## 결정 사항 (CL-067 논의에서 합의)

### "시각 ≥ 판정" 원칙

- Hitbox 가 mechanical truth (실제 데미지 영역)
- Visual 은 hitbox 기준 약간 큰 박스 — 플레이어 신뢰감
- Slash 위치 = `hitboxOffset + slashFeelOffset` 으로 derive → 시각이 항상 hitbox 따라감, 일치 자동 확보

### 검 1자루 스코프

본 ticket은 **`Sword_Default.asset` 1개만** 다룸. 망치/활/적/캐릭터 등은 CL-104 또는 그 이후 ticket으로.

### Lean 라이브 튠

Play 중 SO 변경 → 즉시 반영 + 자동 저장. Custom Inspector 폴리시 (그룹핑/Tooltip/Validation) 는 본 ticket 범위 밖, CL-103.

## 작업 단계

### Step 1: 데이터 클래스 정의 (1~1.5h)

**파일**: 
- `Assets/_Project/Scripts/Runtime/Combat/Data/WeaponData.cs`
- `Assets/_Project/Scripts/Runtime/Combat/Data/AttackStepData.cs`

**WeaponData 구조** (1차 초안):
```csharp
[CreateAssetMenu(fileName = "WeaponData", menuName = "LostMemory/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string displayName;

    [Header("Common")]
    public float baseDamage = 10f;
    public float comboInputWindow = 0.6f;
    public float minimumChainInputDelay = 0.12f;
    public float inputBufferDuration = 0.25f;

    [Header("Combo Steps")]
    public AttackStepData[] steps; // 1타/2타/3타
}

[Serializable]
public class AttackStepData
{
    [Header("Identity")]
    public string label;          // "1타: 빠른 사선"
    public int comboStep = 1;
    public float damageMultiplier = 1f;

    [Header("Timing")]
    public float startupDuration = 0.05f;
    public float activeDuration = 0.07f;
    public float recoveryDuration = 0.08f;
    public string animatorTrigger = "Attack_1";

    [Header("Hitbox (mechanical truth)")]
    public Vector2 hitboxOffset;
    public Vector2 hitboxSize;

    [Header("Visual (시각 ≥ 판정)")]
    public Sprite[] slashFrames;
    public Color slashTint = Color.white;
    public Vector2 slashFeelOffset;   // hitbox 기준 추가 보정
    public float slashScale = 1f;
    public float slashRotationOffset; // 3타의 -60° 등
}
```

상세 필드는 작업 시작 시 현재 코드 보면서 확정.

### Step 2: 자산 생성 + 데이터 이전 (1~1.5h)

**작업**:
- `Assets/_Project/Data/Weapons/Sword_Default.asset` 생성
- 현재 `TestKhi_MinimalCharacter2D.prefab` 의 `KhiMeleeComboController.attackSteps` 값들을 SO 에 복사
- `KhiSlashAnimator` 의 `combo1/2/3 Frames/Tint` 값들도 SO 에 복사
- `SlashSlot_1/2/3` 의 localPosition / localRotation / scale 도 적절히 분해해서 SO 에 (hitboxOffset/slashFeelOffset/slashRotationOffset 등으로)

### Step 3: 코드 wiring (1~2h)

**KhiMeleeComboController**:
- `[SerializeField] private WeaponData weaponData;` 추가
- 기존 `attackSteps` 배열 → `weaponData.steps` 사용
- `EnsureDefaultSteps()` 는 weaponData null 체크로 변경

**KhiSlashAnimator**:
- `[SerializeField] private WeaponData weaponData;` 추가 (또는 KhiMeleeComboController 통해 참조)
- 기존 `combo1Frames/Tint` 등 → `weaponData.steps[i].slashFrames/slashTint` 사용
- 슬롯 위치도 SO 의 hitboxOffset + slashFeelOffset 기반으로 계산

**KhiMeleeHitbox** (필요 시):
- `step.Baseline` → `step.hitboxOffset`/`hitboxSize` (구조 변경)

### Step 4: Live tune (1~1.5h)

**Editor 스크립트** `Assets/_Project/Scripts/Editor/WeaponDataPlayModeSaver.cs`:
- Play 중 `WeaponData` 자산 변경 감지 → `EditorUtility.SetDirty()` + `AssetDatabase.SaveAssets()` 자동 호출
- 또는 SO 에 `OnValidate()` 추가해서 변경 시 자동 저장 트리거

**구현 옵션 (작업 시 결정)**:
- A: 자동 저장 (모든 변경)
- B: "Apply Live Values" 버튼 (Custom Editor 필요 → 본 ticket 범위 외, A 추천)

### Step 5: 검증 (0.5h)

- Play 시작
- 게임 도중 `Sword_Default.asset` Inspector 에서 `baseDamage` / `slashTint` 등 슬라이더 드래그
- 즉시 반영되는지 확인
- Play 정지 → 변경값 보존 확인
- 값 원복 (Reset to Default) 동작 확인 (선택)

## 시간 초과 시 비상 계획

**5시간 hard timer**. 그 안에 Step 3 (wiring) 까지 못 끝내면:
- Step 4 (라이브 튠) 포기, Step 1~3 만 마무리 후 닫기
- 또는 git revert 해서 현재 인스펙터 in-place 튜닝으로 폴리시 진행

## 본 ticket 산출물

코드:
- `WeaponData.cs`, `AttackStepData.cs`
- `WeaponDataPlayModeSaver.cs` (Editor)
- `KhiMeleeComboController` / `KhiSlashAnimator` / `KhiMeleeHitbox` 변경 (SO 참조하도록)

자산:
- `Sword_Default.asset`

문서:
- 본 doc 업데이트 (작업 후 검증 결과)

## 후속 작업 (별도 ticket으로 분리)

본 ticket 완료 후, 다음 작업들이 자연 후속. **본 ticket 안에 넣지 말고 별도로** 진행:

### CL-103: 디자이너 친화 툴 폴리시
- Custom Inspector (콤보 1/2/3 접힘 그룹, Tooltip 풍부, Validation 경고)
- Scene Gizmo (hitbox 빨간 박스, slash 영역 파란 박스 시각화)
- (선택) Scene 핸들 드래그로 hitbox 직접 편집
- 시간 견적: 4~6h
- 시작 신호: MVP 끝난 후, 또는 디자이너/밸런스 담당자 합류 시점
- 포트폴리오 가치 ★★★★★

### CL-104: 데이터 카테고리 확장
- `EnemyData` SO 도입 (Orc Rider 등 — CL-039/040 연계)
- `CharacterData` SO 도입 (Khi 체력/이동속도/대시 등)
- 기존 prefab/scripts 마이그레이션
- 시간 견적: 카테고리당 2~3h
- 시작 신호: 두 번째 무기 추가, 또는 적 종류 늘어남, 또는 캐릭터 밸런스 본격 작업
- 본 ticket의 WeaponData 패턴이 검증된 후 확장

### 까먹지 않게 — 미리 ticket 발급은 안 함

CL-103/CL-104는 ticket 번호만 예약. 실제 ticket 발급은 시작 시점에. 까먹 방지를 위해:
- 본 doc 의 "후속 작업" 섹션 (← 여기) — CL-090 닫을 때 자연스럽게 보임
- `client1_tasks_master_plan.md` 의 후순위 메모 — 다음 ticket 정할 때 보임

## 미해결 / 작업 시 결정할 항목

1. **WeaponData 의 정확한 필드 정의** — 현재 코드 자세히 보면서 Step 1 진입 시 확정. 위 초안은 골격만.
2. **라이브 튠 자동 저장 트리거** — Step 4 진입 시 Editor 스크립트 / OnValidate / 명시 버튼 중 결정.
3. **Hitbox 회전 vs visual 회전 통합 방식** — 현재 hitbox 는 baseline 1개 + aim 회전, visual 은 SlashRig 가 회전. SO 화 할 때 하나로 통합할지 그대로 둘지.
4. **`SlashRig.localPosition` 의 0.6 값 처리** — 현재 `slashRigCenterY` 필드. SO 로 옮길지, KhiSlashAnimator 에 그대로 둘지.

## 관련 문서

- 직전 ticket: [cl067_combat_visuals_and_effects.md](cl067_combat_visuals_and_effects.md)
- 마스터 플랜: [client1_tasks_master_plan.md](client1_tasks_master_plan.md)
- 참고 패턴: [cl039_cl040_charge_enemy_plan.md](cl039_cl040_charge_enemy_plan.md) (ChargeHitboxAnchor 분리 원칙 — 본 ticket의 "시각 ≥ 판정" 원칙의 선례)
- 콤보 시스템: [cl009_sword_combo_implementation_plan.md](cl009_sword_combo_implementation_plan.md)

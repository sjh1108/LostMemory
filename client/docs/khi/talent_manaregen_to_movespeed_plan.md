# 재능: ManaRegen → MoveSpeed 교체 계획

## Context
현재 재능 5종 중 **ManaRegen(마나회복)** 은 등록은 되지만 실제 사용되는 코드가 없음 (마나 시스템 미구현). 분배해도 캐릭터에 영향 X — 데드 스탯.

마나 시스템 구현은 별도 티켓이라 미루고, 그 슬롯을 **MoveSpeed(이동속도)** 로 교체. MoveSpeed 는 `StatId.MoveSpeed` 가 이미 정의되어 있고 `PlayerMovementStatApplier.cs:21` 에서 `characterMovement.MovementSpeedMultiplier` 에 직접 적용하는 시스템이 동작 중이라 등록만 하면 즉시 캐릭터 이동속도에 반영됨.

5종 유지 → UI/레이아웃 변경 없음, 코드 변경 최소.

## 결정 사항
- **교체 대상**: `TalentType.ManaRegen` → `TalentType.MoveSpeed`
- **DisplayName**: "마나회복" → "이동속도"
- **IncreasePerPoint**: 기존 ManaRegen 값 그대로 유지 (밸런싱은 추후 별도 작업)
- **저장 데이터**: PlayerPrefs 키가 enum 이름 기반(`Talent_<TalentType>`)이라 자동으로 새 키(`Talent_MoveSpeed`) 사용. 기존 `Talent_ManaRegen` 키 값은 잊혀짐 (디버그 단계라 OK)
- **UI**: TalentPanel.prefab 미변경 (5행 그대로, 라벨만 SO 의 DisplayName 으로 자동 갱신)

## 핵심 제약
- **노소연 작업 코드 미변경**: `Memory*.cs` 는 영향 없음 (Talent 시스템과 독립)
- 외부 에셋 미변경 (`TopDownEngine/**` 등)

## 작업 단계

### 1. 코드 수정 (4파일)

#### 1-1. `TalentType.cs:11`
```csharp
// 변경 전
ManaRegen,      // 마나 회복

// 변경 후
MoveSpeed,      // 이동 속도
```

#### 1-2. `RunStartStats.cs:18-19`
```csharp
// 변경 전
/// <summary>마나 회복 증가량</summary>
public float ManaRegen;

// 변경 후
/// <summary>이동 속도 증가량</summary>
public float MoveSpeed;
```

#### 1-3. `TalentCalculator.cs:21`
```csharp
// 변경 전
ManaRegen    = model.GetStatValue(TalentType.ManaRegen),

// 변경 후
MoveSpeed    = model.GetStatValue(TalentType.MoveSpeed),
```

#### 1-4. `TalentStartupApplier.cs:135-145`
- `if (stats.ManaRegen != 0f) AddPermanent(StatId.ManaRegen, stats.ManaRegen, this)` → MoveSpeed 버전
- 로그 문자열의 `MaxHealth={...}` 다음에 `MoveSpeed={stats.MoveSpeed:+0.0%;-0.0%;0%}` 추가 (현재 로그에서 MoveSpeed 누락 — 추가 권장)

### 2. Unity 에셋 수정 (사용자가 Unity에서 진행)

#### 2-1. SO 에셋 rename + 수정
- Project 창: `Assets/_Project/ScriptableObjects/Talents/TalentData_ManaRegen.asset`
- F2 키로 rename → `TalentData_MoveSpeed.asset`
- Inspector 에서:
  - `Talent Type` 드롭다운: **MoveSpeed** 선택 (enum 변경 후 자동으로 보일 것)
  - `Display Name`: "이동속도"
  - `IncreasePerPoint`: 기존 값 유지 (혹은 0.05 = 5%/포인트 권장)
  - `MaxLevel`: 기존 값 유지

#### 2-2. 참조 확인 (자동 갱신될 가능성 높음)
- `TalentPanel.prefab` 의 `Talent Datas` 배열 → 4번 슬롯에 새 SO 정상 참조되는지 확인
- `TalentStartupApplier.prefab` 의 `Talent Datas` 배열 → 동일 확인
- rename 이라 자동으로 잡힐 것. 안 잡히면 새 SO 드래그로 재바인딩

### 3. 사전 점검: 캐릭터에 PlayerMovementStatApplier 부착 여부
MoveSpeed 가 실제로 캐릭터 이동속도에 반영되려면 `PlayerMovementStatApplier` 컴포넌트가 TestKhi 에 부착돼 있어야 함. (PlayerHealthStatApplier 처럼 누락된 경우 등록만 되고 효과 없음)

- TestKhi prefab 또는 씬 인스턴스 선택
- Inspector 에서 `Player Movement Stat Applier` 컴포넌트 존재 확인
- 없으면 `Add Component` → 추가 → 필드 와이어링 (CharacterMovement, Container)

### 4. 검증 시나리오
1. Town → NPC 상호작용 → TalentPanel 열기
2. 5번째 행 라벨이 **"이동속도"** 로 표시되는지
3. + 버튼으로 분배 → 저장 → 패널 닫기
4. 던전 1F-1R 진입 → Console:
   ```
   [StatModifier] AddPermanent MoveSpeed +X% src=TalentStartupApplier
   [TalentStartupApplier] 재능 스탯 적용 — ... MoveSpeed=+X%
   ```
5. **체감 검증**: 분배 안 한 상태와 비교해서 캐릭터 이동 속도가 빨라졌는지

## 핵심 파일

### 수정 (이번 작업)
- `Assets/_Project/Scripts/Runtime/Talents/TalentType.cs`
- `Assets/_Project/Scripts/Runtime/Talents/RunStartStats.cs`
- `Assets/_Project/Scripts/Runtime/Talents/TalentCalculator.cs`
- `Assets/_Project/Scripts/Runtime/Talents/TalentStartupApplier.cs`

### Unity 에셋
- `Assets/_Project/ScriptableObjects/Talents/TalentData_ManaRegen.asset` (rename)

### 참조용 (수정 X)
- `Assets/_Project/Scripts/Runtime/Combat/PlayerMovementStatApplier.cs` — MoveSpeed 적용 로직
- `Assets/_Project/Scripts/Runtime/Combat/StatId.cs` — `MoveSpeed` 정의 확인
- `Assets/_Project/Prefabs/UI/TalentPanel.prefab` — 미변경

## 위험/주의

| 위험 | 대응 |
|---|---|
| 기존에 분배된 ManaRegen 포인트 사라짐 | 디버그 단계라 수용. 사용자 첫 진입 시 패널에서 재분배 |
| SO rename 후 prefab 참조 깨질 가능성 | Unity 가 GUID 기반 참조라 보통 자동 유지. 확인만 |
| TalentType enum 값 변경 → 직렬화 영향 | enum 값은 이름 기반 직렬화. ManaRegen 자리가 MoveSpeed 가 되면 기존 ManaRegen 값은 무시됨. 그러나 enum 정수 인덱스 기반 직렬화면 위치가 같으므로 OK (3번 인덱스) |
| PlayerMovementStatApplier 누락 시 등록만 되고 효과 없음 | 작업 단계 3 에서 사전 점검 |
| `[TalentStartupApplier] 재능 스탯 적용` 로그에 MoveSpeed 항목 추가 | 작업 단계 1-4 에 포함 |

## 작업량
- 코딩: **~15분** (4파일, 라인 수정 minimal)
- Unity 작업: **~10분** (SO rename + 필드 수정 + 컴포넌트 확인)
- 검증: **~10분** (시나리오 5단계)
- **총 ~35분**

## 후속 (이번 작업 X)
- 마나 시스템 구현 시 ManaRegen 다시 추가 (별도 TalentType 슬롯)
- IncreasePerPoint 밸런싱 (디자이너/QA)

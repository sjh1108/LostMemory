# CL230 — 단검을 Q/휠 무기 사이클에 통합

## Context

현재 단검은 F9 디버그 키로만 진입 (WeaponUpgradeService → SO 교체). Q/휠은 Sword/Bow/Staff/Flamethrower 모드만 cycle (WeaponModeController). 두 시스템 분리.

사용자 요청: 단검을 정식 무기로 승격. Q/휠 cycle에 포함. F9 디버그 키 제거. 닌자 단검은 데이터 보존하되 F8 진입은 불가능.

병행 문제: 단검 모드 → 다른 무기 전환 시 보조 단검 (Weapon_Sub) 이 안 꺼지는 버그. WeaponModeController 가 swordObject 만 토글하고 Weapon_Sub 은 WeaponUpgradeService 가 관리. 단검 → 활 전환 시 보조 단검 떠다님.

## 변경 사항

### 1. WeaponModeController.cs

**enum 확장**:
```csharp
public enum WeaponMode
{
    Sword = 0,
    Dagger = 1,        // 신규 (Sword 다음 = cycle 시 인접)
    Bow = 2,
    Staff = 3,
    Flamethrower = 4,
}
```

**Inspector 신규 필드**:
- `[SerializeField] private WeaponUpgradeService weaponUpgradeService;`

**ApplyMode 변경**:
- `swordActive = (mode == Sword || mode == Dagger)` — 검 컴포넌트는 두 모드에서 활성
- `swordParry.enabled = (mode == Sword)` — 패링은 검 모드만 (단검은 우클릭 텔레포트)
- 모드별 WeaponUpgradeService 호출:
  - `Dagger` → `weaponUpgradeService.UpgradeToDagger()`
  - 그 외 모든 모드 → `weaponUpgradeService.RevertToDefault()` (보조 단검 자동 꺼짐)

→ 다른 무기로 가면 자동으로 검 SO 복귀 → WeaponUpgradeService 가 secondaryWeaponObject SetActive(false) 처리 → 보조 단검 꺼짐. 두 자루 문제 해결.

### 2. KhiWeaponUpgradeDebug.cs

- F8 (닌자 진입) 키 처리 제거
- F9 (단검 진입) 키 처리 제거
- F10 (검 복귀) 키도 제거
- 결과: 컴포넌트 사실상 빈 컴포넌트 → **prefab 에서 컴포넌트 제거 권장** (사용자 액션)

대안: 컴포넌트 자체는 보존하되 enable=false 두기 (코드는 남김, 키 입력 X)

### 3. KhiDaggerTeleportController.cs

- IsDaggerMode 체크는 `WeaponUpgradeService.Instance.CurrentKind == Dagger` 그대로 사용
- WeaponModeController 가 단검 모드 진입 시 WeaponUpgradeService.UpgradeToDagger() 호출 → CurrentKind 가 Dagger 됨 → 우클릭 텔레포트 자동 작동
- 코드 변경 없음

### 4. WeaponUpgradeService.cs

- 변경 없음 (UpgradeToDagger / RevertToDefault API 그대로)
- 닌자 SO 슬롯 (daggerNinjaSword) 보존 — 외부 코드에서 `UpgradeToDaggerNinja()` API 호출 가능
- 닌자 모드 진입은 키 없음 (F8 디버그 제거). API 호출로만 (향후 인런 이벤트 등)

### 5. 닌자 SO

- `Sword_DaggerNinja.asset` 파일 그대로 보존
- 데이터 (transform/slashFrames 등) 모두 유지
- 진입 경로 없음 (F8 제거 + Q/휠 cycle 미포함)
- 향후 활용: WeaponUpgradeService.Instance.UpgradeToDaggerNinja() 외부 호출 (인런 이벤트, 아이템 픽업 등)

## 코드 변경 파일

1. `Assets/_Project/Scripts/Runtime/TestKhi/WeaponModeController.cs` — enum 확장 + WeaponUpgradeService 연결
2. `Assets/_Project/Scripts/Runtime/TestKhi/KhiWeaponUpgradeDebug.cs` — 키 처리 제거 (또는 컴포넌트 자체 비활성)

## 사용자 액션 (Unity Editor)

1. WeaponModeController Inspector 에서:
   - `Weapon Upgrade Service` 슬롯에 WeaponUpgradeService 컴포넌트 드래그
2. KhiWeaponUpgradeDebug 컴포넌트 제거 (prefab 모드에서) — 또는 enabled 체크 해제
3. (선택) Initial Mode 가 Sword 인지 확인

## 검증

1. Play 진입 → Q 또는 휠 cycle:
   - Sword → **Dagger** → Bow → Staff → Flamethrower → Sword …
2. Dagger 모드 진입 시:
   - 단검 sprite 보임 (Sword_Dagger SO 적용)
   - 보조 단검 (Weapon_Sub) 활성
   - 우클릭 = 텔레포트 (KhiDaggerTeleportController 자동 작동)
3. Dagger → Bow 전환:
   - 검/단검 sprite 모두 꺼짐
   - 보조 단검 (Weapon_Sub) 도 꺼짐 ✓ (두 자루 문제 해결)
4. F8/F9 키 입력 무시 (디버그 키 제거)
5. 닌자 SO (Sword_DaggerNinja.asset) Project 창에 그대로 존재

## 잠재 이슈

- Initial Mode = Dagger 두면 게임 시작 즉시 단검 모드. 가능. 다만 Sword default 권장
- WeaponUpgradeService 미연결 시 → ApplyMode 에서 null 체크 → 단검 모드 진입해도 SO 교체 안 됨. Inspector 슬롯 채워야 함
- Initial Mode 가 Dagger인 상태에서 게임 시작 시 WeaponUpgradeService.Awake 가 먼저 실행되어야 (DefaultExecutionOrder 조정 또는 ModeChanged 이벤트 보장)

## 후속 확장

- 인런 이벤트로 닌자 단검 활성: `WeaponUpgradeService.Instance.UpgradeToDaggerNinja()` 한 줄
- 무기 종류별 sprite 아이콘 UI (선택)
- 단검 → 닌자단검 진화 트리거 (특정 적 처치 등)

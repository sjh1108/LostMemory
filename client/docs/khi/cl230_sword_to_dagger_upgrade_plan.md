# CL230 — 일반 검 → 단검 업그레이드 시스템

## Context

현재 플레이어 무기는 `Sword_Default.asset` (WeaponData ScriptableObject) 하나만 존재하며, 런타임에 무기를 교체하거나 진화시키는 시스템이 없다. 인런(in-run) 진행 중 일반 검을 "단검"(빠르고 짧은 무기) 상태로 업그레이드할 수 있게 하고, 사용자가 추후 직접 트리거(아이템/이벤트)를 붙일 수 있도록 **API + 디버그 키**를 제공한다. 시각 차별화는 우선 `slashTint` 변경으로, 단검 전용 스프라이트는 후속 작업으로 분리.

**사용자 결정 사항**:
- 별도 WeaponData SO로 분리 (Sword_Dagger.asset)
- 콘셉트: 속도↑ 사거리↓ (DPS는 비슷)
- 이번 작업 범위: API 노출 + 디버그 키 (인런 트리거는 추후 사용자가 연결)
- 시각: 기존 스프라이트 재활용 + slashTint로 차별화

---

## 1. Sword_Dagger.asset 스탯

**모든 축에 일관 배수 0.60×** (time, range, per-hit damage). 결과적으로 DPS와 damage/area 밀도 보존.

| 항목 | Sword_Default | Sword_Dagger | 배수 |
|---|---|---|---|
| baseDamage | 10 | **6** | 0.60× |
| comboInputWindow | 0.6 | **0.45** | 0.75× (속도와 동일 배수는 너무 빡빡) |
| minimumChainInputDelay | 0.12 | **0.07** | ~0.60× |
| inputBufferDuration | 0.25 | **0.20** | 0.80× |
| frameInterval | 0.04 | **0.025** | ~0.60× |
| 1타 startup/active/recovery | 0.10/0.10/0.15 | **0.06/0.06/0.09** | 0.60× |
| 1타 hitboxOffset.x | 1.05 | **0.63** | 0.60× |
| 1타 hitboxSize | (1.7, 1.1) | **(1.02, 0.66)** | 0.60× |
| 2타 timing | 0.12/0.12/0.20 | **0.07/0.07/0.12** | ~0.60× |
| 2타 hitboxSize | (1.8, 1.2) | **(1.08, 0.72)** | 0.60× |
| 3타 timing | 0.20/0.20/0.30 | **0.12/0.12/0.18** | 0.60× |
| 3타 hitboxSize | (2.0, 1.8) | **(1.20, 1.08)** | 0.60× |
| damageMultiplier (전 step) | 1.0 / 1.1 / 1.5 | **동일** (baseDamage에서 흡수) | — |
| slashTint 1타 | (1,1,1,1) | **(0.55, 0.85, 1.0, 0.9)** | 차가운 시안 |
| slashTint 2타 | (1,1,1,1) | **(0.6, 0.9, 1.0, 0.9)** | 유사 톤 |
| slashTint 3타 | (1,1,1,1) | **(0.4, 0.7, 1.0, 1.0)** | 진한 청록 |
| slashFrames | (기존 4 GUID) | **동일 GUID 그대로 복붙** | 추후 교체 |

생성 위치: `Assets/_Project/ScriptableObjects/Weapon/Sword_Dagger.asset` (Unity 메뉴 `Create → LostMemory → Combat → WeaponData` 사용 권장)

> 디자이너가 차후 "단검 살짝 셈"으로 의도하면 `_baseDamage` 6 → 7만 올리면 +16% DPS.

---

## 2. 런타임 WeaponData 교체 API

### 2-1. `KhiMeleeComboController.SetWeaponData` 추가

`Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs`

```csharp
public event Action<WeaponData, WeaponData> WeaponDataChanged;

public void SetWeaponData(WeaponData next, bool finishCurrentSwing = true)
{
    if (next == null || next == weaponData) return;

    if (!finishCurrentSwing && _isAttacking)
        AbortCurrentAttack();

    WeaponData previous = weaponData;
    weaponData = next;

    // 다음 swing이 새 무기의 1타로 시작하도록 콤보 reset
    _nextComboStep = 1;
    _comboExpiresAt = -1f;
    ClearBufferedAttack();

    // KhiSlashAnimator는 자체 weaponData 필드 보유 → 명시 동기화 필수
    var slashAnim = GetComponent<KhiSlashAnimator>();
    if (slashAnim != null) slashAnim.SetWeaponData(next);

    WeaponDataChanged?.Invoke(previous, next);
}
```

### 2-2. `RunAttack` race condition 완화 (같은 PR 포함 권장)

`RunAttack` 시작부에서 `baseDamage`, `globalPostRotationOffset` 로컬 캡처. 코루틴 내부 active 루프(현 `.cs:237, 252`)가 매 frame `weaponData.BaseDamage`를 다시 읽기 때문에, 교체 직후 swing 중간부터 데미지가 점프하는 것 방지.

```csharp
// RunAttack 시작부
float capturedBaseDamage = weaponData.BaseDamage;
Vector2 capturedPostRotationOffset = weaponData.GlobalHitboxPostRotationOffset;
// 이후 루프에서 위 캡처값만 사용
```

### 2-3. `KhiSlashAnimator.SetWeaponData` 추가

`Assets/_Project/Scripts/Runtime/TestKhi/KhiSlashAnimator.cs`

```csharp
public void SetWeaponData(WeaponData next)
{
    if (next == null || next == weaponData) return;
    weaponData = next;
    // 재생 중 frame 코루틴은 캡처된 frames로 끝까지 진행 (자연스러움)
}
```

### 2-4. 자동 추종 확인

| 컴포넌트 | 추종 방식 | 추가 작업 |
|---|---|---|
| `KhiAttackVisualPresenter` (.cs:118) | 매 swing `_comboController.WeaponData` 호출 | 없음 |
| `KhiMeleeHitbox` (.cs:41) | controller가 인자 전달 | 없음 |
| `KhiWeaponPresenter` | WeaponData 미사용 | 없음 |
| `KhiSlashAnimator` (.cs:24, 141, 168, 205) | 자체 SerializeField | **명시 sync 필요 (위 2-3)** |

---

## 3. WeaponUpgradeService (신규)

`Assets/_Project/Scripts/Runtime/Combat/WeaponUpgradeService.cs`

```csharp
namespace LostMemory.Combat
{
    [AddComponentMenu("Lost Memory/Combat/Weapon Upgrade Service")]
    [DefaultExecutionOrder(-100)]
    public class WeaponUpgradeService : MonoBehaviour
    {
        [SerializeField] private WeaponData defaultSword;
        [SerializeField] private WeaponData daggerSword;
        [SerializeField] private KhiMeleeComboController targetController;

        public enum WeaponKind { Default, Dagger }
        public WeaponKind CurrentKind { get; private set; } = WeaponKind.Default;
        public event Action<WeaponKind, WeaponData> WeaponUpgraded;

        private static WeaponUpgradeService _instance;
        public static WeaponUpgradeService Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            if (targetController == null)
                targetController = FindFirstObjectByType<KhiMeleeComboController>(FindObjectsInactive.Include);
            if (defaultSword == null && targetController != null)
                defaultSword = targetController.WeaponData;
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        public bool UpgradeToDagger()    // 이미 단검이면 false (no-op, no event)
        public bool RevertToDefault()    // 이미 기본이면 false
        private bool ApplyWeapon(WeaponData next, WeaponKind kind)  // SetWeaponData 호출 + 이벤트 발화
    }
}
```

- **형태**: MonoBehaviour 싱글톤 (씬 부착). Inspector에서 SO 드래그 가능.
- **카탈로그**: 지금은 2개 SO 명시 필드. 추후 무기 5~10개로 늘면 `WeaponData[]` + enum index로 리팩토링.
- **외부 트리거**: `WeaponUpgradeService.Instance.UpgradeToDagger()` 한 줄로 호출 가능.

---

## 4. KhiWeaponUpgradeDebug (신규, DEV 가드)

`Assets/_Project/Scripts/Runtime/TestKhi/KhiWeaponUpgradeDebug.cs`

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace LostMemory.TestKhi
{
    [AddComponentMenu("Lost Memory/Test Khi/Khi Weapon Upgrade Debug")]
    public class KhiWeaponUpgradeDebug : MonoBehaviour
    {
        [SerializeField] private Key upgradeKey = Key.F9;
        [SerializeField] private Key revertKey = Key.F10;
        [SerializeField] private bool showOnGuiHint = true;
        // Update: F9 → UpgradeToDagger, F10 → RevertToDefault
        // OnGUI: "[Weapon] F9: Dagger / F10: Default (current: {kind})"
    }
}
#endif
```

- 키: **F9 / F10** (코드베이스 grep 결과 충돌 없음, F3 = 상태 오버레이와 무관)
- 시각: 좌상단 OnGUI 1줄 + 콘솔 로그
- 릴리스 빌드에서는 클래스 전체 컴파일 제외
- 패턴 참고: `KhiPlayerStateDebugOverlay.cs:1` 의 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 가드

---

## 5. 수정 / 생성 파일

### 신규 (3)
- `Assets/_Project/ScriptableObjects/Weapon/Sword_Dagger.asset`
- `Assets/_Project/Scripts/Runtime/Combat/WeaponUpgradeService.cs`
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiWeaponUpgradeDebug.cs`

### 수정 (2)
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs` — `SetWeaponData` 추가, `WeaponDataChanged` 이벤트, RunAttack baseDamage 캡처
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiSlashAnimator.cs` — `SetWeaponData` 추가

### 씬 작업 (Unity Editor)
- 씬에 GameObject 생성 (예: `_WeaponUpgrade`) → `WeaponUpgradeService` + `KhiWeaponUpgradeDebug` 부착
- Inspector에서 `defaultSword` ← Sword_Default, `daggerSword` ← Sword_Dagger 드래그
- `targetController`는 비워두면 Awake에서 자동 탐색

### 수정 불필요 (자동 추종 확인됨)
- `KhiMeleeHitbox.cs`, `KhiAttackVisualPresenter.cs`, `KhiWeaponPresenter.cs`, `WeaponModeController.cs`, `WeaponData.cs`

---

## 6. 검증 (Unity Play 모드)

1. **빌드 통과** — 컴파일 에러 없음
2. **기본 검 (F9 전)** — 좌클릭 콤보 기존 동작 그대로, OnGUI: `current: Default`
3. **F9 업그레이드** — 콘솔 `UpgradeToDagger -> True`, 슬래시 시안색, 속도/사거리 ~60%
4. **공격 중 F9 (race)** — 진행 swing은 기존 timing/damage로 마무리, 다음 swing부터 단검, 에러 없음, frame 튐 없음
5. **이중 F9 (no-op)** — 두 번째 호출 `False`, 시각 변화 없음
6. **F10 복귀** — 기본 검으로 원복
7. **활 모드(Q) 중 F9** — 데이터 교체는 정상, 검 모드 복귀 시 단검 적용
8. **SO 무결성** — Play 종료 후 Inspector에서 Sword_Dagger.asset 값 그대로 (mutation 없음)

(선택) EditMode 테스트: `WeaponUpgradeServiceTests.UpgradeToDagger_*`, `SetWeaponData_DuringAttack_*`

---

## 7. 위험 요소 / 주의점

- **콤보 중 교체 race** — RunAttack가 매 frame `weaponData.BaseDamage` 다시 읽음 → **§2-2 로컬 캡처로 해결** (같은 PR 포함)
- **KhiSlashAnimator 자체 weaponData 필드** — SetWeaponData에서 명시 sync (§2-3)
- **slashTint만으로 시각 차별화** — 콤보 속도/사거리 체감이 더 큼. 부족 시 후속 PR에서 단검 전용 sprite GUID 교체
- **WeaponData SO mutation** — 본 설계는 reference 교체만 (mutation 없음). 안전
- **씬 재로드** — 씬 부착이라 unload 시 destroy. in-run 도중 단검 → 다음 층 진입 시 reset됨. **후속 작업**: `RunState`/`PlayerSession`에 `CurrentWeaponKind` 저장 + 복원
- **디버그 키와 실제 트리거 공존** — 추후 아이템 트리거 추가 시 디버그 F9는 그대로 유지. 둘 다 같은 `UpgradeToDagger()` 호출, 두 번째는 no-op

---

## 8. PR 분할 권장

1. **PR-1 (코드)**: SetWeaponData API + WeaponUpgradeService + KhiWeaponUpgradeDebug + RunAttack baseDamage 캡처
2. **PR-2 (데이터)**: Sword_Dagger.asset 생성 + 씬에 service/debug 부착 + Inspector 연결, 디자이너 수치 튜닝
3. **PR-3 (시각)**: 단검 전용 sprite, slashTint 미세조정

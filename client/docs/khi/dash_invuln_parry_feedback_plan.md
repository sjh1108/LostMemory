# 대시 무적 / 패링 범위·피드백 — 구현 plan

> 사용자 의도: (1) 대시 무적 on/off, (2) 패링 성공 이펙트, (3) 패링 범위(시간+공간) 인스펙터 노출.
> (4) "패링 후 공격 안 되는 버그" 는 본 작업 *별도* 이지만, 동일 영역(KhiParry 상태 머신)에 보너스 안전장치 1줄 추가로 부분 완화 가능 — Optional 항목으로 포함.

## Context

CL-019 combat feel checklist 후속으로 다음 폴리시 3가지를 한 묶음으로 처리:

1. **대시 무적 (Dash i-frames)**: 현재 `KhiDashController` 에 무적 메커니즘이 *아예 없음* — 코드 주석 ([KhiDashController.cs:116](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDashController.cs)) 에서 "Cooldown/Feedback/무적 등 side effect 없음" 명시. 디자인 결정 — 대시 회피가 게임의 기본 방어 수단이라면 무적이 필요. on/off 토글 + duration 두 인스펙터 필드로 노출.

2. **패링 범위 인스펙터 노출 (시간 + 공간)**: 현재 시간 윈도우 `parryWindow=0.16s` 는 이미 [KhiParryController.cs:35](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs) 에 SerializeField. 공간 범위는 별도 GameObject 의 `KhiParryDamageOnTouch` 옆에 부착된 `Collider2D` 의 size — 두 군데를 오가야 함. 두 값을 KhiParryController 인스펙터 한 `[Header("Parry Range")]` 아래 묶어서 한 화면에서 튜닝 가능하도록.

3. **패링 성공 이펙트**: 기존 [KhiParryFeedbackPresenter.cs](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryFeedbackPresenter.cs) 가 이미 패링 피드백 전담 클래스로 존재. `SpawnSuccessSpark`/`PlaySuccessSfx` 메서드 있지만 직접 `Instantiate` + 수동 `Destroy` 패턴. 프로젝트 표준인 `VFXSpawner.SpawnAttached` 로 마이그레이션 + prefab/sfx SerializeField 노출 + 인스턴스 정책(중복 spawn 방지) 정리.

## 변경 사항

### Task 1 — `KhiDashController.cs`

추가 필드:
```csharp
[Header("Invulnerability (i-frames)")]
[SerializeField, Tooltip("대시 시작 시 일정 시간 무적 처리. KhiHitStunController 의 무적 패턴과 동일하게 TDE Health.DamageDisabled/Enabled 코루틴 사용.")]
private bool dashInvulnerabilityEnabled = true;

[SerializeField, Min(0f), Tooltip("무적 지속 시간 (초). dashInvulnerabilityEnabled=true 때만 적용.")]
private float dashInvulnerabilityDuration = 0.3f;

[SerializeField, Tooltip("대시 시 무적을 적용할 TDE Health 컴포넌트. Player 의 Health 드래그.")]
private MoreMountains.TopDownEngine.Health health;
```

`DashStart()` 안 `base.DashStart()` 호출 직후:
```csharp
if (dashInvulnerabilityEnabled && health != null && dashInvulnerabilityDuration > 0f)
{
    health.DamageDisabled();
    StartCoroutine(health.DamageEnabled(dashInvulnerabilityDuration));
}
```

> 패턴 참고: [KhiHitStunController.cs:246-251](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiHitStunController.cs), [KhiParryController.cs:271-272](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs). 둘 다 같은 API 사용 중.

### Task 2 — `KhiParryController.cs`

기존 `[Header("Timings")]` 와 별개로 새 헤더 추가, 시간/공간 둘 다 한 곳에 표시:

```csharp
[Header("Parry Range")]
[SerializeField, Tooltip("패링 입력 유효 시간 (초). 짧을수록 어려움.")]
private float parryWindow = 0.16f;   // 기존 Timings 에서 이동

[SerializeField, Tooltip("패링 판정 콜라이더 (Player 옆 KhiParryDamageOnTouch 의 Collider2D). 이 콜라이더의 size/offset 으로 공간 범위 조정.")]
private Collider2D parryHitbox;

[SerializeField, Tooltip("PlayMode 중 parryHitboxScale 로 실시간 size 배수. 1=원래 콜라이더 size, 2=2배. OnValidate/Update 가 적용.")]
private float parryHitboxScale = 1f;
```

처리:
- `parryWindow` 는 기존 Timings 헤더에서 Parry Range 헤더로 옮김 (의미 충돌 없음, 직렬화 호환)
- `parryHitbox` 는 단순 reference — 사용자가 인스펙터에서 해당 Collider2D 의 size 도 같이 표시되어 보임 (Unity 인스펙터 inline expand)
- `parryHitboxScale` 은 *선택* — collider 자체 size 를 매 프레임 곱해서 실시간 튜닝 가능. OnValidate 에서도 적용. 1.0 default 면 무영향
- 새 OnValidate / `ApplyHitboxScale()` 작은 메서드:
  ```csharp
  private Vector2 _baseHitboxSize;
  private void OnValidate() { CacheBaseHitboxSize(); ApplyHitboxScale(); }
  private void Awake() { CacheBaseHitboxSize(); ApplyHitboxScale(); }
  // CacheBaseHitboxSize: BoxCollider2D 이면 size 저장
  // ApplyHitboxScale: _baseHitboxSize * parryHitboxScale 적용
  ```

> 만약 hitbox 가 BoxCollider2D 가 아닌 CircleCollider2D 라면 `radius` 처리도 같은 패턴. 둘 다 지원하는 작은 분기 추가.

### Task 3 — `KhiParryFeedbackPresenter.cs`

기존 `SpawnSuccessSpark` (직접 Instantiate) 와 `PlaySuccessSfx` (PlayOneShot) 를 프로젝트 표준 패턴으로 정렬:

추가 SerializeField (이미 일부 있을 수 있음 — 정리):
```csharp
[Header("Success VFX")]
[SerializeField, Tooltip("패링 성공 시 player 에 부착되어 spawn 될 prefab. ParticleSystem/SpriteRenderer 포함. 빈 칸이면 spawn 안 함.")]
private GameObject successVFXPrefab;
[SerializeField, Tooltip("자동 destroy 시간 (초). 0 이면 prefab 자체가 처리.")]
private float successVFXAutoDestroy = 0.6f;
[SerializeField, Tooltip("부착 시 local offset.")]
private Vector3 successVFXLocalOffset = Vector3.zero;

[Header("Success SFX")]
[SerializeField] private AudioClip successSfx;
[SerializeField, Range(0f, 1f)] private float sfxVolume = 0.6f;
```

`SpawnSuccessSpark` 내부를 `VFXSpawner.SpawnAttached(successVFXPrefab, transform, successVFXLocalOffset, successVFXAutoDestroy)` 로 교체. `PlaySuccessSfx` 는 `AudioSource.PlayClipAtPoint(successSfx, transform.position, sfxVolume)` 로 통일 — RelicEffectRegistry 동일 패턴.

활성 인스턴스 중복 방지: 짧은 effect 라 매 spawn 새 인스턴스로 OK (RelicEffectRegistry 의 ShieldVFX 처럼 장시간 buff 가 아님).

> ParrySucceeded 이벤트 구독은 *이미* Presenter 가 함 (또는 KhiParryController 가 직접 호출). 코드 확인 후 둘 중 하나로 일관화. 둘 다 호출되면 spark 두 번 spawn 됨.

### Task 4 (Optional 보너스) — 패링 후 공격 안 되는 버그 부분 완화

[KhiParryController.cs:104-110](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs) 의 LateUpdate 안전장치가 *Idle 상태* 에서만 작동. ParryWindow / FailureRecovery / Cooldown 에서 비정상 종료 시 `_hasCachedPermits=true` 잔존 → `meleeCombo.ExternalBlock=true` 영구 lock.

조건부 보강 — 상태가 비-Idle 인데 `_stateEndTime` 이 큰 폭으로 초과 됐다면 `ForceIdle()` 강제 호출:

```csharp
private void LateUpdate()
{
    if (_state != KhiParryState.Idle && Time.time > _stateEndTime + 2f)
    {
        // 정상 흐름이면 TickState 에서 이미 전이됐어야. 2초 초과는 어떤 원인(timeScale=0, 코루틴 정지 등)으로 stuck.
        Debug.LogWarning($"[KhiParryController] state stuck ({_state}) >{Time.time - _stateEndTime:F1}s past end. ForceIdle.");
        ForceIdle();
    }
    // 기존 Idle + cached permits 복원 로직 유지
    ...
}
```

리스크: 정상 상태가 길어지는 케이스 (예: 디자인상 의도된 긴 cooldown) 가 있으면 false-positive. 2초 threshold 는 보수적. 본 작업의 명시 범위가 아니라 *Optional* — 구현 동의 시 추가.

## 수정 파일

- `LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDashController.cs` (Task 1)
- `LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs` (Task 2, Optional Task 4)
- `LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryFeedbackPresenter.cs` (Task 3)

## Unity Editor 작업 (선행 / 검증용)

1. **Player 프리팹** 또는 PlayMode 중 Player 인스턴스의 `KhiDashController`:
   - `health` 필드에 같은 GameObject 의 TDE `Health` 컴포넌트 드래그
   - `dashInvulnerabilityEnabled = true`, `dashInvulnerabilityDuration = 0.3` (디자인에 맞게 조정)
2. `KhiParryController`:
   - `parryHitbox` 필드에 KhiParryDamageOnTouch GameObject 의 `Collider2D` 드래그
   - `parryHitboxScale` 은 1.0 유지 (필요 시만 변경)
3. `KhiParryFeedbackPresenter`:
   - `successVFXPrefab` 에 [Prefabs/Effect/Star_VFX.prefab](LostMemory/Assets/_Project/Prefabs/Effect/Star_VFX.prefab) 또는 [Flash_VFX.prefab](LostMemory/Assets/_Project/Prefabs/Effect/Flash_VFX.prefab) 드래그 (디자인 선호)
   - `successSfx` 에 AudioClip 드래그 (있다면)
   - `successVFXAutoDestroy = 0.6`, `sfxVolume = 0.6` 정도

## Verification

1. **대시 무적**
   - 적 옆에서 대시 발동 → 무적 0.3초 동안 데미지 받지 않음 (`health.CurrentHealth` 유지)
   - `dashInvulnerabilityEnabled = false` 토글 → 같은 상황에서 데미지 받음
   - `dashInvulnerabilityDuration = 0` → 무적 미적용
2. **패링 범위**
   - `parryHitboxScale = 2` → 적 공격이 더 멀리서도 패링 가능
   - `parryWindow = 0.05` (짧게) → 정확한 타이밍 안 맞으면 실패
3. **패링 성공 이펙트**
   - 패링 성공 직후 VFX prefab 이 player 위치에 spawn + 0.6초 후 자동 사라짐
   - SFX 한 번 재생
4. **(Optional) 안전장치**
   - 인위적으로 `Time.timeScale = 0` → 패링 입력 → timeScale 복원 → 2초 후 `[KhiParryController] state stuck ... ForceIdle.` 경고 + 공격 다시 됨

## Known Issues / 추후 작업

- **본 plan 의 안전장치(Task 4)는 임시 완화**. 진짜 fix 는 *왜 timeScale=0 또는 코루틴 정지가 발생하는지* 추적 필요 — 별도 진단 세션. RewardPanel 떠있을 때 timeScale=0 인 상태에서 패링 입력 처리되면 그게 원인일 가능성. 본 plan 의 보너스는 stuck 회복만 보장.
- `KhiParryFeedbackPresenter` 의 기존 링 펄스 / 플래시 자체 구현은 유지. 본 plan 의 VFX 마이그레이션은 `SpawnSuccessSpark` 라인만 대상.
- 멀티플레이 호스트 권위 — Player 본인 시점에서만 spawn 하면 충분 (현재 패턴과 동일).

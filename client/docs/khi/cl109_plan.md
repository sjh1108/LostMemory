# CL-109 Wave D — RelicEffectRegistry 통합 + IRelicEffectAuthority 분리 + 10종 통합 검증

## Context

CL-107 / CL-108 이 *임시 어댑터* `RelicEffectApplier` 를 통해 MVP 10 종 + 회복약 2 종의 효과를 wiring 했다. 본 CL 의 목적은:

1. **임시 어댑터 → 정식 Registry 로 격상** — `RelicEffectApplier` 를 `RelicEffectRegistry` 로 재명명/리팩토링하여 *효과 표 (effect table)* 의 정식 진입점으로 확립
2. **IRelicEffectAuthority 인터페이스 분리** — `RunManager.IsAuthority` 와 동일한 future-proof 패턴. 현재 single-player 라 항상 true 반환하지만, 미래 멀티 도입 시 한 줄만 host 권위 분기로 전환 가능
3. **10 종 통합 검증** — CL-107 의 6 종 + CL-108 의 4 종이 동시에 활성화된 상태에서 회귀 시나리오 1 회 통과

**중요**: 본 CL 은 *동작 변경 0* 가 목표. 현재 single-player 환경에서 IsAuthority 항상 true 라 effect 적용은 CL-107/108 와 100% 동일. 리팩토링 + 미래 대비 + 회귀 검증.

---

## 결정사항

1. **Registry 위치** = 기존 RelicEffectApplier 와 동일하게 *Player root* 부착. PlayerRelicInventory 와 같은 lifetime (Run 단위). RunManager 로 옮기지 않는 이유:
   - PlayerStatModifierContainer / Health / KhiDashController / KhiParryController 등 모든 효과 적용 대상이 Player 컴포넌트
   - PlayerRelicInventory 가 Player 부착 → OnRelicAcquired 이벤트 구독도 동일 위치가 자연스러움
   - RunManager 는 *런 단위 상태 머신* 책임. Registry 책임 분리 유지
2. **Registry 이름 = `RelicEffectRegistry`**. Applier 클래스 자체 제거 (rename).
   - 마이그레이션 안전: CL-107/108 plan 이 *plan 단계* 라면 코드는 처음부터 `RelicEffectRegistry` 로 작성. 만약 CL-107/108 가 이미 구현된 후 본 CL 진입이면 rename 1 회
3. **IRelicEffectAuthority 인터페이스**:
   ```csharp
   public interface IRelicEffectAuthority
   {
       bool IsAuthority { get; }
   }
   ```
   - `RunManager.IsAuthority` 와 동일 시그니처. 일관성 우선
4. **기본 구현 = `LocalRelicEffectAuthority`** — 항상 true. 미래 `NetworkRelicEffectAuthority` 추가 시 `RunManager.IsAuthority` 위임:
   ```csharp
   public sealed class LocalRelicEffectAuthority : IRelicEffectAuthority
   {
       public bool IsAuthority => true;
   }

   // 미래 (CL-XXX 멀티 도입 시):
   public sealed class NetworkRelicEffectAuthority : IRelicEffectAuthority
   {
       public bool IsAuthority => RunManager.Instance != null && RunManager.Instance.IsAuthority;
       // 또는 NetworkBehaviour.IsHost 등 멀티 framework 시그니처
   }
   ```
5. **Authority 분기 위치** = `RelicEffectRegistry.HandleAcquired` 의 *최상단* 단일 게이트:
   ```csharp
   private void HandleAcquired(RelicData relic)
   {
       if (!_authority.IsAuthority) return;
       switch (relic.EffectType) { /* ... */ }
   }
   ```
   - 각 case 마다 게이트 X. 단일 진입점 게이트가 단순. 효과별 권위 차이가 필요해지면 그때 case 별 분기
   - ParrySucceeded / OnDashEnded / OnConsumableUsed 등 *이벤트 핸들러* 도 동일 패턴 적용
6. **Registry 가 직접 의존하는 컴포넌트** (변경 없음, CL-107/108 와 동일):
   - PlayerStatModifierContainer (multiplier 등록)
   - PlayerShield (보호막 부여)
   - PlayerHealing (회복 호출 — 회복약은 PlayerRelicInventory.TryUseOrAdd 가 직접 호출하지만, Registry 는 PlayerHealing 참조 보유 안 해도 됨)
   - Health (전투 북 조건부 평가용 — playerHealth.CurrentHealth/MaximumHealth)
   - KhiDashController (OnDashEnded 구독)
   - KhiParryController (ParrySucceeded 구독)
7. **Run 종료 시 정리** — `PlayerRelicInventory.Clear()` 호출 시점에 Registry 도 모든 modifier 제거. 두 가지 방식:
   - (a) Registry 가 PlayerRelicInventory.OnCleared 이벤트 구독 → container.ClearAll() + PlayerShield 리셋
   - (b) RunManager 의 RunState 전이 (None) 구독 → 정리
   - **권장 (a)**: 인벤토리 lifetime 과 결합. RunManager 와의 직접 의존 회피
   - 신규 이벤트 `PlayerRelicInventory.OnCleared` 추가 필요
8. **GlobalEvent 시스템 미활용** — `Relic.Acquired`, `Relic.EffectApplied` 키는 정의만 있고 publisher/subscriber 0. 본 CL 도 발화하지 않음. 미래 GameEventBus 구현 ticket 에서 일괄 도입
9. **회복약 (HealConsumablePercent) 처리 위치** — CL-108 plan 에서 `PlayerRelicInventory.TryUseOrAdd` 가 `PlayerHealing.UseConsumable` 직접 호출. Registry 는 *no-op* (case 만 비워둠). 즉 회복약은 Registry 우회. 이유: 회복약은 *즉시 1회 효과* 라 modifier 등록 의미 없음

---

## 핵심 파일

### 신규 (1 인터페이스 + 1 기본 구현)

| 파일 | 역할 |
|---|---|
| `Relics/IRelicEffectAuthority.cs` | `bool IsAuthority { get; }` 인터페이스 |
| `Relics/LocalRelicEffectAuthority.cs` | 항상 true 반환하는 기본 구현 (single-player) |

### 수정 / 재명명

| 변경 | 변경 |
|---|---|
| `Relics/RelicEffectApplier.cs` → `Relics/RelicEffectRegistry.cs` | rename + IRelicEffectAuthority 주입 + HandleAcquired/이벤트 핸들러 최상단에 IsAuthority 게이트 |
| `Relics/PlayerRelicInventory.cs` | `event Action OnCleared` 추가, `Clear()` (L61) 끝에 발화 |

### 참조 (수정 없음, future-proof 패턴 재사용)

- `Stage/RunManager.cs:45` — `IsAuthority => true` 동일 패턴 참고
- `Combat/PlayerStatModifierContainer.ClearAll()` — Registry 가 OnCleared 핸들러에서 호출
- `Combat/PlayerShield` (CL-108) — Registry 가 OnCleared 시 보호막 리셋

---

## 작업 단계 (구현 순서)

### Step 1 — IRelicEffectAuthority 인터페이스 + LocalRelicEffectAuthority (10분)

`Relics/IRelicEffectAuthority.cs`:
```csharp
namespace LostMemory.Relics
{
    /// <summary>
    /// 유물 효과 적용 권위. CL-109 future-proof.
    /// 현재 single-player: LocalRelicEffectAuthority (항상 true).
    /// 미래 멀티 도입: NetworkRelicEffectAuthority — host 만 true.
    /// 패턴: RunManager.IsAuthority 와 동일.
    /// </summary>
    public interface IRelicEffectAuthority
    {
        bool IsAuthority { get; }
    }
}
```

`Relics/LocalRelicEffectAuthority.cs`:
```csharp
namespace LostMemory.Relics
{
    public sealed class LocalRelicEffectAuthority : IRelicEffectAuthority
    {
        public bool IsAuthority => true;
    }
}
```

### Step 2 — RelicEffectApplier → RelicEffectRegistry rename (15분)

파일 이름 + 클래스 이름 변경. Unity .meta 파일 동기화. namespace 유지.

### Step 3 — Authority 게이트 추가 (15분)

`RelicEffectRegistry.cs` 안:
```csharp
private IRelicEffectAuthority _authority = new LocalRelicEffectAuthority();

// 또는 SerializeField 로 주입 받게 하려면 ScriptableObject 또는 MonoBehaviour 어댑터 필요.
// 본 CL 은 코드 new 로 충분. 미래 NetworkAuthority 등장 시 주입 패턴 도입.

private void HandleAcquired(RelicData relic)
{
    if (!_authority.IsAuthority) return;
    // 기존 switch 그대로
}

private void HandleParrySuccess()
{
    if (!_authority.IsAuthority) return;
    foreach ((float mag, float dur, RelicData relic) in _onParrySuccessSubscriptions)
    {
        playerShield.GrantShield(playerHealth.MaximumHealth * mag, dur);
    }
}

private void HandleDashEnded()
{
    if (!_authority.IsAuthority) return;
    foreach ((float mag, float dur, RelicData relic) in _onDashEndSubscriptions)
    {
        container.AddTimed(StatId.MoveSpeed, mag, dur, relic);
    }
}

private void HandleEnemyKilledByPlayer(KhiAttackRequest req, AttackStepData step, Health killed)
{
    if (!_authority.IsAuthority) return;
    foreach ((float mag, float dur, RelicData relic) in _onKillSubscriptions)
    {
        container.AddTimed(StatId.AttackSpeed, mag, dur, relic);
    }
}
```

### Step 4 — PlayerRelicInventory.OnCleared 이벤트 (5분)

`PlayerRelicInventory.cs`:
```csharp
public event Action OnCleared;

public void Clear()
{
    _ownedRelics.Clear();
    OnCleared?.Invoke();
}
```

### Step 5 — RelicEffectRegistry 가 OnCleared 구독 (10분)

`RelicEffectRegistry.cs`:
```csharp
private void OnEnable()
{
    inventory.OnRelicAcquired += HandleAcquired;
    inventory.OnConsumableUsed += HandleConsumableUsed;  // CL-108 신규 (no-op 가능)
    inventory.OnCleared += HandleRunCleared;
    parryController.ParrySucceeded += HandleParrySuccess;
    dashController.OnDashEnded += HandleDashEnded;
    meleeCombo.EnemyKilledByPlayer += HandleEnemyKilledByPlayer;
}

private void OnDisable()
{
    if (inventory != null)
    {
        inventory.OnRelicAcquired -= HandleAcquired;
        inventory.OnConsumableUsed -= HandleConsumableUsed;
        inventory.OnCleared -= HandleRunCleared;
    }
    if (parryController != null) parryController.ParrySucceeded -= HandleParrySuccess;
    if (dashController != null) dashController.OnDashEnded -= HandleDashEnded;
    if (meleeCombo != null) meleeCombo.EnemyKilledByPlayer -= HandleEnemyKilledByPlayer;
}

private void HandleRunCleared()
{
    if (!_authority.IsAuthority) return;
    container.ClearAll();
    if (playerShield != null) playerShield.ClearShield();  // 보호막도 리셋 (PlayerShield 에 public ClearShield 추가 필요)
    _onParrySuccessSubscriptions.Clear();
    _onDashEndSubscriptions.Clear();
    _onKillSubscriptions.Clear();
}
```

### Step 6 — PlayerShield.ClearShield 공개화 (5분)
CL-108 의 PlayerShield 의 `private void ClearShield()` 를 `public void ClearShield()` 로 변경 (또는 별도 public Reset() 메서드 추가).

### Step 7 — RunManager 통합 검증 (수동)

`RunManager.CloseResulting()` (L138) 가 호출되면:
1. RunStateMachine 이 None 으로 전이
2. `UnsubscribeAllRoomControllers()` 호출
3. 별도로 PlayerRelicInventory.Clear() 가 호출되는지 확인

> **확인 필요**: 현재 RunManager 가 PlayerRelicInventory.Clear() 를 호출하는지. 미호출이면 본 CL 에서 추가 (RunManager.CloseResulting 끝에 1 줄):
> ```csharp
> if (playerRelicInventory != null) playerRelicInventory.Clear();
> ```

---

## 검증 (PlayMode, 통합 시나리오 1 + 회귀 4)

### 사전 준비
- 10 종 유물 모두 디버그 진입점으로 즉시 TryUseOrAdd 가능한 메뉴
- PlayerStatModifierContainer logModifierChanges = true
- PlayerShield logShieldEvents = true
- PlayerHealing logHeals = true
- 적 처치 가능한 일반 적 1 + 패링 가능한 적 1 + Boss 1 (RunCleared 검증)

### 시나리오 1 — 10 종 동시 보유 + 종합 동작 (메인 통합 검증)

**Step A — 10 종 등록**
1. 6 종 (CL-107): 전사의끈, 분쇄의팔찌, 전투북, 붉은송곳니, 바람깃털, 수호의파편 모두 TryUseOrAdd
2. 4 종 (CL-108): 반격의표식, 철의깃, 질풍장화, 추적자의망토 모두 TryUseOrAdd
3. **기대 로그 (10 줄)**: 각 등록마다 `[StatModifier] AddPermanent ...` (영구 6 종) 또는 `[StatModifier] AddConditional ...` (전투북) 또는 *no-op* (반격/추적자/붉은송곳니 — 이벤트 대기)

**Step B — Multiplier baseline 검증 (디버그 메뉴 "Log all totals")**
- AttackPower = **1.05** (전사의끈) — HP 만피 시 +전투북 0.10 → 1.15
- FinisherDamage = **1.20** (분쇄의팔찌)
- AttackSpeed = **1.000** (붉은송곳니는 적 처치 트리거)
- MoveSpeed = **1.08** (바람깃털)
- MaxHealth = **1.12** (수호의파편 — Health.MaximumHealth 도 *1.12 적용 확인)
- HealReceived = **1.25** (철의깃)
- DashCooldown = **0.88** (질풍장화)

**Step C — Combat scenario**
1. 적 1 마리 처치 → 붉은송곳니 발동 → AttackSpeed = 1.120 (3 초간), 동시에 step durations 단축 체감
2. 즉시 3 타 콤보 → 3 타 데미지 = base × stepMul × 1.15 × 1.20 (AttackPower + FinisherDamage)
3. 패링 성공 → 반격의표식 발동 → `[PlayerShield] Grant {maxHp*0.08*1.12=원래maxHp*0.0896}` (수호의파편으로 maxHp 12% 증가 적용된 값)
4. 다음 적 공격 → 보호막 흡수
5. 대시 → 88% cooldown + 종료 후 추적자의망토 발동 → MoveSpeed = 1.08 + 0.20 = **1.28** (2 초간)

**Step D — 만료 동기 검증**
- 붉은송곳니 (3 초) + 추적자의망토 (2 초) + 반격의표식 (3 초) 동시 활성 후 자연 만료
- "Log all totals" 로 만료 후 baseline 복귀 확인

### 시나리오 2 — Authority 게이트 (수동 toggle)
1. 디버그용으로 RelicEffectRegistry 의 `_authority` 를 임시 `class FalseAuthority : IRelicEffectAuthority { public bool IsAuthority => false; }` 로 교체
2. 유물 TryUseOrAdd → **기대**: container 에 modifier 등록 안 됨, ParrySucceeded 발화돼도 Shield 안 부여됨
3. true 복귀 후 정상 동작 확인
4. 본 검증은 *코드 수동 교체* 필요. 별도 SerializeField 로 빼두면 Inspector toggle 가능 (선택)

### 시나리오 3 — Run 종료 시 정리
1. 10 종 등록 + 보호막 활성 + 임시 modifier 활성
2. RunManager.CloseResulting() 호출 (또는 보스 클리어 → 결과 화면 닫기)
3. **기대**:
   - PlayerRelicInventory.OwnedRelics = empty
   - PlayerStatModifierContainer 의 모든 modifier 제거 (`[StatModifier] Cleared all modifiers.`)
   - PlayerShield.CurrentShield = 0
   - 다음 런 시작 시 baseline 1.000 / 1.000 / ... 복귀

### 시나리오 4 — 이벤트 누수 회귀
1. Player GameObject 비활성화 → Re-활성화 (Disable/Enable Component 또는 SetActive)
2. **기대**: OnDisable 의 unsubscribe 가 누수 없이 동작. Re-enable 후에도 정상 작동

### 시나리오 5 — Source 기반 제거 (RemoveBySource)
1. 전사의끈 + 바람깃털 등록
2. 디버그로 `container.RemoveBySource(전사의끈)` 호출
3. **기대**: AttackPower = 1.000 / MoveSpeed = 1.080 (전사의끈만 제거, 바람깃털 유지)

---

## 완료 기준

- [ ] Unity 컴파일 통과
- [ ] 신규 2 파일 (IRelicEffectAuthority + LocalRelicEffectAuthority)
- [ ] RelicEffectApplier → RelicEffectRegistry rename
- [ ] PlayerRelicInventory.OnCleared 이벤트 추가 + Registry 가 구독
- [ ] HandleAcquired + 모든 이벤트 핸들러 최상단 IsAuthority 게이트
- [ ] 시나리오 1 (10 종 동시) 통과 — 핵심
- [ ] 시나리오 2 ~ 5 통과
- [ ] CL-107 / CL-108 의 기존 시나리오 모두 회귀 통과 (총 7 + 8 = 15 시나리오)

---

## 위험 / 결정 보류

1. **Authority 주입 방식** — 현재 plan: `_authority = new LocalRelicEffectAuthority()` (코드 new). Unity Inspector toggle 못 함. 시나리오 2 의 false 검증 위해 SerializeField 또는 디버그 ContextMenu 로 toggle 가능하게 할지 결정 필요. 본 plan: 코드 new 로 가고, 검증은 1 회성 코드 수정
2. **NetworkAuthority 구현 시점** — 본 CL 은 인터페이스만 도입. 실제 NetworkRelicEffectAuthority 는 멀티 framework (Mirror/Netcode/Photon) 도입 ticket 에서. 인터페이스 시그니처 (`IsAuthority`) 가 충분히 단순해서 미래 변경 비용 낮음
3. **GlobalEvent 발화 안 함** — `Relic.Acquired`, `Relic.EffectApplied` 키 정의는 있지만 publisher 없음. 본 CL 에서 발화하면 *수신자 0* 인 죽은 이벤트가 됨. 미래 GameEventBus 구현 ticket 에서 일괄 도입
4. **OnCleared 시 PlayerShield 리셋** — Step 6 의 ClearShield 공개화. PlayerShield 가 단독으로 만료 처리 잘하지만, Run 종료 시 *즉시* 리셋이 안전. PlayerShield API 확장으로 처리
5. **회복약 효과 위치 모호** — Registry 의 HealConsumablePercent case 는 no-op (PlayerRelicInventory.TryUseOrAdd 가 직접 PlayerHealing.UseConsumable 호출). Registry 의 책임은 *modifier 등록 + 시간/조건부 효과 발현* 만. 회복약은 *1회성 즉시 효과* 라 Registry 영역 외. plan doc 에 명시
6. **수호의파편 + 반격의표식 합산** — 시나리오 1 Step C 에서 보호막 부여 시 maxHp = 원래maxHp × 1.12. 즉 수호의파편이 *먼저 적용된 상태에서* 반격의 표식이 그 maxHp 기준 8% 부여. 합산 정합성. 만약 수호의파편이 *나중에* 등록되면 이미 부여된 보호막은 옛날 maxHp 기준이라 작아짐. 본 plan: 등록 시점 maxHp 기준 채택 (직관적). 정책 이슈 시 기획 확정
7. **AttackStepData SO 가정 (CL-107 공유 위험)** — CL-107 의 in-place 변경 금지 결정이 본 CL 에도 그대로 적용. Step 4 의 AttackSpeed local 변수 패턴 회귀 검증
8. **Player prefab 부착 누락 회귀** — Inspector wiring 누락 시 Registry 가 null reference. Awake 에서 명시적 null 체크 + Debug.LogError 추천

---

## 후속 CL 인터페이스

| CL | 본 CL 산출물 활용 |
|---|---|
| **CL-110** (보상 UI ↔ 방 클리어 연동) | PlayerRelicInventory.OnRelicAcquired 이벤트는 이미 존재. RewardController 가 Reward 표시 결과 처리 후 동일 경로 트리거. Registry 는 무관 |
| **멀티 framework 도입 ticket (미정)** | NetworkRelicEffectAuthority 신설 → RelicEffectRegistry 의 `_authority` 만 교체. 코드 변경 1 줄 + Inspector wiring |
| **GlobalEvent 시스템 구현 ticket (미정)** | Registry 의 IsAuthority=true 분기 끝에 `GameEventBus.Publish(GameEventKeys.RelicEffectApplied, ...)` 한 줄 추가. UI/VFX 가 구독 |
| **랜덤박스 메커니즘 ticket (미정)** | Registry 의 None case 에 RelicEffectType.None & IsConsumable & DisplayName=="랜덤박스" 분기 추가. 또는 별도 RandomBoxHandler 컴포넌트 |

---

## 참고 — explore 검증 결과

- `RunManager.cs:45` — `public bool IsAuthority => true;` ✅. 동일 시그니처 IRelicEffectAuthority 채택
- `RunManager.cs:118` — `if (!IsAuthority) return;` 패턴 ✅. RelicEffectRegistry 도 동일 패턴
- 멀티플레이 framework — manifest.json 에 Mirror/Netcode/Photon 등 0건 확인 ✅. NetworkBehaviour 등 키워드 0건. **현재 단계는 인터페이스만 신설**
- `PlayerRelicInventory.cs:61` — `Clear()` 메서드 존재. OnCleared 이벤트만 추가하면 됨
- `global-event-keys-and-hook-points.md:219-220` — `Relic.Acquired`, `Relic.EffectApplied` 키 정의만 있고 GameEventBus 미구현. 본 CL 발화 안 함
- `RunManager.CloseResulting()` (L138) — Run 종료 진입점. PlayerRelicInventory.Clear() 호출 추가 여부 확인 필요 (현재 코드에 없으면 본 CL 에서 추가)

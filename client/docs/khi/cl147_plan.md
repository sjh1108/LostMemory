# CL-147 — 타로 시스템 프레임워크 + 메이저 아르카나 8장

## Context

Epic S Phase 3의 마지막 ticket. **Phase 3 마무리 (P2, 5점)**.

CL-146에서 placeholder 만 둔 `TarotSystem` 본체 신설.

기존 hook (확인 완료):
- ✅ `RelicTag.Tarot` (line 35, RelicTag.cs)
- ✅ `RelicEffectType.TarotProc` (line 42, RelicEffectType.cs)
- ✅ `RoomEnteredPayload` 이벤트 (RoomEntryRuntimeEvents.cs) — 방 진입 시점 hook 가능
- ⏳ CL-146의 SetEffectApplicator 의 `TarotProc` case 가 LogWarning 만 함 → 본 CL에서 활성화

회의록 컨셉:
- 메이저 아르카나 **8장** 발췌 (P2 시스템)
- "타로 발동 확률" (TarotProc) → 방 진입 시 dice roll
- "타로 카드 효과 +N%" / "두 배" → **신규 EffectType 1개 추가** (`TarotEffectMultiplier`)

items_draft 의 타로 관련 6개 아이템이 본 CL을 가정한 hook을 가짐:
- #57 타로 카드: 별 — 행운 +1, 타로 발동 +5%
- #58 점성술사의 별자리 — 행운 +1, 타로 카드 효과 +10%
- #59 부의 신탁 — 골드 +10%, 타로 발동 +5%
- #62 운명의 카드 — 행운 +3, 타로 카드 효과 +25%
- #64 운명의 수레바퀴 — 행운 +5, 타로 카드 효과 두 배
- #65 별의 운명 (전설) — 행운 +7, 타로 카드 효과 두 배

---

## 결정사항 (사용자 확정)

| # | 항목 | 선택 |
|---|---|---|
| 1 | 카드 8종 | 별 / 죽음 / 탑 / 연인 / 은둔자 / 운명의수레바퀴 / 바보 / 마법사 |
| 2 | 발동 트리거 | **방 진입 시 1회 추첨** |
| 3 | 효과 지속 | **해당 방에서만** |
| 4 | TarotProc magnitude | **확률 % (0.05 = 5%)** |
| 5 | 시각 | 텍스트 + placeholder 색상 (MVP) |

### 신규 enum 값 1개 추가

```csharp
// RelicEffectType.cs 에 추가
TarotEffectMultiplier,   // 카드 효과 강도 % (0.10 = +10%, 1.0 = +100% = 두 배)
```

→ 현재 31개 enum + 1 = 32개.

→ #58/#62 magnitude 0.10/0.25, #64/#65 magnitude 1.0.

---

## 8장 카드 효과 (확정 디폴트)

각 카드 효과는 "해당 방" 한정. `TarotEffectMultiplier` 적용 가능한 항목은 (×) 표시.

| 카드 | 효과 | 강도 (×배율) |
|---|---|---|
| **별 (The Star)** | 다음 보상 카드 +1장 (3장 → 4장) | 고정 +1 |
| **죽음 (Death)** | 현재 방 모든 적 즉발 데미지 (현재 HP의 50%) | 50% × (1 + mul) |
| **탑 (The Tower)** | 현재 방 모든 적 2초 스턴 | 2.0초 × (1 + mul) |
| **연인 (The Lovers)** | 미소녀 임시 +1 (해당 방 끝까지) | 고정 +1 |
| **은둔자 (The Hermit)** | 다음 방 적 수 30% 감소 | 30% × (1 + mul) |
| **운명의 수레바퀴** | 랜덤 보유 세트 1단계 일시 부스트 (해당 방) | 1단계 × (1 + mul) → 정수 올림 |
| **바보 (The Fool)** | 다른 카드 2장을 추첨해서 동시 발동 (와일드) | (재귀) |
| **마법사 (The Magician)** | 임시 공격력 +30% (해당 방) | 30% × (1 + mul) |

> **바보 재귀 가드**: 바보가 바보를 뽑지 않도록 재추첨. 깊이 1 (서브 카드의 효과만 발동, 그 카드들이 또 바보면 무시).

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Tarot/TarotSystem.cs` | 추첨 + 발동 + RoomEntered 구독 |
| `Assets/_Project/Scripts/Runtime/Tarot/ITarotCard.cs` | 카드 효과 인터페이스 |
| `Assets/_Project/Scripts/Runtime/Tarot/Cards/StarCard.cs` | 별 |
| `Assets/_Project/Scripts/Runtime/Tarot/Cards/DeathCard.cs` | 죽음 |
| `Assets/_Project/Scripts/Runtime/Tarot/Cards/TowerCard.cs` | 탑 |
| `Assets/_Project/Scripts/Runtime/Tarot/Cards/LoversCard.cs` | 연인 |
| `Assets/_Project/Scripts/Runtime/Tarot/Cards/HermitCard.cs` | 은둔자 |
| `Assets/_Project/Scripts/Runtime/Tarot/Cards/WheelOfFortuneCard.cs` | 운명의 수레바퀴 |
| `Assets/_Project/Scripts/Runtime/Tarot/Cards/FoolCard.cs` | 바보 |
| `Assets/_Project/Scripts/Runtime/Tarot/Cards/MagicianCard.cs` | 마법사 |
| `Assets/_Project/Scripts/Runtime/Tarot/TarotCardId.cs` | enum (Star, Death, ...) |
| `Assets/_Project/Scripts/Runtime/Tarot/TarotProcNotifier.cs` | UI 알림 (text floating) |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_타로.asset` | 타로 set |

### 수정

| 경로 | 변경 |
|---|---|
| `RelicEffectType.cs` | `TarotEffectMultiplier` 값 추가 |
| `SetEffectApplicator.cs` (CL-140) | `TarotProc` case 활성 + `TarotEffectMultiplier` case 추가 |
| `RewardPool.cs` | `PicksAllowed` 일시 +1 (별 카드용) — CL-146 의 PicksAllowed hook 재사용 |
| `MagicalGirlSpawner.cs` (CL-144) | `AddTemporary(int count)` / `RemoveTemporary` 메서드 추가 (연인용) |

### 참조

| 경로 | 사용 |
|---|---|
| `RoomEntryRuntimeEvents` | `RoomEntered` 이벤트 구독 |
| `BuildManager` (CL-139) | 보유 세트 목록 (수레바퀴) + Tarot 태그 카운트 |
| `Health` (TDE) | 죽음 데미지 |
| `PlayerStatModifierContainer` | 마법사 임시 공격력 (Timed modifier) |
| `RoomData` / 다음 방 spawn 컨트롤러 | 은둔자 적 수 -% |

---

## 구현 단계

### 1단계: enum 추가 + SetEffectApplicator hook (15분)

```csharp
// RelicEffectType.cs
TarotEffectMultiplier,
```

```csharp
// SetEffectApplicator.cs
case RelicEffectType.TarotProc:
    tarotSystem.AddProcRate(tier.Magnitude);
    break;
case RelicEffectType.TarotEffectMultiplier:
    tarotSystem.AddEffectMultiplier(tier.Magnitude);
    break;
```

### 2단계: ITarotCard 인터페이스 + TarotCardId enum (15분)

```csharp
public enum TarotCardId { Star, Death, Tower, Lovers, Hermit, WheelOfFortune, Fool, Magician }

public interface ITarotCard
{
    TarotCardId Id { get; }
    string DisplayName { get; }
    void Activate(TarotContext ctx);
}

public class TarotContext
{
    public float EffectMultiplier;       // 0 = +0%, 1.0 = +100%
    public Character Player;
    public RoomData CurrentRoom;
    public TarotSystem System;           // 바보 카드 재귀용
}
```

### 3단계: TarotSystem 본체 (1시간)

```csharp
public class TarotSystem : MonoBehaviour
{
    [SerializeField] private RoomEntryRuntimeEvents roomEvents;
    [SerializeField] private List<MonoBehaviour> cardComponents;  // 8개 ITarotCard impl

    private float _procRate;          // 0~1
    private float _effectMultiplier;  // 0=+0%, 1.0=+100%
    private readonly List<ITarotCard> _cards = new();
    private System.Random _rng;

    private void Awake()
    {
        foreach (var mb in cardComponents)
            if (mb is ITarotCard c) _cards.Add(c);
        _rng = new System.Random();
    }

    private void OnEnable()  => roomEvents.RoomEntered += OnRoomEntered;
    private void OnDisable() => roomEvents.RoomEntered -= OnRoomEntered;

    public void AddProcRate(float delta)         => _procRate         = Mathf.Clamp01(_procRate + delta);
    public void AddEffectMultiplier(float delta) => _effectMultiplier += delta;
    public void Reset() { _procRate = 0; _effectMultiplier = 0; }  // run 시작 시 호출

    private void OnRoomEntered(RoomEnteredPayload p)
    {
        if (_procRate <= 0) return;
        if (_rng.NextDouble() >= _procRate) return;
        DrawAndActivate(allowFool: true);
    }

    public void DrawAndActivate(bool allowFool)
    {
        var pool = allowFool ? _cards : _cards.Where(c => c.Id != TarotCardId.Fool).ToList();
        var picked = pool[_rng.Next(pool.Count)];
        var ctx = new TarotContext
        {
            EffectMultiplier = _effectMultiplier,
            Player = ResolvePlayer(),
            CurrentRoom = ResolveCurrentRoom(),
            System = this,
        };
        picked.Activate(ctx);
        TarotProcNotifier.Show(picked.DisplayName);
    }
}
```

### 4단계: 카드 8개 구현 (각 10~20분, 총 1.5시간)

각 카드는 MonoBehaviour + `ITarotCard` 구현 (Spawner/Manager 참조 필요해서). 단순한 카드는 `[SerializeField] private XxxRef` 만.

**별 (Star)** — `RewardPool.AddBonusPick(1)` 호출. 다음 보상 패널에서 +1장.

**죽음 (Death)** — 현재 방 모든 Enemy Health 검색 → `health.Damage(health.MaximumHealth * 0.50f * (1 + mul), this, ...)`.

**탑 (Tower)** — 모든 Enemy → `enemyAI.Stun(2f * (1+mul))`. TDE 의 `CharacterStun` 같은 ability 사용. 미존재 시 단순 movement disable.

**연인 (Lovers)** — `magicalGirlSpawner.AddTemporary(1)` → 방 종료 이벤트 시 `RemoveTemporary(1)`. (Spawner에 신규 메서드 필요)

**은둔자 (Hermit)** — 다음 방 spawn 시 적 수 -30%. `RoomEntryRuntimeController` 또는 spawner에 `nextRoomEnemyMultiplier` 일시 적용.

**운명의 수레바퀴** — `buildManager.GetActiveSets()` 중 랜덤 하나 → `tier += Mathf.CeilToInt(1 * (1+mul))` 일시. 방 종료 시 복원.

**바보 (Fool)** — `system.DrawAndActivate(allowFool: false)` 두 번 연속 호출.

**마법사 (Magician)** — `playerStat.AddTimed(StatId.AttackPower, 0.30f * (1+mul), Until.RoomEnd)` 또는 자체 토큰 발급.

### 5단계: 방 종료 시 효과 해제 (30분)

`RoomCleared` 또는 `RoomExited` 이벤트 hook → 본 CL이 발급한 모든 일시 효과 unwind.

각 카드가 자체 cleanup 등록:
```csharp
ctx.RegisterRoomEndCleanup(() => playerStat.RemoveTimed(token));
```

→ TarotContext 에 `Action OnRoomEnd` 콜백 리스트 + TarotSystem 이 RoomCleared 시 일괄 실행.

### 6단계: BuildSet_타로.asset (10분)

| 티어 | RequiredCount | EffectType | Magnitude | Description |
|---|---|---|---|---|
| 1 | 2 | TarotProc | 0.10 | 방 진입 시 10% 확률로 카드 |
| 2 | 4 | TarotProc | 0.25 | 25% 확률 |
| 3 | 6 | TarotEffectMultiplier | 0.50 | 카드 효과 +50% |

→ 단순 3티어 구성. 회의록 / items_draft 가 명확한 표를 안 줘서 (디폴트 가정) 추후 밸런스에서 재조정.

### 7단계: TarotProcNotifier UI (20분)

화면 중앙에 카드 이름 floating text 1.5초 + fade. placeholder.

```csharp
public static class TarotProcNotifier
{
    public static void Show(string cardName)
    {
        // PopupTextSpawner 가 있으면 재사용, 없으면 단순 Debug.Log
        Debug.Log($"[TAROT] {cardName} 발동!");
    }
}
```

### 8단계: 검증 (1시간)

```
시나리오 1: TarotProc 0
- 타로 set 미발동 → 방 진입해도 카드 안 뽑힘

시나리오 2: TarotProc 100% (강제)
- TarotSystem._procRate = 1f 디버그 강제
- 방 진입마다 카드 1장 추첨, 화면에 카드 이름 표시

시나리오 3: 별 카드
- 강제로 별 발동 → 다음 보상 4장 표시

시나리오 4: 죽음 카드
- 강제로 죽음 → 모든 적 HP 50% 즉시 소실

시나리오 5: 탑 카드
- 강제로 탑 → 모든 적 2초 동안 정지

시나리오 6: 마법사 카드 (방 한정)
- 강제 마법사 → 공격력 +30%
- 방 클리어 → 공격력 원복

시나리오 7: 바보 카드
- 강제 바보 → 두 카드 이름 연달아 표시 (둘 다 발동)
- 바보가 또 바보를 안 뽑는지 확인 (allowFool=false)

시나리오 8: TarotEffectMultiplier 적용
- mul = 1.0 (수레바퀴/별의운명) → 죽음 데미지가 100% (50% × 2)
```

---

## 위험 / 결정 미정

### 위험

1. **TDE Stun 메커니즘 미확인**: 탑 카드가 TDE 의 `CharacterMovement.MovementForbidden` 또는 별도 stun ability 가 있는지 확인 필요. 없으면 단순 `enemyAI.enabled = false` + 코루틴.
2. **은둔자 "다음 방 적 수 -30%"**: 방 spawn 로직이 어디서 결정되는지에 따라 구현 위치 다름. `WaveSpawnedPayload` 발생 전에 카운트 조정해야 함. → MVP 는 **다음 방 한정 spawn count multiplier** 를 RoomEntryRuntimeController 에 일시 필드로.
3. **수레바퀴 "랜덤 세트 1티어 부스트"**: BuildManager 의 set tier 가 보유 카운트 함수임 → 카운트를 일시적으로 +N 할지, tier override 할지 결정 필요. → **tier override 추천** (`buildManager.AddTemporaryTierBoost(setTag, +1)`).
4. **바보 재귀**: 깊이 1 강제 (allowFool=false). 무한 루프 방지.
5. **방 종료 cleanup 누락**: 마법사/연인/수레바퀴 각각 cleanup 안 되면 효과 영구화 → OP 버그. RoomCleared 일괄 실행 + 안전망 (run 종료 시 풀 리셋).
6. **TarotProcNotifier 의존**: PopupTextSpawner 없으면 Debug.Log fallback. 시각 ticket 별도.
7. **`_cards` 직렬화**: 8개 MonoBehaviour 를 List 에 끌어다 놓을지, 자체 enumerate 할지. → **자체 GetComponentsInChildren** 권장 (Awake 시 자동 수집).

### 결정 미정

- [ ] 은둔자 적용 방식 — 본 plan: **다음 방 spawn multiplier 일시 -30%**
- [ ] 수레바퀴 부스트 방식 — 본 plan: **tier override (+1)**
- [ ] 탑 stun 구현 — 본 plan: **TDE CharacterMovement.MovementForbidden + 코루틴 fallback**
- [ ] BuildSet_타로 티어 (RequiredCount/Magnitude) 정확값 — 본 plan: **2/4/6 + 0.10/0.25/+0.50**
- [ ] TarotProcNotifier 정식 UI — 본 plan: **Debug.Log fallback** (별도 ticket)
- [ ] 방 진입 0.5초 딜레이 (UX) vs 즉시 발동 — 본 plan: **즉시** (단순)

---

## 작업 단위 가이드 (Plan 분할, Ticket 단일)

> ticket 은 **CL-147 단일**, plan 에서 단위 분할.

### 작업 단위 A (1.5~2시간) — 프레임워크
- enum 추가
- ITarotCard / TarotCardId / TarotContext
- TarotSystem 본체 + RoomEntered 구독
- SetEffectApplicator hook 활성
- BuildSet_타로.asset
- TarotProcNotifier (Debug.Log)

### 작업 단위 B (2.5~3시간) — 카드 8장 + 통합
- 별, 죽음, 탑, 연인 (1.5시간)
- 은둔자, 수레바퀴, 바보, 마법사 (1.5시간)
- 방 종료 cleanup
- MagicalGirlSpawner.AddTemporary 신설
- RewardPool.AddBonusPick 신설 (또는 PicksAllowed +1 hook 재사용)

### 검증 (1시간)

---

## 후속 ticket 영향

| Ticket | CL-147 과의 관계 |
|---|---|
| **CL-148 (인벤토리 UI)** | 무관 |
| **CL-152 (보상/상점 통합)** | 별 카드의 PicksAllowed +1 이 보상 시스템과 연동 |
| **CL-153 (QA)** | 8장 발동 시각/효과 검증 |
| **별도 ticket: 타로 카드 도트** | placeholder text 를 정식 카드 일러스트로 |
| **별도 ticket: 타로 발동 정식 UI** | Notifier → 풀 카드 컴포지션 + 애니메이션 |

---

## 예상 시간

### 단위 A (프레임워크)
| 단계 | 시간 |
|---|---|
| enum + SetEffectApplicator hook | 15분 |
| ITarotCard / Id / Context | 15분 |
| TarotSystem 본체 | 1시간 |
| BuildSet_타로 | 10분 |
| TarotProcNotifier | 20분 |
| **A 합계** | **약 2시간** |

### 단위 B (카드 8장)
| 단계 | 시간 |
|---|---|
| 별 | 10분 |
| 죽음 | 15분 |
| 탑 (TDE stun 확인) | 25분 |
| 연인 (Spawner 확장) | 20분 |
| 은둔자 (다음 방 hook) | 30분 |
| 수레바퀴 (BuildManager 확장) | 25분 |
| 바보 (재귀) | 10분 |
| 마법사 (Timed modifier) | 15분 |
| 방 종료 cleanup | 30분 |
| **B 합계** | **약 3시간** |

| 검증 | 1시간 |
| **전체 합계** | **약 6시간** |

→ 5점 P2 ticket. CL-146 과 비슷한 규모.

---

## Phase 3 진행률 (CL-147 후)

| Ticket | Plan | Code |
|---|---|---|
| CL-142 평타 5세트 | ✅ | - |
| CL-143 스킬 3세트 | ✅ | - |
| CL-144 미소녀 1~4 | ✅ | - |
| CL-145 미소녀 5합체 | ✅ | - |
| CL-146 공통 7세트 | ✅ | - |
| **CL-147 타로** | ✅ ← 방금 | - |

**Phase 3 plan 100% 완료.**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-149 골렘 몬스터 | 3점 | 클라2 영역 (몬스터 추가) |
| B | CL-150 아이템 사이즈 시스템 | 2점 | CL-138 의 _size 필드 활용 |
| C | CL-151 인벤토리 자동 배치 + 정리 버튼 | 3점 | Phase 4 인벤토리 UI |
| D | CL-152 보상/상점 풀 ItemData 연결 | 2점 | RewardPool 통합 |

**추천: B 또는 C** (Phase 3 완료 후 Phase 4 인벤토리 흐름 자연스러움). CL-149 는 클라2 작업이라 클라1 흐름 끊김.

뭐로 갈까요?

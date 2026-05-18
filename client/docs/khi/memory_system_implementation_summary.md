# 기억 시스템 / 보상 시스템 구현 종합 정리

작성일: 2026-05-14

## Context

기억(퍼즐) 시스템과 그 보상 효과들을 실제 게임에 반영하기 위한 일련의 작업.
저장만 되고 효과 없던 보상들을 살리고, 새 보상 타입을 추가하며, 부수적 버그(체력 보존, 부활 애니메이션 등)도 함께 처리.

---

## 1. 새 보상 타입 추가 (2종)

### A. `TalentPointsBonus` — 재능 포인트 +N

런 시작 시 사용 가능한 재능 포인트 풀에 +N 영구 추가.

**관련 파일**:
- [`MemoryCompletionRewardType.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemoryCompletionRewardType.cs) — enum `TalentPointsBonus` (= 11) 추가
- [`MemorySaveData.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemorySaveData.cs) — 필드 `BonusTalentPoints` 추가
- [`MemoryPieceUnlockService.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemoryPieceUnlockService.cs) — 해금 시 누적 case 추가
- [`MemoryPieceSlotView.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Memory/UI/MemoryPieceSlotView.cs) — UI 라벨 `"재능 포인트 +N"`
- [`TalentStartupApplier.cs:121`](../../LostMemory/Assets/_Project/Scripts/Runtime/Talents/TalentStartupApplier.cs) — `new TalentModel(_talentDatas, _totalPoints + memorySave.BonusTalentPoints, saved)` 합산
- [`TalentPanelView.cs:30,66`](../../LostMemory/Assets/_Project/Scripts/Runtime/Talents/TalentPanelView.cs) — UI 패널의 Start/Open 양쪽 합산

### B. `StartingRelicCount` — 시작 유물 +N

던전 1F 1R 진입 직후 보상 패널을 N회 연속 띄워 유물 선택.

**관련 파일**:
- [`MemoryCompletionRewardType.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemoryCompletionRewardType.cs) — enum `StartingRelicCount` (= 12) 추가
- [`MemorySaveData.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemorySaveData.cs) — 필드 `BonusStartingRelicCount` 추가
- [`MemoryPieceUnlockService.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemoryPieceUnlockService.cs) — 해금 시 누적 case
- [`MemoryPieceSlotView.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Memory/UI/MemoryPieceSlotView.cs) — UI 라벨 `"시작 유물 +N"`
- [`RewardPool.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardPool.cs) — `DrawCount()` 에 `relicOnly` 옵션 + `ApplyRarityBoost()` 헬퍼
- [`RewardPanelView.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardPanelView.cs) — `Show()` 오버로드 (relicOnly, rarityBoostPercent)
- [`RewardController.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs) — `ShowStartingRelicReward(N)` public API + 체이닝 로직 + `_startingRelicRemaining` 상태
- [`RunManager.cs:HandleDungeonBuilt`](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs) — 던전 빌드 시 hook

---

## 2. 미구현 보상 살리기 (3종)

기존에 `MemorySaveData` 에 저장만 되고 효과 없던 보상들을 실제 작동시킴.

### A. `StartingGold` — 시작 골드 +N
**조각 3개 살림**: 0-0 (+50), 1-1 (+100), 3-1 (+200)

[`RunManager.HandleDungeonBuilt`](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs) 에서 `goldWallet.Add(BonusStartingGold)` 호출.

### B. `RewardRarityBoost` — 보상 등급 상향 확률
**조각 2개 살림**: 1-4 (+5%), 3-2 (+10%)

[`RewardPool.ApplyRarityBoost`](../../LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardPool.cs) — 추첨 후 N% 확률로 한 단계 상위 등급(Common→Rare→Unique→Legendary)으로 교체.
[`RewardController.ShowReward`](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs) — `MemoryMetaService.Load().BonusRewardRarityPercent` 전달.

### C. `ReviveOnce` — 자동 부활 1회
**조각 1개 살림**: 1-5 (불굴의 의지)

[`KhiDownController`](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs):
- `_memoryReviveAvailable` 플래그 + `SetMemoryReviveAvailable(bool)` public API
- `EnterDown()` 끝에 1.5초 지연 후 자동 `ForceRevive` 호출 (코루틴)
- `memoryReviveDelay` 인스펙터 파라미터 (기본 1.5초)
- Animator transition 누락 fallback: `EnsureNotStuckInDownAnimationCoroutine` — 부활 후 강제 Rebind

[`RunManager.HandleDungeonBuilt`](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs) — `HasRevive==true` 면 활성화.

---

## 3. 조각(Piece) 데이터 변경 (총 8개)

| 조각 ID | 이름 (Before → After) | 보상 (Before → After) |
|---|---|---|
| `0-4` | 상인의 눈 → **잊혀진 재능** | ShopSlotExpand → TalentPointsBonus +5 |
| `0-5` | 풍요로운 선택 → **되찾은 기억** | RewardSlotExpand → TalentPointsBonus +5 |
| `2-4` | 유물 수집가 II → **깨어난 재능** | RelicSlotExpand → TalentPointsBonus +5 |
| `2-5` | 지름길 → **완전한 각성** | RoomSkip → TalentPointsBonus +5 |
| `1-2` | 유물 수집가 → **운명의 동반자** | RelicSlotExpand → StartingRelicCount +1 |
| `3-5` | 각성의 유물 (이름 유지) | RunStartRelic → StartingRelicCount +1 |

---

## 4. 보상 적용 상태 감사 (Audit)

### ✅ 정상 작동 (8종)
- StatBoost (`TalentStartupApplier`)
- RelicSlotExpand (`TalentStartupApplier`)
- ShardDropBonus (`MemoryProgressTracker`)
- **TalentPointsBonus** ← 신규
- **StartingRelicCount** ← 신규
- **StartingGold** ← 살림
- **RewardRarityBoost** ← 살림
- **ReviveOnce** ← 살림

### ❌ 여전히 미구현 (현재 사용 조각 없음)
- `RoomSkip` — 방 건너뛰기 (2-5 가 TalentPointsBonus 로 바뀌어 사용 조각 없음)
- `RewardSlotExpand` — 보상 슬롯 +1 (사용 조각 없음)
- `ShopSlotExpand` — 상점 슬롯 (사용 조각 없음)
- `RunStartRelic` — Deprecated, `StartingRelicCount` 로 대체됨

---

## 5. 부수 수정/개선

### A. 씬 이동 시 체력 보존 버그 수정
**파일**: [`PlayerHealthSnapshotter.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Player/PlayerHealthSnapshotter.cs)

`Start()` 의 복원 작업을 코루틴으로 **다음 프레임 지연**. Unity 의 Start 호출 순서 비결정성으로 TDE `Health.Start()` 가 뒤늦게 `CurrentHealth = MaximumHealth` 로 덮어쓰던 문제 해결.

⚠️ **주의**: Player 프리팹(`TestKhi_MinimalCharacter2D.prefab`)에 `PlayerHealthSnapshotter` 컴포넌트가 부착되어야 작동. 씬 안의 Character 에서 떼어져 있다면 인스펙터에서 다시 추가 필요.

### B. 행운 5스택 보상 효과 임시 변경
**파일**: [`RewardController.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs)

5택 2픽 다중 픽 모드 → **4택 1픽** 으로 임시 변경 (`_cards` 배열 5장 미만 환경 대응).

```csharp
// 현재
int picksAllowed = 1;
int count        = luckSlotExpand ? 4 : 3;

// TODO 추후 복귀
int picksAllowed = luckSlotExpand ? 2 : 1;
int count        = luckSlotExpand ? 5 : 3;
```

### C. MagicalGirl 합체 분홍 LineRenderer 토글
**파일**: [`MagicalGirlFusion.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlFusion.cs)

`_showLaserLine` 인스펙터 토글 추가 (기본 false). T 키 레이저 발동 시 LineRenderer 빔 시각 효과 ON/OFF 가능. 데미지/카메라 흔들림/Trail VFX 는 그대로.

### D. 셀 슬롯 UI 누락된 라벨 추가
**파일**: [`MemoryPieceSlotView.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Memory/UI/MemoryPieceSlotView.cs)

`BuildRewardText` switch 에 `TalentPointsBonus`, `StartingRelicCount` 케이스 추가. 이전엔 빈 텍스트로 표시되던 버그.

---

## 6. 변경 파일 한눈 목록

### 코드 (Runtime)
- `_Project/Scripts/Runtime/Memory/MemoryCompletionRewardType.cs`
- `_Project/Scripts/Runtime/Memory/MemorySaveData.cs`
- `_Project/Scripts/Runtime/Memory/MemoryPieceUnlockService.cs`
- `_Project/Scripts/Runtime/Memory/UI/MemoryPieceSlotView.cs`
- `_Project/Scripts/Runtime/Player/PlayerHealthSnapshotter.cs`
- `_Project/Scripts/Runtime/Rewards/RewardPool.cs`
- `_Project/Scripts/Runtime/Rewards/RewardPanelView.cs`
- `_Project/Scripts/Runtime/Stage/RewardController.cs`
- `_Project/Scripts/Runtime/Stage/RunManager.cs`
- `_Project/Scripts/Runtime/Talents/TalentStartupApplier.cs`
- `_Project/Scripts/Runtime/Talents/TalentPanelView.cs`
- `_Project/Scripts/Runtime/TestKhi/KhiDownController.cs`
- `_Project/Scripts/Runtime/MagicalGirl/MagicalGirlFusion.cs`

### 데이터(에셋)
- `_Project/Data/Memory/MemoryPieceData_0-4.asset`
- `_Project/Data/Memory/MemoryPieceData_0-5.asset`
- `_Project/Data/Memory/MemoryPieceData_1-2.asset`
- `_Project/Data/Memory/MemoryPieceData_2-4.asset`
- `_Project/Data/Memory/MemoryPieceData_2-5.asset`
- `_Project/Data/Memory/MemoryPieceData_3-5.asset`

---

## 7. 검증 절차 (전체 흐름)

### 사전 준비
1. Unity Editor 컴파일 통과 확인
2. **`Edit > Clear All PlayerPrefs`** 로 초기화

### 새 보상 검증
1. `LostMemory > Memory > Debug > Grant 10000 Shards (khi)`
2. 기억 NPC F → 다음 조각들 해금 후 던전 입장:
   - **0-4 또는 0-5** → 재능 포인트 +5 확인 (재능 패널 사용 가능 포인트 10→15)
   - **1-2** → 던전 진입 직후 유물 추첨 패널 1회 표시
   - **0-0** → 시작 골드 +50 (HUD)
   - **1-4** → 보상 추첨에서 한 단계 상위 등급 카드 등장 (5% 확률)
   - **1-5** → 사망 시 자동 부활 (Down 1.5초 → Revive)

### 씬 이동 시 체력 보존 검증
1. Dungeon 1F 1R 에서 체력을 일부러 줄임 (예: 40/110)
2. 1F 2R 로 이동
3. **Console 로그**: `[PlayerHealthSnapshotter] Restored 40.0/110.0`
4. HUD 체력바가 40/110 으로 표시 (이전 버그: 110/110)

### 행운 5스택 보상 검증
1. 행운 태그 유물 5개 보유
2. 방 클리어 → 보상 패널
3. **카드 4장** 표시 (이전 3장)

---

## 8. 알려진 잔여 이슈 / TODO

| 이슈 | 상태 | 비고 |
|---|---|---|
| `RewardPanelView._cards` 배열 크기 4 이상 보장 | ⚠️ 인스펙터 수동 작업 필요 | 사용자가 카드 GameObject 복제해서 등록 |
| `PlayerHealthSnapshotter` 부착 확인 | ⚠️ 인스펙터 수동 작업 가능 | 씬에서 떼어진 상태면 재부착 |
| 행운 5택 2픽 모드 복귀 | TODO | `_cards` 5장 보장 후 코드 2줄 변경 |
| Animator Revive transition 추가 (정석) | TODO | Hero_Animator 에 Down→Idle (Revive 트리거) 추가. 현재 코드 fallback 으로 작동 중 |
| 다운 중 RoomCleared → 보상 패널 동시 등장 | 관찰됨 | UX 이슈, 의도된 동작인지 확인 필요 |
| Build deprecated API 경고 30+ 건 | 기능 영향 없음 | 추후 정리 |

---

## 결론 요약

- **기억 시스템 24조각 중 21조각이 실제 효과 작동** (이전 15조각 → 21조각)
- 미사용 enum (`RoomSkip`, `RewardSlotExpand`, `ShopSlotExpand`, `RunStartRelic`) 만 미구현 상태로 남음
- 씬 이동 시 체력 보존 + 부활 시스템 작동 검증 완료
- 미소녀 합체 시각 효과 + 행운 보상 효과는 임시 처리 (추후 복귀 예정)

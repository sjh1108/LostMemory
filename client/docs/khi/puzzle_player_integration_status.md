# 기억 퍼즐 ↔ 플레이어 연동 / 저장 / 씬 이동 상태 보고

작성일: 2026-05-13

## Context
사용자가 `Assets/_Project/Prefabs/UI/Test/MemoryPuzzleCell.prefab` 을 기준으로
기억 퍼즐 시스템과 플레이어가 어디까지 연동되어 있고, 무엇이 저장되며,
씬을 이동해도 데이터가 유지되는지를 확인 요청.

실제 프로젝트 루트는 `LostMemory/Assets/_Project/` 임.

---

## 1. 플레이어 ↔ 퍼즐 연동: **연동됨 (트리거 + F키 방식)**

연결 경로:
- `LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemoryNpcInteractable.cs:60` — `OnTriggerEnter2D` 에서 `Player` 태그 검증
- `MemoryNpcInteractable.cs:43` — `_playerInRange && Input.GetKeyDown(F)` 일 때 패널 오픈
- `MemoryNpcInteractable.cs:56` — `_unlockPanel.Open()` / `_collectionPanel.Open()` 호출

플로우:
1. Player(태그) → NPC Collider2D 진입 → `_playerInRange = true`, prompt UI 활성
2. F 키 → `MemoryUnlockPanelView.Open()` (해금 패널) + `MemoryCollectionPanelView.Open()` (수집 현황)
3. 패널 안에서 셀 클릭 → 확인 다이얼로그 → `MemoryPieceUnlockService.TryUnlock(piece)`
4. 해금 성공 시 `OnPieceUnlocked` 이벤트 → 그리드 페이드 애니메이션 + 보상 적용

**필요 인스펙터 세팅**: NPC GameObject에 `Collider2D (IsTrigger)` + `MemoryNpcInteractable` 컴포넌트, 그리고 `_unlockPanel`/`_collectionPanel` 참조 연결.

---

## 2. 저장 범위: **PlayerPrefs (JSON) 영구 저장**

저장소: `LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemoryMetaService.cs:17`
- Key: `"MemorySystem_SaveData"`
- 방식: `JsonUtility.ToJson(MemorySaveData)` → `PlayerPrefs.SetString` → `PlayerPrefs.Save`

저장되는 내용 (`MemorySaveData`):
| 필드 | 의미 |
|---|---|
| `AccumulatedShards` | 누적 기억 조각(재화) |
| `UnlockedPieceIds` | 해금한 조각 ID 목록 — **퍼즐 진행도 = 이 리스트** |
| `BonusShardPerRun` | 런당 추가 조각 |
| `BonusStartingGold` | 시작 골드 보너스 |
| `PermanentBonusRelicSlots` | 유물 슬롯 보너스 |
| `BonusRewardSlots`, `BonusShopSlots` | 보상/상점 슬롯 보너스 |
| `BonusRewardRarityPercent` | 보상 희귀도 % |
| `HasRevive`, `HasRoomSkip`, `HasRunStartRelic` | 해금 플래그 |
| `PermanentBoosts` | 영구 스탯 보너스 (조각 출처 기록 포함) |

저장 호출 지점:
- `MemoryProgressTracker.Awake()` → `Load()`
- `MemoryProgressTracker.SaveRunFragments()` → 런 종료 시 `Save()`
- `MemoryCompletionRewardApplicator` → StatBoost 적용 시 `Load → 수정 → Save`
- 퍼즐 셀 해금 시 (Unlock 트랜잭션 내부) → `Save()`

**퍼즐 자체의 별도 저장 키는 없음.** 셀이 잠겼는지 풀렸는지는 `UnlockedPieceIds` 에 ID가 들어있는지로 판정 (`MemoryPuzzleView_khi.Refresh()` 가 이 리스트를 읽음).

---

## 3. 씬 이동 시 적용: **유지됨**

이유:
- **PlayerPrefs 는 씬과 무관하게 디스크에 저장** → 어느 씬에서 Load 해도 동일한 데이터를 반환.
- 런타임 캐시 역할의 `MemoryShardWallet` 은 `RuntimeInitializeOnLoadMethod` + `DontDestroyOnLoad` 싱글톤. 게임 시작 시 1회 부트스트랩되어 모든 씬에서 동일 인스턴스 사용.
- 새 씬에서 패널이 열릴 때 `MemoryCollectionPanelView.Open()` 이 `MemoryMetaService.Load()` 를 다시 호출하므로 최신 상태 반영.

해당 씬 목록 (모두에서 동일하게 작동):
- `Scenes/Title/Title.unity`
- `Scenes/Town/Town.unity`
- `Scenes/Dungeon/Dungeon.unity`, `Dungeon_1F_1R~4R.unity`, `Dungeon_1F_Boss.unity`, `Dungeon_1F_Shop.unity`

---

## 4. 아직 연결 안 된 부분 (Gap)

| 항목 | 상태 |
|---|---|
| 셀 클릭 → 다이얼로그 → 해금 → UI 새로고침 | ✅ 완성 |
| 셀 해금 → 보상(스탯/슬롯/플래그) 적용 | ✅ 완성 |
| 런 종료 시 shard 적립 (`MemoryProgressTracker.SaveRunFragments`) | ✅ 완성 |
| PlayerPrefs 영구 저장 | ✅ 완성 |
| 씬 이동 후 상태 유지 | ✅ 완성 (PlayerPrefs 기반) |
| **캔버스 1장(6조각) 완성 콜백** | ❌ 없음 (Next 버튼 게이팅용 `IsCurrentCanvasFullyUnlocked()` 만 존재) |
| **전체 4캔버스(24조각) 완성 이벤트** | ❌ 없음 — 엔딩/시네마틱/플래그 트리거 부재 |
| **`MemoryUnlockPanelView._autoOpenOnStart`** | ⚠️ 디버그 플래그. 코드에 "서비스 배포 전 false 로 변경" TODO 주석 있음 |

---

## 결론 요약

- 퍼즐 ↔ 플레이어 **연동: 완성** (트리거 + F 키)
- 저장 **범위: 누적 조각, 해금한 조각 ID 목록, 영구 보너스/플래그 모두 저장**
- 씬 이동 적용: **자동 유지** (PlayerPrefs 기반 + DontDestroyOnLoad 싱글톤)
- 단, **"전체 완성 시 무언가가 일어나는" 트리거는 아직 구현되지 않음**.

---

# 작업 계획

## Context (작업 배경)

1. **실기기에서 퍼즐 1조각 해금 흐름이 정상 작동하는지** 직접 검증한다.
2. **퍼즐 데이터 보상 변경**: Canvas 0 (첫 번째 캔버스) 의 5번/6번 조각(`order` 4, 5)의 보상을 "재능 포인트 +5"로 바꾼다.
   - 해금한 조각 수만큼 매 런 사용 가능 재능 포인트가 누적 증가 (예: 기본 10점 → 1조각 해금 시 15점 → 2조각 해금 시 20점)

## 변경 대상 / 추가 작업

### (1) 새 보상 타입 추가
**파일**: `LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemoryCompletionRewardType.cs`

`MemoryPieceRewardType` enum 끝에 추가:
```csharp
/// <summary>
/// 런 시작 시 사용 가능한 재능 포인트 영구 +N. RewardMagnitude = 추가 포인트 수.
/// MemorySaveData.BonusTalentPoints 에 누적 → TalentStartupApplier 가 totalPoints 에 합산.
/// </summary>
TalentPointsBonus,   // = 11
```

### (2) 저장 데이터 필드 추가
**파일**: `LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemorySaveData.cs`

`MemorySaveData` 클래스에 필드 추가:
```csharp
/// <summary>런 시작 시 추가 사용 가능한 재능 포인트. TalentPointsBonus 보상 합산.</summary>
public int BonusTalentPoints;
```

### (3) 보상 적용 로직 분기 추가
**파일**: `LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemoryPieceUnlockService.cs:208` (default 직전)

```csharp
case MemoryPieceRewardType.TalentPointsBonus:
    save.BonusTalentPoints += Mathf.Max(1, Mathf.RoundToInt(piece.RewardMagnitude));
    break;
```

### (4) 런 시작 시 보너스 합산
**파일**: `LostMemory/Assets/_Project/Scripts/Runtime/Talents/TalentStartupApplier.cs:121` 의 `ApplyTalentStats()` 수정

기존:
```csharp
var saved = TalentSaveService.Load();
var model = new TalentModel(_talentDatas, _totalPoints, saved);
```

변경:
```csharp
var saved = TalentSaveService.Load();
MemorySaveData memorySave = MemoryMetaService.Load();
int effectiveTotal = _totalPoints + memorySave.BonusTalentPoints;
var model = new TalentModel(_talentDatas, effectiveTotal, saved);
```
※ 이 함수 끝(`OnEnable` 부근)에서 `MemoryMetaService.Load()` 가 line 114에서 이미 호출되고 있으므로, 그 결과를 재사용하도록 약간 리팩토링 가능 (선택).
※ `using LostMemory.Memory;` 가 필요할 수 있음 — 파일 상단 확인.

### (5) 에셋 파일 수정 (2개)
- `LostMemory/Assets/_Project/Data/Memory/MemoryPieceData_0-4.asset`
  - `_rewardType: 7` → `_rewardType: 11`
  - `_rewardMagnitude: 1` → `_rewardMagnitude: 5`
  - `_displayName: "상인의 눈"` → 재능 포인트 보상에 어울리는 이름으로 변경 (사용자 의견 요청 가능)
  - `_description` 도 함께 갱신
- `LostMemory/Assets/_Project/Data/Memory/MemoryPieceData_0-5.asset`
  - `_rewardType: 6` → `_rewardType: 11`
  - `_rewardMagnitude: 1` → `_rewardMagnitude: 5`
  - `_displayName: "풍요로운 선택"`, `_description` 갱신

### (참고) UI / 디버그 패널
`TalentPanelView.cs:32, 68` 의 `_debugTotalPoints` 는 **에디터 디버그용**으로 보이며, 실제 게임 흐름은 `TalentStartupApplier` 를 거치므로 (4) 만으로 충분.
다만 디버그 패널을 실기기 테스트에서 사용한다면 `_debugTotalPoints` 도 동일하게 보너스를 반영하도록 동일 패턴 적용 권장.

---

## 검증 절차 (실기기)

### 사전 준비
1. Unity Editor 에서 위 (1)~(5) 변경 적용 후 빌드.
2. (선택) 디버그용으로 `MemoryMetaService.DeleteAll()` 호출하는 임시 버튼/콘솔 명령으로 상태 초기화.
3. 또는 빌드 후 PlayerPrefs 키 `MemorySystem_SaveData`, `Talent_*` 를 직접 삭제하여 클린 시작.

### 1조각 해금 흐름 검증
1. 게임 실행 → Town 씬에서 기억 NPC 에게 접근 → F 키.
2. `MemoryCollectionPanelView` (수집 패널) 열림 → Canvas 0 보임.
3. `_shardCost: 15` 만큼 Shard 가 필요하므로, Dungeon 런을 1회 클리어해서 Shard 적립 (또는 디버그로 `MemoryShardWallet` 의 값 강제 설정).
4. order 4 셀 클릭 → 확인 다이얼로그 → 해금.
5. **즉시 확인**: UI 의 그리드에서 해당 셀 페이드/색상 변경.
6. **저장 확인**: 게임 종료 → 재실행 → 같은 셀이 해금 상태 유지 + Shard 잔량 차감 유지.
7. **재능 포인트 증가 확인**: 재능 패널 열기 → 사용 가능 포인트가 기존 10 → 15 로 표시되는지.

### 씬 이동 후 영속성 검증
8. Town 에서 해금 직후 Dungeon 입장 → 다시 Town 복귀 → 해금 상태 유지 + 재능 포인트 유지.
9. Dungeon 입장 직후 재능 패널이 작동한다면 그 안에서도 +5 반영 확인.

### 2조각 모두 해금
10. 같은 절차로 order 5 도 해금 → 재능 포인트가 20 (= 10 + 5 + 5) 으로 증가 확인.

### 회귀 확인
11. 다른 캔버스(Canvas 1, 2, 3)의 order 4, 5 조각은 **기존 보상 유지** 됨을 확인 (변경 안 됐는지 검증).
12. 다른 RewardType (StatBoost, RelicSlotExpand 등) 해금이 여전히 정상 작동하는지 1건 이상 확인.

### 실패 시 디버그 포인트
- 재능 포인트가 늘지 않으면 → `TalentStartupApplier.ApplyTalentStats()` 가 호출되는 타이밍 확인 (런 시작 시점인지, Town 진입 시점인지)
- 저장이 안 되면 → `MemoryPieceUnlockService.TryUnlock()` 의 `MemoryMetaService.Save()` 호출 여부 로그로 확인
- 새 RewardType 11 이 "처리되지 않은 RewardType" 경고로 빠지면 → `MemoryPieceUnlockService.cs` 의 case 추가 누락

---

## 영향 범위 정리

| 항목 | 파일 |
|---|---|
| Enum 1개 값 추가 | `MemoryCompletionRewardType.cs` |
| SaveData 필드 1개 추가 | `MemorySaveData.cs` |
| 보상 적용 case 1개 추가 | `MemoryPieceUnlockService.cs` |
| 런 시작 보너스 합산 | `TalentStartupApplier.cs` |
| Asset 2개 수정 | `MemoryPieceData_0-4.asset`, `MemoryPieceData_0-5.asset` |

코드 변경은 **누적적(additive)**이므로 기존 보상 흐름과의 호환성에 문제 없음. PlayerPrefs 의 기존 저장 데이터도 `JsonUtility.FromJson` 이 새 필드를 기본값 0 으로 처리하므로 마이그레이션 불필요.

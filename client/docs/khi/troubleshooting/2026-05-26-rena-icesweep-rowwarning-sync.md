# 2026-05-26 — Rena 보스 IceSweep row warning guest 측 visual 부재

## 증상
- Rena 보스 IceSweep 패턴 시:
  - **Host 화면**: pillar 깔리기 전 행마다 **빨강/노랑 가로 row warning 선 + 펄스/스윕 애니메이션** 잘 보임
  - **Guest 화면**: 같은 위치에 pillar 는 깔리지만 **warning 선이 안 보임 / 흐릿한 파란 띠만 표시됨**
- Ice pillar 자체 sync 는 정상 (`BroadcastBossIcePillarCellClientRpc` 동작)
- 데미지 로직 정상 (host-authoritative)
- 시각 비대칭으로 guest 가 어느 행이 안전한지 시각적으로 못 파악

## Root cause (왜 안 됐는가)

### 핵심: Host 와 Guest 의 visual hierarchy 가 다름

`UpdateIceSweepRowWarnings()` (`RenaBossSpellCombatController.cs:1262~1329`) 는
**GameObject name 기반 분기** 로 5종의 자식 렌더러를 각각 다른 애니메이션 식으로 보간:

```csharp
bool isEdge = rendererName.IndexOf("Edge", ...) >= 0;
bool isSweep = rendererName.IndexOf("WarningSweep", ...) >= 0;
bool isCenterGlow = rendererName.IndexOf("CenterGlow", ...) >= 0;
bool isCenterRail = rendererName.IndexOf("CenterRail", ...) >= 0;
bool isFill = rendererName.IndexOf("WarningFill", ...) >= 0;
```

| 측 | Spawn 메서드 | 생성된 hierarchy |
|---|---|---|
| **Host** | `SpawnIceSweepRowWarnings` (1008~1108) | 행마다 root + **6 children**: Fill / CenterGlow / EdgeTop / EdgeBottom / CenterRail / Sweep(LTR\|RTL) — 각 자식 이름이 위 분기 케이스에 매칭 |
| **Guest** | `SpawnVisualOnlyIceSweepRowWarnings` (1110~1147) | 행마다 root **1개만** (이름: `VisualOnly_IceSweepRowWarning`) — 자식 없음 |

### Guest 측에서 어떻게 잘못 작동했나
1. Guest 도 `UpdateIceSweepRowWarnings(warnings, progress)` 를 매 프레임 호출 (정상)
2. 그러나 `warnings` 리스트의 유일한 원소 이름은 `VisualOnly_IceSweepRowWarning` 한 가지
3. 그 이름은 어떤 케이스 (`Edge` / `WarningSweep` / `CenterGlow` / `CenterRail` / `WarningFill`) 에도 매칭 안 됨
4. → 마지막 `else` 의 default fill 분기로 빠짐. `isFill=false` 라 scale 변경 없음, color 만 적용
5. 결과: **단조롭고 흐릿한 파란 띠** 만 표시 — host 의 화려한 애니메이션 효과 (펄스, edge 확장, sweep 슬라이드) 전부 손실

### 그래서 host 와 guest 의 visual 격차가 큼
Host 측 visual 은 `UpdateIceSweepRowWarnings` 의 5개 분기가 함께 작동해야 완성됨 — 5개 children 각각이 다른 sortingOrder, 다른 alpha curve, 다른 position lerp 적용. 그 중 어느 하나라도 빠지면 시각 효과의 대부분이 사라짐.

### ClientRpc 시그니처 누락
또한 `BroadcastBossIceSweepRowWarningsClientRpc` 가 `sweepLeftToRight` 인자를 전달하지 않음 — guest 가 Sweep 의 방향 (LTR vs RTL) 을 모름. 비록 위 root cause 보다는 부차적이지만 hierarchy 확장 시 함께 fix 필요.

## Fix

**파일 3곳**:

### 1. `RenaBossSpellCombatController.cs:1110~1147` — `SpawnVisualOnlyIceSweepRowWarnings`

Guest 측 hierarchy 를 host 와 동일한 6-child 구조로 확장. `CreateIceSweepWarningRenderer` static helper 재사용:

```csharp
public void SpawnVisualOnlyIceSweepRowWarnings(
    Vector3 areaCenter,
    float bottom,
    float cellHeight,
    Vector2 damageRowSize,
    int rowCount,
    int safeRow,
    float warningDuration,
    bool sweepLeftToRight)  // 신규 인자
{
    // ...
    for (int row = 0; row < rowCount; row++)
    {
        if (row == safeRow) continue;

        float y = bottom + row * cellHeight;
        GameObject warningRoot = new GameObject("VisualOnly_RenaIceSweepRowWarning");
        Transform warningTransform = warningRoot.transform;
        warningTransform.position = new Vector3(areaCenter.x, y, transform.position.z);
        warningTransform.localScale = new Vector3(warningSize.x, warningSize.y, 1f);

        // Host 와 완전 동일한 6 children 생성 (Fill / CenterGlow / EdgeTop / EdgeBottom / CenterRail / Sweep[LTR|RTL])
        // 자식 이름 / sortingOrder / localPosition / localScale 모두 host 와 일치 →
        // UpdateIceSweepRowWarnings 의 name 기반 분기가 동일하게 적용됨.
        visualWarnings.Add(CreateIceSweepWarningRenderer(warningTransform, "RenaIceSweepRowWarningFill", ...));
        visualWarnings.Add(CreateIceSweepWarningRenderer(warningTransform, "RenaIceSweepRowWarningCenterGlow", ...));
        visualWarnings.Add(CreateIceSweepWarningRenderer(warningTransform, "RenaIceSweepRowWarningEdgeTop", ...));
        visualWarnings.Add(CreateIceSweepWarningRenderer(warningTransform, "RenaIceSweepRowWarningEdgeBottom", ...));
        visualWarnings.Add(CreateIceSweepWarningRenderer(warningTransform, "RenaIceSweepRowWarningCenterRail", ...));
        visualWarnings.Add(CreateIceSweepWarningRenderer(warningTransform,
            sweepLeftToRight ? "RenaIceSweepRowWarningSweepLTR" : "RenaIceSweepRowWarningSweepRTL", ...));
    }
    // ...
}
```

### 2. `RenaBossSpellCombatController.cs:1232~1255` — `BroadcastIceSweepRowWarningsIfHost`

시그니처에 `bool sweepLeftToRight` 추가 + ClientRpc 호출에 전달.

### 3. `PlayerMovementSync.cs:397` — `BroadcastBossIceSweepRowWarningsClientRpc`

시그니처에 `bool sweepLeftToRight` 추가 + `SpawnVisualOnlyIceSweepRowWarnings` 호출 시 전달.

## 검증 후 결과
- Host / Guest 양쪽 모두 동일한 row warning 애니메이션 표시
- Pulse 깜빡임 (`GetSyncedTime() = NetworkManager.ServerTime.Time`) 도 위상까지 일치
- Sweep 방향 (LTR/RTL) 도 일치

## 영향 범위
- Guest visual 만 더 풍부해짐, host 동작 변화 0
- ClientRpc 인자 1개 추가 (NGO 안정성 영향 없음)
- Prefab 변경 0
- 다른 enemy / 보스 영향 0

## 교훈
1. **Visual sync 시 host/guest hierarchy 구조 동일성이 핵심** — name 기반 분기를 쓰는 update 로직은 자식 이름까지 1:1 일치해야 안전.
2. **단순 sprite 1개 spawn 으론 host 의 5-layer 시각을 절대 재현 못 함** — host 의 visual 이 정교할수록 guest visual-only 메서드도 같은 정교도 필요.
3. **시각 sync 헬퍼 (`CreateIceSweepWarningRenderer` 같은 static 메서드) 는 host/guest 양쪽에서 재사용** → 미래 sync 불일치 방지.
4. **ClientRpc 시그니처는 향후 시각 분기에 필요한 모든 입력을 명시적으로 포함** — `sweepLeftToRight` 가 안 들어가서 방향 일치 불가능했던 경우처럼, "혹시 나중에 쓸 수도 있는 입력" 도 함께 보내는 게 안전.

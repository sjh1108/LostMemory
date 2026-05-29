# 2F 미니맵 방 잘림 — Editor 도구로 수정

날짜: 2026-05-25
관련 코드: [FixMinimapColliderBoundsTool.cs](../../LostMemory/Assets/_Project/Scripts/Editor/FixMinimapColliderBoundsTool.cs)

## Context

증상: 2F 던전 미니맵에서 방들이 **잘려서** 보임. 1F 는 정상.

원인 (확정): 각 module prefab 의 `Minimap` 자식 GameObject 에 붙은 `BoxCollider2D.size` 가 **24x17 로 고정**되어 있는데, 2F 모듈들은 실제 방 크기가 34~47 × 27~29 라서 collider 가 방보다 작음. [`MinimapRoomReveal`](LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapRoomReveal.cs:47) 이 그 collider 의 bounds 를 그대로 `MinimapFog.RevealBounds()` 에 넘기기 때문에 미니맵에는 24x17 만 그려짐.

영향 받는 prefab: 32개 (2F 모듈 30개 + 1F entry 2개). 1F 일반 모듈들은 이미 module 별 적정 크기로 설정돼 있음.

## 변경할 파일

**신규**: `LostMemory/Assets/_Project/Scripts/Editor/FixMinimapColliderBoundsTool.cs`

기존 [`AddNetworkObjectToMapModules.cs`](LostMemory/Assets/_Project/Scripts/Editor/AddNetworkObjectToMapModules.cs) 패턴 그대로 복제 (이미 같은 폴더 prefab 들을 일괄 처리하는 검증된 구조).

## 도구 동작

### 메뉴 항목
- `Tools > Lost Memory > Modules > [DRY RUN] List Minimap Collider Mismatch` — 변경 없이 스캔만, 보고만
- `Tools > Lost Memory > Modules > [APPLY] Fix Minimap Collider Bounds` — 확인 대화상자 후 실제 수정

### Prefab 1개당 처리
1. `PrefabUtility.LoadPrefabContents(path)` → 임시 root 인스턴스
2. `root.GetComponentsInChildren<Tilemap>(true)` 로 모든 Tilemap 수집 (decoration 포함)
3. 각 Tilemap 의 `localBounds` 를 `tilemap.transform.localToWorldMatrix` 로 world bounds 변환 후 `Encapsulate` 로 합치기. 빈 Tilemap (`cellBounds.size == zero`) 은 skip
4. `root.transform.Find("Minimap")` (또는 재귀 검색) 으로 Minimap 자식 GameObject 탐색
5. `Minimap` GameObject 의 `BoxCollider2D` 찾기 (없으면 skip + 경고 로그)
6. 현재 size 와 계산된 size 비교 — 차이 0.5 이하면 skip (이미 적절). 차이 크면:
   - `box.size = new Vector2(bounds.size.x, bounds.size.y)`
   - `box.offset = (Vector2)(bounds.center - minimapGO.transform.position)` (root local 좌표라 단순 차이)
7. `PrefabUtility.SaveAsPrefabAsset(root, path)`
8. `PrefabUtility.UnloadPrefabContents(root)` (try/finally)

### 안전장치
- DRY RUN 모드에서 처리 대상/skip 사유/실패 목록 모두 콘솔 출력 — 사용자가 확인 후 APPLY
- APPLY 도 `EditorUtility.DisplayDialog` 확인 통과해야 실행
- "차이 0.5 이하면 skip" → 기존 정상 1F 모듈들은 건드리지 않음 (회귀 차단)
- 한 prefab 실패해도 다음 prefab 진행 (try/catch)
- `AssetDatabase.StartAssetEditing()` / `StopAssetEditing()` 로 batch 효율
- 진행률 표시 `EditorUtility.DisplayProgressBar`

### 결과 출력 형식 (AddNetworkObjectToMapModules 와 동일 톤)
```
[FixMinimapColliderBounds] === 결과 (APPLIED) ===
[FixMinimapColliderBounds] 수정 대상: 32개 → 32개 적용 완료
  + Assets/_Project/Map/Modules/2F1R/Map_2F_1R_1_NS_EP.prefab  (24x17 → 47x29)
  + ...
[FixMinimapColliderBounds] 이미 적절한 크기 (건너뜀): N개
[FixMinimapColliderBounds] Minimap/BoxCollider 없음: N개
[FixMinimapColliderBounds] === 끝 ===
```

## Verification

1. **DRY RUN 먼저**: 콘솔에 32개 prefab (2F 30 + 1F entry 2) 가 처리 대상으로 떠야 함. 1F 일반 module 들은 "이미 적절" 카운트에 들어가야 함
2. **git commit 으로 백업** 후 APPLY 실행
3. **Sample prefab inspector 확인** — 예: `Map_2F_1R_1_NS_EP.prefab` 의 Minimap > BoxCollider2D.size 가 24x17 → ~47x29 로 변경됐는지
4. **Unity Play → 2F 던전 진입** → 미니맵에서 방 전체가 회색 pre-fill + 진입 시 잘림 없이 reveal 되는지
5. **1F 회귀 확인** — 1F 던전 진입해서 미니맵 그림이 기존과 동일한지 (collider 가 잘못 덮어쓰이지 않았는지)

## 의도적 비범위

- Minimap GameObject 의 위치/회전은 수정하지 않음 (size/offset 만)
- Tilemap 외 collider 기반 영역 (예: 통로용 별도 trigger) 은 자동 검출 안 함 — 필요하면 후속 ticket
- 1F entry 모듈 2개 (`_S_EP`) 도 24x17 이지만, 실제 작은 방이라 잘림 안 보일 가능성 — 차이 검출되면 자동으로 fix, 안 되면 skip

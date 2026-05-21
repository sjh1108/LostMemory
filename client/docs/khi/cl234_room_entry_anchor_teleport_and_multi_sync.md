# CL234 — 방 진입 시스템 단순화 + RoomExitWall 멀티 동기화

## Context

발표 D-7 멀티 작동 안정화 작업. 두 가지 동시 처리:

1. **방 진입 위치 단순화** — `RoomEntryAnchor` 강제 정렬을 보스방-only 였다가, 결국 *모든 방* 에 다시 적용하되 *전 플레이어 텔레포트* 로 확장. 한 명이 진입 트리거 밟으면 모든 팀원이 anchor 로 동시 텔레포트 → 멀티 팀원 분리 / 벽 잠금 후 입장 불가 문제 원천 차단.

2. **RoomExitWall 멀티 sync 버그 fix** — 기존: 호스트만 `GameObject.SetActive(true)` → 게스트는 출구 항상 열려있어 자유 탈출 가능. 변경: `NetworkVariable<bool> IsLocked` 로 상태 sync + 각 클라가 collider/renderer 토글.

추가로 **보상 선택 대기** 처리 (게스트가 RewardPanel 보는 중이면 텔레포트 보류).

## 수정 파일

### 1. `Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs`

**전환:**
- `MonoBehaviour` → `NetworkBehaviour`

**추가:**
- `public readonly NetworkVariable<bool> IsLocked` (server write / everyone read, default false)
- `public event Action<bool> ExitLockedStateChanged` — RoomExitWall 들이 구독
- `public override void OnNetworkSpawn()` — IsLocked.OnValueChanged 구독 + 현재 값 즉시 발행 (late join 대응)
- `public override void OnNetworkDespawn()` — 구독 해제
- `[ClientRpc] TeleportLocalPlayerClientRpc(Vector3 position, Vector2 facing)` — 호스트가 브로드캐스트, 각 클라가 자기 로컬 player 이동
- `WaitForRewardThenTeleport` 코루틴 — RewardPanel 활성 시 닫힐 때까지 대기
- `DoLocalPlayerTeleport` static 헬퍼 — `LocalPlayerResolver.LocalCharacter` + random offset(0.4u)
- `TeleportAllToAnchor(initiator, position, facing)` — IsSpawned 분기로 멀티/싱글 분기

**변경:**
- `ApplyInitContext`: 보스방-only 제거 → 모든 방에서 anchor 텔레포트 적용
- `SetExitWallsActive(bool)`: 직접 GameObject 토글 X → IsLocked.Value 토글 (싱글 fallback: ExitLockedStateChanged 직접 발행)
- `Awake()` 의 `SetExitWallsActive(false)` 호출 제거 (Wall 자체가 default false 로 시작)
- `OnDestroy()` → `public override void OnDestroy()` + `base.OnDestroy()` 추가 (NetworkBehaviour 컨벤션)

### 2. `Assets/_Project/Scripts/Runtime/Stage/RoomExitWall.cs`

**전환:**
- GameObject.SetActive 토글 X → `Collider2D.enabled` + `Renderer.enabled` 토글 방식
- 자식 트리 전체 `GetComponentsInChildren<Collider2D/Renderer>` 자동 탐색 (Inspector 비할당 시)

**추가:**
- 부모 트리의 `RoomEntryRuntimeController.ExitLockedStateChanged` 구독
- Awake 에서 default false (안 보이고 안 막힘) 적용
- controller.IsSpawned 이면 현재 값 즉시 적용 (late init 안전)
- OnDestroy 에서 이벤트 해제

### 3. `Assets/_Project/Scripts/Editor/AddNetworkObjectToMapModules.cs` (신규)

`Map/Modules/` 하위 prefab 일괄 NetworkObject 부착 Editor 도구.
- `Tools > Lost Memory > Modules > [DRY RUN] List Modules Needing NetworkObject`
- `Tools > Lost Memory > Modules > [APPLY] Add NetworkObject To Map Modules`
- 안전 패턴: `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` (GlobalObjectIdHash 자동 처리)

**실행 결과 (2026-05-19):**
- 처리: 66개 모듈에 NetworkObject 추가 완료
- 스킵: 69개 (controller 없는 deco/bridge)
- 실패: 0개

## 동작 흐름

### 싱글플레이
```
누군가 진입 트리거 밟음
  → BeginRoomEntry(initiator) [IsAuthority=true]
  → TeleportAllToAnchor → IsSpawned=false 분기 → initiator 직접 AlignCharacterTo
  → SetExitWallsActive(true) → IsSpawned=false 분기 → ExitLockedStateChanged 직접 발행
  → 각 RoomExitWall: collider+renderer 활성화
```

### 멀티플레이 (NetworkPrefabsList + Spawn 작업 완료 후)
```
누군가 진입 트리거 밟음 (호스트 측 Physics 감지)
  → BeginRoomEntry(initiator) [호스트만 IsAuthority]
  → TeleportAllToAnchor → TeleportLocalPlayerClientRpc 브로드캐스트
  → 각 클라: RewardPanel 열려있나? 
      → 열려있음: WaitForRewardThenTeleport (RewardSelected 이벤트 1회 구독 + 패널 닫힘 대기)
      → 닫혀있음: 즉시 DoLocalPlayerTeleport
  → LocalPlayerResolver.LocalCharacter + random offset 0.4u → AlignCharacterTo
  → PlayerMovementSync (Owner-Auth NetworkTransform) 가 위치 자동 sync
  → SetExitWallsActive(true) → IsLocked.Value = true
  → 모든 클라: OnValueChanged → ExitLockedStateChanged 발행
  → 각 RoomExitWall: collider+renderer 활성화
```

## 남은 작업 (멀티 작동 완전화 위해)

### B. DungeonRunBootstrap 에서 module NetworkObject.Spawn() 호출 추가
DA Architect 가 module instantiate 한 직후 호스트가 spawn 해야 게스트로 sync 됨.

수정 위치: `DungeonRunBootstrap.OnSpawnedManagedObjects` (line 132~186 근처)
```csharp
NetworkObject no = moduleInstance.GetComponent<NetworkObject>();
if (no != null && HostAuthority.IsHost && !no.IsSpawned)
{
    no.Spawn();
}
```

### C. NetworkManager 의 Network Prefabs List 등록
66개 모듈 prefab 을 NetworkManager 의 Network Prefab List 에 추가. 안 하면 호스트가 Spawn 호출해도 NGO 가 어떤 prefab 인지 모름 → 게스트로 sync 실패.

→ 별도 Editor 스크립트 작성 권장 (60+ 개 수동 등록 부담).

### D. 게스트 측 던전 build 정책 확인
호스트가 spawn 하면 NGO 가 게스트 측에 동일 prefab instantiate (NetworkPrefabsList 에 있어야). 작동 확인 필요.

## 검증 시나리오

### 싱글 (현재 가능)
1. Play Mode → 던전 진입
2. 일반 방 트리거 밟음 → 캐릭터가 anchor 위치로 텔레포트
3. 보스방 진입 → 동일하게 anchor 위치
4. 진입 후 출구 벽 나타남 + 막힘
5. 적 처치 → 벽 사라짐
6. 진입 전엔 벽 안 보이는지 확인

### 멀티 (B, C 완료 후)
1. 호스트 + 게스트 접속
2. 초기 상태: 양쪽 모두 출구 벽 안 보이고 자유 통과
3. 누군가 진입 트리거 → 양쪽 동시에 anchor 로 텔레포트 + 벽 나타남
4. 게스트 출구 시도 → 막힘
5. 클리어 → 양쪽 벽 동시에 사라짐
6. 보상 선택 미완료 시 다음 방 진입해도 텔레포트 대기 → 카드 선택 후 자동 텔레포트

## 부수 작업 (같은 세션, 별도 영역)

### 단검 우클릭 대쉬 fix
- `KhiDaggerTeleportController._weaponUpgrade` null 버그 fix (`GetComponentInParent` → `GetComponentInChildren`)
- 즉시 텔레포트 → 스무스 런지 + CircleCast 벽 검사 + FreeMovement 차단
- 마나/슬래시/데미지 부가 기능 유지

### Reward UI 개선
- `RewardPanelView`: 카드 등장 후 **1초 selection delay** (`_selectionDelay`) — 즉시 선택 방지
- `RewardCardView.SetInteractable`: CanvasGroup alpha 토글 (interactable=false 시 카드 전체 dim 0.4)
- `RewardCardEntrancePunch` (신규): 카드 OnEnable 마다 scale 0→1.17→1.0 elastic back-out (0.35초)

## 변경하지 않는 것
- `RoomEncounterAnchor` (적 스폰 포인트) — 유지
- `RoomEntryAnchor` 컴포넌트 자체 — 유지 (텔레포트 타겟)
- `RoomEntryZone` 트리거 — 유지
- 클리어 / 보상 흐름 (RewardController) — 유지
- `KhiDashController.PerformLunge` 등 기존 메서드 — 유지

## 참고 파일
- `docs/khi/multi_session_design_reply.md` — 멀티 작업 전체 컨텍스트
- `docs/khi/cl224_minimap_multiplayer_integration_plan.md` — 던전 빌드 멀티 선검증 단계
- `Assets/_Project/Scripts/Runtime/Networking/Player/LocalPlayerResolver.cs` — 로컬 player 단일 진입점 (Phase B-2~B-3)
- `Assets/_Project/Scripts/Runtime/Networking/Common/HostAuthority.cs` — 권위 판정 통일 API

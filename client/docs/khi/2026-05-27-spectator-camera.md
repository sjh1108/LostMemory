# Defeated 시 관전 카메라 (자동 follow + Q/E cycle)

## Context

`KhiDownController.AnyPlayerDefeated` static event 도입 + portal ready 게이트 Defeated 제외
작업으로 "한 명 죽어도 팀원이 진행 가능" 흐름이 완성됨. 다만 죽은 사람의 카메라가 자기 시체를
계속 따라가는 상태라 게임에서 빠진 느낌이 강함.

**목표**: Defeated 즉시 카메라가 살아있는 팀원 character.transform 으로 swap. 죽은 사람도
팀이 다음 방까지 가는 모습을 함께 보며 결과창 진입.

## 현재 카메라 구조 (확인 완료)

- **`KhiPlayerCamera`** (Main Camera 부착) — SmoothDamp + deadzone follow.
  `SetFollowTarget(Transform)` API 보유.
- **`TestKhiInputManager.FollowMainCamera()`** — 매 프레임 자기 `_followTarget` 을
  `KhiPlayerCamera.SetFollowTarget` 으로 전달. 또는 직접 transform.position 조작 (KhiPlayerCamera
  없는 케이스 폴백).
- **`_followTarget`** 은 `LinkCharactersForId` 에서 `character.PlayerID == 내 PlayerID`
  매치되는 Character.transform 으로 1회 설정 + destroyed 됐을 때만 재link.
- **그룹 카메라 미사용** — TDE `MultiplayerCameraGroupTarget` 은 라이브러리에만 있고 Lost Memory
  씬엔 없음. 각 클라가 독립적으로 본인만 follow.

핵심: `FollowMainCamera` 가 매 프레임 `KhiPlayerCamera.SetFollowTarget(_followTarget)` 을
다시 덮어쓰므로 KhiPlayerCamera 직접 조작은 무효. **반드시 TestKhiInputManager 안에서 override
가능한 경로를 둬야 함**.

## 접근 — 2 phase

코드 구조 자체는 한 번에 설계하되 commit 은 phase 별로 분리해서 중간에 플레이 테스트 가능.

### Phase 1 — 자동 관전 (MVP)

본인 Defeated 시 가장 가까운 alive 팀원으로 자동 카메라 전환. cycle / UI 없음.

### Phase 2 — Q/E cycle + UI 오버레이

화살표 키로 다음/이전 alive 팀원 전환. 화면 하단에 `"X 님을 관전 중 [Q/E 전환]"` 안내.

## 핵심 변경 파일

### Phase 1

**1. 신규 `LostMemory/TestKhi/SpectatorCameraController.cs` (~100줄)**

```csharp
namespace LostMemory.TestKhi
{
    /// <summary>
    /// 본인이 Defeated 됐을 때 카메라를 살아있는 팀원으로 swap.
    /// TestKhiInputManager 의 SetCameraOverrideTarget 을 호출. 네트워크 동기 불필요 — 각 클라 독립.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Test Khi/Spectator Camera Controller")]
    public sealed class SpectatorCameraController : MonoBehaviour
    {
        [SerializeField] private TestKhiInputManager inputManager;

        // alive 팀원 캐시 — Phase 2 cycle 에서도 재사용.
        private readonly List<KhiDownController> _aliveTeammates = new();
        private bool _spectating;

        private void Awake()
        {
            if (inputManager == null) inputManager = FindObjectOfType<TestKhiInputManager>();
        }

        private void OnEnable() { KhiDownController.AnyPlayerDefeated += HandleAnyDefeated; }
        private void OnDisable() { KhiDownController.AnyPlayerDefeated -= HandleAnyDefeated; }

        private void HandleAnyDefeated(KhiDownController dc)
        {
            // 내가 Defeated 됐거나, 내가 관전 중인 사람이 Defeated 됐으면 재선택.
            Character me = LocalPlayerResolver.LocalCharacter;
            KhiDownController myDc = me != null ? me.GetComponentInChildren<KhiDownController>(true) : null;

            bool meIsDefeated = myDc != null && myDc.IsDefeated;
            if (!meIsDefeated && !_spectating) return; // 나도 안 죽었고 관전 중도 아니면 무시.

            RefreshAliveTeammates();
            if (_aliveTeammates.Count == 0)
            {
                // 전원 Defeated — 본인 위치 그대로 (RunFailed 결과창 진입 직전).
                inputManager?.SetCameraOverrideTarget(null);
                _spectating = false;
                return;
            }

            // 본인 위치 기준 가장 가까운 alive 팀원 선택. Phase 2 에서 cycle 도 같은 list 사용.
            Transform anchor = me != null ? me.transform : transform;
            KhiDownController nearest = FindNearestTo(anchor.position);
            inputManager?.SetCameraOverrideTarget(nearest.transform);
            _spectating = true;
        }

        // Phase 2 에서 노출될 public API — Phase 1 에선 private 으로 두고 cycle 진입 시 변경.
        private void RefreshAliveTeammates() { /* FindObjectsByType + !IsDefeated 필터 + 본인 제외 */ }
        private KhiDownController FindNearestTo(Vector3 pos) { /* sqrMagnitude 누적 비교 */ }
    }
}
```

**2. `LostMemory/TestKhi/TestKhiInputManager.cs` 수정 (~20줄 추가)**

```csharp
// 필드 추가 (private)
private Transform _cameraOverrideTarget;

// 공개 API — SpectatorCameraController 가 호출.
public void SetCameraOverrideTarget(Transform target)
{
    _cameraOverrideTarget = target;
}

// FollowMainCamera 안에서 _followTarget 사용처를 ternary 로 교체:
private void FollowMainCamera()
{
    if (!followPlayerWithMainCamera) return;
    Transform effective = _cameraOverrideTarget != null ? _cameraOverrideTarget : _followTarget;
    if (effective == null) return;
    // ... 이후 _followTarget 참조를 effective 로 교체
}
```

### Phase 2 (Phase 1 코드 위에 확장)

**1. `SpectatorCameraController.cs` 확장**

```csharp
// 새 필드
private int _currentSpectateIndex = -1;
[SerializeField] private KeyCode prevKey = KeyCode.Q;
[SerializeField] private KeyCode nextKey = KeyCode.E;

private void Update()
{
    if (!_spectating || _aliveTeammates.Count <= 1) return;
    if (Input.GetKeyDown(prevKey)) Cycle(-1);
    else if (Input.GetKeyDown(nextKey)) Cycle(+1);
}

private void Cycle(int delta)
{
    RefreshAliveTeammates();
    if (_aliveTeammates.Count == 0) return;
    _currentSpectateIndex = (_currentSpectateIndex + delta + _aliveTeammates.Count) % _aliveTeammates.Count;
    KhiDownController target = _aliveTeammates[_currentSpectateIndex];
    inputManager?.SetCameraOverrideTarget(target.transform);
}
```

**2. 신규 `LostMemory/UI/Spectator/SpectatorOverlayView.cs` (~50줄)**

화면 하단 TMP_Text 1개. `SpectatorCameraController` 이벤트 (또는 polling) 로 현재 관전 대상
이름 + `"[Q/E 로 전환]"` 안내. prefab 1개 (`SpectatorOverlay.prefab`) 신규 + Resources 자동
인스턴스화 패턴 재사용 (`InventoryToggleController` 처럼 SpectatorCameraController.Awake 에서
`Resources.Load<SpectatorOverlayView>("UI/SpectatorOverlay")` 폴백).

**3. 부활 처리**

`KhiDownController.AnyPlayerDefeated` 외에도 본인 부활 (`ApplyDownStateFromNetwork(Normal)`
또는 `CompleteRevive` 호출) 시 spectator 모드 종료가 필요.

→ `KhiDownController` 에 `AnyPlayerRevived` static event 추가 (Defeated → Normal 전이는 보통
디버그 외엔 없지만 Down → Normal 은 있음. SpectatorCameraController 는 본인이 부활하면
override 해제). 또는 Phase 2 에선 단순히 매 Update 에 본인 상태 검사 후 override 해제.

## 의도적으로 안 하는 것

- **네트워크 동기화** — 누가 누구를 관전하는지 다른 클라에 sync 안 함. 로컬 카메라만 변경.
- **그룹 카메라 도입** — 현재 1인 follow 구조 유지. 그룹 카메라 도입은 별도 큰 작업.
- **Defeated 부활** — 게임 규칙상 Defeated 는 영구. 부활 시나리오는 RunFailed 결과창에서
  처리되므로 spectator 종료 자체가 거의 안 일어남. 디버그 부활은 Phase 3 정도에서.
- **카메라 페이드 / 부드러운 lerp** — `KhiPlayerCamera.snapOnTargetChange` 가 이미 target
  변경 시 snap 처리. 필요하면 Phase 3 폴리시.

## 검증

### Phase 1
1. **솔로** — Defeated = RunFailed → 결과창. spectator 트리거 안 됨. 회귀 X.
2. **멀티 2인, 본인이 죽음** — 카메라가 다른 alive 팀원으로 즉시 swap. 그 팀원 따라다님.
3. **관전 중 그 팀원이 또 죽음** — `AnyPlayerDefeated` 재발화 → `RefreshAliveTeammates` →
   남은 alive 로 자동 전환. 0명이면 override 해제 (마지막 위치).
4. **부활 (Down → Normal 같은 경우)** — Defeated 가 아니라 Down 상태였다가 부활했다면
   `_spectating=false` 안 되므로 카메라가 남이 따라다닐 수 있음. Phase 2 또는 별도 fix 에서 처리.

### Phase 2
5. **Q/E 입력** — alive 팀원이 2명 이상일 때 cycle. 1명이면 무시.
6. **UI 오버레이** — 관전 시작 시 화면 하단에 `"OOO 님을 관전 중 [Q/E 전환]"` 표시.
7. **관전 대상 사망 시 자동 다음** — cycle index 가 list 변경에 맞춰 clamp.

## 후속 (선택)

- **Phase 3 폴리시** — 전환 시 페이드, 깜빡임 방지, 부활 시 본인 복귀, alive 0 시 UI 처리.
- **dev md 업데이트** — Phase 1, 2 각각 별도 commit 후 기존 `2026-05-27-portal-ready-gate-and-status-panel.md`
  에 섹션 추가 또는 새 파일.

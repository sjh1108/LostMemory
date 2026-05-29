using System.Collections.Generic;
using LostMemory.Networking.Player;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 본인이 Defeated 됐을 때 카메라를 살아있는 팀원으로 swap.
    /// `KhiDownController.AnyPlayerDefeated` static event 구독 후, 본인 자신이 Defeated 됐거나
    /// 현재 관전 중인 사람이 또 Defeated 되면 alive 팀원 중 가장 가까운 사람으로 카메라 override.
    ///
    /// TestKhiInputManager.SetCameraOverrideTarget 으로 위임 — KhiPlayerCamera 직접 조작 X
    /// (FollowMainCamera 가 매 프레임 덮어쓰기 때문).
    ///
    /// 네트워크 동기 불필요 — 각 클라가 자기 LocalCharacter 상태만 보고 독립 동작.
    /// Phase 1 (MVP): 자동으로 가장 가까운 alive 팀원 follow. cycle / UI 없음.
    /// Phase 2 에서 Q/E cycle + 오버레이 추가 예정.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Test Khi/Spectator Camera Controller")]
    public sealed class SpectatorCameraController : MonoBehaviour
    {
        [Tooltip("씬의 TestKhiInputManager. 미할당 시 Awake 에서 FindObjectOfType 으로 해결.")]
        [SerializeField] private TestKhiInputManager inputManager;

        [Tooltip("디버그 로그 출력.")]
        [SerializeField] private bool debugLogging = false;

        // alive 팀원 캐시 — Phase 2 cycle 도 이 리스트 그대로 사용.
        private readonly List<KhiDownController> _aliveTeammates = new();

        // 현재 spectator override 활성 여부. 본인 Defeated 또는 관전 대상이 또 Defeated 됐을 때만 true.
        private bool _spectating;

        private void Awake()
        {
            if (inputManager == null) inputManager = FindObjectOfType<TestKhiInputManager>();
        }

        private void OnEnable()
        {
            KhiDownController.AnyPlayerDefeated += HandleAnyPlayerDefeated;
        }

        private void OnDisable()
        {
            KhiDownController.AnyPlayerDefeated -= HandleAnyPlayerDefeated;
            // 안전망: 비활성화되면 override 해제 — 씬 전환 시 다음 씬에서 잔재 방지.
            if (_spectating && inputManager != null)
            {
                inputManager.SetCameraOverrideTarget(null);
            }
            _spectating = false;
        }

        // 어느 클라의 player 든 Defeated → 본인 상태와 관전 상태 검사 후 대상 재선택.
        private void HandleAnyPlayerDefeated(KhiDownController defeatedDc)
        {
            Character me = LocalPlayerResolver.LocalCharacter;
            KhiDownController myDc = ResolveMyDownController(me);

            bool meIsDefeated = myDc != null && myDc.IsDefeated;

            // 트리거 조건:
            // 1) 본인이 방금 Defeated 됐다 → 관전 시작.
            // 2) 이미 관전 중인데 관전 대상 또는 본인 외 누군가가 죽었다 → list 재구축 + 가장 가까운 사람 재선택.
            if (!meIsDefeated && !_spectating) return;

            RefreshAliveTeammates(myDc);

            if (_aliveTeammates.Count == 0)
            {
                // 전원 Defeated — 본인 위치 그대로 (RunFailed 결과창 진입 직전).
                ClearOverride();
                Log("전원 Defeated — spectator override 해제.");
                return;
            }

            Transform anchor = me != null ? me.transform : transform;
            KhiDownController nearest = FindNearestTo(anchor.position);
            if (nearest == null)
            {
                ClearOverride();
                return;
            }

            inputManager?.SetCameraOverrideTarget(nearest.transform);
            _spectating = true;
            Log($"관전 시작/갱신 — target='{nearest.gameObject.name}', alive count={_aliveTeammates.Count}.");
        }

        // 본인 KhiDownController 해결. LocalCharacter 자식/부모 어디 있어도 한 번 찾음.
        private static KhiDownController ResolveMyDownController(Character me)
        {
            if (me == null) return null;
            return me.GetComponent<KhiDownController>()
                ?? me.GetComponentInChildren<KhiDownController>(includeInactive: true)
                ?? me.GetComponentInParent<KhiDownController>();
        }

        // 씬 안 모든 KhiDownController 중 본인 제외 + !IsDefeated 인 사람 수집. 거리순 정렬 안 함
        // (FindNearestTo 가 단일 nearest 만 필요로 함). Phase 2 cycle 에선 정렬 / index 추가.
        private void RefreshAliveTeammates(KhiDownController myDc)
        {
            _aliveTeammates.Clear();
            KhiDownController[] all = FindObjectsByType<KhiDownController>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                KhiDownController dc = all[i];
                if (dc == null) continue;
                if (dc == myDc) continue;            // 본인 제외
                if (dc.IsDefeated) continue;         // Defeated 제외 (Down 은 포함 — 부활 가능)
                _aliveTeammates.Add(dc);
            }
        }

        private KhiDownController FindNearestTo(Vector3 pos)
        {
            float bestSqr = float.MaxValue;
            KhiDownController best = null;
            for (int i = 0; i < _aliveTeammates.Count; i++)
            {
                KhiDownController dc = _aliveTeammates[i];
                if (dc == null) continue;
                float dSqr = (dc.transform.position - pos).sqrMagnitude;
                if (dSqr < bestSqr) { bestSqr = dSqr; best = dc; }
            }
            return best;
        }

        private void ClearOverride()
        {
            if (inputManager != null) inputManager.SetCameraOverrideTarget(null);
            _spectating = false;
        }

        private void Log(string msg)
        {
            if (debugLogging) Debug.Log($"[SpectatorCameraController] {msg}", this);
        }
    }
}

using System;
using LostMemory.TestKhi;
using UnityEngine;

namespace LostMemory.Networking.Player
{
    /// <summary>
    /// 로컬 플레이어 식별 단일 진입점. UI/카메라/HUD 가 *내 플레이어* 를 찾을 때 여기를 본다.
    ///
    /// Phase B 단계별 동작:
    ///   - B-1 (현재): 골격 — 싱글 실행 시 씬에서 첫 번째 KhiPlayerStateAggregator 자동 검색
    ///   - B-2: 네트워크 spawn 시 OwnerClientId 기반 정확한 식별로 교체. 신규 플레이어 컴포넌트가
    ///          OnNetworkSpawn 에서 IsOwner 인 경우 <see cref="Register"/> 호출.
    ///
    /// UI 작업자 권장 패턴 (commonness/networking-integration-rules.md §4.3):
    ///
    /// <code>
    /// private void OnEnable() {
    ///     if (LocalPlayerResolver.LocalPlayer != null) {
    ///         BindToPlayer(LocalPlayerResolver.LocalPlayer);
    ///     } else {
    ///         LocalPlayerResolver.LocalPlayerReady += BindToPlayer;
    ///     }
    /// }
    /// private void OnDisable() {
    ///     LocalPlayerResolver.LocalPlayerReady -= BindToPlayer;
    /// }
    /// </code>
    /// </summary>
    public static class LocalPlayerResolver
    {
        private static KhiPlayerStateAggregator _localPlayer;

        /// <summary>
        /// 현재 클라이언트의 로컬 플레이어. 싱글 실행 중이면 씬의 첫 번째 aggregator 를 lazy-find.
        /// 멀티 실행 시엔 <see cref="Register"/> 로 명시 등록된 인스턴스 반환.
        /// 결과적으로 해당 인스턴스가 destroy 됐으면 null 반환.
        /// </summary>
        public static KhiPlayerStateAggregator LocalPlayer
        {
            get
            {
                if (_localPlayer == null)
                {
                    // 싱글 실행 fallback. Phase B-2 에서 네트워크 spawn 이 명시 Register 하면
                    // 이 fallback 은 거의 호출되지 않음.
                    _localPlayer = UnityEngine.Object.FindFirstObjectByType<KhiPlayerStateAggregator>();
                    if (_localPlayer != null)
                    {
                        try { LocalPlayerReady?.Invoke(_localPlayer); }
                        catch (Exception ex) { Debug.LogError($"[LocalPlayerResolver] LocalPlayerReady handler threw: {ex.Message}"); }
                    }
                }
                return _localPlayer;
            }
        }

        /// <summary>로컬 플레이어가 결정됐을 때 발화. 이미 결정된 후 구독하면 즉시 발화는 *안 함* — 호출 측이 LocalPlayer 직접 체크 권장.</summary>
        public static event Action<KhiPlayerStateAggregator> LocalPlayerReady;

        /// <summary>
        /// fallback 검색 없이 현재 명시 등록된 로컬 플레이어만 조회한다.
        /// 네트워크 UI는 잘못된 첫 번째 플레이어 바인딩을 피하기 위해 이 경로를 우선 사용한다.
        /// </summary>
        public static bool TryGetRegisteredLocalPlayer(out KhiPlayerStateAggregator player)
        {
            player = _localPlayer;
            return player != null;
        }

        /// <summary>
        /// Phase B-2 의 네트워크 플레이어 컴포넌트가 호출. 본인 클라이언트의 IsOwner 인 aggregator 를 등록.
        /// </summary>
        public static void Register(KhiPlayerStateAggregator player)
        {
            if (_localPlayer == player) return;
            _localPlayer = player;
            if (player != null)
            {
                try { LocalPlayerReady?.Invoke(player); }
                catch (Exception ex) { Debug.LogError($"[LocalPlayerResolver] LocalPlayerReady handler threw: {ex.Message}"); }
            }
        }

        /// <summary>플레이어 despawn / 씬 전환 시 호출. 같은 인스턴스만 해제.</summary>
        public static void Unregister(KhiPlayerStateAggregator player)
        {
            if (_localPlayer == player)
            {
                _localPlayer = null;
            }
        }

        /// <summary>
        /// 도메인 리로드 비활성 환경(<c>EnterPlayMode Settings → Reload Domain = false</c>)에서
        /// Play 모드 종료 후 정적 상태가 남는 문제 방지. Phase B-2 의 NetworkBehaviour 가
        /// OnNetworkDespawn 에서 호출하거나, Bootstrap 진입 시 호출.
        /// </summary>
        public static void Clear()
        {
            _localPlayer = null;
        }
    }
}

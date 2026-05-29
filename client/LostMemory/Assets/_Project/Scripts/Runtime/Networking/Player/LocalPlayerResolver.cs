using System;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Networking.Player
{
    /// <summary>
    /// 로컬 플레이어 식별 단일 진입점. UI/카메라/HUD/Combat 가 *내 플레이어* 를 찾을 때 여기를 본다.
    ///
    /// Phase B 단계별 동작:
    ///   - B-1 (역사): 골격 — 싱글 실행 시 씬에서 첫 번째 KhiPlayerStateAggregator 자동 검색
    ///   - B-2: 네트워크 spawn 시 OwnerClientId 기반 정확한 식별. PlayerMovementSync.OnNetworkSpawn
    ///          에서 IsOwner 인 경우 <see cref="Register"/> 호출
    ///   - B-3 (현재): TDE Character 기반 fallback + 컴포넌트 generic 조회 API 추가.
    ///          던전 분신 fix 후 SerializeField wiring 을 본 API 로 마이그레이션
    ///
    /// UI 작업자 권장 패턴:
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
    ///
    /// Player 의 컴포넌트 SerializeField wiring 대체 패턴:
    ///
    /// <code>
    /// // Before — SerializeField 직접 wiring (prefab serialization 제약으로 던전 NGO 환경에서 깨짐)
    /// [SerializeField] private BuildManager buildManager;
    ///
    /// // After — 동적 조회
    /// private BuildManager buildManager;
    /// private void OnEnable() {
    ///     buildManager = LocalPlayerResolver.GetComponentOnLocalPlayer&lt;BuildManager&gt;();
    /// }
    /// </code>
    /// </summary>
    public static class LocalPlayerResolver
    {
        private static KhiPlayerStateAggregator _localPlayer;
        private static Character _localCharacter;

        /// <summary>
        /// 현재 클라이언트의 로컬 플레이어 (KhiPlayerStateAggregator).
        /// 싱글 실행 중이면 씬의 첫 번째 aggregator 를 lazy-find.
        /// 멀티 실행 시엔 <see cref="Register"/> 로 명시 등록된 인스턴스 반환.
        /// 결과적으로 해당 인스턴스가 destroy 됐으면 null 반환.
        /// </summary>
        public static KhiPlayerStateAggregator LocalPlayer
        {
            get
            {
                if (_localPlayer == null)
                {
                    // 싱글 / Editor 단일 씬 fallback. 네트워크 spawn 이 명시 Register 하면 거의 호출 안 됨.
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

        /// <summary>
        /// 로컬 플레이어의 TDE <see cref="Character"/> 컴포넌트.
        ///
        /// 검색 우선순위:
        /// 1. 명시 Register 된 LocalPlayer (aggregator) 와 같은 GameObject 의 Character
        /// 2. 씬에서 첫 번째 Character (Editor 단일 씬 테스트 fallback — NGO 미활성 시)
        ///
        /// UI / Combat 작업자가 캐릭터 자체 (movement / health / character type 등) 가 필요한 경우 사용.
        /// </summary>
        public static Character LocalCharacter
        {
            get
            {
                if (_localCharacter != null) return _localCharacter;

                // 1. aggregator 등록된 경우 같은 GameObject 의 Character
                if (_localPlayer != null)
                {
                    _localCharacter = _localPlayer.GetComponent<Character>();
                    if (_localCharacter != null) return _localCharacter;
                }

                // 2. NGO 활성 시 fallback 차단 — Spawn race 시 게스트가 host Character 잡는 버그 방지.
                //    호출 측은 LocalPlayerReady 이벤트 대기 필요.
                //    솔로 / Editor 단일 씬은 IsListening=false 라 기존 fallback 진입.
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm != null && nm.IsListening) return null;

                // 3. 솔로 / Editor 단일 씬 fallback — 첫 번째 Character 검색
                _localCharacter = UnityEngine.Object.FindFirstObjectByType<Character>();
                return _localCharacter;
            }
        }

        /// <summary>
        /// 로컬 플레이어 GameObject 의 컴포넌트 T 조회.
        ///
        /// SerializeField 가 prefab serialization 제약 (prefab → scene 참조 불가) 으로 깨지는 환경에서
        /// 동적 조회로 대체. BuildManager / KhiWeaponPresenter / KhiPlayerAim 등 player-side 컴포넌트가 대상.
        /// </summary>
        public static T GetComponentOnLocalPlayer<T>() where T : Component
        {
            var character = LocalCharacter;
            return character != null ? character.GetComponent<T>() : null;
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
            _localCharacter = null; // cache invalidate — 다음 access 시 재검색
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
                _localCharacter = null;
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
            _localCharacter = null;
        }
    }
}

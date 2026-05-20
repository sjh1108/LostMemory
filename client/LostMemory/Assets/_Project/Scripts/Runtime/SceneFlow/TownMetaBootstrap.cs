using System.Threading.Tasks;
using LostMemory.Networking.Common;
using LostMemory.Networking.Session;
using UnityEngine;

namespace LostMemory.SceneFlow
{
    /// <summary>
    /// 마을(Town_solo / Test_MultiLobby) 진입 시 백엔드의 메타 진행 데이터를 fetch.
    ///
    /// 책임:
    ///   - 무기 마스터 + 본인 인벤토리(해금/장착) 캐시 채움 (<see cref="WeaponInventoryCache"/>)
    ///   - 재능 분배는 <c>TalentPanelView</c> 가 자기 책임으로 PullAsync — 본 컴포넌트는 미관여
    ///
    /// 부착:
    ///   - 마을 씬(Town_solo, Test_MultiLobby) 의 빈 GameObject 에 컴포넌트 부착
    ///   - 씬 안에 단 1개만 두기 — 중복 부착 시 같은 fetch 두 번
    ///
    /// 호출 시점:
    ///   - 씬 로드 후 Start() 1회. <see cref="SessionApiClient.IsLoggedIn"/> 체크 — 토큰 없으면 skip
    ///   - 로그인 보장은 <see cref="RelaySession.EnsureInitializedAsync"/> 가 멀티 진입 흐름에서 처리. 솔로 마을은 본 컴포넌트가 직접 호출
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Scene Flow/Town Meta Bootstrap")]
    public sealed class TownMetaBootstrap : MonoBehaviour
    {
        [Tooltip("로그인 미상태 시 자동 로그인 시도 (RelaySession.EnsureInitializedAsync). 솔로 마을 테스트에서 true 권장.")]
        [SerializeField] private bool _ensureLoginIfNeeded = true;

        private async void Start()
        {
            await BootstrapAsync();
        }

        private async Task BootstrapAsync()
        {
            // 로그인 보장 (옵션) — 멀티 흐름은 이미 RelaySession 이 보장하지만, 솔로 마을 직접 진입 시엔 미보장
            if (!SessionApiClient.IsLoggedIn)
            {
                if (!_ensureLoginIfNeeded)
                {
                    NetLog.Info("TownMeta", "Not logged in — skip fetch (ensureLogin=false)");
                    return;
                }

                try
                {
                    await RelaySession.EnsureInitializedAsync();
                }
                catch (System.Exception ex)
                {
                    NetLog.Warn("TownMeta", $"EnsureInitialized 실패 — skip fetch: {ex.Message}");
                    return;
                }
            }

            bool ok = await WeaponInventoryCache.FetchAllAsync();
            if (!ok)
            {
                NetLog.Warn("TownMeta", "WeaponInventoryCache.FetchAllAsync 실패 — 트리 UI 가 빈 상태로 보일 수 있음");
            }
        }
    }
}

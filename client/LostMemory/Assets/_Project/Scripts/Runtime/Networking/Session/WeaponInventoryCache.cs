using System.Collections.Generic;
using System.Threading.Tasks;
using LostMemory.Networking.Common;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 무기 마스터 트리 + 본인 인벤토리(해금/장착) 의 메모리 캐시.
    ///
    /// 책임:
    ///   - 마스터(`/master/weapons`) 1회 fetch → 트리 UI 구성용
    ///   - 본인 인벤토리(`/users/me/weapons`) — 해금 무기 set + 현재 장착 ID
    ///   - 무기 해금/장착 API 호출 후 캐시 갱신
    ///
    /// 사용처:
    ///   - 마을 진입 시 <see cref="FetchAllAsync"/> 1회 호출
    ///   - 무기 트리 UI: <see cref="Master"/>, <see cref="Unlocked"/>, <see cref="SelectedWeaponId"/> 참조
    ///   - 해금 버튼: <see cref="UnlockAsync"/>
    ///   - 장착 버튼: <see cref="SelectAsync"/>
    ///   - 런 시작 시: <see cref="SelectedWeaponId"/> 로 무기 wire
    /// </summary>
    public static class WeaponInventoryCache
    {
        /// <summary>무기 마스터 평탄 리스트 — parentWeaponId 로 클라가 트리 재구성. 앱 라이프타임 동안 1회만 fetch.</summary>
        public static SessionApiClient.WeaponMasterData[] Master { get; private set; }

        /// <summary>현재 장착 무기 ID. 회원가입 직후 기본값 1 (검).</summary>
        public static long SelectedWeaponId { get; private set; }

        /// <summary>본인이 해금한 weaponId 집합 — 조회는 O(1).</summary>
        public static readonly HashSet<long> Unlocked = new();

        /// <summary>
        /// 마스터(1회만) + 본인 인벤토리(매번) fetch. 마을 진입 시 호출.
        /// 실패 시 false — 호출자가 토스트 또는 재시도.
        /// </summary>
        public static async Task<bool> FetchAllAsync()
        {
            if (Master == null)
            {
                Master = await SessionApiClient.GetMasterWeaponsAsync();
                if (Master == null)
                {
                    NetLog.Warn("Weapon", "Master fetch 실패");
                    return false;
                }
                NetLog.Info("Weapon", $"Master fetch OK — {Master.Length} weapons");
            }

            var inv = await SessionApiClient.GetMyWeaponsAsync();
            if (inv == null)
            {
                NetLog.Warn("Weapon", "Inventory fetch 실패");
                return false;
            }

            SelectedWeaponId = inv.selectedWeaponId;
            Unlocked.Clear();
            if (inv.unlocks != null)
            {
                foreach (var u in inv.unlocks) Unlocked.Add(u.unlockNodeId);
            }
            NetLog.Info("Weapon", $"Inventory fetch OK — selected={SelectedWeaponId}, unlocked={Unlocked.Count}");
            return true;
        }

        /// <summary>
        /// 무기 해금. cost 는 클라가 계산해서 보냄.
        /// 성공 시 true + 캐시 갱신. 실패 시 (errorCode, false) — 호출자가 error.code 로 사용자 메시지 분기.
        /// </summary>
        public static async Task<UnlockOutcome> UnlockAsync(long weaponId, int consumedShards)
        {
            var resp = await SessionApiClient.UnlockWeaponAsync(weaponId, consumedShards);
            if (resp == null || !resp.success)
            {
                return new UnlockOutcome(false, resp?.error?.code);
            }
            Unlocked.Add(resp.data.unlockNodeId);
            NetLog.Info("Weapon", $"Unlock OK — weaponId={resp.data.unlockNodeId}");
            return new UnlockOutcome(true, null);
        }

        /// <summary>
        /// 장착 무기 갱신. 본인이 해금한 무기만 가능.
        /// 성공 시 true + 캐시 갱신.
        /// </summary>
        public static async Task<SelectOutcome> SelectAsync(long weaponId)
        {
            var resp = await SessionApiClient.SelectWeaponAsync(weaponId);
            if (resp == null || !resp.success)
            {
                return new SelectOutcome(false, resp?.error?.code);
            }
            SelectedWeaponId = resp.data.selectedWeaponId;
            NetLog.Info("Weapon", $"Select OK — selected={SelectedWeaponId}");
            return new SelectOutcome(true, null);
        }

        /// <summary>로그아웃 시 호출 — 다음 사용자가 같은 정적 캐시를 안 보도록.</summary>
        public static void Reset()
        {
            Master = null;
            SelectedWeaponId = 0;
            Unlocked.Clear();
        }

        public readonly struct UnlockOutcome
        {
            public readonly bool Success;
            public readonly string ErrorCode;   // 실패 시 백엔드 ErrorCode.name (예: WEAPON_SHARDS_INSUFFICIENT)

            public UnlockOutcome(bool success, string errorCode)
            {
                Success = success;
                ErrorCode = errorCode;
            }
        }

        public readonly struct SelectOutcome
        {
            public readonly bool Success;
            public readonly string ErrorCode;

            public SelectOutcome(bool success, string errorCode)
            {
                Success = success;
                ErrorCode = errorCode;
            }
        }
    }
}

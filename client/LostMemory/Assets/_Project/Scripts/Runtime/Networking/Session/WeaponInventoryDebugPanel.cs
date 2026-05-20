using System.Threading.Tasks;
using LostMemory.Networking.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 무기 인벤토리 검증용 디버그 패널 — 발표 D-1 의 임시 UI.
    ///
    /// 정식 무기 트리 UI 가 도입되기 전까지 본 컴포넌트로 4개 API 호출 검증:
    ///   - <see cref="WeaponInventoryCache.FetchAllAsync"/> — 마스터 + 인벤토리 fetch
    ///   - <see cref="WeaponInventoryCache.UnlockAsync"/> — 해금
    ///   - <see cref="WeaponInventoryCache.SelectAsync"/> — 장착
    ///
    /// 부착:
    ///   - Test prefab 루트에 본 컴포넌트 부착
    ///   - Inspector 에서 SerializeField 슬롯 채우기 (statusText / refreshButton / 각 unlock+select 버튼)
    ///   - Prefab 위치 권장: <c>Assets/_Project/Prefabs/Test/WeaponInventoryDebugPanel.prefab</c>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Weapon Inventory Debug Panel")]
    public sealed class WeaponInventoryDebugPanel : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TMP_Text statusText;

        [Header("Refresh")]
        [SerializeField] private Button refreshButton;

        [Header("Unlock Buttons (cost is sent as fixed value below)")]
        [SerializeField] private Button unlockDaggerButton;        // weaponId=2 (parent=검=1)
        [SerializeField] private Button unlockFlamethrowerButton;  // weaponId=4 (parent=활=3)
        [SerializeField] private Button unlockEnhancedStaffButton; // weaponId=6 (parent=스태프=5)

        [Header("Select Buttons")]
        [SerializeField] private Button selectSwordButton;     // weaponId=1
        [SerializeField] private Button selectDaggerButton;    // weaponId=2
        [SerializeField] private Button selectBowButton;       // weaponId=3
        [SerializeField] private Button selectStaffButton;     // weaponId=5

        [Header("Config")]
        [SerializeField, Min(1)] private int defaultConsumedShards = 5;

        private async void OnEnable()
        {
            if (refreshButton != null) refreshButton.onClick.AddListener(OnRefreshButtonClicked);
            if (unlockDaggerButton != null) unlockDaggerButton.onClick.AddListener(() => UnlockClicked(2));
            if (unlockFlamethrowerButton != null) unlockFlamethrowerButton.onClick.AddListener(() => UnlockClicked(4));
            if (unlockEnhancedStaffButton != null) unlockEnhancedStaffButton.onClick.AddListener(() => UnlockClicked(6));
            if (selectSwordButton != null) selectSwordButton.onClick.AddListener(() => SelectClicked(1));
            if (selectDaggerButton != null) selectDaggerButton.onClick.AddListener(() => SelectClicked(2));
            if (selectBowButton != null) selectBowButton.onClick.AddListener(() => SelectClicked(3));
            if (selectStaffButton != null) selectStaffButton.onClick.AddListener(() => SelectClicked(5));

            await RefreshAsync();
        }

        private void OnDisable()
        {
            if (refreshButton != null) refreshButton.onClick.RemoveAllListeners();
            if (unlockDaggerButton != null) unlockDaggerButton.onClick.RemoveAllListeners();
            if (unlockFlamethrowerButton != null) unlockFlamethrowerButton.onClick.RemoveAllListeners();
            if (unlockEnhancedStaffButton != null) unlockEnhancedStaffButton.onClick.RemoveAllListeners();
            if (selectSwordButton != null) selectSwordButton.onClick.RemoveAllListeners();
            if (selectDaggerButton != null) selectDaggerButton.onClick.RemoveAllListeners();
            if (selectBowButton != null) selectBowButton.onClick.RemoveAllListeners();
            if (selectStaffButton != null) selectStaffButton.onClick.RemoveAllListeners();
        }

        private async void OnRefreshButtonClicked()
        {
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            SetStatus("Fetching...");
            if (!SessionApiClient.IsLoggedIn)
            {
                SetStatus("Not logged in.");
                return;
            }
            bool ok = await WeaponInventoryCache.FetchAllAsync();
            if (!ok)
            {
                SetStatus("Fetch failed.");
                return;
            }
            RenderStatus();
        }

        private async void UnlockClicked(long weaponId)
        {
            SetStatus($"Unlocking weaponId={weaponId}...");
            var outcome = await WeaponInventoryCache.UnlockAsync(weaponId, defaultConsumedShards);
            if (!outcome.Success)
            {
                SetStatus($"Unlock 실패: {TalentWeaponErrorPolicy.ToUserMessage(outcome.ErrorCode)}");
                return;
            }
            RenderStatus($"Unlock OK — weaponId={weaponId}");
        }

        private async void SelectClicked(long weaponId)
        {
            SetStatus($"Selecting weaponId={weaponId}...");
            var outcome = await WeaponInventoryCache.SelectAsync(weaponId);
            if (!outcome.Success)
            {
                SetStatus($"Select 실패: {TalentWeaponErrorPolicy.ToUserMessage(outcome.ErrorCode)}");
                return;
            }
            RenderStatus($"Select OK — weaponId={weaponId}");
        }

        private void RenderStatus(string prefix = null)
        {
            string master = WeaponInventoryCache.Master == null ? "?" : WeaponInventoryCache.Master.Length.ToString();
            string unlocked = string.Join(",", WeaponInventoryCache.Unlocked);
            string text = $"master={master}  selected={WeaponInventoryCache.SelectedWeaponId}  unlocked=[{unlocked}]";
            if (!string.IsNullOrEmpty(prefix)) text = prefix + "\n" + text;
            SetStatus(text);
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
            NetLog.Info("WeaponDebug", text);
        }
    }
}

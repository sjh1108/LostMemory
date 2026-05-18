using System;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// CL-179: 캐릭터 base 능력치 통합 SO. CL-180 어댑터가 OnEnable 시점에 TDE 컴포넌트
    /// (CharacterMovement / Health / CharacterDash2D) 의 인라인 값에 주입 예정.
    /// 본 CL 시점엔 SO 만 존재 — 게임 미반영 (디자이너 안내 필요).
    /// 점프는 Khi 미사용으로 제외 (탑다운). 향후 점프 캐릭터 추가 시 필드 추가.
    /// </summary>
    public static class PlayerStatsDataEvents
    {
        public static event Action<PlayerStatsData> OnAssetSaved;
        internal static void RaiseAssetSaved(PlayerStatsData asset) => OnAssetSaved?.Invoke(asset);
    }

    [CreateAssetMenu(fileName = "PlayerStatsData",
                     menuName = "LostMemory/Player/Player Stats Data",
                     order = 20)]
    public class PlayerStatsData : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField, Min(0f)] private float _baseMoveSpeed = 7f;
        [SerializeField, Min(0f)] private float _baseMaxHealth = 100f;
        [SerializeField, Min(0f)] private float _baseDashCooldown = 1.5f;

        public string DisplayName      => _displayName;
        public float  BaseMoveSpeed    => _baseMoveSpeed;
        public float  BaseMaxHealth    => _baseMaxHealth;
        public float  BaseDashCooldown => _baseDashCooldown;

#if UNITY_EDITOR
        [ContextMenu("Save Current Values")]
        private void SaveCurrentValues()
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
            PlayerStatsDataEvents.RaiseAssetSaved(this);
            Debug.Log($"[PlayerStatsData] Saved: {name}");
        }
#endif
    }
}

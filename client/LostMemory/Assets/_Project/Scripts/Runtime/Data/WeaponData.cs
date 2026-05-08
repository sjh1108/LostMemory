using System;
using UnityEngine;

namespace LostMemory.Data
{
    /// <summary>
    /// 본 namespace 외부에서 WeaponData 라이프사이클을 hook 하기 위한 정적 이벤트.
    /// Editor 측 (WeaponDataPlayModeIsolator) 가 OnAssetSaved 구독해서 Auto-revert 시스템에 commit 신호 전달.
    /// Runtime → Editor 어셈블리 직접 참조 회피용.
    /// </summary>
    public static class WeaponDataEvents
    {
        public static event Action<WeaponData> OnAssetSaved;
        internal static void RaiseAssetSaved(WeaponData asset) => OnAssetSaved?.Invoke(asset);
    }

    /// <summary>
    /// 무기 한 종류의 콤보 데이터·시각 콘텐츠를 담는 ScriptableObject.
    /// Assets/_Project/ScriptableObjects/Weapons/ 아래에 .asset 파일로 저장한다.
    ///
    /// 책임 분리 (CL-090, 옵션 B):
    /// - 본 SO 가 담는 것: 무엇이 일어나는가 (timing, damage, hitbox), 어떤 그림이 나오는가 (slash frames, tint).
    /// - 본 SO 가 담지 않는 것: 슬래시 시각의 위치/회전/스케일.
    ///   → SlashSlot 의 위치 데이터는 Khi 플레이어 prefab 의 SlashRig 자식 GameObject (SlashSlot_1/2/3) 의
    ///     Transform 에서 결정된다. 이는 Scene 뷰 시각 편집 워크플로우를 보존하기 위한 의도.
    /// - hitbox 와 slash 시각 위치의 일치는 디자이너가 두 군데를 함께 조정해 보장한다.
    ///
    /// 라이브 튠:
    /// - Play 중 Inspector 에서 슬라이더 드래그하면 다음 attack 부터 즉시 반영된다 (Unity SerializedObject 기본 동작).
    /// - Play 중 변경값 영구 저장: SO 자산 우클릭 → "Save Current Values" ContextMenu 사용.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData_New", menuName = "LostMemory/Combat/WeaponData")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName = "Sword";

        [Header("Combo Common")]
        [SerializeField] private float _baseDamage = 10f;
        [SerializeField, Min(0f)] private float _comboInputWindow = 0.6f;
        [SerializeField, Min(0f)] private float _minimumChainInputDelay = 0.12f;
        [SerializeField, Min(0f)] private float _inputBufferDuration = 0.25f;

        [Tooltip("회전 후 적용되는 글로벌 hitbox/VFX 오프셋. 캐릭터 발 기준 → 몸통 중앙으로 올리고 싶을 때 (0, 0.5) 등. " +
                 "Quaternion 회전 뒤 더해지므로 좌/우/상/하 어느 방향이든 항상 같은 양만큼 시프트됨 (좌우 비대칭 X).")]
        [SerializeField] private Vector2 _globalHitboxPostRotationOffset = Vector2.zero;

        [Header("Visual Common")]
        [Tooltip("좌반평면 aim (aim.x<0) 시 SlashRig 에 flipX + 회전 보정 적용. sprite 비대칭 (한 방향 arc curl) 일 때 ON.")]
        [SerializeField] private bool _autoMirrorOnLeftAim = true;
        [SerializeField, Range(0.005f, 0.2f)] private float _frameInterval = 0.04f;

        [Header("Combo Steps (1타/2타/3타)")]
        [SerializeField] private AttackStepData[] _steps = Array.Empty<AttackStepData>();

        public string DisplayName => _displayName;
        public float BaseDamage => _baseDamage;
        public float ComboInputWindow => _comboInputWindow;
        public float MinimumChainInputDelay => _minimumChainInputDelay;
        public float InputBufferDuration => _inputBufferDuration;
        public bool AutoMirrorOnLeftAim => _autoMirrorOnLeftAim;
        public float FrameInterval => _frameInterval;
        public AttackStepData[] Steps => _steps;
        public Vector2 GlobalHitboxPostRotationOffset => _globalHitboxPostRotationOffset;

#if UNITY_EDITOR
        /// <summary>
        /// Play 모드에서 Inspector 변경한 값을 자산에 영구 저장 + Auto-revert 시스템에 commit 신호.
        ///
        /// 동작:
        /// 1. 현재 값을 디스크에 영구 기록 (Ctrl+S 한 효과)
        /// 2. WeaponDataEvents.OnAssetSaved 이벤트 발화 → Editor 측 PlayModeIsolator 가 구독해 snapshot 갱신
        ///    → Play 종료 시 본 자산은 자동 복원에서 제외 (이 시점 값 보존)
        ///
        /// 따라서: Save 안 누른 변경은 Play 종료 시 자동 폐기, Save 누른 값만 보존.
        /// </summary>
        [ContextMenu("Save Current Values")]
        private void SaveCurrentValues()
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
            WeaponDataEvents.RaiseAssetSaved(this);
            Debug.Log($"[WeaponData] Saved: {name}");
        }
#endif
    }

    /// <summary>
    /// 콤보 한 step 의 데이터 (1타/2타/3타 각자 한 인스턴스).
    /// WeaponData.Steps 배열 항목.
    /// </summary>
    [Serializable]
    public class AttackStepData
    {
        [Header("Identity")]
        [Tooltip("Inspector 식별용 라벨 (예: '1타: 빠른 사선'). 게임 로직과 무관.")]
        public string label = "1타";
        public int comboStep = 1;
        public float damageMultiplier = 1f;

        [Header("Timing")]
        [Min(0f)] public float startupDuration = 0.05f;
        [Min(0f)] public float activeDuration = 0.07f;
        [Min(0f)] public float recoveryDuration = 0.08f;
        public string animatorTrigger = "Attack_1";

        [Header("Hitbox (mechanical truth, Right baseline)")]
        [Tooltip("Right aim 기준 hitbox 의 플레이어 로컬 좌표. 다른 aim 방향은 런타임에 자동 회전됨.")]
        public Vector2 hitboxOffset;
        public Vector2 hitboxSize;

        [Header("Visual (frames + tint)")]
        [Tooltip("어떤 그림이 나올지. 어디에 보일지는 prefab 의 SlashSlot_N localPosition 에서 결정됨.")]
        public Sprite[] slashFrames;
        public Color slashTint = Color.white;
    }
}

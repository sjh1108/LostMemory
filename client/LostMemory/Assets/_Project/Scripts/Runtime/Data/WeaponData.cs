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

        [Header("Weapon Presenter Visual (CL-230: KhiWeaponPresenter procedural sword)")]
        [Tooltip("OFF (기본): KhiWeaponPresenter 가 자기 Inspector 필드값으로 검 sprite 생성. 본 SO 의 _presenter* 값은 무시됨. " +
                 "ON: 본 SO 의 _presenter* 값으로 procedural sprite 재생성. 단검처럼 무기별로 크기/색을 다르게 하고 싶을 때만 ON.")]
        [SerializeField] private bool _overridePresenterVisual = false;
        [SerializeField, Min(8)] private int _presenterWidthPx = 96;
        [SerializeField, Min(4)] private int _presenterHeightPx = 16;
        [SerializeField, Min(8f)] private float _presenterPixelsPerUnit = 64f;
        [SerializeField] private Color _presenterBladeColor = new Color(0.85f, 0.88f, 0.95f, 1f);
        [SerializeField] private Color _presenterHiltColor = new Color(0.40f, 0.25f, 0.10f, 1f);
        [SerializeField] private Color _presenterGuardColor = new Color(0.85f, 0.70f, 0.20f, 1f);
        [Tooltip("들고 있는 검의 orbit 반경 (캐릭터로부터의 거리). 단검은 짧게 0.3, 검은 기본 0.4. _overridePresenterVisual 가 ON 일 때만 적용.")]
        [SerializeField, Min(0f)] private float _presenterOrbitRadius = 0.4f;

        [Tooltip("들고 있는 무기의 외부 sprite (예: Dagger_2.png). " +
                 "할당되면 KhiWeaponPresenter 가 procedural sword 대신 본 sprite 사용. " +
                 "_overridePresenterVisual 와 무관하게 작동. KhiDaggerSpriteCycler 의 임시 override 보다는 낮은 우선순위.")]
        [SerializeField] private Sprite _weaponSprite;

        [Header("Secondary Weapon (CL-230: 쌍단검 등 보조 손)")]
        [Tooltip("ON: 두 번째 무기 GameObject(Weapon_Sub)도 활성화. 쌍단검 / dual wield 표현.\n" +
                 "OFF (기본): 단일 무기.\n" +
                 "WeaponUpgradeService 가 SO 교체 시 Weapon_Sub.SetActive(이 값) 호출.")]
        [SerializeField] private bool _useSecondaryWeapon = false;
        [Tooltip("보조 무기의 orbit 각도 오프셋 (메인 위치 대비). 예: -60 = 메인이 우측이면 보조는 좌측 60도. " +
                 "자세 3 (좌우 대칭 닌자) = -60 ~ -90 추천.")]
        [SerializeField, Range(-180f, 180f)] private float _secondaryOrbitAngleOffset = -60f;
        [Tooltip("ON (추천): 보조 무기의 swing 방향이 메인과 반대 (가위 모션). " +
                 "OFF: 메인과 같은 방향 (병렬 swing).")]
        [SerializeField] private bool _secondaryMirrorSwing = true;
        [Tooltip("두 칼날 사이의 벌어진 각도. 메인은 +spread/2, 보조는 -spread/2 회전 적용. " +
                 "양수: 두 칼끝이 위쪽 한 점으로 모임 (∧ 자세). 30 = 살짝, 60 = 강한 ∧, 90~120 = X자 교차, 180 = 완전 반대 방향. " +
                 "음수: 두 칼끝이 아래/바깥쪽으로 벌어짐 (∨ 또는 발산 자세). -30 = 살짝 벌어짐, -60 = 강한 발산. " +
                 "0 = 평행. _useSecondaryWeapon 가 ON 일 때만 적용. 검 모드(=false)에는 영향 없음.")]
        [SerializeField, Range(-180f, 180f)] private float _handAngleSpread = 0f;
        [Tooltip("두 자루의 orbit 위치를 aim 기준 대칭 시프트. 메인은 +spread/2, 보조는 -spread/2 위치 적용. " +
                 "0 = 둘 다 aim 방향(겹침), 60 = 살짝 위아래 분리, 120 = X 좌표 같음 (명확한 나란히), 180 = 캐릭터 정수직 위/아래. " +
                 "_secondaryOrbitAngleOffset 와 함께 적용됨 (그쪽은 보조만 추가 시프트). " +
                 "_useSecondaryWeapon 가 ON 일 때만 적용.")]
        [SerializeField, Range(-180f, 180f)] private float _handOrbitSpread = 0f;
        [Tooltip("메인 손이 역수 그립인지. ON 이면 메인 sprite 가 180도 회전 (칼끝이 캐릭터 쪽, 손잡이가 바깥). " +
                 "어쌔신/닌자 자세에서 한 손 정수 + 한 손 역수 비대칭 그립 표현용.")]
        [SerializeField] private bool _mainGripReverse = false;
        [Tooltip("보조 손이 역수 그립인지. ON 이면 보조 sprite 가 180도 회전. " +
                 "전형적 닌자 = 메인 정수(false) + 보조 역수(true).")]
        [SerializeField] private bool _secondaryGripReverse = false;

        [Header("Element (CL-230: 속성 시각)")]
        [Tooltip("이 무기의 속성. KhiSlashAnimator 가 WeaponElementCatalog 에서 색/glow/scroll 조회 후 슬래시 sprite 에 적용. " +
                 "None = 속성 효과 비활성 (기본 sprite 그대로).")]
        [SerializeField] private WeaponElement _currentElement = WeaponElement.None;

        public string DisplayName => _displayName;
        public float BaseDamage => _baseDamage;
        public float ComboInputWindow => _comboInputWindow;
        public float MinimumChainInputDelay => _minimumChainInputDelay;
        public float InputBufferDuration => _inputBufferDuration;
        public bool AutoMirrorOnLeftAim => _autoMirrorOnLeftAim;
        public float FrameInterval => _frameInterval;
        public AttackStepData[] Steps => _steps;
        public Vector2 GlobalHitboxPostRotationOffset => _globalHitboxPostRotationOffset;

        public bool OverridePresenterVisual => _overridePresenterVisual;
        public int PresenterWidthPx => _presenterWidthPx;
        public int PresenterHeightPx => _presenterHeightPx;
        public float PresenterPixelsPerUnit => _presenterPixelsPerUnit;
        public Color PresenterBladeColor => _presenterBladeColor;
        public Color PresenterHiltColor => _presenterHiltColor;
        public Color PresenterGuardColor => _presenterGuardColor;
        public float PresenterOrbitRadius => _presenterOrbitRadius;
        public Sprite WeaponSprite => _weaponSprite;

        public bool UseSecondaryWeapon => _useSecondaryWeapon;
        public float SecondaryOrbitAngleOffset => _secondaryOrbitAngleOffset;
        public bool SecondaryMirrorSwing => _secondaryMirrorSwing;
        public float HandAngleSpread => _handAngleSpread;
        public float HandOrbitSpread => _handOrbitSpread;
        public bool MainGripReverse => _mainGripReverse;
        public bool SecondaryGripReverse => _secondaryGripReverse;
        public WeaponElement CurrentElement => _currentElement;

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

        [Header("Slash Slot Transform Override (CL-230: 무기별 자세)")]
        [Tooltip("ON: 본 step 의 SlashSlot 위치/회전/크기를 아래 값으로 매 swing 마다 override. " +
                 "OFF (기본): prefab 의 SlashSlot transform 그대로 사용. 기존 무기는 OFF 유지로 동작 보존.")]
        public bool overrideSlashSlotTransform = false;
        [Tooltip("SlashSlot.localPosition. override ON 일 때만 적용.")]
        public Vector3 slashSlotLocalPosition = Vector3.zero;
        [Tooltip("SlashSlot.localRotation Z 각도 (degrees). 양수 = 반시계, 음수 = 시계.")]
        public float slashSlotRotationZ = 0f;
        [Tooltip("SlashSlot.localScale. (1,1,1) = 기본. (1.5, 1.5, 1) = 가로/세로 1.5배.")]
        public Vector3 slashSlotLocalScale = Vector3.one;

        [Header("Weapon Swing Multiplier (CL-230: KhiWeaponPresenter swing 호 배수)")]
        [Tooltip("KhiWeaponPresenter base swingArcDegrees 에 곱할 배수. " +
                 "1.0 = 기본 호, 1.6 = 큰 회전 (검 3타 마무리), 0.5 = 작은 호. " +
                 "각 step (1/2/3타) 마다 개별 설정 가능.")]
        [Min(0f)] public float swingArcMultiplier = 1f;
        [Tooltip("KhiWeaponPresenter base swingDuration 에 곱할 배수. " +
                 "1.0 = 기본 속도, 1.3 = 1.3배 느림 (검 3타 마무리), 0.7 = 빠른 swing.")]
        [Min(0f)] public float swingDurationMultiplier = 1f;

        [Header("Extra Slashes (CL-230: 본 step 에 동시 표시될 추가 슬래시)")]
        [Tooltip("ON: KhiSlashAnimator 의 extraSlashSlots 배열도 같이 활성화하여 다중 슬래시 표시. " +
                 "OFF (기본): 단일 슬래시 (메인 슬롯만). 단검 3타 같이 화려한 마무리에 ON.")]
        public bool enableExtraSlashes = false;
    }
}

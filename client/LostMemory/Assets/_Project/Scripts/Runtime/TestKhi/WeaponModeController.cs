using System;
using System.Collections;
using LostMemory.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    public enum WeaponMode
    {
        Sword = 0,
        Dagger = 1,
        Bow = 2,
        Staff = 3,
        Flamethrower = 4,
    }

    /// <summary>
    /// Q 키로 검 ↔ 활 모드를 토글. 각 모드에 속한 컴포넌트들의 enabled 와 시각 GameObject 의 active 를 일괄 전환.
    /// 비활성 모드의 컨트롤러는 입력을 무시하므로 좌/우 클릭이 동시에 두 모드를 발화하지 않음.
    /// 캐릭터 root 에 부착. 검·활 필드는 각각 정확한 타입으로 노출 — 사용자가 GameObject 드래그 시
    /// Unity 가 알아서 해당 타입 컴포넌트를 자동 매핑한다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("Lost Memory/Test Khi/Weapon Mode Controller")]
    public class WeaponModeController : MonoBehaviour
    {
        [Header("Initial Mode")]
        [SerializeField] private WeaponMode initialMode = WeaponMode.Sword;

        [Header("Sword Mode — 각 필드에 해당 컴포넌트 드래그 (빈 칸은 자동 무시)")]
        [SerializeField] private KhiMeleeComboController swordCombo;
        [SerializeField] private KhiMeleeHitbox swordHitbox;
        [SerializeField] private KhiAttackVisualPresenter swordAttackVisual;
        [SerializeField] private KhiSlashAnimator swordSlashAnim;
        [SerializeField] private KhiWeaponPresenter swordPresenter;
        [Tooltip("검 모드에서만 동작할 패링 컨트롤러. enabled=false 시 우클릭 입력을 무시한다.")]
        [SerializeField] private KhiParryController swordParry;
        [Tooltip("검 모드 활성 시 SetActive(true). 검 시각이 들어있는 GameObject (예: 자식 'Weapon'). 통째로 켜고 끄므로 자식·내부 컴포넌트 모두 함께 토글됨.")]
        [SerializeField] private GameObject swordObject;

        [Header("Bow Mode")]
        [SerializeField] private KhiBowController bowController;
        [SerializeField] private KhiBowPresenter bowPresenter;
        [Tooltip("활 모드 활성 시 SetActive(true). 활 시각이 들어있는 GameObject (예: 자식 'BowVisual').")]
        [SerializeField] private GameObject bowObject;

        [Header("Staff Mode")]
        [SerializeField] private KhiStaffController staffController;
        [SerializeField] private KhiStaffPresenter staffPresenter;
        [Tooltip("스태프 모드 활성 시 SetActive(true). 스태프 시각이 들어있는 GameObject (예: 자식 'StaffVisual').")]
        [SerializeField] private GameObject staffObject;

        [Header("Flamethrower Mode")]
        [SerializeField] private KhiFlamethrowerController flamethrowerController;
        [SerializeField] private KhiFlamethrowerPresenter flamethrowerPresenter;
        [Tooltip("화염방사기 콘 데미지·화상 적용 컴포넌트. 모드 활성/비활성에 따라 enabled 토글.")]
        [SerializeField] private KhiFlameZone flamethrowerZone;
        [Tooltip("화염방사기 모드 활성 시 SetActive(true). 무기 시각 + 분사 ParticleSystem 이 들어있는 GameObject.")]
        [SerializeField] private GameObject flamethrowerObject;

        [Header("Dagger Mode (CL-230: 검 컴포넌트 공유, SO 만 교체)")]
        [Tooltip("WeaponUpgradeService. Dagger 모드 진입 시 UpgradeToDagger(), 그 외 모드 진입 시 RevertToDefault() 자동 호출. " +
                 "Sword_Dagger.asset 으로 SO 교체 + 보조 단검 (Weapon_Sub) 활성/비활성 처리.")]
        [SerializeField] private LostMemory.Combat.WeaponUpgradeService weaponUpgradeService;

        [Header("Input")]
        [Tooltip("키보드 모드 전환 키 (백업). 기본 Q.")]
        [SerializeField] private Key switchKey = Key.Q;
        [Tooltip("마우스 휠 모드 전환 활성화. 휠 위/아래로 순환.")]
        [SerializeField] private bool enableMouseWheelSwitch = true;
        [Tooltip("마우스 휠 한 번에 여러 모드 전환되는 것 방지용 디바운스.")]
        [SerializeField, Min(0.05f)] private float wheelSwitchCooldown = 0.15f;

        [Header("Debug")]
        [SerializeField] private bool logModeChanges = false;

        private WeaponMode _currentMode;
        private float _lastWheelSwitchTime;

        public WeaponMode CurrentMode => _currentMode;

        /// <summary>모드가 바뀐 직후 발화. (newMode)</summary>
        public event Action<WeaponMode> ModeChanged;

        private void Awake()
        {
            ApplyMode(initialMode, fireEvent: false);
        }

        private void Start()
        {
            // 씬 전환 직후 스냅샷 복구 — PlayerHealthSnapshotter 와 동일 패턴.
            // 다음 프레임까지 대기해서 다른 컴포넌트 Start 가 끝난 뒤 안전하게 모드 교체.
            StartCoroutine(RestoreSnapshotNextFrame());
        }

        private IEnumerator RestoreSnapshotNextFrame()
        {
            yield return null;

            PlayerRunState runState = PlayerRunState.Instance;
            if (runState == null || !runState.HasSnapshot) yield break;

            PlayerSnapshot snap = runState.Snapshot;
            if (!snap.HasWeaponMode) yield break;

            WeaponMode targetMode = (WeaponMode)snap.WeaponMode;
            if (targetMode == _currentMode) yield break;

            // ApplyMode 가 WeaponUpgradeService.UpgradeToDagger/RevertToDefault 까지 자동 호출.
            ApplyMode(targetMode, fireEvent: true);
        }

        /// <summary>씬 전환 직전 현재 무기 모드를 스냅샷에 기록. StageRouteManager.CapturePlayerSnapshot 에서 호출.</summary>
        public void CaptureInto(ref PlayerSnapshot snapshot)
        {
            snapshot.HasWeaponMode = true;
            snapshot.WeaponMode = (int)_currentMode;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[switchKey].wasPressedThisFrame)
            {
                CycleMode(+1);
            }

            if (enableMouseWheelSwitch)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null && Time.unscaledTime > _lastWheelSwitchTime + wheelSwitchCooldown)
                {
                    float scrollY = mouse.scroll.ReadValue().y;
                    if (scrollY > 0.1f)
                    {
                        CycleMode(+1);
                        _lastWheelSwitchTime = Time.unscaledTime;
                    }
                    else if (scrollY < -0.1f)
                    {
                        CycleMode(-1);
                        _lastWheelSwitchTime = Time.unscaledTime;
                    }
                }
            }
        }

        public void ToggleMode()
        {
            CycleMode(+1);
        }

        /// <summary>다음/이전 모드로 순환. direction: +1 다음, -1 이전.</summary>
        public void CycleMode(int direction)
        {
            int count = System.Enum.GetValues(typeof(WeaponMode)).Length;
            int next = ((int)_currentMode + direction + count) % count;
            ApplyMode((WeaponMode)next, fireEvent: true);
        }

        public void SetMode(WeaponMode mode)
        {
            if (_currentMode == mode) return;
            ApplyMode(mode, fireEvent: true);
        }

        private void ApplyMode(WeaponMode mode, bool fireEvent)
        {
            _currentMode = mode;
            // CL-230: Dagger 도 검 컴포넌트 공유 (Sword 모드 + Dagger 모드 둘 다 sword 컴포넌트 활성).
            bool swordOrDaggerActive = mode == WeaponMode.Sword || mode == WeaponMode.Dagger;
            bool bowActive = mode == WeaponMode.Bow;
            bool staffActive = mode == WeaponMode.Staff;
            bool flamethrowerActive = mode == WeaponMode.Flamethrower;

            SetEnabled(swordCombo, swordOrDaggerActive);
            SetEnabled(swordHitbox, swordOrDaggerActive);
            SetEnabled(swordAttackVisual, swordOrDaggerActive);
            SetEnabled(swordSlashAnim, swordOrDaggerActive);
            SetEnabled(swordPresenter, swordOrDaggerActive);
            // CL-230: 패링은 Sword 모드만. Dagger 모드는 우클릭이 텔레포트로 사용됨 (KhiDaggerTeleportController).
            SetEnabled(swordParry, mode == WeaponMode.Sword);
            SetActive(swordObject, swordOrDaggerActive);

            SetEnabled(bowController, bowActive);
            SetEnabled(bowPresenter, bowActive);
            SetActive(bowObject, bowActive);

            SetEnabled(staffController, staffActive);
            SetEnabled(staffPresenter, staffActive);
            SetActive(staffObject, staffActive);

            SetEnabled(flamethrowerController, flamethrowerActive);
            SetEnabled(flamethrowerPresenter, flamethrowerActive);
            SetEnabled(flamethrowerZone, flamethrowerActive);
            SetActive(flamethrowerObject, flamethrowerActive);

            // CL-230: Dagger 모드 진입 시 SO 교체 + 보조 단검 활성. 그 외 모드는 기본 검 SO 복귀 + 보조 단검 비활성.
            // 이 한 줄 분기로 "단검 → 활 전환 시 보조 단검 안 꺼지는 문제" 해결.
            if (weaponUpgradeService != null)
            {
                if (mode == WeaponMode.Dagger)
                {
                    weaponUpgradeService.UpgradeToDagger();
                }
                else
                {
                    weaponUpgradeService.RevertToDefault();
                }
            }

            if (logModeChanges)
            {
                Debug.Log($"[WeaponMode] → {mode}");
            }

            if (fireEvent)
            {
                ModeChanged?.Invoke(mode);
            }
        }

        private static void SetEnabled(Behaviour behaviour, bool enabled)
        {
            if (behaviour != null) behaviour.enabled = enabled;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}

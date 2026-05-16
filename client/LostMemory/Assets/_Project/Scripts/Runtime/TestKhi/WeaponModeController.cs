using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    public enum WeaponMode
    {
        Sword = 0,
        Bow = 1,
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

        [Header("Input")]
        [Tooltip("모드 전환 키. 기본 Q.")]
        [SerializeField] private Key switchKey = Key.Q;

        [Header("Debug")]
        [SerializeField] private bool logModeChanges = false;

        private WeaponMode _currentMode;

        public WeaponMode CurrentMode => _currentMode;

        /// <summary>모드가 바뀐 직후 발화. (newMode)</summary>
        public event Action<WeaponMode> ModeChanged;

        private void Awake()
        {
            ApplyMode(initialMode, fireEvent: false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard[switchKey].wasPressedThisFrame)
            {
                ToggleMode();
            }
        }

        public void ToggleMode()
        {
            ApplyMode(_currentMode == WeaponMode.Sword ? WeaponMode.Bow : WeaponMode.Sword, fireEvent: true);
        }

        public void SetMode(WeaponMode mode)
        {
            if (_currentMode == mode) return;
            ApplyMode(mode, fireEvent: true);
        }

        private void ApplyMode(WeaponMode mode, bool fireEvent)
        {
            _currentMode = mode;
            bool swordActive = mode == WeaponMode.Sword;
            bool bowActive = mode == WeaponMode.Bow;

            SetEnabled(swordCombo, swordActive);
            SetEnabled(swordHitbox, swordActive);
            SetEnabled(swordAttackVisual, swordActive);
            SetEnabled(swordSlashAnim, swordActive);
            SetEnabled(swordPresenter, swordActive);
            SetEnabled(swordParry, swordActive);
            SetActive(swordObject, swordActive);

            SetEnabled(bowController, bowActive);
            SetEnabled(bowPresenter, bowActive);
            SetActive(bowObject, bowActive);

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

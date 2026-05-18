using System;
using LostMemory.Data;
using LostMemory.TestKhi;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// CL-230: 인런(in-run) 무기 업그레이드 서비스.
    ///
    /// 책임:
    /// - 씬에 1개 부착 (보통 SceneBootstrap / GameManager GameObject 또는 별도 _WeaponUpgrade GameObject).
    /// - WeaponData SO 카탈로그 보유 (현재는 defaultSword, daggerSword 2개).
    /// - 외부 트리거(아이템 픽업/이벤트 등)는 본 서비스의 UpgradeToDagger() / RevertToDefault() 만 호출.
    /// - 실제 SO 교체는 씬의 KhiMeleeComboController 를 찾아 SetWeaponData() 호출로 처리.
    ///
    /// 라이프사이클:
    /// - 씬 부착 컴포넌트. 씬 unload 시 destroy. _instance 자동 cleanup.
    /// - in-run 도중 단검 상태를 다음 씬으로 carry over 하려면 별도 RunState/PlayerSession 시스템에 저장 필요 (후속 작업).
    /// </summary>
    [AddComponentMenu("Lost Memory/Combat/Weapon Upgrade Service")]
    [DefaultExecutionOrder(-100)] // 다른 시스템보다 먼저 instance 등록되도록
    public class WeaponUpgradeService : MonoBehaviour
    {
        [Header("Weapon Catalog (Inspector 에서 SO 드래그)")]
        [Tooltip("기본 검 SO. RevertToDefault() 의 타겟. 비워두면 Awake 에 controller 의 현재 weaponData 를 baseline 으로 캡처.")]
        [SerializeField] private WeaponData defaultSword;

        [Tooltip("단검 SO. Sword_Dagger.asset 드래그. UpgradeToDagger() 의 타겟.")]
        [SerializeField] private WeaponData daggerSword;

        [Tooltip("닌자 단검 SO (정수+역수 비대칭 그립). Sword_DaggerNinja.asset 드래그. UpgradeToDaggerNinja() 의 타겟.")]
        [SerializeField] private WeaponData daggerNinjaSword;

        [Header("Target")]
        [Tooltip("교체 대상 컨트롤러. 비워두면 Awake 에 FindFirstObjectByType 으로 자동 탐색.")]
        [SerializeField] private KhiMeleeComboController targetController;

        [Tooltip("CL-230: 쌍단검용 두 번째 무기 GameObject (Weapon_Sub). " +
                 "WeaponData.UseSecondaryWeapon 값에 따라 SetActive 토글. " +
                 "비워두면 토글 안 함 (단일 무기만 사용).")]
        [SerializeField] private GameObject secondaryWeaponObject;

        [Tooltip("CL-230: 단검 모드 진입 시 비활성화할 패링 컨트롤러. " +
                 "단검은 우클릭이 텔레포트로 사용되므로 패링과 충돌. " +
                 "검 모드 복귀 시 자동 재활성화. 비워두면 토글 안 함.")]
        [SerializeField] private LostMemory.TestKhi.KhiParryController swordParry;

        [Header("Behavior")]
        [Tooltip("true: 진행 중 swing 은 그대로 끝까지 진행, 다음 swing 부터 새 무기 적용 (권장).\n" +
                 "false: 진행 중 swing 즉시 abort 후 교체.")]
        [SerializeField] private bool finishCurrentSwingBeforeSwap = true;

        public enum WeaponKind { Default, Dagger, DaggerNinja }

        /// <summary>현재 controller 의 SO 가 어떤 종류인지.</summary>
        public WeaponKind CurrentKind { get; private set; } = WeaponKind.Default;

        /// <summary>(kind, weaponData) — UI / 사운드 / 도전과제 구독 지점. UpgradeToDagger/RevertToDefault 성공 시 발화.</summary>
        public event Action<WeaponKind, WeaponData> WeaponUpgraded;

        private static WeaponUpgradeService _instance;
        public static WeaponUpgradeService Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[WeaponUpgradeService] 중복 인스턴스. 본 인스턴스 destroy. (existing on '{_instance.name}', new on '{name}')", this);
                Destroy(this);
                return;
            }
            _instance = this;

            if (targetController == null)
            {
                targetController = FindFirstObjectByType<KhiMeleeComboController>(FindObjectsInactive.Include);
            }

            // defaultSword 미지정 시 controller 의 현재 SO 캡처.
            if (defaultSword == null && targetController != null)
            {
                defaultSword = targetController.WeaponData;
            }

            // 초기 kind 추론: controller 의 현재 SO 가 어느 카탈로그 SO 와 일치하는지.
            if (targetController != null && daggerNinjaSword != null && targetController.WeaponData == daggerNinjaSword)
            {
                CurrentKind = WeaponKind.DaggerNinja;
            }
            else if (targetController != null && daggerSword != null && targetController.WeaponData == daggerSword)
            {
                CurrentKind = WeaponKind.Dagger;
            }
            else
            {
                CurrentKind = WeaponKind.Default;
            }

            // CL-230: 시작 시점에 controller 의 현재 SO 기준으로 보조 무기 GameObject 초기 sync.
            if (secondaryWeaponObject != null && targetController != null && targetController.WeaponData != null)
            {
                secondaryWeaponObject.SetActive(targetController.WeaponData.UseSecondaryWeapon);
            }

            // CL-230: 시작 시점에 패링 활성 상태 초기 sync (현재 무기 kind 기준).
            if (swordParry != null)
            {
                bool isDaggerMode = (CurrentKind == WeaponKind.Dagger || CurrentKind == WeaponKind.DaggerNinja);
                swordParry.enabled = !isDaggerMode;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 단검으로 업그레이드.
        /// 이미 단검이면 false (no-op, 이벤트 발화 X).
        /// daggerSword 미할당 또는 controller 미발견 시 false + LogError.
        /// </summary>
        public bool UpgradeToDagger()
        {
            if (CurrentKind == WeaponKind.Dagger)
            {
                return false;
            }
            if (daggerSword == null)
            {
                Debug.LogError("[WeaponUpgradeService] daggerSword 미할당. Inspector 에서 Sword_Dagger.asset 을 드래그하세요.", this);
                return false;
            }
            return ApplyWeapon(daggerSword, WeaponKind.Dagger);
        }

        /// <summary>
        /// 닌자 단검 (정수+역수 비대칭 그립)으로 업그레이드.
        /// 이미 닌자 모드면 false.
        /// daggerNinjaSword 미할당 또는 controller 미발견 시 false + LogError.
        /// </summary>
        public bool UpgradeToDaggerNinja()
        {
            if (CurrentKind == WeaponKind.DaggerNinja)
            {
                return false;
            }
            if (daggerNinjaSword == null)
            {
                Debug.LogError("[WeaponUpgradeService] daggerNinjaSword 미할당. Inspector 에서 Sword_DaggerNinja.asset 을 드래그하세요.", this);
                return false;
            }
            return ApplyWeapon(daggerNinjaSword, WeaponKind.DaggerNinja);
        }

        /// <summary>
        /// 기본 검으로 복귀.
        /// 이미 기본이면 false (no-op).
        /// </summary>
        public bool RevertToDefault()
        {
            if (CurrentKind == WeaponKind.Default)
            {
                return false;
            }
            if (defaultSword == null)
            {
                Debug.LogError("[WeaponUpgradeService] defaultSword 미할당.", this);
                return false;
            }
            return ApplyWeapon(defaultSword, WeaponKind.Default);
        }

        private bool ApplyWeapon(WeaponData next, WeaponKind kind)
        {
            if (targetController == null)
            {
                targetController = FindFirstObjectByType<KhiMeleeComboController>(FindObjectsInactive.Include);
                if (targetController == null)
                {
                    Debug.LogError("[WeaponUpgradeService] KhiMeleeComboController 를 씬에서 찾지 못함.", this);
                    return false;
                }
            }

            targetController.SetWeaponData(next, finishCurrentSwingBeforeSwap);

            // CL-230: 쌍단검 등 보조 무기 GameObject 토글.
            if (secondaryWeaponObject != null)
            {
                secondaryWeaponObject.SetActive(next.UseSecondaryWeapon);
            }

            // CL-230: 단검/닌자 모드 시 패링 비활성 (우클릭이 텔레포트로 사용됨).
            if (swordParry != null)
            {
                bool isDaggerMode = (kind == WeaponKind.Dagger || kind == WeaponKind.DaggerNinja);
                swordParry.enabled = !isDaggerMode;
            }

            CurrentKind = kind;
            WeaponUpgraded?.Invoke(kind, next);
            return true;
        }
    }
}

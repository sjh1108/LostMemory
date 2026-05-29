using LostMemory.Stage;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LostMemory.Stage
{
    /// <summary>
    /// 시연 안전망 — Time.timeScale=0 또는 Character.Freeze 가 어떤 이유로 안 풀리는 경우 강제 복구.
    ///
    /// 두 가지 진입점:
    /// 1. 씬 전환 시 자동 복구 — sceneLoaded hook 에서 ForceRestore 호출.
    ///    사용자 시나리오 "맵 이동 후에도 멈춤 유지" 정확히 fix.
    /// 2. F12 단축키 — 비상용. 사용자가 인지 시 직접 복구.
    ///
    /// 복구 항목:
    /// - Time.timeScale = 1
    /// - 모든 Character.UnFreeze
    /// - 모든 RewardController.PanicRestore (timeScale + aim + input 복원)
    ///
    /// 자동 등록 — RuntimeInitializeOnLoadMethod 로 게임 시작 시 driver 생성.
    /// </summary>
    public static class EmergencyTimeScaleRecovery
    {
        private const KeyCode RecoveryHotkey = KeyCode.F12;

        // 자동 등록 활성 — AfterSceneLoad 는 NetworkManager.Awake 이후 호출, Init() 은 단일 GameObject 생성만 (NM 참조 0).
        // 시연 직전 게스트 멈춤 발생 시 F12 비상탈출 필요 — 재활성화.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;

            // Driver MonoBehaviour 자동 생성 — F12 입력 모니터링.
            var driverGo = new GameObject("[EmergencyTimeScaleRecovery]");
            UnityEngine.Object.DontDestroyOnLoad(driverGo);
            driverGo.AddComponent<EmergencyRecoveryDriver>();

            Debug.Log("[EmergencyRecovery] Initialized — F12 강제 복구 + 씬 전환 자동 복구 활성.");
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 씬 전환 시 자동 복구 — Time.timeScale=0 또는 freeze 상태 강제 reset.
            ForceRestore("scene-loaded");
        }

        /// <summary>
        /// 강제 복구 — 외부에서 호출 가능. F12 단축키, scene transition, 또는 진단 메뉴에서.
        /// </summary>
        public static void ForceRestore(string reason = "manual")
        {
            // 1. Time.timeScale 복구.
            if (Time.timeScale < 0.99f)
            {
                Debug.Log($"[EmergencyRecovery] Time.timeScale={Time.timeScale} → 1.0 강제 복구 (reason={reason})");
                Time.timeScale = 1f;
            }

            // 2. 모든 RewardController PanicRestore — _isShowingReward + saved timeScale + aim + input 복원.
            var rewardControllers = UnityEngine.Object.FindObjectsByType<RewardController>(FindObjectsSortMode.None);
            for (int i = 0; i < rewardControllers.Length; i++)
            {
                if (rewardControllers[i] != null)
                {
                    rewardControllers[i].PanicRestore();
                }
            }

            // 3. 모든 Character.UnFreeze — BossIntroSequenceController.Freeze 또는 다른 Freeze 잔재 정리.
            var characters = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                var ch = characters[i];
                if (ch == null) continue;
                if (ch.CharacterType != Character.CharacterTypes.Player) continue;
                ch.UnFreeze();
            }

            Debug.Log($"[EmergencyRecovery] ForceRestore 완료 (reason={reason}) — players unfrozen + timeScale=1");
        }

        internal static KeyCode GetHotkey() => RecoveryHotkey;
    }

    /// <summary>
    /// F12 단축키 입력 모니터링 — Update 사용 위한 MonoBehaviour driver.
    /// EmergencyTimeScaleRecovery.Init 가 자동 생성.
    /// </summary>
    internal sealed class EmergencyRecoveryDriver : MonoBehaviour
    {
        // 시연용 진단 토글. heartbeat (5초마다) + Driver Start 로그 — 평소 콘솔 도배 방지 위해 default OFF.
        // F12 동작 자체엔 영향 없음. driver 동작 여부 확인 필요 시 true 로.
        private static bool LogHeartbeat = false;

        private float _nextHeartbeatTime;

        private void Start()
        {
            if (LogHeartbeat) Debug.Log("[EmergencyRecovery] Driver Start — F12 polling 활성. obj=" + gameObject.name);
        }

        private void Update()
        {
            // 5초마다 heartbeat — driver 가 살아있는지 grep 으로 확인.
            if (LogHeartbeat && Time.unscaledTime >= _nextHeartbeatTime)
            {
                _nextHeartbeatTime = Time.unscaledTime + 5f;
                Debug.Log($"[EmergencyRecovery] Driver heartbeat t={Time.unscaledTime:F1} — F12 polling alive");
            }

            bool pressed = false;

#if ENABLE_INPUT_SYSTEM
            // new input system — 키보드 directly poll.
            if (Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame)
            {
                pressed = true;
            }
#else
            // legacy input.
            if (Input.GetKeyDown(EmergencyTimeScaleRecovery.GetHotkey()))
            {
                pressed = true;
            }
#endif
            // legacy fallback try (new input + legacy 둘 다 활성화된 환경 대응).
            try
            {
                if (!pressed && Input.GetKeyDown(EmergencyTimeScaleRecovery.GetHotkey()))
                {
                    pressed = true;
                }
            }
            catch (System.InvalidOperationException)
            {
                // new input single mode — 무시.
            }

            if (pressed)
            {
                Debug.Log("[EmergencyRecovery] F12 pressed — ForceRestore 호출.");
                EmergencyTimeScaleRecovery.ForceRestore("F12 hotkey");
            }
        }
    }
}

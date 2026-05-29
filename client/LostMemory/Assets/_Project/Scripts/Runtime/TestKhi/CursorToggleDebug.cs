using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 시연/스크린샷용 마우스 커서 토글. F5 한 번 누르면 커서 숨김, 다시 누르면 보임.
    /// RuntimeInitializeOnLoadMethod 로 자동 spawn + DontDestroyOnLoad — scene 어디에도 prefab 변경 0.
    ///
    /// F5 는 원래 KhiDownController.debugForceDownKey 였으나 시연 도구로 양보 (_forceDownDebugEnabled=false).
    /// Cursor.visible 만 토글 — lockState 는 안 건드림 (UI/게임 입력 영향 X).
    /// </summary>
    public class CursorToggleDebug : MonoBehaviour
    {
        // Singleton — F5 입력이 여러 인스턴스에서 동시 토글되어 즉시 복귀되는 버그 방지.
        private static CursorToggleDebug _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // 이미 spawn 됐으면 중복 방지 (도메인 reload / Editor scene 재로드 / 다중 호출 대비)
            if (_instance != null) return;
            if (FindAnyObjectByType<CursorToggleDebug>(FindObjectsInactive.Include) != null) return;

            var go = new GameObject("[CursorToggleDebug]");
            go.AddComponent<CursorToggleDebug>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            // Awake 단계에서 중복 인스턴스 즉시 destroy — Bootstrap 가드가 race condition 으로
            // 뚫린 경우의 최후 방어선. 같은 frame 에 2번 Update 발화하는 케이스 차단.
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[CursorToggleDebug] 중복 인스턴스 감지 — destroy. (기존={_instance.gameObject.name}, 새={gameObject.name})", this);
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (IsTogglePressed())
            {
                bool newVisible = !Cursor.visible;
                Cursor.visible = newVisible;
                // lockState 도 같이 — Editor windowed 모드 / 다른 창 위 hover 시에도 효과 보장.
                // Confined = 게임 창 안 제한, 클릭 자유. None = 자유 이동.
                Cursor.lockState = newVisible ? CursorLockMode.None : CursorLockMode.Confined;
                Debug.Log($"[CursorToggleDebug] F5 토글 — Cursor.visible={Cursor.visible} lockState={Cursor.lockState}");
            }
        }

        /// <summary>
        /// F5 감지 — New Input System (Keyboard.current.f5Key) 우선, legacy Input.GetKeyDown fallback.
        /// KhiDownController.IsForceDownPressed 와 동일 패턴.
        /// </summary>
        private static bool IsTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.f5Key.wasPressedThisFrame) return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.F5)) return true;
            }
            catch (System.InvalidOperationException)
            {
                // New Input System 단독 모드 — 무시.
            }
            return false;
        }
    }
}

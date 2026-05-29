using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace LostMemory.UI
{
    /// <summary>
    /// 시연/스크린샷용 로딩 fade overlay. Title Canvas 디자인 재사용.
    /// scene 전환 시 자동으로 fade in/out — NGO 환경에서는 양쪽 클라이언트 sync,
    /// 솔로에서는 sceneLoaded 이벤트 hook.
    ///
    /// 사용법:
    ///   1. LoadingPanel prefab 을 Assets/_Project/Resources/UI/LoadingPanel.prefab 위치에 두기
    ///      (또는 Assets/Resources/UI/LoadingPanel.prefab — 어떤 Resources 폴더든 OK)
    ///   2. RuntimeInitializeOnLoadMethod 가 자동으로 instantiate + DontDestroyOnLoad
    ///   3. Show() / Hide() static API 또는 scene 전환 이벤트로 자동 토글
    ///
    /// NGO 양쪽 sync:
    ///   호스트가 NetworkManager.SceneManager.LoadScene 호출 시 NGO 의 OnSceneEvent 가
    ///   양쪽 클라이언트에서 fire — 양쪽 panel 동시 표시. ClientRpc 추가 불필요.
    /// </summary>
    public class LoadingFadeOverlay : MonoBehaviour
    {
        private static LoadingFadeOverlay _instance;
        private const string PrefabResourcePath = "UI/LoadingPanel";  // Resources 폴더 기준
        private const float FadeDuration = 0.3f;

        private CanvasGroup _canvasGroup;
        private Coroutine _fadeCoroutine;
        private bool _ngoHooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            if (FindAnyObjectByType<LoadingFadeOverlay>(FindObjectsInactive.Include) != null) return;

            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[LoadingFadeOverlay] Prefab not found at Resources/{PrefabResourcePath}. " +
                                 "Move LoadingPanel.prefab into a Resources/UI/ folder so Resources.Load can find it.");
                return;
            }

            var go = Instantiate(prefab);
            go.name = "[LoadingFadeOverlay]";
            DontDestroyOnLoad(go);

            // LoadingFadeOverlay 컴포넌트가 prefab 에 이미 있으면 중복 방지, 없으면 추가.
            if (go.GetComponent<LoadingFadeOverlay>() == null)
            {
                go.AddComponent<LoadingFadeOverlay>();
            }
        }

        private void Awake()
        {
            // Singleton 보장 — Bootstrap 가드가 race 로 뚫린 경우 최후 방어선.
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[LoadingFadeOverlay] 중복 인스턴스 감지 — destroy. (기존={_instance.gameObject.name})", this);
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // 솔로 fallback — NGO 미활성 환경에서 scene 로드 완료 시 자동 hide.
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            // NGO hook — NetworkManager 가 늦게 활성될 수 있어 Start 에서 처음 시도.
            HookNgoIfAvailable();
        }

        private void Update()
        {
            // NetworkManager 가 게임 도중 시작되는 케이스 (Relay join 등) 도 hook 시도.
            // 1초마다 한 번씩 — 비싸지 않음.
            if (!_ngoHooked && Time.frameCount % 60 == 0)
            {
                HookNgoIfAvailable();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnhookNgo();
        }

        private void HookNgoIfAvailable()
        {
            if (_ngoHooked) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SceneManager == null) return;

            nm.SceneManager.OnSceneEvent -= HandleNgoSceneEvent;
            nm.SceneManager.OnSceneEvent += HandleNgoSceneEvent;
            _ngoHooked = true;
            Debug.Log("[LoadingFadeOverlay] NGO SceneEvent hooked.");
        }

        private void UnhookNgo()
        {
            if (!_ngoHooked) return;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SceneManager != null)
            {
                nm.SceneManager.OnSceneEvent -= HandleNgoSceneEvent;
            }
            _ngoHooked = false;
        }

        private void HandleNgoSceneEvent(SceneEvent evt)
        {
            // 호스트가 LoadScene 호출 시 양쪽 클라 자동 fire — Show
            if (evt.SceneEventType == SceneEventType.Load)
            {
                ShowInternal();
            }
            // 새 scene 로드 완료 — Hide.
            // LoadEventCompleted = 모든 클라이언트 완료 신호 (서버 전용 이벤트지만 양쪽 fire 됨)
            // LoadComplete = 자기 클라이언트 완료
            else if (evt.SceneEventType == SceneEventType.LoadEventCompleted
                  || evt.SceneEventType == SceneEventType.LoadComplete)
            {
                HideInternal();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 솔로 환경 fallback — scene 로드 직후 panel 내려감.
            // NGO 환경에서도 추가 안전망 (LoadEventCompleted 가 어떤 사유로 안 오는 경우 대비).
            HideInternal();
        }

        /// <summary>
        /// 외부에서 명시 호출 — portal Button OnClick 등.
        /// NGO 환경에서 SceneEvent 가 자동 잡아도 중복 호출 무해 (idempotent).
        /// </summary>
        public static void Show() => _instance?.ShowInternal();

        public static void Hide() => _instance?.HideInternal();

        private void ShowInternal() => StartFade(1f);
        private void HideInternal() => StartFade(0f);

        private void StartFade(float target)
        {
            if (!isActiveAndEnabled) return;
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeTo(target));
        }

        private IEnumerator FadeTo(float target)
        {
            float start = _canvasGroup.alpha;
            float elapsed = 0f;
            _canvasGroup.blocksRaycasts = target > 0f;
            _canvasGroup.interactable = target > 0f;
            while (elapsed < FadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;  // timeScale=0 (일시정지) 중에도 fade 작동
                _canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / FadeDuration);
                yield return null;
            }
            _canvasGroup.alpha = target;
            _canvasGroup.blocksRaycasts = target > 0f;
            _canvasGroup.interactable = target > 0f;
            _fadeCoroutine = null;
        }
    }
}

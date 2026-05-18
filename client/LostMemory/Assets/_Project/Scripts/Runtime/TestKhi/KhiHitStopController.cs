using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-017 히트스톱 컨트롤러. Time.timeScale pulse.
    /// 씬 레벨 싱글톤. 외부(KhiCombatFeedbackBinder)에서 RequestFreeze로 호출.
    /// 중첩 호출 시 더 긴 요청만 승계 (짧은 요청은 무시).
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Hit Stop Controller")]
    [DefaultExecutionOrder(-200)]
    public class KhiHitStopController : MonoBehaviour
    {
        public static KhiHitStopController Instance { get; private set; }

        [SerializeField] private bool logTransitions = false;

        private float _freezeEndUnscaled;
        private float _restoreTimeScale = 1f;
        private bool _isFrozen;

        public bool IsFrozen => _isFrozen;
        public float FreezeRemaining => _isFrozen ? Mathf.Max(0f, _freezeEndUnscaled - Time.unscaledTime) : 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            RestoreIfFrozen();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnDisable()
        {
            RestoreIfFrozen();
        }

        private void OnApplicationQuit()
        {
            RestoreIfFrozen();
        }

        /// <summary>
        /// Time.timeScale을 `frozenScale`(기본 0)으로 일시 변경 후 duration(unscaled) 뒤에 복원.
        /// 이미 freeze 중이면 더 긴 요청만 승계, 짧은 요청은 무시.
        /// </summary>
        public void RequestFreeze(float duration, float frozenScale = 0f)
        {
            if (duration <= 0f)
            {
                return;
            }

            float desiredEnd = Time.unscaledTime + duration;

            if (!_isFrozen)
            {
                _restoreTimeScale = Time.timeScale;
                Time.timeScale = frozenScale;
                _isFrozen = true;
                _freezeEndUnscaled = desiredEnd;
                Log($"Freeze enter scale={frozenScale:F2} dur={duration:F3}");
                return;
            }

            if (desiredEnd > _freezeEndUnscaled)
            {
                _freezeEndUnscaled = desiredEnd;
                Log($"Freeze extended to dur≈{(desiredEnd - Time.unscaledTime):F3}");
            }
        }

        private void Update()
        {
            if (!_isFrozen)
            {
                return;
            }

            if (Time.unscaledTime >= _freezeEndUnscaled)
            {
                RestoreIfFrozen();
            }
        }

        private void RestoreIfFrozen()
        {
            if (!_isFrozen)
            {
                return;
            }

            Time.timeScale = _restoreTimeScale > 0f ? _restoreTimeScale : 1f;
            _isFrozen = false;
            _freezeEndUnscaled = 0f;
            Log("Freeze exit");
        }

        private void Log(string msg)
        {
            if (!logTransitions)
            {
                return;
            }

            Debug.Log($"[KhiHitStop] {msg} t={Time.unscaledTime:F3}");
        }
    }
}

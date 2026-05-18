using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI.Minimap
{
    /// <summary>
    /// 미니맵 HUD 루트. 코너 작은 맵(상시 표시) + M키 토글하는 큰 맵을 관리.
    /// 두 RawImage 모두 같은 RenderTexture 를 참조 — 한 번 렌더, 두 곳에 표시.
    /// 마커 오버레이는 각 맵마다 별도로 부착(RectTransform 크기가 달라 좌표 별도 계산).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Minimap/Minimap HUD")]
    public sealed class MinimapHUD : MonoBehaviour
    {
        [Header("Map Source")]
        [SerializeField, Tooltip("MinimapCameraRig 가 그리는 RenderTexture. 두 RawImage 가 공유.")]
        private RenderTexture minimapRenderTexture;

        [Header("Small (HUD) Map")]
        [SerializeField] private GameObject smallMapRoot;
        [SerializeField] private RawImage smallMapImage;

        [Header("Big Map (M키 토글)")]
        [SerializeField] private GameObject bigMapRoot;
        [SerializeField] private RawImage bigMapImage;

        [Header("Input")]
        [SerializeField] private KeyCode toggleBigMapKey = KeyCode.M;
        [SerializeField, Tooltip("씬 시작 시 큰 맵 표시 여부. 기본 false 권장.")]
        private bool startWithBigMapVisible = false;
        [SerializeField, Tooltip("큰 맵 띄울 때 게임 시간 일시정지 여부. 디아블로 스타일이라 기본 false.")]
        private bool pauseTimeWhenBigMapOpen = false;

        private bool _bigMapVisible;
        private float _previousTimeScale = 1f;

        public bool IsBigMapVisible => _bigMapVisible;
        public RenderTexture MinimapRenderTexture => minimapRenderTexture;

        private void Awake()
        {
            ApplyRenderTexture();
            SetBigMapVisible(startWithBigMapVisible, applyTimeScale: false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleBigMapKey))
            {
                ToggleBigMap();
            }
        }

        private void OnDisable()
        {
            // 큰 맵이 열린 채 비활성화되면 timeScale 이 0 으로 남는 것을 방지
            if (_bigMapVisible && pauseTimeWhenBigMapOpen)
            {
                Time.timeScale = _previousTimeScale;
            }
        }

        public void ToggleBigMap() => SetBigMapVisible(!_bigMapVisible);

        public void SetBigMapVisible(bool visible)
        {
            SetBigMapVisible(visible, applyTimeScale: true);
        }

        private void SetBigMapVisible(bool visible, bool applyTimeScale)
        {
            bool changed = _bigMapVisible != visible;
            _bigMapVisible = visible;

            if (bigMapRoot != null)
            {
                bigMapRoot.SetActive(visible);
            }

            if (applyTimeScale && pauseTimeWhenBigMapOpen && changed)
            {
                if (visible)
                {
                    _previousTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
                else
                {
                    Time.timeScale = _previousTimeScale;
                }
            }
        }

        public void SetMinimapRenderTexture(RenderTexture renderTexture)
        {
            minimapRenderTexture = renderTexture;
            ApplyRenderTexture();
        }

        private void ApplyRenderTexture()
        {
            if (smallMapImage != null)
            {
                smallMapImage.texture = minimapRenderTexture;
            }

            if (bigMapImage != null)
            {
                bigMapImage.texture = minimapRenderTexture;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyRenderTexture();
        }
#endif
    }
}

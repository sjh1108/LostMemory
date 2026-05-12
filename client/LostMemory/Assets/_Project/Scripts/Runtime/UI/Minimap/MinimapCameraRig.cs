using UnityEngine;

namespace LostMemory.UI.Minimap
{
    /// <summary>
    /// 미니맵 카메라(orthographic) 설정 + 던전 영역에 맞춰 자동 정렬.
    /// "Minimap" 레이어만 culling 하도록 인스펙터에서 구성.
    /// 2D 프로젝트(XY 평면) 기준 — 카메라는 +Z 방향을 보며 위치는 -Z 큰 값(메인 카메라 z=-10 보다 더 멀리).
    ///
    /// FitMode:
    /// - Manual: manualCenter / manualSize 인스펙터 값 사용. MVP 검증용.
    /// - AutoFromBounds: SetAutoBounds() 외부 호출로 bounds 갱신 (DungeonRunBootstrap.DungeonBuilt 시점 등).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Minimap/Minimap Camera Rig")]
    [RequireComponent(typeof(Camera))]
    public sealed class MinimapCameraRig : MonoBehaviour
    {
        public enum FitMode
        {
            Manual,
            AutoFromBounds
        }

        [Header("Camera")]
        [SerializeField, Tooltip("카메라가 렌더할 RenderTexture. MinimapHUD 에서 같은 RT 참조.")]
        private RenderTexture targetTexture;

        [SerializeField, Tooltip("\"Minimap\" 레이어만 포함하도록 설정 권장.")]
        private LayerMask cullingMask = ~0;

        [SerializeField] private Color backgroundColor = new Color(0.05f, 0.05f, 0.08f, 1f);

        [SerializeField, Tooltip("카메라의 z 위치. 메인 카메라(z=-10)보다 더 음수 권장. orthographic 이라 거리 자체는 영향 없음.")]
        private float cameraZ = -50f;

        [Header("Fit")]
        [SerializeField] private FitMode fitMode = FitMode.Manual;
        [SerializeField] private Vector2 manualCenter = Vector2.zero;
        [SerializeField, Min(1f)] private float manualSize = 20f;

        [SerializeField, Tooltip("AutoFromBounds 모드 진입 시 사용할 초기/현재 bounds.")]
        private Bounds autoBounds = new Bounds(Vector3.zero, new Vector3(40f, 40f, 1f));

        [SerializeField, Range(1f, 1.5f), Tooltip("자동 fit 시 가장자리 여유. 1.0=꽉 차게, 1.05=5% 여유.")]
        private float autoPadding = 1.05f;

        private Camera _camera;

        public RenderTexture TargetTexture => targetTexture;

        /// <summary>Manual fit 중심 좌표 (world space). MinimapFog 가 fog 영역 정의에 사용.</summary>
        public Vector2 ManualCenter => manualCenter;

        /// <summary>Manual fit 절반 크기 (world space). MinimapFog 가 fog 영역 크기에 사용.</summary>
        public float ManualSize => manualSize;

        /// <summary>현재 카메라의 world 중심 (x,y). 추적 모드에서 매 프레임 변함.</summary>
        public Vector2 GetCurrentCameraCenter()
            => new Vector2(transform.position.x, transform.position.y);

        /// <summary>현재 카메라의 orthographicSize. fit 모드에 따라 변할 수 있음.</summary>
        public float GetCurrentCameraOrthographicSize()
            => _camera != null ? _camera.orthographicSize : manualSize;

        private void Awake()
        {
            ConfigureCamera();
            ApplyFit();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ConfigureCamera();
            ApplyFit();
        }
#endif

        /// <summary>인스펙터 값이 외부에서 바뀐 경우 다시 적용.</summary>
        public void RefreshFit() => ApplyFit();

        /// <summary>
        /// 던전 빌드 후 외부에서 호출 — 던전 전체를 감싸는 bounds 로 카메라 자동 정렬.
        /// fitMode 가 AutoFromBounds 가 아니면 bounds 만 저장하고 fit 적용은 스킵.
        /// </summary>
        public void SetAutoBounds(Bounds bounds)
        {
            autoBounds = bounds;
            if (fitMode == FitMode.AutoFromBounds)
            {
                ApplyFit();
            }
        }

        public void SetFitMode(FitMode mode)
        {
            fitMode = mode;
            ApplyFit();
        }

        private void ConfigureCamera()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            if (_camera == null)
            {
                return;
            }

            _camera.orthographic = true;
            _camera.cullingMask = cullingMask;
            _camera.backgroundColor = backgroundColor;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.targetTexture = targetTexture;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 1000f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            // 메인 카메라와 다른 depth 로 두면 게임 화면 렌더 순서 충돌 회피
            _camera.depth = -1f;
        }

        private void ApplyFit()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            if (_camera == null)
            {
                return;
            }

            Vector3 center;
            float orthographicSize;

            if (fitMode == FitMode.AutoFromBounds)
            {
                center = autoBounds.center;
                float halfWidth = autoBounds.size.x * 0.5f;
                float halfHeight = autoBounds.size.y * 0.5f;
                orthographicSize = Mathf.Max(halfWidth, halfHeight) * autoPadding;
            }
            else
            {
                center = new Vector3(manualCenter.x, manualCenter.y, 0f);
                orthographicSize = manualSize;
            }

            transform.position = new Vector3(center.x, center.y, cameraZ);
            transform.rotation = Quaternion.identity;
            _camera.orthographicSize = Mathf.Max(1f, orthographicSize);
        }
    }
}

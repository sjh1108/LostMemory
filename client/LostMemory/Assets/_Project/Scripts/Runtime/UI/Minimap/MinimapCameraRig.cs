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
    /// - FollowTarget: MinimapAgent.All 중 Kind=PlayerLocal 자동 탐색, Vector3.Lerp 로 부드럽게 추적 (CL-226).
    ///
    /// LateUpdate 가 MinimapMarkerOverlay 보다 먼저 실행되도록 [DefaultExecutionOrder(-100)] —
    /// 추적 모드에서 카메라 위치 갱신 후 마커가 새 위치 기준으로 그려져 1프레임 어긋남 방지.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Minimap/Minimap Camera Rig")]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-100)]
    public sealed class MinimapCameraRig : MonoBehaviour
    {
        public enum FitMode
        {
            Manual,
            AutoFromBounds,
            FollowTarget
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

        [Header("Follow")]
        [SerializeField, Min(0.1f), Tooltip("Lerp 강도. 클수록 빠른 추적. 5 권장. (FollowTarget 모드 전용)")]
        private float followDamping = 5f;

        [Header("Fog Coverage")]
        [SerializeField, Tooltip("Fog mask 가 덮을 world 영역의 중심. 카메라 줌(manualSize)과 분리.")]
        private Vector2 fogCoverageCenter = Vector2.zero;
        [SerializeField, Min(1f), Tooltip("Fog mask 가 덮을 world 영역의 반변. 카메라 줌(manualSize)과 분리.")]
        private float fogCoverageHalfSize = 50f;

        private Camera _camera;
        private Transform _cachedFollowTarget;

        public RenderTexture TargetTexture => targetTexture;

        /// <summary>
        /// Fog mask 가 사용하는 영역 중심 (world space). 카메라 줌과 무관 — 던전 전체 커버용으로 따로 설정.
        /// MinimapFog 가 IsRevealedAtWorld / PaintCircle 좌표 변환에 사용.
        /// </summary>
        public Vector2 ManualCenter => fogCoverageCenter;

        /// <summary>
        /// Fog mask 가 덮는 영역의 반변 (world space). manualSize 와 분리되어 던전 전체를 커버.
        /// </summary>
        public float ManualSize => fogCoverageHalfSize;

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

        /// <summary>FollowTarget 모드 한정: 매 프레임 PlayerLocal 위치를 부드럽게 추적.</summary>
        private void LateUpdate()
        {
            if (fitMode != FitMode.FollowTarget || _camera == null)
            {
                return;
            }

            Transform target = GetFollowTarget();
            if (target == null)
            {
                return;
            }

            Vector3 current = transform.position;
            Vector3 desired = new Vector3(target.position.x, target.position.y, cameraZ);
            // damping 기반 부드러운 추적. Time.deltaTime * damping 가 1.0 을 넘으면 사실상 즉시.
            float t = Mathf.Clamp01(Time.deltaTime * followDamping);
            transform.position = Vector3.Lerp(current, desired, t);
        }

        /// <summary>MinimapAgent.All 에서 Kind=PlayerLocal 첫 번째 transform. 비활성/파괴되면 재탐색.</summary>
        private Transform GetFollowTarget()
        {
            if (_cachedFollowTarget != null && _cachedFollowTarget.gameObject.activeInHierarchy)
            {
                return _cachedFollowTarget;
            }

            var agents = MinimapAgent.All;
            for (int i = 0; i < agents.Count; i++)
            {
                if (agents[i] != null && agents[i].Kind == MinimapAgent.AgentKind.PlayerLocal)
                {
                    _cachedFollowTarget = agents[i].transform;
                    return _cachedFollowTarget;
                }
            }

            return null;
        }

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

        /// <summary>
        /// Fog 가 덮는 영역만 동적으로 설정. 카메라 줌(manualSize)은 변경하지 않음.
        /// 던전 빌드 후 RoomEntryRuntimeController 들의 bounds 로 호출되는 게 표준.
        /// </summary>
        public void SetFogCoverage(Vector2 center, float halfSize)
        {
            fogCoverageCenter = center;
            fogCoverageHalfSize = Mathf.Max(1f, halfSize);
        }

        private void Start()
        {
            AutoFitToRooms();
        }

        /// <summary>
        /// 씬의 모든 RoomEntryRuntimeController 자식 콜라이더(2D) bounds 를 union 해서
        /// ManualCenter/Size 를 자동 설정. fog 영역이 던전 전체를 덮도록 함.
        /// 방을 못 찾으면 인스펙터 기본값 유지.
        /// </summary>
        private void AutoFitToRooms()
        {
            var rooms = FindObjectsByType<LostMemory.Stage.RoomEntryRuntimeController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (rooms == null || rooms.Length == 0) return;

            bool init = false;
            Bounds union = default;
            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i] == null) continue;
                Collider2D[] cols = rooms[i].GetComponentsInChildren<Collider2D>(true);
                for (int c = 0; c < cols.Length; c++)
                {
                    if (cols[c] == null || !cols[c].enabled) continue;
                    Bounds b = cols[c].bounds;
                    if (!init) { union = b; init = true; }
                    else union.Encapsulate(b);
                }
                // 콜라이더가 없으면 transform.position 으로라도 fallback
                if (!init)
                {
                    union = new Bounds(rooms[i].transform.position, Vector3.zero);
                    init = true;
                }
                else
                {
                    union.Encapsulate(rooms[i].transform.position);
                }
            }
            if (!init) return;

            // 던전 가장자리 여유 패딩
            union.Expand(5f);

            Vector2 center = new Vector2(union.center.x, union.center.y);
            float half = Mathf.Max(union.extents.x, union.extents.y);
            SetFogCoverage(center, half);
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
            else if (fitMode == FitMode.FollowTarget)
            {
                // 추적 모드: size 는 manualSize 그대로. 위치는 target 있으면 그 위치, 없으면 manualCenter fallback.
                // 매 프레임 LateUpdate 가 위치 갱신 — 본 ApplyFit 은 초기 위치/모드 전환 시점에만 호출됨.
                orthographicSize = manualSize;
                Transform target = GetFollowTarget();
                center = target != null
                    ? new Vector3(target.position.x, target.position.y, 0f)
                    : new Vector3(manualCenter.x, manualCenter.y, 0f);
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

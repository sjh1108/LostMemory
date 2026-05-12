using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI.Minimap
{
    [System.Flags]
    public enum AgentKindMask
    {
        PlayerLocal = 1 << 0,
        PlayerRemote = 1 << 1,
        Enemy = 1 << 2,
        Boss = 1 << 3,
        Pickup = 1 << 4,
        Portal = 1 << 5,
    }

    /// <summary>
    /// world space fog of war 마스크.
    /// <para>
    /// MinimapAgent.All 중 revealKinds 에 매치되는 agent (보통 PlayerLocal/PlayerRemote) 의 위치 주변을
    /// 원형으로 reveal. 영구 reveal — 한 번 본 곳은 영원히 밝아짐 (alpha 더 낮은 값으로만 갱신).
    /// </para>
    /// <para>
    /// fog 마스크가 덮는 world 영역 = MinimapCameraRig.ManualCenter ± ManualSize 의 정사각.
    /// 추적 모드(CL-226) 에서도 카메라 위치만 변하지 fog 좌표계는 그대로 → 마스크 의미 무손실.
    /// SyncFogImageUVs 가 매 프레임 RawImage.uvRect 를 카메라 시야에 맞춰 갱신.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Minimap/Minimap Fog")]
    public sealed class MinimapFog : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField, Tooltip("MinimapCameraRig 참조. manualCenter/manualSize 기반 world↔mask 좌표 변환.")]
        private MinimapCameraRig cameraRig;

        [SerializeField, Tooltip("Fog 마스크가 표시될 RawImage 들 (SmallMap, BigMap 의 자식).")]
        private RawImage[] fogImages;

        [Header("Mask")]
        [SerializeField, Min(64), Tooltip("마스크 해상도 (정사각). 256 표준.")]
        private int maskResolution = 256;

        [SerializeField, Tooltip("Fog 색상. 안 본 영역의 픽셀 색.")]
        private Color fogColor = Color.black;

        [Header("Reveal")]
        [SerializeField, Min(0.1f), Tooltip("플레이어 주위 reveal 반경 (world unit).")]
        private float revealRadius = 5f;

        [SerializeField, Range(0f, 1f), Tooltip("0=단단한 원, 1=완전 그라데이션.")]
        private float falloff = 0.3f;

        [SerializeField, Range(1f, 30f), Tooltip("Mask SetPixels 갱신 빈도 (Hz). 10Hz 충분.")]
        private float updateHz = 10f;

        [SerializeField, Range(0f, 1f), Tooltip("이 임계값보다 어두우면 MarkerOverlay 가 마커 hide.")]
        private float visibilityThreshold = 0.5f;

        [Header("Filter")]
        [SerializeField, Tooltip("이 Kind 들의 agent 위치 주변을 reveal.")]
        private AgentKindMask revealKinds = AgentKindMask.PlayerLocal | AgentKindMask.PlayerRemote;

        private Texture2D _mask;
        private Color32[] _maskBuffer;
        private float _accumulator;

        public Texture2D MaskTexture => _mask;
        public float VisibilityThreshold => visibilityThreshold;

        private void OnEnable()
        {
            EnsureMask();
            ResetMask();
            BindToFogImages();
            _accumulator = 0f;
        }

        private void Update()
        {
            _accumulator += Time.deltaTime;
            float interval = 1f / Mathf.Max(updateHz, 0.1f);
            if (_accumulator < interval)
            {
                // throttle 중에도 uvRect 만은 매 프레임 갱신 (추적 모드 부드러움)
                SyncFogImageUVs();
                return;
            }
            _accumulator = 0f;

            UpdateReveal();
            if (_mask != null && _maskBuffer != null)
            {
                _mask.SetPixels32(_maskBuffer);
                _mask.Apply(false);
            }

            SyncFogImageUVs();
        }

        /// <summary>world position 이 reveal 됐는지 검사. MinimapMarkerOverlay 가 매 프레임 호출.</summary>
        public bool IsRevealedAtWorld(Vector3 worldPos)
        {
            if (_mask == null || _maskBuffer == null) return true;
            if (!TryWorldToMaskPixel(worldPos, out int x, out int y)) return false;

            byte a = _maskBuffer[y * maskResolution + x].a;
            return a < (byte)(visibilityThreshold * 255f);
        }

        /// <summary>
        /// 외부 호출: 직사각 world bounds 영역 전체를 영구 reveal.
        /// 방 진입 시 MinimapRoomReveal 등이 호출. 영구 누적이라 같은 영역 재호출은 무비용.
        /// </summary>
        public void RevealBounds(Bounds worldBounds)
        {
            EnsureMask();
            if (_maskBuffer == null || cameraRig == null) return;

            Vector2 fogCenter = cameraRig.ManualCenter;
            float fogHalf = cameraRig.ManualSize;
            if (fogHalf <= 0f) return;

            float fogSpan = fogHalf * 2f;

            // world bounds → fog 마스크 픽셀 범위
            float uMin = (worldBounds.min.x - (fogCenter.x - fogHalf)) / fogSpan;
            float uMax = (worldBounds.max.x - (fogCenter.x - fogHalf)) / fogSpan;
            float vMin = (worldBounds.min.y - (fogCenter.y - fogHalf)) / fogSpan;
            float vMax = (worldBounds.max.y - (fogCenter.y - fogHalf)) / fogSpan;

            int xMin = Mathf.Clamp((int)(uMin * maskResolution), 0, maskResolution - 1);
            int xMax = Mathf.Clamp((int)(uMax * maskResolution), 0, maskResolution - 1);
            int yMin = Mathf.Clamp((int)(vMin * maskResolution), 0, maskResolution - 1);
            int yMax = Mathf.Clamp((int)(vMax * maskResolution), 0, maskResolution - 1);

            // 영역 전체 완전 reveal (alpha = 0). 영구 누적 — 이미 reveal 된 픽셀은 변화 없음.
            bool anyChanged = false;
            for (int y = yMin; y <= yMax; y++)
            {
                int rowBase = y * maskResolution;
                for (int x = xMin; x <= xMax; x++)
                {
                    int idx = rowBase + x;
                    if (_maskBuffer[idx].a > 0)
                    {
                        _maskBuffer[idx].a = 0;
                        anyChanged = true;
                    }
                }
            }

            // 즉시 적용 — 다음 throttle 사이클 기다리지 않음
            if (anyChanged && _mask != null)
            {
                _mask.SetPixels32(_maskBuffer);
                _mask.Apply(false);
            }
        }

        /// <summary>외부에서 fog 마스크를 초기 상태(전부 검정)로 되돌림. 새 run 시작 등.</summary>
        public void ResetMask()
        {
            EnsureMask();
            if (_maskBuffer == null) return;

            Color32 fog = new Color32(
                (byte)(fogColor.r * 255f),
                (byte)(fogColor.g * 255f),
                (byte)(fogColor.b * 255f),
                255);
            for (int i = 0; i < _maskBuffer.Length; i++)
            {
                _maskBuffer[i] = fog;
            }
            if (_mask != null)
            {
                _mask.SetPixels32(_maskBuffer);
                _mask.Apply(false);
            }
        }

        /// <summary>world pos → fog 마스크 픽셀 좌표. fog 영역 = cameraRig.ManualCenter ± ManualSize.</summary>
        private bool TryWorldToMaskPixel(Vector3 worldPos, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (cameraRig == null) return false;

            Vector2 fogCenter = cameraRig.ManualCenter;
            float fogHalf = cameraRig.ManualSize;
            if (fogHalf <= 0f) return false;

            float fogSpan = fogHalf * 2f;
            float u = (worldPos.x - (fogCenter.x - fogHalf)) / fogSpan;
            float v = (worldPos.y - (fogCenter.y - fogHalf)) / fogSpan;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

            x = Mathf.Clamp((int)(u * maskResolution), 0, maskResolution - 1);
            y = Mathf.Clamp((int)(v * maskResolution), 0, maskResolution - 1);
            return true;
        }

        /// <summary>RawImage.uvRect 를 카메라 시야 영역에 맞춰 갱신. 추적 모드 호환 핵심.</summary>
        private void SyncFogImageUVs()
        {
            if (cameraRig == null || fogImages == null) return;

            Vector2 fogCenter = cameraRig.ManualCenter;
            float fogHalf = cameraRig.ManualSize;
            if (fogHalf <= 0f) return;

            float camHalf = cameraRig.GetCurrentCameraOrthographicSize();
            Vector2 camCenter = cameraRig.GetCurrentCameraCenter();

            float fogSpan = fogHalf * 2f;
            float uMin = (camCenter.x - camHalf - (fogCenter.x - fogHalf)) / fogSpan;
            float vMin = (camCenter.y - camHalf - (fogCenter.y - fogHalf)) / fogSpan;
            float uvSize = (camHalf * 2f) / fogSpan;

            Rect uvRect = new Rect(uMin, vMin, uvSize, uvSize);
            for (int i = 0; i < fogImages.Length; i++)
            {
                if (fogImages[i] != null)
                {
                    fogImages[i].uvRect = uvRect;
                }
            }
        }

        private void UpdateReveal()
        {
            IReadOnlyList<MinimapAgent> agents = MinimapAgent.All;
            for (int i = 0; i < agents.Count; i++)
            {
                MinimapAgent agent = agents[i];
                if (agent == null) continue;
                if (!HasKind(revealKinds, agent.Kind)) continue;

                PaintCircle(agent.WorldPosition);
            }
        }

        /// <summary>world center 주위 반경 revealRadius 의 원을 reveal. 영구 reveal (더 밝은 값으로만 갱신).</summary>
        private void PaintCircle(Vector3 worldCenter)
        {
            if (cameraRig == null || _maskBuffer == null) return;

            Vector2 fogCenter = cameraRig.ManualCenter;
            float fogHalf = cameraRig.ManualSize;
            if (fogHalf <= 0f) return;

            float fogSpan = fogHalf * 2f;
            float radiusVp = revealRadius / fogSpan;
            int radiusPx = Mathf.Max(1, (int)(radiusVp * maskResolution));

            float uCenter = (worldCenter.x - (fogCenter.x - fogHalf)) / fogSpan;
            float vCenter = (worldCenter.y - (fogCenter.y - fogHalf)) / fogSpan;
            int cx = (int)(uCenter * maskResolution);
            int cy = (int)(vCenter * maskResolution);

            float falloffStart = 1f - falloff;
            float safeFalloff = Mathf.Max(falloff, 0.001f);

            int xMin = Mathf.Max(0, cx - radiusPx);
            int xMax = Mathf.Min(maskResolution - 1, cx + radiusPx);
            int yMin = Mathf.Max(0, cy - radiusPx);
            int yMax = Mathf.Min(maskResolution - 1, cy + radiusPx);

            for (int y = yMin; y <= yMax; y++)
            {
                int rowBase = y * maskResolution;
                for (int x = xMin; x <= xMax; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / radiusPx;
                    if (dist > 1f) continue;

                    float reveal = (dist <= falloffStart)
                        ? 1f
                        : 1f - (dist - falloffStart) / safeFalloff;

                    byte newAlpha = (byte)((1f - reveal) * 255f);
                    int idx = rowBase + x;
                    if (newAlpha < _maskBuffer[idx].a)
                    {
                        _maskBuffer[idx].a = newAlpha;
                    }
                }
            }
        }

        private void EnsureMask()
        {
            if (_mask != null && _mask.width == maskResolution && _mask.height == maskResolution
                && _maskBuffer != null && _maskBuffer.Length == maskResolution * maskResolution)
            {
                return;
            }

            _mask = new Texture2D(maskResolution, maskResolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "MinimapFogMask",
            };
            _maskBuffer = new Color32[maskResolution * maskResolution];
        }

        private void BindToFogImages()
        {
            if (fogImages == null) return;
            for (int i = 0; i < fogImages.Length; i++)
            {
                if (fogImages[i] != null)
                {
                    fogImages[i].texture = _mask;
                    fogImages[i].color = Color.white;
                }
            }
        }

        private static bool HasKind(AgentKindMask mask, MinimapAgent.AgentKind kind)
        {
            AgentKindMask flag;
            switch (kind)
            {
                case MinimapAgent.AgentKind.PlayerLocal:  flag = AgentKindMask.PlayerLocal;  break;
                case MinimapAgent.AgentKind.PlayerRemote: flag = AgentKindMask.PlayerRemote; break;
                case MinimapAgent.AgentKind.Enemy:        flag = AgentKindMask.Enemy;        break;
                case MinimapAgent.AgentKind.Boss:         flag = AgentKindMask.Boss;         break;
                case MinimapAgent.AgentKind.Pickup:       flag = AgentKindMask.Pickup;       break;
                case MinimapAgent.AgentKind.Portal:       flag = AgentKindMask.Portal;       break;
                default: flag = 0; break;
            }
            return (mask & flag) != 0;
        }
    }
}

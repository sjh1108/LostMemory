using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI.Minimap
{
    /// <summary>
    /// 매 프레임 활성 MinimapAgent 들을 순회하며 마커 UI 인스턴스를 풀에서 꺼내 배치.
    /// 마커 위치 = MinimapCamera.WorldToViewportPoint → mapRect 내 anchoredPosition.
    /// 카메라/마커 prefab/맵 RectTransform 중 하나라도 없으면 silent skip.
    ///
    /// 같은 RenderTexture 가 여러 RawImage(코너 HUD + M키 큰 맵)에 표시될 때,
    /// 각 표시 영역마다 별도 MinimapMarkerOverlay 인스턴스를 두는 것을 권장
    /// (RectTransform 크기가 다르므로 마커 좌표도 별도로 계산되어야 함).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Minimap/Minimap Marker Overlay")]
    public sealed class MinimapMarkerOverlay : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField, Tooltip("미니맵을 렌더하는 카메라. WorldToViewportPoint 기준점.")]
        private Camera minimapCamera;

        [SerializeField, Tooltip("마커가 배치될 영역 RectTransform. 보통 RawImage(맵)의 RectTransform.")]
        private RectTransform mapRect;

        [SerializeField, Tooltip("마커 인스턴스의 부모. 비어있으면 mapRect 사용.")]
        private RectTransform markerParent;

        [SerializeField, Tooltip("마커 prefab. RectTransform + Image 컴포넌트 보유.")]
        private Image markerPrefab;

        [Header("Behavior")]
        [SerializeField, Tooltip("뷰포트 밖 마커 처리: true=가장자리 클램프, false=숨김.")]
        private bool clampOffscreen = true;

        [SerializeField, Min(0)] private int initialPoolSize = 16;

        [Header("Fog of War")]
        [SerializeField, Tooltip("fog 가시성 검사. null 이면 모든 마커 항상 표시 (CL-222 기본 동작). PlayerLocal/PlayerRemote 는 fog 와 무관하게 항상 표시.")]
        private MinimapFog fog;

        private readonly List<Image> _pool = new List<Image>();
        private readonly List<Image> _activeMarkers = new List<Image>();

        private void Awake()
        {
            if (markerParent == null)
            {
                markerParent = mapRect;
            }

            for (int i = 0; i < initialPoolSize; i++)
            {
                _pool.Add(CreateMarker());
            }
        }

        private void LateUpdate()
        {
            if (minimapCamera == null || mapRect == null || markerPrefab == null || markerParent == null)
            {
                return;
            }

            ReturnAllToPool();

            IReadOnlyList<MinimapAgent> agents = MinimapAgent.All;
            Rect rect = mapRect.rect;

            for (int i = 0; i < agents.Count; i++)
            {
                MinimapAgent agent = agents[i];
                if (agent == null)
                {
                    continue;
                }

                // icon fallback — agent.Icon 비어있으면 markerPrefab 의 sprite 사용 (CL-223 결정).
                Sprite spriteToUse = agent.Icon != null ? agent.Icon : markerPrefab.sprite;
                if (spriteToUse == null)
                {
                    continue;
                }

                Vector3 viewportPoint = minimapCamera.WorldToViewportPoint(agent.WorldPosition);

                bool offscreen = viewportPoint.x < 0f || viewportPoint.x > 1f
                              || viewportPoint.y < 0f || viewportPoint.y > 1f
                              || viewportPoint.z < 0f;

                if (offscreen)
                {
                    if (!clampOffscreen)
                    {
                        continue;
                    }

                    viewportPoint.x = Mathf.Clamp01(viewportPoint.x);
                    viewportPoint.y = Mathf.Clamp01(viewportPoint.y);
                }

                // fog 가시성 검사 — Player(Local/Remote) 외 agent 는 fog 가린 영역 skip.
                if (fog != null && !IsAlwaysVisible(agent.Kind))
                {
                    if (!fog.IsRevealedAtWorld(agent.WorldPosition))
                    {
                        continue;
                    }
                }

                Image marker = AcquireMarker();
                marker.sprite = spriteToUse;
                marker.color = agent.Tint;

                RectTransform rt = (RectTransform)marker.transform;
                rt.anchoredPosition = new Vector2(
                    (viewportPoint.x - 0.5f) * rect.width,
                    (viewportPoint.y - 0.5f) * rect.height);
                rt.localScale = Vector3.one * agent.IconScale;
                rt.localRotation = agent.GetMarkerRotation();

                // priority 가 높을수록 sibling 뒤쪽(앞에 그려짐)으로 이동
                marker.transform.SetSiblingIndex(marker.transform.parent.childCount - 1);

                marker.gameObject.SetActive(true);
            }
        }

        private Image CreateMarker()
        {
            Image marker = Instantiate(markerPrefab, markerParent);
            RectTransform rt = (RectTransform)marker.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            marker.gameObject.SetActive(false);
            marker.raycastTarget = false;
            return marker;
        }

        private Image AcquireMarker()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                Image candidate = _pool[i];
                if (candidate != null && !candidate.gameObject.activeSelf)
                {
                    _activeMarkers.Add(candidate);
                    return candidate;
                }
            }

            Image newMarker = CreateMarker();
            _pool.Add(newMarker);
            _activeMarkers.Add(newMarker);
            return newMarker;
        }

        private void ReturnAllToPool()
        {
            for (int i = 0; i < _activeMarkers.Count; i++)
            {
                Image m = _activeMarkers[i];
                if (m != null)
                {
                    m.gameObject.SetActive(false);
                }
            }

            _activeMarkers.Clear();
        }

        /// <summary>fog 가림과 무관하게 항상 표시되는 kind. Player 본인/팀원.</summary>
        private static bool IsAlwaysVisible(MinimapAgent.AgentKind kind)
            => kind == MinimapAgent.AgentKind.PlayerLocal
            || kind == MinimapAgent.AgentKind.PlayerRemote;
    }
}

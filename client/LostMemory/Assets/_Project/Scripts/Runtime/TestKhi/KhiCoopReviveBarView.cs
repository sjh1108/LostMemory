using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 협력 부활 차징바 — Down 상태인 player 머리 위에 진행률 표시.
    /// KhiStaffChargeBarView 패턴 — procedural 흰 사각형 sprite, asset 추가 0.
    /// KhiDownController.Awake() 가 동적으로 child GameObject 생성 + 본 컴포넌트 attach.
    /// prefab 수정 0 (NGO GlobalObjectIdHash 변화 0 — NetworkObject 가 아닌 자식 GO).
    ///
    /// 외부 API:
    ///   SetProgress(0~1) — > 0 이면 visible + fill 갱신. 0 이면 hide.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Coop Revive Bar View")]
    public class KhiCoopReviveBarView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer backgroundSprite;
        [SerializeField] private SpriteRenderer fillSprite;

        [SerializeField] private Color backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.75f);
        [SerializeField] private Color fillColor = new Color(0.25f, 0.85f, 1f, 0.95f);
        [SerializeField] private Color readyColor = new Color(0.6f, 1f, 0.85f, 1f);

        [SerializeField, Tooltip("Ready 도달 시 깜빡임 효과 시간.")]
        private float readyPulseDuration = 0.25f;

        private Vector3 _fillBaseScale;
        private bool _wasReady;
        private float _readyPulseStartedAt = -1f;
        private float _currentProgress;

        private static Sprite _cachedBarSprite;

        private void Awake()
        {
            // Awake 가 AddComponent 즉시 호출 — SerializeField wire 가 인스펙터 prefab 에서 wireup 된 경우만 작동.
            // CreateOnTransform 으로 동적 생성된 경우엔 sprite 필드가 아직 null 이므로 InitializeAfterAttach 가 보충.
            EnsureInitialized();
        }

        /// <summary>
        /// 1) prefab 에서 wireup 된 경우: Awake 가 자동 호출 (sprite 필드 set 상태).
        /// 2) 동적 생성 (CreateOnTransform): AddComponent 직후 sprite 필드 wireup 한 다음 외부에서 호출.
        /// 멱등 — _fillBaseScale 이 이미 (0,0,0) 이 아닌 유효값이면 다시 capture 안 함.
        /// </summary>
        public void EnsureInitialized()
        {
            if (backgroundSprite != null && backgroundSprite.sprite == null) backgroundSprite.sprite = GetBarSprite();
            if (fillSprite != null && fillSprite.sprite == null) fillSprite.sprite = GetBarSprite();

            if (backgroundSprite != null) backgroundSprite.color = backgroundColor;
            if (fillSprite != null)
            {
                fillSprite.color = fillColor;
                // _fillBaseScale 이 zero (uninitialized) 일 때만 capture — 그 후 SetProgress 가 매 프레임 덮어쓸 때 baseline 보존.
                if (_fillBaseScale == Vector3.zero)
                {
                    _fillBaseScale = fillSprite.transform.localScale;
                }
            }

            SetVisible(_currentProgress > 0f);
        }

        private void LateUpdate()
        {
            // PlayerHealthSync.ApplyLocalDeathReset 등 외부에서 자식 Renderer 일괄 enable 처리하는 경로 때문에
            // _currentProgress = 0 인데 enabled=true 잔재가 생길 수 있음 → 매 프레임 강제 enforce.
            bool shouldShow = _currentProgress > 0f;
            if (backgroundSprite != null && backgroundSprite.enabled != shouldShow) backgroundSprite.enabled = shouldShow;
            if (fillSprite != null && fillSprite.enabled != shouldShow) fillSprite.enabled = shouldShow;
        }

        /// <summary>
        /// 외부 호출 — 0 이면 hide, > 0 이면 visible + fill 갱신.
        /// KhiDownController 의 ReviveProgressChanged event + 네트워크 ClientRpc 양쪽에서 호출.
        /// </summary>
        public void SetProgress(float ratio01)
        {
            ratio01 = Mathf.Clamp01(ratio01);
            _currentProgress = ratio01;

            if (ratio01 <= 0f)
            {
                SetVisible(false);
                _wasReady = false;
                _readyPulseStartedAt = -1f;
                return;
            }

            SetVisible(true);

            bool ready = ratio01 >= 0.99f;

            if (fillSprite != null)
            {
                Vector3 s = _fillBaseScale;
                s.x = _fillBaseScale.x * ratio01;
                fillSprite.transform.localScale = s;

                Color targetColor = ready ? readyColor : fillColor;
                if (ready && _readyPulseStartedAt >= 0f && readyPulseDuration > 0f)
                {
                    float t = (Time.time - _readyPulseStartedAt) / readyPulseDuration;
                    if (t < 1f)
                    {
                        float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.5f;
                        targetColor = new Color(
                            Mathf.Clamp01(readyColor.r * pulse),
                            Mathf.Clamp01(readyColor.g * pulse),
                            Mathf.Clamp01(readyColor.b * pulse),
                            readyColor.a);
                    }
                }
                fillSprite.color = targetColor;
            }

            if (ready && !_wasReady)
            {
                _readyPulseStartedAt = Time.time;
            }
            _wasReady = ready;
        }

        public float CurrentProgress => _currentProgress;

        /// <summary>
        /// 외부에서 색상 override (KhiDownController.Awake 가 SerializeField 값으로 호출).
        /// 부활 차징 중에도 호출 가능 — fill color 는 다음 SetProgress 호출 시 반영.
        /// </summary>
        public void SetColors(Color background, Color fill, Color ready)
        {
            backgroundColor = background;
            fillColor = fill;
            readyColor = ready;
            if (backgroundSprite != null) backgroundSprite.color = backgroundColor;
            if (fillSprite != null && _currentProgress < 0.99f) fillSprite.color = fillColor;
        }

        /// <summary>
        /// sorting layer / order override.
        /// sortingLayerName 빈 문자열이면 변경 안 함 (현재 유지).
        /// fill 은 background 보다 항상 1 위에.
        /// </summary>
        public void SetSorting(string sortingLayerName, int backgroundOrder)
        {
            if (backgroundSprite != null)
            {
                if (!string.IsNullOrWhiteSpace(sortingLayerName)) backgroundSprite.sortingLayerName = sortingLayerName;
                backgroundSprite.sortingOrder = backgroundOrder;
            }
            if (fillSprite != null)
            {
                if (!string.IsNullOrWhiteSpace(sortingLayerName)) fillSprite.sortingLayerName = sortingLayerName;
                fillSprite.sortingOrder = backgroundOrder + 1;
            }
        }

        private void SetVisible(bool show)
        {
            if (backgroundSprite != null) backgroundSprite.enabled = show;
            if (fillSprite != null) fillSprite.enabled = show;
        }

        /// <summary>
        /// KhiDownController.Awake() 가 호출 — 자식 GameObject 두 개 동적 생성 + SpriteRenderer attach.
        /// 머리 위 위치는 외부에서 transform.localPosition 으로 설정.
        /// </summary>
        public static KhiCoopReviveBarView CreateOnTransform(Transform parent, Vector3 localOffset, float barWidth = 0.9f, float barHeight = 0.12f)
        {
            GameObject barRoot = new GameObject("_CoopReviveBar");
            barRoot.transform.SetParent(parent, false);
            barRoot.transform.localPosition = localOffset;
            barRoot.transform.localRotation = Quaternion.identity;
            barRoot.transform.localScale = Vector3.one;

            // pivot (0, 0.5) sprite 라 두 SpriteRenderer 모두 localPosition.x = -barWidth*0.5 로 두면
            // 좌측 끝이 origin 의 왼쪽 -barWidth/2 에 align — bar 가 origin 중심으로 좌우 대칭 정렬.
            // 두 sprite 가 같은 출발점 → fill 이 ratio 만큼 오른쪽으로 자라며 bg 를 덮는 progress bar 동작.
            GameObject bgGo = new GameObject("Background");
            bgGo.transform.SetParent(barRoot.transform, false);
            bgGo.transform.localPosition = new Vector3(-barWidth * 0.5f, 0f, 0f);
            bgGo.transform.localScale = new Vector3(barWidth, barHeight, 1f);
            SpriteRenderer bgSr = bgGo.AddComponent<SpriteRenderer>();
            bgSr.sprite = GetBarSprite();
            bgSr.sortingOrder = 50;

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barRoot.transform, false);
            // fill 은 bg 와 동일한 좌측 끝 정렬 + z 살짝 앞 (transparency sort 안전망).
            fillGo.transform.localPosition = new Vector3(-barWidth * 0.5f, 0f, -0.001f);
            fillGo.transform.localScale = new Vector3(barWidth, barHeight, 1f);
            SpriteRenderer fillSr = fillGo.AddComponent<SpriteRenderer>();
            fillSr.sprite = GetBarSprite();
            fillSr.sortingOrder = 51;

            KhiCoopReviveBarView view = barRoot.AddComponent<KhiCoopReviveBarView>();
            view.backgroundSprite = bgSr;
            view.fillSprite = fillSr;
            // AddComponent 가 Awake 즉시 호출했을 때 sprite 필드가 null 이라 _fillBaseScale capture 누락됨 — 명시적 보충.
            view.EnsureInitialized();
            return view;
        }

        /// <summary>procedural 흰 사각형 sprite (pivot 왼쪽 — KhiStaffChargeBarView 와 동일).</summary>
        private static Sprite GetBarSprite()
        {
            if (_cachedBarSprite != null) return _cachedBarSprite;
            const int w = 64, h = 8;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "KhiCoopReviveBar",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color white = Color.white;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, white);
            tex.Apply();
            _cachedBarSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0f, 0.5f), w);
            _cachedBarSprite.name = "KhiCoopReviveBar";
            return _cachedBarSprite;
        }
    }
}

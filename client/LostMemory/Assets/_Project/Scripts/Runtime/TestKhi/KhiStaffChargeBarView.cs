using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 캐릭터 머리 위 차징바 — KhiStaffController.IsCharging 동안 표시.
    /// fillSprite scale.x = ChargeProgress01 로 fill 진행. IsChargeReady 시 readyColor 로 변경.
    /// 사용자 워크플로우:
    ///   ChargeBar.prefab 자체에 배치 — 캐릭터 머리 위(예: localPos (0, 0.8)) 자식으로 부착.
    ///   staffController 비워두면 GetComponentInParent 로 자동 resolve.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Staff Charge Bar View")]
    public class KhiStaffChargeBarView : MonoBehaviour
    {
        [Header("Refs (Awake 시 자동 resolve)")]
        [SerializeField] private KhiStaffController staffController;
        [Tooltip("배경 (회색/검정 빈 바). 같은 ChargeBar prefab 자식 GameObject 의 SpriteRenderer.")]
        [SerializeField] private SpriteRenderer backgroundSprite;
        [Tooltip("진행 fill (노란→ 도달 시 readyColor 로 변경). pivot 왼쪽 권장.")]
        [SerializeField] private SpriteRenderer fillSprite;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        [SerializeField] private Color fillColor = new Color(1f, 0.85f, 0.2f, 0.95f);
        [SerializeField] private Color readyColor = new Color(1f, 0.5f, 0.1f, 1f);

        [Header("Behavior")]
        [SerializeField] private bool hideWhenNotCharging = true;
        [Tooltip("Ready 도달 직후 짧은 깜빡임 효과 시간.")]
        [SerializeField, Min(0f)] private float readyPulseDuration = 0.18f;

        private Vector3 _fillBaseScale;
        private bool _wasReady;
        private float _readyPulseStartedAt = -1f;

        private static Sprite _cachedBarSprite;

        private void Awake()
        {
            if (staffController == null) staffController = GetComponentInParent<KhiStaffController>();

            // procedural 흰 사각형 sprite fallback (한 번만 생성).
            if (backgroundSprite != null && backgroundSprite.sprite == null) backgroundSprite.sprite = GetBarSprite();
            if (fillSprite != null && fillSprite.sprite == null) fillSprite.sprite = GetBarSprite();

            if (backgroundSprite != null) backgroundSprite.color = backgroundColor;
            if (fillSprite != null)
            {
                fillSprite.color = fillColor;
                _fillBaseScale = fillSprite.transform.localScale;
            }

            SetVisible(false);
        }

        private void Update()
        {
            if (staffController == null) return;

            bool charging = staffController.IsCharging;
            if (!charging)
            {
                if (hideWhenNotCharging) SetVisible(false);
                _wasReady = false;
                _readyPulseStartedAt = -1f;
                return;
            }

            SetVisible(true);

            float progress = staffController.ChargeProgress01;
            bool ready = staffController.IsChargeReady;

            if (fillSprite != null)
            {
                Vector3 s = _fillBaseScale;
                s.x = _fillBaseScale.x * progress;
                fillSprite.transform.localScale = s;

                Color targetColor = ready ? readyColor : fillColor;
                if (ready && _readyPulseStartedAt >= 0f && readyPulseDuration > 0f)
                {
                    float t = (Time.time - _readyPulseStartedAt) / readyPulseDuration;
                    if (t < 1f)
                    {
                        float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.6f;
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

        private void SetVisible(bool show)
        {
            if (backgroundSprite != null) backgroundSprite.enabled = show;
            if (fillSprite != null) fillSprite.enabled = show;
        }

        /// <summary>procedural 흰 사각형 sprite (pivot 왼쪽). 한 번만 생성, 모든 차징바 공유.</summary>
        private static Sprite GetBarSprite()
        {
            if (_cachedBarSprite != null) return _cachedBarSprite;
            const int w = 64, h = 8;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "KhiStaffBar",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color white = Color.white;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, white);
            tex.Apply();
            _cachedBarSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0f, 0.5f), w);
            _cachedBarSprite.name = "KhiStaffBar";
            return _cachedBarSprite;
        }
    }
}

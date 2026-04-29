using LostMemory.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Shop
{
    /// <summary>
    /// 인벤토리/단축키바 슬롯 위에 마우스를 올렸을 때 나타나는 아이템 설명 팝업.
    /// 씬에 하나만 존재하며 Instance로 어디서든 접근한다.
    /// </summary>
    public class TooltipView : MonoBehaviour
    {
        public static TooltipView Instance { get; private set; }

        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _rarityText;
        [SerializeField] private TextMeshProUGUI _descText;

        private RectTransform _rt;
        private Canvas        _canvas;
        private RectTransform _canvasRT;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance  = this;
            _rt       = GetComponent<RectTransform>();
            _canvas   = GetComponentInParent<Canvas>();
            if (_canvas != null) _canvasRT = _canvas.GetComponent<RectTransform>();
            gameObject.SetActive(false);
        }

        /// <summary>유물 정보를 채워 팝업을 표시한다.</summary>
        public void Show(RelicData relic, Vector2 screenPos)
        {
            transform.SetAsLastSibling();   // 항상 최상단에 렌더링
            gameObject.SetActive(true);

            _nameText.text   = relic.DisplayName;
            _nameText.color  = GetRarityColor(relic);
            _rarityText.text = GetRarityTagLabel(relic);
            _descText.text   = string.IsNullOrEmpty(relic.EffectDescription)
                ? "(설명 없음)" : relic.EffectDescription;

            // ContentSizeFitter가 텍스트 크기에 맞게 즉시 리사이즈되도록 강제 갱신
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);

            UpdatePosition(screenPos);
        }

        /// <summary>팝업을 숨긴다.</summary>
        public void Hide() => gameObject.SetActive(false);

        /// <summary>마우스 이동 시 팝업 위치를 갱신한다.</summary>
        public void UpdatePosition(Vector2 screenPos)
        {
            if (_canvas == null || _canvasRT == null) return;

            Camera cam = (_canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _canvas.worldCamera : null;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRT, screenPos, cam, out Vector2 localPos);

            // 커서 오른쪽 위로 오프셋
            localPos += new Vector2(14f, 14f);

            // 화면 경계 안으로 클램핑
            Vector2 half    = _canvasRT.rect.size * 0.5f;
            Vector2 tipSize = _rt.rect.size;

            localPos.x = Mathf.Clamp(localPos.x, -half.x,             half.x - tipSize.x);
            localPos.y = Mathf.Clamp(localPos.y, -half.y + tipSize.y, half.y);

            _rt.localPosition = localPos;
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────

        private static string GetRarityTagLabel(RelicData relic)
        {
            if (relic.IsConsumable) return "[소모품]";

            string rarityName = relic.Rarity switch
            {
                RelicRarity.Common    => "일반",
                RelicRarity.Rare      => "레어",
                RelicRarity.Unique    => "유니크",
                RelicRarity.Legendary => "전설",
                _                     => ""
            };
            string tagName = relic.Tag switch
            {
                RelicTag.Assault  => "맹공",
                RelicTag.Guardian => "수호",
                RelicTag.Sprint   => "질주",
                _                 => ""
            };
            return $"[{rarityName}] {tagName}";
        }

        private static Color GetRarityColor(RelicData relic)
        {
            if (relic.IsConsumable) return new Color(0.7f, 0.7f, 0.7f);
            return relic.Rarity switch
            {
                RelicRarity.Common    => Color.white,
                RelicRarity.Rare      => new Color(0.31f, 0.59f, 0.96f),
                RelicRarity.Unique    => new Color(1f,    0.30f, 0.99f),
                RelicRarity.Legendary => new Color(0.96f, 0.77f, 0.26f),
                _                     => Color.white
            };
        }
    }
}

using System.Text;
using LostMemory.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Shop
{
    /// <summary>
    /// 인벤토리/단축키바 슬롯 위에 마우스를 올렸을 때 나타나는 아이템 설명 팝업.
    /// 씬에 하나만 존재하며 Instance로 어디서든 접근한다.
    /// prefab 인스턴스가 없으면 RuntimeInitializeOnLoad / 첫 호버 시점에 자동 생성.
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
        private BuildManager  _cachedBuildManager;

        /// <summary>씬 로드 후 Instance가 없으면 코드로 자동 부트스트랩.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            EnsureInstance();
        }

        /// <summary>첫 호버 시점에 호출 가능한 lazy 부트스트랩 진입점.</summary>
        public static TooltipView EnsureInstance()
        {
            if (Instance != null) return Instance;

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            Canvas target = null;
            foreach (Canvas c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    target = c;
                    break;
                }
            }
            if (target == null && canvases.Length > 0) target = canvases[0];
            if (target == null)
            {
                Debug.LogWarning("[TooltipView] 씬에 Canvas가 없어 툴팁을 부트스트랩할 수 없음.");
                return null;
            }

            GameObject go = new GameObject("TooltipView_Auto",
                typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(target.transform, false);
            return go.AddComponent<TooltipView>();
        }

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

            EnsureUI();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>유물 정보를 채워 팝업을 표시한다.</summary>
        public void Show(RelicData relic, Vector2 screenPos)
        {
            transform.SetAsLastSibling();   // 항상 최상단에 렌더링
            gameObject.SetActive(true);

            _nameText.text   = relic.DisplayName;
            _nameText.color  = RelicRarityColors.Text(relic);
            _rarityText.text = BuildRarityLine(relic);
            _descText.text   = BuildBodyText(relic);

            // ContentSizeFitter가 텍스트 크기에 맞게 즉시 리사이즈되도록 강제 갱신
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);

            UpdatePosition(screenPos);
        }

        /// <summary>세트 정보를 채워 팝업을 표시한다. (SetEffectRowView 호버)</summary>
        public void ShowSet(BuildSetData set, int count, int activeTier, Vector2 screenPos)
        {
            if (set == null) return;

            transform.SetAsLastSibling();
            gameObject.SetActive(true);

            _nameText.text   = set.DisplayName;
            _nameText.color  = activeTier >= 0 ? new Color(1f, 0.85f, 0.35f) : Color.white;
            _rarityText.text = BuildSetSummaryLine(set, count, activeTier);
            _descText.text   = BuildSetTierList(set, count, activeTier);

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

        // ── UI 자체 생성 ───────────────────────────────────────────

        /// <summary>인스펙터 wiring이 없으면 코드로 UI 구조를 생성한다.</summary>
        private void EnsureUI()
        {
            if (_nameText != null && _rarityText != null && _descText != null)
                return; // 이미 prefab 인스턴스로 wired

            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(320f, 200f);

            Image bg = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
            bg.raycastTarget = false;

            VerticalLayoutGroup vlg = gameObject.GetComponent<VerticalLayoutGroup>()
                ?? gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 10, 10);
            vlg.spacing = 4f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment         = TextAnchor.UpperLeft;

            ContentSizeFitter csf = gameObject.GetComponent<ContentSizeFitter>()
                ?? gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            if (_nameText   == null) _nameText   = CreateChildText("NameText",   18, FontStyles.Bold);
            if (_rarityText == null) _rarityText = CreateChildText("RarityText", 13, FontStyles.Italic);
            if (_descText   == null) _descText   = CreateChildText("DescText",   14, FontStyles.Normal);
        }

        private TextMeshProUGUI CreateChildText(string n, int size, FontStyles style)
        {
            GameObject go = new GameObject(n,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize            = size;
            t.fontStyle           = style;
            t.color               = Color.white;
            t.alignment           = TextAlignmentOptions.TopLeft;
            t.enableWordWrapping  = true;
            t.raycastTarget       = false;

            // 한글 폰트: 씬의 다른 TMP_Text 에서 복사 (없으면 TMP 기본).
            TMP_FontAsset borrowed = BorrowSceneFont();
            if (borrowed != null) t.font = borrowed;
            return t;
        }

        private static TMP_FontAsset BorrowSceneFont()
        {
            TMP_Text[] existing = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
            foreach (TMP_Text txt in existing)
            {
                if (txt != null && txt.font != null) return txt.font;
            }
            return null;
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────

        private static string BuildRarityLine(RelicData relic)
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

            var sb = new StringBuilder();
            sb.Append('[').Append(rarityName).Append(']');
            if (relic.TagPrimary != RelicTag.None)
                sb.Append(" [").Append(RelicTagLabels.ToKorean(relic.TagPrimary)).Append(']');
            if (relic.TagSecondary != RelicTag.None && relic.TagSecondary != relic.TagPrimary)
                sb.Append(" [").Append(RelicTagLabels.ToKorean(relic.TagSecondary)).Append(']');
            return sb.ToString();
        }

        private string BuildBodyText(RelicData relic)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(relic.EffectDescription))
                sb.AppendLine(relic.EffectDescription);

            if (relic.Effects != null)
            {
                foreach (EffectEntry e in relic.Effects)
                {
                    string line = RelicEffectLabels.Format(e);
                    if (!string.IsNullOrEmpty(line))
                        sb.Append("• ").AppendLine(line);
                }
            }

            if (sb.Length == 0) sb.AppendLine("(설명 없음)");

            if (!relic.IsConsumable)
            {
                BuildManager bm = ResolveBuildManager();
                if (bm != null)
                {
                    int before = sb.Length;
                    AppendSetProgress(sb, bm, relic.TagPrimary);
                    if (relic.TagSecondary != relic.TagPrimary)
                        AppendSetProgress(sb, bm, relic.TagSecondary);
                    if (sb.Length > before)
                        sb.Insert(before, "\n");
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendSetProgress(StringBuilder sb, BuildManager bm, RelicTag tag)
        {
            if (tag == RelicTag.None) return;
            BuildSetData set = bm.GetSetForTag(tag);
            if (set == null || set.Tiers == null || set.Tiers.Count == 0) return;

            int count    = bm.GetTagCount(tag);
            int activeIx = bm.GetActiveTier(tag);
            string label = RelicTagLabels.ToKorean(tag);

            if (activeIx + 1 >= set.Tiers.Count)
            {
                sb.AppendLine($"[{label}] {count}개 — 최종 효과 발동 중");
                return;
            }

            SetTier next = set.Tiers[activeIx + 1];
            string nextDesc = string.IsNullOrEmpty(next.Description)
                ? RelicEffectLabels.LabelFor(next.EffectType)
                : next.Description;
            sb.AppendLine($"[{label}] {count}/{next.RequiredCount} — {nextDesc}");
        }

        private BuildManager ResolveBuildManager()
        {
            if (_cachedBuildManager != null) return _cachedBuildManager;
            _cachedBuildManager = FindFirstObjectByType<BuildManager>();
            return _cachedBuildManager;
        }

        // ── 세트 라벨 빌더 ────────────────────────────────────────

        private static string BuildSetSummaryLine(BuildSetData set, int count, int activeTier)
        {
            var tiers = set.Tiers;
            if (tiers == null || tiers.Count == 0)
                return $"보유 {count}";

            int maxRequired = tiers[tiers.Count - 1].RequiredCount;
            string status = activeTier >= 0 ? $"  T{activeTier} 발동" : "  미발동";
            return $"보유 {count} / {maxRequired}{status}";
        }

        private static string BuildSetTierList(BuildSetData set, int count, int activeTier)
        {
            var tiers = set.Tiers;
            if (tiers == null || tiers.Count == 0) return "(티어 정보 없음)";

            var sb = new StringBuilder();
            for (int i = 0; i < tiers.Count; i++)
            {
                SetTier t = tiers[i];
                bool isActive = i <= activeTier;
                bool isNext   = i == activeTier + 1;

                string desc = string.IsNullOrEmpty(t.Description)
                    ? RelicEffectLabels.LabelFor(t.EffectType)
                    : t.Description;

                string mark   = isActive ? "✓" : (isNext ? "▶" : " ");
                string colorOpen  = isActive ? "<color=#FFD966>" : (isNext ? "<color=#FFFFFF>" : "<color=#888888>");
                string colorClose = "</color>";

                sb.Append(colorOpen)
                  .Append(mark).Append(" T").Append(i)
                  .Append(" (").Append(t.RequiredCount).Append("개) — ")
                  .Append(desc)
                  .Append(colorClose);

                if (i < tiers.Count - 1) sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}

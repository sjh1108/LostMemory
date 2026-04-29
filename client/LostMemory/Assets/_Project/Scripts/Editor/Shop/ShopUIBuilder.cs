using LostMemory.Relics;
using LostMemory.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostMemory.Editor.Shop
{
    /// <summary>
    /// 상점 UI Sync 도구.
    /// "Build"(전부 삭제 후 재생성) 방식이 아닌 "Sync"(없는 것만 추가) 방식으로 동작한다.
    /// → Inspector에서 직접 수정한 값은 재실행해도 유지된다.
    /// </summary>
    public static class ShopUIBuilder
    {
        // ── 색상 팔레트 ──────────────────────────────────────────────
        private static readonly Color ColPanelBg   = new(0.17f, 0.17f, 0.17f);
        private static readonly Color ColTitleBg   = new(0.10f, 0.10f, 0.10f);
        private static readonly Color ColItemBg    = new(0.22f, 0.22f, 0.22f);
        private static readonly Color ColScrollBg  = new(0.20f, 0.20f, 0.20f);
        private static readonly Color ColSlotBg    = new(0.25f, 0.25f, 0.25f);
        private static readonly Color ColBuyBtn    = new(0.20f, 0.45f, 0.20f);
        private static readonly Color ColScrollbar = new(0.15f, 0.15f, 0.15f);
        private static readonly Color ColHandle    = new(0.40f, 0.40f, 0.40f);
        private static readonly Color ColGold      = new(0.96f, 0.77f, 0.26f);

        // ── 폰트 ─────────────────────────────────────────────────────
        private const string FontPath = "Assets/_Project/Art/Fonts/Galmuri9.asset";
        private static TMP_FontAsset s_font;

        // ════════════════════════════════════════════════════════════
        // 메뉴 진입점
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// UI만 Sync한다. 없는 오브젝트/컴포넌트만 추가하고
        /// Inspector에서 직접 수정한 값은 건드리지 않는다.
        /// </summary>
        [MenuItem("LostMemory/Shop/Sync Shop UI")]
        public static void SyncShopUI()
        {
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (s_font == null)
                Debug.LogWarning($"[ShopUIBuilder] Galmuri9 폰트 없음: {FontPath}");

            var canvas         = FindOrCreateCanvas();
            var shopPanel      = SyncShopPanel(canvas.transform);
            var inventoryPanel = SyncInventoryPanel(canvas.transform);

            WireShopPanelView(shopPanel);
            WireInventoryPanelView(inventoryPanel);
            RewireTestShopPanelsIfExists(
                shopPanel.GetComponent<ShopPanelView>(),
                inventoryPanel.GetComponent<InventoryPanelView>());

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[ShopUIBuilder] ✅ Sync 완료 — Inspector 수정값 유지됨");
        }

        /// <summary>
        /// UI Sync + ShopData 에셋 생성 + PlayerInventory + TestShop 연결.
        /// Play 버튼만 누르면 바로 테스트 가능.
        /// </summary>
        [MenuItem("LostMemory/Shop/Build Test Scene")]
        public static void BuildTestScene()
        {
            SyncShopUI();

            var shopData = CreateOrUpdateTestShopData();
            if (shopData == null)
            {
                Debug.LogError("[ShopUIBuilder] ShopData 생성 실패");
                return;
            }

            var inventoryComp = FindOrCreatePlayerInventory();
            WireTestShop(shopData, inventoryComp);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[ShopUIBuilder] ✅ Test Scene 완료 — Play를 눌러 확인하세요.");
        }

        // ════════════════════════════════════════════════════════════
        // ShopPanel
        // ════════════════════════════════════════════════════════════

        private static GameObject SyncShopPanel(Transform canvasT)
        {
            var (go, created) = GetOrCreateChild(canvasT, "ShopPanel");
            if (created)
                SetRect(go, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(370f, 0f), new Vector2(340f, 480f));

            var (img, imgNew) = GetOrAdd<Image>(go);
            if (imgNew) img.color = ColPanelBg;

            var (vlg, vlgNew) = GetOrAdd<VerticalLayoutGroup>(go);
            if (vlgNew)
            {
                vlg.childControlWidth      = true;
                vlg.childControlHeight     = true;
                vlg.childForceExpandWidth  = true;
                vlg.childForceExpandHeight = false;
                vlg.spacing                = 0f;
            }

            GetOrAdd<ShopPanelView>(go);

            SyncTitleBar(go.transform, "상 점");
            SyncShopScrollView(go.transform);
            SyncDetailArea(go.transform);

            return go;
        }

        private static void SyncTitleBar(Transform parent, string titleStr)
        {
            var (go, _) = GetOrCreateChild(parent, "TitleBar");

            var (img, imgNew) = GetOrAdd<Image>(go);
            if (imgNew) img.color = ColTitleBg;

            var (le, leNew) = GetOrAdd<LayoutElement>(go);
            if (leNew) le.preferredHeight = 50f;

            // TitleText
            var (titleGO, titleNew) = GetOrCreateChild(go.transform, "TitleText");
            if (titleNew) SetStretch(titleGO, 10f, 50f, 0f, 0f);
            var (tmp, tmpNew) = GetOrAdd<TextMeshProUGUI>(titleGO);
            if (tmpNew) ApplyTMP(tmp, titleStr, 18f, Color.white, TextAlignmentOptions.Center);

            // CloseButton
            var (btnGO, btnNew) = GetOrCreateChild(go.transform, "CloseButton");
            if (btnNew)
                SetRect(btnGO, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(1f, 0.5f), new Vector2(-5f, 0f), new Vector2(40f, 40f));

            var (btnImg, btnImgNew) = GetOrAdd<Image>(btnGO);
            if (btnImgNew) btnImg.color = ColScrollbar;

            var (btn, btnCompNew) = GetOrAdd<Button>(btnGO);
            if (btnCompNew) btn.targetGraphic = btnImg;

            var (closeTextGO, closeNew) = GetOrCreateChild(btnGO.transform, "CloseText");
            if (closeNew) SetStretch(closeTextGO, 0f, 0f, 0f, 0f);
            var (ctmp, ctmpNew) = GetOrAdd<TextMeshProUGUI>(closeTextGO);
            if (ctmpNew) ApplyTMP(ctmp, "X", 18f, Color.white, TextAlignmentOptions.Center);
        }

        private static void SyncShopScrollView(Transform parent)
        {
            var (go, _) = GetOrCreateChild(parent, "ShopScrollView");

            var (img, imgNew) = GetOrAdd<Image>(go);
            if (imgNew) img.color = ColScrollBg;

            var (le, leNew) = GetOrAdd<LayoutElement>(go);
            if (leNew) le.preferredHeight = 270f; // Content(~346px) > 270px → 스크롤 동작

            var (sr, srNew) = GetOrAdd<ScrollRect>(go);
            if (srNew)
            {
                sr.horizontal = false;
                sr.vertical   = true;
            }

            // ── Viewport ──
            var (vpGO, vpNew) = GetOrCreateChild(go.transform, "Viewport");
            if (vpNew) SetStretch(vpGO, 0f, 17f, 0f, 0f);

            var (vpImg, vpImgNew) = GetOrAdd<Image>(vpGO);
            if (vpImgNew) vpImg.color = Color.white;

            var (mask, maskNew) = GetOrAdd<Mask>(vpGO);
            if (maskNew) mask.showMaskGraphic = false;

            // ── Content ──
            var (contentGO, contentNew) = GetOrCreateChild(vpGO.transform, "Content");
            if (contentNew)
            {
                var rt             = contentGO.GetComponent<RectTransform>();
                rt.anchorMin       = new Vector2(0f, 1f);
                rt.anchorMax       = new Vector2(1f, 1f);
                rt.pivot           = new Vector2(0f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta       = Vector2.zero;
            }

            var (cvlg, cvlgNew) = GetOrAdd<VerticalLayoutGroup>(contentGO);
            if (cvlgNew)
            {
                cvlg.padding                = new RectOffset(0, 0, 4, 4);
                cvlg.spacing                = 6f;
                cvlg.childControlWidth      = true;
                cvlg.childControlHeight     = false;
                cvlg.childForceExpandWidth  = true;
                cvlg.childForceExpandHeight = false;
            }

            // ContentSizeFitter: 아이템 수에 따라 Content 높이 자동 계산 → 스크롤 동작
            var (csf, csfNew) = GetOrAdd<ContentSizeFitter>(contentGO);
            if (csfNew)
            {
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            }

            for (int i = 0; i < 4; i++)
                SyncShopItem(contentGO.transform, i);

            // ── Scrollbar ──
            var (sbGO, sbNew) = GetOrCreateChild(go.transform, "Scrollbar Vertical");
            if (sbNew)
                SetRect(sbGO, new Vector2(1f, 0f), new Vector2(1f, 1f),
                        new Vector2(1f, 0.5f), Vector2.zero, new Vector2(17f, 0f));

            var (sbImg, sbImgNew) = GetOrAdd<Image>(sbGO);
            if (sbImgNew) sbImg.color = ColScrollbar;

            var (sb, sbCompNew) = GetOrAdd<Scrollbar>(sbGO);
            if (sbCompNew) sb.direction = Scrollbar.Direction.BottomToTop;

            var (slidingGO, slidingNew) = GetOrCreateChild(sbGO.transform, "Sliding Area");
            if (slidingNew)
                SetRect(slidingGO, new Vector2(0f, 0f), new Vector2(1f, 1f),
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, -20f));

            var (handleGO, handleNew) = GetOrCreateChild(slidingGO.transform, "Handle");
            var handleRt = handleGO.GetComponent<RectTransform>();
            if (handleNew)
            {
                handleRt.anchorMin = Vector2.zero;
                handleRt.anchorMax = new Vector2(1f, 0.2f);
                handleRt.offsetMin = new Vector2(-10f, -10f);
                handleRt.offsetMax = new Vector2(10f, 10f);
            }

            var (handleImg, handleImgNew) = GetOrAdd<Image>(handleGO);
            if (handleImgNew) handleImg.color = ColHandle;

            // 항상 연결 (구조적 참조)
            sb.handleRect    = handleRt;
            sb.targetGraphic = handleImg;

            sr.viewport                     = vpGO.GetComponent<RectTransform>();
            sr.content                      = contentGO.GetComponent<RectTransform>();
            sr.verticalScrollbar            = sb;
            sr.verticalScrollbarVisibility  = ScrollRect.ScrollbarVisibility.Permanent;
            sr.scrollSensitivity            = 20f;  // 기본값 1.0은 1px/클릭 → 체감상 "안 움직임"
        }

        private static void SyncShopItem(Transform parent, int index)
        {
            var (go, created) = GetOrCreateChild(parent, $"ShopItem_{index}");

            var (img, imgNew) = GetOrAdd<Image>(go);
            if (imgNew) img.color = ColItemBg;

            if (created)
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 80f);

            var (hlg, hlgNew) = GetOrAdd<HorizontalLayoutGroup>(go);
            if (hlgNew)
            {
                hlg.padding                = new RectOffset(8, 8, 8, 8);
                hlg.spacing                = 8f;
                hlg.childControlWidth      = true;
                hlg.childControlHeight     = true;
                hlg.childForceExpandWidth  = false;
                hlg.childForceExpandHeight = true;
            }

            // IconImage
            var (iconGO, _)           = GetOrCreateChild(go.transform, "IconImage");
            var (iconImg, iconImgNew) = GetOrAdd<Image>(iconGO);
            if (iconImgNew) iconImg.color = ColSlotBg;
            var (iconLE, iconLENew) = GetOrAdd<LayoutElement>(iconGO);
            if (iconLENew) { iconLE.minWidth = 64f; iconLE.preferredWidth = 64f; }

            // InfoArea
            var (infoGO, _)         = GetOrCreateChild(go.transform, "InfoArea");
            var (infoLE, infoLENew) = GetOrAdd<LayoutElement>(infoGO);
            if (infoLENew) infoLE.flexibleWidth = 1f;
            var (infoVLG, infoVLGNew) = GetOrAdd<VerticalLayoutGroup>(infoGO);
            if (infoVLGNew)
            {
                infoVLG.spacing             = 2f;
                infoVLG.childControlWidth   = true;
                infoVLG.childControlHeight  = false;
                infoVLG.childForceExpandWidth  = true;
                infoVLG.childForceExpandHeight = false;
            }

            var (nameGO, nameNew)     = GetOrCreateChild(infoGO.transform, "NameText");
            if (nameNew) nameGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 27f);
            var (nameTMP, nameTMPNew) = GetOrAdd<TextMeshProUGUI>(nameGO);
            if (nameTMPNew) ApplyTMP(nameTMP, "아이템 이름", 18f, Color.white);

            var (descGO, descNew)     = GetOrCreateChild(infoGO.transform, "DescText");
            if (descNew) descGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f);
            var (descTMP, descTMPNew) = GetOrAdd<TextMeshProUGUI>(descGO);
            if (descTMPNew) ApplyTMP(descTMP, "", 14f, new Color(0.7f, 0.7f, 0.7f));

            // PriceArea — VLG 사용 금지
            // ※ VLG가 있으면 Unity LayoutUtility가 "VLG 자식들의 preferred width"를
            //   PriceArea 자신의 preferred width로도 올려서 부모 HLG가 70px 제한을 무시하는 버그 발생.
            //   → VLG 제거 후 앵커 기반 배치로 변경.
            var (priceAreaGO, _) = GetOrCreateChild(go.transform, "PriceArea");

            // 기존 VLG가 있으면 제거 (이전 버전과의 호환)
            var oldPriceVLG = priceAreaGO.GetComponent<VerticalLayoutGroup>();
            if (oldPriceVLG != null) Object.DestroyImmediate(oldPriceVLG);

            var (priceLE, priceLENew) = GetOrAdd<LayoutElement>(priceAreaGO);
            if (priceLENew) { priceLE.minWidth = 70f; priceLE.preferredWidth = 70f; }

            // PriceText — 위쪽 앵커 고정, PriceArea 전체 폭 사용
            var (priceGO, priceNew) = GetOrCreateChild(priceAreaGO.transform, "PriceText");
            if (priceNew)
            {
                var rt              = priceGO.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0f, 1f);
                rt.anchorMax        = new Vector2(1f, 1f);
                rt.pivot            = new Vector2(0.5f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = new Vector2(0f, 27f);
            }
            var (priceTMP, priceTMPNew) = GetOrAdd<TextMeshProUGUI>(priceGO);
            if (priceTMPNew) ApplyTMP(priceTMP, "0", 18f, ColGold, TextAlignmentOptions.Right);

            // BuyButton — 아래쪽 앵커 고정, PriceArea 전체 폭 사용
            var (buyGO, buyNew) = GetOrCreateChild(priceAreaGO.transform, "BuyButton");
            if (buyNew)
            {
                var rt              = buyGO.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0f, 0f);
                rt.anchorMax        = new Vector2(1f, 0f);
                rt.pivot            = new Vector2(0.5f, 0f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = new Vector2(0f, 22f);
            }
            var (buyImg, buyImgNew)       = GetOrAdd<Image>(buyGO);
            if (buyImgNew) buyImg.color = ColBuyBtn;
            var (buyBtn, buyBtnNew)       = GetOrAdd<Button>(buyGO);
            if (buyBtnNew) buyBtn.targetGraphic = buyImg;

            var (buyTextGO, buyTextNew)   = GetOrCreateChild(buyGO.transform, "BuyText");
            if (buyTextNew) SetStretch(buyTextGO, 0f, 0f, 0f, 0f);
            var (buyTMP, buyTMPNew)       = GetOrAdd<TextMeshProUGUI>(buyTextGO);
            if (buyTMPNew) ApplyTMP(buyTMP, "구매", 14f, Color.white, TextAlignmentOptions.Center);

            GetOrAdd<ShopItemView>(go);
        }

        private static void SyncDetailArea(Transform parent)
        {
            var (go, _) = GetOrCreateChild(parent, "DetailArea");

            var (img, imgNew) = GetOrAdd<Image>(go);
            if (imgNew) img.color = ColTitleBg;

            var (le, leNew) = GetOrAdd<LayoutElement>(go);
            if (leNew) le.preferredHeight = 160f;

            var (hlg, hlgNew) = GetOrAdd<HorizontalLayoutGroup>(go);
            if (hlgNew)
            {
                hlg.padding                = new RectOffset(10, 10, 10, 10);
                hlg.spacing                = 10f;
                hlg.childControlWidth      = false;
                hlg.childControlHeight     = true;
                hlg.childForceExpandWidth  = false;
                hlg.childForceExpandHeight = true;
            }

            var (iconGO, _)           = GetOrCreateChild(go.transform, "IconImage");
            var (iconImg, iconImgNew) = GetOrAdd<Image>(iconGO);
            if (iconImgNew) iconImg.color = ColSlotBg;
            var (iconLE, iconLENew)   = GetOrAdd<LayoutElement>(iconGO);
            if (iconLENew) { iconLE.minWidth = 80f; iconLE.preferredWidth = 80f; }

            var (infoGO, _)               = GetOrCreateChild(go.transform, "InfoGroup");
            var (infoLE, infoLENew)       = GetOrAdd<LayoutElement>(infoGO);
            if (infoLENew) infoLE.flexibleWidth = 1f;
            var (infoVLG, infoVLGNew)     = GetOrAdd<VerticalLayoutGroup>(infoGO);
            if (infoVLGNew)
            {
                infoVLG.spacing             = 4f;
                infoVLG.childControlWidth   = true;
                infoVLG.childControlHeight  = false;
                infoVLG.childForceExpandWidth  = true;
                infoVLG.childForceExpandHeight = false;
            }

            var (nameGO, nameNew)     = GetOrCreateChild(infoGO.transform, "DetailNameText");
            if (nameNew) nameGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 27f);
            var (nameTMP, nameTMPNew) = GetOrAdd<TextMeshProUGUI>(nameGO);
            if (nameTMPNew) ApplyTMP(nameTMP, "", 18f, Color.white);

            var (descGO, descNew)     = GetOrCreateChild(infoGO.transform, "DetailDescText");
            if (descNew) descGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 60f);
            var (descTMP, descTMPNew) = GetOrAdd<TextMeshProUGUI>(descGO);
            if (descTMPNew) ApplyTMP(descTMP, "", 14f, new Color(0.7f, 0.7f, 0.7f));
        }

        // ════════════════════════════════════════════════════════════
        // InventoryPanel
        // ════════════════════════════════════════════════════════════

        private static GameObject SyncInventoryPanel(Transform canvasT)
        {
            var (go, created) = GetOrCreateChild(canvasT, "InventoryPanel");
            if (created)
                SetRect(go, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(780f, 50f), new Vector2(280f, 390f));

            var (img, imgNew) = GetOrAdd<Image>(go);
            if (imgNew) img.color = ColPanelBg;

            var (vlg, vlgNew) = GetOrAdd<VerticalLayoutGroup>(go);
            if (vlgNew)
            {
                vlg.childControlWidth      = true;
                vlg.childControlHeight     = true;
                vlg.childForceExpandWidth  = true;
                vlg.childForceExpandHeight = false;
                vlg.spacing                = 0f;
            }

            GetOrAdd<InventoryPanelView>(go);

            SyncTitleBar(go.transform, "인벤토리");
            SyncSlotGrid(go.transform);
            SyncGoldArea(go.transform);

            return go;
        }

        private static void SyncSlotGrid(Transform parent)
        {
            var (go, _) = GetOrCreateChild(parent, "SlotGrid");

            var (le, leNew) = GetOrAdd<LayoutElement>(go);
            if (leNew) le.preferredHeight = 300f;

            var (glg, glgNew) = GetOrAdd<GridLayoutGroup>(go);
            if (glgNew)
            {
                glg.cellSize        = new Vector2(55f, 55f);
                glg.spacing         = new Vector2(6f, 6f);
                glg.padding         = new RectOffset(10, 10, 10, 10);
                glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
                glg.constraintCount = 4;
            }

            for (int i = 0; i < 16; i++)
            {
                var (slotGO, _)           = GetOrCreateChild(go.transform, $"Slot_{i}");
                var (slotImg, slotImgNew) = GetOrAdd<Image>(slotGO);
                if (slotImgNew) slotImg.color = ColSlotBg;
                GetOrAdd<InventorySlotView>(slotGO);
            }
        }

        private static void SyncGoldArea(Transform parent)
        {
            var (go, _) = GetOrCreateChild(parent, "GoldArea");

            var (img, imgNew) = GetOrAdd<Image>(go);
            if (imgNew) img.color = ColTitleBg;

            var (le, leNew) = GetOrAdd<LayoutElement>(go);
            if (leNew) le.preferredHeight = 40f;

            var (goldTextGO, goldNew) = GetOrCreateChild(go.transform, "GoldText");
            if (goldNew) SetStretch(goldTextGO, 10f, 10f, 0f, 0f);
            var (tmp, tmpNew) = GetOrAdd<TextMeshProUGUI>(goldTextGO);
            if (tmpNew) ApplyTMP(tmp, "0", 18f, ColGold, TextAlignmentOptions.Right);
        }

        // ════════════════════════════════════════════════════════════
        // 참조 연결 (구조적 연결 → 항상 재연결)
        // ════════════════════════════════════════════════════════════

        private static void WireShopPanelView(GameObject shopPanel)
        {
            var view = shopPanel.GetComponent<ShopPanelView>();
            if (view == null) return;

            var so        = new SerializedObject(view);
            var itemViews = shopPanel.GetComponentsInChildren<ShopItemView>();

            var itemViewsProp = so.FindProperty("_itemViews");
            itemViewsProp.arraySize = itemViews.Length;
            for (int i = 0; i < itemViews.Length; i++)
                itemViewsProp.GetArrayElementAtIndex(i).objectReferenceValue = itemViews[i];

            so.FindProperty("_detailNameText").objectReferenceValue =
                shopPanel.transform.Find("DetailArea/InfoGroup/DetailNameText")
                                   ?.GetComponent<TextMeshProUGUI>();

            // _detailRarityText / _detailDescText 는 수동 생성된 경로(DetailDescArea)를 우선,
            // 없으면 빌더 기본 경로(InfoGroup) 사용
            so.FindProperty("_detailRarityText").objectReferenceValue =
                (shopPanel.transform.Find("DetailArea/DetailDescArea/RarityText")
                 ?? shopPanel.transform.Find("DetailArea/InfoGroup/DetailRarityText"))
                ?.GetComponent<TextMeshProUGUI>();

            so.FindProperty("_detailDescText").objectReferenceValue =
                (shopPanel.transform.Find("DetailArea/DetailDescArea/DescText")
                 ?? shopPanel.transform.Find("DetailArea/InfoGroup/DetailDescText"))
                ?.GetComponent<TextMeshProUGUI>();

            so.ApplyModifiedProperties();

            foreach (var itemView in itemViews)
                WireShopItemView(itemView);
        }

        private static void WireShopItemView(ShopItemView itemView)
        {
            var so = new SerializedObject(itemView);
            var t  = itemView.transform;

            so.FindProperty("_iconImage").objectReferenceValue =
                t.Find("IconImage")?.GetComponent<Image>();
            so.FindProperty("_nameText").objectReferenceValue =
                t.Find("InfoArea/NameText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_descriptionText").objectReferenceValue =
                t.Find("InfoArea/DescText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_priceText").objectReferenceValue =
                t.Find("PriceArea/PriceText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_buyButton").objectReferenceValue =
                t.Find("PriceArea/BuyButton")?.GetComponent<Button>();
            so.ApplyModifiedProperties();
        }

        private static void WireInventoryPanelView(GameObject inventoryPanel)
        {
            var view = inventoryPanel.GetComponent<InventoryPanelView>();
            if (view == null) return;

            var so    = new SerializedObject(view);
            var slots = inventoryPanel.GetComponentsInChildren<InventorySlotView>();

            var slotsProp = so.FindProperty("_slots");
            slotsProp.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

            so.FindProperty("_goldText").objectReferenceValue =
                inventoryPanel.transform.Find("GoldArea/GoldText")
                              ?.GetComponent<TextMeshProUGUI>();
            so.ApplyModifiedProperties();
        }

        private static void RewireTestShopPanelsIfExists(
            ShopPanelView shopPanelView, InventoryPanelView invPanelView)
        {
            var testShopGO = GameObject.Find("TestShop");
            if (testShopGO == null) return;

            var testShop = testShopGO.GetComponent<TestShop>();
            if (testShop == null) return;

            var so = new SerializedObject(testShop);
            if (shopPanelView != null)
                so.FindProperty("_shopPanel").objectReferenceValue = shopPanelView;
            if (invPanelView != null)
                so.FindProperty("_inventoryPanel").objectReferenceValue = invPanelView;
            so.ApplyModifiedProperties();

            Debug.Log("[ShopUIBuilder] TestShop 패널 재연결 완료");
        }

        // ════════════════════════════════════════════════════════════
        // Build Test Scene 전용
        // ════════════════════════════════════════════════════════════

        private static ShopData CreateOrUpdateTestShopData()
        {
            const string dir      = "Assets/_Project/ScriptableObjects/Shop";
            const string savePath = dir + "/TestShopData.asset";

            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects", "Shop");

            var shopData = AssetDatabase.LoadAssetAtPath<ShopData>(savePath);
            if (shopData == null)
            {
                shopData = ScriptableObject.CreateInstance<ShopData>();
                AssetDatabase.CreateAsset(shopData, savePath);
            }

            var guids = AssetDatabase.FindAssets("t:RelicData");
            int count = Mathf.Min(guids.Length, 4);
            shopData.Items = new ShopItemData[count];

            for (int i = 0; i < count; i++)
            {
                var relic = AssetDatabase.LoadAssetAtPath<RelicData>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                shopData.Items[i] = new ShopItemData
                {
                    Relic = relic,
                    Price = GetDefaultPrice(relic)
                };
            }

            EditorUtility.SetDirty(shopData);
            AssetDatabase.SaveAssets();
            return shopData;
        }

        private static int GetDefaultPrice(RelicData relic)
        {
            if (relic == null || relic.IsConsumable) return 50;
            return relic.Rarity switch
            {
                RelicRarity.Common    => 100,
                RelicRarity.Rare      => 200,
                RelicRarity.Unique    => 300,
                RelicRarity.Legendary => 500,
                _                     => 100
            };
        }

        private static PlayerRelicInventory FindOrCreatePlayerInventory()
        {
            var existing = Object.FindFirstObjectByType<PlayerRelicInventory>();
            if (existing != null) return existing;

            var go = new GameObject("PlayerInventory");
            return go.AddComponent<PlayerRelicInventory>();
        }

        private static void WireTestShop(ShopData shopData, PlayerRelicInventory inventory)
        {
            var testShopGO = GameObject.Find("TestShop") ?? new GameObject("TestShop");
            var testShop   = testShopGO.GetComponent<TestShop>()
                          ?? testShopGO.AddComponent<TestShop>();

            var canvas        = Object.FindFirstObjectByType<Canvas>();
            var shopPanelView = canvas?.transform.Find("ShopPanel")
                                       ?.GetComponent<ShopPanelView>();
            var invPanelView  = canvas?.transform.Find("InventoryPanel")
                                       ?.GetComponent<InventoryPanelView>();

            var so = new SerializedObject(testShop);
            so.FindProperty("_shopPanel").objectReferenceValue      = shopPanelView;
            so.FindProperty("_inventoryPanel").objectReferenceValue = invPanelView;
            so.FindProperty("_inventory").objectReferenceValue      = inventory;
            so.FindProperty("_shopData").objectReferenceValue       = shopData;
            so.FindProperty("_startGold").intValue                  = 320;
            so.ApplyModifiedProperties();
        }

        // ════════════════════════════════════════════════════════════
        // 핵심 헬퍼
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 컴포넌트를 찾거나 없으면 추가한다.
        /// wasAdded == true 이면 새로 추가된 것 → if (wasAdded) { 기본값 설정 } 패턴으로 사용.
        /// </summary>
        private static (T comp, bool wasAdded) GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) return (c, false);
            return (go.AddComponent<T>(), true);
        }

        /// <summary>
        /// 자식 오브젝트를 이름으로 찾거나 없으면 생성한다.
        /// wasCreated == true 이면 새로 만든 것 → if (wasCreated) { 위치/크기 설정 } 패턴으로 사용.
        /// </summary>
        private static (GameObject go, bool wasCreated) GetOrCreateChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return (t.gameObject, false);

            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<RectTransform>();
            return (child, true);
        }

        /// <summary>TMP 컴포넌트에 기본값을 적용한다. wasAdded == true 일 때만 호출한다.</summary>
        private static void ApplyTMP(TextMeshProUGUI tmp, string text, float fontSize,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.color     = color;
            tmp.alignment = align;
            if (s_font != null) tmp.font = s_font;
        }

        private static void SetRect(GameObject go,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var rt             = go.GetComponent<RectTransform>();
            rt.anchorMin       = anchorMin;
            rt.anchorMax       = anchorMax;
            rt.pivot           = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta       = sizeDelta;
        }

        private static void SetStretch(GameObject go,
            float left, float right, float top, float bottom)
        {
            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static Canvas FindOrCreateCanvas()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("Canvas");
                canvas             = go.AddComponent<Canvas>();
                canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
                var scaler         = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight  = 0.5f;
                go.AddComponent<GraphicRaycaster>();
            }

            // EventSystem 없으면 생성 — 없으면 마우스 휠 포함 모든 UI 이벤트가 차단됨
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
                Debug.Log("[ShopUIBuilder] EventSystem 생성 완료");
            }

            return canvas;
        }
    }
}

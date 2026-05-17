using LostMemory.Events;
using LostMemory.Relics;
using LostMemory.Shop;
using LostMemory.Stage;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostMemory.Editor.Events
{
    /// <summary>
    /// CL-228 (A 옵션) — Vending machine UI/씬 자동 셋업.
    /// ShopUIBuilder Sync 패턴 준용. 메뉴: LostMemory/Vending Machine/Sync In Active Scene.
    /// </summary>
    public static class VendingMachineUIBuilder
    {
        private static readonly Color ColPanelBg  = new(0.17f, 0.17f, 0.17f);
        private static readonly Color ColTitleBg  = new(0.10f, 0.10f, 0.10f);
        private static readonly Color ColCardBg   = new(0.22f, 0.22f, 0.22f);
        private static readonly Color ColIconBg   = new(0.30f, 0.30f, 0.30f);
        private static readonly Color ColBuyBtn   = new(0.20f, 0.45f, 0.20f);
        private static readonly Color ColSkipBtn  = new(0.45f, 0.30f, 0.20f);
        private static readonly Color ColGold     = new(0.96f, 0.77f, 0.26f);
        private static readonly Color ColSoldOut  = new(0.10f, 0.10f, 0.10f, 0.7f);

        private const string FontPath        = "Assets/_Project/Art/Fonts/Galmuri9.asset";
        private const string ConfigAssetPath = "Assets/_Project/ScriptableObjects/Events/VendingMachineConfig_RandomBox200.asset";

        private static TMP_FontAsset s_font;

        // ════════════════════════════════════════════════════════════
        // 메뉴 진입점
        // ════════════════════════════════════════════════════════════

        [MenuItem("LostMemory/Vending Machine/Sync In Active Scene")]
        public static void SyncInActiveScene()
        {
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (s_font == null)
                Debug.LogWarning($"[VendingMachineUIBuilder] Galmuri9 폰트 없음: {FontPath} (TMP 기본 폰트로 fallback)");

            var canvas = FindCanvasOrFail();
            if (canvas == null) return;

            var panelGO = SyncPanel(canvas.transform);
            WirePanelView(panelGO);

            var controllerGO = SyncController();
            WireController(controllerGO, panelGO);

            var npcGO = SyncNpc();
            WireNpc(npcGO, controllerGO);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[VendingMachineUIBuilder] ✅ Sync 완료. NPC 위치 조정 후 Play 테스트하세요.");
        }

        [MenuItem("LostMemory/Vending Machine/Wire References Only")]
        public static void WireReferencesOnly()
        {
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { Debug.LogError("[VendingMachineUIBuilder] Canvas 없음."); return; }

            var panelGO = canvas.transform.Find("VendingMachinePanel")?.gameObject;
            if (panelGO == null) { Debug.LogError("[VendingMachineUIBuilder] VendingMachinePanel 없음."); return; }
            WirePanelView(panelGO);

            var controllerGO = GameObject.Find("VendingMachineController");
            if (controllerGO != null) WireController(controllerGO, panelGO);

            var npcGO = GameObject.Find("VendingMachineNpc");
            if (npcGO != null && controllerGO != null) WireNpc(npcGO, controllerGO);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[VendingMachineUIBuilder] ✅ Wire 완료.");
        }

        // ════════════════════════════════════════════════════════════
        // Panel
        // ════════════════════════════════════════════════════════════

        private static GameObject SyncPanel(Transform canvasT)
        {
            var (panel, panelCreated) = GetOrCreateChild(canvasT, "VendingMachinePanel");
            if (panelCreated)
            {
                SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 380f));
                panel.SetActive(false);
            }

            var (panelImg, panelImgNew) = GetOrAdd<Image>(panel);
            if (panelImgNew) panelImg.color = ColPanelBg;

            GetOrAdd<VendingMachinePanelView>(panel);

            // ── TitleBar ──
            var (titleGO, titleNew) = GetOrCreateChild(panel.transform, "TitleBar");
            if (titleNew)
                SetRect(titleGO, new Vector2(0f, 1f), new Vector2(1f, 1f),
                        new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 50f));
            var (titleImg, titleImgNew) = GetOrAdd<Image>(titleGO);
            if (titleImgNew) titleImg.color = ColTitleBg;

            var (titleTextGO, titleTextNew) = GetOrCreateChild(titleGO.transform, "TitleText");
            if (titleTextNew) SetStretch(titleTextGO, 10f, 10f, 0f, 0f);
            var (titleTMP, titleTMPNew) = GetOrAdd<TextMeshProUGUI>(titleTextGO);
            if (titleTMPNew) ApplyTMP(titleTMP, "미스터리 상점", 22f, Color.white, TextAlignmentOptions.Center);

            // ── ItemCard (중앙) ──
            var (cardGO, cardNew) = GetOrCreateChild(panel.transform, "ItemCard");
            if (cardNew)
                SetRect(cardGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(380f, 220f));
            var (cardImg, cardImgNew) = GetOrAdd<Image>(cardGO);
            if (cardImgNew) cardImg.color = ColCardBg;

            // Icon (상단 중앙)
            var (iconBgGO, iconBgNew) = GetOrCreateChild(cardGO.transform, "IconBg");
            if (iconBgNew)
                SetRect(iconBgGO, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(120f, 120f));
            var (iconBgImg, iconBgImgNew) = GetOrAdd<Image>(iconBgGO);
            if (iconBgImgNew) iconBgImg.color = ColIconBg;

            var (iconGO, iconNew) = GetOrCreateChild(iconBgGO.transform, "ItemIcon");
            if (iconNew) SetStretch(iconGO, 6f, 6f, 6f, 6f);
            var (iconImg, iconImgNew) = GetOrAdd<Image>(iconGO);
            if (iconImgNew) { iconImg.color = Color.white; iconImg.enabled = false; iconImg.raycastTarget = false; }

            // Name + Desc + Price (카드 하단)
            var (nameGO, nameNew) = GetOrCreateChild(cardGO.transform, "ItemNameText");
            if (nameNew)
                SetRect(nameGO, new Vector2(0f, 0f), new Vector2(1f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(-20f, 26f));
            var (nameTMP, nameTMPNew) = GetOrAdd<TextMeshProUGUI>(nameGO);
            if (nameTMPNew) ApplyTMP(nameTMP, "랜덤 박스", 18f, Color.white, TextAlignmentOptions.Center);

            var (descGO, descNew) = GetOrCreateChild(cardGO.transform, "ItemDescText");
            if (descNew)
                SetRect(descGO, new Vector2(0f, 0f), new Vector2(1f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(-20f, 22f));
            var (descTMP, descTMPNew) = GetOrAdd<TextMeshProUGUI>(descGO);
            if (descTMPNew) ApplyTMP(descTMP, "보유하지 않은 랜덤 유물 1개 획득", 13f, new Color(0.75f, 0.75f, 0.75f), TextAlignmentOptions.Center);

            var (priceGO, priceNew) = GetOrCreateChild(cardGO.transform, "PriceText");
            if (priceNew)
                SetRect(priceGO, new Vector2(0f, 0f), new Vector2(1f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-20f, 24f));
            var (priceTMP, priceTMPNew) = GetOrAdd<TextMeshProUGUI>(priceGO);
            if (priceTMPNew) ApplyTMP(priceTMP, "200 G", 22f, ColGold, TextAlignmentOptions.Center);

            // ── Buttons ──
            var (buyGO, buyNew) = GetOrCreateChild(panel.transform, "BuyButton");
            if (buyNew)
                SetRect(buyGO, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(-90f, 30f), new Vector2(160f, 44f));
            var (buyImg, buyImgNew) = GetOrAdd<Image>(buyGO);
            if (buyImgNew) buyImg.color = ColBuyBtn;
            var (buyBtn, buyBtnNew) = GetOrAdd<Button>(buyGO);
            if (buyBtnNew) buyBtn.targetGraphic = buyImg;
            var (buyTextGO, buyTextNew) = GetOrCreateChild(buyGO.transform, "BuyText");
            if (buyTextNew) SetStretch(buyTextGO, 0f, 0f, 0f, 0f);
            var (buyTMP, buyTMPNew) = GetOrAdd<TextMeshProUGUI>(buyTextGO);
            if (buyTMPNew) ApplyTMP(buyTMP, "구매", 18f, Color.white, TextAlignmentOptions.Center);

            var (skipGO, skipNew) = GetOrCreateChild(panel.transform, "SkipButton");
            if (skipNew)
                SetRect(skipGO, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(90f, 30f), new Vector2(160f, 44f));
            var (skipImg, skipImgNew) = GetOrAdd<Image>(skipGO);
            if (skipImgNew) skipImg.color = ColSkipBtn;
            var (skipBtn, skipBtnNew) = GetOrAdd<Button>(skipGO);
            if (skipBtnNew) skipBtn.targetGraphic = skipImg;
            var (skipTextGO, skipTextNew) = GetOrCreateChild(skipGO.transform, "SkipText");
            if (skipTextNew) SetStretch(skipTextGO, 0f, 0f, 0f, 0f);
            var (skipTMP, skipTMPNew) = GetOrAdd<TextMeshProUGUI>(skipTextGO);
            if (skipTMPNew) ApplyTMP(skipTMP, "건너뛰기", 18f, Color.white, TextAlignmentOptions.Center);

            // ── GoldText (TitleBar 우측) ──
            var (goldGO, goldNew) = GetOrCreateChild(panel.transform, "GoldText");
            if (goldNew)
                SetRect(goldGO, new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(1f, 1f), new Vector2(-15f, -16f), new Vector2(180f, 24f));
            var (goldTMP, goldTMPNew) = GetOrAdd<TextMeshProUGUI>(goldGO);
            if (goldTMPNew) ApplyTMP(goldTMP, "보유 골드: 0", 14f, ColGold, TextAlignmentOptions.Right);

            // SoldOutOverlay (옵션)
            var (soldGO, soldNew) = GetOrCreateChild(cardGO.transform, "SoldOutOverlay");
            if (soldNew)
            {
                SetStretch(soldGO, 0f, 0f, 0f, 0f);
                soldGO.SetActive(false);
            }
            var (soldImg, soldImgNew) = GetOrAdd<Image>(soldGO);
            if (soldImgNew) { soldImg.color = ColSoldOut; soldImg.raycastTarget = false; }

            return panel;
        }

        private static void WirePanelView(GameObject panelGO)
        {
            var view = panelGO.GetComponent<VendingMachinePanelView>();
            if (view == null) return;
            var so = new SerializedObject(view);

            so.FindProperty("itemIcon").objectReferenceValue =
                panelGO.transform.Find("ItemCard/IconBg/ItemIcon")?.GetComponent<Image>();
            so.FindProperty("itemNameText").objectReferenceValue =
                panelGO.transform.Find("ItemCard/ItemNameText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("itemDescText").objectReferenceValue =
                panelGO.transform.Find("ItemCard/ItemDescText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("priceText").objectReferenceValue =
                panelGO.transform.Find("ItemCard/PriceText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("goldText").objectReferenceValue =
                panelGO.transform.Find("GoldText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("buyButton").objectReferenceValue =
                panelGO.transform.Find("BuyButton")?.GetComponent<Button>();
            so.FindProperty("skipButton").objectReferenceValue =
                panelGO.transform.Find("SkipButton")?.GetComponent<Button>();
            so.FindProperty("soldOutOverlay").objectReferenceValue =
                panelGO.transform.Find("ItemCard/SoldOutOverlay")?.gameObject;

            so.ApplyModifiedProperties();
        }

        // ════════════════════════════════════════════════════════════
        // Controller
        // ════════════════════════════════════════════════════════════

        private static GameObject SyncController()
        {
            var existing = GameObject.Find("VendingMachineController");
            if (existing != null) return existing;

            var go = new GameObject("VendingMachineController");
            go.AddComponent<VendingMachineController>();
            Debug.Log("[VendingMachineUIBuilder] VendingMachineController 신규 생성.");
            return go;
        }

        private static void WireController(GameObject controllerGO, GameObject panelGO)
        {
            var controller = controllerGO.GetComponent<VendingMachineController>();
            if (controller == null) return;

            var so = new SerializedObject(controller);
            so.FindProperty("panel").objectReferenceValue = panelGO.GetComponent<VendingMachinePanelView>();

            // 기존 ShopController 의 ref 모두 복사
            var existingShopController = Object.FindFirstObjectByType<ShopController>();
            if (existingShopController != null)
            {
                var sso = new SerializedObject(existingShopController);
                CopyRef(sso, so, "inventoryPanel");
                CopyRef(sso, so, "shortcutBar");
                CopyRef(sso, so, "playerRelicInventory");
                CopyRef(sso, so, "playerConsumableInventory");
                CopyRef(sso, so, "goldWallet");
                CopyRef(sso, so, "playerAim");
                CopyRef(sso, so, "playerMovement");
                CopyRef(sso, so, "playerWeaponPresenter");
                CopyRef(sso, so, "playerMeleeCombo");
                CopyRef(sso, so, "playerDash");
                CopyRef(sso, so, "playerParry");
                Debug.Log("[VendingMachineUIBuilder] 기존 ShopController 의 ref 복사 완료.");
            }
            else
            {
                Debug.LogWarning("[VendingMachineUIBuilder] 기존 ShopController 없음 — Inspector 에서 수동 wire 필요.");
            }

            var roomController = Object.FindFirstObjectByType<RoomEntryRuntimeController>();
            if (roomController != null)
            {
                so.FindProperty("roomController").objectReferenceValue = roomController;
                Debug.Log($"[VendingMachineUIBuilder] roomController = '{roomController.name}' wired.");
            }

            so.ApplyModifiedProperties();
        }

        private static void CopyRef(SerializedObject from, SerializedObject to, string propName)
        {
            var src = from.FindProperty(propName);
            var dst = to.FindProperty(propName);
            if (src == null || dst == null) return;
            dst.objectReferenceValue = src.objectReferenceValue;
        }

        // ════════════════════════════════════════════════════════════
        // NPC
        // ════════════════════════════════════════════════════════════

        private static GameObject SyncNpc()
        {
            var existing = GameObject.Find("VendingMachineNpc");
            if (existing != null) return existing;

            var go = new GameObject("VendingMachineNpc");
            var player = GameObject.FindGameObjectWithTag("Player");
            go.transform.position = player != null
                ? player.transform.position + new Vector3(3f, 0f, 0f)
                : Vector3.zero;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 2f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.5f, 0.3f, 0.7f, 0.6f);
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            go.AddComponent<VendingMachineInteractable>();
            Debug.Log($"[VendingMachineUIBuilder] VendingMachineNpc 생성 (위치: {go.transform.position}). 위치 조정 후 Play.");
            return go;
        }

        private static void WireNpc(GameObject npcGO, GameObject controllerGO)
        {
            var npc = npcGO.GetComponent<VendingMachineInteractable>();
            if (npc == null) return;
            var so = new SerializedObject(npc);

            so.FindProperty("controller").objectReferenceValue =
                controllerGO.GetComponent<VendingMachineController>();

            var config = AssetDatabase.LoadAssetAtPath<VendingMachineConfig>(ConfigAssetPath);
            if (config != null) so.FindProperty("config").objectReferenceValue = config;
            else Debug.LogWarning($"[VendingMachineUIBuilder] {ConfigAssetPath} 로드 실패.");

            so.ApplyModifiedProperties();
        }

        // ════════════════════════════════════════════════════════════
        // 헬퍼 (ShopUIBuilder 패턴)
        // ════════════════════════════════════════════════════════════

        private static (T comp, bool wasAdded) GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) return (c, false);
            return (go.AddComponent<T>(), true);
        }

        private static (GameObject go, bool wasCreated) GetOrCreateChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return (t.gameObject, false);

            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<RectTransform>();
            return (child, true);
        }

        private static void ApplyTMP(TextMeshProUGUI tmp, string text, float fontSize,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            if (s_font != null) tmp.font = s_font;
        }

        private static void SetRect(GameObject go,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static void SetStretch(GameObject go,
            float left, float right, float top, float bottom)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static Canvas FindCanvasOrFail()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[VendingMachineUIBuilder] Canvas 없음. 활성 씬에 Canvas 먼저 만들어주세요.");
                return null;
            }
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
                Debug.Log("[VendingMachineUIBuilder] EventSystem 신규 생성.");
            }
            return canvas;
        }
    }
}

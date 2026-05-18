using LostMemory.Events;
using LostMemory.Relics;
using LostMemory.Rewards;
using LostMemory.Shop;
using LostMemory.Stage;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostMemory.Editor.Events
{
    /// <summary>
    /// CL-228 미스터리 구매 — 활성 씬에 UI Panel + Controller + NPC 셋업.
    /// ShopUIBuilder ([Editor/Shop/ShopUIBuilder.cs]) 의 Sync 패턴 준용 — 없는 것만 추가, 기존 인스펙터 값 보존.
    ///
    /// 사용법: 메뉴 LostMemory/Mystery Shop/Sync In Active Scene 클릭.
    /// 동작:
    ///   1. Canvas 자식으로 MysteryShopPanel 생성 (3 카드 슬롯 + 골드 표시 + Skip 버튼)
    ///   2. MysteryShopController GameObject 생성 — 기존 ShopController 의 ref 복사 + 새 panel ref
    ///   3. MysteryNpc GameObject 생성 — BoxCollider2D trigger + MysteryShopNpcInteractable
    ///   4. 자산 로드: MysteryShopConfig_Default + RewardPool
    /// </summary>
    public static class MysteryShopUIBuilder
    {
        // ── 색상 (ShopUIBuilder 와 동일 톤) ────────────────────────
        private static readonly Color ColPanelBg  = new(0.17f, 0.17f, 0.17f);
        private static readonly Color ColTitleBg  = new(0.10f, 0.10f, 0.10f);
        private static readonly Color ColCardBack = new(0.30f, 0.20f, 0.40f); // 보라 — 미스터리감
        private static readonly Color ColCardFront= new(0.22f, 0.22f, 0.22f);
        private static readonly Color ColBuyBtn   = new(0.20f, 0.45f, 0.20f);
        private static readonly Color ColSkipBtn  = new(0.45f, 0.30f, 0.20f);
        private static readonly Color ColGold     = new(0.96f, 0.77f, 0.26f);
        private static readonly Color ColSoldOut  = new(0.10f, 0.10f, 0.10f, 0.7f);

        private const string FontPath        = "Assets/_Project/Art/Fonts/Galmuri9.asset";
        private const string ConfigAssetPath = "Assets/_Project/ScriptableObjects/Events/MysteryShopConfig_Default.asset";
        private const string RewardPoolPath  = "Assets/_Project/ScriptableObjects/Reward/RewardPool.asset";

        private static TMP_FontAsset s_font;

        // ════════════════════════════════════════════════════════════
        // 메뉴 진입점
        // ════════════════════════════════════════════════════════════

        [MenuItem("LostMemory/Mystery Shop/Sync In Active Scene")]
        public static void SyncInActiveScene()
        {
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (s_font == null)
                Debug.LogWarning($"[MysteryShopUIBuilder] Galmuri9 폰트 없음: {FontPath} (TMP 기본 폰트로 fallback)");

            var canvas = FindCanvasOrFail();
            if (canvas == null) return;

            var panelGO = SyncPanel(canvas.transform);
            WirePanelView(panelGO);

            var controllerGO = SyncController();
            WireController(controllerGO, panelGO);

            var npcGO = SyncNpc();
            WireNpc(npcGO, controllerGO);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[MysteryShopUIBuilder] ✅ Sync 완료. NPC 위치 조정 후 Play 테스트하세요.");
        }

        [MenuItem("LostMemory/Mystery Shop/Wire References Only")]
        public static void WireReferencesOnly()
        {
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { Debug.LogError("[MysteryShopUIBuilder] Canvas 없음."); return; }

            var panelGO = canvas.transform.Find("MysteryShopPanel")?.gameObject;
            if (panelGO == null) { Debug.LogError("[MysteryShopUIBuilder] MysteryShopPanel 없음."); return; }
            WirePanelView(panelGO);

            var controllerGO = GameObject.Find("MysteryShopController");
            if (controllerGO != null) WireController(controllerGO, panelGO);

            var npcGO = GameObject.Find("MysteryNpc");
            if (npcGO != null && controllerGO != null) WireNpc(npcGO, controllerGO);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[MysteryShopUIBuilder] ✅ Wire 완료.");
        }

        // ════════════════════════════════════════════════════════════
        // Panel
        // ════════════════════════════════════════════════════════════

        private static GameObject SyncPanel(Transform canvasT)
        {
            var (panel, panelCreated) = GetOrCreateChild(canvasT, "MysteryShopPanel");
            if (panelCreated)
            {
                // 화면 중앙 — 가로 720, 세로 480
                SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 480f));
                panel.SetActive(false); // 평소 숨김 — Open 시 활성
            }

            var (panelImg, panelImgNew) = GetOrAdd<Image>(panel);
            if (panelImgNew) panelImg.color = ColPanelBg;

            GetOrAdd<MysteryShopPanelView>(panel);

            // ── TitleBar ──
            var (titleGO, titleNew) = GetOrCreateChild(panel.transform, "TitleBar");
            if (titleNew)
                SetRect(titleGO, new Vector2(0f, 1f), new Vector2(1f, 1f),
                        new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 50f));
            var (titleImg, titleImgNew) = GetOrAdd<Image>(titleGO);
            if (titleImgNew) titleImg.color = ColTitleBg;

            var (titleTextGO, titleTextNew) = GetOrCreateChild(titleGO.transform, "TitleText");
            if (titleTextNew) SetStretch(titleTextGO, 10f, 10f, 0f, 0f);
            var (titleTMP, titleTMPNew) = GetOrAdd<TextMeshProUGUI>(titleTextGO);
            if (titleTMPNew) ApplyTMP(titleTMP, "미스터리 상점", 22f, Color.white, TextAlignmentOptions.Center);

            // ── GoldText (TitleBar 우측) ──
            var (goldGO, goldNew) = GetOrCreateChild(panel.transform, "GoldText");
            if (goldNew)
                SetRect(goldGO, new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(1f, 1f), new Vector2(-20f, -65f), new Vector2(220f, 30f));
            var (goldTMP, goldTMPNew) = GetOrAdd<TextMeshProUGUI>(goldGO);
            if (goldTMPNew) ApplyTMP(goldTMP, "보유 골드: 0", 18f, ColGold, TextAlignmentOptions.Right);

            // ── Card slots area ──
            var (slotsGO, slotsNew) = GetOrCreateChild(panel.transform, "CardSlots");
            if (slotsNew)
                SetRect(slotsGO, new Vector2(0f, 0f), new Vector2(1f, 1f),
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-40f, -180f));
            var (hlg, hlgNew) = GetOrAdd<HorizontalLayoutGroup>(slotsGO);
            if (hlgNew)
            {
                hlg.padding                = new RectOffset(20, 20, 20, 20);
                hlg.spacing                = 30f;
                hlg.childControlWidth      = true;
                hlg.childControlHeight     = true;
                hlg.childForceExpandWidth  = true;
                hlg.childForceExpandHeight = true;
                hlg.childAlignment         = TextAnchor.MiddleCenter;
            }

            for (int i = 0; i < 3; i++)
                SyncCardSlot(slotsGO.transform, i);

            // ── SkipButton (하단) ──
            var (skipGO, skipNew) = GetOrCreateChild(panel.transform, "SkipButton");
            if (skipNew)
                SetRect(skipGO, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(180f, 40f));
            var (skipImg, skipImgNew) = GetOrAdd<Image>(skipGO);
            if (skipImgNew) skipImg.color = ColSkipBtn;
            var (skipBtn, skipBtnNew) = GetOrAdd<Button>(skipGO);
            if (skipBtnNew) skipBtn.targetGraphic = skipImg;

            var (skipTextGO, skipTextNew) = GetOrCreateChild(skipGO.transform, "SkipText");
            if (skipTextNew) SetStretch(skipTextGO, 0f, 0f, 0f, 0f);
            var (skipTMP, skipTMPNew) = GetOrAdd<TextMeshProUGUI>(skipTextGO);
            if (skipTMPNew) ApplyTMP(skipTMP, "건너뛰기", 16f, Color.white, TextAlignmentOptions.Center);

            return panel;
        }

        private static void SyncCardSlot(Transform parent, int index)
        {
            var (slot, slotCreated) = GetOrCreateChild(parent, $"CardSlot_{index}");
            if (slotCreated) slot.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 240f);

            // 슬롯 자체엔 보이는 요소 X — back/front root 가 모든 표시 담당
            GetOrAdd<MysteryCardView>(slot);

            // ── BackRoot ──
            var (backRoot, backRootNew) = GetOrCreateChild(slot.transform, "BackRoot");
            if (backRootNew) SetStretch(backRoot, 0f, 0f, 0f, 0f);
            var (backImg, backImgNew) = GetOrAdd<Image>(backRoot);
            if (backImgNew) backImg.color = ColCardBack;

            // BackPriceText (BackRoot 가운데 위쪽)
            var (backPriceGO, backPriceNew) = GetOrCreateChild(backRoot.transform, "BackPriceText");
            if (backPriceNew)
                SetRect(backPriceGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(160f, 40f));
            var (backPriceTMP, backPriceTMPNew) = GetOrAdd<TextMeshProUGUI>(backPriceGO);
            if (backPriceTMPNew) ApplyTMP(backPriceTMP, "??? G", 24f, ColGold, TextAlignmentOptions.Center);

            // BuyButton (BackRoot 하단)
            var (buyGO, buyNew) = GetOrCreateChild(backRoot.transform, "BuyButton");
            if (buyNew)
                SetRect(buyGO, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(140f, 36f));
            var (buyImg, buyImgNew) = GetOrAdd<Image>(buyGO);
            if (buyImgNew) buyImg.color = ColBuyBtn;
            var (buyBtn, buyBtnNew) = GetOrAdd<Button>(buyGO);
            if (buyBtnNew) buyBtn.targetGraphic = buyImg;

            var (buyTextGO, buyTextNew) = GetOrCreateChild(buyGO.transform, "BuyText");
            if (buyTextNew) SetStretch(buyTextGO, 0f, 0f, 0f, 0f);
            var (buyTMP, buyTMPNew) = GetOrAdd<TextMeshProUGUI>(buyTextGO);
            if (buyTMPNew) ApplyTMP(buyTMP, "구매", 16f, Color.white, TextAlignmentOptions.Center);

            // ── FrontRoot (기본 비활성) ──
            var (frontRoot, frontRootNew) = GetOrCreateChild(slot.transform, "FrontRoot");
            if (frontRootNew)
            {
                SetStretch(frontRoot, 0f, 0f, 0f, 0f);
                frontRoot.SetActive(false);
            }
            var (frontImg, frontImgNew) = GetOrAdd<Image>(frontRoot);
            if (frontImgNew) frontImg.color = ColCardFront;

            // FrontIcon (FrontRoot 상단 - 가운데 정사각형)
            var (frontIconGO, frontIconNew) = GetOrCreateChild(frontRoot.transform, "FrontIcon");
            if (frontIconNew)
                SetRect(frontIconGO, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(120f, 120f));
            var (frontIconImg, frontIconImgNew) = GetOrAdd<Image>(frontIconGO);
            if (frontIconImgNew) { frontIconImg.color = Color.white; frontIconImg.enabled = false; }

            // FrontNameText (아이콘 아래)
            var (frontNameGO, frontNameNew) = GetOrCreateChild(frontRoot.transform, "FrontNameText");
            if (frontNameNew)
                SetRect(frontNameGO, new Vector2(0f, 0f), new Vector2(1f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(-10f, 30f));
            var (frontNameTMP, frontNameTMPNew) = GetOrAdd<TextMeshProUGUI>(frontNameGO);
            if (frontNameTMPNew) ApplyTMP(frontNameTMP, "", 16f, Color.white, TextAlignmentOptions.Center);

            // FrontRarityTag
            var (frontRarityGO, frontRarityNew) = GetOrCreateChild(frontRoot.transform, "FrontRarityTagText");
            if (frontRarityNew)
                SetRect(frontRarityGO, new Vector2(0f, 0f), new Vector2(1f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(-10f, 24f));
            var (frontRarityTMP, frontRarityTMPNew) = GetOrAdd<TextMeshProUGUI>(frontRarityGO);
            if (frontRarityTMPNew) ApplyTMP(frontRarityTMP, "", 13f, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Center);

            // FrontPriceText (옵션 — 작게 우상단)
            var (frontPriceGO, frontPriceNew) = GetOrCreateChild(frontRoot.transform, "FrontPriceText");
            if (frontPriceNew)
                SetRect(frontPriceGO, new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(80f, 20f));
            var (frontPriceTMP, frontPriceTMPNew) = GetOrAdd<TextMeshProUGUI>(frontPriceGO);
            if (frontPriceTMPNew) ApplyTMP(frontPriceTMP, "", 12f, ColGold, TextAlignmentOptions.Right);

            // SoldOutOverlay (옵션 — 회색 전체 덮개)
            var (soldGO, soldNew) = GetOrCreateChild(slot.transform, "SoldOutOverlay");
            if (soldNew)
            {
                SetStretch(soldGO, 0f, 0f, 0f, 0f);
                soldGO.SetActive(false);
            }
            var (soldImg, soldImgNew) = GetOrAdd<Image>(soldGO);
            if (soldImgNew) { soldImg.color = ColSoldOut; soldImg.raycastTarget = false; }
        }

        private static void WirePanelView(GameObject panelGO)
        {
            var view = panelGO.GetComponent<MysteryShopPanelView>();
            if (view == null) return;

            var so = new SerializedObject(view);

            // cardViews[3]
            var cardSlotsT = panelGO.transform.Find("CardSlots");
            var slots = new MysteryCardView[3];
            for (int i = 0; i < 3; i++)
            {
                var t = cardSlotsT?.Find($"CardSlot_{i}");
                slots[i] = t != null ? t.GetComponent<MysteryCardView>() : null;
            }
            var cardViewsProp = so.FindProperty("cardViews");
            cardViewsProp.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
                cardViewsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

            so.FindProperty("goldText").objectReferenceValue =
                panelGO.transform.Find("GoldText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("skipButton").objectReferenceValue =
                panelGO.transform.Find("SkipButton")?.GetComponent<Button>();

            so.ApplyModifiedProperties();

            // 각 카드 슬롯 wire
            foreach (var slot in slots)
                if (slot != null) WireCardView(slot);
        }

        private static void WireCardView(MysteryCardView card)
        {
            var so = new SerializedObject(card);
            var t = card.transform;

            so.FindProperty("backRoot").objectReferenceValue = t.Find("BackRoot")?.gameObject;
            so.FindProperty("frontRoot").objectReferenceValue = t.Find("FrontRoot")?.gameObject;
            so.FindProperty("backPriceText").objectReferenceValue =
                t.Find("BackRoot/BackPriceText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("buyButton").objectReferenceValue =
                t.Find("BackRoot/BuyButton")?.GetComponent<Button>();

            so.FindProperty("frontIcon").objectReferenceValue =
                t.Find("FrontRoot/FrontIcon")?.GetComponent<Image>();
            so.FindProperty("frontNameText").objectReferenceValue =
                t.Find("FrontRoot/FrontNameText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("frontRarityTagText").objectReferenceValue =
                t.Find("FrontRoot/FrontRarityTagText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("frontPriceText").objectReferenceValue =
                t.Find("FrontRoot/FrontPriceText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("soldOutOverlay").objectReferenceValue =
                t.Find("SoldOutOverlay")?.gameObject;

            so.ApplyModifiedProperties();
        }

        // ════════════════════════════════════════════════════════════
        // Controller — 기존 ShopController 의 ref 미러
        // ════════════════════════════════════════════════════════════

        private static GameObject SyncController()
        {
            var existing = GameObject.Find("MysteryShopController");
            if (existing != null) return existing;

            var go = new GameObject("MysteryShopController");
            go.AddComponent<MysteryShopController>();
            Debug.Log("[MysteryShopUIBuilder] MysteryShopController GameObject 신규 생성.");
            return go;
        }

        private static void WireController(GameObject controllerGO, GameObject panelGO)
        {
            var controller = controllerGO.GetComponent<MysteryShopController>();
            if (controller == null) return;

            var so = new SerializedObject(controller);

            // 1. panel
            so.FindProperty("panel").objectReferenceValue = panelGO.GetComponent<MysteryShopPanelView>();

            // 2. 기존 ShopController 가 있으면 그 ref 모두 복사 (player 컴포넌트 + wallet + inventory)
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
                Debug.Log("[MysteryShopUIBuilder] 기존 ShopController 의 ref 복사 완료.");
            }
            else
            {
                Debug.LogWarning("[MysteryShopUIBuilder] 기존 ShopController 없음 — Inspector 에서 수동 wire 필요.");
            }

            // 3. roomController — 검증 씬 단독은 비워둠 (출구 해제 skip 됨, console 경고만)
            // 자동 탐색: 활성 씬에 RoomEntryRuntimeController 있으면 첫 번째 wire
            var roomController = Object.FindFirstObjectByType<RoomEntryRuntimeController>();
            if (roomController != null)
            {
                so.FindProperty("roomController").objectReferenceValue = roomController;
                Debug.Log($"[MysteryShopUIBuilder] roomController = '{roomController.name}' wired.");
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
        // NPC — Trigger zone + MysteryShopNpcInteractable
        // ════════════════════════════════════════════════════════════

        private static GameObject SyncNpc()
        {
            var existing = GameObject.Find("MysteryNpc");
            if (existing != null) return existing;

            var go = new GameObject("MysteryNpc");
            // 위치 — Player 근처 + 우측 3 unit 으로 임시 배치 (사용자가 옮기면 됨)
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                go.transform.position = player.transform.position + new Vector3(3f, 0f, 0f);
            else
                go.transform.position = Vector3.zero;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 2f);

            // 시각용 Sprite (placeholder — 흰 사각형)
            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.5f, 0.3f, 0.7f, 0.6f);
            // sprite 비워두면 안 보임 → 임시로 Unity built-in UI sprite 사용
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            go.AddComponent<MysteryShopNpcInteractable>();
            Debug.Log($"[MysteryShopUIBuilder] MysteryNpc 생성 (위치: {go.transform.position}). 위치 조정 후 Play.");
            return go;
        }

        private static void WireNpc(GameObject npcGO, GameObject controllerGO)
        {
            var npc = npcGO.GetComponent<MysteryShopNpcInteractable>();
            if (npc == null) return;

            var so = new SerializedObject(npc);

            so.FindProperty("mysteryShopController").objectReferenceValue =
                controllerGO.GetComponent<MysteryShopController>();

            // 자산 로드 — config + reward pool
            var config = AssetDatabase.LoadAssetAtPath<MysteryShopConfig>(ConfigAssetPath);
            if (config != null) so.FindProperty("mysteryShopConfig").objectReferenceValue = config;
            else Debug.LogWarning($"[MysteryShopUIBuilder] {ConfigAssetPath} 로드 실패.");

            var pool = AssetDatabase.LoadAssetAtPath<RewardPool>(RewardPoolPath);
            if (pool != null) so.FindProperty("rewardPool").objectReferenceValue = pool;
            else Debug.LogWarning($"[MysteryShopUIBuilder] {RewardPoolPath} 로드 실패.");

            // PlayerRelicInventory — 기존 ShopController 와 동일하게 씬에서 첫 번째 사용
            var inventory = Object.FindFirstObjectByType<PlayerRelicInventory>();
            if (inventory != null) so.FindProperty("playerRelicInventory").objectReferenceValue = inventory;

            so.ApplyModifiedProperties();
        }

        // ════════════════════════════════════════════════════════════
        // 헬퍼 (ShopUIBuilder 와 동일 패턴)
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
                Debug.LogError("[MysteryShopUIBuilder] Canvas 없음. 활성 씬에 Canvas 먼저 만들어주세요.");
                return null;
            }
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
                Debug.Log("[MysteryShopUIBuilder] EventSystem 신규 생성.");
            }
            return canvas;
        }
    }
}

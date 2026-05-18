using LostMemory.Events;
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
    /// CL-227 V2 — 카드 뽑기 UI/씬 자동 셋업.
    /// VendingMachineUIBuilder 패턴 복제. 메뉴: LostMemory/Card Draw/Sync In Active Scene.
    /// </summary>
    public static class CardDrawUIBuilder
    {
        // ── 색상 ────────────────────────────────────────────────────
        // (back/front 자체는 sprite — 코드 색상은 패널/버튼/텍스트만)
        private static readonly Color ColPanelBg     = new(0.17f, 0.17f, 0.17f);
        private static readonly Color ColTitleBg     = new(0.10f, 0.10f, 0.10f);
        private static readonly Color ColPickBtn     = new(0.20f, 0.45f, 0.60f);
        private static readonly Color ColSkipBtn     = new(0.45f, 0.30f, 0.20f);
        private static readonly Color ColGold        = new(0.96f, 0.77f, 0.26f);
        private static readonly Color ColInfo        = new(0.85f, 0.85f, 0.85f);

        private const string FontPath        = "Assets/_Project/Art/Fonts/Galmuri9.asset";
        private const string ConfigAssetPath = "Assets/_Project/ScriptableObjects/Events/CardDrawConfig_Default.asset";

        // Card sprite paths — 2D Pixel Quest Vol.3
        private const string BackSpriteBase  = "Assets/2D Pixel Quest Vol.3 - The UI-GUI/Sprites PNG/Skill Cards- Flip Animations/Back Face Flip/Back Face Flip A/F_U_CardA_Back_Flip";
        private const string FrontSpriteBase = "Assets/2D Pixel Quest Vol.3 - The UI-GUI/Sprites PNG/Skill Cards- Flip Animations/Skills face flip - Blank/F_U_Blank Yellow Card_Flip";

        private static TMP_FontAsset s_font;
        private static Sprite[] s_backFrames;
        private static Sprite[] s_frontFrames;

        // ════════════════════════════════════════════════════════════
        // 메뉴 진입점
        // ════════════════════════════════════════════════════════════

        [MenuItem("LostMemory/Card Draw/Sync In Active Scene")]
        public static void SyncInActiveScene()
        {
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (s_font == null)
                Debug.LogWarning($"[CardDrawUIBuilder] Galmuri9 폰트 없음: {FontPath} (TMP 기본 폰트 fallback)");

            LoadCardSprites();

            var canvas = FindCanvasOrFail();
            if (canvas == null) return;

            var panelGO = SyncPanel(canvas.transform);
            WirePanelView(panelGO);

            var controllerGO = SyncController();
            WireController(controllerGO, panelGO);

            var npcGO = SyncNpc();
            WireNpc(npcGO, controllerGO);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[CardDrawUIBuilder] ✅ Sync 완료. NPC 위치 조정 후 Play 테스트하세요.");
        }

        /// <summary>
        /// 현재 씬의 CardDrawNpc / CardDrawPanel 를 prefab 파일로 저장.
        /// 경로: Assets/_Project/Prefabs/NPC/CardDrawNpc.prefab, .../CardDrawPanel.prefab
        /// 한 번 실행하면 prefab 생성 + 씬 인스턴스가 prefab 에 연결됨.
        /// 이후엔 prefab 에서 디자인 수정 → 모든 인스턴스 자동 반영.
        /// </summary>
        [MenuItem("LostMemory/Card Draw/Save As Prefabs")]
        public static void SaveAsPrefabs()
        {
            const string folder = "Assets/_Project/Prefabs/NPC";
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder(folder);

            int saved = 0;

            var npcGO = GameObject.Find("CardDrawNpc");
            if (npcGO != null)
            {
                string path = $"{folder}/CardDrawNpc.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(npcGO, path, InteractionMode.UserAction);
                Debug.Log($"[CardDrawUIBuilder] CardDrawNpc → {path}");
                saved++;
            }
            else Debug.LogWarning("[CardDrawUIBuilder] CardDrawNpc 없음 — Sync 먼저 실행하세요.");

            // Panel 은 Canvas 자식이라 GameObject.Find 로 못 찾을 수도 — 활성 씬에서 검색.
            GameObject panelGO = null;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                var t = canvas.transform.Find("CardDrawPanel");
                if (t != null) panelGO = t.gameObject;
            }
            if (panelGO != null)
            {
                string path = $"{folder}/CardDrawPanel.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(panelGO, path, InteractionMode.UserAction);
                Debug.Log($"[CardDrawUIBuilder] CardDrawPanel → {path}");
                saved++;
            }
            else Debug.LogWarning("[CardDrawUIBuilder] Canvas/CardDrawPanel 없음 — Sync 먼저 실행하세요.");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardDrawUIBuilder] ✅ Save As Prefabs 완료 — {saved}개 저장.");
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            var parent = System.IO.Path.GetDirectoryName(folderPath).Replace('\\', '/');
            var name = System.IO.Path.GetFileName(folderPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        [MenuItem("LostMemory/Card Draw/Wire References Only")]
        public static void WireReferencesOnly()
        {
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            LoadCardSprites();

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { Debug.LogError("[CardDrawUIBuilder] Canvas 없음."); return; }

            var panelGO = canvas.transform.Find("CardDrawPanel")?.gameObject;
            if (panelGO == null) { Debug.LogError("[CardDrawUIBuilder] CardDrawPanel 없음."); return; }
            WirePanelView(panelGO);

            var controllerGO = GameObject.Find("CardDrawController");
            if (controllerGO != null) WireController(controllerGO, panelGO);

            var npcGO = GameObject.Find("CardDrawNpc");
            if (npcGO != null && controllerGO != null) WireNpc(npcGO, controllerGO);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[CardDrawUIBuilder] ✅ Wire 완료.");
        }

        // ════════════════════════════════════════════════════════════
        // Panel
        // ════════════════════════════════════════════════════════════

        private static GameObject SyncPanel(Transform canvasT)
        {
            var (panel, panelCreated) = GetOrCreateChild(canvasT, "CardDrawPanel");
            if (panelCreated)
            {
                SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 520f));
                panel.SetActive(false);
            }

            var (panelImg, panelImgNew) = GetOrAdd<Image>(panel);
            if (panelImgNew) panelImg.color = ColPanelBg;

            GetOrAdd<CardDrawPanelView>(panel);

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
            if (titleTMPNew) ApplyTMP(titleTMP, "카드 뽑기", 22f, Color.white, TextAlignmentOptions.Center);

            // ── GoldText (우측 상단) ──
            var (goldGO, goldNew) = GetOrCreateChild(panel.transform, "GoldText");
            if (goldNew)
                SetRect(goldGO, new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(1f, 1f), new Vector2(-15f, -65f), new Vector2(220f, 28f));
            var (goldTMP, goldTMPNew) = GetOrAdd<TextMeshProUGUI>(goldGO);
            if (goldTMPNew) ApplyTMP(goldTMP, "보유 골드: 0", 16f, ColGold, TextAlignmentOptions.Right);

            // ── InfoText (좌측 상단) ──
            var (infoGO, infoNew) = GetOrCreateChild(panel.transform, "InfoText");
            if (infoNew)
                SetRect(infoGO, new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(0f, 1f), new Vector2(15f, -65f), new Vector2(380f, 28f));
            var (infoTMP, infoTMPNew) = GetOrAdd<TextMeshProUGUI>(infoGO);
            if (infoTMPNew) ApplyTMP(infoTMP, "100G 를 걸고 카드 1장 선택", 14f, ColInfo, TextAlignmentOptions.Left);

            // ── CardSlots (HLG) ──
            var (slotsGO, slotsNew) = GetOrCreateChild(panel.transform, "CardSlots");
            if (slotsNew)
                SetRect(slotsGO, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(-40f, 280f));
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

            // ── ResultText (카드 영역 아래 / Skip 버튼 위) ──
            var (resultGO, resultNew) = GetOrCreateChild(panel.transform, "ResultText");
            if (resultNew)
                SetRect(resultGO, new Vector2(0f, 0f), new Vector2(1f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 85f), new Vector2(-40f, 36f));
            var (resultTMP, resultTMPNew) = GetOrAdd<TextMeshProUGUI>(resultGO);
            if (resultTMPNew) ApplyTMP(resultTMP, "", 20f, Color.white, TextAlignmentOptions.Center);

            // ── SkipButton (픽 전 무료 종료) ──
            var (skipGO, skipNew) = GetOrCreateChild(panel.transform, "SkipButton");
            if (skipNew)
                SetRect(skipGO, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(180f, 44f));
            var (skipImg, skipImgNew) = GetOrAdd<Image>(skipGO);
            if (skipImgNew) skipImg.color = ColSkipBtn;
            var (skipBtn, skipBtnNew) = GetOrAdd<Button>(skipGO);
            if (skipBtnNew) skipBtn.targetGraphic = skipImg;

            var (skipTextGO, skipTextNew) = GetOrCreateChild(skipGO.transform, "SkipText");
            if (skipTextNew) SetStretch(skipTextGO, 0f, 0f, 0f, 0f);
            var (skipTMP, skipTMPNew) = GetOrAdd<TextMeshProUGUI>(skipTextGO);
            if (skipTMPNew) ApplyTMP(skipTMP, "건너뛰기", 18f, Color.white, TextAlignmentOptions.Center);

            // ── CloseButton (X) — 우측 상단 코너, 픽 전/후 항상 닫기 ──
            var (closeGO, closeNew) = GetOrCreateChild(panel.transform, "CloseButton");
            if (closeNew)
                SetRect(closeGO, new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(1f, 1f), new Vector2(-15f, -15f), new Vector2(36f, 36f));
            var (closeImg, closeImgNew) = GetOrAdd<Image>(closeGO);
            if (closeImgNew) closeImg.color = new Color(0.35f, 0.20f, 0.20f);
            var (closeBtn, closeBtnNew) = GetOrAdd<Button>(closeGO);
            if (closeBtnNew) closeBtn.targetGraphic = closeImg;

            var (closeTextGO, closeTextNew) = GetOrCreateChild(closeGO.transform, "CloseText");
            if (closeTextNew) SetStretch(closeTextGO, 0f, 0f, 0f, 0f);
            var (closeTMP, closeTMPNew) = GetOrAdd<TextMeshProUGUI>(closeTextGO);
            if (closeTMPNew) ApplyTMP(closeTMP, "X", 22f, Color.white, TextAlignmentOptions.Center);

            return panel;
        }

        private static void SyncCardSlot(Transform parent, int index)
        {
            var (slot, slotCreated) = GetOrCreateChild(parent, $"CardSlot_{index}");
            if (slotCreated) slot.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 280f);

            GetOrAdd<CardDrawCardView>(slot);

            // ── BackRoot — 카드 뒷면 sprite (Card A Back) + cost text + pick button ──
            var (backRoot, backRootNew) = GetOrCreateChild(slot.transform, "BackRoot");
            if (backRootNew) SetStretch(backRoot, 0f, 0f, 0f, 0f);
            var (backImg, backImgNew) = GetOrAdd<Image>(backRoot);
            // 기본 idle = Flip01 (정면). Runtime entrance flip 이 Flip04 → 01 로 펼침.
            if (s_backFrames != null && s_backFrames.Length == 4 && s_backFrames[0] != null)
                backImg.sprite = s_backFrames[0];
            backImg.color = Color.white;
            backImg.preserveAspect = true;

            // 이전 데코 (코드로만 만들었던) GameObject 모두 정리 — sprite 카드로 교체.
            foreach (var legacyName in new[] { "BackFrame", "BackSymbol", "BackQuestion",
                                                "BackCornerTL", "BackCornerTR", "BackCornerBL", "BackCornerBR" })
            {
                var legacy = backRoot.transform.Find(legacyName);
                if (legacy != null) Object.DestroyImmediate(legacy.gameObject);
            }

            // BackEntryCostText — 카드 상단 작은 영역 (Card A 디자인의 상단 frame 안쪽)
            var (costGO, costNew) = GetOrCreateChild(backRoot.transform, "BackEntryCostText");
            if (costNew)
                SetRect(costGO, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(100f, 22f));
            var (costTMP, costTMPNew) = GetOrAdd<TextMeshProUGUI>(costGO);
            if (costTMPNew)
            {
                ApplyTMP(costTMP, "100 G", 14f, ColGold, TextAlignmentOptions.Center);
                costTMP.raycastTarget = false;
            }

            // PickButton (BackRoot 하단)
            var (btnGO, btnNew) = GetOrCreateChild(backRoot.transform, "PickButton");
            if (btnNew)
                SetRect(btnGO, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(140f, 40f));
            var (btnImg, btnImgNew) = GetOrAdd<Image>(btnGO);
            if (btnImgNew) btnImg.color = ColPickBtn;
            var (btn, btnCompNew) = GetOrAdd<Button>(btnGO);
            if (btnCompNew) btn.targetGraphic = btnImg;

            var (btnTextGO, btnTextNew) = GetOrCreateChild(btnGO.transform, "PickText");
            if (btnTextNew) SetStretch(btnTextGO, 0f, 0f, 0f, 0f);
            var (btnTMP, btnTMPNew) = GetOrAdd<TextMeshProUGUI>(btnTextGO);
            if (btnTMPNew) ApplyTMP(btnTMP, "선택", 16f, Color.white, TextAlignmentOptions.Center);

            // ── FrontRoot — Blank Yellow Card sprite (기본 비활성, reveal 시 활성) ──
            var (frontRoot, frontRootNew) = GetOrCreateChild(slot.transform, "FrontRoot");
            if (frontRootNew)
            {
                SetStretch(frontRoot, 0f, 0f, 0f, 0f);
                frontRoot.SetActive(false);
            }
            var (frontImg, frontImgNew) = GetOrAdd<Image>(frontRoot);
            // 기본 = Flip04 (정면 — Yellow 카드는 [3] 이 정면. back 과 반대).
            if (s_frontFrames != null && s_frontFrames.Length == 4 && s_frontFrames[3] != null)
                frontImg.sprite = s_frontFrames[3];
            frontImg.color = Color.white;
            frontImg.preserveAspect = true;

            // FrontLabelText (상단 큼지막)
            var (labelGO, labelNew) = GetOrCreateChild(frontRoot.transform, "FrontLabelText");
            if (labelNew)
                SetRect(labelGO, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(-10f, 50f));
            var (labelTMP, labelTMPNew) = GetOrAdd<TextMeshProUGUI>(labelGO);
            if (labelTMPNew) ApplyTMP(labelTMP, "", 30f, Color.white, TextAlignmentOptions.Center);

            // FrontReturnText (라벨 아래)
            var (retGO, retNew) = GetOrCreateChild(frontRoot.transform, "FrontReturnText");
            if (retNew)
                SetRect(retGO, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(-10f, 36f));
            var (retTMP, retTMPNew) = GetOrAdd<TextMeshProUGUI>(retGO);
            if (retTMPNew) ApplyTMP(retTMP, "", 22f, ColGold, TextAlignmentOptions.Center);
        }

        private static void WirePanelView(GameObject panelGO)
        {
            var view = panelGO.GetComponent<CardDrawPanelView>();
            if (view == null) return;
            var so = new SerializedObject(view);

            // cardViews[3]
            var cardSlotsT = panelGO.transform.Find("CardSlots");
            var slots = new CardDrawCardView[3];
            for (int i = 0; i < 3; i++)
            {
                var t = cardSlotsT?.Find($"CardSlot_{i}");
                slots[i] = t != null ? t.GetComponent<CardDrawCardView>() : null;
            }
            var cardViewsProp = so.FindProperty("cardViews");
            cardViewsProp.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
                cardViewsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

            so.FindProperty("goldText").objectReferenceValue =
                panelGO.transform.Find("GoldText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("infoText").objectReferenceValue =
                panelGO.transform.Find("InfoText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("resultText").objectReferenceValue =
                panelGO.transform.Find("ResultText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("skipButton").objectReferenceValue =
                panelGO.transform.Find("SkipButton")?.GetComponent<Button>();
            so.FindProperty("closeButton").objectReferenceValue =
                panelGO.transform.Find("CloseButton")?.GetComponent<Button>();

            so.ApplyModifiedProperties();

            foreach (var slot in slots)
                if (slot != null) WireCardView(slot);
        }

        private static void WireCardView(CardDrawCardView card)
        {
            var so = new SerializedObject(card);
            var t = card.transform;

            so.FindProperty("backRoot").objectReferenceValue = t.Find("BackRoot")?.gameObject;
            so.FindProperty("frontRoot").objectReferenceValue = t.Find("FrontRoot")?.gameObject;

            // Image refs — sprite swap 대상
            so.FindProperty("backImage").objectReferenceValue =
                t.Find("BackRoot")?.GetComponent<Image>();
            so.FindProperty("frontImage").objectReferenceValue =
                t.Find("FrontRoot")?.GetComponent<Image>();

            // Sprite frame arrays — 4프레임씩
            WireSpriteArray(so, "backFrames", s_backFrames);
            WireSpriteArray(so, "frontFrames", s_frontFrames);

            so.FindProperty("backEntryCostText").objectReferenceValue =
                t.Find("BackRoot/BackEntryCostText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("pickButton").objectReferenceValue =
                t.Find("BackRoot/PickButton")?.GetComponent<Button>();
            so.FindProperty("frontLabelText").objectReferenceValue =
                t.Find("FrontRoot/FrontLabelText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("frontReturnText").objectReferenceValue =
                t.Find("FrontRoot/FrontReturnText")?.GetComponent<TextMeshProUGUI>();

            so.ApplyModifiedProperties();
        }

        private static void WireSpriteArray(SerializedObject so, string propName, Sprite[] sprites)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;
            int n = sprites?.Length ?? 0;
            prop.arraySize = n;
            for (int i = 0; i < n; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        // ════════════════════════════════════════════════════════════
        // Controller
        // ════════════════════════════════════════════════════════════

        private static GameObject SyncController()
        {
            var existing = GameObject.Find("CardDrawController");
            if (existing != null) return existing;

            var go = new GameObject("CardDrawController");
            go.AddComponent<CardDrawController>();
            Debug.Log("[CardDrawUIBuilder] CardDrawController 신규 생성.");
            return go;
        }

        private static void WireController(GameObject controllerGO, GameObject panelGO)
        {
            var controller = controllerGO.GetComponent<CardDrawController>();
            if (controller == null) return;
            var so = new SerializedObject(controller);

            so.FindProperty("panel").objectReferenceValue = panelGO.GetComponent<CardDrawPanelView>();

            // 기존 ShopController ref 복사
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
                Debug.Log("[CardDrawUIBuilder] 기존 ShopController 의 ref 복사 완료.");
            }
            else
            {
                Debug.LogWarning("[CardDrawUIBuilder] 기존 ShopController 없음 — Inspector 에서 수동 wire 필요.");
            }

            var roomController = Object.FindFirstObjectByType<RoomEntryRuntimeController>();
            if (roomController != null)
            {
                so.FindProperty("roomController").objectReferenceValue = roomController;
                Debug.Log($"[CardDrawUIBuilder] roomController = '{roomController.name}' wired.");
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
            var existing = GameObject.Find("CardDrawNpc");
            if (existing != null) return existing;

            var go = new GameObject("CardDrawNpc");
            var player = GameObject.FindGameObjectWithTag("Player");
            go.transform.position = player != null
                ? player.transform.position + new Vector3(-3f, 0f, 0f)  // Vending(+3) 과 다른 방향
                : Vector3.zero;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 2f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.3f, 0.5f, 0.7f, 0.6f); // 파랑 — Vending(보라) 과 구분
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            go.AddComponent<CardDrawNpcInteractable>();
            Debug.Log($"[CardDrawUIBuilder] CardDrawNpc 생성 (위치: {go.transform.position}). 위치 조정 후 Play.");
            return go;
        }

        private static void WireNpc(GameObject npcGO, GameObject controllerGO)
        {
            var npc = npcGO.GetComponent<CardDrawNpcInteractable>();
            if (npc == null) return;
            var so = new SerializedObject(npc);

            so.FindProperty("controller").objectReferenceValue =
                controllerGO.GetComponent<CardDrawController>();

            var config = AssetDatabase.LoadAssetAtPath<CardDrawConfig>(ConfigAssetPath);
            if (config != null) so.FindProperty("config").objectReferenceValue = config;
            else Debug.LogWarning($"[CardDrawUIBuilder] {ConfigAssetPath} 로드 실패.");

            so.ApplyModifiedProperties();
        }

        // ════════════════════════════════════════════════════════════
        // 헬퍼
        // ════════════════════════════════════════════════════════════

        /// <summary>Card A Back + Blank Yellow Front sprite 4프레임씩 로드. AssetDatabase 캐시 활용.</summary>
        private static void LoadCardSprites()
        {
            s_backFrames = new Sprite[4];
            s_frontFrames = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                string num = (i + 1).ToString("D2"); // 01,02,03,04
                s_backFrames[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{BackSpriteBase}{num}.png");
                s_frontFrames[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{FrontSpriteBase}{num}.png");
                if (s_backFrames[i] == null)
                    Debug.LogWarning($"[CardDrawUIBuilder] Back sprite 로드 실패: {BackSpriteBase}{num}.png");
                if (s_frontFrames[i] == null)
                    Debug.LogWarning($"[CardDrawUIBuilder] Front sprite 로드 실패: {FrontSpriteBase}{num}.png");
            }
        }

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
                Debug.LogError("[CardDrawUIBuilder] Canvas 없음. 활성 씬에 Canvas 먼저 만들어주세요.");
                return null;
            }
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
                Debug.Log("[CardDrawUIBuilder] EventSystem 신규 생성.");
            }
            return canvas;
        }
    }
}

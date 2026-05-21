using System.IO;
using LostMemory.Friend;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostMemory.Editor.Friend
{
    /// <summary>
    /// 소꿉친구 채팅 테스트 씬 빌더.
    /// ShopUIBuilder 와 동일한 "Sync" 패턴(없는 것만 추가, 기존 값 보존)을 따른다.
    ///
    /// 메뉴:
    ///   LostMemory/Friend/Build Test Chat Scene
    ///     → ChatLine 프리팹 생성 + FriendCharacterContext 에셋 생성
    ///     + Canvas + Panel + Controller 세팅까지 한 번에.
    ///   LostMemory/Friend/Sync Chat UI
    ///     → Panel 구조만 재동기화 (이미 만든 씬 손상 없이 재실행 가능).
    /// </summary>
    public static class FriendChatSceneBuilder
    {
        // ── 색상 ────────────────────────────────────────────────────────
        private static readonly Color ColPanelBg    = new(0.10f, 0.10f, 0.12f, 0.92f);
        private static readonly Color ColHeaderBg   = new(0.06f, 0.06f, 0.08f, 1.00f);
        private static readonly Color ColScrollBg   = new(0.13f, 0.13f, 0.16f, 1.00f);
        private static readonly Color ColInputBg    = new(0.18f, 0.18f, 0.22f, 1.00f);
        private static readonly Color ColSendBtn    = new(0.30f, 0.50f, 0.85f, 1.00f);
        private static readonly Color ColCloseBtn   = new(0.40f, 0.20f, 0.20f, 1.00f);
        private static readonly Color ColLogText    = new(0.92f, 0.92f, 0.94f, 1.00f);
        private static readonly Color ColScrollbar  = new(0.10f, 0.10f, 0.12f, 1.00f);
        private static readonly Color ColHandle     = new(0.40f, 0.40f, 0.45f, 1.00f);

        // ── 경로 ────────────────────────────────────────────────────────
        private const string FontPath          = "Assets/_Project/Art/Fonts/Galmuri9.asset";
        private const string PrefabFolder      = "Assets/_Project/Prefabs/Friend";
        private const string ChatLinePrefab    = PrefabFolder + "/ChatLine.prefab";
        private const string ContextFolder     = "Assets/_Project/Data";
        private const string ContextAssetPath  = ContextFolder + "/FriendCharacterContext_Luana.asset";

        private static TMP_FontAsset s_font;

        // ════════════════════════════════════════════════════════════════
        // 메뉴 진입점
        // ════════════════════════════════════════════════════════════════

        [MenuItem("LostMemory/Friend/Build Test Chat Scene")]
        public static void BuildTestChatScene()
        {
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (s_font == null)
                Debug.LogWarning($"[FriendChatSceneBuilder] 폰트 없음: {FontPath} (기본 TMP 폰트 사용)");

            // 1. ChatLine 프리팹 생성/획득
            var chatLinePrefab = CreateOrLoadChatLinePrefab();

            // 2. 캐릭터 컨텍스트 에셋 생성/획득
            var context = CreateOrLoadCharacterContext();

            // 3. 씬: Canvas + EventSystem
            var canvas = FindOrCreateCanvas();

            // 4. 씬: Panel
            var panelGO = SyncChatPanel(canvas.transform);

            // 5. 씬: Controller
            var controllerGO = FindOrCreateController();

            // 6. 참조 연결
            WirePanelView(panelGO, chatLinePrefab, context);
            WireController(controllerGO, panelGO);

            // 7. 패널은 기본 비활성화로 둔다 (Controller 가 Open 시 켜는 흐름).
            //    하지만 테스트 편의를 위해 켜둘지 여부는 사용자 선택. 기본은 ON 으로 두어
            //    빌더 직후 Play 만 눌러도 보이게 한다.
            panelGO.SetActive(true);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[FriendChatSceneBuilder] Build 완료 — Play 후 메시지 입력 → Enter 로 테스트.");
        }

        [MenuItem("LostMemory/Friend/Sync Chat UI")]
        public static void SyncChatUI()
        {
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var canvas    = FindOrCreateCanvas();
            var panelGO   = SyncChatPanel(canvas.transform);

            var chatLinePrefab = AssetDatabase.LoadAssetAtPath<ChatMessageView>(ChatLinePrefab);
            var context        = AssetDatabase.LoadAssetAtPath<FriendCharacterContext>(ContextAssetPath);
            WirePanelView(panelGO, chatLinePrefab, context);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[FriendChatSceneBuilder] Sync 완료.");
        }

        // ════════════════════════════════════════════════════════════════
        // ChatLine 프리팹 생성
        // ════════════════════════════════════════════════════════════════

        private static ChatMessageView CreateOrLoadChatLinePrefab()
        {
            EnsureFolder(PrefabFolder);

            var existing = AssetDatabase.LoadAssetAtPath<ChatMessageView>(ChatLinePrefab);
            if (existing != null) return existing;

            // 임시 GameObject 를 만들어 프리팹으로 저장
            var temp = new GameObject("ChatLine", typeof(RectTransform));
            var tmp  = temp.AddComponent<TextMeshProUGUI>();
            tmp.text                 = "";
            tmp.fontSize             = 22f;
            tmp.color                = ColLogText;
            tmp.alignment            = TextAlignmentOptions.TopLeft;
            tmp.richText             = true;             // 핵심: 색상 태그 활성화
            tmp.enableWordWrapping   = true;
            tmp.raycastTarget        = false;
            if (s_font != null) tmp.font = s_font;

            // VerticalLayoutGroup(부모) 의 childControlHeight 가 줄 높이를 자동 계산하도록
            // ContentSizeFitter 는 줄 자체엔 붙이지 않음 (부모 VLG 가 처리).
            var view = temp.AddComponent<ChatMessageView>();
            var so   = new SerializedObject(view);
            so.FindProperty("messageText").objectReferenceValue = tmp;
            so.ApplyModifiedProperties();

            // RectTransform 초기값 — VerticalLayoutGroup 안에서는 sizeDelta 가 큰 의미 없음
            var rt = temp.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 30f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, ChatLinePrefab);
            Object.DestroyImmediate(temp);

            Debug.Log($"[FriendChatSceneBuilder] ChatLine 프리팹 생성: {ChatLinePrefab}");
            return prefab.GetComponent<ChatMessageView>();
        }

        // ════════════════════════════════════════════════════════════════
        // CharacterContext 에셋
        // ════════════════════════════════════════════════════════════════

        private static FriendCharacterContext CreateOrLoadCharacterContext()
        {
            EnsureFolder(ContextFolder);

            var existing = AssetDatabase.LoadAssetAtPath<FriendCharacterContext>(ContextAssetPath);
            if (existing != null) return existing;

            var ctx = ScriptableObject.CreateInstance<FriendCharacterContext>();
            AssetDatabase.CreateAsset(ctx, ContextAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FriendChatSceneBuilder] CharacterContext 생성: {ContextAssetPath}");
            return ctx;
        }

        // ════════════════════════════════════════════════════════════════
        // 채팅 패널 구조
        // ════════════════════════════════════════════════════════════════

        private static GameObject SyncChatPanel(Transform canvasT)
        {
            var (panel, panelCreated) = GetOrCreateChild(canvasT, "FriendChatPanel");
            if (panelCreated)
            {
                // 화면 좌하단 큰 사각형 (롤 채팅창 위치 느낌)
                SetRect(panel,
                    anchorMin: new Vector2(0f, 0f),
                    anchorMax: new Vector2(0f, 0f),
                    pivot:     new Vector2(0f, 0f),
                    anchoredPos: new Vector2(30f, 30f),
                    sizeDelta:   new Vector2(640f, 400f));
            }

            var (panelImg, panelImgNew) = GetOrAdd<Image>(panel);
            if (panelImgNew) panelImg.color = ColPanelBg;

            var (vlg, vlgNew) = GetOrAdd<VerticalLayoutGroup>(panel);
            if (vlgNew)
            {
                vlg.padding                = new RectOffset(0, 0, 0, 0);
                vlg.spacing                = 0f;
                vlg.childControlWidth      = true;
                vlg.childControlHeight     = true;
                vlg.childForceExpandWidth  = true;
                vlg.childForceExpandHeight = false;
            }

            GetOrAdd<FriendChatPanelView>(panel);

            SyncHeader(panel.transform);
            SyncScrollView(panel.transform);
            SyncInputArea(panel.transform);

            return panel;
        }

        private static void SyncHeader(Transform parent)
        {
            var (header, _) = GetOrCreateChild(parent, "Header");

            var (img, imgNew) = GetOrAdd<Image>(header);
            if (imgNew) img.color = ColHeaderBg;

            var (le, leNew) = GetOrAdd<LayoutElement>(header);
            if (leNew) le.preferredHeight = 40f;

            // TitleText
            var (titleGO, titleNew) = GetOrCreateChild(header.transform, "TitleText");
            if (titleNew) SetStretch(titleGO, 12f, 50f, 0f, 0f);
            var (titleTMP, titleTMPNew) = GetOrAdd<TextMeshProUGUI>(titleGO);
            if (titleTMPNew) ApplyTMP(titleTMP, "[루아나와의 채팅]", 18f, Color.white, TextAlignmentOptions.Left);

            // CloseButton
            var (closeGO, closeNew) = GetOrCreateChild(header.transform, "CloseButton");
            if (closeNew)
                SetRect(closeGO,
                    anchorMin: new Vector2(1f, 0.5f),
                    anchorMax: new Vector2(1f, 0.5f),
                    pivot:     new Vector2(1f, 0.5f),
                    anchoredPos: new Vector2(-6f, 0f),
                    sizeDelta:   new Vector2(28f, 28f));

            var (closeImg, closeImgNew) = GetOrAdd<Image>(closeGO);
            if (closeImgNew) closeImg.color = ColCloseBtn;
            var (closeBtn, closeBtnNew) = GetOrAdd<Button>(closeGO);
            if (closeBtnNew) closeBtn.targetGraphic = closeImg;

            var (closeTxtGO, closeTxtNew) = GetOrCreateChild(closeGO.transform, "Text");
            if (closeTxtNew) SetStretch(closeTxtGO, 0f, 0f, 0f, 0f);
            var (closeTxt, closeTxtTMPNew) = GetOrAdd<TextMeshProUGUI>(closeTxtGO);
            if (closeTxtTMPNew) ApplyTMP(closeTxt, "X", 16f, Color.white, TextAlignmentOptions.Center);
        }

        private static void SyncScrollView(Transform parent)
        {
            var (scroll, _) = GetOrCreateChild(parent, "ChatScrollView");

            var (img, imgNew) = GetOrAdd<Image>(scroll);
            if (imgNew) img.color = ColScrollBg;

            var (le, leNew) = GetOrAdd<LayoutElement>(scroll);
            if (leNew) le.flexibleHeight = 1f;

            var (sr, srNew) = GetOrAdd<ScrollRect>(scroll);
            if (srNew)
            {
                sr.horizontal   = false;
                sr.vertical     = true;
                sr.movementType = ScrollRect.MovementType.Clamped;
            }

            // Viewport
            var (vp, vpNew) = GetOrCreateChild(scroll.transform, "Viewport");
            if (vpNew) SetStretch(vp, 0f, 17f, 0f, 0f);
            var (vpImg, vpImgNew) = GetOrAdd<Image>(vp);
            if (vpImgNew) vpImg.color = new Color(1, 1, 1, 0.02f);
            var (mask, maskNew) = GetOrAdd<Mask>(vp);
            if (maskNew) mask.showMaskGraphic = false;

            // Content
            var (content, contentNew) = GetOrCreateChild(vp.transform, "Content");
            if (contentNew)
            {
                var rt              = content.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0f, 1f);
                rt.anchorMax        = new Vector2(1f, 1f);
                rt.pivot            = new Vector2(0f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = Vector2.zero;
            }
            var (cvlg, cvlgNew) = GetOrAdd<VerticalLayoutGroup>(content);
            if (cvlgNew)
            {
                cvlg.padding                = new RectOffset(8, 8, 6, 6);
                cvlg.spacing                = 2f;
                cvlg.childControlWidth      = true;
                cvlg.childControlHeight     = true;
                cvlg.childForceExpandWidth  = true;
                cvlg.childForceExpandHeight = false;
            }
            var (csf, csfNew) = GetOrAdd<ContentSizeFitter>(content);
            if (csfNew)
            {
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            }

            // Scrollbar
            var (sbGO, sbNew) = GetOrCreateChild(scroll.transform, "Scrollbar Vertical");
            if (sbNew)
                SetRect(sbGO,
                    anchorMin: new Vector2(1f, 0f),
                    anchorMax: new Vector2(1f, 1f),
                    pivot:     new Vector2(1f, 0.5f),
                    anchoredPos: Vector2.zero,
                    sizeDelta:   new Vector2(17f, 0f));

            var (sbImg, sbImgNew) = GetOrAdd<Image>(sbGO);
            if (sbImgNew) sbImg.color = ColScrollbar;
            var (sb, sbCompNew) = GetOrAdd<Scrollbar>(sbGO);
            if (sbCompNew) sb.direction = Scrollbar.Direction.BottomToTop;

            var (slidingGO, slidingNew) = GetOrCreateChild(sbGO.transform, "Sliding Area");
            if (slidingNew)
                SetRect(slidingGO,
                    anchorMin: Vector2.zero, anchorMax: Vector2.one,
                    pivot: new Vector2(0.5f, 0.5f),
                    anchoredPos: Vector2.zero,
                    sizeDelta: new Vector2(-20f, -20f));

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

            // 항상 연결 (구조 참조)
            sb.handleRect    = handleRt;
            sb.targetGraphic = handleImg;
            sr.viewport      = vp.GetComponent<RectTransform>();
            sr.content       = content.GetComponent<RectTransform>();
            sr.verticalScrollbar           = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            sr.scrollSensitivity           = 25f;
        }

        private static void SyncInputArea(Transform parent)
        {
            var (input, _) = GetOrCreateChild(parent, "InputArea");

            var (img, imgNew) = GetOrAdd<Image>(input);
            if (imgNew) img.color = ColInputBg;

            var (le, leNew) = GetOrAdd<LayoutElement>(input);
            if (leNew) le.preferredHeight = 50f;

            var (hlg, hlgNew) = GetOrAdd<HorizontalLayoutGroup>(input);
            if (hlgNew)
            {
                hlg.padding                = new RectOffset(8, 8, 6, 6);
                hlg.spacing                = 6f;
                hlg.childControlWidth      = true;
                hlg.childControlHeight     = true;
                hlg.childForceExpandWidth  = false;
                hlg.childForceExpandHeight = true;
            }

            // InputField
            var (fieldGO, _) = GetOrCreateChild(input.transform, "InputField");
            var (fieldLE, fieldLENew) = GetOrAdd<LayoutElement>(fieldGO);
            if (fieldLENew) fieldLE.flexibleWidth = 1f;

            var (fieldImg, fieldImgNew) = GetOrAdd<Image>(fieldGO);
            if (fieldImgNew) fieldImg.color = new Color(0.10f, 0.10f, 0.12f);

            var (field, fieldNew) = GetOrAdd<TMP_InputField>(fieldGO);

            // TextArea (TMP_InputField 가 요구하는 자식 구조)
            var (textArea, textAreaNew) = GetOrCreateChild(fieldGO.transform, "Text Area");
            if (textAreaNew) SetStretch(textArea, 10f, 10f, 6f, 6f);
            var (textAreaMask, _) = GetOrAdd<RectMask2D>(textArea);

            var (placeholderGO, placeholderNew) = GetOrCreateChild(textArea.transform, "Placeholder");
            if (placeholderNew) SetStretch(placeholderGO, 0f, 0f, 0f, 0f);
            var (placeholderTMP, placeholderTMPNew) = GetOrAdd<TextMeshProUGUI>(placeholderGO);
            if (placeholderTMPNew)
                ApplyTMP(placeholderTMP, "메시지를 입력하세요...", 18f, new Color(0.6f, 0.6f, 0.65f));

            var (textCompGO, textCompNew) = GetOrCreateChild(textArea.transform, "Text");
            if (textCompNew) SetStretch(textCompGO, 0f, 0f, 0f, 0f);
            var (textCompTMP, textCompTMPNew) = GetOrAdd<TextMeshProUGUI>(textCompGO);
            if (textCompTMPNew) ApplyTMP(textCompTMP, "", 18f, Color.white);

            if (fieldNew)
            {
                field.targetGraphic = fieldImg;
                field.textViewport  = textArea.GetComponent<RectTransform>();
                field.textComponent = textCompTMP;
                field.placeholder   = placeholderTMP;
                field.lineType      = TMP_InputField.LineType.SingleLine;
            }

            // SendButton
            var (sendGO, sendNew) = GetOrCreateChild(input.transform, "SendButton");
            var (sendLE, sendLENew) = GetOrAdd<LayoutElement>(sendGO);
            if (sendLENew) sendLE.preferredWidth = 80f;

            var (sendImg, sendImgNew) = GetOrAdd<Image>(sendGO);
            if (sendImgNew) sendImg.color = ColSendBtn;
            var (sendBtn, sendBtnNew) = GetOrAdd<Button>(sendGO);
            if (sendBtnNew) sendBtn.targetGraphic = sendImg;

            var (sendTxtGO, sendTxtNew) = GetOrCreateChild(sendGO.transform, "Text");
            if (sendTxtNew) SetStretch(sendTxtGO, 0f, 0f, 0f, 0f);
            var (sendTxt, sendTxtTMPNew) = GetOrAdd<TextMeshProUGUI>(sendTxtGO);
            if (sendTxtTMPNew) ApplyTMP(sendTxt, "전송", 18f, Color.white, TextAlignmentOptions.Center);
        }

        // ════════════════════════════════════════════════════════════════
        // Controller
        // ════════════════════════════════════════════════════════════════

        private static GameObject FindOrCreateController()
        {
            var existing = Object.FindFirstObjectByType<FriendChatController>();
            if (existing != null) return existing.gameObject;

            var go = new GameObject("FriendChatController");
            go.AddComponent<FriendChatController>();
            return go;
        }

        // ════════════════════════════════════════════════════════════════
        // 참조 연결
        // ════════════════════════════════════════════════════════════════

        private static void WirePanelView(GameObject panel, ChatMessageView linePrefab, FriendCharacterContext ctx)
        {
            var view = panel.GetComponent<FriendChatPanelView>();
            if (view == null) return;

            var so = new SerializedObject(view);

            so.FindProperty("scrollRect").objectReferenceValue =
                panel.transform.Find("ChatScrollView")?.GetComponent<ScrollRect>();

            so.FindProperty("contentParent").objectReferenceValue =
                panel.transform.Find("ChatScrollView/Viewport/Content");

            so.FindProperty("inputField").objectReferenceValue =
                panel.transform.Find("InputArea/InputField")?.GetComponent<TMP_InputField>();

            so.FindProperty("sendButton").objectReferenceValue =
                panel.transform.Find("InputArea/SendButton")?.GetComponent<Button>();

            so.FindProperty("closeButton").objectReferenceValue =
                panel.transform.Find("Header/CloseButton")?.GetComponent<Button>();

            if (linePrefab != null)
                so.FindProperty("chatLinePrefab").objectReferenceValue = linePrefab;

            if (ctx != null)
                so.FindProperty("characterContext").objectReferenceValue = ctx;

            so.ApplyModifiedProperties();
        }

        private static void WireController(GameObject controllerGO, GameObject panelGO)
        {
            var ctrl = controllerGO.GetComponent<FriendChatController>();
            if (ctrl == null) return;

            var so = new SerializedObject(ctrl);
            so.FindProperty("panel").objectReferenceValue = panelGO.GetComponent<FriendChatPanelView>();
            so.ApplyModifiedProperties();
        }

        // ════════════════════════════════════════════════════════════════
        // 공통 헬퍼 (ShopUIBuilder 와 동일 패턴)
        // ════════════════════════════════════════════════════════════════

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
            tmp.richText  = true;
            if (s_font != null) tmp.font = s_font;
        }

        private static void SetRect(GameObject go,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var rt              = go.GetComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.pivot            = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = sizeDelta;
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
                var go              = new GameObject("Canvas");
                canvas              = go.AddComponent<Canvas>();
                canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
                var scaler          = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight  = 0.5f;
                go.AddComponent<GraphicRaycaster>();
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            return canvas;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf   = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}

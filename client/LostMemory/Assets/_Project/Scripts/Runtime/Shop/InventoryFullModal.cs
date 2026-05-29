using System.Collections.Generic;
using LostMemory.Networking.Player;
using LostMemory.Relics;
using LostMemory.Stage;
using LostMemory.TestKhi;
using LostMemory.UI;
using MoreMountains.TopDownEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Shop
{
    /// <summary>
    /// 인벤토리가 가득 찬 상태에서 새 유물을 픽업하려 했을 때(공간 부족 reject) 처리 모달.
    /// Step 1: 픽업할 유물에 대해 [판매] / [파괴] / [골드 변환] / [교환] / [줍지 않음]
    /// Step 2: 교환 선택 시 현재 인벤토리 유물 그리드에서 빠질 슬롯 선택
    /// Step 3: Step 2 로 빠진 유물에 대해 [판매] / [파괴] / [골드 변환] / [줍지 않음]
    ///
    /// UI 는 첫 사용 시 코드로 자동 생성 (overlay Canvas 자식). 별도 prefab 불필요.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Shop/Inventory Full Modal")]
    public class InventoryFullModal : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PlayerRelicInventory inventory;
        [SerializeField] private GoldWallet goldWallet;
        [Tooltip("상점 열림 상태 감지용. null 이면 항상 [판매] 비활성.")]
        [SerializeField] private ShopController shopController;
        [Tooltip("모달을 띄울 ScreenSpace Canvas. null 이면 첫 ScreenSpaceOverlay Canvas 자동 탐색.")]
        [SerializeField] private Canvas overlayCanvas;

        [Header("Gold conversion — 등급별 골드")]
        [SerializeField] private int commonGold    = 5;
        [SerializeField] private int rareGold      = 15;
        [SerializeField] private int uniqueGold    = 35;
        [SerializeField] private int legendaryGold = 80;

        [Header("Sell price — 등급별 판매가 (상점 안에서만)")]
        [SerializeField] private int commonSell    = 10;
        [SerializeField] private int rareSell      = 30;
        [SerializeField] private int uniqueSell    = 70;
        [SerializeField] private int legendarySell = 150;

        private GameObject       _rootGO;
        private RectTransform    _panelRT;
        private TextMeshProUGUI  _titleText;
        private TextMeshProUGUI  _bodyText;
        private Image            _itemIcon;
        private Transform        _buttonsParent;
        private Transform        _gridParent;

        private RelicData _pendingRelic; // step 1/2 의 새 유물
        private RelicData _replacedRelic; // step 3 의 빠진 유물 (정보 표시용)

        /// <summary>
        /// 씬 로드 후 InventoryFullModal 이 씬에 배치되지 않았으면 자동 spawn.
        /// PlayerRelicInventory 가 있는 씬(던전/마을)에서만 의미 있고,
        /// 없는 씬에서는 OnEnable 의 자동 탐색이 실패해 조용히 disable 된다.
        ///
        /// 멀티 fix: RuntimeInitializeOnLoadMethod 는 앱 시작 시 한 번만 실행 → 타이틀 씬에서 modal 생성 후
        /// 던전 LoadScene 시 GameObject 소멸 → modal 사라짐. sceneLoaded 이벤트도 같이 구독해서 매 씬마다 확인.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureExists();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            EnsureExists();
        }

        private static void EnsureExists()
        {
            if (FindAnyObjectByType<InventoryFullModal>() != null) return;
            GameObject go = new GameObject("InventoryFullModal_Auto");
            go.AddComponent<InventoryFullModal>();
        }

        private void OnEnable()
        {
            // 멀티 fix: 씬 전환 시 modal 이 destroy 되면 재bind 못 함 → DontDestroyOnLoad 로 영속화.
            //   - 씬마다 새 PlayerRelicInventory 가 생성됨 → TryBind 의 validation 이 self-heal 로 재bind.
            //   - 솔로/Editor 단일 씬에서도 무해 (modal 은 invisible idle 상태).
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            // Phase E: NGO 환경에선 Bootstrap 시점에 Player GameObject 가 아직 spawn 되지 않아 FindAnyObjectByType 실패 →
            // modal disable. LocalPlayerReady event 구독 + 매 시도마다 TryBind 로 lazy 재시도.
            LocalPlayerResolver.LocalPlayerReady += HandleLocalPlayerReady;
            TryBind();
        }

        private void OnDisable()
        {
            LocalPlayerResolver.LocalPlayerReady -= HandleLocalPlayerReady;
            if (inventory != null) inventory.OnTryAddRejected -= HandleRejected;
            HideModal();
        }

        private void HandleLocalPlayerReady(KhiPlayerStateAggregator _) => TryBind();

        /// <summary>
        /// Phase E: PlayerRelicInventory 를 LocalPlayer 측에서 lazy resolve 후 OnTryAddRejected 구독.
        /// 1) 인스펙터 inventory 우선
        /// 2) LocalPlayerResolver 의 LocalCharacter 기반 (NGO 멀티 — 본인 player 보장)
        /// 3) FindAnyObjectByType fallback (싱글환경 / LocalPlayerResolver 미초기화)
        /// </summary>
        private void TryBind()
        {
            var localCharacter = LocalPlayerResolver.LocalCharacter;

            // 잘못된 binding 검증 — 기존 inventory 가 현재 LocalCharacter 의 child 아니면 unsubscribe + 재bind.
            // 멀티에서 Spawn race 시 호스트 inventory 가 잡혔다가 LocalPlayerReady 발화 후 게스트 inventory 로 재bind 되는 self-heal.
            if (inventory != null && localCharacter != null)
            {
                bool isLocalInventory = inventory.transform.IsChildOf(localCharacter.transform)
                                     || inventory.GetComponentInParent<Character>() == localCharacter;
                if (!isLocalInventory)
                {
                    Debug.Log($"[InventoryFullModal] Rebind — 이전 binding '{inventory.name}' 가 LocalCharacter '{localCharacter.name}' 의 child 아님. 재bind.", this);
                    inventory.OnTryAddRejected -= HandleRejected;
                    inventory = null;
                }
            }

            if (inventory != null) return; // 이미 올바른 binding

            // 1) LocalPlayer 측 — 멀티 안전. (NGO 활성 시 LocalCharacter 가 null 이면 LocalPlayerReady 대기.)
            if (localCharacter != null)
            {
                inventory = localCharacter.GetComponentInChildren<PlayerRelicInventory>(includeInactive: true);
                if (inventory == null) inventory = localCharacter.GetComponentInParent<PlayerRelicInventory>();
            }

            // 2) fallback — 싱글환경. (LocalPlayerResolver.LocalCharacter 가 NGO 활성 시 null 반환하므로 멀티에선 진입 안 됨.)
            if (inventory == null)
            {
                inventory = FindAnyObjectByType<PlayerRelicInventory>();
            }

            if (inventory == null)
            {
                // LocalPlayerReady 가 나중에 발화하면 그때 재시도. 지금은 조용히 대기.
                return;
            }

            inventory.OnTryAddRejected += HandleRejected;
            Debug.Log($"[InventoryFullModal] Bound to '{inventory.name}' (LocalCharacter='{(localCharacter != null ? localCharacter.name : "null")}').", this);
        }

        private void HandleRejected(RelicData relic, string reason)
        {
            Debug.Log($"[InventoryFullModal] HandleRejected: relic='{(relic != null ? relic.DisplayName : "null")}', reason='{reason}'", this);
            if (reason != "공간 부족") return;
            if (relic == null || relic.IsConsumable) return;
            _pendingRelic = relic;
            ShowStep1();
        }

        // ── Step 1: 새 유물 처리 ────────────────────────────────────

        private void ShowStep1()
        {
            EnsureUI();
            ClearButtons();
            _gridParent.gameObject.SetActive(false);
            _buttonsParent.gameObject.SetActive(true);

            _titleText.text = "인벤토리가 가득 찼습니다";
            _bodyText.text  = $"새 유물: <b>{_pendingRelic.DisplayName}</b>\n어떻게 할까요?";
            _itemIcon.sprite  = _pendingRelic.Icon;
            _itemIcon.enabled = _pendingRelic.Icon != null;

            bool inShop = shopController != null && shopController.IsOpen;
            AddButton("판매", () => Step1Sell(),    enabled: inShop, hint: inShop ? null : "(상점에서만 가능)");
            AddButton("파괴", () => Step1Destroy());
            AddButton($"골드 변환 (+{GoldFor(_pendingRelic)}G)", () => Step1Convert());
            AddButton("교환…", () => ShowStep2(), enabled: inventory != null && inventory.OwnedRelics.Count > 0);
            AddButton("줍지 않음", () => HideModal());

            _rootGO.SetActive(true);
        }

        private void Step1Sell()
        {
            int price = SellPriceFor(_pendingRelic);
            goldWallet?.Add(price);
            ToastNotifier.Show($"'{_pendingRelic.DisplayName}' 판매 (+{price}G)");
            HideModal();
        }

        private void Step1Destroy()
        {
            ToastNotifier.Show($"'{_pendingRelic.DisplayName}' 파괴");
            HideModal();
        }

        private void Step1Convert()
        {
            int gold = GoldFor(_pendingRelic);
            goldWallet?.Add(gold);
            ToastNotifier.Show($"'{_pendingRelic.DisplayName}' 골드로 변환 (+{gold}G)");
            HideModal();
        }

        // ── Step 2: 교환할 슬롯 선택 ────────────────────────────────

        private void ShowStep2()
        {
            EnsureUI();
            ClearButtons();
            ClearGrid();
            _buttonsParent.gameObject.SetActive(true);
            _gridParent.gameObject.SetActive(true);

            _titleText.text = "교환할 유물 선택";
            _bodyText.text  = $"<b>{_pendingRelic.DisplayName}</b> 와 교환할 유물을 선택하세요.";

            IReadOnlyList<RelicData> owned = inventory.OwnedRelics;
            foreach (RelicData r in owned)
            {
                if (r == null) continue;
                RelicData captured = r; // closure capture
                AddGridButton(captured, () => PerformSwap(captured));
            }
            AddButton("취소", () => ShowStep1());
        }

        private void PerformSwap(RelicData toRemove)
        {
            if (toRemove == null) return;
            if (!inventory.Remove(toRemove))
            {
                Debug.LogWarning($"[InventoryFullModal] Remove 실패: {toRemove.DisplayName}");
                HideModal();
                return;
            }
            if (!inventory.TryAdd(_pendingRelic))
            {
                Debug.LogError($"[InventoryFullModal] 교환 후 TryAdd 실패: {_pendingRelic.DisplayName} — 데이터 정합성 확인 필요");
                HideModal();
                return;
            }
            _replacedRelic = toRemove;
            ShowStep3();
        }

        // ── Step 3: 빠진 유물 처리 ─────────────────────────────────

        private void ShowStep3()
        {
            EnsureUI();
            ClearButtons();
            _gridParent.gameObject.SetActive(false);
            _buttonsParent.gameObject.SetActive(true);

            _titleText.text   = "빠진 유물 처리";
            _bodyText.text    = $"빠진 유물: <b>{_replacedRelic.DisplayName}</b>\n어떻게 할까요?";
            _itemIcon.sprite  = _replacedRelic.Icon;
            _itemIcon.enabled = _replacedRelic.Icon != null;

            bool inShop = shopController != null && shopController.IsOpen;
            AddButton("판매", () => Step3Sell(),    enabled: inShop, hint: inShop ? null : "(상점에서만 가능)");
            AddButton("파괴", () => Step3Destroy());
            AddButton($"골드 변환 (+{GoldFor(_replacedRelic)}G)", () => Step3Convert());
            AddButton("줍지 않음", () => HideModal());
        }

        private void Step3Sell()
        {
            int price = SellPriceFor(_replacedRelic);
            goldWallet?.Add(price);
            ToastNotifier.Show($"'{_replacedRelic.DisplayName}' 판매 (+{price}G)");
            HideModal();
        }

        private void Step3Destroy()
        {
            ToastNotifier.Show($"'{_replacedRelic.DisplayName}' 파괴");
            HideModal();
        }

        private void Step3Convert()
        {
            int gold = GoldFor(_replacedRelic);
            goldWallet?.Add(gold);
            ToastNotifier.Show($"'{_replacedRelic.DisplayName}' 골드로 변환 (+{gold}G)");
            HideModal();
        }

        // ── 등급별 가치 ────────────────────────────────────────────

        private int GoldFor(RelicData relic) => relic.Rarity switch
        {
            RelicRarity.Common    => commonGold,
            RelicRarity.Rare      => rareGold,
            RelicRarity.Unique    => uniqueGold,
            RelicRarity.Legendary => legendaryGold,
            _                     => commonGold,
        };

        private int SellPriceFor(RelicData relic) => relic.Rarity switch
        {
            RelicRarity.Common    => commonSell,
            RelicRarity.Rare      => rareSell,
            RelicRarity.Unique    => uniqueSell,
            RelicRarity.Legendary => legendarySell,
            _                     => commonSell,
        };

        // ── UI 생성 / 정리 ─────────────────────────────────────────

        private void HideModal()
        {
            if (_rootGO != null) _rootGO.SetActive(false);
            _pendingRelic = null;
            _replacedRelic = null;
        }

        private void EnsureUI()
        {
            if (_rootGO != null) return;

            if (overlayCanvas == null)
            {
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (Canvas c in canvases)
                {
                    if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        overlayCanvas = c;
                        break;
                    }
                }
                if (overlayCanvas == null && canvases.Length > 0)
                    overlayCanvas = canvases[0];
            }
            if (overlayCanvas == null)
            {
                Debug.LogError("[InventoryFullModal] Canvas 를 찾지 못해 모달을 띄울 수 없습니다.");
                return;
            }

            _rootGO = new GameObject("InventoryFullModal_Runtime",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _rootGO.transform.SetParent(overlayCanvas.transform, false);
            _rootGO.transform.SetAsLastSibling();
            var rootRT = (RectTransform)_rootGO.transform;
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;
            var overlay = _rootGO.GetComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.65f);
            overlay.raycastTarget = true;

            // 중앙 패널
            GameObject panelGO = new GameObject("Panel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGO.transform.SetParent(_rootGO.transform, false);
            _panelRT = (RectTransform)panelGO.transform;
            _panelRT.anchorMin = _panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRT.pivot     = new Vector2(0.5f, 0.5f);
            _panelRT.sizeDelta = new Vector2(480f, 320f);
            panelGO.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f, 0.97f);

            var vlg = panelGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.spacing = 8f;
            vlg.childAlignment    = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            var csf = panelGO.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            _titleText = AddText(panelGO.transform, "Title", 22, FontStyles.Bold);
            _itemIcon  = AddIcon(panelGO.transform, "Icon", 64f);
            _bodyText  = AddText(panelGO.transform, "Body", 16, FontStyles.Normal);

            // 버튼 컨테이너
            GameObject btns = new GameObject("Buttons",
                typeof(RectTransform), typeof(VerticalLayoutGroup));
            btns.transform.SetParent(panelGO.transform, false);
            var btnVlg = btns.GetComponent<VerticalLayoutGroup>();
            btnVlg.spacing = 6f;
            btnVlg.childForceExpandWidth  = true;
            btnVlg.childForceExpandHeight = false;
            _buttonsParent = btns.transform;

            // 그리드 (Step2 용)
            GameObject grid = new GameObject("Grid",
                typeof(RectTransform), typeof(GridLayoutGroup));
            grid.transform.SetParent(panelGO.transform, false);
            var glg = grid.GetComponent<GridLayoutGroup>();
            glg.cellSize    = new Vector2(72f, 72f);
            glg.spacing     = new Vector2(6f, 6f);
            glg.constraint  = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 5;
            grid.SetActive(false);
            _gridParent = grid.transform;
        }

        private TextMeshProUGUI AddText(Transform parent, string n, int size, FontStyles style)
        {
            GameObject go = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize   = size;
            t.alignment  = TextAlignmentOptions.Center;
            t.fontStyle  = style;
            t.color      = Color.white;
            t.enableWordWrapping = true;

            // 한글 폰트: 씬의 다른 TMP_Text 에서 복사. (기본 LiberationSans 에는 한글 글리프 없음 → □□□ 표시)
            TMP_FontAsset borrowed = BorrowSceneFont();
            if (borrowed != null) t.font = borrowed;
            return t;
        }

        private static TMP_FontAsset BorrowSceneFont()
        {
            TMP_Text[] existing = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
            foreach (TMP_Text txt in existing)
            {
                if (txt != null && txt.font != null && txt.font.name != "LiberationSans SDF")
                    return txt.font;
            }
            return null;
        }

        private Image AddIcon(Transform parent, string n, float size)
        {
            GameObject go = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = size;
            le.preferredWidth  = size;
            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            return img;
        }

        private void ClearButtons()
        {
            if (_buttonsParent == null) return;
            for (int i = _buttonsParent.childCount - 1; i >= 0; i--)
                Destroy(_buttonsParent.GetChild(i).gameObject);
        }

        private void ClearGrid()
        {
            if (_gridParent == null) return;
            for (int i = _gridParent.childCount - 1; i >= 0; i--)
                Destroy(_gridParent.GetChild(i).gameObject);
        }

        private void AddButton(string label, System.Action onClick, bool enabled = true, string hint = null)
        {
            GameObject go = new GameObject(label,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_buttonsParent, false);
            var img = go.GetComponent<Image>();
            img.color = enabled ? new Color(0.22f, 0.22f, 0.30f) : new Color(0.16f, 0.16f, 0.18f);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 36f;
            var btn = go.GetComponent<Button>();
            btn.interactable = enabled;
            if (enabled) btn.onClick.AddListener(() => onClick?.Invoke());

            var txt = AddText(go.transform, "Label", 15, FontStyles.Normal);
            txt.text  = hint == null ? label : $"{label} <size=12><alpha=#80>{hint}</size>";
            txt.color = enabled ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            var txtRT = txt.rectTransform;
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;
        }

        private void AddGridButton(RelicData relic, System.Action onClick)
        {
            GameObject go = new GameObject(relic.name,
                typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_gridParent, false);
            var img = go.GetComponent<Image>();
            img.color = RelicRarityColors.Slot(relic);

            // 아이콘 자식
            GameObject icoGO = new GameObject("Icon",
                typeof(RectTransform), typeof(Image));
            icoGO.transform.SetParent(go.transform, false);
            var icoRT = (RectTransform)icoGO.transform;
            icoRT.anchorMin = new Vector2(0.1f, 0.1f);
            icoRT.anchorMax = new Vector2(0.9f, 0.9f);
            icoRT.offsetMin = Vector2.zero;
            icoRT.offsetMax = Vector2.zero;
            var ico = icoGO.GetComponent<Image>();
            ico.sprite = relic.Icon;
            ico.preserveAspect = true;
            ico.raycastTarget = false;

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
        }
    }
}

using LostMemory.Relics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostMemory.Editor.InventoryTest
{
    /// <summary>
    /// Epic U 인벤토리 테스트 도구. Play 모드 전제 별도 EditorWindow.
    /// CL-174: 셸 + Play 모드 가드 + Player 자동 검색.
    /// CL-175~177: 트리/슬롯/동작 추가 예정.
    /// </summary>
    public class InventoryTestWindow : EditorWindow
    {
        private const string UxmlPath = "InventoryTestWindow";
        private const string UssPath  = "InventoryTestWindow";

        // CL-175 이후 사용 — 셸 단계에서 hook 만 마련
        private PlayerRelicInventory _relicInv;
        private PlayerConsumableInventory _consumeInv;

        private VisualElement _bodyContainer;
        private Label _modeHint;

        [MenuItem("LostMemory/Inventory Test Window")]
        public static void Open()
        {
            var window = GetWindow<InventoryTestWindow>();
            window.titleContent = new GUIContent("Inventory Test");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            var uxml = Resources.Load<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[CL-174] UXML 미발견: Resources/{UxmlPath}");
                return;
            }
            uxml.CloneTree(root);

            var uss = Resources.Load<StyleSheet>(UssPath);
            if (uss != null) root.styleSheets.Add(uss);

            _bodyContainer = root.Q<VisualElement>("BodyContainer");
            _modeHint      = root.Q<Label>("ModeHint");

            UpdateModeView();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode ||
                change == PlayModeStateChange.EnteredEditMode)
            {
                UpdateModeView();
            }
        }

        private void UpdateModeView()
        {
            bool playMode = EditorApplication.isPlaying;
            if (_modeHint != null)
                _modeHint.style.display = playMode ? DisplayStyle.None : DisplayStyle.Flex;
            if (_bodyContainer != null)
                _bodyContainer.SetEnabled(playMode);

            if (playMode) RefreshPlayerInstances();
        }

        private void RefreshPlayerInstances()
        {
            // 단일 Player 가정 (MVP). 멀티 디버깅 필요 시 후속 ticket 에서 드롭다운 추가.
            _relicInv   = Object.FindAnyObjectByType<PlayerRelicInventory>();
            _consumeInv = Object.FindAnyObjectByType<PlayerConsumableInventory>();
            // CL-175 이후: _relicInv / _consumeInv 사용해서 슬롯 표시
        }
    }
}

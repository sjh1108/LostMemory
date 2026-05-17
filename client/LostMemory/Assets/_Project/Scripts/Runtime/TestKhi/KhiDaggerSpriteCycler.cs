#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
#endif

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-230 디버그용. F7 키로 단검 sprite 를 cycle 하면서 어떤 모양이 어울리는지 빠른 테스트.
    ///
    /// 사용법:
    /// 1. 씬에 본 컴포넌트 부착 (보통 _WeaponUpgrade GameObject 옆에)
    /// 2. mainPresenter / secondaryPresenter 에 Weapon_Main / Weapon_Sub 의 KhiWeaponPresenter 드래그
    /// 3. Inspector 우클릭 → "Auto Fill Sprites From Daggers Folder" 한 번 클릭하면 daggerSprites 자동 채워짐
    ///    (또는 daggerSprites 배열에 직접 sprite 드래그)
    /// 4. Play → F7 눌러서 sprite cycle. 마지막 인덱스 다음엔 procedural sword 로 복귀.
    ///
    /// 릴리스 빌드에서는 클래스 전체 컴파일 제외.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Dagger Sprite Cycler")]
    [DefaultExecutionOrder(320)]
    public class KhiDaggerSpriteCycler : MonoBehaviour
    {
        [Header("Keys")]
        [SerializeField] private Key cycleKey = Key.F7;
        [Tooltip("Shift+F7 = 이전 sprite 로 cycle.")]
        [SerializeField] private bool allowReverseWithShift = true;

        [Header("Targets")]
        [Tooltip("메인 손 KhiWeaponPresenter. 비워두면 자동 탐색.")]
        [SerializeField] private KhiWeaponPresenter mainPresenter;
        [Tooltip("보조 손 KhiWeaponPresenter (양손 단검 모드용). 없으면 비워두기.")]
        [SerializeField] private KhiWeaponPresenter secondaryPresenter;

        [Header("Sprite Catalog")]
        [Tooltip("cycle 할 단검 sprite 들. Inspector 우클릭 → Auto Fill 로 자동 채우기 가능.")]
        [SerializeField] private Sprite[] daggerSprites = new Sprite[0];

        [Header("Visual Tuning (모든 sprite 공통)")]
        [Tooltip("sprite 표시 크기 배율. 16x16 sprite 가 너무 작으면 1.5~3 으로 키우기.")]
        [SerializeField, Min(0.1f)] private float spriteScale = 2f;
        [Tooltip("ON: cycle 시 두 presenter 의 SpriteRenderer.transform.localScale 도 spriteScale 로 갱신.")]
        [SerializeField] private bool applyScaleOnCycle = true;

        [Header("UI Hint")]
        [SerializeField] private bool showOnGuiHint = true;
        [SerializeField] private Vector2 hintOrigin = new Vector2(10f, 285f);
        [SerializeField] private int fontSize = 12;

        private int _currentIndex = -1; // -1 = procedural sword (cycle 시작 상태)
        private GUIStyle _labelStyle;
        private bool _styleReady;

        private const string DaggersFolderPath = "Assets/_Project/Art/16x16 RPG Item Icons (upd2)/Daggers";

        private void Awake()
        {
            ResolvePresenters();
        }

        private void ResolvePresenters()
        {
            if (mainPresenter == null || secondaryPresenter == null)
            {
                KhiWeaponPresenter[] all = FindObjectsByType<KhiWeaponPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var p in all)
                {
                    if (mainPresenter == null && !p.name.ToLowerInvariant().Contains("sub"))
                    {
                        mainPresenter = p;
                    }
                    else if (secondaryPresenter == null && p.name.ToLowerInvariant().Contains("sub"))
                    {
                        secondaryPresenter = p;
                    }
                }
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb[cycleKey].wasPressedThisFrame)
            {
                bool reverse = allowReverseWithShift && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                Cycle(reverse ? -1 : +1);
            }
        }

        private void Cycle(int step)
        {
            if (daggerSprites == null || daggerSprites.Length == 0)
            {
                Debug.LogWarning("[KhiDaggerSpriteCycler] daggerSprites 비어있음. Inspector 우클릭 → Auto Fill Sprites From Daggers Folder.", this);
                return;
            }

            // -1 (procedural) → 0 → 1 → ... → N-1 → -1 (procedural) → 0 → ...
            // total states = daggerSprites.Length + 1 (procedural 포함)
            int total = daggerSprites.Length + 1;
            // 현재 상태를 0~total-1 범위로 매핑 (0 = procedural, 1~N = sprite[0~N-1])
            int currentState = _currentIndex + 1;
            int nextState = ((currentState + step) % total + total) % total;
            _currentIndex = nextState - 1;

            Sprite next = _currentIndex >= 0 ? daggerSprites[_currentIndex] : null;
            ApplyToPresenters(next);

            string label = next != null ? $"{_currentIndex + 1}/{daggerSprites.Length}: {next.name}" : "Procedural";
            Debug.Log($"[KhiDaggerSpriteCycler] {label}");
        }

        private void ApplyToPresenters(Sprite next)
        {
            if (mainPresenter == null && secondaryPresenter == null)
            {
                ResolvePresenters();
            }

            if (mainPresenter != null)
            {
                mainPresenter.SetExternalSprite(next);
                if (applyScaleOnCycle) ApplyScale(mainPresenter);
            }
            if (secondaryPresenter != null)
            {
                secondaryPresenter.SetExternalSprite(next);
                if (applyScaleOnCycle) ApplyScale(secondaryPresenter);
            }
        }

        private void ApplyScale(KhiWeaponPresenter presenter)
        {
            SpriteRenderer sr = presenter.GetComponent<SpriteRenderer>();
            if (sr == null) sr = presenter.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null)
            {
                Transform t = sr.transform;
                Vector3 currentScale = t.localScale;
                float sign = Mathf.Sign(currentScale.x == 0 ? 1 : currentScale.x);
                t.localScale = new Vector3(spriteScale * sign, spriteScale, currentScale.z == 0 ? 1 : currentScale.z);
            }
        }

        private void OnGUI()
        {
            if (!showOnGuiHint) return;

            if (!_styleReady)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    normal = { textColor = Color.white }
                };
                _styleReady = true;
            }

            string current = _currentIndex >= 0 && daggerSprites != null && _currentIndex < daggerSprites.Length
                ? $"{_currentIndex + 1}/{daggerSprites.Length}: {daggerSprites[_currentIndex]?.name}"
                : "Procedural";
            string text = $"[Dagger Sprite] {cycleKey}: next (Shift={cycleKey}: prev)   current: {current}";
            GUI.Label(new Rect(hintOrigin.x + 1f, hintOrigin.y + 1f, 600f, 22f), text,
                new GUIStyle(_labelStyle) { normal = { textColor = Color.black } });
            GUI.Label(new Rect(hintOrigin.x, hintOrigin.y, 600f, 22f), text, _labelStyle);
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Fill Sprites From Daggers Folder")]
        private void AutoFillSpritesFromDaggersFolder()
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { DaggersFolderPath });
            if (guids == null || guids.Length == 0)
            {
                Debug.LogWarning($"[KhiDaggerSpriteCycler] {DaggersFolderPath} 에서 Sprite 못 찾음. 폴더 경로 확인하세요.", this);
                return;
            }

            List<Sprite> sprites = new List<Sprite>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // 한 .png 안에 여러 sliced sprite 가 있을 수 있으므로 LoadAllAssetsAtPath 사용.
                Object[] objs = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var obj in objs)
                {
                    if (obj is Sprite s)
                    {
                        sprites.Add(s);
                    }
                }
            }

            // 이름순 정렬 (Dagger_1, Dagger_2, ... 자연 순서).
            sprites.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.name, b.name));

            daggerSprites = sprites.ToArray();
            EditorUtility.SetDirty(this);
            Debug.Log($"[KhiDaggerSpriteCycler] Auto-filled {daggerSprites.Length} sprites from {DaggersFolderPath}.", this);
        }
#endif
    }
}
#endif

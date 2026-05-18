using System;
using System.Collections;
using LostMemory.Enemies.Boss.Bertha;
using LostMemory.Stage;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LostMemory.UI
{
    public enum BossHealthBarDisplayMode
    {
        Manual,
        OnEnable,
        OnIntroStarted,
        OnIntroCompleted
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Boss Health Bar View")]
    public sealed class BossHealthBarView : MonoBehaviour, MMEventListener<HealthChangeEvent>
    {
        [Header("Target")]
        [SerializeField] private Health targetHealth;
        [SerializeField] private BossIntroSequenceController introSequenceController;
        [SerializeField] private bool autoResolveBerthaBoss = true;

        [Header("Display")]
        [SerializeField] private BossHealthBarDisplayMode displayMode = BossHealthBarDisplayMode.OnIntroStarted;
        [SerializeField] private bool hideOnAwake = true;
        [SerializeField] private bool showWhenDamaged = true;
        [SerializeField] private bool hideOnDeath = true;
        [SerializeField, Min(0f)] private float deathHideDelay = 0.6f;

        [Header("View")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Image hpFillImage;
        [SerializeField] private Image hpFrameImage;

        [Header("Sprites")]
        [SerializeField] private Sprite portraitSprite;
        [SerializeField] private Sprite hpFrameSprite;
        [SerializeField] private Sprite[] hpFillSprites = Array.Empty<Sprite>();

#if UNITY_EDITOR
        [Header("Editor Asset Paths")]
        [SerializeField] private string portraitAssetPath =
            "Assets/_Project/Art/Enemies/Boss/1_Bertha/Portraits/BerthaPortrait.png";
        [SerializeField] private string hpBarFolderPath =
            "Assets/_Project/Art/Enemies/Boss/HpBar";
#endif

        private Health _subscribedHealth;
        private BossIntroSequenceController _subscribedIntroSequenceController;
        private Coroutine _hideRoutine;
        private bool _isVisible;
        private bool _isListeningToHealthEvents;

        private void Reset()
        {
            ResolveViewReferences(createCanvasGroupIfMissing: false);
            ResolveBossReferences();
        }

        private void Awake()
        {
            ResolveViewReferences(createCanvasGroupIfMissing: true);
            ResolveBossReferences();
            ApplyStaticSprites();
            Refresh();

            if (displayMode == BossHealthBarDisplayMode.OnEnable)
            {
                Show();
            }
            else
            {
                SetVisible(!hideOnAwake);
            }
        }

        private void OnEnable()
        {
            ResolveBossReferences();
            Subscribe();

            if (displayMode == BossHealthBarDisplayMode.OnEnable)
            {
                Show();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();

            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }
        }

        public void Show()
        {
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            ResolveBossReferences();
            Refresh();
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void SetTarget(Health health, BossIntroSequenceController introController = null)
        {
            UnsubscribeTarget();
            UnsubscribeIntro();

            targetHealth = health;
            introSequenceController = introController;

            if (targetHealth != null && introSequenceController == null)
            {
                introSequenceController = targetHealth.GetComponent<BossIntroSequenceController>();
            }

            SubscribeTarget();
            SubscribeIntro();
            Refresh();
        }

        public void Refresh()
        {
            ApplyStaticSprites();

            if (hpFillImage == null)
            {
                return;
            }

            Sprite sprite = GetSpriteForHealth();
            if (sprite != null)
            {
                hpFillImage.sprite = sprite;
            }
        }

        public void OnMMEvent(HealthChangeEvent healthChangeEvent)
        {
            ResolveBossReferences();

            if (healthChangeEvent.AffectedHealth != targetHealth)
            {
                return;
            }

            Refresh();

            if (!_isVisible && showWhenDamaged && IsDamaged())
            {
                Show();
            }
        }

        private void Subscribe()
        {
            if (!_isListeningToHealthEvents)
            {
                this.MMEventStartListening<HealthChangeEvent>();
                _isListeningToHealthEvents = true;
            }

            SubscribeTarget();
            SubscribeIntro();
        }

        private void Unsubscribe()
        {
            if (_isListeningToHealthEvents)
            {
                this.MMEventStopListening<HealthChangeEvent>();
                _isListeningToHealthEvents = false;
            }

            UnsubscribeTarget();
            UnsubscribeIntro();
        }

        private void SubscribeTarget()
        {
            if (_subscribedHealth == targetHealth)
            {
                return;
            }

            UnsubscribeTarget();

            if (targetHealth == null)
            {
                return;
            }

            targetHealth.OnDeath += HandleDeath;
            targetHealth.OnRevive += HandleRevive;
            _subscribedHealth = targetHealth;
        }

        private void UnsubscribeTarget()
        {
            if (_subscribedHealth == null)
            {
                return;
            }

            _subscribedHealth.OnDeath -= HandleDeath;
            _subscribedHealth.OnRevive -= HandleRevive;
            _subscribedHealth = null;
        }

        private void SubscribeIntro()
        {
            if (_subscribedIntroSequenceController == introSequenceController)
            {
                return;
            }

            UnsubscribeIntro();

            if (introSequenceController == null)
            {
                return;
            }

            introSequenceController.IntroStarted += HandleIntroStarted;
            introSequenceController.IntroCompleted += HandleIntroCompleted;
            _subscribedIntroSequenceController = introSequenceController;
        }

        private void UnsubscribeIntro()
        {
            if (_subscribedIntroSequenceController == null)
            {
                return;
            }

            _subscribedIntroSequenceController.IntroStarted -= HandleIntroStarted;
            _subscribedIntroSequenceController.IntroCompleted -= HandleIntroCompleted;
            _subscribedIntroSequenceController = null;
        }

        private void HandleIntroStarted(BossRoomTransitionCompletedContext context)
        {
            if (displayMode == BossHealthBarDisplayMode.OnIntroStarted)
            {
                Show();
            }
        }

        private void HandleIntroCompleted(BossRoomTransitionCompletedContext context)
        {
            if (displayMode == BossHealthBarDisplayMode.OnIntroCompleted)
            {
                Show();
            }
        }

        private void HandleDeath()
        {
            Refresh();

            if (!hideOnDeath)
            {
                return;
            }

            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
            }

            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private void HandleRevive()
        {
            Refresh();

            if (displayMode == BossHealthBarDisplayMode.OnEnable)
            {
                Show();
            }
        }

        private IEnumerator HideAfterDelay()
        {
            if (deathHideDelay > 0f)
            {
                yield return new WaitForSeconds(deathHideDelay);
            }

            _hideRoutine = null;
            Hide();
        }

        private void ResolveViewReferences(bool createCanvasGroupIfMissing)
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null && createCanvasGroupIfMissing)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            portraitImage ??= FindImage("PortraitImage");
            hpFillImage ??= FindImage("HpFillImage");
            hpFrameImage ??= FindImage("HpFrameImage");
        }

        private Image FindImage(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private void ResolveBossReferences()
        {
            if (!autoResolveBerthaBoss)
            {
                return;
            }

            if (IsActiveSceneObject(targetHealth))
            {
                if (!IsActiveSceneObject(introSequenceController))
                {
                    introSequenceController = targetHealth.GetComponent<BossIntroSequenceController>();
                }

                SubscribeTarget();
                SubscribeIntro();
                return;
            }

            if (targetHealth != null)
            {
                UnsubscribeTarget();
                UnsubscribeIntro();
                targetHealth = null;
                introSequenceController = null;
            }

            if (TryAssignBossController(GetComponentInParent<BerthaBossEncounterController>(includeInactive: true)))
            {
                return;
            }

            BerthaBossEncounterController[] controllers =
                Resources.FindObjectsOfTypeAll<BerthaBossEncounterController>();

            for (int i = 0; i < controllers.Length; i++)
            {
                BerthaBossEncounterController controller = controllers[i];
                if (controller != null &&
                    controller.gameObject.scene == gameObject.scene &&
                    TryAssignBossController(controller))
                {
                    return;
                }
            }

            for (int i = 0; i < controllers.Length; i++)
            {
                if (TryAssignBossController(controllers[i]))
                {
                    return;
                }
            }
        }

        private bool TryAssignBossController(BerthaBossEncounterController controller)
        {
            if (!IsActiveSceneObject(controller))
            {
                return false;
            }

            Health health = controller.GetComponent<Health>();
            if (!IsActiveSceneObject(health))
            {
                return false;
            }

            targetHealth = health;
            introSequenceController = controller.GetComponent<BossIntroSequenceController>();
            SubscribeTarget();
            SubscribeIntro();
            return true;
        }

        private static bool IsActiveSceneObject(Component component)
        {
            return component != null &&
                   component.gameObject.scene.IsValid() &&
                   component.gameObject.activeInHierarchy;
        }

        private void ApplyStaticSprites()
        {
            if (portraitImage != null && portraitSprite != null)
            {
                portraitImage.sprite = portraitSprite;
            }

            if (hpFrameImage != null && hpFrameSprite != null)
            {
                hpFrameImage.sprite = hpFrameSprite;
            }
        }

        private Sprite GetSpriteForHealth()
        {
            if (hpFillSprites == null || hpFillSprites.Length == 0)
            {
                return null;
            }

            float normalizedHealth = GetNormalizedHealth();
            int index = Mathf.RoundToInt(normalizedHealth * (hpFillSprites.Length - 1));
            index = Mathf.Clamp(index, 0, hpFillSprites.Length - 1);
            return hpFillSprites[index];
        }

        private float GetNormalizedHealth()
        {
            if (targetHealth == null)
            {
                return 1f;
            }

            float maxHealth = Mathf.Max(targetHealth.MaximumHealth, targetHealth.InitialHealth, 0.0001f);
            return Mathf.Clamp01(targetHealth.CurrentHealth / maxHealth);
        }

        private bool IsDamaged()
        {
            if (targetHealth == null)
            {
                return false;
            }

            float maxHealth = Mathf.Max(targetHealth.MaximumHealth, targetHealth.InitialHealth, 0.0001f);
            return targetHealth.CurrentHealth < maxHealth;
        }

        private void SetVisible(bool isVisible)
        {
            _isVisible = isVisible;

            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveViewReferences(createCanvasGroupIfMissing: false);
            AutoAssignDefaultSprites();
            ApplyStaticSprites();
            Refresh();
        }

        private void AutoAssignDefaultSprites()
        {
            bool changed = false;

            if (portraitSprite == null)
            {
                portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(portraitAssetPath);
                changed = portraitSprite != null;
            }

            if (hpFrameSprite == null)
            {
                hpFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{hpBarFolderPath}/Frame.png");
                changed = changed || hpFrameSprite != null;
            }

            if (hpFillSprites == null || hpFillSprites.Length == 0 || ContainsMissingSprite(hpFillSprites))
            {
                hpFillSprites = LoadHpSprites();
                changed = changed || hpFillSprites.Length > 0;
            }

            if (changed)
            {
                EditorUtility.SetDirty(this);
            }
        }

        private Sprite[] LoadHpSprites()
        {
            Sprite[] loadedSprites = new Sprite[51];
            for (int i = 0; i < loadedSprites.Length; i++)
            {
                int percent = i * 2;
                loadedSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{hpBarFolderPath}/{percent}.png");
            }

            return loadedSprites;
        }

        private static bool ContainsMissingSprite(Sprite[] sprites)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                {
                    return true;
                }
            }

            return false;
        }
#endif
    }
}

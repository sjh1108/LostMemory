using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Dialogue
{
    /// <summary>
    /// 대화 패널 UI 표현 담당. DialogueController 가 컨트롤한다.
    ///
    /// 구조 (DialoguePanel.prefab 자식 GameObject 들을 inspector 슬롯에 wiring):
    ///   - Root (이 컴포넌트가 붙는 GameObject — 켜고 끄기로 패널 등장/숨김)
    ///   - Background (Image)
    ///   - LeftPortrait (Image) — 단일 portrait. 현재 화자의 sprite 가 표시됨.
    ///   - NameBox (Image) + NameText (TMP_Text)
    ///   - TextBox (Image) + DialogueText (TMP_Text)
    ///   - ContinueIndicator (Image)
    ///
    /// API:
    ///   - ApplySkin(skin): sprite/color/속도 일괄 적용
    ///   - ShowLine(line, portraitSprite): portrait/이름/텍스트 갱신 + 타이프라이터 시작
    ///   - SkipTypewriter(): 진행 중인 타이프라이터 즉시 완성
    ///   - HidePanel(): root 비활성
    ///   - IsTyping: 타이프라이터 진행 중인지
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Dialogue/Dialogue Panel View")]
    public sealed class DialoguePanelView : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("패널 전체 GameObject. SetActive(true/false) 로 등장/숨김.")]
        [SerializeField] private GameObject panelRoot;

        [Header("Background / Boxes")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image namePlateImage;
        [SerializeField] private Image textBoxImage;
        [SerializeField] private Image continueIndicatorImage;

        [Header("Portraits")]
        [Tooltip("단일 portrait — 현재 화자의 sprite 가 표시됨 (isPlayer 무관)")]
        [SerializeField] private Image leftPortraitImage;

        [Header("Text")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text dialogueText;

        // === Runtime state ===
        private DialogueSkin _skin;
        private Coroutine _typewriterRoutine;
        private string _pendingFullText;
        private bool _isTyping;

        public bool IsTyping => _isTyping;
        public bool IsPanelVisible => panelRoot != null && panelRoot.activeSelf;

        public event Action TypewriterCompleted;

        private void Awake()
        {
            HidePanel();
        }

        public void ApplySkin(DialogueSkin skin)
        {
            _skin = skin;
            if (skin == null) return;

            // sprite 만 Skin 에서 override. 색상은 prefab Inspector 의 값 그대로 사용
            // (edit 모드에서 본 색이 Play 모드에서도 동일하게 보이도록 — 사용자 결정사항)
            if (backgroundImage != null && skin.backgroundSprite != null)
            {
                backgroundImage.sprite = skin.backgroundSprite;
            }
            if (namePlateImage != null && skin.namePlateSprite != null)
            {
                namePlateImage.sprite = skin.namePlateSprite;
            }
            if (textBoxImage != null && skin.textBoxSprite != null)
            {
                textBoxImage.sprite = skin.textBoxSprite;
            }
            if (continueIndicatorImage != null && skin.continueIndicatorSprite != null)
            {
                continueIndicatorImage.sprite = skin.continueIndicatorSprite;
            }
        }

        public void ShowPanel()
        {
            if (panelRoot != null && !panelRoot.activeSelf) panelRoot.SetActive(true);
        }

        public void HidePanel()
        {
            StopAllInternalRoutines();
            if (continueIndicatorImage != null) continueIndicatorImage.enabled = false;
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
        }

        /// <summary>
        /// 한 라인 표시 시작. portrait/이름 즉시 갱신, text 는 타이프라이터로 채워짐.
        /// 호출자(DialogueController)가 portraitSprite 를 PortraitCatalog 에서 미리 조회해 넘긴다.
        /// </summary>
        public void ShowLine(DialogueLine line, Sprite portraitSprite)
        {
            if (line == null) return;

            ShowPanel();
            ApplyPortraits(line.IsPlayer, portraitSprite);

            if (nameText != null) nameText.text = line.Speaker ?? string.Empty;

            StopTypewriter();
            HideContinueIndicator();

            _pendingFullText = line.Text ?? string.Empty;
            float cps = _skin != null ? _skin.charactersPerSecond : 30f;

            if (cps <= 0f)
            {
                if (dialogueText != null) dialogueText.text = _pendingFullText;
                OnTypewriterFinished();
                return;
            }

            _typewriterRoutine = StartCoroutine(RunTypewriter(_pendingFullText, cps));
        }

        /// <summary>
        /// 타이프라이터 진행 중 호출되면 즉시 완성. 아니면 무시.
        /// </summary>
        public void SkipTypewriter()
        {
            if (!_isTyping) return;
            StopTypewriter();
            if (dialogueText != null) dialogueText.text = _pendingFullText ?? string.Empty;
            OnTypewriterFinished();
        }

        private void ApplyPortraits(bool speakerIsPlayer, Sprite portraitSprite)
        {
            // 단일 portrait — isPlayer 와 무관하게 현재 화자의 sprite 를 항상 표시.
            // (이전: 좌=플레이어 / 우=상대방 양쪽 노출. 현재는 LeftPortrait 단일.)
            float speakerAlpha = _skin != null ? _skin.speakerPortraitAlpha : 1f;
            SetPortrait(leftPortraitImage, portraitSprite, speakerAlpha, replaceSprite: true);
        }

        private static void SetPortrait(Image image, Sprite sprite, float alpha, bool replaceSprite)
        {
            if (image == null) return;
            if (replaceSprite)
            {
                image.sprite = sprite;
                image.enabled = sprite != null;
            }
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }

        private IEnumerator RunTypewriter(string fullText, float cps)
        {
            _isTyping = true;
            if (dialogueText != null) dialogueText.text = string.Empty;

            float secondsPerChar = 1f / Mathf.Max(0.01f, cps);
            int shown = 0;
            int total = fullText.Length;
            float timer = 0f;

            while (shown < total)
            {
                timer += Time.unscaledDeltaTime;
                while (timer >= secondsPerChar && shown < total)
                {
                    timer -= secondsPerChar;
                    shown++;
                }

                if (dialogueText != null) dialogueText.text = fullText.Substring(0, shown);
                yield return null;
            }

            OnTypewriterFinished();
        }

        private void OnTypewriterFinished()
        {
            _isTyping = false;
            _typewriterRoutine = null;
            ShowContinueIndicator();
            TypewriterCompleted?.Invoke();
        }

        private void ShowContinueIndicator()
        {
            if (continueIndicatorImage != null) continueIndicatorImage.enabled = true;
        }

        private void HideContinueIndicator()
        {
            if (continueIndicatorImage != null) continueIndicatorImage.enabled = false;
        }

        private void StopTypewriter()
        {
            if (_typewriterRoutine != null)
            {
                StopCoroutine(_typewriterRoutine);
                _typewriterRoutine = null;
            }
            _isTyping = false;
        }

        private void StopAllInternalRoutines()
        {
            StopTypewriter();
        }

        private void OnDisable()
        {
            StopAllInternalRoutines();
        }
    }
}

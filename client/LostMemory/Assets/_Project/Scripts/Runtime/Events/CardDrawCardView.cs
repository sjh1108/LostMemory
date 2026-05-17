using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-227 V2 — 카드 뽑기 슬롯 1장.
    /// 구매 전: 카드 뒷면 sprite (Card A back) + 입장료 표시.
    /// 픽 후: 카드 뒤집기 애니메이션 (4프레임 back closing + 4프레임 front opening) → outcome 표시.
    ///
    /// sprite flip frames 필요:
    ///   - backFrames[0~3]: Flip01~04 (01 = 가장 좁음, 04 = 정면).
    ///     reveal 시 04 → 01 순서로 swap (back 닫힘).
    ///   - frontFrames[0~3]: Flip01~04 (Blank card 종류). 01 → 04 순서로 swap (front 펼침).
    /// sprite ref 없으면 즉시 swap fallback.
    /// </summary>
    public sealed class CardDrawCardView : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject backRoot;
        [SerializeField] private GameObject frontRoot;

        [Header("Sprite Images (flip 대상)")]
        [Tooltip("BackRoot 의 Image 컴포넌트 — flip 시 backFrames 로 sprite swap.")]
        [SerializeField] private Image backImage;
        [Tooltip("FrontRoot 의 Image 컴포넌트 — flip 시 frontFrames 로 sprite swap.")]
        [SerializeField] private Image frontImage;

        [Header("Flip Sprite Frames (각 4장)")]
        [Tooltip("뒷면 flip frames (Card A). [0]=Flip01 (정면, 완전 펴짐), [3]=Flip04 (좁음, 측면). 기본 idle = [0].")]
        [SerializeField] private Sprite[] backFrames;
        [Tooltip("앞면 flip frames (Blank Yellow Card). 주의: back 과 반대 방향. [0]=Flip01 (좁음), [3]=Flip04 (정면, 완전 펴짐). 기본 = [3].")]
        [SerializeField] private Sprite[] frontFrames;

        [Header("Back (픽 전 오버레이)")]
        [Tooltip("입장료 텍스트. 옵션 — 없으면 무시.")]
        [SerializeField] private TextMeshProUGUI backEntryCostText;
        [SerializeField] private Button pickButton;

        [Header("Front (reveal 후 오버레이)")]
        [SerializeField] private TextMeshProUGUI frontLabelText;
        [SerializeField] private TextMeshProUGUI frontReturnText;

        [Header("Flip Animation")]
        [SerializeField, Min(0.01f)] private float flipFrameDuration = 0.05f;
        [SerializeField, Tooltip("픽 후 추가 대기 — 결과 텍스트를 좀 더 보여주려면.")]
        private float postFlipHold = 0.0f;
        [SerializeField, Tooltip("패널 열림 시 entrance flip (Flip01→04) 재생. true 면 카드가 옆에서 펼쳐지듯 등장.")]
        private bool playEntranceFlip = true;

        private Action<CardDrawCardView> _onPickClicked;
        private bool _canAfford = true;
        private bool _picked;
        private Coroutine _flipRoutine;
        private Coroutine _entranceRoutine;

        private Color _originalBtnColor;

        private void Awake()
        {
            if (pickButton != null)
            {
                var btnImg = pickButton.GetComponent<Image>();
                if (btnImg != null) _originalBtnColor = btnImg.color;
            }
        }

        public void Init(int entryCost, Action<CardDrawCardView> onPickClicked)
        {
            _onPickClicked = onPickClicked;
            _picked = false;
            if (_flipRoutine != null) { StopCoroutine(_flipRoutine); _flipRoutine = null; }
            if (_entranceRoutine != null) { StopCoroutine(_entranceRoutine); _entranceRoutine = null; }

            if (backRoot != null) backRoot.SetActive(true);
            if (frontRoot != null) frontRoot.SetActive(false);

            if (pickButton != null)
            {
                pickButton.onClick.RemoveAllListeners();
                pickButton.onClick.AddListener(() => _onPickClicked?.Invoke(this));
            }

            // 앞면 텍스트 초기화 + 비활성 (flip 마지막 프레임에서 enable)
            if (frontLabelText != null) { frontLabelText.text = ""; frontLabelText.enabled = false; }
            if (frontReturnText != null) { frontReturnText.text = ""; frontReturnText.enabled = false; }

            // entrance flip 가능 여부
            bool canEntranceFlip = playEntranceFlip
                                   && backImage != null
                                   && backFrames != null && backFrames.Length == 4
                                   && AllNonNull(backFrames);

            if (canEntranceFlip)
            {
                // 시작 sprite = Flip04 (좁음, 측면). 코루틴이 Flip01 (정면) 까지 펼침.
                backImage.sprite = backFrames[3];
                // entrance 도중엔 cost text / button 숨김 — 펼친 후 enable.
                if (backEntryCostText != null) backEntryCostText.enabled = false;
                if (pickButton != null) pickButton.gameObject.SetActive(false);
                _entranceRoutine = StartCoroutine(EntranceFlipCoroutine(entryCost));
            }
            else
            {
                // entrance 없이 즉시 정면 표시 (fallback) — Flip01 = 정면.
                if (backImage != null && backFrames != null && backFrames.Length == 4 && backFrames[0] != null)
                    backImage.sprite = backFrames[0];
                if (backEntryCostText != null)
                {
                    backEntryCostText.text = entryCost.ToString("N0") + " G";
                    backEntryCostText.enabled = true;
                }
                if (pickButton != null) pickButton.gameObject.SetActive(true);
            }

            RefreshButtonState();
        }

        private IEnumerator EntranceFlipCoroutine(int entryCost)
        {
            // Flip04 (좁음) → 03 → 02 → 01 (정면) — 카드가 측면에서 펼쳐지며 등장.
            for (int i = 3; i >= 0; i--)
            {
                backImage.sprite = backFrames[i];
                yield return new WaitForSecondsRealtime(flipFrameDuration);
            }
            // 펼친 후 cost text + button 표시.
            if (backEntryCostText != null)
            {
                backEntryCostText.text = entryCost.ToString("N0") + " G";
                backEntryCostText.enabled = true;
            }
            if (pickButton != null) pickButton.gameObject.SetActive(true);
            _entranceRoutine = null;
            RefreshButtonState();
        }

        public void RevealOutcome(CardDrawOutcomeEntry outcome) => RevealOutcome(outcome, withFlip: true);

        public void RevealOutcome(CardDrawOutcomeEntry outcome, bool withFlip)
        {
            _picked = true;
            if (pickButton != null) pickButton.interactable = false;

            bool canFlip = withFlip
                           && backImage != null && frontImage != null
                           && backFrames != null && backFrames.Length == 4
                           && frontFrames != null && frontFrames.Length == 4
                           && AllNonNull(backFrames) && AllNonNull(frontFrames);

            if (canFlip)
            {
                if (_flipRoutine != null) StopCoroutine(_flipRoutine);
                _flipRoutine = StartCoroutine(FlipCoroutine(outcome));
            }
            else
            {
                // 즉시 swap fallback — sprite ref 누락 시.
                if (backRoot != null) backRoot.SetActive(false);
                if (frontRoot != null) frontRoot.SetActive(true);
                ApplyOutcomeText(outcome, show: true);
            }
            RefreshButtonState();
        }

        public void SetAffordable(bool canAfford)
        {
            _canAfford = canAfford;
            RefreshButtonState();
        }

        public bool IsPicked => _picked;

        private IEnumerator FlipCoroutine(CardDrawOutcomeEntry outcome)
        {
            // back 닫힘 중엔 cost text / button 숨김 (sprite 좁아짐 도중 오버레이 어색).
            if (backEntryCostText != null) backEntryCostText.enabled = false;
            if (pickButton != null) pickButton.gameObject.SetActive(false);

            if (backRoot != null) backRoot.SetActive(true);
            if (frontRoot != null) frontRoot.SetActive(false);

            // Back closing: Flip01 (정면) → 02 → 03 → 04 (좁음) — 정면에서 측면으로 회전.
            for (int i = 0; i < 4; i++)
            {
                backImage.sprite = backFrames[i];
                yield return new WaitForSecondsRealtime(flipFrameDuration);
            }

            // Swap to front
            if (backRoot != null) backRoot.SetActive(false);
            if (frontRoot != null) frontRoot.SetActive(true);

            // outcome 텍스트는 마지막 프레임 (Flip01, 정면) 에서 reveal.
            ApplyOutcomeText(outcome, show: false);

            // Front opening: Flip01 (좁음) → 02 → 03 → 04 (정면) — 측면에서 정면으로 펼쳐짐.
            // (front 는 back 과 인덱스 의미 반대 — Yellow [3] = 정면)
            for (int i = 0; i < 4; i++)
            {
                frontImage.sprite = frontFrames[i];
                if (i == 3)
                {
                    // 마지막 프레임 (정면) = 텍스트 표시
                    if (frontLabelText != null) frontLabelText.enabled = true;
                    if (frontReturnText != null) frontReturnText.enabled = true;
                }
                yield return new WaitForSecondsRealtime(flipFrameDuration);
            }

            if (postFlipHold > 0f) yield return new WaitForSecondsRealtime(postFlipHold);
            _flipRoutine = null;
        }

        private void ApplyOutcomeText(CardDrawOutcomeEntry outcome, bool show)
        {
            if (frontLabelText != null)
            {
                frontLabelText.text = outcome.DisplayLabel;
                frontLabelText.color = outcome.LabelColor;
                frontLabelText.enabled = show;
            }
            if (frontReturnText != null)
            {
                frontReturnText.text = "+" + outcome.ReturnGold.ToString("N0") + " G";
                frontReturnText.color = outcome.LabelColor;
                frontReturnText.enabled = show;
            }
        }

        private void RefreshButtonState()
        {
            if (pickButton == null) return;
            pickButton.interactable = !_picked && _canAfford;

            var btnImg = pickButton.GetComponent<Image>();
            if (btnImg != null)
            {
                if (_picked) btnImg.color = new Color(_originalBtnColor.r * 0.5f, _originalBtnColor.g * 0.5f, _originalBtnColor.b * 0.5f);
                else if (!_canAfford) btnImg.color = new Color(0.35f, 0.35f, 0.35f);
                else btnImg.color = _originalBtnColor;
            }
        }

        private static bool AllNonNull(Sprite[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == null) return false;
            return true;
        }
    }
}

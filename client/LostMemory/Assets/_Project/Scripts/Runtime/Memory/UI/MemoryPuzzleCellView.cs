using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Memory.UI
{
    /// <summary>
    /// 퍼즐 그리드 한 셀. MemoryPuzzleView 가 width*height 개를 동적으로 인스턴스화한다.
    ///
    /// 책임:
    ///   - artwork 의 일부 영역만 잘라낸 Sprite 를 _image 에 표시
    ///   - 잠금/해금 상태에 따라 Material(_Saturation) 값을 조정
    ///   - 다음 해금 순서 셀만 _button.interactable=true 로 클릭 허용
    ///   - 해금 시 Saturation 0→1 코루틴 페이드
    ///
    /// Inspector 연결 항목:
    ///   _image    — 셀 이미지. 그리드 등분된 sprite 가 들어감.
    ///   _button   — 클릭 허용 시 활성. (Image 와 같은 GameObject 권장)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MemoryPuzzleCellView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Image _image;
        [SerializeField] private Button _button;

        [Header("Fade")]
        [Tooltip("해금 시 Saturation 0→1 페이드 시간(초).")]
        [SerializeField] private float _fadeDuration = 0.6f;

        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");

        private MemoryFragmentData _piece;
        private Material _materialInstance;
        private Sprite _ownedSprite;
        private Coroutine _fadeCoroutine;

        public MemoryFragmentData Piece => _piece;

        /// <summary>셀이 클릭됐을 때 발화. MemoryPuzzleView 가 구독.</summary>
        public event Action<MemoryPuzzleCellView> OnClicked;

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(HandleClick);

            DisposeMaterial();
            DisposeSprite();
        }

        /// <summary>
        /// 셀 1개를 초기화한다. MemoryPuzzleView 가 그리드 생성 시 1회 호출.
        /// </summary>
        public void Init(MemoryFragmentData piece, Sprite slicedSprite, Material desaturateMat)
        {
            _piece = piece;

            DisposeSprite();
            _ownedSprite = slicedSprite;

            if (_image != null)
            {
                _image.sprite = slicedSprite;
                _image.enabled = slicedSprite != null;

                DisposeMaterial();
                if (desaturateMat != null)
                {
                    _materialInstance = new Material(desaturateMat);
                    _image.material = _materialInstance;
                }
            }
        }

        /// <summary>
        /// 상태를 갱신한다. unlocked=true 면 컬러, false 면 그레이.
        /// canInteract=true 면 클릭 허용 (다음 해금 순서 셀에만 권장).
        /// animate=true 면 페이드, false 면 즉시.
        /// </summary>
        public void SetState(bool unlocked, bool canInteract, bool animate)
        {
            float target = unlocked ? 1f : 0f;

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            if (animate && unlocked && isActiveAndEnabled)
                _fadeCoroutine = StartCoroutine(FadeSaturation(target));
            else
                SetSaturation(target);

            if (_button != null)
                _button.interactable = canInteract;
        }

        private IEnumerator FadeSaturation(float target)
        {
            float start = GetSaturation();
            float t = 0f;
            float dur = Mathf.Max(0.01f, _fadeDuration);

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                SetSaturation(Mathf.Lerp(start, target, k));
                yield return null;
            }

            SetSaturation(target);
            _fadeCoroutine = null;
        }

        private void SetSaturation(float v)
        {
            if (_materialInstance != null)
                _materialInstance.SetFloat(SaturationId, v);
        }

        private float GetSaturation()
        {
            return _materialInstance != null ? _materialInstance.GetFloat(SaturationId) : 1f;
        }

        private void HandleClick()
        {
            OnClicked?.Invoke(this);
        }

        private void DisposeMaterial()
        {
            if (_materialInstance != null)
            {
                Destroy(_materialInstance);
                _materialInstance = null;
            }
        }

        private void DisposeSprite()
        {
            if (_ownedSprite != null)
            {
                Destroy(_ownedSprite);
                _ownedSprite = null;
            }
        }
    }
}

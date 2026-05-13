using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// 키보드 힌트 패널에서 키 입력 시 해당 버튼 애니메이션을 재생한다.
    ///
    /// Inspector 연결 항목:
    ///   _wButton, _aButton, _sButton, _dButton — 이동 키 Animator
    ///   _fButton — 상호작용 키 Animator
    ///   _toggleButtonImage — 토글 버튼 Image (패널 열림/닫힘 스프라이트 교체용)
    ///   _panelOpenSprite   — 패널이 열려있을 때 버튼 스프라이트
    ///   _panelClosedSprite — 패널이 닫혀있을 때 버튼 스프라이트
    /// </summary>
    [AddComponentMenu("Lost Memory/UI/Keyboard Hint Panel Controller")]
    public sealed class KeyboardHintPanelController : MonoBehaviour
    {
        [Header("이동 키")]
        [SerializeField] private Animator _wButton;
        [SerializeField] private Animator _aButton;
        [SerializeField] private Animator _sButton;
        [SerializeField] private Animator _dButton;

        [Header("상호작용 키")]
        [SerializeField] private Animator _fButton;

        [Header("애니메이션 상태 이름")]
        [SerializeField] private string _stateName = "Button01a";

        [Header("토글 버튼")]
        [SerializeField] private Image _toggleButtonImage;
        [SerializeField] private Sprite _panelOpenSprite;
        [SerializeField] private Sprite _panelClosedSprite;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.W)) Play(_wButton);
            if (Input.GetKeyDown(KeyCode.A)) Play(_aButton);
            if (Input.GetKeyDown(KeyCode.S)) Play(_sButton);
            if (Input.GetKeyDown(KeyCode.D)) Play(_dButton);
            if (Input.GetKeyDown(KeyCode.F)) Play(_fButton);
        }

        private void Play(Animator animator)
        {
            if (animator == null) return;
            // 처음부터 다시 재생 (0번 레이어, 0f = 첫 프레임)
            animator.Play("Button01a", 0, 0f);
        }

        /// <summary>패널 표시/숨김을 토글한다.</summary>
        public void TogglePanel()
        {
            bool willBeActive = !gameObject.activeSelf;
            gameObject.SetActive(willBeActive);
            UpdateToggleSprite(willBeActive);
        }

        private void UpdateToggleSprite(bool isOpen)
        {
            if (_toggleButtonImage == null) return;
            _toggleButtonImage.sprite = isOpen ? _panelOpenSprite : _panelClosedSprite;
        }
    }
}

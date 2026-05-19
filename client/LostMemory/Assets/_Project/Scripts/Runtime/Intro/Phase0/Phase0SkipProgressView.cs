using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Intro.Phase0
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Intro/Phase0 Skip Progress View")]
    public sealed class Phase0SkipProgressView : MonoBehaviour
    {
        [SerializeField] private Phase0IntroSequenceController controller;
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text label;

        private bool _warnedNull;

        private void Awake()
        {
            if (fillImage != null) fillImage.fillAmount = 0f;
        }

        private void Update()
        {
            if (controller == null)
            {
                if (!_warnedNull)
                {
                    Debug.LogWarning("[Phase0SkipProgressView] controller is NULL — connect Phase0IntroRoot to the Controller field.", this);
                    _warnedNull = true;
                }
                return;
            }

            if (fillImage != null)
            {
                fillImage.fillAmount = controller.SkipProgress;
            }
        }
    }
}

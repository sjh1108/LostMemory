using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI
{
    public class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;   // 달리기 스프라이트 배열
        [SerializeField] private float _fps = 10f;   // 초당 프레임 수

        private Image _image;
        private float _timer;
        private int _currentFrame;

        void Start()
        {
            _image = GetComponent<Image>();
        }

        void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= 1f / _fps)
            {
                _timer = 0f;
                _currentFrame = (_currentFrame + 1) % _frames.Length;
                _image.sprite = _frames[_currentFrame];
            }
        }
    }
}
using UnityEngine;

namespace LostMemory.UI
{
    public class InfiniteScroll : MonoBehaviour
    {
        [SerializeField] private float _speed = 100f;
        [SerializeField] private float _imageWidth = 1920f;

        private RectTransform _childA, _childB;

        void Start()
        {
            _childA = transform.GetChild(0).GetComponent<RectTransform>();
            _childB = transform.GetChild(1).GetComponent<RectTransform>();
        }

        void Update()
        {
            _childA.anchoredPosition += Vector2.left * _speed * Time.deltaTime;
            _childB.anchoredPosition += Vector2.left * _speed * Time.deltaTime;

            if (_childA.anchoredPosition.x <= -_imageWidth)
                _childA.anchoredPosition = _childB.anchoredPosition + Vector2.right * _imageWidth;

            if (_childB.anchoredPosition.x <= -_imageWidth)
                _childB.anchoredPosition = _childA.anchoredPosition + Vector2.right * _imageWidth;
        }
    }
}